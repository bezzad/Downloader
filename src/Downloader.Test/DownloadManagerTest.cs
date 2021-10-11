using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Downloader.Test
{
    //[TestClass]
    public class DownloadManagerTest
    {
        private IDownloadRequest[] _successDownloadRequest;
        private IDownloadRequest[] _emptyDownloadRequest;

        [TestInitialize]
        public void Initial()
        {
            _successDownloadRequest = new[] {
                new DownloadRequest() { Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), Path = Path.GetTempPath(), DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(102400, 1024, 1) },
                new DownloadRequest() { Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), Path = Path.GetTempPath(), DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(204800, 1024, 1) },
                new DownloadRequest() { Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), Path = Path.GetTempPath(), DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(102400, 512, 1) },
                new DownloadRequest() { Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), Path = Path.GetTempPath(), DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(204800, 512, 1) },
                new DownloadRequest() { Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), Path = Path.GetTempPath(), DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(102400, 2048, 1) },
                new DownloadRequest() { Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), Path = Path.GetTempPath(), DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(204800, 2048, 1) }
            };

            _emptyDownloadRequest = new[] {
                new DownloadRequest(),
                new DownloadRequest(),
                new DownloadRequest(),
                new DownloadRequest(),
                new DownloadRequest(),
                new DownloadRequest()
            };
        }

        private static IDownloadRequest[] GetDownloadServices(int count, int delayPerProcess, bool isCancelled, bool hasError)
        {
            var services = new List<IDownloadRequest>();
            for (var i = 0; i < count; i++)
            {
                var totalSize = (i+1) * 10240;
                var sizeOfProgress = totalSize / 10;
                var download = new DownloadRequest() {
                    Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb),
                    Path = Path.GetTempPath()
                };
                services.Add(download);

                if (isCancelled)
                {
                    download.DownloadService = 
                        DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(totalSize, sizeOfProgress, delayPerProcess);
                }
                else if (hasError)
                {
                    download.DownloadService = 
                        DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(totalSize, sizeOfProgress, delayPerProcess);
                }
                else
                {
                    download.DownloadService = 
                        DownloadServiceMockHelper.GetSuccessDownloadService(totalSize, sizeOfProgress, delayPerProcess);
                }
            }

            return services.ToArray();
        }

        [TestMethod]
        public void TestMaxConcurrentDownloadsDegree()
        {
            // arrange
            var maxNumber1 = 1;
            var maxNumber2 = 100;

            // act
            var downloadManager1 = new DownloadManager(new DownloadConfiguration(), maxNumber1);
            var downloadManager2 = new DownloadManager(new DownloadConfiguration(), maxNumber2);

            // assert
            Assert.AreEqual(maxNumber1, downloadManager1.MaxConcurrentDownloadsDegree);
            Assert.AreEqual(maxNumber2, downloadManager2.MaxConcurrentDownloadsDegree);
        }

        [TestMethod]
        public void TestMaxConcurrentDownloadsDegreeWhenLessThan1()
        {
            // arrange
            var maxNumber1 = 0;
            var maxNumber2 = -5;

            // act
            Action act1 = ()=> new DownloadManager(new DownloadConfiguration(), maxNumber1);
            Action act2 = ()=> new DownloadManager(new DownloadConfiguration(), maxNumber2);

            // assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(act1);
            Assert.ThrowsException<ArgumentOutOfRangeException>(act2);
        }

        [TestMethod]
        public void TestNumberOfDownloadsInProgress()
        {
            // arrange
            var maxDownload = 1;
            var isNumberOfDownloadsMoreThanMaxDownloads = false;
            var downloadManager = new DownloadManager(new DownloadConfiguration(), maxDownload);
            downloadManager.DownloadStarted += (s, e) => {
                if (downloadManager.NumberOfDownloadsInProgress > maxDownload)
                    isNumberOfDownloadsMoreThanMaxDownloads = true;
            };

            // act
            downloadManager.DownloadTaskAsync(_successDownloadRequest).Wait();

            // assert
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
            Assert.IsFalse(isNumberOfDownloadsMoreThanMaxDownloads);
        }

        [TestMethod]
        public void TestNullPathException()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            Action act = () => downloadManager.DownloadTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), null).Wait();

            // assert
            Assert.ThrowsException<ArgumentNullException>(act);
        }

        [TestMethod]
        public void TestNullUrlException()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            Action act = () => downloadManager.DownloadTaskAsync(null, Path.GetTempPath()).Wait();

            // assert
            Assert.ThrowsException<ArgumentNullException>(act);
        }

        [TestMethod]
        public void TestGetDownloadRequests()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            downloadManager.DownloadTaskAsync(_emptyDownloadRequest).Wait();
            var requests = downloadManager.GetDownloadRequests();

            // assert
            Assert.AreEqual(_successDownloadRequest.Length, requests.Count);
            for (var i = 0; i<requests.Count; i++)
            {
                Assert.AreEqual(_successDownloadRequest[i].Url, requests[i].Url);
                Assert.AreEqual(_successDownloadRequest[i].Path, requests[i].Path);
                Assert.IsNotNull(requests[i].DownloadService);
            }
        }

        [TestMethod]
        public void TestCancelAsync()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);
            var slowlyDownloadService = DownloadServiceMockHelper.GetSpecialDownloadService(102400, 1024, 100, 100, false, null);
            var request = new DownloadRequest() {
                Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb),
                Path = Path.GetTempPath(),
                DownloadService = slowlyDownloadService
            };

            // act
            downloadManager.DownloadAsync(request);
            downloadManager.CancelAsync(request);

            // assert
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
            Assert.IsFalse(request.IsSaving);
            Assert.IsFalse(request.IsSaveComplete);
        }

        [TestMethod]
        public void TestCancelAllAsync()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 3);

            // act
            downloadManager.DownloadAsync(_successDownloadRequest);
            downloadManager.CancelAllAsync();

            // assert
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
            foreach (var request in _successDownloadRequest)
            {
                Assert.IsFalse(request.IsSaving);
                Assert.IsFalse(request.IsSaveComplete);
            }
        }

        [TestMethod]
        public void TestClear()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 2);

            // act
            downloadManager.DownloadTaskAsync(_successDownloadRequest).Wait();
            downloadManager.Clear();
            var requests = downloadManager.GetDownloadRequests();

            // assert
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
            Assert.AreEqual(0, requests.Count);
        }

        [TestMethod]
        public void TestAddNewDownloadEvent()
        {
            // arrange
            var addingEventCount = 0;
            var areSaving = true;
            var areSaveComplete = false;
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);
            downloadManager.AddNewDownload += (s, e) => {
                addingEventCount++;
                areSaving &= e.IsSaving;
                areSaveComplete |= e.IsSaveComplete;
            };

            // act
            downloadManager.DownloadTaskAsync(_successDownloadRequest).Wait();

            // assert
            Assert.AreEqual(_successDownloadRequest.Length, addingEventCount);
            Assert.IsTrue(areSaving);
            Assert.IsFalse(areSaveComplete);
        }

        [TestMethod]
        public void TestDownloadStartedEvent()
        {
            // arrange
            var eventsChangingState = new DownloadServiceEventsState(_successDownloadRequest[0].DownloadService);
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            downloadManager.DownloadTaskAsync(_successDownloadRequest[0]).Wait();

            // assert
            Assert.AreEqual(_successDownloadRequest[0].Path, eventsChangingState.ActualFileName);
            Assert.IsTrue(eventsChangingState.DownloadStarted);
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
        }

        [TestMethod]
        public void TestDownloadCompletedEventWhenSuccessfulCompleted()
        {
            // arrange
            var eventsChangingState = new DownloadServiceEventsState(_successDownloadRequest[0].DownloadService);
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            downloadManager.DownloadTaskAsync(_successDownloadRequest[0]).Wait();

            // assert
            Assert.IsFalse(_successDownloadRequest[0].IsSaving);
            Assert.IsTrue(eventsChangingState.DownloadSuccessfullCompleted);
            Assert.IsFalse(eventsChangingState.IsDownloadCancelled);
            Assert.IsNull(eventsChangingState.DownloadError);
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
        }

        [TestMethod]
        public void TestDownloadCompletedEventWhenCancelled()
        {
            // arrange
            var downloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(204800, 1024, 1);
            var eventsChangingState = new DownloadServiceEventsState(downloadService);
            var downloadRequest = new DownloadRequest() { 
                Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), 
                Path = Path.GetTempPath(), 
                DownloadService = downloadService };
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            downloadManager.DownloadTaskAsync(downloadRequest).Wait();

            // assert
            Assert.IsFalse(downloadRequest.IsSaving);
            Assert.IsFalse(eventsChangingState.DownloadSuccessfullCompleted);
            Assert.IsTrue(eventsChangingState.IsDownloadCancelled);
            Assert.IsNull(eventsChangingState.DownloadError);
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
        }

        [TestMethod]
        public void TestDownloadCompletedEventWithError()
        {
            // arrange
            var downloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(204800, 1024, 1);
            var eventsChangingState = new DownloadServiceEventsState(downloadService);
            var downloadRequest = new DownloadRequest() {
                Url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb),
                Path = Path.GetTempPath(),
                DownloadService = downloadService
            };
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            downloadManager.DownloadTaskAsync(downloadRequest).Wait();

            // assert
            Assert.IsFalse(downloadRequest.IsSaving);
            Assert.IsFalse(eventsChangingState.DownloadSuccessfullCompleted);
            Assert.IsFalse(eventsChangingState.IsDownloadCancelled);
            Assert.IsNotNull(eventsChangingState.DownloadError);
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
        }

        [TestMethod]
        public void TestSequencialSuccessDownloads()
        {
            // arrange
            var maxDownload = 2;
            var isNumberOfDownloadsMoreThanMaxDownloads = false;
            var eventsChangingStates = _successDownloadRequest.Select(req => new DownloadServiceEventsState(req.DownloadService)).ToArray();
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), maxDownload);
            downloadManager.DownloadStarted += (s, e) => {
                if (downloadManager.NumberOfDownloadsInProgress > maxDownload)
                    isNumberOfDownloadsMoreThanMaxDownloads = true;
            };

            // act
            downloadManager.DownloadTaskAsync(_successDownloadRequest).Wait();

            // assert
            Assert.IsFalse(isNumberOfDownloadsMoreThanMaxDownloads);
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
        }
    }
}