using System;

namespace Downloader
{
    internal static class ExceptionHelper
    {
        public static bool HasSource(this Exception exp, string source)
        {
            Exception innerException = exp;
            while (innerException != null)
            {
                if (string.Equals(innerException.Source, source, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                innerException = innerException.InnerException;
            }

            return false;
        }
    }
}
