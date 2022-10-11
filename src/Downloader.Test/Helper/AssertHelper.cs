using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.Helper
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

        public static void AreEquals(Chunk source, Chunk destination)
        {
            Assert.IsNotNull(source);
            Assert.IsNotNull(destination);

            foreach (var prop in typeof(Chunk).GetProperties())
                Assert.AreEqual(prop.GetValue(source), prop.GetValue(destination));
        }
    }
}
