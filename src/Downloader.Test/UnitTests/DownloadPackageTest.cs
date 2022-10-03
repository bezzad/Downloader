using Downloader.DummyHttpServer;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public abstract class DownloadPackageTest
    {
        protected DownloadConfiguration Config { get; set; }
        protected DownloadPackage Package { get; set; }

        [TestInitialize]
        public virtual void Initial()
        {
            Config = new DownloadConfiguration() { ChunkCount = 8 };
            var testData = DummyData.GenerateOrderedBytes(DummyFileHelper.FileSize16Kb);
            Package.BuildStorage();
            new ChunkHub(Config).SetFileChunks(Package);
            Package.Storage.WriteAsync(0, testData, DummyFileHelper.FileSize16Kb);
            Package.Storage.Flush();
        }

        [TestCleanup]
        public virtual void Cleanup()
        {
            Package?.Clear();
            Package?.Storage?.Dispose();
        }

        [TestMethod]
        public void PackageSerializationTest()
        {
            // act
            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(Package);
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<DownloadPackage>(serialized);

            // assert
            PackagesAreEqual(Package, deserialized);

            Package.Clear();
        }

        [TestMethod]
        public void PackageBinarySerializationTest()
        {
            // arrange
            IFormatter formatter = new BinaryFormatter();
            using var serializedStream = new MemoryStream();

            // act
            formatter.Serialize(serializedStream, Package);
            serializedStream.Flush();
            serializedStream.Seek(0, SeekOrigin.Begin);
            var deserialized = formatter.Deserialize(serializedStream) as DownloadPackage;

            // assert
            PackagesAreEqual(Package, deserialized);

            Package.Clear();
        }

        private void PackagesAreEqual(DownloadPackage source, DownloadPackage destination)
        {
            Assert.IsNotNull(source);
            Assert.IsNotNull(destination);
            Assert.IsNotNull(source.Chunks);
            Assert.IsNotNull(destination.Chunks);
            Assert.AreEqual(source.FileName, destination.FileName);
            Assert.AreEqual(source.ReceivedBytesSize, destination.ReceivedBytesSize);
            Assert.AreEqual(source.Address, destination.Address);
            Assert.AreEqual(source.TotalFileSize, destination.TotalFileSize);
            Assert.AreEqual(source.IsSaving, destination.IsSaving);
            Assert.AreEqual(source.IsSaveComplete, destination.IsSaveComplete);
            Assert.AreEqual(source.SaveProgress, destination.SaveProgress);
            Assert.AreEqual(source.Chunks?.Length, destination.Chunks?.Length);
            Assert.AreEqual(source.IsSupportDownloadInRange, destination.IsSupportDownloadInRange);
            Assert.AreEqual(source.InMemoryStream, destination.InMemoryStream);
            Assert.AreEqual(source.Storage.Path, destination.Storage.Path);
            Assert.AreEqual(source.Storage.Length, destination.Storage.Length);
            Assert.AreEqual(source.Storage.Data, destination.Storage.Data);

            for (int i = 0; i < source.Chunks.Length; i++)
            {
                AssertHelper.AreEquals(source.Chunks[i], destination.Chunks[i]);
            }
        }

        [TestMethod]
        public void ClearChunksTest()
        {
            // act
            Package.Clear();

            // assert
            Assert.IsNull(Package.Chunks);
        }

        [TestMethod]
        public void ClearPackageTest()
        {
            // act
            Package.Clear();

            // assert
            Assert.AreEqual(0, Package.ReceivedBytesSize);
        }

        [TestMethod]
        public void PackageValidateTest()
        {
            // arrange
            var actualPosition = Package.Chunks[0].Length;

            // act
            Package.Validate();

            // assert
            Assert.AreEqual(actualPosition, Package.Chunks[0].Position);
        }

        [TestMethod]
        public void TestPackageValidateWhenDoesNotSupportDownloadInRange()
        {
            // arrange
            Package.Chunks[0].Position = 1000;
            Package.IsSupportDownloadInRange = false;

            // act
            Package.Validate();

            // assert
            Assert.AreEqual(0, Package.Chunks[0].Position);
        }
    }
}
