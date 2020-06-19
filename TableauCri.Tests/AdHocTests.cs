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
using System.Xml;
using System.Xml.XPath;

namespace TableauCri.Tests
{
    [Ignore("AdHocTests")]
    public class AdHocTests
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
            await Task.Run(() => Console.WriteLine("AdHocTest"));
            Assert.Pass();
        }

        private T GetService<T>()
        {
            return ServiceFactory.Instance.GetService<T>();
        }

        private IOptionsMonitor<T> GetOptions<T>()
        {
            return ServiceFactory.Instance.GetOptions<T>();
        }
    }
}

