using System;

namespace TomKerkhove.ApiApps.Decompressor.Exceptions
{
    [System.Serializable]
    public class DecompressionException : Exception
    {
        public DecompressionException() { }
        public DecompressionException(string message) : base(message) { }
        public DecompressionException(string message, Exception inner) : base(message, inner) { }
        protected DecompressionException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}