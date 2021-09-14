using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Downloader.Test
{
    [TestClass]
    public abstract class DownloadPackageTest
    {
        protected DownloadConfiguration Configuration { get; set; }
        private DownloadPackage _package;

        [TestInitialize]
        public virtual void Initial()
        {
            var testData = DummyData.GenerateOrderedBytes(DownloadTestHelper.FileSize16Kb);
            _package = new DownloadPackage() {
                FileName = DownloadTestHelper.File16KbName,
                Address = DownloadTestHelper.File16KbUrl,
                Chunks = new ChunkHub(Configuration).ChunkFile(DownloadTestHelper.FileSize16Kb, 8),
                TotalFileSize = DownloadTestHelper.FileSize16Kb
            };

            foreach (var chunk in _package.Chunks)
            {
                chunk.Storage.WriteAsync(testData, (int)chunk.Start, (int)chunk.Length);
            }
        }

        [TestMethod]
        public void PackageSerializationTest()
        {
            // arrange
            var settings = new Newtonsoft.Json.JsonSerializerSettings();
            settings.Converters.Add(new StorageConverter());

            // act
            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(_package);
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<DownloadPackage>(serialized, settings);

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
    }
}
