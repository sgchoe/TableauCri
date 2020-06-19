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
    [Ignore("TableauProjectServiceTests")]
    public class TableauProjectServiceTests
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

        [Ignore("GetProjectsAsyncTest")]
        [Test]
        public async Task GetProjectsAsyncTest()
        {
            var svc = GetService();

            var projects = await svc.GetProjectsAsync();

            Assert.IsNotNull(projects);
            Assert.IsNotEmpty(projects);

            foreach (var project in projects)
            {
                Console.WriteLine(JsonConvert.SerializeObject(project));
            }
        }

        [Ignore("FindProjectAsyncTest")]
        [Test]
        public async Task FindProjectAsyncTest()
        {
            var svc = GetService();

            var name = "name";
            var project = await svc.FindProjectAsync(name);

            Assert.IsNotNull(project);
            Assert.True(!String.IsNullOrWhiteSpace(project.Id));

            Console.WriteLine(JsonConvert.SerializeObject(project));

            // cleanup
            //Assert.DoesNotThrowAsync(async () => await svc.DeleteProjectAsync(project.Id));

            // caching prevents instant verification
            // var projects = await svc.GetProjectsAsync();
            // Assert.That(projects.Any(p => p.Id.Equals(project.Id)));
        }

        [Ignore("CreateProjectAsyncTest")]
        [Test]
        public async Task CreateProjectAsyncTest()
        {
            var svc = GetService();

            var parentProjectId = "luid";
            var name = "name";
            var desc = "desc";
            var project = await svc.CreateProjectAsync(name, desc, parentProjectId);
            
            Assert.IsNotNull(project);
            Assert.True(!String.IsNullOrWhiteSpace(project.Id));

            Console.WriteLine(JsonConvert.SerializeObject(project));

            // cleanup
            //Assert.DoesNotThrowAsync(async () => await svc.DeleteProjectAsync(project.Id));
            
            // caching prevents instant verification
            // var projects = await svc.GetProjectsAsync();
            // Assert.That(projects.Any(p => p.Id.Equals(project.Id)));
        }

        [Ignore("DeleteProjectAsyncTest")]
        [Test]
        public void DeleteProjectAsyncTest()
        {
            var svc = GetService();

            var projectId = "luid";
            Assert.DoesNotThrowAsync(async () => await svc.DeleteProjectAsync(projectId));

            // caching prevents instant verification
            // var projects = await svc.GetProjectsAsync();
            // Assert.That(!projects.Any(p => p.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase)));
        }

        private ITableauProjectService GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauProjectService>();
        }

        private IOptionsMonitor<TableauApiSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauApiSettings>();
        }
    }
}
