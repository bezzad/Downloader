namespace Downloader.Test.Helper
{
    public static class DummyFileHelper
    {
        static int port = 3333;

        static DummyFileHelper()
        {
            DummyHttpServer.HttpServer.Run(port);
        }

        public static string GetFileUrl(int size)
        {
            return $"http://localhost:{port}/dummyfile/file/size/{size}";
        }

        public static string GetFileWithNameUrl(string filename, int size)
        {
            return $"http://localhost:{port}/dummyfile/file/{filename}?size={size}";
        }

        public static string GetFileWithoutHeaderUrl(string filename, int size)
        {
            return $"http://localhost:{port}/dummyfile/file/{filename}?size={size}&noheader=true";
        }

        public static string GetFileWithContentDispositionUrl(string filename, int size)
        {
            return $"http://localhost:{port}/dummyfile/file/{filename}/size/{size}";
        }
    }
}
