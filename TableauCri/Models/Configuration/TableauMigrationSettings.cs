using System.Collections.Generic;
using TableauCri.Models.Configuration;

namespace TableauCri.Models.Configuration
{
    public class TableauMigrationSettings
    {
        public TableauApiSettingsSource TableauApiSettingsSource { get; set; }
        public TableauApiSettingsDestination TableauApiSettingsDestination { get; set; }
        public VizDatasourceSettings VizDatasourceSettings { get; set; }
        public Dictionary<string, string> ProjectsToMigrate { get; set; }
        public string workbookDownloadPath { get; set; }
        public string DestinationRootProjectName { get; set; }
        public string DefaultOwnerUsername { get; set; }
        public Dictionary<string, string> EmbeddedConnectionCredentials { get; set; }
        public string[] WorkbooksToSkip { get; set; }
        public bool DryRun { get; set; }
    }
}
