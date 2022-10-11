using System;
using System.Runtime.Serialization;

namespace Downloader.Test.HelperTests
{
    [Serializable]
    internal class IoException : Exception
    {
        public IoException()
        {
        }

        public IoException(string message) : base(message)
        {
        }

        public IoException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected IoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}