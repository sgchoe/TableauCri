using Microsoft.Extensions.Options;
using Serilog;
using TableauCri.Models.Configuration;

namespace TableauCri.Services
{
    public interface ITableauApiServiceSource : ITableauApiService { }

    public class TableauApiServiceSource : TableauApiService, ITableauApiServiceSource
    {
        public TableauApiServiceSource() : base() { }

        public TableauApiServiceSource(IOptionsMonitor<TableauApiSettingsSource> settingsMonitor, ILogger logger)
            : base(settingsMonitor, logger) { }
    }

}
