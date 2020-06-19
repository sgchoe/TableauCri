using Microsoft.Extensions.Options;
using Serilog;
using TableauCri.Models.Configuration;

namespace TableauCri.Services
{
    public interface ITableauApiServiceDestination : ITableauApiService { }

    public class TableauApiServiceDestination : TableauApiService, ITableauApiServiceDestination
    {
        public TableauApiServiceDestination() : base() { }

        public TableauApiServiceDestination(
            IOptionsMonitor<TableauApiSettingsDestination> settingsMonitor,
            ILogger logger
        ) : base(settingsMonitor, logger) { }
    }

}
