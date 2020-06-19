using System.Collections.Generic;
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
    public interface ITableauProjectService : ITableauFactoryService
    {
        /// <summary>
        /// Get projects for current auth session site
        /// </summary>
        Task<IEnumerable<TableauProject>> GetProjectsAsync();

        /// <summary>
        /// Find project with specified name
        /// </summary>
        /// <param name="name"></param>
        Task<TableauProject> FindProjectAsync(string name);

        /// <summary>
        /// Create project with specified name and optional description and parent project in current auth session site
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="parentProjectId"></param>
        Task<TableauProject> CreateProjectAsync(string name, string description = null, string parentProjectId = null);

        /// <summary>
        /// Delete project with specified id
        /// </summary>
        /// <param name="id"></param>
        Task DeleteProjectAsync(string id);
    }

    public class TableauProjectService : ITableauProjectService
    {
        private ITableauApiService _tableauApiService = null;
        private ILogger _logger = null;

        public TableauProjectService(ITableauApiService tableauApiService, ILogger logger)
        {
            _tableauApiService = tableauApiService;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="ITableauProjectService.GetProjectsAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauProject>> GetProjectsAsync()
        {
            _logger?.Debug("Getting projects");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var projects = new List<TableauProject>();

            do
            {
                var url = _tableauApiService.SiteUrl.AppendUri(
                    $"projects?pageSize={pageSize}&pageNumber={pageNumber}"
                );
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("projects").ContainsKey("project"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageProjects = JsonConvert.DeserializeObject<List<TableauProject>>(
                    responseJson.Value<JObject>("projects").Value<JArray>("project").ToString()
                );
                projects.AddRange(pageProjects);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            projects.ForEach(
                p =>
                {
                    p.SiteId = _tableauApiService.SiteId;
                    p.ApiVersion = _tableauApiService.ApiVersion;
                }
            );

            _logger?.Debug($"{projects.Count} projects returned");

            return projects;
        }

        /// <summary>
        /// <see cref="ITableauProjectService.FindProjectAsync"/>
        /// </summary>
        public async Task<TableauProject> FindProjectAsync(string name)
        {
            _logger?.Debug($"Finding project {name}");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var projects = new List<TableauProject>();

            do
            {
                var queryFilter = "filter=" + _tableauApiService.BuildQueryFilter("name", QueryFilterOperator.eq, name);
                var url = _tableauApiService.SiteUrl.AppendUri(
                    $"projects?pageSize={pageSize}&pageNumber={pageNumber}&{queryFilter}"
                );
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("projects").ContainsKey("project"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageProjects = JsonConvert.DeserializeObject<List<TableauProject>>(
                    responseJson.Value<JObject>("projects").Value<JArray>("project").ToString()
                );
                projects.AddRange(pageProjects);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            projects.ForEach(
                p =>
                {
                    p.SiteId = _tableauApiService.SiteId;
                    p.ApiVersion = _tableauApiService.ApiVersion;
                }
            );

            if (!projects.Any())
            {
                _logger?.Debug("No matching projects found");
            }
            else if (projects.Count > 1)
            {
                _logger?.Debug($"{projects.Count} matching projects found");
            }

            return projects.SingleOrDefault();
        }

        /// <summary>
        /// <see cref="ITableauProjectService.CreateProjectAsync"/>
        /// </summary>
        public async Task<TableauProject> CreateProjectAsync(
            string name,
            string description = null,
            string parentProjectId = null
        )
        {
            _logger?.Debug($"Creating project {name}");

            var url = _tableauApiService.SiteUrl.AppendUri("projects");

            var project = new TableauProject()
            {
                Name = name,
                Description = description,
                ParentProjectId = parentProjectId
            };

            var requestJson = new JObject();
            requestJson["project"] = JToken.Parse(project.ToRequestString());

            var responseString = await _tableauApiService.SendPostAsync(
                url, requestJson.ToString()
            ).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            project = JsonConvert.DeserializeObject<TableauProject>(
                responseJson.Value<JObject>("project").ToString()
            );

            _logger?.Debug($"Project {name} created with id {project.Id}");

            return project;
        }

        /// <summary>
        /// <see cref="ITableauProjectService.DeleteProjectAsync"/>
        /// </summary>
        public async Task DeleteProjectAsync(string id)
        {
            _logger?.Debug($"Deleting project {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"projects/{id}");

            var responseString = await _tableauApiService.SendDeleteAsync(url).ConfigureAwait(false);
            _logger?.Debug($"Response: {responseString}");

            _logger?.Debug($"Project {id} deleted");
        }


    }
}