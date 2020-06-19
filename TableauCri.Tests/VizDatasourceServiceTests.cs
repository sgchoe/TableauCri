using NUnit.Framework;
using TableauCri.Services;
using System;
using System.Threading.Tasks;
using TableauCri.Models.Configuration;
using Microsoft.Extensions.Options;
using System.Linq;
using TableauCri.Models;
using Newtonsoft.Json;
using System.IO;

namespace TableauCri.Tests
{
    [Ignore("VizDatasourceServiceTests")]
    public class VizDatasourceServiceTests
    {
        [SetUp]
        public async Task Setup()
        {
            await ServiceFactory.Instance.GetService<ITableauApiServiceSource>().SignInAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await ServiceFactory.Instance.GetService<ITableauApiService>().SignOutAsync();
        }

        [Ignore("AdHocTest")]
        [Test]
        public async Task AdHocTest()
        {
            await Task.Run(() => Console.WriteLine("adhoc test"));
            Assert.Pass();
        }

        [Ignore("CreateVizDatasourceReportAsyncTest")]
        [Test]
        public void CreateVizDatasourceReportAsyncTest()
        {
            var svc = GetService();
            var options = GetOptions();
            File.Delete(options.CurrentValue.ReportOutputPath);
            Assert.DoesNotThrowAsync(async () => await svc.CreateVizDatasourceReportAsync());
            FileAssert.Exists(options.CurrentValue.ReportOutputPath);
        }

        [Ignore("LoadVizDatasourceReportAsyncTest")]
        [Test]
        public void LoadVizDatasourceReportAsyncTest()
        {
            var svc = GetService();
            var options = GetOptions();
            Assert.DoesNotThrowAsync(async () => await svc.LoadVizDatasourceReportAsync());
            var name = "name";
            var vizDatasource = svc.GetVizDatasource(name);
            Assert.IsNotNull(vizDatasource?.Name);
        }

        [Ignore("DownloadVizDatasourceFilesAsyncTest")]
        [Test]
        public async Task DownloadVizDatasourceFilesAsyncTest()
        {
            var svc = GetService();
            var options = GetOptions();
            if (Directory.Exists(options.CurrentValue.DatasourceFilesPath))
            {
                Directory.Delete(options.CurrentValue.DatasourceFilesPath, true);
            }
            await svc.DownloadVizDatasourceFilesAsync();
            DirectoryAssert.Exists(options.CurrentValue.DatasourceFilesPath);
        }

        [Ignore("GetVizDatasourceTest")]
        [Test]
        public async Task GetVizDatasourceTest()
        {
            var svc = GetService();
            var name = "name";
            await svc.LoadVizDatasourceReportAsync();
            var vizDatasource = svc.GetVizDatasource(name);
            Assert.IsNotNull(vizDatasource);
            Console.WriteLine(JsonConvert.SerializeObject(vizDatasource));
        }

        [Ignore("GetVizDatasourceFileTest")]
        [Test]
        public async Task GetVizDatasourceFileTest()
        {
            var svc = GetService();
            var name = "name";
            await svc.LoadVizDatasourceReportAsync();
            var vizDatasourceFile = svc.GetVizDatasourceFile(name);
            Assert.IsNotNull(vizDatasourceFile);
            vizDatasourceFile.Bytes = null;
            Console.WriteLine(JsonConvert.SerializeObject(vizDatasourceFile));
        }

        [Ignore("FindPasswordTest")]
        [Test]
        public async Task FindPasswordTest()
        {
            var svc = GetService();
            await svc.LoadVizDatasourceReportAsync();
            var username = "username";
            var password = svc.FindPassword(username);
            Assert.IsFalse(String.IsNullOrWhiteSpace(password));

            Console.WriteLine($"{username}:{password}");

            username = "dummy_username";
            password = svc.FindPassword(username);
            Assert.IsTrue(String.IsNullOrWhiteSpace(password));
        }

        private IVizDatasourceService GetService()
        {
            return ServiceFactory.Instance.GetService<IVizDatasourceService>();
        }

        private IOptionsMonitor<VizDatasourceSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<VizDatasourceSettings>();
        }
    }
}
