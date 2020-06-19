using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TableauCri.Models.Configuration;
using Serilog;
using TableauCri.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.IO;
using static TableauCri.Services.TableauApiService;

namespace TableauCri.Services
{
    public interface ITableauApiService : IDisposable
    {
        /// <summary>
        /// Tableau REST API version, e.g. "2.6"
        /// </summary>
        string ApiVersion { get; }

        /// <summary>
        /// Tableau site id, e.g. "cri"
        /// </summary>
        string SiteId { get; }

        /// <summary>
        /// Tableau REST API base site url constructed using api version and site, e.g. "api/2.6/sites/cri"
        /// </summary>
        string SiteUrl { get; }

        /// <summary>
        /// Sign in to Tableau
        /// </summary>
        Task SignInAsync();

        /// <summary>
        /// Sign out of Tableau
        /// </summary>
        Task SignOutAsync();

        /// <summary>
        /// Download file from specified url to (optional) local directory and file name
        /// </summary>
        /// <param name="url"></param>
        /// <param name="saveDirectory"></param>
        /// <param name="fileName"></param>
        /// <param name="overwrite"></param>
        Task<FileInfo> DownloadFileAsync(
            string url,
            string saveDirectory = null,
            string fileName = null,
            bool overwrite = false
        );

        /// <summary>
        /// Download file from specified url to byte array
        /// </summary>
        /// <param name="url"></param>
        Task<TableauFileBytes> DownloadFileBytesAsync(string url);

        /// <summary>
        /// Upload file located at path to specified url with optional embedded content
        /// </summary>
        /// <param name="url"></param>
        /// <param name="path"></param>
        /// <param name="fileContentType"></param>
        /// <param name="fileContentName"></param>
        /// <param name="content"></param>
        /// <param name="contentContentType"></param>
        /// <param name="contentDispositionName"></param>
        Task<string> UploadFileAsync(
            string url,
            string path,
            string fileContentType = null,
            string fileContentName = null,
            string content = null,
            string contentContentType = null,
            string contentDispositionName = null
        );

        /// <summary>
        /// Upload file contained in buffer to specified url with optional embedded content
        /// </summary>
        /// <param name="url"></param>
        /// <param name="fileBytes"></param>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <param name="contentDispositionName"></param>
        Task<string> UploadFileAsync(
            string url,
            TableauFileBytes fileBytes,
            string content = null,
            string contentType = null,
            string contentDispositionName = null
        );

        /// <summary>
        /// Send request to specified URL using provided HTTP method and optional content
        /// </summary>
        /// <param name="url"></param>
        /// <param name="httpMethod"></param>
        /// <param name="content"></param>
        /// <param name="headers"></param>
        /// <param name="accept"></param>
        /// <param name="cookie"></param>
        Task<T> SendRequestAsync<T>(
            string url,
            HttpMethod httpMethod,
            string content = null,
            Dictionary<string, string> headers = null,
            string accept = null,
            string cookie = null
        );

        /// <summary>
        /// Send GET request to specified URL
        /// </summary>
        /// <param name="url"></param>
        Task<string> SendGetAsync(string url);

        /// <summary>
        /// Send POST request to specified URL with optional content
        /// </summary>
        /// <param name="url"></param>
        /// <param name="content"></param>
        Task<string> SendPostAsync(string url, string content = null);

        /// <summary>
        /// Send PUT request to specified URL with optional content
        /// </summary>
        /// <param name="url"></param>
        /// <param name="content"></param>
        Task<string> SendPutAsync(string url, string content = null);

        /// <summary>
        /// Send DELETE request to specified URL
        /// </summary>
        /// <param name="url"></param>
        Task<string> SendDeleteAsync(string url);

        /// <summary>
        /// Build query filter for specified field, operator, and value
        /// </summary>
        /// <param name="field"></param>
        /// <param name="queryFilterOperator"></param>
        /// <param name="value"></param>
        string BuildQueryFilter(string field, QueryFilterOperator queryFilterOperator, string value);
    }

    public class TableauApiService : ITableauApiService
    {
        public enum QueryFilterOperator { eq, gt, gte, has, lt, lte, @in };
        // Old API:
        // Interactor, Publisher, ServerAdministrator, SiteAdministrator, Viewer
        //
        // New API:
        // Creator, Explorer, ExplorerCanPublish, ReadOnly, ServerAdministrator,
        // SiteAdministratorExplorer, SiteAdministratorCreator, Unlicensed, Viewer
        public const string SITE_ROLE_LEGACY_INTERACTOR = "Interactor";
        public const string SITE_ROLE_LEGACY_PUBLISHER = "Publisher";
        public const string SITE_ROLE_LEGACY_SERVER_ADMIN = "ServerAdministrator";
        public const string SITE_ROLE_LEGACY_SITE_ADMIN = "SiteAdministrator";
        public const string SITE_ROLE_LEGACY_VIEWER = "Viewer";

        public const string SITE_ROLE_2018_CREATOR = "Creator";
        public const string SITE_ROLE_2018_EXPLORER = "Explorer";
        public const string SITE_ROLE_2018_PUBLISHER = "ExplorerCanPublish";
        public const string SITE_ROLE_2018_READ_ONLY = "ReadOnly";
        public const string SITE_ROLE_2018_SERVER_ADMIN = "ServerAdministrator";
        public const string SITE_ROLE_2018_SITE_ADMIN_EXPLORER = "SiteAdministratorExplorer";
        public const string SITE_ROLE_2018_SITE_ADMIN_CREATOR = "SiteAdministratorCreator";
        public const string SITE_ROLE_2018_UNLICENSED = "Unlicensed";
        public const string SITE_ROLE_2018_VIEWER = "Viewer";

        public const string CONNECTION_TYPE_MYSQL = "mysql";
        public const string CONNECTION_TYPE_NETEZZA = "netezza";
        public const string CONNECTION_TYPE_ORACLE = "oracle";
        public const string CONNECTION_TYPE_SQLPROXY = "sqlproxy";
        public const string CONNECTION_TYPE_SQLSERVER = "sqlserver";
        
        private readonly IOptionsMonitor<TableauApiSettings> _settingsMonitor = null;
        private ILogger _logger = null;
        private HttpClientHandler _httpClientHandler = null;
        private HttpClient _httpClient = null;
        private bool _disposed = false;

        public TableauApiService()
        {
            ServicePointManager.ServerCertificateValidationCallback = (
                (sender2, certificate, chain, sslPolicyErrors) => true
            );
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            _httpClientHandler = new HttpClientHandler() { UseCookies = false };
            _httpClient = new HttpClient(_httpClientHandler);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public TableauApiService(
            IOptionsMonitor<TableauApiSettings> settingsMonitor,
            ILogger logger
        ) : this()
        {
            _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException("Missing Tableau API settings");
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_settingsMonitor.CurrentValue.BaseUrl);
            _httpClient.Timeout = _settingsMonitor.CurrentValue.TimeoutSeconds > 0
                ? TimeSpan.FromSeconds(_settingsMonitor.CurrentValue.TimeoutSeconds)
                : _httpClient.Timeout;
        }

        /// <summary>
        /// <see cref="ITableauApiService.ApiVersion"/>
        /// </summary>
        public string ApiVersion => _settingsMonitor?.CurrentValue?.ApiVersion;

        /// <summary>
        /// <see cref="ITableauApiService.SiteId"/>
        /// </summary>
        public string SiteId => ApiCredentialResponse?.SiteId;

        /// <summary>
        /// <see cref="ITableauApiService.SiteUrl"/>
        /// </summary>
        public string SiteUrl => $"api/{_settingsMonitor?.CurrentValue?.ApiVersion}/sites/{SiteId}/";

        public TableauCredentialResponse ApiCredentialResponse { get; private set; }
        public string UserId => ApiCredentialResponse?.UserId;
        public string Token => ApiCredentialResponse?.Token;
        public string AuthUrl => $"api/{_settingsMonitor?.CurrentValue?.ApiVersion}/auth/";

        /// <summary>
        /// <see cref="ITableauApiService.SignInAsync"/>
        /// </summary>
        public async Task SignInAsync()
        {
            if (!String.IsNullOrWhiteSpace(Token))
            {
                await SignOutAsync().ConfigureAwait(false);
            }

            _logger?.Information(
                $"Signing in to {_settingsMonitor.CurrentValue.BaseUrl} as {_settingsMonitor.CurrentValue.Username}"
            );

            TableauCredentialRequest apiCredentialRequest = new TableauCredentialRequest(
                _settingsMonitor.CurrentValue.SiteContentUrl,
                _settingsMonitor.CurrentValue.Username,
                _settingsMonitor.CurrentValue.Password
            );

            var responseString = await SendPostAsync(
                $"{AuthUrl}/signin", apiCredentialRequest.ToString()
            ).ConfigureAwait(false);

            ApiCredentialResponse = JsonConvert.DeserializeObject<TableauCredentialResponse>(responseString);

            ClearClientDefaultRequestHeaders();

            _httpClient.DefaultRequestHeaders.Add("X-Tableau-Auth", Token);
            _httpClient.DefaultRequestHeaders.Add("Cookie", $"workgroup_session_id={Token}");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        }

        /// <summary>
        /// <see cref="ITableauApiService.SignOutAsync"/>
        /// </summary>
        public async Task SignOutAsync()
        {
            _logger?.Debug("Signing out");
            var responseString = await SendPostAsync($"{AuthUrl}/signout").ConfigureAwait(false);

            ApiCredentialResponse = null;

            ClearClientDefaultRequestHeaders();
        }

        /// <summary>
        /// <see cref="ITableauApiService.DownloadFileAsync"/>
        /// </summary>
        public async Task<FileInfo> DownloadFileAsync(
            string url,
            string saveDirectory = null,
            string fileName = null,
            bool overwrite = false
        )
        {
            _logger?.Debug($"Downloading file from {url}");

            var localPath = null as string;

            if (!String.IsNullOrWhiteSpace(saveDirectory) &&
                Directory.Exists(saveDirectory) &&
                !String.IsNullOrWhiteSpace(fileName))
            {
                localPath = Path.Combine(saveDirectory, fileName);
            }

            using (
                var response = await SendRequestAsync<HttpResponseMessage>(url, HttpMethod.Get).ConfigureAwait(false)
            )
            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {

                if (
                    !response.Content.Headers.TryGetValues(
                        "Content-Disposition",
                        out IEnumerable<string> contentDispositionValues
                    )
                )
                {
                    var responseString = Encoding.UTF8.GetString(
                        await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
                    );
                    _logger?.Error("Error downloading file:");
                    _logger?.Error(responseString);
                    throw new Exception($"Error downloading file: {responseString}");
                }

                // format => name="tableau_datasource"; filename="filename.ext"
                var responseFileName = contentDispositionValues.First()
                    .Split(';')
                    .Last()
                    .Split('=')
                    .Last()
                    .Replace("\"", "");

                var savePath = localPath ??
                    Path.Combine(
                        !String.IsNullOrWhiteSpace(saveDirectory) && Directory.Exists(saveDirectory)
                            ? saveDirectory : "",
                        responseFileName
                    );

                using (var fs = new FileStream(savePath, overwrite ? FileMode.Create : FileMode.CreateNew))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    await stream.CopyToAsync(fs).ConfigureAwait(false);
                    await fs.FlushAsync().ConfigureAwait(false);
                    fs.Close();
                    stream.Close();
                }

                _logger?.Debug($"Downloaded file from {url} to {savePath}");

                return new FileInfo(savePath);
            }
        }

        /// <summary>
        /// <see cref="ITableauApiService.DownloadFileAsync"/>
        /// </summary>
        public async Task<TableauFileBytes> DownloadFileBytesAsync(string url)
        {
            _logger?.Debug($"Downloading file from {url}");

            var localPath = null as string;

            using (
                var response = await SendRequestAsync<HttpResponseMessage>(url, HttpMethod.Get).ConfigureAwait(false)
            )
            {
                if (
                    !response.Content.Headers.TryGetValues(
                        "Content-Disposition",
                        out IEnumerable<string> contentDispositionValues
                    )
                )
                {
                    var responseString = Encoding.UTF8.GetString(
                        await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
                    );
                    _logger?.Error("Error downloading file:");
                    _logger?.Error(responseString);
                    throw new Exception($"Error downloading file: {responseString}");
                }

                response.Content.Headers.TryGetValues("Content-Type", out IEnumerable<string> contentTypeValues);

                // format => name="tableau_datasource"; filename="filename.ext"
                var responseContentDispositionName = contentDispositionValues.First()
                    .Split(';')
                    .First()
                    .Split('=')
                    .Last()
                    .Trim('"');
                var responseFileName = contentDispositionValues.First()
                    .Split(';')
                    .Last()
                    .Split('=')
                    .Last()
                    .Trim('"');

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var fileDownload = new TableauFileBytes(
                    responseFileName,
                    bytes,
                    contentTypeValues.FirstOrDefault(),
                    responseContentDispositionName
                );

                return fileDownload;
            }
        }

        /// <summary>
        /// <see cref="ITableauApiService.UploadFileAsync"/>
        /// </summary>
        public async Task<string> UploadFileAsync(
            string url,
            string path,
            string fileContentType = null,
            string fileContentName = null,
            string content = null,
            string contentContentType = null,
            string contentDispositionName = null
        )
        {
            _logger?.Debug($"Uploading file '{path}' to {url}");
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return await UploadFileAsync(
                url,
                new TableauFileBytes(fileInfo.Name, File.ReadAllBytes(path), fileContentType, fileContentName),
                content,
                contentContentType,
                contentDispositionName
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauApiService.UploadFileAsync"/>
        /// </summary>
        public async Task<string> UploadFileAsync(
            string url,
            TableauFileBytes fileBytes,
            string content = null,
            string contentType = null,
            string contentDispositionName = null
        )
        {
            _logger?.Debug($"Uploading stream to {url}");

            using (var memoryStream = new MemoryStream(fileBytes.Bytes))
            using (var streamContent = new StreamContent(memoryStream))
            using (var formContent = new MultipartContent("mixed"))
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                if (!String.IsNullOrWhiteSpace(content))
                {
                    var stringContent = new StringContent(content);
                    stringContent.Headers.Clear();
                    if (!String.IsNullOrWhiteSpace(contentType))
                    {
                        stringContent.Headers.TryAddWithoutValidation("Content-Type", contentType);
                    }
                    if (!String.IsNullOrWhiteSpace(contentDispositionName))
                    {
                        stringContent.Headers.TryAddWithoutValidation(
                            "Content-Disposition",
                            $"name=\"{contentDispositionName}\""
                        );
                    }

                    formContent.Add(stringContent);
                }
                if (!String.IsNullOrWhiteSpace(fileBytes.ContentType))
                {
                    streamContent.Headers.TryAddWithoutValidation("Content-Type", fileBytes.ContentType);
                }
                string contentDisposition = $"filename=\"{fileBytes.Name}\"";
                if (!String.IsNullOrWhiteSpace(fileBytes.ContentDispositionName))
                {
                    contentDisposition = $"name=\"{fileBytes.ContentDispositionName}\"; {contentDisposition}";
                }
                streamContent.Headers.TryAddWithoutValidation(
                    "Content-Disposition",
                    contentDisposition
                );

                formContent.Add(streamContent);

                request.Content = formContent;

                using (HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return Encoding.UTF8.GetString(
                            await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
                        );
                    }
                    else
                    {
                        throw new HttpRequestException(
                            response.StatusCode.ToString() + ": " +
                                await response.Content.ReadAsStringAsync().ConfigureAwait(false)
                        );
                    }
                }
            }
        }

        /// <summary>
        /// <see cref="ITableauApiService.SendRequestAsync"/>
        /// </summary>
        public async Task<T> SendRequestAsync<T>(
            string url,
            HttpMethod httpMethod,
            string content = null,
            Dictionary<string, string> headers = null,
            string accept = null,
            string cookie = null
        )
        {
            HttpContent httpContent = !String.IsNullOrWhiteSpace(content)
                ? new StringContent(content, Encoding.UTF8, "application/json")
                : null;

            var tType = typeof(T);

            var request = new HttpRequestMessage(httpMethod == default ? HttpMethod.Get : httpMethod, url)
            {
                Content = httpContent
            };

            if (headers?.Any() ?? false)
            {
                foreach (var kvp in headers)
                {
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }

            if (!String.IsNullOrWhiteSpace(cookie))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
            }

            if (!String.IsNullOrWhiteSpace(accept))
            {
                // clear the default accept header for this request
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            }
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (!String.IsNullOrWhiteSpace(accept))
            {
                // re-apply the default accept header for subsequent requests
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
            
            if (tType == typeof(HttpResponseMessage))
            {
                using (request)
                {
                    return (T)Convert.ChangeType(response, tType);
                }
            }

            using (request)
            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    var responseOutput = null as object;
                    if (tType == typeof(string))
                    {
                        responseOutput = Encoding.UTF8.GetString(
                            await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
                        );
                    }
                    else if (tType == typeof(byte[]))
                    {
                        responseOutput = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                    else if (tType == typeof(Stream))
                    {
                        responseOutput = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        throw new Exception($"Unsupported return type: {tType.Name}");
                    }
                    return (T)Convert.ChangeType(responseOutput, tType);
                }
                else
                {
                    throw new HttpRequestException(
                        response.StatusCode.ToString() + ": " +
                            await response.Content.ReadAsStringAsync().ConfigureAwait(false)
                    );
                }
            }
        }

        /// <summary>
        /// <see cref="ITableauApiService.SendGetAsync"/>
        /// </summary>
        public async Task<string> SendGetAsync(string url)
        {
            return await SendRequestAsync<string>(url, HttpMethod.Get).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauApiService.SendPostAsync"/>
        /// </summary>
        public async Task<string> SendPostAsync(string url, string content = null)
        {
            return await SendRequestAsync<string>(url, HttpMethod.Post, content).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauApiService.SendPutAsync"/>
        /// </summary>
        public async Task<string> SendPutAsync(string url, string content = null)
        {
            return await SendRequestAsync<string>(url, HttpMethod.Put, content).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauApiService.SendDeleteAsync"/>
        /// </summary>
        public async Task<string> SendDeleteAsync(string url)
        {
            return await SendRequestAsync<string>(url, HttpMethod.Delete).ConfigureAwait(false);
        }

        /// <summary>
        /// <see cref="ITableauApiService.BuildQueryFilter"/>
        /// </summary>
        public string BuildQueryFilter(string field, QueryFilterOperator queryFilterOperator, string value)
        {
            return ConstructQueryFilter(field, queryFilterOperator, value);
        }

        /// <summary>
        /// Static method to build query filter for specified field, operator, and value
        /// </summary>
        /// <param name="field"></param>
        /// <param name="operator"></param>
        /// <param name="value"></param>
        public static string ConstructQueryFilter(string field, QueryFilterOperator queryFilterOperator, string value)
        {
            return $"{field}:{queryFilterOperator.ToString().ToLower()}:{Uri.EscapeDataString(value)}";
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
                _httpClient?.Dispose();
                _httpClientHandler?.Dispose();
            }

            _disposed = true;
        }

        private void ClearClientDefaultRequestHeaders()
        {
            string[] headersToClear = { "X-Tableau-Auth", "Cookie", "Connection" };
            foreach (string headerToClear in headersToClear)
            {
                if (_httpClient.DefaultRequestHeaders.Contains(headerToClear))
                {
                    _httpClient.DefaultRequestHeaders.Remove(headerToClear);
                }
            }
        }

        private string GetNewUserSiteRole(string oldSiteRole)
        {
            // Old API:
            // Interactor, Publisher, ServerAdministrator, SiteAdministrator, Viewer
            //
            // New API:
            // Creator, Explorer, ExplorerCanPublish, ReadOnly, ServerAdministrator,
            // SiteAdministratorExplorer, SiteAdministratorCreator, Unlicensed, Viewer

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { SITE_ROLE_LEGACY_INTERACTOR, SITE_ROLE_2018_EXPLORER },
                { SITE_ROLE_LEGACY_PUBLISHER, SITE_ROLE_2018_PUBLISHER },
                { SITE_ROLE_LEGACY_SITE_ADMIN, SITE_ROLE_2018_SITE_ADMIN_EXPLORER },
                { SITE_ROLE_LEGACY_VIEWER, SITE_ROLE_2018_READ_ONLY }
            };

            return map.ContainsKey(oldSiteRole) ? map[oldSiteRole] : oldSiteRole;
        }


    }
}