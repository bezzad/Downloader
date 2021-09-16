namespace Downloader.Exceptions
{

    [System.Serializable]
    public class DownloadFactoryException : System.Exception
    {
        public DownloadFactoryException() { }
        public DownloadFactoryException(string message) : base(message) { }
        public DownloadFactoryException(string message, System.Exception inner) : base(message, inner) { }
        protected DownloadFactoryException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
