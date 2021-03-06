using NUnit.Framework;
using TableauCri.Services;
using System;
using System.Threading.Tasks;
using TableauCri.Models.Configuration;
using Microsoft.Extensions.Options;

namespace TableauCri.Tests
{
    [Ignore("TableauApiServiceSourceTests")]
    public class TableauApiServiceSourceTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Ignore("AdHocTest")]
        [Test]
        public async Task AdHocTest()
        {
            var svc = GetService();

            await svc.SignInAsync();

            var url = $"api/{GetOptions().CurrentValue.ApiVersion}/sites/{((TableauApiService)svc).SiteId}/users";
            var response = svc.SendRequestAsync<string>(url, System.Net.Http.HttpMethod.Get, null).Result;
            Console.WriteLine(response);

            await svc.SignOutAsync();
        }


        [Ignore("SignInSignOutTest")]
        [Test]
        public void SignInAsyncSignOutAsyncTest()
        {
            var svc = GetService();

            Assert.DoesNotThrowAsync(async () => await svc.SignInAsync());
            Console.WriteLine($"SiteId: {((TableauApiService)svc).SiteId}");
            Console.WriteLine($"UserId: {((TableauApiService)svc).UserId}");
            Console.WriteLine($"Token: {((TableauApiService)svc).Token}");
            Assert.DoesNotThrowAsync(async () => await svc.SignOutAsync());
        }

        private ITableauApiServiceSource GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauApiServiceSource>();
        }

        private IOptionsMonitor<TableauApiSettingsSource> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauApiSettingsSource>();
        }
    }
}
