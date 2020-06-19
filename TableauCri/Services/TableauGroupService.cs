using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using TableauCri.Extensions;
using TableauCri.Models;
using static TableauCri.Services.TableauApiService;

namespace TableauCri.Services
{
    public interface ITableauGroupService : ITableauFactoryService
    {
        /// <summary>
        /// Get groups for current auth session site
        /// </summary>
        Task<IEnumerable<TableauGroup>> GetGroupsAsync();

        /// <summary>
        /// Get group with specified id
        /// </summary>
        /// <param name="id"></param>
        Task<TableauGroup> GetGroupAsync(string id);

        /// <summary>
        /// Find group with specified name
        /// </summary>
        /// <param name="name"></param>
        Task<TableauGroup> FindGroupAsync(string name);

        /// <summary>
        /// Create (local) group with specified name in Tableau server
        /// </summary>
        /// <param name="name"></param>
        Task<TableauGroup> CreateGroupAsync(string name);

        /// <summary>
        /// Add user with specified id to specified group
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="userId"></param>
        Task<TableauUser> AddUserToGroupAsync(string groupId, string userId);

        /// <summary>
        /// Delete group with specified id
        /// </summary>
        /// <param name="id"></param>
        Task DeleteGroupAsync(string id);

        /// <summary>
        /// Remove user with specified id from specified group
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="userId"></param>
        Task RemoveUserFromGroupAsync(string groupId, string userId);
    }

    public class TableauGroupService : ITableauGroupService
    {
        private ITableauApiService _tableauApiService = null;
        private ILogger _logger = null;

        public TableauGroupService(ITableauApiService tableauApiService, ILogger logger)
        {
            _tableauApiService = tableauApiService;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="ITableauGroupService.GetGroupsAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauGroup>> GetGroupsAsync()
        {
            _logger?.Debug("Getting groups");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var groups = new List<TableauGroup>();

            do
            {
                var url = _tableauApiService.SiteUrl.AppendUri($"groups?pageSize={pageSize}&pageNumber={pageNumber}");
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("groups").ContainsKey("group"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageGroups = JsonConvert.DeserializeObject<List<TableauGroup>>(
                    responseJson.Value<JObject>("groups").Value<JArray>("group").ToString()
                );
                groups.AddRange(pageGroups);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            groups.ForEach(
                g =>
                {
                    g.SiteId = _tableauApiService.SiteId;
                    g.ApiVersion = _tableauApiService.ApiVersion;
                }
            );
            
            _logger?.Debug($"{groups.Count} groups returned");

            return groups;
        }

        /// <summary>
        /// <see cref="ITableauGroupService.GetGroupAsync"/>
        /// </summary>
        public async Task<TableauGroup> GetGroupAsync(string id)
        {
            _logger?.Debug($"Getting group {id}");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            
            do
            {
                var url = _tableauApiService.SiteUrl.AppendUri($"groups?pageSize={pageSize}&pageNumber={pageNumber}");
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("groups").ContainsKey("group"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageGroups = JsonConvert.DeserializeObject<List<TableauGroup>>(
                    responseJson.Value<JObject>("groups").Value<JArray>("group").ToString()
                );

                var group = pageGroups.SingleOrDefault(g => g.Id == id);
                if (group != null)
                {
                    group.SiteId = _tableauApiService.SiteId;
                    group.ApiVersion = _tableauApiService.ApiVersion;
                    return group;
                }

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            _logger?.Debug("Group not found");
            return null;
        }

        /// <summary>
        /// <see cref="ITableauGroupService.FindGroupAsync"/>
        /// </summary>
        public async Task<TableauGroup> FindGroupAsync(string name)
        {
            _logger?.Debug("Finding group");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;

            var queryFilter = "filter=" + _tableauApiService.BuildQueryFilter("name", QueryFilterOperator.eq, name);

            var groups = new List<TableauGroup>();

            do
            {
                var url = _tableauApiService.SiteUrl.AppendUri(
                    $"groups?pageSize={pageSize}&pageNumber={pageNumber}&{queryFilter}"
                );
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("groups").ContainsKey("group"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageGroups = JsonConvert.DeserializeObject<List<TableauGroup>>(
                    responseJson.Value<JObject>("groups").Value<JArray>("group").ToString()
                );

                var group = pageGroups.SingleOrDefault(g => g.Name.EqualsIgnoreCase(name));

                if (group != null)
                {
                    group.SiteId = _tableauApiService.SiteId;
                    group.ApiVersion = _tableauApiService.ApiVersion;
                    return group;
                }

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            _logger?.Debug("No matching groups found");

            return null;
        }

        /// <summary>
        /// <see cref="ITableauGroupService.CreateGroupAsync"/>
        /// </summary>
        public async Task<TableauGroup> CreateGroupAsync(string name)
        {
            _logger?.Debug($"Creating group {name}");

            var url = _tableauApiService.SiteUrl.AppendUri("groups");

            var group = new TableauGroup() { Name = name };

            var requestJson = new JObject();
            requestJson["group"] = JToken.Parse(group.ToRequestString());

            var responseString = await _tableauApiService.SendPostAsync(
                url, requestJson.ToString()
            ).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            group = JsonConvert.DeserializeObject<TableauGroup>(
                responseJson.Value<JObject>("group").ToString()
            );

            _logger?.Debug($"Group {name} created with id {group.Id}");

            return group;
        }
        
        /// <summary>
        /// <see cref="ITableauGroupService.AddUserToGroupAsync"/>
        /// </summary>
        public async Task<TableauUser> AddUserToGroupAsync(string groupId, string userId)
        {
            _logger?.Debug($"Adding user {userId} to group {groupId}");

            var url = _tableauApiService.SiteUrl.AppendUri($"groups/{groupId}/users");

            var user = new TableauUser() { Id = userId };

            var userJson = new JObject();
            userJson["id"] = userId;

            var requestJson = new JObject();
            requestJson["user"] = userJson;

            var responseString = await _tableauApiService.SendPostAsync(
                url, requestJson.ToString()
            ).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            user = JsonConvert.DeserializeObject<TableauUser>(
                responseJson.Value<JObject>("user").ToString()
            );

            _logger?.Debug($"User {userId} added to group {groupId}");

            return user;
        }

        /// <summary>
        /// <see cref="ITableauGroupService.DeleteGroupAsync"/>
        /// </summary>
        public async Task DeleteGroupAsync(string id)
        {
            _logger?.Debug($"Deleting group {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"groups/{id}");

            var responseString = await _tableauApiService.SendDeleteAsync(url).ConfigureAwait(false);
            _logger?.Debug($"Response: {responseString}");

            _logger?.Debug($"Group {id} deleted");
        }

        /// <summary>
        /// <see cref="ITableauGroupService.RemoveUserFromGroupAsync"/>
        /// </summary>
        public async Task RemoveUserFromGroupAsync(string groupId, string userId)
        {
            _logger?.Debug($"Removing user {userId} from group {groupId}");

            var url = _tableauApiService.SiteUrl.AppendUri($"groups/{groupId}/users/{userId}");

            var responseString = await _tableauApiService.SendDeleteAsync(url).ConfigureAwait(false);
            _logger?.Debug($"Response: {responseString}");

            _logger?.Debug($"User {userId} removed from group {groupId}");
        }
        

    }
}