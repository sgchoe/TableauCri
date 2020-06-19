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
    public interface ITableauWorkbookService : ITableauFactoryService
    {
        /// <summary>
        /// Get workbooks for current auth session site
        /// </summary>
        Task<IEnumerable<TableauWorkbook>> GetWorkbooksAsync();

        /// <summary>
        /// Get workbook with specified id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="includeConnections"></param>
        Task<TableauWorkbook> GetWorkbookAsync(string id, bool includeConnections = false);

        /// <summary>
        /// Find workbooks with specified name
        /// </summary>
        /// <param name="name"></param>
        Task<IEnumerable<TableauWorkbook>> FindWorkbooksAsync(string name);

        /// <summary>
        /// Find workbooks with specified name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="includeConnections"></param>
        Task<TableauWorkbook> FindWorkbookAsync(string name, bool includeConnections = false);

        /// <summary>
        /// Get connections for workbook with specified id
        /// </summary>
        /// <param name="id"></param>
        Task<IEnumerable<TableauConnection>> GetWorkbookConnectionsAsync(string id);

        /// <summary>
        /// Download workbook with specified id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="saveDirectory"></param>
        /// <param name="fileName"></param>
        Task<FileInfo> DownloadWorkbookAsync(string id, string saveDirectory = null, string fileName = null);

        /// <summary>
        /// Download workbook with specified id
        /// </summary>
        /// <param name="id"></param>
        Task<TableauFileBytes> DownloadWorkbookBytesAsync(string id);

        /// <summary>
        /// Publish specified workbook to server
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="path"></param>
        /// <param name="overwrite"></param>
        Task<TableauWorkbook> PublishWorkbookAsync(TableauWorkbook workbook, string path, bool overwrite = false);

        /// <summary>
        /// Publish specified workbook to server
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="buffer"></param>
        /// <param name="overwrite"></param>
        Task<TableauWorkbook> PublishWorkbookAsync(
            TableauWorkbook workbook,
            TableauFileBytes fileBytes,
            bool overwrite = false
        );

        /// <summary>
        /// Delete specified workbook
        /// </summary>
        /// <param name="id"></param>
        Task DeleteWorkbookAsync(string id);
    }

    public class TableauWorkbookService : ITableauWorkbookService
    {
        private ITableauApiService _tableauApiService = null;
        private ILogger _logger = null;

        public TableauWorkbookService(ITableauApiService tableauApiService, ILogger logger)
        {
            _tableauApiService = tableauApiService;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.GetWorkbooksAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauWorkbook>> GetWorkbooksAsync()
        {
            _logger?.Debug("Getting workbooks");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var workbooks = new List<TableauWorkbook>();

            do
            {
                var url = _tableauApiService.SiteUrl.AppendUri(
                    $"workbooks?pageSize={pageSize}&pageNumber={pageNumber}"
                );
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("workbooks").ContainsKey("workbook"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageWorkbooks = JsonConvert.DeserializeObject<List<TableauWorkbook>>(
                    responseJson.Value<JObject>("workbooks").Value<JArray>("workbook").ToString()
                );
                workbooks.AddRange(pageWorkbooks);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            workbooks.ForEach(
                w =>
                {
                    w.SiteId = _tableauApiService.SiteId;
                    w.ApiVersion = _tableauApiService.ApiVersion;
                }
            );

            _logger?.Debug($"{workbooks.Count} workbooks returned");

            return workbooks;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.GetWorkbookAsync"/>
        /// </summary>
        public async Task<TableauWorkbook> GetWorkbookAsync(string id, bool includeConnections = false)
        {
            _logger?.Debug($"Getting workbook with id {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"workbooks/{id}");
            var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var workbook = JsonConvert.DeserializeObject<TableauWorkbook>(
                responseJson.Value<JObject>("workbook").ToString()
            );

            workbook.SiteId = _tableauApiService.SiteId;
            workbook.ApiVersion = _tableauApiService.ApiVersion;

            _logger?.Debug($"Workbook {workbook.Name}, description '{workbook.Description}' retrieved");

            if (includeConnections)
            {
                _logger?.Debug($"Getting connections for workbook {workbook.Name}");
                var connections = await GetWorkbookConnectionsAsync(id).ConfigureAwait(false);
                workbook.Connections = connections.ToArray();
            }

            return workbook;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.FindWorkbooksAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauWorkbook>> FindWorkbooksAsync(string name)
        {
            _logger?.Debug($"Finding workbooks: {name}");

            var pageSize = 1000;
            var pageNumber = 1;
            var totalRetrieved = 0;
            var totalAvailable = 0;
            var workbooks = new List<TableauWorkbook>();

            do
            {
                var queryFilter = "filter=" + _tableauApiService.BuildQueryFilter("name", QueryFilterOperator.eq, name);
                var url = _tableauApiService.SiteUrl.AppendUri(
                    $"workbooks?pageSize={pageSize}&pageNumber={pageNumber}&{queryFilter}"
                );
                var responseString = await _tableauApiService.SendGetAsync(url).ConfigureAwait(false);
                var responseJson = JToken.Parse(responseString);
                if (!responseJson.Value<JObject>("workbooks").ContainsKey("workbook"))
                {
                    break;
                }

                var pagination = JsonConvert.DeserializeObject<TableauPagination>(
                    responseJson.Value<JObject>("pagination").ToString()
                );

                var pageWorkbooks = JsonConvert.DeserializeObject<List<TableauWorkbook>>(
                    responseJson.Value<JObject>("workbooks").Value<JArray>("workbook").ToString()
                );
                workbooks.AddRange(pageWorkbooks);

                pageNumber++;
                totalAvailable = pagination.TotalAvailable;
                totalRetrieved += pagination.PageSize;
            }
            while (totalRetrieved < totalAvailable);

            workbooks.ForEach(
                w =>
                {
                    w.SiteId = _tableauApiService.SiteId;
                    w.ApiVersion = _tableauApiService.ApiVersion;
                }
            );

            _logger?.Debug($"{workbooks.Count} matching workbook(s) found");

            return workbooks;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.FindWorkbookAsync"/>
        /// </summary>
        public async Task<TableauWorkbook> FindWorkbookAsync(string name, bool includeConnections = false)
        {
            _logger?.Debug($"Finding workbook: {name}");

            var workbooks = await FindWorkbooksAsync(name).ConfigureAwait(false);

            _logger?.Debug($"{workbooks.Count()} matching workbooks found");

            var workbook = workbooks.SingleOrDefault();

            if (includeConnections && workbook != null)
            {
                _logger?.Debug($"Getting connections for workbook {workbook.Name}");
                var connections = await GetWorkbookConnectionsAsync(workbook.Id).ConfigureAwait(false);
                workbook.Connections = connections.ToArray();
            }

            return workbook;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.GetWorkbookConnectionsAsync"/>
        /// </summary>
        public async Task<IEnumerable<TableauConnection>> GetWorkbookConnectionsAsync(string id)
        {
            _logger?.Debug($"Getting connections for workbook {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"workbooks/{id}/connections");

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
        /// <see cref="ITableauWorkbookService.DownloadWorkbookAsync"/>
        /// </summary>
        public async Task<FileInfo> DownloadWorkbookAsync(
            string id,
            string saveDirectory = null,
            string fileName = null
        )
        {
            _logger?.Debug($"Downloading workbook {id}");

            var workbook = await GetWorkbookAsync(id).ConfigureAwait(false);

            var url = _tableauApiService.SiteUrl.AppendUri($"workbooks/{id}/content");

            var fileInfo = await _tableauApiService.DownloadFileAsync(
                url, saveDirectory, fileName, true
            ).ConfigureAwait(false);
            _logger?.Debug($"Workbook {id} ({workbook.Name}) downloaded to {fileInfo.FullName}");

            return fileInfo;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.DownloadWorkbookBytesAsync"/>
        /// </summary>
        public async Task<TableauFileBytes> DownloadWorkbookBytesAsync(string id)
        {
            _logger?.Debug($"Downloading workbook {id}");

            var workbook = await GetWorkbookAsync(id).ConfigureAwait(false);

            var url = _tableauApiService.SiteUrl.AppendUri($"workbooks/{id}/content");

            var fileBytes = await _tableauApiService.DownloadFileBytesAsync(url).ConfigureAwait(false);
            _logger?.Debug(
                String.Format(
                    "Workbook {0} ({1}, {2}) downloaded, {3} bytes",
                    id,
                    workbook.Name,
                    fileBytes.Name,
                    fileBytes.Bytes.Length
                )
            );

            return fileBytes;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.PublishWorkbookAsync"/>
        /// </summary>
        public async Task<TableauWorkbook> PublishWorkbookAsync(
            TableauWorkbook workbook,
            string path,
            bool overwrite = false
        )
        {
            _logger?.Debug($"Publishing workbook {workbook.Name}");

            if (String.IsNullOrWhiteSpace(workbook.Project?.Id))
            {
                throw new InvalidDataException("Project ID must be specified");
            }

            var url = _tableauApiService.SiteUrl.AppendUri($"workbooks?overwrite={overwrite.ToString().ToLower()}");

            var workbookJson = new JObject();
            workbookJson["workbook"] = workbook.ToRequestString();
            
            // note: the content-disposition name header *must* be 'request_payload', the Tableau API
            // is hard-coded to look for the multipart segment with this name for the workbook meta info.
            // similarly, the content-disposition name header *must* be 'tableau_workbook' for the segment
            // that contains the workbook file data
            var responseString = await _tableauApiService.UploadFileAsync(
                url,
                path,
                "application/octet-stream",
                "tableau_workbook",
                workbookJson.ToString(),
                "application/json",
                "request_payload"
            ).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var responseWorkbook = JsonConvert.DeserializeObject<TableauWorkbook>(
                responseJson.Value<JObject>("workbook").ToString()
            );

            _logger?.Debug($"Workbook {workbook.Name} published, id {responseWorkbook.Id}");

            return responseWorkbook;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.PublishWorkbookAsync"/>
        /// </summary>
        public async Task<TableauWorkbook> PublishWorkbookAsync(
            TableauWorkbook workbook,
            TableauFileBytes fileBytes,
            bool overwrite = false
        )
        {
            _logger?.Debug($"Publishing workbook {workbook.Name}");

            if (String.IsNullOrWhiteSpace(workbook.Project?.Id))
            {
                throw new InvalidDataException("Project ID must be specified");
            }

            var url = _tableauApiService.SiteUrl.AppendUri($"workbooks?skipConnectionCheck=true&overwrite={overwrite.ToString().ToLower()}");

            var workbookJson = new JObject();
            workbookJson["workbook"] = JToken.Parse(workbook.ToRequestString());

            // note: the content-disposition name header *must* be 'request_payload', the Tableau API
            // is hard-coded to look for the multipart segment with this name for the workbook meta info.
            // similarly, the content-disposition name header *must* be 'tableau_workbook' for the segment
            // that contains the workbook file data
            var responseString = await _tableauApiService.UploadFileAsync(
                url, fileBytes, workbookJson.ToString(), "application/json", "request_payload"
            ).ConfigureAwait(false);
            var responseJson = JToken.Parse(responseString);

            var responseWorkbook = JsonConvert.DeserializeObject<TableauWorkbook>(
                responseJson.Value<JObject>("workbook").ToString()
            );

            _logger?.Debug($"Workbook {workbook.Name} published, id {responseWorkbook.Id}");

            return responseWorkbook;
        }

        /// <summary>
        /// <see cref="ITableauWorkbookService.DeleteWorkbookAsync"/>
        /// </summary>
        public async Task DeleteWorkbookAsync(string id)
        {
            _logger?.Debug($"Deleting workbook {id}");

            var url = _tableauApiService.SiteUrl.AppendUri($"workbooks/{id}");

            var responseString = await _tableauApiService.SendDeleteAsync(url).ConfigureAwait(false);
            _logger?.Debug($"Response: {responseString}");

            _logger?.Debug($"Workbook {id} deleted");
        }


    }
}