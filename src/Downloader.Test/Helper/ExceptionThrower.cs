namespace Downloader.Test.Helper;

public static class ExceptionThrower
{
    public static Exception GetException()
    {
        try
        {
            ThrowException();
            return new Exception(); // This code will never run.
        }
        catch (Exception e)
        {
            return e;
        }
    }
    public static Exception GetWebException()
    {
        try
        {
            ThrowWebException();
            return new WebException(); // This code will never run.
        }
        catch (Exception e)
        {
            return e;
        }
    }
    private static void ThrowWebException()
    {
        try
        {
            ThrowIoException();
        }
        catch (Exception e)
        {
            throw new WebException("High level exception", e);
        }
    }
    private static void ThrowException()
    {
        try
        {
            ThrowIoException();
        }
        catch (Exception e)
        {
            throw new Exception("High level exception", e);
        }
    }
    private static void ThrowIoException()
    {
        try
        {
            ThrowHttpRequestException();
        }
        catch (Exception e)
        {
            throw new IOException("Mid level exception", e);
        }
    }
    private static void ThrowHttpRequestException()
    {
        throw new HttpRequestException("Low level exception");
    }
}
