using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Serilog;
using TableauCri.Extensions;
using TableauCri.Models;
using static TableauCri.Services.TableauApiService;

namespace TableauCri.Services
{
    public interface ITableauUserService : ITableauFactoryService
    {
        /// <summary>
        /// Get users for current auth session site, optionally limited to members of specified group
        /// </summary>
        /// <param name="groupId"></param>
        Task<IEnumerable<TableauUser>> GetUsersAsync(string groupId = null);

        /// <summary>
        /// Get user with specified id
        /// </summary>
        /// <param name="id"></param>
        Task<TableauUser> GetUserAsync(string id);

        /// <summary>
        /// Find users with specified name and optional domain (FQDN)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="domain"></param>
        Task<IEnumerable<TableauUser>> FindUsersAsync(string name, string domain = null);

        /// <summary>
        /// Find user with specified name and optional domain (FQDN)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="domain"></param>
        Task<TableauUser> FindUserAsync(string name, string domain = null);

        /// <summary>
        /// Add user with specified name to current auth session site
        /// </summary>
        /// <param name="name"></param>
        /// <param name="siteRole"></param>
        Task<TableauUser> AddUserToSiteAsync(string name, string siteRole);

        /// <summary>
        /// Remove user with specified id from current auth session site
        /// </summary>
        /// <param name="id"></param>
        Task RemoveUserFromSiteAsync(string id);
    }

    public class TableauUserService : ITableauUserService
    {
        private ITableauApiService _tableauApiService = null;
        private ILogger _logger = null;

        public TableauUserService(ITableauApiService tableauApiService, ILogger logger)
        {
            _tableauApiService = tableauApiService;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="ITableauUserService.GetUsersAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauUser>> GetUsersAsync(string groupId = null)
        {
            _logger?.Debug($"Getting users{(!String.IsNullOrWhiteSpace(groupId) ? $" in group {groupId}" : "")}");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var users = new List<TableauUser>();

            do
            {
                var url = String.IsNullOrWhiteSpace(groupId)
                    ? _tableauApiService.SiteUrl.AppendUri("users")
                    : _tableauApiService.SiteUrl.AppendUri($"groups/{groupId}/users");
                url += $"?pageSize={pageSize}&pageNumber={pageNumber}";

                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("users").ContainsKey("user"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageUsers = JsonConvert.DeserializeObject<List<TableauUser>>(
                    responseJson.Value<JObject>("users").Value<JArray>("user").ToString(),
                    new StringEnumConverter()
                );
                users.AddRange(pageUsers);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            users.ForEach(
                u =>
                {
                    u.SiteId = _tableauApiService.SiteId;
                    u.ApiVersion = _tableauApiService.ApiVersion;
                }
            );

            _logger?.Debug($"{users.Count} users returned");

            return users;
        }

        /// <summary>
        /// <see cref="ITableauUserService.GetUserAsync"/>
        /// </summary>
        public async Task<TableauUser> GetUserAsync(string id)
        {
            _logger?.Debug($"Getting user {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"users/{id}");

            var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var user = JsonConvert.DeserializeObject<TableauUser>(
                responseJson.Value<JObject>("user").ToString()
            );

            user.SiteId = _tableauApiService.SiteId;
            user.ApiVersion = _tableauApiService.ApiVersion;

            _logger?.Debug($"User {id} ({user.Name}, {user.FullName}) returned");

            return user;
        }

        /// <summary>
        /// <see cref="ITableauUserService.FindUsersAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauUser>> FindUsersAsync(string name, string domain = null)
        {
            _logger?.Debug($"Finding users: {name}");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var users = new List<TableauUser>();
            
            do
            {
                var queryFilter = "filter=" + _tableauApiService.BuildQueryFilter("name", QueryFilterOperator.eq, name);
                var url = _tableauApiService.SiteUrl.AppendUri(
                    $"users?pageSize={pageSize}&pageNumber={pageNumber}&{queryFilter}"
                );
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);

                if (!responseJson.Value<JObject>("users").ContainsKey("user"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageUsers = JsonConvert.DeserializeObject<List<TableauUser>>(
                    responseJson.Value<JObject>("users").Value<JArray>("user").ToString(),
                    new StringEnumConverter()
                );
                users.AddRange(pageUsers);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while(totalRetrieved < totalAvailable);

            // domain is only returned when performing direct query for user's id
            if (!String.IsNullOrWhiteSpace(domain))
            {
                var detailedUsers = new List<TableauUser>();
                foreach (var user in users)
                {
                    var detailedUser = await GetUserAsync(user.Id).ConfigureAwait(false);
                    if ((detailedUser.Domain?.Name ?? "").Equals(domain, StringComparison.OrdinalIgnoreCase))
                    {
                        detailedUsers.Add(detailedUser);
                    }
                }
                users = detailedUsers;
            }

            users.ForEach(
                u =>
                {
                    u.SiteId = _tableauApiService.SiteId;
                    u.ApiVersion = _tableauApiService.ApiVersion;
                }
            );

            _logger?.Debug($"{users.Count} matching user(s) found");

            return users;
        }

        /// <summary>
        /// <see cref="ITableauUserService.FindUserAsync"/>
        /// </summary>
        public async Task<TableauUser> FindUserAsync(string name, string domain = null)
        {
            _logger?.Debug($"Finding user: {name}");

            var users = await FindUsersAsync(name, domain).ConfigureAwait(false);

            _logger?.Debug($"{users.Count()} matching users found");

            return users.SingleOrDefault();
        }

        /// <summary>
        /// <see cref="ITableauUserService.AddUserToSiteAsync"/>
        /// </summary>
        public async Task<TableauUser> AddUserToSiteAsync(string name, string siteRole)
        {
            _logger?.Debug($"Adding user {name} with role {siteRole}");

            var url = _tableauApiService.SiteUrl.AppendUri($"users");
            var user = new TableauUser() { Name = name, SiteRole = siteRole };

            var requestJson = new JObject();
            requestJson["user"] = JToken.Parse(user.ToRequestString());

            var responseString = await _tableauApiService.SendPostAsync(url, requestJson.ToString()).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            user = JsonConvert.DeserializeObject<TableauUser>(
                responseJson.Value<JObject>("user").ToString()
            );

            _logger?.Debug($"User {user.Id} added as {siteRole}");

            return user;
        }

        /// <summary>
        /// <see cref="ITableauUserService.RemoveUserFromSiteAsync"/>
        /// </summary>
        public async Task RemoveUserFromSiteAsync(string id)
        {
            _logger?.Debug($"Removing user {id} from site {_tableauApiService.SiteId}");

            var url = _tableauApiService.SiteUrl.AppendUri($"users/{id}");

            var responseString = await _tableauApiService.SendDeleteAsync(url).ConfigureAwait(false);
            _logger?.Debug($"Response: {responseString}");

            _logger?.Debug($"User {id} removed from site {_tableauApiService.SiteId}");
        }


    }
}