using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Serilog;
using TableauCri.Extensions;
using TableauCri.Models;
using static TableauCri.Services.TableauPermissionService;

namespace TableauCri.Services
{
    public interface ITableauPermissionService : ITableauFactoryService
    {
        /// <summary>
        /// Get permissions for specified resource
        /// </summary>
        /// <param name="resourceType"></param>
        /// <param name="resourceId"></param>
        Task<TableauPermission> GetPermissionsAsync(ResourceType resourceType, string resourceId);

        /// <summary>
        /// Get default permissions for specified project and resource
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="resourceType"></param>
        Task<TableauPermission> GetDefaultPermissionsAsync(string projectId, ResourceType resourceType);

        /// <summary>
        /// Add capability for specified resource (e.g. datasource, project, workbook) and user/group
        /// </summary>
        /// <param name="resourceType"></param>
        /// <param name="resourceId"></param>
        /// <param name="granteeType"></param>
        /// <param name="granteeId"></param>
        /// <param name="capabilityName"></param>
        /// <param name="capabilityMode"></param>
        Task<TableauPermission> AddCapabilityAsync(
            ResourceType resourceType,
            string resourceId,
            GranteeType granteeType,
            string granteeId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        );

        /// <summary>
        /// Add default capability for specified project's resource (e.g. datasource, project, workbook) and user/group
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="resourceType"></param>
        /// <param name="granteeType"></param>
        /// <param name="granteeId"></param>
        /// <param name="capabilityName"></param>
        /// <param name="capabilityMode"></param>
        Task<TableauPermission> AddDefaultCapabilityAsync(
            string projectId,
            ResourceType resourceType,
            GranteeType granteeType,
            string granteeId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        );

        /// <summary>
        /// Add default capabilities for specified project, grantee, and resource type role.  For example to grant
        /// the same permissions as changing 'None' to 'Editor' for 'Workbooks' in a project's permissions menu
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="granteeType"></param>
        /// <param name="granteeId"></param>
        /// <param name="capabilityRole"></param>
        Task<TableauPermission> AddDefaultCapabilityRolePermissionsAsync(
            string projectId,
            GranteeType granteeType,
            string granteeId,
            CapabilityRole capabilityRole
        );

        /// <summary>
        /// Add specified permission
        /// </summary>
        /// <param name="permission"></param>
        Task<TableauPermission> AddPermissionAsync(TableauPermission permission);

        /// <summary>
        /// Add default permission for specified project
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="resourceType"></param>
        /// <param name="permission"></param>
        Task<TableauPermission> AddDefaultPermissionAsync(
            string projectId,
            ResourceType resourceType,
            TableauPermission permission
        );

        /// <summary>
        /// Delete capability for specified resource (e.g. datasource, project, workbook) and user/group
        /// </summary>
        /// <param name="resourceType"></param>
        /// <param name="resourceId"></param>
        /// <param name="granteeType"></param>
        /// <param name="granteeId"></param>
        /// <param name="capabilityName"></param>
        /// <param name="capabilityMode"></param>
        Task DeleteCapabilityAsync(
            ResourceType resourceType,
            string resourceId,
            GranteeType granteeType,
            string granteeId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        );

        /// <summary>
        /// Delete default capability for specified resource (e.g. datasource, project, workbook) and user/group
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="resourceType"></param>
        /// <param name="granteeType"></param>
        /// <param name="granteeId"></param>
        /// <param name="capabilityName"></param>
        /// <param name="capabilityMode"></param>
        Task DeleteDefaultCapabilityAsync(
            string projectId,
            ResourceType resourceType,
            GranteeType granteeType,
            string granteeId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        );

        /// <summary>
        /// Delete default capabilities for specified project, grantee, and resource type role.  For example to revoke
        /// the same permissions as changing 'Editor' to 'None' for 'Workbooks' in a project's permissions menu
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="granteeType"></param>
        /// <param name="granteeId"></param>
        /// <param name="capabilityRole"></param>
        Task DeleteDefaultCapabilityRolePermissionsAsync(
            string projectId,
            GranteeType granteeType,
            string granteeId,
            CapabilityRole capabilityRole
        );

        /// <summary>
        /// Delete specified permission
        /// </summary>
        /// <param name="permission"></param>
        Task DeletePermissionAsync(TableauPermission permission);

        /// <summary>
        /// Delete default permission for specified project
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="resourceType"></param>
        /// <param name="permission"></param>
        Task DeleteDefaultPermissionAsync(string projectId, ResourceType resourceType, TableauPermission permission);
    }

    public class TableauPermissionService : ITableauPermissionService
    {
        public enum ResourceType { Datasource, Project, Workbook };
        public enum GranteeType { User, Group };

        public enum CapabilityMode { Allow, Deny };
        public enum CapabilityName
        {
            AddComment,
            ChangeHierarchy,
            ChangePermissions,
            Connect,
            Delete,
            ExportData,
            ExportImage,
            ExportXml,
            Filter,
            InheritedProjectLeader,
            ProjectLeader,
            Read,
            ShareView,
            ViewComments,
            ViewUnderlyingData,
            WebAuthoring,
            Write
        };

        public enum CapabilityRole
        {
            WorkbookViewer,
            WorkbookInteractor,
            WorkbookEditor,
            DatasourceConnector,
            DatasourceEditor
        }

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> WorkbookViewCapabilities =
            new ReadOnlyDictionary<CapabilityName, CapabilityName>(
                new Dictionary<CapabilityName, CapabilityName>()
                {
                    { CapabilityName.Read, CapabilityName.Read },
                    { CapabilityName.ExportImage, CapabilityName.ExportImage },
                    { CapabilityName.ExportData, CapabilityName.ExportData },
                    { CapabilityName.ViewComments, CapabilityName.ViewComments },
                    { CapabilityName.AddComment, CapabilityName.AddComment }
                }
            );

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> WorkbookInteractCapabilities =
            new ReadOnlyDictionary<CapabilityName, CapabilityName>(
                new Dictionary<CapabilityName, CapabilityName>()
                {
                    { CapabilityName.Filter, CapabilityName.Filter },
                    { CapabilityName.ViewUnderlyingData, CapabilityName.ViewUnderlyingData },
                    { CapabilityName.ShareView, CapabilityName.ShareView },
                    { CapabilityName.WebAuthoring, CapabilityName.WebAuthoring }
                }
            );

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> WorkbookEditCapabilities =
            new ReadOnlyDictionary<CapabilityName, CapabilityName>(
                new Dictionary<CapabilityName, CapabilityName>()
                {
                    { CapabilityName.Write, CapabilityName.Write },
                    { CapabilityName.ExportXml, CapabilityName.ExportXml },
                    { CapabilityName.ChangeHierarchy, CapabilityName.ChangeHierarchy },
                    { CapabilityName.Delete, CapabilityName.Delete },
                    { CapabilityName.ChangePermissions, CapabilityName.ChangePermissions }
                }
            );

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> WorkbookViewerRoleCapabilities =
            WorkbookViewCapabilities;

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> WorkbookInteractorRoleCapabilities =
            new ReadOnlyDictionary<CapabilityName, CapabilityName>(
                new Dictionary<CapabilityName, CapabilityName>(
                    WorkbookViewerRoleCapabilities
                        .Concat(WorkbookInteractCapabilities)
                        .ToDictionary(k => k.Key, v => v.Value)
                )
            );

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> WorkbookEditorRoleCapabilities =
            new ReadOnlyDictionary<CapabilityName, CapabilityName>(
                new Dictionary<CapabilityName, CapabilityName>(
                    WorkbookInteractorRoleCapabilities
                        .Concat(WorkbookEditCapabilities)
                        .ToDictionary(k => k.Key, v => v.Value)
                )
            );

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> DatasourceUseCapabilities =
            new ReadOnlyDictionary<CapabilityName, CapabilityName>(
                new Dictionary<CapabilityName, CapabilityName>()
                {
                    { CapabilityName.Read, CapabilityName.Read },
                    { CapabilityName.Connect, CapabilityName.Connect }
                }
            );

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> DatasourceEditCapabilities =
            new ReadOnlyDictionary<CapabilityName, CapabilityName>(
                new Dictionary<CapabilityName, CapabilityName>()
                {
                    { CapabilityName.Write, CapabilityName.Write },
                    { CapabilityName.ExportXml, CapabilityName.ExportXml },
                    { CapabilityName.Delete, CapabilityName.Delete },
                    { CapabilityName.ChangePermissions, CapabilityName.ChangePermissions }
                }
            );

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> DatasourceConnectorRoleCapabilities =
            DatasourceUseCapabilities;

        public static readonly IReadOnlyDictionary<CapabilityName, CapabilityName> DatasourceEditorRoleCapabilities =
            new ReadOnlyDictionary<CapabilityName, CapabilityName>(
                new Dictionary<CapabilityName, CapabilityName>(
                    DatasourceConnectorRoleCapabilities
                        .Concat(DatasourceEditCapabilities)
                        .ToDictionary(k => k.Key, v => v.Value)
                )
            );

        public static readonly
            IReadOnlyDictionary<CapabilityRole, IReadOnlyDictionary<CapabilityName, CapabilityName>> RoleCapabilityMap =
                new ReadOnlyDictionary<CapabilityRole, IReadOnlyDictionary<CapabilityName, CapabilityName>>(
                    new Dictionary<CapabilityRole, IReadOnlyDictionary<CapabilityName, CapabilityName>>()
                    {
                        { CapabilityRole.WorkbookViewer, WorkbookViewerRoleCapabilities },
                        { CapabilityRole.WorkbookInteractor, WorkbookInteractorRoleCapabilities },
                        { CapabilityRole.WorkbookEditor, WorkbookEditorRoleCapabilities },
                        { CapabilityRole.DatasourceConnector, DatasourceConnectorRoleCapabilities },
                        { CapabilityRole.DatasourceEditor, DatasourceEditorRoleCapabilities },
                    }
                );

        private ITableauApiService _tableauApiService = null;
        private ILogger _logger = null;

        public TableauPermissionService(ITableauApiService tableauApiService, ILogger logger)
        {
            _tableauApiService = tableauApiService;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.GetPermissionsAsync"/>
        /// </summary>
        public async Task<TableauPermission> GetPermissionsAsync(
            ResourceType resourceType,
            string resourceId
        )
        {
            _logger?.Debug($"Getting permissions for {resourceType.ToString().ToLower()} {resourceId}");
            var url = _tableauApiService.SiteUrl.AppendUri(
                $"{resourceType.ToString().ToLower()}s/{resourceId}/permissions"
            );
            var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var permissions = JsonConvert.DeserializeObject<TableauPermission>(
                responseJson.Value<JObject>("permissions").ToString(),
                new StringEnumConverter()
            );

            _logger?.Debug($"{permissions?.GranteeCapabilities?.Count().ToString() ?? "0"} capabilities found");

            return permissions;
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.GetDefaultPermissionsAsync"/>
        /// </summary>
        public async Task<TableauPermission> GetDefaultPermissionsAsync(
            string projectId,
            ResourceType resourceType
        )
        {
            _logger?.Debug($"Getting default {resourceType.ToString().ToLower()} permissions for {projectId}");
            var url = _tableauApiService.SiteUrl.AppendUri(
                $"projects/{projectId}/default-permissions/{resourceType.ToString().ToLower()}s"
            );
            var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var permissions = JsonConvert.DeserializeObject<TableauPermission>(
                responseJson.Value<JObject>("permissions").ToString(),
                new StringEnumConverter()
            );

            _logger?.Debug($"{permissions?.GranteeCapabilities?.Count().ToString() ?? "0"} capabilities found");

            return permissions;
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.AddCapabilityAsync"/>
        /// </summary>
        public async Task<TableauPermission> AddCapabilityAsync(
            ResourceType resourceType,
            string resourceId,
            GranteeType userGroupType,
            string userGroupId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        )
        {
            return await AddPermissionAsync(
                BuildPermission(
                    resourceType,
                    resourceId,
                    userGroupType,
                    userGroupId,
                    capabilityName,
                    capabilityMode
                )
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.AddDefaultCapabilityAsync"/>
        /// </summary>
        public async Task<TableauPermission> AddDefaultCapabilityAsync(
            string projectId,
            ResourceType resourceType,
            GranteeType granteeType,
            string granteeId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        )
        {
            return await AddDefaultPermissionAsync(
                projectId,
                resourceType,
                BuildDefaultPermission(granteeType, granteeId, capabilityName, capabilityMode)
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.AddDefaultCapabilityRolePermissionsAsync"/>
        /// </summary>
        public async Task<TableauPermission> AddDefaultCapabilityRolePermissionsAsync(
            string projectId,
            GranteeType granteeType,
            string granteeId,
            CapabilityRole capabilityRole
        )
        {
            // collate capabilities for specified role into single unified permission
            var permissions = new List<TableauPermission>();
            var capabilityMode = CapabilityMode.Allow;
            var capDic = RoleCapabilityMap[capabilityRole];
            var capDicKeys = capDic.Keys;
            foreach (var capabilityName in RoleCapabilityMap[capabilityRole].Keys)
            {
                permissions.Add(BuildDefaultPermission(granteeType, granteeId, capabilityName, capabilityMode));
            }
            var capabilities = permissions.SelectMany(
                p => p.GranteeCapabilities.First().Capabilities.Capability
            ).ToArray();

            var permission = permissions.First();
            permission.GranteeCapabilities.First().Capabilities.Capability = capabilities;

            var resourceType = Enum.GetValues(typeof(ResourceType))
                .Cast<ResourceType>()
                .Single(r => capabilityRole.ToString().StartsWith(r.ToString(), StringComparison.OrdinalIgnoreCase));

            return await AddDefaultPermissionAsync(projectId, resourceType, permission).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.AddPermissionAsync"/>
        /// </summary>
        public async Task<TableauPermission> AddPermissionAsync(TableauPermission permission)
        {
            _logger?.Debug($"Adding permission");
            var resourceTypeName = "";
            var resourceId = "";
            if (permission.Datasource != null)
            {
                resourceTypeName = ResourceType.Datasource.ToString().ToLower();
                resourceId = permission.Datasource.Id;
            }
            else if (permission.Project != null)
            {
                resourceTypeName = ResourceType.Project.ToString().ToLower();
                resourceId = permission.Project.Id;
            }
            else if (permission.Workbook != null)
            {
                resourceTypeName = ResourceType.Workbook.ToString().ToLower();
                resourceId = permission.Workbook.Id;
            }
            else
            {
                throw new Exception("Unsupported resource/object type");
            }

            foreach (var granteeCapability in permission.GranteeCapabilities)
            {
                var granteeTypeName = "";
                var granteeId = "";
                if (granteeCapability.Group != null)
                {
                    granteeTypeName = GranteeType.Group.ToString().ToLower();
                    granteeId = granteeCapability.Group.Id;
                }
                else if (granteeCapability.User != null)
                {
                    granteeTypeName = GranteeType.User.ToString().ToLower();
                    granteeId = granteeCapability.User.Id;
                }
                else
                {
                    throw new Exception("Unsupported grantee type");
                }

                foreach (var capability in granteeCapability.Capabilities.Capability)
                {
                    _logger?.Debug(
                        String.Format(
                            "Adding permission for {0} {1} to {2} {3}: {4} {5}",
                            resourceTypeName,
                            resourceId,
                            granteeTypeName,
                            granteeId,
                            capability.Mode,
                            capability.Name
                        )
                    );
                }
            }

            var url = _tableauApiService.SiteUrl.AppendUri($"{resourceTypeName}s/{resourceId}/permissions");

            var requestJson = new JObject();
            requestJson["permissions"] = JToken.Parse(permission.ToRequestString());

            var responseString = await _tableauApiService.SendPutAsync(url, requestJson.ToString()).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var newPermission = JsonConvert.DeserializeObject<TableauPermission>(
                responseJson.Value<JObject>("permissions").ToString()
            );

            _logger?.Debug($"Permission added");

            return newPermission;
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.AddDefaultPermissionAsync"/>
        /// </summary>
        public async Task<TableauPermission> AddDefaultPermissionAsync(
            string projectId,
            ResourceType resourceType,
            TableauPermission permission
        )
        {
            _logger?.Debug($"Adding default {resourceType.ToString().ToLower()} permission for project {projectId}");

            foreach (var granteeCapability in permission.GranteeCapabilities)
            {
                var granteeTypeName = "";
                var granteeId = "";
                if (granteeCapability.Group != null)
                {
                    granteeTypeName = GranteeType.Group.ToString().ToLower();
                    granteeId = granteeCapability.Group.Id;
                }
                else if (granteeCapability.User != null)
                {
                    granteeTypeName = GranteeType.User.ToString().ToLower();
                    granteeId = granteeCapability.User.Id;
                }
                else
                {
                    throw new Exception("Unsupported grantee type");
                }

                foreach (var capability in granteeCapability.Capabilities.Capability)
                {
                    _logger?.Debug(
                        String.Format(
                            "Adding default {0} permission on {1} for {2} {3}: {4} {5}",
                            resourceType.ToString().ToLower(),
                            projectId,
                            granteeTypeName,
                            granteeId,
                            capability.Mode,
                            capability.Name
                        )
                    );
                }
            }

            var url = _tableauApiService.SiteUrl.AppendUri(
                $"projects/{projectId}/default-permissions/{resourceType.ToString().ToLower()}s/"
            );

            var requestJson = new JObject();
            requestJson["permissions"] = JToken.Parse(permission.ToRequestString());

            var responseString = await _tableauApiService.SendPutAsync(
                url, requestJson.ToString()
            ).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var newPermission = JsonConvert.DeserializeObject<TableauPermission>(
                responseJson.Value<JObject>("permissions").ToString()
            );

            _logger?.Debug($"Default permission added");

            return newPermission;
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.DeleteCapabilityAsync"/>
        /// </summary>
        public async Task DeleteCapabilityAsync(
            ResourceType resourceType,
            string resourceId,
            GranteeType userGroupType,
            string userGroupId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        )
        {
            await DeletePermissionAsync(
                BuildPermission(
                    resourceType,
                    resourceId,
                    userGroupType,
                    userGroupId,
                    capabilityName,
                    capabilityMode
                )
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.DeleteDefaultCapabilityAsync"/>
        /// </summary>
        public async Task DeleteDefaultCapabilityAsync(
            string projectId,
            ResourceType resourceType,
            GranteeType granteeType,
            string granteeId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        )
        {
            await DeleteDefaultPermissionAsync(
                projectId,
                resourceType,
                BuildDefaultPermission(granteeType, granteeId, capabilityName, capabilityMode)
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.DeleteDefaultCapabilityRolePermissionsAsync"/>
        /// </summary>
        public async Task DeleteDefaultCapabilityRolePermissionsAsync(
            string projectId,
            GranteeType granteeType,
            string granteeId,
            CapabilityRole capabilityRole
        )
        {
            // collate capabilities for specified role into single unified permission
            var permissions = new List<TableauPermission>();
            var capabilityMode = CapabilityMode.Allow;
            foreach (var capabilityName in RoleCapabilityMap[capabilityRole].Keys)
            {
                permissions.Add(BuildDefaultPermission(granteeType, granteeId, capabilityName, capabilityMode));
            }
            var capabilities = permissions.SelectMany(
                p => p.GranteeCapabilities.First().Capabilities.Capability
            ).ToArray();

            var permission = permissions.First();
            permission.GranteeCapabilities.First().Capabilities.Capability = capabilities;

            var resourceType = Enum.GetValues(typeof(ResourceType))
                .Cast<ResourceType>()
                .Single(r => capabilityRole.ToString().StartsWith(r.ToString(), StringComparison.OrdinalIgnoreCase));

            await DeleteDefaultPermissionAsync(projectId, resourceType, permission).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.DeletePermissionAsync"/>
        /// </summary>
        public async Task DeletePermissionAsync(TableauPermission permission)
        {
            _logger?.Debug($"Deleting permission");
            var resourceTypeName = "";
            var resourceId = "";
            if (permission.Datasource != null)
            {
                resourceTypeName = ResourceType.Datasource.ToString().ToLower();
                resourceId = permission.Datasource.Id;
            }
            else if (permission.Project != null)
            {
                resourceTypeName = ResourceType.Project.ToString().ToLower();
                resourceId = permission.Project.Id;
            }
            else if (permission.Workbook != null)
            {
                resourceTypeName = ResourceType.Workbook.ToString().ToLower();
                resourceId = permission.Workbook.Id;
            }

            foreach (var granteeCapability in permission.GranteeCapabilities)
            {
                var granteeTypeName = "";
                var granteeId = "";
                if (granteeCapability.Group != null)
                {
                    granteeTypeName = GranteeType.Group.ToString().ToLower();
                    granteeId = granteeCapability.Group.Id;
                }
                else if (granteeCapability.User != null)
                {
                    granteeTypeName = GranteeType.User.ToString().ToLower();
                    granteeId = granteeCapability.User.Id;
                }
                else
                {
                    throw new Exception("Unsupported resource/object type");
                }

                foreach (var capability in granteeCapability.Capabilities.Capability)
                {
                    _logger?.Debug(
                        String.Format(
                            "Deleting permission for {0} {1} to {2} {3}: {4} {5}",
                            resourceTypeName,
                            resourceId,
                            granteeTypeName,
                            granteeId,
                            capability.Mode,
                            capability.Name
                        )
                    );

                    var url = _tableauApiService.SiteUrl.AppendUri(
                        $"{resourceTypeName}s/{resourceId}/permissions/{granteeTypeName}s/{granteeId}/" +
                        $"{capability.Name}/{capability.Mode}"
                    );

                    try
                    {
                        var responseString = await _tableauApiService.SendDeleteAsync(url).ConfigureAwait(false);
                        _logger?.Debug($"Response: {responseString}");
                        _logger?.Debug($"Permission deleted");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Error deleting permission: {ex.ToString()}");
                    }

                }
            }

            _logger?.Debug($"Permission deleted");
        }

        /// <summary>
        /// <see cref="ITableauPermissionService.DeleteDefaultPermissionAsync"/>
        /// </summary>
        public async Task DeleteDefaultPermissionAsync(
            string projectId,
            ResourceType resourceType,
            TableauPermission permission
        )
        {
            _logger?.Debug($"Deleting default {resourceType.ToString().ToLower()} permission for project {projectId}");

            foreach (var granteeCapability in permission.GranteeCapabilities)
            {
                var granteeTypeName = "";
                var granteeId = "";
                if (granteeCapability.Group != null)
                {
                    granteeTypeName = GranteeType.Group.ToString().ToLower();
                    granteeId = granteeCapability.Group.Id;
                }
                else if (granteeCapability.User != null)
                {
                    granteeTypeName = GranteeType.User.ToString().ToLower();
                    granteeId = granteeCapability.User.Id;
                }
                else
                {
                    throw new Exception("Unsupported resource/object type");
                }

                foreach (var capability in granteeCapability.Capabilities.Capability)
                {
                    _logger?.Debug(
                        String.Format(
                            "Deleting default {0} permission on {1} for {2} {3}: {4} {5}",
                            resourceType.ToString().ToLower(),
                            projectId,
                            granteeTypeName,
                            granteeId,
                            capability.Mode,
                            capability.Name
                        )
                    );

                    var url = _tableauApiService.SiteUrl.AppendUri(
                        $"projects/{projectId}/default-permissions/" +
                        $"{resourceType.ToString().ToLower()}s/" +
                        $"{granteeTypeName}s/{granteeId}/" +
                        $"{capability.Name}/{capability.Mode}"
                    );

                    try
                    {
                        var responseString = await _tableauApiService.SendDeleteAsync(url).ConfigureAwait(false);
                        _logger?.Debug($"Response: {responseString}");
                        _logger?.Debug($"Default permission deleted");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Error deleting default permission: {ex.ToString()}");
                    }

                }
            }

            _logger?.Debug($"Default permission deleted");
        }

        /// <summary>
        /// Build permission for specified resource (e.g. datasource, project, workbook), user/group, and capability
        /// </summary>
        /// <param name="resourceType"></param>
        /// <param name="resourceId"></param>
        /// <param name="granteeType"></param>
        /// <param name="granteeId"></param>
        /// <param name="capabilityName"></param>
        /// <param name="capabilityMode"></param>
        public static TableauPermission BuildPermission(
            ResourceType resourceType,
            string resourceId,
            GranteeType granteeType,
            string granteeId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        )
        {
            var permission = BuildDefaultPermission(granteeType, granteeId, capabilityName, capabilityMode);

            switch (resourceType)
            {
                case ResourceType.Datasource:
                    permission.Datasource = new TableauDatasource() { Id = resourceId };
                    break;

                case ResourceType.Project:
                    permission.Project = new TableauProject() { Id = resourceId };
                    break;

                case ResourceType.Workbook:
                    permission.Workbook = new TableauWorkbook() { Id = resourceId };
                    break;

                default:
                    // default permission
                    break;
            }

            return permission;
        }

        /// <summary>
        /// Build permission for specified resource (e.g. datasource, project, workbook), user/group, and capability
        /// </summary>
        /// <param name="granteeType"></param>
        /// <param name="granteeId"></param>
        /// <param name="capabilityName"></param>
        /// <param name="capabilityMode"></param>
        public static TableauPermission BuildDefaultPermission(
            GranteeType granteeType,
            string granteeId,
            CapabilityName capabilityName,
            CapabilityMode capabilityMode
        )
        {
            var permission = new TableauPermission();

            var granteeCapabilities = new TableauGranteeCapabilities();
            switch (granteeType)
            {
                case GranteeType.Group:
                    granteeCapabilities.Group = new TableauGroup() { Id = granteeId };
                    break;

                case GranteeType.User:
                    granteeCapabilities.User = new TableauUser() { Id = granteeId };
                    break;

                default:
                    throw new Exception("Unsupported user/group type");
            }
            granteeCapabilities.Capabilities = new TableauCapabilities()
            {
                Capability = new TableauCapability[] {
                    new TableauCapability()
                    {
                        Name = capabilityName.ToString(),
                        Mode = capabilityMode.ToString()
                    }
                }
            };

            permission.GranteeCapabilities = new TableauGranteeCapabilities[] { granteeCapabilities };

            return permission;
        }


    }
}