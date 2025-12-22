using System.Collections.Generic;
using Common.Annotations;
using Common.Domain;

namespace PolarChartLib.Services
{
    /// <summary>
    /// Abstraction for rendering Common annotation specs to a LightningChart MVVM polar chart.
    /// </summary>
    public interface IPolarChartRenderer
    {
        void RenderAnnotations(IReadOnlyList<AnnotationSpec> specs, ProcessedDataSet dataSet);

        int? FindNearestAnnotation(double mouseAngle, double mouseAmplitude, double angleThreshold = 15.0, double amplitudeThreshold = 20.0);
    }
}

