using Downloader.DummyHttpServer;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public abstract class DownloadPackageTest
    {
        protected DownloadConfiguration Config { get; set; }
        private DownloadPackage _package;

        [TestInitialize]
        public virtual async Task Initial()
        {
            var testData = DummyData.GenerateOrderedBytes(DummyFileHelper.FileSize16Kb);
            Config.ChunkCount = 8;
            _package = new DownloadPackage() {
                FileName = DummyFileHelper.SampleFile16KbName,
                Address = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb),
                Chunks = new ChunkHub(Config).ChunkFile(DummyFileHelper.FileSize16Kb),
                TotalFileSize = DummyFileHelper.FileSize16Kb
            };

            foreach (var chunk in _package.Chunks)
            {
                await chunk.Storage.WriteAsync(testData, (int)chunk.Start, (int)chunk.Length, new CancellationToken())
                    .ConfigureAwait(false);
            }
        }

        [TestMethod]
        public void PackageSerializationTest()
        {
            // act
            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(_package);
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<DownloadPackage>(serialized);

            // assert
            PackagesAreEqual(_package, deserialized);

            _package.Clear();
        }

        [TestMethod]
        public void PackageBinarySerializationTest()
        {
            // arrange
            IFormatter formatter = new BinaryFormatter();
            using var serializedStream = new MemoryStream();

            // act
            formatter.Serialize(serializedStream, _package);
            serializedStream.Flush();
            serializedStream.Seek(0, SeekOrigin.Begin);
            var deserialized = formatter.Deserialize(serializedStream) as DownloadPackage;

            // assert
            PackagesAreEqual(_package, deserialized);

            _package.Clear();
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

            for (int i = 0; i < source.Chunks.Length; i++)
            {
                AssertHelper.AreEquals(source.Chunks[i], destination.Chunks[i]);
            }
        }

        [TestMethod]
        public void ClearChunksTest()
        {
            // act
            _package.Clear();

            // assert
            Assert.IsNull(_package.Chunks);
        }

        [TestMethod]
        public void ClearPackageTest()
        {
            // act
            _package.Clear();

            // assert
            Assert.AreEqual(0, _package.ReceivedBytesSize);
        }

        [TestMethod]
        public void PackageValidateTest()
        {
            // arrange
            var actualPosition = _package.Chunks[0].Length;

            // act
            _package.Validate();

            // assert
            Assert.AreEqual(actualPosition, _package.Chunks[0].Position);
        }

        [TestMethod]
        public void TestPackageValidateWhenDoesNotSupportDownloadInRange()
        {
            // arrange
            _package.Chunks[0].Position = 1000;
            _package.IsSupportDownloadInRange = false;

            // act
            _package.Validate();

            // assert
            Assert.AreEqual(0, _package.Chunks[0].Position);
        }
    }
}
