namespace Downloader
{
    public enum FileExistPolicy
    {
        Ignore = 0,
        Delete = 1,
        Exception = 2,
        // Resume = 3 // TODO: developing on future to resume downloading with filename.ext.download files
    }
}