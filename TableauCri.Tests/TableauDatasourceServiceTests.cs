using NUnit.Framework;
using TableauCri.Services;
using System;
using System.Threading.Tasks;
using TableauCri.Models.Configuration;
using Microsoft.Extensions.Options;
using System.Linq;
using TableauCri.Models;
using Newtonsoft.Json;

namespace TableauCri.Tests
{
    [Ignore("TableauDatasourceServiceTests")]
    public class TableauDatasourceServiceTests
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

        [Ignore("GetDatasourcesAsyncTest")]
        [Test]
        public async Task GetDatasourcesAsyncTest()
        {
            var svc = GetService();

            var datasources = await svc.GetDatasourcesAsync();

            Assert.IsNotNull(datasources);
            Assert.IsNotEmpty(datasources);

            Console.WriteLine(JsonConvert.SerializeObject(datasources.First()));
            Console.WriteLine(JsonConvert.SerializeObject(datasources.Last()));

            Assert.AreEqual(
                datasources.Count(),
                datasources.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            );
        }

        [Ignore("GetDatasourceAsyncTest")]
        [Test]
        public async Task GetDatasourceAsyncTest()
        {
            var svc = GetService();

            var id = "luid";

            var datasource = await svc.GetDatasourceAsync(id, true);

            Assert.IsNotNull(datasource);

            Console.WriteLine(JsonConvert.SerializeObject(datasource));
        }

        [Ignore("FindDatasourceAsyncTest")]
        [Test]
        public async Task FindDatasourceAsyncTest()
        {
            var svc = GetService();

            var name = "name";
            var datasource = await svc.FindDatasourceAsync(name);

            Assert.IsNotNull(datasource);

            Console.WriteLine(JsonConvert.SerializeObject(datasource));
        }

        [Ignore("GetDatasourceConnectionsAsyncTest")]
        [Test]
        public async Task GetDatasourceConnectionsAsyncTest()
        {
            var svc = GetService();

            var datasourceId = "luid";
            
            var connections = await svc.GetDatasourceConnectionsAsync(datasourceId);

            Assert.IsNotNull(connections);
            Assert.IsNotEmpty(connections);

            Console.WriteLine(JsonConvert.SerializeObject(connections));

            foreach (var connection in connections)
            {
                Console.WriteLine(JsonConvert.SerializeObject(connection));
            }
        }

        [Ignore("DownloadDatasourceAsyncTest")]
        [Test]
        public async Task DownloadDatasourceAsyncTest()
        {
            var svc = GetService();

            var datasourceId = "luid";
            var fileInfo = await svc.DownloadDatasourceAsync(datasourceId);

            Assert.IsNotNull(fileInfo);
            Assert.That(fileInfo.Exists);

            Console.WriteLine($"{fileInfo.FullName}, {fileInfo.Length}");

            fileInfo.Delete();
        }

        [Ignore("DownloadDatasourceBytesAsyncTest")]
        [Test]
        public async Task DownloadDatasourceBytesAsyncTest()
        {
            var svc = GetService();

            var datasourceId = "luid";
            var fileBytes = await svc.DownloadDatasourceBytesAsync(datasourceId);

            Assert.IsNotNull(fileBytes);

            Console.WriteLine($"Downloaded file: {fileBytes.Name}, {fileBytes.Bytes.Length} bytes");
        }

        [Ignore("DeleteDatasourceAsync")]
        [Test]
        public void DeleteDatasourceAsync()
        {
            var svc = GetService(); ;

            var id = "luid";
            Assert.DoesNotThrowAsync(async () => await svc.DeleteDatasourceAsync(id));

            // caching prevents instant verification
            // var projects = await svc.GetProjectsAsync();
            // Assert.That(!projects.Any(p => p.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase)));
        }

        private ITableauDatasourceService GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauDatasourceService>();
        }

        private IOptionsMonitor<TableauApiSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauApiSettings>();
        }
    }
}
