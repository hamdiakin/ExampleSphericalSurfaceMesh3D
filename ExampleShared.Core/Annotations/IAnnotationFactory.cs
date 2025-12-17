using System.Collections.Generic;
using ExampleShared.Core.Domain;

namespace ExampleShared.Core.Annotations
{
    public interface IAnnotationFactory
    {
        IReadOnlyList<AnnotationSpec> CreateAnnotations(
            ProcessedDataSet dataSet,
            int? selectedIndex = null,
            int? hoveredIndex = null,
            bool showAllLabels = false);
    }
}

