using System;
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
    public interface ITableauFactoryService { }

    public interface ITableauServiceFactory
    {
        /// <summary>
        /// Get service of specified type
        /// </summary>
        T GetService<T>() where T : ITableauFactoryService;
    }

    public class TableauServiceFactory : ITableauServiceFactory
    {
        private ITableauApiService _tableauApiService = null;
        private ILogger _logger = null;

        public TableauServiceFactory(ITableauApiService tableauApiService, ILogger logger)
        {
            _tableauApiService = tableauApiService;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="ITableauServiceFactory.GetService"/>
        /// </summary>
        public T GetService<T>() where T : ITableauFactoryService
        {
            return (T)Activator.CreateInstance(typeof(T), _tableauApiService, _logger);
        }


    }
}