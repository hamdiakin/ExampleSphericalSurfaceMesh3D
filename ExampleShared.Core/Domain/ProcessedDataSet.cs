using System;
using System.Collections.Generic;

namespace ExampleShared.Core.Domain
{
    public class ProcessedDataSet
    {
        public IReadOnlyList<SphereDataPoint> DataPoints { get; init; } = Array.Empty<SphereDataPoint>();
        
        public DateTime GeneratedAt { get; init; }
    }
}

