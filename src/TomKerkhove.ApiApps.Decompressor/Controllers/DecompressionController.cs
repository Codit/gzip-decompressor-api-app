using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

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
        /// <param name="uri">Uri of the compressed file to download</param>
        /// <param name="storageAccountName">Name Azure Storage Account</param>
        /// <param name="storageAccountKey">Key for Azure Storage Account</param>
        /// <param name="containerName">Name of the container</param>
        /// <param name="blobName">Name of the blob</param>
        [HttpGet]
        public async Task<IHttpActionResult> Get(string uri, string storageAccountName, string storageAccountKey,
            string containerName, string blobName)
        {
            var compressedStream = await DownloadFileAsync(uri);

            var unzippedStream = await UnzipStream(compressedStream);

            await StoreStream(storageAccountName, storageAccountKey, containerName, blobName, unzippedStream);

            return Ok();
        }

        private static async Task<Stream> DownloadFileAsync(string uri)
        {
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(uri);

            var responseStream = await response.Content.ReadAsStreamAsync();
            return responseStream;
        }

        private static async Task<Stream> UnzipStream(Stream responseStream)
        {
            var unzippedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(responseStream, CompressionMode.Decompress))
            {
                await gzipStream.CopyToAsync(unzippedStream);
                unzippedStream.Seek(0, SeekOrigin.Begin);
            }
            return unzippedStream;
        }

        private async Task StoreStream(string storageAccountName, string storageAccountKey, string containerName, string blobName, Stream unzippedStream)
        {
            StorageCredentials storageCreds = new StorageCredentials(storageAccountName, storageAccountKey);
            CloudStorageAccount account = new CloudStorageAccount(storageCreds, useHttps: true);
            var blobClient = account.CreateCloudBlobClient();

            var container = blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference(blobName);
            await blob.UploadFromStreamAsync(unzippedStream);

            unzippedStream.Dispose();
        }
    }
}
