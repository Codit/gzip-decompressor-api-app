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
            var container = blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();

            // Get a reference to the blob
            var blob = container.GetBlockBlobReference(blobName);
            await blob.UploadFromStreamAsync(decompressedStream);

            // Cleanup
            decompressedStream.Dispose();
        }
    }
}
