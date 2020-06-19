using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using TableauCri.Extensions;
using TableauCri.Models;
using TableauCri.Models.Configuration;
using static TableauCri.Services.TableauApiService;

namespace TableauCri.Services
{
    public interface IVizDatasourceService
    {
        /// <summary>
        /// Create CSV report with Viz datasource names, server and credential details
        /// </summary>
        /// <param name="delimiter"></param>
        Task CreateVizDatasourceReportAsync(string delimiter = ",");

        /// <summary>
        /// Load CSV report with Viz datasource names, server and credential details
        /// </summary>
        /// <param name="delimiter"></param>
        Task LoadVizDatasourceReportAsync(string delimiter = ",");

        /// <summary>
        /// Download Viz datasource files (https://server/t/sitename/datasources/datasourcename.tds)
        /// to specifed parent folder path
        /// </summary>
        Task DownloadVizDatasourceFilesAsync();

        /// <summary>
        /// Get Viz datasource with specified name
        /// </summary>
        /// <param name="name"></param>
        VizDatasource GetVizDatasource(string name);

        /// <summary>
        /// Get Viz datasource file with specified name
        /// </summary>
        /// <param name="name"></param>
        TableauFileBytes GetVizDatasourceFile(string name);

        /// <summary>
        /// Find password for specified username in Viz datasources
        /// </summary>
        /// <param name="username"></param>
        string FindPassword(string username);

        /// <summary>
        /// Get specified name with invalid filename chars ('\', '/', etc) removed
        /// </summary>
        /// <param name="name"></param>
        string GetValidName(string name);
    }

    /// <summary>
    /// This entire class exists because Tableau API v3.6 (2019.4) can't download the content file for
    /// datasources that have whitespace in the name
    /// </summary>
    public class VizDatasourceService : IVizDatasourceService
    {
        private readonly IOptionsMonitor<VizDatasourceSettings> _settingsMonitor = null;
        private ITableauApiServiceSource _tableauApiServiceSource = null;
        private ILogger _logger = null;

        private List<VizDatasource> _vizDatasources = null;

        public VizDatasourceService(
            IOptionsMonitor<VizDatasourceSettings> settingsMonitor,
            ITableauApiServiceSource tableauApiServiceSource,
            ILogger logger
        )
        {
            _settingsMonitor = settingsMonitor;
            _tableauApiServiceSource = tableauApiServiceSource;
            _logger = logger;

            if (String.IsNullOrWhiteSpace(_settingsMonitor.CurrentValue.ReportOutputPath))
            {
                throw new ArgumentNullException("Viz datasource report path required");
            }

            // populate the viz datasources - a list of datasources stored in Tableau used by the web UI
            // having a different format/schema from the datasource managed through the REST API
            LoadVizDatasourceJson();
        }

        /// <summary>
        /// <see cref="IVizDatasourceService.CreateVizDatasourceReportAsync"/>
        /// </summary>
        public async Task CreateVizDatasourceReportAsync(string delimiter = ",")
        {
            using (var sw = new StreamWriter(_settingsMonitor.CurrentValue.ReportOutputPath, false))
            using (var cw = new CsvWriter(sw, CultureInfo.InvariantCulture))
            {
                cw.WriteField("Name");
                cw.WriteField("Server");
                cw.WriteField("Port");
                cw.WriteField("HasEmbeddedPassword");
                cw.WriteField("Username");
                cw.WriteField("Password");

                foreach (var vizDatasource in _vizDatasources)
                {
                    cw.WriteField($"#{vizDatasource.Name}");
                    cw.WriteField(vizDatasource.VizConnectionDetail.ServerName);
                    cw.WriteField(vizDatasource.VizConnectionDetail.ServerPort);
                    cw.WriteField(vizDatasource.VizConnectionDetail.HasEmbeddedPassword);
                    cw.WriteField(vizDatasource.VizConnectionDetail.Username);
                    cw.WriteField("");
                    cw.NextRecord();
                }
                await cw.FlushAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// <see cref="IVizDatasourceService.LoadVizDatasourceReportAsync"/>
        /// </summary>
        public async Task LoadVizDatasourceReportAsync(string delimiter = ",")
        {
            _logger?.Debug($"Loading Viz datasource report {_settingsMonitor.CurrentValue.ReportOutputPath}");
            using (var sr = new StreamReader(_settingsMonitor.CurrentValue.ReportOutputPath))
            using (var cr = new CsvReader(sr, CultureInfo.InvariantCulture))
            {
                await cr.ReadAsync().ConfigureAwait(false);
                cr.ReadHeader();
                while (await cr.ReadAsync().ConfigureAwait(false))
                {
                    var name = GetValidName(cr.GetField("Name"));
                    var serverName = cr.GetField("Server");
                    var serverPort = cr.GetField("Port");
                    bool.TryParse(cr.GetField("HasEmbeddedPassword"), out bool hasEmbeddedPassword);
                    var username = cr.GetField("Username");
                    var password = cr.GetField("Password");

                    if (name.StartsWith("#") ||
                        String.IsNullOrWhiteSpace(username) ||
                        String.IsNullOrWhiteSpace(password))
                    {
                        continue;
                    }
                    
                    if (String.IsNullOrWhiteSpace(name) ||
                        String.IsNullOrWhiteSpace(serverName) ||
                        !hasEmbeddedPassword ||
                        String.IsNullOrWhiteSpace(password))
                    {
                        throw new Exception($"Invalid record: {cr.Context.RawRow}, {cr.Context.RawRecord}");
                    }
                    var vizDatasource = _vizDatasources.SingleOrDefault(
                        v =>
                            v.Name.EqualsIgnoreCase(name) &&
                            v.VizConnectionDetail.ServerName.EqualsIgnoreCase(serverName) &&
                            v.VizConnectionDetail.Username.EqualsIgnoreCase(username)
                    );
                    if (vizDatasource == null)
                    {
                        throw new Exception(
                            $"Unable to find datasource for line: {cr.Context.RawRow}, {cr.Context.RawRecord}"
                        );
                    }
                    vizDatasource.VizConnectionDetail.Password = password;
                }
            }


            if (_vizDatasources.Any(v => String.IsNullOrWhiteSpace(v.VizConnectionDetail.Password)))
            {
                _logger?.Warning("Datasources without passwords still present after load");
            }
        }

        /// <summary>
        /// <see cref="IVizDatasourceService.DownloadVizDatasourceFilesAsync"/>
        /// </summary>
        public async Task DownloadVizDatasourceFilesAsync()
        {
            if (!String.IsNullOrEmpty(_settingsMonitor.CurrentValue.DatasourceFilesPath))
            {
                Directory.CreateDirectory(_settingsMonitor.CurrentValue.DatasourceFilesPath);
            }
            foreach (var vizDatasource in _vizDatasources)
            {
                _logger?.Debug($"Downloading Viz datasource {vizDatasource.Name}");
                var url = _settingsMonitor.CurrentValue.BaseUrl.AppendUri(vizDatasource.DownloadUrl);
                var headers = new Dictionary<string, string>()
                {
                    { "User-Agent", "Mozilla Chrome Safari" },
                };
                var datasourceBytes = await _tableauApiServiceSource.SendRequestAsync<byte[]>(
                    url,
                    HttpMethod.Get,
                    null,
                    headers,
                    "*/*",
                    _settingsMonitor.CurrentValue.Cookie
                ).ConfigureAwait(false);

                if ((datasourceBytes?.Length ?? 0) == 0)
                {
                    _logger?.Error($"Error downloading Viz datasource file, no data returned");
                    continue;
                }
                _logger?.Debug($"Downloaded Viz datasource, {datasourceBytes.Length} bytes");

                var uri = new Uri(url);
                var fileName = vizDatasource.Name + Path.GetExtension(Uri.UnescapeDataString(uri.Segments.Last()));
                File.WriteAllBytes(
                    Path.Combine(_settingsMonitor.CurrentValue.DatasourceFilesPath, fileName),
                    datasourceBytes
                );
            }
        }

        /// <summary>
        /// <see cref="IVizDatasourceService.GetVizDatasource"/>
        /// </summary>
        public VizDatasource GetVizDatasource(string name)
        {
            return _vizDatasources.SingleOrDefault(v => v.Name.EqualsIgnoreCase(name));
        }

        /// <summary>
        /// <see cref="IVizDatasourceService.GetVizDatasourceFile"/>
        /// </summary>
        public TableauFileBytes GetVizDatasourceFile(string name)
        {
            _logger?.Debug($"Searching for Viz datasource file '{name}.tds'");
            var dirInfo = new DirectoryInfo(_settingsMonitor.CurrentValue.DatasourceFilesPath);
            var files = dirInfo.GetFiles($"{name}.tds", SearchOption.AllDirectories);
            if (!files.Any())
            {
                _logger?.Debug($"Viz datasource file '{name}.tds' not found");
                return null;
            }
            else if (files.Count() != 1)
            {
                _logger?.Debug($"{files.Count()} matching Viz datasource files '{name}.tds' found");
                return null;
            }
            var file = files.Single();
            return new TableauFileBytes(
                file.FullName,
                File.ReadAllBytes(file.FullName),
                "application/x-tds",
                "tableau_datasource"
            );
        }

        /// <summary>
        /// <see cref="IVizDatasourceService.FindPassword"/>
        /// </summary>
        public string FindPassword(string username)
        {
            var passwords = _vizDatasources.Where(
                v =>
                    (v.VizConnectionDetail.Username ?? "").EqualsIgnoreCase(username) &&
                    !String.IsNullOrWhiteSpace(v.VizConnectionDetail.Password)
            )
            .Select(v => v.VizConnectionDetail.Password)
            .Distinct();
            
            return _vizDatasources.Where(
                v =>
                    (v.VizConnectionDetail.Username ?? "").EqualsIgnoreCase(username) &&
                    !String.IsNullOrWhiteSpace(v.VizConnectionDetail.Password)
            )
            .Select(v => v.VizConnectionDetail.Password)
            .Distinct()
                .SingleOrDefault();
        }

        /// <summary>
        /// <see cref="IVizDatasourceService.GetValidName"/>
        /// </summary>
        public string GetValidName(string name)
        {
            return GetValidFileName(name);
        }

        /// <summary>
        /// Get specified name with invalid path chars ('\', '/', etc) removed
        /// </summary>
        /// <param name="name"></param>
        public static string GetValidFileName(string name)
        {
            return String.Concat((name ?? "").Split(Path.GetInvalidFileNameChars()));
        }

        private void LoadVizDatasourceJson()
        {
            _vizDatasources = JsonConvert.DeserializeObject<List<VizDatasource>>(
                File.ReadAllText(_settingsMonitor.CurrentValue.JsonSourcePath)
            );
            foreach (var vizDatasource in _vizDatasources)
            {
                //vizDatasource.Name = GetValidName(vizDatasource.Name.Trim());
                vizDatasource.Name = !vizDatasource.Name.EqualsIgnoreCase("Topse ")
                    ? GetValidName(vizDatasource.Name.Trim())
                    : vizDatasource.Name;
                vizDatasource.VizConnectionDetail.ServerName = vizDatasource.VizConnectionDetail.ServerName?.Trim();
                vizDatasource.VizConnectionDetail.ServerPort = vizDatasource.VizConnectionDetail.ServerPort?.Trim();
                vizDatasource.VizConnectionDetail.Username = vizDatasource.VizConnectionDetail.Username?.Trim();
            }
        }

    }
}