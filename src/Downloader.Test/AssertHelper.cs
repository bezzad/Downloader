using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    public static class AssertHelper
    {
        public static void DoesNotThrow<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                Assert.Fail("Expected no {0} to be thrown", typeof(T).Name);
            }
        }
    }
}
