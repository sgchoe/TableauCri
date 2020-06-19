using System.Collections.Generic;
using TableauCri.Models.Configuration;

namespace TableauCri.Models.Configuration
{
    public class VizDatasourceSettings
    {
        public string BaseUrl { get; set; }
        public string Cookie { get; set; }
        public string JsonSourcePath { get; set; }
        public string ReportOutputPath { get; set; }
        public string DatasourceFilesPath { get; set; }
    }
}
