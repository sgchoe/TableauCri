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
    public interface ITableauDatasourceService : ITableauFactoryService
    {
        /// <summary>
        /// Get datasources for current auth session site
        /// </summary>
        Task<IEnumerable<TableauDatasource>> GetDatasourcesAsync();

        /// <summary>
        /// Get datasource with specified id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="includeConnections"></param>
        Task<TableauDatasource> GetDatasourceAsync(string id, bool includeConnections = false);

        /// <summary>
        /// Find datasource with specified name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="includeConnections"></param>
        Task<TableauDatasource> FindDatasourceAsync(string name, bool includeCOnnection = false);

        /// <summary>
        /// Get connections for datasource with specified id
        /// </summary>
        /// <param name="id"></param>
        Task<IEnumerable<TableauConnection>> GetDatasourceConnectionsAsync(string id);

        /// <summary>
        /// Download datasource with specified id to optional save directory and filename
        /// </summary>
        /// <param name="id"></param>
        /// <param name="saveDirectory"></param>
        /// <param name="fileName"></param>
        Task<FileInfo> DownloadDatasourceAsync(string id, string saveDirectory = null, string fileName = null);

        /// <summary>
        /// Download datasource with specified id to byte array.  Left for referenc only because calls
        /// to the '.../datasources/luid/content' endpoint (v3.6/2019.4) fail with a 'not found' error
        /// if the datasource name contains whitespace.  See <see cref="VizDatasourceService"/> for an
        /// alternative implementation
        /// </summary>
        /// <param name="id"></param>
        Task<TableauFileBytes> DownloadDatasourceBytesAsync(string id);

        /// <summary>
        /// Publish specified datasource to server
        /// </summary>
        /// <param name="datasource"></param>
        /// <param name="path"></param>
        /// <param name="overwrite"></param>
        Task<TableauDatasource> PublishDatasourceAsync(
            TableauDatasource datasource,
            string path,
            bool overwrite = false
        );

        /// <summary>
        /// Publish specified datasource to server
        /// </summary>
        /// <param name="datasource"></param>
        /// <param name="fileBytes"></param>
        /// <param name="overwrite"></param>
        Task<TableauDatasource> PublishDatasourceAsync(
            TableauDatasource datasource,
            TableauFileBytes fileBytes,
            bool overwrite = false
        );

        /// <summary>
        /// Delete specified datasource
        /// </summary>
        /// <param name="id"></param>
        Task DeleteDatasourceAsync(string id);
    }

    public class TableauDatasourceService : ITableauDatasourceService
    {
        private ITableauApiService _tableauApiService = null;
        private ILogger _logger = null;

        public TableauDatasourceService(ITableauApiService tableauApiService, ILogger logger)
        {
            _tableauApiService = tableauApiService;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.GetDatasourcesAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauDatasource>> GetDatasourcesAsync()
        {
            _logger?.Debug("Getting datasources");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var datasources = new List<TableauDatasource>();

            do
            {
                var url = _tableauApiService.SiteUrl.AppendUri($"datasources");
                url += $"?pageSize={pageSize}&pageNumber={pageNumber}";
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("datasources").ContainsKey("datasource"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageDatasources = JsonConvert.DeserializeObject<List<TableauDatasource>>(
                    responseJson.Value<JObject>("datasources").Value<JArray>("datasource").ToString()
                );
                datasources.AddRange(pageDatasources);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            datasources.ForEach(
                d =>
                {
                    d.SiteId = _tableauApiService.SiteId;
                    d.ApiVersion = _tableauApiService.ApiVersion;
                }
            );

            _logger?.Debug($"{datasources.Count} datasources returned");

            return datasources;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.GetDatasourceAsync"/>
        /// </summary>
        public async Task<TableauDatasource> GetDatasourceAsync(string id, bool includeConnections = false)
        {
            _logger?.Debug($"Getting datasource {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"datasources/{id}");

            var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var datasource = JsonConvert.DeserializeObject<TableauDatasource>(
                responseJson.Value<JObject>("datasource").ToString()
            );

            datasource.SiteId = _tableauApiService.SiteId;
            datasource.ApiVersion = _tableauApiService.ApiVersion;

            _logger?.Debug($"Datasource {id} ({datasource.Name}, {datasource.Type}) returned");

            if (includeConnections)
            {
                _logger?.Debug($"Getting connections for datasource {datasource.Name}");
                var connections = await GetDatasourceConnectionsAsync(datasource.Id).ConfigureAwait(false);
                datasource.Connections = connections.ToArray();
            }

            return datasource;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.FindDatasourceAsync"/>
        /// </summary>
        public async Task<TableauDatasource> FindDatasourceAsync(string name, bool includeConnections = false)
        {
            _logger?.Debug($"Finding datasource {name}");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var datasources = new List<TableauDatasource>();

            do
            {
                var queryFilter = "filter=" + _tableauApiService.BuildQueryFilter("name", QueryFilterOperator.eq, name);
                var url = _tableauApiService.SiteUrl.AppendUri(
                    $"datasources?pageSize={pageSize}&pageNumber={pageNumber}&{queryFilter}"
                );
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("datasources").ContainsKey("datasource"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageDatasources = JsonConvert.DeserializeObject<List<TableauDatasource>>(
                    responseJson.Value<JObject>("datasources").Value<JArray>("datasource").ToString()
                );
                datasources.AddRange(pageDatasources);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            datasources.ForEach(
                d =>
                {
                    d.SiteId = _tableauApiService.SiteId;
                    d.ApiVersion = _tableauApiService.ApiVersion;
                }
            );

            if (!datasources.Any())
            {
                _logger?.Debug("No matching datasources found");
            }
            else if (datasources.Count > 1)
            {
                _logger?.Debug($"{datasources.Count} matching datasources found");
            }

            var datasource = datasources.SingleOrDefault();

            if (includeConnections && datasource != null)
            {
                _logger?.Debug($"Getting connections for datasource {datasource.Name}");
                var connections = await GetDatasourceConnectionsAsync(datasource.Id).ConfigureAwait(false);
                datasource.Connections = connections.ToArray();
            }

            return datasource;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.GetDatasourceConnectionsAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauConnection>> GetDatasourceConnectionsAsync(string id)
        {
            _logger?.Debug($"Getting connections for datasource {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"datasources/{id}/connections");

            var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);
            if (!responseJson.Value<JObject>("connections").ContainsKey("connection"))
            {
                return Enumerable.Empty<TableauConnection>();
            }

            var connections = JsonConvert.DeserializeObject<List<TableauConnection>>(
                responseJson.Value<JObject>("connections").Value<JArray>("connection").ToString()
            );

            _logger?.Debug($"{connections.Count()} connections returned");

            return connections;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.DownloadDatasourceAsync"/>
        /// </summary>
        public async Task<FileInfo> DownloadDatasourceAsync(
            string id,
            string saveDirectory = null,
            string fileName = null
        )
        {
            _logger?.Debug($"Downloading datasource {id}");

            var datasource = await GetDatasourceAsync(id).ConfigureAwait(false);

            var url = _tableauApiService.SiteUrl.AppendUri($"datasources/{id}/content");

            var fileInfo = await _tableauApiService.DownloadFileAsync(
                url, saveDirectory, fileName, true
            ).ConfigureAwait(false);
            _logger?.Debug($"Datasource {id} ({datasource.Name}) downloaded to {fileInfo.FullName}");

            return fileInfo;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.DownloadDatasourceBytesAsync"/>
        /// </summary>
        public async Task<TableauFileBytes> DownloadDatasourceBytesAsync(string id)
        {
            _logger?.Debug($"Downloading datasource {id}");

            var datasource = await GetDatasourceAsync(id).ConfigureAwait(false);

            var url = _tableauApiService.SiteUrl.AppendUri($"datasources/{id}/content");

            var fileDownload = await _tableauApiService.DownloadFileBytesAsync(url).ConfigureAwait(false);
            _logger?.Debug(
                String.Format(
                    "Datasource {0} ({1}, {2}) downloaded, {3} bytes",
                    id,
                    datasource.Name,
                    fileDownload.Name,
                    fileDownload.Bytes.Length
                )
            );

            return fileDownload;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.PublishDatasourceAsync"/>
        /// </summary>
        public async Task<TableauDatasource> PublishDatasourceAsync(
            TableauDatasource datasource,
            string path,
            bool overwrite = false
        )
        {
            _logger?.Debug($"Publishing datasource {datasource.Name}");

            if (String.IsNullOrWhiteSpace(datasource.Project?.Id))
            {
                throw new InvalidDataException("Project ID must be specified");
            }

            var url = _tableauApiService.SiteUrl.AppendUri($"datasources?overwrite={overwrite.ToString().ToLower()}");

            var datasourceJson = new JObject();
            datasourceJson["datasource"] = datasource.ToRequestString();

            // note: the content-disposition name header *must* be 'request_payload', the Tableau API
            // is hard-coded to look for the multipart segment with this name for the datasource meta info.
            // similarly, the content-disposition name header *must* be 'tableau_datasource' for the segment
            // that contains the datasource file data
            var responseString = await _tableauApiService.UploadFileAsync(
                url,
                path,
                "application/octet-stream",
                "tableau_datasource",
                datasourceJson.ToString(),
                "application/json",
                "request_payload"
            ).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var responseDatasource = JsonConvert.DeserializeObject<TableauDatasource>(
                responseJson.Value<JObject>("datasource").ToString()
            );

            _logger?.Debug($"Datasource {datasource.Name} published, id {responseDatasource.Id}");

            return responseDatasource;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.PublishDatasourceAsync"/>
        /// </summary>
        public async Task<TableauDatasource> PublishDatasourceAsync(
            TableauDatasource datasource,
            TableauFileBytes fileBytes,
            bool overwrite = false
        )
        {
            _logger?.Debug($"Publishing datasource {datasource.Name}");

            if (String.IsNullOrWhiteSpace(datasource.Project?.Id))
            {
                throw new InvalidDataException("Project ID must be specified");
            }

            var url = _tableauApiService.SiteUrl.AppendUri($"datasources?overwrite={overwrite.ToString().ToLower()}");

            var datasourceJson = new JObject();
            datasourceJson["datasource"] = JToken.Parse(datasource.ToRequestString());

            // note: the content-disposition name header *must* be 'request_payload', the Tableau API
            // is hard-coded to look for the multipart segment with this name for the datasource meta info.
            // similarly, the content-disposition name header *must* be 'tableau_datasource' for the segment
            // that contains the datasource file data
            var responseString = await _tableauApiService.UploadFileAsync(
                url, fileBytes, datasourceJson.ToString(), "application/json", "request_payload"
            ).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var responseDatasource = JsonConvert.DeserializeObject<TableauDatasource>(
                responseJson.Value<JObject>("datasource").ToString()
            );

            _logger?.Debug($"Datasource {datasource.Name} published, id {responseDatasource.Id}");

            return responseDatasource;
        }

        /// <summary>
        /// <see cref="ITableauDatasourceService.DeleteDatasourceAsync"/>
        /// </summary>
        public async Task DeleteDatasourceAsync(string id)
        {
            _logger?.Debug($"Deleting datasource {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"datasources/{id}");

            var responseString = await _tableauApiService.SendDeleteAsync(url).ConfigureAwait(false);
            _logger?.Debug($"Response: {responseString}");

            _logger?.Debug($"Datasource {id} deleted");
        }


    }
}