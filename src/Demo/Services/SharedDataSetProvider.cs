using System;
using Common.Domain;
using Common.Providers;

namespace Demo.Services
{
    /// <summary>
    /// A data set provider that uses SharedDataService as the source
    /// </summary>
    public class SharedDataSetProvider : IDataSetProvider
    {
        private readonly SharedDataService sharedDataService;

        public SharedDataSetProvider(SharedDataService sharedDataService)
        {
            this.sharedDataService = sharedDataService ?? throw new ArgumentNullException(nameof(sharedDataService));
        }

        public ProcessedDataSet GenerateDataSet(int count, int? seed = null)
        {
            // This provider doesn't generate - it returns the current dataset from SharedDataService
            // The actual generation is done by SharedDataService
            return sharedDataService.CurrentDataSet;
        }
    }
}

