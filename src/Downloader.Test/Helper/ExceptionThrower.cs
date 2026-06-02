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
            throw new WebException("High level exception", e, WebExceptionStatus.Timeout, null);
        }
    }
    // A genuinely non-transient error chain (no network/IO types), so IsMomentumError() must be
    // false. Using IOException/HttpRequestException here would be classified as transient.
    private static void ThrowException()
    {
        try
        {
            ThrowInvalidOperationException();
        }
        catch (Exception e)
        {
            throw new Exception("High level exception", e);
        }
    }
    private static void ThrowInvalidOperationException()
    {
        try
        {
            ThrowFormatException();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Mid level exception", e);
        }
    }
    private static void ThrowFormatException()
    {
        throw new FormatException("Low level exception");
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
