using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using TomKerkhove.ApiApps.Decompressor.Exceptions;

namespace TomKerkhove.ApiApps.Decompressor.Compression
{
    /// <summary>
    /// Gzip compression library
    /// </summary>
    public static class Gzip
    {
        /// <summary>
        /// Decompresses a stream
        /// </summary>
        /// <param name="compressedStream">Compressed stream in GZIP format</param>
        /// <param name="destionationStream">Destionation stream that will contain decompressed data</param>
        public static async Task DecompressToAsync(Stream compressedStream, Stream destionationStream)
        {
            try
            {
                // Seek to origin, if possible
                if (compressedStream.CanSeek && compressedStream.Position > 0)
                {
                    compressedStream.Seek(0, SeekOrigin.Begin);
                }
                
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    await gzipStream.CopyToAsync(destionationStream);

                    // Seek back to the origin of the unzipped stream
                    destionationStream.Seek(0, SeekOrigin.Begin);
                }
            }
            catch (Exception ex)
            {
                // Wrap in a custom exception to clearly state that it's the decompression that failed
                throw new DecompressionException("Failed to decompress the stream", ex);
            }
        }
    }
}