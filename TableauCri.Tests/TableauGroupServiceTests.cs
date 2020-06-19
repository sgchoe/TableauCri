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
    [Ignore("TableauGroupServiceTests")]
    public class TableauGroupServiceTests
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

        [Ignore("GetGroupsAsyncTest")]
        [Test]
        public async Task GetGroupsAsyncTest()
        {
            var svc = GetService();

            var groups = await svc.GetGroupsAsync();

            Assert.IsNotNull(groups);
            Assert.IsNotEmpty(groups);

            foreach (var group in groups)
            {
                Console.WriteLine(JsonConvert.SerializeObject(group));
            }
        }

        [Ignore("GetGroupAsyncTest")]
        [Test]
        public async Task GetGroupAsyncTest()
        {
            var svc = GetService();

            var id = "luid";
            var group = await svc.GetGroupAsync(id);

            Assert.IsNotNull(group);

            Console.WriteLine(JsonConvert.SerializeObject(group));
        }

        [Ignore("FindGroupAsyncTest")]
        [Test]
        public async Task FindGroupAsyncTest()
        {
            var svc = GetService();

            var name = "name";
            var group = await svc.FindGroupAsync(name);

            Assert.IsNotNull(group);
            Assert.True(!String.IsNullOrWhiteSpace(group.Id));

            Console.WriteLine(JsonConvert.SerializeObject(group));
        }

        [Ignore("CreateGroupAsyncTest")]
        [Test]
        public async Task CreateGroupAsyncTest()
        {
            var svc = GetService();

            var name = "name";
            var group = await svc.CreateGroupAsync(name);

            Assert.IsNotNull(group);
            Assert.True(!String.IsNullOrWhiteSpace(group.Id));

            Console.WriteLine(JsonConvert.SerializeObject(group));
        }

        [Ignore("AddUserToGroupAsyncTest")]
        [Test]
        public async Task AddUserToGroupAsyncTest()
        {
            var svc = GetService();

            var groupId = "group luid";
            var userId = "user luid";

            var user = await svc.AddUserToGroupAsync(groupId, userId);

            Assert.IsNotNull(user);
            Assert.True(!String.IsNullOrWhiteSpace(user.Id));

            Console.WriteLine(JsonConvert.SerializeObject(user));
        }

        [Ignore("DeleteGroupAsyncTest")]
        [Test]
        public void DeleteGroupAsyncTest()
        {
            var svc = GetService();

            var groupId = "luid";
            Assert.DoesNotThrowAsync(async () => await svc.DeleteGroupAsync(groupId));

            // caching prevents instant verification
            // var groups = await svc.GetGroupsAsync();
            // Assert.That(!groups.Any(g => g.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase)));
        }

        [Ignore("RemoveUserFromGroupAsyncTest")]
        [Test]
        public void RemoveUserFromGroupAsyncTest()
        {
            var svc = GetService();

            var groupId = "group luid";
            var userId = "user luid";

            Assert.DoesNotThrowAsync(async () => await svc.RemoveUserFromGroupAsync(groupId, userId));

            // caching prevents instant verification
            // var users = await svc.GetUsersAsync(groupId);
            // Assert.That(!users.Any(u => u.Id.Equals(userId, StringComparison.OrdinalIgnoreCase)));
        }

        private ITableauGroupService GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauGroupService>();
        }

        private IOptionsMonitor<TableauApiSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauApiSettings>();
        }
    }
}
