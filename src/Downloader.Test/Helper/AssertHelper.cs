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
            Assert.AreEqual(source.Id, destination.Id);
            Assert.AreEqual(source.Start, destination.Start);
            Assert.AreEqual(source.End, destination.End);
            Assert.AreEqual(source.Length, destination.Length);
            Assert.AreEqual(source.Position, destination.Position);
            Assert.AreEqual(source.Timeout, destination.Timeout);
            Assert.AreEqual(source.MaxTryAgainOnFailover, destination.MaxTryAgainOnFailover);
            Assert.AreEqual(source.Storage.GetLength(), destination.Storage.GetLength());

            var sourceStreamReader = source.Storage.OpenRead();
            var destinationStreamReader = destination.Storage.OpenRead();
            for (int i = 0; i < source.Storage.GetLength(); i++)
            {
                Assert.AreEqual(sourceStreamReader.ReadByte(), destinationStreamReader.ReadByte());
            }
        }
    }
}
