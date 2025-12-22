using System;
using System.Collections.Generic;
using System.Linq;
using Common.Domain;

namespace Common.Pipeline
{
    public class SphericalCoordinateProcessor : IDataProcessor
    {
        public ProcessedDataSet Process(ProcessedDataSet raw)
        {
            if (raw == null)
                throw new ArgumentNullException(nameof(raw));

            // Filter out points that are outside sphere bounds
            var filteredPoints = raw.DataPoints
                .Where(p => p.IsWithinSphereBounds())
                .ToList();

            return new ProcessedDataSet
            {
                DataPoints = filteredPoints,
                GeneratedAt = raw.GeneratedAt
            };
        }
    }
}


