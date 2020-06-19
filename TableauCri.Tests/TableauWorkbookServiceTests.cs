using NUnit.Framework;
using TableauCri.Services;
using System;
using System.Threading.Tasks;
using TableauCri.Models.Configuration;
using Microsoft.Extensions.Options;
using System.Linq;
using TableauCri.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace TableauCri.Tests
{
    [Ignore("TableauWorkbookServiceTests")]
    public class TableauWorkbookServiceTests
    {
        [SetUp]
        public async Task Setup()
        {
            await ServiceFactory.Instance.GetService<ITableauApiService>().SignInAsync();
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

        [Ignore("GetWorkbooksAsyncTest")]
        [Test]
        public async Task GetWorkbooksAsyncTest()
        {
            var svc = GetService();

            var workbooks = await svc.GetWorkbooksAsync();

            Assert.IsNotNull(workbooks);
            Assert.IsNotEmpty(workbooks);

            Console.WriteLine(JsonConvert.SerializeObject(workbooks));
        }

        [Ignore("GetWorkbookAsyncTest")]
        [Test]
        public async Task GetWorkbookAsyncTest()
        {
            var svc = GetService();

            var id = "luid";
            var workbook = await svc.GetWorkbookAsync(id, true);

            Assert.IsNotNull(workbook);
            Console.WriteLine(JsonConvert.SerializeObject(workbook));
        }

        [Ignore("FindWorkbooksAsyncTest")]
        [Test]
        public async Task FindWorkbooksAsyncTest()
        {
            var svc = GetService();

            var name = "name";
            var workbooks = await svc.FindWorkbooksAsync(name);

            Assert.IsNotNull(workbooks);
            Console.WriteLine(JsonConvert.SerializeObject(workbooks));
        }

        [Ignore("FindWorkbookAsyncTest")]
        [Test]
        public async Task FindWorkbookAsyncTest()
        {
            var svc = GetService();

            var name = "name";
            var workbook = await svc.FindWorkbookAsync(name);

            Assert.IsNotNull(workbook);
            Console.WriteLine(JsonConvert.SerializeObject(workbook));
        }

        [Ignore("GetWorkbookConnectionsAsyncTest")]
        [Test]
        public async Task GetWorkbookConnectionsAsyncTest()
        {
            var svc = GetService();

            var workbookId = "luid";
            
            var connections = await svc.GetWorkbookConnectionsAsync(workbookId);

            Assert.IsNotNull(connections);
            Assert.IsNotEmpty(connections);

            // Console.WriteLine(JsonConvert.SerializeObject(connections));
            // return;

            var datasourceService = ServiceFactory.Instance.GetService<ITableauDatasourceService>();

            var workbooks = await svc.GetWorkbooksAsync();
            var connDict = new Dictionary<string, string>();
            foreach (var workbook in workbooks)
            {
                var workbookConnections = await svc.GetWorkbookConnectionsAsync(workbook.Id);
                foreach (var workbookConnection in workbookConnections)
                {
                    if (workbookConnection.Type.Equals(TableauApiService.CONNECTION_TYPE_SQLPROXY))
                    {
                        continue;
                    }
                    var connLine = String.Format(
                        "{0},{1},{2}",
                        workbookConnection.Type,
                        workbookConnection.ServerAddress,
                        workbookConnection.ServerPort
                    );
                    connDict[connLine] = workbook.Name;
                }
            }
            foreach (var kvp in connDict)
            {
                Console.WriteLine($"{kvp.Key} ({kvp.Value})");
            }
        }

        [Ignore("DownloadWorkbookAsyncTest")]
        [Test]
        public async Task DownloadWorkbookAsyncTest()
        {
            var svc = GetService();

            var workbookId = "luid";
            var fileInfo = await svc.DownloadWorkbookAsync(workbookId);

            Assert.That(fileInfo?.Exists ?? false);

            Console.WriteLine($"Downloaded file: {fileInfo.Name}, {fileInfo.Length} bytes");
        }

        [Ignore("DownloadWorkbookBytesAsyncTest")]
        [Test]
        public async Task DownloadWorkbookBytesAsyncTest()
        {
            var svc = GetService();

            var workbookId = "luid";
            var fileBytes = await svc.DownloadWorkbookBytesAsync(workbookId);

            Assert.IsNotNull(fileBytes);

            Console.WriteLine($"Downloaded file: {fileBytes.Name}, {fileBytes.Bytes.Length} bytes");
        }

        [Ignore("DeleteWorkbookAsync")]
        [Test]
        public void DeleteWorkbookAsync()
        {
            var svc = GetService();

            var id = "luid";
            Assert.DoesNotThrowAsync(async () => await svc.DeleteWorkbookAsync(id));

            // caching prevents instant verification
            // var projects = await svc.GetProjectsAsync();
            // Assert.That(!projects.Any(p => p.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase)));
        }

        private ITableauWorkbookService GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauWorkbookService>();
        }

        private IOptionsMonitor<TableauApiSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauApiSettings>();
        }
    }
}
