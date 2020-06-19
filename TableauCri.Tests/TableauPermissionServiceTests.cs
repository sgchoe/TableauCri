using NUnit.Framework;
using TableauCri.Services;
using System;
using System.Threading.Tasks;
using TableauCri.Models.Configuration;
using Microsoft.Extensions.Options;
using System.Linq;
using TableauCri.Models;
using Newtonsoft.Json;
using static TableauCri.Services.TableauPermissionService;

namespace TableauCri.Tests
{
    [Ignore("TableauPermissionServiceTests")]
    public class TableauPermissionServiceTests
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

        [Ignore("GetPermissionsAsyncTest")]
        [Test]
        public async Task GetPermissionsAsyncTest()
        {
            var svc = GetService();

            var resourceType = ResourceType.Project;
            var resourceId = "luid";

            var permission = await svc.GetPermissionsAsync(resourceType, resourceId);
            Assert.IsNotNull(permission);

            Console.WriteLine($"Permissions for {resourceType.ToString().ToLower()} {resourceId}:");
            Console.WriteLine(permission.ToRequestString());
        }

        [Ignore("GetDefaultPermissionsAsyncTest")]
        [Test]
        public async Task GetDefaultPermissionsAsyncTest()
        {
            var svc = GetService();

            var resourceType = ResourceType.Workbook;
            var projectId = "luid";

            var permission = await svc.GetDefaultPermissionsAsync(projectId, resourceType);
            Assert.IsNotNull(permission);

            Console.WriteLine($"Default {resourceType.ToString().ToLower()} permissions for project {projectId}:");
            Console.WriteLine(permission.ToRequestString());

            resourceType = ResourceType.Datasource;

            permission = await svc.GetDefaultPermissionsAsync(projectId, resourceType);
            Assert.IsNotNull(permission);

            Console.WriteLine($"Default {resourceType.ToString().ToLower()} permissions for project {projectId}:");
            Console.WriteLine(permission.ToRequestString());
        }

        [Ignore("AddCapabilityAsyncTest")]
        [Test]
        public async Task AddCapabilityAsyncTest()
        {
            var svc = GetService();

            var resourceType = ResourceType.Project;
            var resourceId = "project luid";

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var capabilityName = CapabilityName.ProjectLeader;
            var capabilityMode = CapabilityMode.Allow;

            var permission = await svc.AddCapabilityAsync(
                resourceType,
                resourceId,
                granteeType,
                granteeId,
                capabilityName,
                capabilityMode
            );
            Assert.IsNotNull(permission);

            Console.WriteLine(JsonConvert.SerializeObject(permission));
        }

        [Ignore("AddDefaultCapabilityAsyncTest")]
        [Test]
        public async Task AddDefaultCapabilityAsyncTest()
        {
            var svc = GetService();

            var resourceType = ResourceType.Workbook;

            var projectId = "project luid";

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var capabilityName = CapabilityName.Write;
            var capabilityMode = CapabilityMode.Allow;

            var permission = await svc.AddDefaultCapabilityAsync(
                projectId,
                resourceType,
                granteeType,
                granteeId,
                capabilityName,
                capabilityMode
            );
            Assert.IsNotNull(permission);

            Console.WriteLine(JsonConvert.SerializeObject(permission));
        }

        [Ignore("AddDefaultCapabilityRolePermissionsAsyncTest")]
        [Test]
        public async Task AddDefaultCapabilityRolePermissionsAsyncTest()
        {
            var svc = GetService();

            var projectId = "project luid";

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var permission = await svc.AddDefaultCapabilityRolePermissionsAsync(
                projectId,
                granteeType,
                granteeId,
                CapabilityRole.WorkbookEditor
            );
            Assert.IsNotNull(permission);

            Console.WriteLine(JsonConvert.SerializeObject(permission));

            permission = await svc.AddDefaultCapabilityRolePermissionsAsync(
                projectId,
                granteeType,
                granteeId,
                CapabilityRole.DatasourceEditor
            );
            Assert.IsNotNull(permission);

            Console.WriteLine(JsonConvert.SerializeObject(permission));
        }

        [Ignore("AddPermissionAsyncTest")]
        [Test]
        public async Task AddPermissionAsyncTest()
        {
            var svc = GetService();

            var resourceType = ResourceType.Project;
            var resourceId = "project luid";

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var capabilityName = CapabilityName.ProjectLeader;
            var capabilityMode = CapabilityMode.Allow;

            var permission = TableauPermissionService.BuildPermission(
                resourceType, resourceId, granteeType, granteeId, capabilityName, capabilityMode
            );

            permission = await svc.AddPermissionAsync(permission);
            Assert.IsNotNull(permission);

            Console.WriteLine(JsonConvert.SerializeObject(permission));
        }

        [Ignore("AddDefaultPermissionAsyncTest")]
        [Test]
        public async Task AddDefaultPermissionAsyncTest()
        {
            var svc = GetService();

            var projectId = "project luid";
            var resourceType = ResourceType.Workbook;

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var capabilityName = CapabilityName.Write;
            var capabilityMode = CapabilityMode.Allow;

            var permission = TableauPermissionService.BuildDefaultPermission(
                granteeType, granteeId, capabilityName, capabilityMode
            );

            permission = await svc.AddDefaultPermissionAsync(projectId, resourceType, permission);
            Assert.IsNotNull(permission);

            Console.WriteLine(JsonConvert.SerializeObject(permission));
        }

        [Ignore("DeleteCapabilityAsyncTest")]
        [Test]
        public void DeleteCapabilityAsyncTest()
        {
            var svc = GetService();

            var resourceType = ResourceType.Project;
            var resourceId = "project luid";

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var capabilityName = CapabilityName.ProjectLeader;
            var capabilityMode = CapabilityMode.Allow;

            Assert.DoesNotThrowAsync(
                async () =>
                await svc.DeleteCapabilityAsync(
                    resourceType,
                    resourceId,
                    granteeType,
                    granteeId,
                    capabilityName,
                    capabilityMode
                )
            );
        }

        [Ignore("DeleteDefaultCapabilityAsyncTest")]
        [Test]
        public void DeleteDefaultCapabilityAsyncTest()
        {
            var svc = GetService();

            var resourceType = ResourceType.Workbook;
            var projectId = "project luid";

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var capabilityName = CapabilityName.Write;
            var capabilityMode = CapabilityMode.Allow;

            Assert.DoesNotThrowAsync(
                async () =>
                    await svc.DeleteDefaultCapabilityAsync(
                        projectId,
                        resourceType,
                        granteeType,
                        granteeId,
                        capabilityName,
                        capabilityMode
                    )
            );
        }

        [Ignore("DeleteDefaultCapabilityRolePermissionsAsyncTest")]
        [Test]
        public void DeleteDefaultCapabilityRolePermissionsAsyncTest()
        {
            var svc = GetService();

            var projectId = "project luid";

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            Assert.DoesNotThrowAsync(
                async () =>
                    await svc.DeleteDefaultCapabilityRolePermissionsAsync(
                        projectId,
                        granteeType,
                        granteeId,
                        CapabilityRole.WorkbookEditor
                    )
                );

            Assert.DoesNotThrowAsync(
                async () =>
                    await svc.DeleteDefaultCapabilityRolePermissionsAsync(
                        projectId,
                        granteeType,
                        granteeId,
                        CapabilityRole.DatasourceEditor
                    )
                );
        }

        [Ignore("DeletePermissionAsync")]
        [Test]
        public void DeletePermissionAsync()
        {
            var svc = GetService();

            var resourceType = ResourceType.Project;
            var resourceId = "project luid";

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var capabilityName = CapabilityName.ProjectLeader;
            var capabilityMode = CapabilityMode.Allow;

            var permission = TableauPermissionService.BuildPermission(
                resourceType, resourceId, granteeType, granteeId, capabilityName, capabilityMode
            );

            Assert.DoesNotThrowAsync(async () => await svc.DeletePermissionAsync(permission));

            // caching prevents instant verification
            // var projects = await svc.GetProjectsAsync();
            // Assert.That(!projects.Any(p => p.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase)));
        }

        [Ignore("DeleteDefaultPermissionAsyncTest")]
        [Test]
        public void DeleteDefaultPermissionAsyncTest()
        {
            var svc = GetService();

            var projectId = "project luid";
            var resourceType = ResourceType.Workbook;

            var granteeType = GranteeType.Group;
            var granteeId = "group luid";

            var capabilityName = CapabilityName.Write;
            var capabilityMode = CapabilityMode.Allow;

            var permission = TableauPermissionService.BuildDefaultPermission(
                granteeType, granteeId, capabilityName, capabilityMode
            );

            Assert.DoesNotThrowAsync(
                async () => await svc.DeleteDefaultPermissionAsync(projectId, resourceType, permission)
            );
        }

        private ITableauPermissionService GetService()
        {
            return ServiceFactory.Instance.GetService<ITableauPermissionService>();
        }

        private IOptionsMonitor<TableauApiSettings> GetOptions()
        {
            return ServiceFactory.Instance.GetOptions<TableauApiSettings>();
        }
    }
}
