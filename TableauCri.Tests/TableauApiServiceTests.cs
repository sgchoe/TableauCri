using NUnit.Framework;
using TableauCri.Services;
using System;
using System.Threading.Tasks;
using TableauCri.Models.Configuration;
using Microsoft.Extensions.Options;
using System.Linq;
using TableauCri.Models;
using Newtonsoft.Json;
using static TableauCri.Services.TableauApiService;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;

namespace TableauCri.Tests
{
    [Ignore("TableauApiServiceTests")]
    public class TableauApiServiceTests
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

        [Ignore("BuildQueryFilterTest")]
        [Test]
        public void BuildQueryFilterTest()
        {
            var svc = GetService();

            var queryFilter = svc.BuildQueryFilter("field", QueryFilterOperator.eq, "name");
            Assert.AreEqual("field:eq:name", queryFilter);

            queryFilter = svc.BuildQueryFilter("field", QueryFilterOperator.eq, "field name");
            Assert.AreEqual("field:eq:field%20name", queryFilter);
        }

        [Ignore("SendRequestAsyncTest")]
        [Test]
        public async Task SendRequestAsyncTest()
        {
            var svc = GetService();

            var server = "tableau.example.com";
            var datasourceNameId = "datasourcenameid";
            var cookie = "cookie";

            var url = $"https://{server}/t/site/datasources/{datasourceNameId}.tds";
            var headers = new Dictionary<string, string>()
            {
                { "User-Agent", "Mozilla Chrome Safari" },
            };
            var datasourceBytes = await svc.SendRequestAsync<byte[]>(url, HttpMethod.Get, null, headers, "*/*", cookie);
            Assert.IsNotNull(datasourceBytes);
            Console.WriteLine($"downloaded datasource, {datasourceBytes.Length} bytes");
            await File.WriteAllBytesAsync($"{datasourceNameId}.tds", datasourceBytes);
        }

        private ITableauApiService GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauApiService>();
        }

        private IOptionsMonitor<TableauApiSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauApiSettings>();
        }
    }
}
