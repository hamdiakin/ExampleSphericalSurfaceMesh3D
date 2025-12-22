using System;
using System.Collections.Generic;
using Common.Domain;

namespace Common.Annotations
{
    public class SphereAnnotationFactory : IAnnotationFactory
    {
        public IReadOnlyList<AnnotationSpec> CreateAnnotations(
            ProcessedDataSet dataSet,
            int? selectedIndex = null,
            int? hoveredIndex = null,
            bool showAllLabels = false)
        {
            if (dataSet == null)
                throw new ArgumentNullException(nameof(dataSet));

            var specs = new List<AnnotationSpec>(dataSet.DataPoints.Count);

            for (int i = 0; i < dataSet.DataPoints.Count; i++)
            {
                var point = dataSet.DataPoints[i];
                bool isSelected = selectedIndex.HasValue && selectedIndex.Value == i;
                bool isHovered = hoveredIndex.HasValue && hoveredIndex.Value == i;

                string? label = null;
                if (showAllLabels || isSelected || isHovered)
                {
                    if (isSelected)
                    {
                        label = $">>> [{i}] <<<\nX: {point.X:F1}\nY: {point.Y:F1}";
                    }
                    else
                    {
                        label = $"[{i}]\nX: {point.X:F1}\nY: {point.Y:F1}";
                    }
                }

                var spec = new ArrowAnnotationSpec
                {
                    Id = $"arrow_{i}",
                    Color = point.Color,
                    StartX = point.X,
                    StartY = point.Y,
                    StartZ = point.Z,
                    EndX = 0,
                    EndY = 0,
                    EndZ = 0,
                    Label = label,
                    IsSelected = isSelected,
                    IsHovered = isHovered,
                    DataPointIndex = i,
                    Pace = point.Pace
                };

                specs.Add(spec);
            }

            return specs;
        }
    }
}


