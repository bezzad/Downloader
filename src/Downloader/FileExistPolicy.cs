namespace Downloader
{
    public enum FileExistPolicy
    {
        Ignore = 0,
        Delete = 1,
        Exception = 2,
        // TODO: developing on future to Resume download from previews position if the file downloaded before this and file continuable
        // ResumeDownloadIfCan = 3 
    }
}