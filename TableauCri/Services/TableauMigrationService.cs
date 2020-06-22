using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TableauCri.Models.Configuration;
using Serilog;
using System.Linq;
using static TableauCri.Services.TableauPermissionService;
using System.Collections.Generic;
using TableauCri.Models;
using TableauCri.Extensions;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using CsvHelper;
using System.Globalization;
using System.Threading;
using System.Xml;

namespace TableauCri.Services
{
    public interface ITableauMigrationService : IDisposable
    {
        Task MigrateProjects();
    }

    public class TableauMigrationService : ITableauMigrationService
    {
        private readonly IOptionsMonitor<TableauMigrationSettings> _settingsMonitor = null;
        private ITableauApiServiceSource _tableauApiServiceSource = null;
        private ITableauApiServiceDestination _tableauApiServiceDestination;

        private ITableauServiceFactory _tableauServiceFactorySource = null;
        private ITableauServiceFactory _tableauServiceFactoryDest = null;
        private IVizDatasourceService _vizDatasourceService = null;

        private ILogger _logger = null;
        private bool _disposed = false;

        public TableauMigrationService(
            IOptionsMonitor<TableauMigrationSettings> settingsMonitor,
            ITableauApiServiceSource tableauApiServiceSource,
            ITableauApiServiceDestination tableauApiServiceDestination,
            IVizDatasourceService vizDatasourceService,
            ILogger logger = null
        )
        {
            _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException("Missing Tableau Migration settings");
            _tableauApiServiceSource = tableauApiServiceSource;
            _tableauApiServiceDestination = tableauApiServiceDestination;
            _vizDatasourceService = vizDatasourceService;
            _logger = logger;

            _settingsMonitor.CurrentValue.DefaultOwnerUsername =
                _settingsMonitor.CurrentValue.DefaultOwnerUsername ?? "";

            _tableauServiceFactorySource = new TableauServiceFactory(_tableauApiServiceSource, _logger);
            _tableauServiceFactoryDest = new TableauServiceFactory(_tableauApiServiceDestination, _logger);

            _tableauApiServiceSource.SignInAsync().Wait();
            _tableauApiServiceDestination.SignInAsync().Wait();

            // ensure case-insensitive connection credential lookup
            _settingsMonitor.CurrentValue.EmbeddedConnectionCredentials = new Dictionary<string, string>(
                _settingsMonitor.CurrentValue.EmbeddedConnectionCredentials,
                StringComparer.OrdinalIgnoreCase
            );
        }

        public async Task MigrateProjects()
        {
            _logger?.Information(
                String.Format(
                    "Starting migration from {0} to {1} (root project '{2}')",
                    _settingsMonitor.CurrentValue.TableauApiSettingsSource.BaseUrl,
                    _settingsMonitor.CurrentValue.TableauApiSettingsDestination.BaseUrl,
                    _settingsMonitor.CurrentValue.DestinationRootProjectName
                )
            );
            if (_settingsMonitor.CurrentValue.DryRun)
            {
                _logger?.Warning("Dry run specified, no changes will be made");
            }

            var sourceProjectService = _tableauServiceFactorySource.GetService<TableauProjectService>();
            var destProjectService = _tableauServiceFactoryDest.GetService<TableauProjectService>();

            var destRootProject = null as TableauProject;
            if (!String.IsNullOrWhiteSpace(_settingsMonitor.CurrentValue.DestinationRootProjectName))
            {
                destRootProject = await destProjectService.FindProjectAsync(
                    _settingsMonitor.CurrentValue.DestinationRootProjectName
                ).ConfigureAwait(false);
                if (destRootProject == null)
                {
                    _logger?.Debug(
                        "Destination root project '{0}' not found",
                        _settingsMonitor.CurrentValue.DestinationRootProjectName
                    );
                }
                else
                {
                    _logger?.Debug(
                        "Migrating to destination root project '{0}' ({1})",
                        _settingsMonitor.CurrentValue.DestinationRootProjectName,
                        destRootProject.Id
                    );
                }
            }

            foreach (var kvp in _settingsMonitor.CurrentValue.ProjectsToMigrate)
            {
                var projectToMigrate = kvp.Key;
                var workbooksToMigrate = kvp.Value.Split(
                    new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries
                ).Select(s => s.Trim());
                _logger?.Information($"Beginning migration of project '{projectToMigrate}'");

                var sourceProject = await sourceProjectService.FindProjectAsync(
                    projectToMigrate
                ).ConfigureAwait(false);

                if (sourceProject == null)
                {
                    _logger?.Warning($"Project '{projectToMigrate}' not found in Tableau source, skipping");
                    continue;
                }

                // find project in destination, create if missing
                var destProject = await destProjectService.FindProjectAsync(
                    projectToMigrate
                ).ConfigureAwait(false);
                if (destProject == null)
                {
                    _logger?.Information($"Project '{projectToMigrate}' not found in Tableau destination, creating");

                    if (!_settingsMonitor.CurrentValue.DryRun)
                    {
                        destProject = await destProjectService.CreateProjectAsync(
                            sourceProject.Name,
                            sourceProject.Description,
                            destRootProject?.Id
                        ).ConfigureAwait(false);
                    }
                }
                else
                {
                    _logger?.Information($"Project '{projectToMigrate}' found in Tableau destination");
                }

                // create users from source project missing in destination site
                await MigrateProjectUsersAsync(sourceProject, destProject).ConfigureAwait(false);

                // create groups from source project missing in destination site
                await MigrateProjectGroupsAsync(sourceProject, destProject).ConfigureAwait(false);

                // migrate project permissions
                await MigrateProjectPermissionsAsync(sourceProject, destProject).ConfigureAwait(false);

                // migrate project default permissions
                var resourceTypes = new List<ResourceType>() { ResourceType.Datasource, ResourceType.Workbook };
                foreach (var resourceType in resourceTypes)
                {
                    await MigrateProjectDefaultPermissionsAsync(
                        sourceProject,
                        destProject,
                        resourceType
                    ).ConfigureAwait(false);
                }

                await _vizDatasourceService.LoadVizDatasourceReportAsync().ConfigureAwait(false);

                // migrate project data sources
                // skip mass migration of datasources, handle as needed on a per-workbook basis
                //await MigrateProjectDatasourcesAsync(sourceProject, destProject).ConfigureAwait(false);

                // download project workbooks
                await DownloadProjectWorkbooksAsync(sourceProject, destProject).ConfigureAwait(false);

                // migrate project workbooks with embedded datasources
                await MigrateProjectWorkbooksAsync(
                    sourceProject, destProject, workbooksToMigrate
                ).ConfigureAwait(false);

                _logger?.Information($"Completed migration of project {projectToMigrate}");
            } // foreach projectToMigrate

            _logger?.Information("Migration complete");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _tableauApiServiceSource.SignOutAsync().Wait();
                _tableauApiServiceSource.Dispose();

                _tableauApiServiceDestination.SignOutAsync().Wait();
                _tableauApiServiceDestination.Dispose();
            }

            _disposed = true;
        }

        private string UpdateWorkbookPublishedDatasourceConnections(
            string workbookContent,
            string sourceServer,
            string destServer
        )
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(workbookContent);
            var xpath = "//workbook/datasources/datasource/connection[@server='" + sourceServer + "']";
            _logger?.Debug($"Replacing instances of {sourceServer} with {destServer} in workbook");
            int count = 0;
            foreach (var node in xmlDoc.DocumentElement.SelectNodes(xpath))
            {
                var elem = node as XmlElement;
                elem.SetAttribute("server", destServer);
                count++;
            }
            _logger?.Debug($"Replaced {count} instances of {sourceServer} with {destServer} in workbook");
            return xmlDoc.OuterXml;
        }

        private string GetConnectionPassword(string username)
        {
            return _vizDatasourceService.FindPassword(username) ??
                (
                    _settingsMonitor.CurrentValue.EmbeddedConnectionCredentials.TryGetValue(
                        username, out string password
                    ) ? password : null
                );
        }

        private static async Task<Dictionary<string, TableauUser>> GetProjectUsersWithDomain(
            string projectId,
            ITableauServiceFactory serviceFactory
        )
        {
            var userService = serviceFactory.GetService<TableauUserService>();
            var groupService = serviceFactory.GetService<TableauGroupService>();
            var permissionService = serviceFactory.GetService<TableauPermissionService>();

            var projectUsers = new Dictionary<string, TableauUser>();

            // get users defined in project permissions as well as default permissions for datasources, workbooks
            var permissions = new List<TableauPermission>();
            permissions.Add(
                await permissionService.GetPermissionsAsync(
                    ResourceType.Project, projectId
                ).ConfigureAwait(false)
            );
            permissions.Add(
                await permissionService.GetDefaultPermissionsAsync(
                    projectId, ResourceType.Datasource
                ).ConfigureAwait(false)
            );
            permissions.Add(
                await permissionService.GetDefaultPermissionsAsync(
                    projectId, ResourceType.Workbook
                ).ConfigureAwait(false)
            );

            foreach (var permission in permissions)
            {
                if (permission.GranteeCapabilities == null)
                {
                    continue;
                }
                foreach (var granteeCapability in permission.GranteeCapabilities)
                {
                    if (granteeCapability.GranteeType == GranteeType.Group)
                    {
                        // get all users in group individually so user's domain is included if specified (slandron)
                        var members = await userService.GetUsersAsync(
                            granteeCapability.GranteeId
                        ).ConfigureAwait(false);
                        foreach (var member in members)
                        {
                            if (projectUsers.ContainsKey(member.Id))
                            {
                                continue;
                            }
                            projectUsers.Add(
                                member.Id,
                                await userService.GetUserAsync(member.Id).ConfigureAwait(false)
                            );
                        }
                    }

                    if (granteeCapability.GranteeType == GranteeType.User &&
                        !projectUsers.ContainsKey(granteeCapability.GranteeId))
                    {
                        projectUsers.Add(
                            granteeCapability.GranteeId,
                            await userService.GetUserAsync(granteeCapability.GranteeId).ConfigureAwait(false)
                        );
                    }
                }
            }

            return projectUsers;
        }

        private static async Task<Dictionary<string, TableauGroup>> GetProjectGroups(
            string projectId,
            ITableauServiceFactory serviceFactory
        )
        {
            var groupService = serviceFactory.GetService<TableauGroupService>();
            var permissionService = serviceFactory.GetService<TableauPermissionService>();

            var projectGroups = new Dictionary<string, TableauGroup>();

            // get groups defined in project permissions as well as default permissions for datasources, workbooks
            var permissions = new List<TableauPermission>();
            permissions.Add(
                await permissionService.GetPermissionsAsync(
                    ResourceType.Project, projectId
                ).ConfigureAwait(false)
            );
            permissions.Add(
                await permissionService.GetDefaultPermissionsAsync(
                    projectId, ResourceType.Datasource
                ).ConfigureAwait(false)
            );
            permissions.Add(
                await permissionService.GetDefaultPermissionsAsync(
                    projectId, ResourceType.Workbook
                ).ConfigureAwait(false)
            );

            foreach (var permission in permissions)
            {
                if (permission.GranteeCapabilities == null)
                {
                    continue;
                }
                foreach (var granteeCapability in permission.GranteeCapabilities)
                {
                    if (granteeCapability.GranteeType == GranteeType.Group)
                    {
                        var group = await groupService.GetGroupAsync(granteeCapability.GranteeId).ConfigureAwait(false);
                        projectGroups[granteeCapability.GranteeId] = group;
                    }
                }
            }

            return projectGroups;
        }

        private static async Task<Dictionary<string, TableauUser>> GetGroupUsersWithDomain(
            string groupId,
            ITableauServiceFactory serviceFactory
        )
        {
            var groupUsers = new Dictionary<string, TableauUser>();

            var userService = serviceFactory.GetService<TableauUserService>();
            var users = await userService.GetUsersAsync(groupId).ConfigureAwait(false);
            foreach (var user in users)
            {
                // get all users in group individually so user's domain is included if specified (slandron)
                groupUsers.Add(user.Id, await userService.GetUserAsync(user.Id).ConfigureAwait(false));
            }
            return groupUsers;
        }

        private static async Task<Dictionary<string, TableauDatasource>> GetProjectDatasourcesAsync(
            string projectId,
            ITableauServiceFactory serviceFactory
        )
        {
            var datasourceService = serviceFactory.GetService<TableauDatasourceService>();

            var projectDatasources = new Dictionary<string, TableauDatasource>();

            var datasources = await datasourceService.GetDatasourcesAsync().ConfigureAwait(false);
            return datasources.Where(d => d.Project.Id == projectId).ToDictionary(w => w.Id, w => w);
        }

        private static async Task<Dictionary<string, TableauWorkbook>> GetProjectWorkbooksAsync(
            string projectId,
            ITableauServiceFactory serviceFactory
        )
        {
            var workbookService = serviceFactory.GetService<TableauWorkbookService>();

            var projectWorkbooks = new Dictionary<string, TableauWorkbook>();

            var workbooks = await workbookService.GetWorkbooksAsync().ConfigureAwait(false);
            return workbooks.Where(w => w.Project.Id == projectId).ToDictionary(w => w.Id, w => w);
        }

        private async Task MigrateProjectUsersAsync(TableauProject sourceProject, TableauProject destProject)
        {
            _logger?.Debug($"Migrating users for project {sourceProject.Name}");

            var destUserService = _tableauServiceFactoryDest.GetService<TableauUserService>();
            var sourceUsers = await GetProjectUsersWithDomain(
                    sourceProject.Id, _tableauServiceFactorySource
                ).ConfigureAwait(false);

            foreach (var sourceUser in sourceUsers.Values)
            {
                var destUser = await destUserService.FindUserAsync(
                    sourceUser.Name, sourceUser.Domain?.Name
                ).ConfigureAwait(false);
                if (destUser != null)
                {
                    _logger?.Debug($"User {sourceUser.UserPrincipalName} found in destination site");
                    continue;
                }

                _logger?.Debug($"Creating user {sourceUser.UserPrincipalName}");

                if (!_settingsMonitor.CurrentValue.DryRun)
                {
                    try
                    {
                        var newUser = await destUserService.AddUserToSiteAsync(
                            sourceUser.DownLevelLogonName, sourceUser.SiteRole
                        ).ConfigureAwait(false);

                        _logger?.Debug($"Created user {newUser.UserPrincipalName} ({newUser.Id})");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Unable to create user: {ex.Message}");
                    }
                }
            }

            // allow Tableau api to catch up to db changes
            await Task.Delay(1000).ConfigureAwait(false);
        }

        private async Task MigrateProjectGroupsAsync(TableauProject sourceProject, TableauProject destProject)
        {
            _logger?.Debug($"Migrating groups for project {sourceProject.Name}");

            var sourceGroups = await GetProjectGroups(
                sourceProject.Id, _tableauServiceFactorySource
            ).ConfigureAwait(false);

            var destUserService = _tableauServiceFactoryDest.GetService<TableauUserService>();
            var destGroupService = _tableauServiceFactoryDest.GetService<TableauGroupService>();

            foreach (var sourceGroup in sourceGroups.Values)
            {
                var destGroup = await destGroupService.FindGroupAsync(sourceGroup.Name).ConfigureAwait(false);
                if (destGroup == null)
                {
                    _logger?.Debug($"Creating group {sourceGroup.Name} in destination site");

                    if (!_settingsMonitor.CurrentValue.DryRun)
                    {
                        destGroup = await destGroupService.CreateGroupAsync(sourceGroup.Name).ConfigureAwait(false);
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }
                else
                {
                    _logger?.Warning($"Group {sourceGroup.Name} found in destination site ({destGroup.Id})");
                }
                // make sure dest group membership matches source
                var sourceGroupUsers = await GetGroupUsersWithDomain(
                    sourceGroup.Id, _tableauServiceFactorySource
                ).ConfigureAwait(false);
                var destGroupUsers = await GetGroupUsersWithDomain(
                    destGroup.Id, _tableauServiceFactoryDest
                ).ConfigureAwait(false);
                foreach (var sourceGroupUser in sourceGroupUsers.Values)
                {
                    if (
                        !destGroupUsers.Values.Any(
                            u => u.UserPrincipalName.EqualsIgnoreCase(sourceGroupUser.UserPrincipalName)
                        )
                    )
                    {
                        var destGroupUser = await destUserService.FindUserAsync(
                            sourceGroupUser.Name, sourceGroupUser.Domain?.Name
                        ).ConfigureAwait(false);

                        if (destGroupUser == null)
                        {
                            var destUser = await destUserService.FindUserAsync(
                                sourceGroupUser.Name, sourceGroupUser.Domain?.Name
                            ).ConfigureAwait(false);
                            if (destUser == null)
                            {
                                _logger?.Warning(
                                    "User {0} not found in destination, not added to group {1}",
                                    sourceGroupUser.DownLevelLogonName,
                                    destGroup.Name
                                );
                                continue;
                            }
                        }

                        _logger?.Debug($"Adding user {destGroupUser.UserPrincipalName} to {destGroup.Name}");
                        if (!_settingsMonitor.CurrentValue.DryRun)
                        {
                            await destGroupService.AddUserToGroupAsync(
                                destGroup.Id, destGroupUser.Id
                            ).ConfigureAwait(false);

                            _logger?.Debug($"Added user {destGroupUser.UserPrincipalName} to {destGroup.Name}");
                        }
                    }
                }
            }
        }

        private async Task MigrateProjectPermissionsAsync(TableauProject sourceProject, TableauProject destProject)
        {
            _logger?.Debug($"Migrating permissions for project {sourceProject.Name}");

            var sourcePermissionService = _tableauServiceFactorySource.GetService<TableauPermissionService>();
            var destPermissionService = _tableauServiceFactoryDest.GetService<TableauPermissionService>();
            var sourceUserService = _tableauServiceFactorySource.GetService<TableauUserService>();
            var destUserService = _tableauServiceFactoryDest.GetService<TableauUserService>();
            var sourceGroupService = _tableauServiceFactorySource.GetService<TableauGroupService>();
            var destGroupService = _tableauServiceFactoryDest.GetService<TableauGroupService>();

            var sourcePermissions = await sourcePermissionService.GetPermissionsAsync(
                ResourceType.Project, sourceProject.Id
            ).ConfigureAwait(false);

            // pre-load all destination groups to facilitate searching by name
            var destGroups = await destGroupService.GetGroupsAsync().ConfigureAwait(false);

            if (sourcePermissions.GranteeCapabilities == null)
            {
                _logger?.Debug("No granteee capabilities found");
                return;
            }

            foreach (var granteeCapability in sourcePermissions.GranteeCapabilities)
            {
                var destGranteeId = "";
                var destGranteeName = "";
                if (granteeCapability.GranteeType == GranteeType.User)
                {
                    var sourceUser = await sourceUserService.GetUserAsync(
                        granteeCapability.GranteeId
                    ).ConfigureAwait(false);
                    var destUser = await destUserService.FindUserAsync(
                        sourceUser.Name, sourceUser.Domain?.Name
                    ).ConfigureAwait(false);
                    destGranteeId = destUser.Id;
                    destGranteeName = destUser.UserPrincipalName;
                }
                else if (granteeCapability.GranteeType == GranteeType.Group)
                {
                    var sourceGroup = await sourceGroupService.GetGroupAsync(
                        granteeCapability.GranteeId
                    ).ConfigureAwait(false);
                    if (!destGroups.Any(g => g.Name.EqualsIgnoreCase(sourceGroup.Name)))
                    {
                        _logger?.Fatal($"Destination group not found: {sourceGroup.Name}");
                        throw new Exception($"Destination group not found: {sourceGroup.Name}");
                    }
                    var destGroup = destGroups.Single(g => g.Name.EqualsIgnoreCase(sourceGroup.Name));
                    destGranteeId = destGroup.Id;
                    destGranteeName = destGroup.Name;
                }

                foreach (var capability in granteeCapability.Capabilities.Capability)
                {
                    _logger?.Debug(
                        String.Format(
                            "Adding permission for {0} {1}: {2} {3}",
                            granteeCapability.GranteeType.ToString(),
                            destGranteeName,
                            capability.Mode,
                            capability.Name
                        )
                    );

                    if (!_settingsMonitor.CurrentValue.DryRun)
                    {
                        await destPermissionService.AddCapabilityAsync(
                            ResourceType.Project,
                            destProject.Id,
                            granteeCapability.GranteeType,
                            destGranteeId,
                            (CapabilityName)Enum.Parse(typeof(CapabilityName), capability.Name, true),
                            (CapabilityMode)Enum.Parse(typeof(CapabilityMode), capability.Mode, true)
                        ).ConfigureAwait(false);

                        _logger?.Debug("Added permission");
                    }
                }
            }
        }

        private async Task MigrateProjectDefaultPermissionsAsync(
            TableauProject sourceProject,
            TableauProject destProject,
            ResourceType resourceType
        )
        {
            _logger?.Debug($"Migrating default {resourceType.ToString()} permissions for project {sourceProject.Name}");

            var sourcePermissionService = _tableauServiceFactorySource.GetService<TableauPermissionService>();
            var destPermissionService = _tableauServiceFactoryDest.GetService<TableauPermissionService>();
            var sourceUserService = _tableauServiceFactorySource.GetService<TableauUserService>();
            var destUserService = _tableauServiceFactoryDest.GetService<TableauUserService>();
            var sourceGroupService = _tableauServiceFactorySource.GetService<TableauGroupService>();
            var destGroupService = _tableauServiceFactoryDest.GetService<TableauGroupService>();

            var sourcePermissions = await sourcePermissionService.GetDefaultPermissionsAsync(
                sourceProject.Id, resourceType
            ).ConfigureAwait(false);

            // pre-load all destination to faciliate searching by name
            var destGroups = await destGroupService.GetGroupsAsync().ConfigureAwait(false);

            if (sourcePermissions.GranteeCapabilities == null)
            {
                _logger?.Debug("No granteee capabilities found");
                return;
            }

            foreach (var granteeCapability in sourcePermissions.GranteeCapabilities)
            {
                var destGranteeId = "";
                var destGranteeName = "";
                if (granteeCapability.GranteeType == GranteeType.User)
                {
                    var sourceUser = await sourceUserService.GetUserAsync(
                        granteeCapability.GranteeId
                    ).ConfigureAwait(false);
                    var destUser = await destUserService.FindUserAsync(
                        sourceUser.Name, sourceUser.Domain?.Name
                    ).ConfigureAwait(false);
                    destGranteeId = destUser.Id;
                    destGranteeName = destUser.UserPrincipalName;
                }
                else if (granteeCapability.GranteeType == GranteeType.Group)
                {
                    var sourceGroup = await sourceGroupService.GetGroupAsync(
                        granteeCapability.GranteeId
                    ).ConfigureAwait(false);
                    var destGroup = destGroups.Single(g => g.Name.EqualsIgnoreCase(sourceGroup.Name));
                    destGranteeId = destGroup.Id;
                    destGranteeName = destGroup.Name;
                }

                foreach (var capability in granteeCapability.Capabilities.Capability)
                {
                    _logger?.Debug(
                        String.Format(
                            "Adding default permission for {0} {1}: {2} {3}",
                            granteeCapability.GranteeType.ToString(),
                            destGranteeName,
                            capability.Mode,
                            capability.Name
                        )
                    );

                    if (!_settingsMonitor.CurrentValue.DryRun)
                    {
                        await destPermissionService.AddDefaultCapabilityAsync(
                            destProject.Id,
                            resourceType,
                            granteeCapability.GranteeType,
                            destGranteeId,
                            (CapabilityName)Enum.Parse(typeof(CapabilityName), capability.Name, true),
                            (CapabilityMode)Enum.Parse(typeof(CapabilityMode), capability.Mode, true)
                        ).ConfigureAwait(false);

                        _logger?.Debug("Added default permission");
                    }
                }
            }
        }

        private async Task MigrateProjectDatasourcesAsync(TableauProject sourceProject, TableauProject destProject)
        {
            _logger?.Debug($"Migrating datasources for project {sourceProject.Name}");

            var sourceDatasourceService = _tableauServiceFactorySource.GetService<TableauDatasourceService>();
            var destDatasourceService = _tableauServiceFactoryDest.GetService<TableauDatasourceService>();

            var sourceDatasources = await GetProjectDatasourcesAsync(
                sourceProject.Id, _tableauServiceFactorySource
            ).ConfigureAwait(false);
            await _vizDatasourceService.LoadVizDatasourceReportAsync().ConfigureAwait(false);

            foreach (var sourceDatasource in sourceDatasources.Values)
            {
                await MigrateDatasourceAsync(sourceDatasource.Id, destProject.Id).ConfigureAwait(false);
            }
        }

        private async Task<TableauDatasource> MigrateDatasourceAsync(string sourceDatasourceId, string destProjectId)
        {
            _logger?.Debug($"Migrating datasource {sourceDatasourceId}");
            var sourceDatasourceService = _tableauServiceFactorySource.GetService<TableauDatasourceService>();
            var destDatasourceService = _tableauServiceFactoryDest.GetService<TableauDatasourceService>();

            var sourceDatasource = await sourceDatasourceService.GetDatasourceAsync(
                sourceDatasourceId, true
            ).ConfigureAwait(false);

            // find viz datasource from loaded report for username and password
            var vizDatasource = _vizDatasourceService.GetVizDatasource(
                _vizDatasourceService.GetValidName(sourceDatasource.Name)
            );

            // reassign project for import to destination
            sourceDatasource.Project.Id = destProjectId;

            // populate datasource connection credentials
            sourceDatasource.ConnectionCredentials = new TableauConnectionCredentials()
            {
                Username = vizDatasource.VizConnectionDetail.Username,
                Password = vizDatasource.VizConnectionDetail.Password,
                Embed = true
            };

            // this preliminary bulk download and lookup is necessary because the datasource file can't
            // be downloaded through the rest api if the datasource has spaces in the name as of API v3.6 (2019.4)
            var datasourceBytes = _vizDatasourceService.GetVizDatasourceFile(vizDatasource.Name);
            datasourceBytes.ContentType = "application/octet-stream";
            datasourceBytes.ContentDispositionName = "tableau_datasource";

            _logger?.Debug($"Publishing datasource '{sourceDatasource.Name}'");

            if (!_settingsMonitor.CurrentValue.DryRun)
            {
                var datasource = await destDatasourceService.PublishDatasourceAsync(
                    sourceDatasource, datasourceBytes
                ).ConfigureAwait(false);
                _logger?.Debug($"Published datasource '{sourceDatasource.Name}'");
                return datasource;
            }
            return null;
        }

        private async Task DownloadProjectWorkbooksAsync(TableauProject sourceProject, TableauProject destProject)
        {
            _logger?.Debug($"Downloading workbooks for project {sourceProject.Name}");

            var sourceWorkbookService = _tableauServiceFactorySource.GetService<TableauWorkbookService>();

            var sourceWorkbooks = await GetProjectWorkbooksAsync(
                sourceProject.Id, _tableauServiceFactorySource
            ).ConfigureAwait(false);

            if (!Directory.Exists(_settingsMonitor.CurrentValue.workbookDownloadPath))
            {
                Directory.CreateDirectory(_settingsMonitor.CurrentValue.workbookDownloadPath);
            }

            foreach (var sourceWorkbook in sourceWorkbooks.Values)
            {
                _logger?.Debug($"Downloading workbook '{sourceWorkbook.Name}'");
                if (!_settingsMonitor.CurrentValue.DryRun)
                {
                    var fileName = VizDatasourceService.GetValidFileName(sourceWorkbook.Name) + ".twb";
                    var fileInfo = await sourceWorkbookService.DownloadWorkbookAsync(
                        sourceWorkbook.Id,
                        _settingsMonitor.CurrentValue.workbookDownloadPath,
                        fileName
                    ).ConfigureAwait(false);
                    _logger?.Debug($"Downloaded workbook '{sourceWorkbook.Name}' to '{fileInfo.FullName}'");
                }
            }

            _logger?.Debug($"Downloaded workbooks for project {sourceProject.Name}");
        }

        private async Task MigrateProjectWorkbooksAsync(
            TableauProject sourceProject,
            TableauProject destProject,
            IEnumerable<string> workbooksToMigrate = null
        )
        {
            _logger?.Debug(
                "Migrating workbooks for project '{0}': '{1}'",
                sourceProject.Name,
                String.Join(", ", workbooksToMigrate)
            );

            var sourceWorkbookService = _tableauServiceFactorySource.GetService<TableauWorkbookService>();
            var destWorkbookService = _tableauServiceFactoryDest.GetService<TableauWorkbookService>();

            var sourceUserService = _tableauServiceFactorySource.GetService<TableauUserService>();
            var destUserService = _tableauServiceFactoryDest.GetService<TableauUserService>();

            var sourceWorkbooks = await GetProjectWorkbooksAsync(
                sourceProject.Id, _tableauServiceFactorySource
            ).ConfigureAwait(false);

            workbooksToMigrate = workbooksToMigrate ?? Enumerable.Empty<string>();

            var workbooksToSkip = _settingsMonitor.CurrentValue.WorkbooksToSkip ?? Enumerable.Empty<string>();

            foreach (var sourceWorkbook in sourceWorkbooks.Values)
            {
                if (
                    workbooksToSkip.Contains(sourceWorkbook.Name.Trim(), StringComparer.OrdinalIgnoreCase) ||
                    (
                        !workbooksToMigrate.Contains("*") &&
                        !workbooksToMigrate.Any(w => w.EqualsIgnoreCase(sourceWorkbook.Name.Trim()))
                    )
                )
                {
                    _logger?.Debug($"Skipping workbook {sourceWorkbook.Name}");
                    continue;
                }

                _logger?.Debug($"Publishing workbook '{sourceWorkbook.Name}'");
                // get workbook metadata
                var workbook = await sourceWorkbookService.GetWorkbookAsync(
                    sourceWorkbook.Id, true
                ).ConfigureAwait(false);

                var destWorkbook = await destWorkbookService.FindWorkbookAsync(
                    sourceWorkbook.Name, false
                ).ConfigureAwait(false);

                if (destWorkbook != null)
                {
                    if (destWorkbook.Project?.Id == sourceWorkbook.Project?.Id)
                    {
                        _logger?.Debug($"Workbook '{sourceWorkbook.Name}' already exists in destination, skipping");
                        continue;
                    }
                    else
                    {
                        _logger?.Warning(
                            $"Workbook '{sourceWorkbook.Name}' found in project {destWorkbook.Project?.Id}"
                        );
                    }
                }

                var defaultDestUsername = "";
                var defaultDestDomain = "";
                var defaultDestUsernameTokens = _settingsMonitor.CurrentValue.DefaultOwnerUsername.Split('@', '\\');

                if (_settingsMonitor.CurrentValue.DefaultOwnerUsername.Contains("@"))
                {
                    defaultDestUsername = defaultDestUsernameTokens.First();
                    defaultDestDomain = defaultDestUsernameTokens.Last();
                }
                else if (_settingsMonitor.CurrentValue.DefaultOwnerUsername.Contains("\\"))
                {
                    defaultDestUsername = defaultDestUsernameTokens.Last();
                    defaultDestDomain = defaultDestUsernameTokens.First();
                }
                else
                {
                    defaultDestUsername = _settingsMonitor.CurrentValue.DefaultOwnerUsername;
                }

                var defaultDestUser = !String.IsNullOrWhiteSpace(defaultDestUsername)
                    ? await destUserService.FindUserAsync(defaultDestUsername, defaultDestDomain).ConfigureAwait(false)
                    : null;

                if (defaultDestUser == null)
                {
                    _logger?.Warning(
                        $"Default owner '{_settingsMonitor.CurrentValue.DefaultOwnerUsername}' not found in destination"
                    );
                }

                var sourceUser = await sourceUserService.GetUserAsync(workbook.Owner.Id).ConfigureAwait(false);
                var destUser = await destUserService.FindUserAsync(
                    sourceUser.Name, sourceUser.Domain?.Name
                ).ConfigureAwait(false) ?? defaultDestUser;
                
                if (destUser == null)
                {
                    _logger?.Error($"Error, owner {sourceUser.DownLevelLogonName} not found in destination");
                    throw new Exception($"Error, owner {sourceUser.DownLevelLogonName} not found in destination");
                }

                if (!_settingsMonitor.CurrentValue.DryRun)
                {
                    try
                    {
                        _logger?.Debug($"Publishing workbook '{sourceWorkbook.Name}'");
                        var newWorkbook = await MigrateWorkbookAsync(
                                    sourceWorkbook.Id, destProject.Id, destUser.Id
                                ).ConfigureAwait(false);

                        _logger?.Debug($"Published workbook '{sourceWorkbook.Name}'");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Error publishing workbook {sourceWorkbook.Name}: {ex.Message}");
                    }
                }
            }
        }

        private async Task<TableauWorkbook> MigrateWorkbookAsync(
            string sourceWorkbookId,
            string destProjectId,
            string destUserId
        )
        {
            _logger?.Debug($"Migrating workbook {sourceWorkbookId}");

            var sourceWorkbookService = _tableauServiceFactorySource.GetService<TableauWorkbookService>();
            var destWorkbookService = _tableauServiceFactoryDest.GetService<TableauWorkbookService>();

            var sourceDatasourceService = _tableauServiceFactorySource.GetService<TableauDatasourceService>();
            var destDatasourceService = _tableauServiceFactoryDest.GetService<TableauDatasourceService>();

            var sourceBaseUri = new Uri(_settingsMonitor.CurrentValue.TableauApiSettingsSource.BaseUrl);
            var destBaseUri = new Uri(_settingsMonitor.CurrentValue.TableauApiSettingsDestination.BaseUrl);

            var sourceServer = sourceBaseUri.Host;
            var destServer = destBaseUri.Host;

            await _vizDatasourceService.LoadVizDatasourceReportAsync().ConfigureAwait(false);

            // get workbook metadata
            var workbook = await sourceWorkbookService.GetWorkbookAsync(
                sourceWorkbookId, true
            ).ConfigureAwait(false);

            _logger?.Debug($"Migrating workbook {workbook.Name}");

            // reassign project and owner for import to destination
            workbook.Project.Id = destProjectId;
            workbook.Owner.Id = destUserId;

            // populate workbook connection info
            foreach (var connection in workbook.Connections)
            {
                if (connection.Type.EqualsIgnoreCase(TableauApiService.CONNECTION_TYPE_SQLPROXY))
                {
                    // published/proxy connection
                    if (!String.IsNullOrWhiteSpace(connection.ServerAddress) &&
                        !connection.ServerAddress.EqualsIgnoreCase(sourceServer) &&
                        !connection.ServerAddress.EqualsIgnoreCase("localhost"))
                    {
                        throw new Exception($"Unexpected source connection server address: {connection.ServerAddress}");
                    }

                    var datasource = await sourceDatasourceService.GetDatasourceAsync(
                        connection.Datasource.Id, true
                    ).ConfigureAwait(false);

                    var destDatasource = await destDatasourceService.FindDatasourceAsync(
                        datasource.Name, true
                    ).ConfigureAwait(false);

                    // get datasource credentials from viz datasource report (csv)
                    var vizDatasource = _vizDatasourceService.GetVizDatasource(
                        _vizDatasourceService.GetValidName(datasource.Name.Trim())
                    );

                    if (destDatasource != null)
                    {
                        _logger?.Debug($"Datasource found in destination: {destDatasource.Id}");
                    }

                    if (vizDatasource == null)
                    {
                        _logger?.Warning(
                            "Viz datasource not found for sqlproxy workbook connection {0}, skipping: {1} ({2})",
                            connection.Id,
                            _vizDatasourceService.GetValidName(datasource.Name),
                            datasource.Name
                        );
                        continue;
                    }

                    if (
                        destDatasource == null ||
                        destDatasource.Project?.Id != destProjectId ||
                        !destDatasource.Connections.Any(c => (c.ServerAddress ?? "").EqualsIgnoreCase(destServer)) &&
                        !destDatasource.Connections.Any(
                            c => (c.Username ?? "").EqualsIgnoreCase(vizDatasource.VizConnectionDetail.Username)
                        )
                    )
                    {
                        // source datasource doesn't exist in destination, publish
                        // reassign project for import to destination
                        datasource.Project.Id = destProjectId;

                        // populate datasource connection credentials
                        datasource.ConnectionCredentials = new TableauConnectionCredentials()
                        {
                            Username = vizDatasource.VizConnectionDetail.Username,
                            Password = vizDatasource.VizConnectionDetail.Password,
                            Embed = true
                        };

                        _logger?.Debug("Publish datasource:");
                        _logger?.Debug(JsonConvert.SerializeObject(datasource));

                        // get datasource file data
                        var datasourceBytes = _vizDatasourceService.GetVizDatasourceFile(vizDatasource.Name);
                        datasourceBytes.ContentType = "application/octet-stream";
                        datasourceBytes.ContentDispositionName = "tableau_datasource";

                        if (!_settingsMonitor.CurrentValue.DryRun)
                        {
                            destDatasource = await destDatasourceService.PublishDatasourceAsync(
                                datasource, datasourceBytes
                            ).ConfigureAwait(false);

                            // allow Tableau api to catch up to db changes
                            await Task.Delay(1000).ConfigureAwait(false);
                        }
                    }

                    connection.ConnectionCredentials = new TableauConnectionCredentials()
                    {
                        Username = vizDatasource.VizConnectionDetail.Username,
                        Password = vizDatasource.VizConnectionDetail.Password,
                        Embed = true
                    };
                    connection.Datasource = destDatasource;
                    connection.ServerAddress = destServer;
                }
                else if (!String.IsNullOrWhiteSpace(connection.Username))
                {
                    // embedded
                    connection.ConnectionCredentials = new TableauConnectionCredentials()
                    {
                        Username = connection.Username,
                        Password = GetConnectionPassword(connection.Username),
                        Embed = true
                    };
                }
                else
                {
                    throw new Exception("Workbook connection not sqlproxy but missing username");
                }
            }

            // get workbook file data
            var workbookBytes = await sourceWorkbookService.DownloadWorkbookBytesAsync(
                workbook.Id
            ).ConfigureAwait(false);
            workbookBytes.ContentType = "application/octet-stream";
            workbookBytes.ContentDispositionName = "tableau_workbook";

            var savePath = Path.Combine(
                _settingsMonitor.CurrentValue.workbookDownloadPath,
                VizDatasourceService.GetValidFileName(workbookBytes.FilePath).Replace(".twb", "_publish.twb")
            );
            workbookBytes.Bytes = Encoding.UTF8.GetBytes(
                UpdateWorkbookPublishedDatasourceConnections(
                    Encoding.UTF8.GetString(workbookBytes.Bytes),
                    sourceServer,
                    destServer
                )
            );

            workbookBytes.Bytes = Encoding.UTF8.GetBytes(
                UpdateWorkbookPublishedDatasourceConnections(
                    Encoding.UTF8.GetString(workbookBytes.Bytes),
                    "localhost",
                    destServer
                )
            );

            _logger?.Debug($"Saving copy of workbook to {savePath}");
            File.WriteAllBytes(savePath, workbookBytes.Bytes);

            _logger?.Debug($"Publishing workbook {workbook.Name}");

            return !_settingsMonitor.CurrentValue.DryRun
                ? await destWorkbookService.PublishWorkbookAsync(workbook, workbookBytes).ConfigureAwait(false)
                : null;
        }

    }
}