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
    [Ignore("TableauMigrationServiceTests")]
    public class TableauMigrationServiceTests
    {
        [SetUp]
        public async Task Setup()
        {
            await ServiceFactory.Instance.GetService<ITableauApiServiceSource>().SignInAsync();
            await ServiceFactory.Instance.GetService<ITableauApiServiceDestination>().SignInAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await ServiceFactory.Instance.GetService<ITableauApiServiceSource>().SignOutAsync();
            await ServiceFactory.Instance.GetService<ITableauApiServiceDestination>().SignOutAsync();
        }

        [Ignore("AdHocTest")]
        [Test]
        public async Task AdHocTest()
        {
            await Task.Run(() => Console.WriteLine("adhoc test"));

            var service = GetService();

            Assert.Pass();
        }

        [Ignore("MigrateProjectsTest")]
        [Test]
        public async Task MigrateProjectsTest()
        {
            var service = GetService();
            await service.MigrateProjects();
            Assert.Pass();
        }

        private ITableauMigrationService GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauMigrationService>();
        }

        private IOptionsMonitor<TableauMigrationSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauMigrationSettings>();
        }
    }
}

