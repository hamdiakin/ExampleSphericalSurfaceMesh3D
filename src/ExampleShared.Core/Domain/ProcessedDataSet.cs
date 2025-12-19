using System;
using System.Collections.Generic;

namespace Common.Domain
{
    public class ProcessedDataSet
    {
        public IReadOnlyList<SphereDataPoint> DataPoints { get; init; } = Array.Empty<SphereDataPoint>();
        
        public DateTime GeneratedAt { get; init; }
    }
}


