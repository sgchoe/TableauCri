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
    [Ignore("TableauUserServiceTests")]
    public class TableauUserServiceTests
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

        [Ignore("GetUsersAsyncTest")]
        [Test]
        public async Task GetUsersAsyncTest()
        {
            var svc = GetService();

            var users = (await svc.GetUsersAsync()).OrderBy(u => u.Name.ToLower());

            Assert.IsNotNull(users);
            Assert.IsNotEmpty(users);

            Console.WriteLine("Users in site:");
            foreach (var user in users)
            {
                Console.WriteLine(JsonConvert.SerializeObject(user));
            }
        }

        [Ignore("GetUserAsyncTest")]
        [Test]
        public async Task GetUserAsyncTest()
        {
            var svc = GetService();

            var id = "luid";

            var user = await svc.GetUserAsync(id);
            Assert.IsNotNull(user);
            Console.WriteLine(JsonConvert.SerializeObject(user));
        }

        [Ignore("FindUsersAsyncTest")]
        [Test]
        public async Task FindUsersAsyncTest()
        {
            var svc = GetService();

            var name = "name";
            var domain = "domain.example.com";

            var users = await svc.FindUsersAsync(name, domain);

            Assert.IsNotNull(users);
            Assert.IsNotEmpty(users);

            Console.WriteLine("Users found:");
            foreach (var user in users)
            {
                Console.WriteLine(JsonConvert.SerializeObject(user));
            }
        }

        [Ignore("FindUserAsyncTest")]
        [Test]
        public async Task FindUserAsyncTest()
        {
            var svc = GetService();

            var name = "name";
            var domain = "domain.example.com";

            var user = await svc.FindUserAsync(name, domain);
            Assert.IsNotNull(user);
            Console.WriteLine(JsonConvert.SerializeObject(user));
        }

        [Ignore("AddUserToSiteAsyncTest")]
        [Test]
        public async Task AddUserToSiteAsyncTest()
        {
            var svc = GetService();

            var downLevelLogonName = "domain\\username";
            var user = await svc.AddUserToSiteAsync(downLevelLogonName, TableauApiService.SITE_ROLE_LEGACY_INTERACTOR);

            Assert.IsNotNull(user);
            Assert.True(!String.IsNullOrWhiteSpace(user.Id));

            Console.WriteLine(JsonConvert.SerializeObject(user));
        }

        [Ignore("RemoveUserFromSiteAsyncTest")]
        [Test]
        public void RemoveUserFromSiteAsyncTest()
        {
            var svc = GetService();

            var id = "luid";

            Assert.DoesNotThrowAsync(async () => await svc.RemoveUserFromSiteAsync(id));

            // caching prevents instant verification
            // var users = await svc.GetUsersAsync();
            // Assert.That(!users.Any(u => u.Id.Equals(userId, StringComparison.OrdinalIgnoreCase)));
        }

        private ITableauUserService GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauUserService>();
        }

        private IOptionsMonitor<TableauApiSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauApiSettings>();
        }
    }
}

