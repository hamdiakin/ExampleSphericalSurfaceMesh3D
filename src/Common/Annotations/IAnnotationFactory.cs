using System.Collections.Generic;
using Common.Domain;

namespace Common.Annotations
{
    public interface IAnnotationFactory
    {
        IReadOnlyList<AnnotationSpec> CreateAnnotations(
            ProcessedDataSet dataSet,
            int? selectedIndex = null,
            int? hoveredIndex = null,
            bool showAllLabels = false,
            bool showAltitudeLabels = false);
    }
}


