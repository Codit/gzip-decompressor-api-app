using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace TomKerkhove.ApiApps.Decompressor.Azure
{
    /// <summary>
    /// Azure Blob Storage library
    /// </summary>
    public static class BlobStorage
    {
        /// <summary>
        /// Uploads a data to Blob Storage
        /// </summary>
        /// <param name="storageAccountName">Name of the storage account</param>
        /// <param name="storageAccountKey">Key to access the storage account</param>
        /// <param name="containerName">Name of the destionation container</param>
        /// <param name="blobName">Name of the blob</param>
        /// <param name="contentStream">Stream containing the blob contents</param>
        public static async Task UploadAsync(string storageAccountName, string storageAccountKey, string containerName, string blobName, Stream contentStream)
        {
            if (contentStream.CanRead == false)
            {
                throw new ArgumentException("Contents stream is not readable.");
            }

            if (contentStream.Position > 0&& contentStream.CanSeek)
            {
                contentStream.Seek(0, SeekOrigin.Begin);
            }

            // Create client
            StorageCredentials storageCreds = new StorageCredentials(storageAccountName, storageAccountKey);
            CloudStorageAccount account = new CloudStorageAccount(storageCreds, useHttps: true);
            var blobClient = account.CreateCloudBlobClient();

            // Get a reference to the container
            var container = blobClient.GetContainerReference(containerName.ToLower());
            await container.CreateIfNotExistsAsync();

            // Get a reference to the blob
            var blob = container.GetBlockBlobReference(blobName);
            await blob.UploadFromStreamAsync(contentStream);
        }
    }
}