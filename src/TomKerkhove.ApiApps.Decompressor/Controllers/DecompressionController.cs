using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Swashbuckle.Swagger.Annotations;
using TomKerkhove.ApiApps.Decompressor.Exceptions;

namespace TomKerkhove.ApiApps.Decompressor.Controllers
{
    /// <summary>
    /// Decompression
    /// </summary>
    public class DecompressionController : ApiController
    {
        /// <summary>
        /// Downloads, decompresses and stores a file to Azure Blob Storage
        /// </summary>
        /// <remarks>
        /// Downloads, decompresses and stores a file to Azure Blob Storage
        /// </remarks>
        /// <param name="uri">Uri of the compressed file to download</param>
        /// <param name="storageAccountName">Name Azure Storage Account</param>
        /// <param name="storageAccountKey">Key for Azure Storage Account</param>
        /// <param name="containerName">Name of the container</param>
        /// <param name="blobName">Name of the blob</param>
        /// <response code="200">The file was successfully processed</response>
        /// <response code="500">We were unable to process the file</response>
        [HttpGet]
        public async Task<IHttpActionResult> Get(string uri, string storageAccountName, string storageAccountKey,
            string containerName, string blobName)
        {
            try
            {
                var validationResult = ValidateInput(uri, storageAccountName, storageAccountKey, containerName, blobName);
                if (validationResult != null)
                {
                    return validationResult;
                }

                // Download the file
                var compressedStream = await DownloadFileAsync(uri);
                if (compressedStream == Stream.Null)
                {
                    return NotFound();
                }

                // Decompress the stream
                var uncompressedStream = await DecompressStreamAsync(compressedStream);

                await
                    StoreOnBlobStorageAsync(storageAccountName, storageAccountKey, containerName, blobName,
                        uncompressedStream);

                return Ok();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private IHttpActionResult ValidateInput(string uri, string storageAccountName, string storageAccountKey, string containerName, string blobName)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return BadRequest($"Invalid {nameof(uri)}.");
            }

            /*
             * TODO: Validate storage account name against:
             * 1. Account names must be from 3 through 24 characters long
             * 2. Account names must alphanumeric
             * Source: https://blogs.msdn.microsoft.com/jmstall/2014/06/12/azure-storage-naming-rules/
             */

            if (string.IsNullOrWhiteSpace(storageAccountName))
            {
                return BadRequest($"Invalid {nameof(storageAccountName)}.");
            }

            if (string.IsNullOrWhiteSpace(storageAccountKey))
            {
                return BadRequest($"Invalid {nameof(storageAccountKey)}.");
            }

            /*
             * TODO: Validate container name against:
             * 1. Container names must start with a letter or number, and can contain only letters, numbers, and the dash (-) character.
             * 2. Every dash (-) character must be immediately preceded and followed by a letter or number; consecutive dashes are not permitted in container names.
             * 3. All letters in a container name must be lowercase.
             * 4. Container names must be from 3 through 63 characters long.
             */

            if (string.IsNullOrWhiteSpace(containerName))
            {
                return BadRequest($"Invalid {nameof(containerName)}.");
            }

            if (containerName.Length < 3)
            {
                return BadRequest($"Container name '{containerName}' is too short.");
            }

            if (containerName.Length > 63)
            {
                return BadRequest($"Container name '{containerName}' is too long.");
            }

            /*
             * TODO: Validate blob name against:
             * 1. A blob name can contain any combination of characters.
             * 2. A blob name must be at least one character long and cannot be more than 1,024 characters long.
             * 3. Blob names are case-sensitive.
             * 4. Reserved URL characters must be properly escaped.
             * 5. The number of path segments comprising the blob name cannot exceed 254. A path segment is the string between consecutive delimiter characters (e.g., the forward slash '/') that corresponds to the name of a virtual directory.
             */
            if (string.IsNullOrWhiteSpace(blobName))
            {
                return BadRequest($"Invalid {nameof(blobName)}.");
            }

            return null;
        }

        private static async Task<Stream> DownloadFileAsync(string uri)
        {
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(uri);

            return (response.IsSuccessStatusCode) ? await response.Content.ReadAsStreamAsync() : Stream.Null;
        }

        private static async Task<Stream> DecompressStreamAsync(Stream compressedStream)
        {
            try
            {
                // Seek to origin, if possible
                if (compressedStream.CanSeek && compressedStream.Position > 0)
                {
                    compressedStream.Seek(0, SeekOrigin.Begin);
                }

                var unzippedStream = new MemoryStream();
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    await gzipStream.CopyToAsync(unzippedStream);

                    // Seek back to the origin of the unzipped stream
                    unzippedStream.Seek(0, SeekOrigin.Begin);
                }

                return unzippedStream;
            }
            catch (Exception ex)
            {
                // Wrap in a custom exception to clearly state that it's the decompression that failed
                throw new DecompressionException("Failed to decompress the stream", ex);
            }
        }

        private async Task StoreOnBlobStorageAsync(string storageAccountName, string storageAccountKey, string containerName, string blobName, Stream decompressedStream)
        {
            // Create client
            StorageCredentials storageCreds = new StorageCredentials(storageAccountName, storageAccountKey);
            CloudStorageAccount account = new CloudStorageAccount(storageCreds, useHttps: true);
            var blobClient = account.CreateCloudBlobClient();

            // Get a reference to the container
            var container = blobClient.GetContainerReference(containerName.ToLower());
            await container.CreateIfNotExistsAsync();

            // Get a reference to the blob
            var blob = container.GetBlockBlobReference(blobName);
            await blob.UploadFromStreamAsync(decompressedStream);

            // Cleanup
            decompressedStream.Dispose();
        }
    }
}

