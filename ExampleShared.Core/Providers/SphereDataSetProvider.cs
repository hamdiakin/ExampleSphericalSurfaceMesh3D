using System;
using System.Collections.Generic;
using ExampleShared.Core.Domain;

namespace ExampleShared.Core.Providers
{
    public class SphereDataSetProvider : IDataSetProvider
    {
        public ProcessedDataSet GenerateDataSet(int count, int? seed = null)
        {
            if (count < 0)
                throw new ArgumentException("Count must be non-negative", nameof(count));

            Random random = seed.HasValue ? new Random(seed.Value) : new Random();
            
            var dataPoints = new List<SphereDataPoint>(count);
            for (int i = 0; i < count; i++)
            {
                dataPoints.Add(SphereDataPoint.GenerateRandom(random));
            }

            return new ProcessedDataSet
            {
                DataPoints = dataPoints,
                GeneratedAt = DateTime.Now
            };
        }
    }
}


