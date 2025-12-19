using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Common.Annotations;
using Common.Domain;
using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Annotations;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;

namespace PolarChartPoC.Adapters
{
    public class PolarChartRenderer
    {
        private readonly ViewPolar viewPolar;
        private readonly AnnotationPolarCollection annotationCollection;
        private readonly Dictionary<string, AnnotationPolar> annotationCache;

        public PolarChartRenderer(ViewPolar viewPolar, AnnotationPolarCollection annotationCollection)
        {
            this.viewPolar = viewPolar ?? throw new ArgumentNullException(nameof(viewPolar));
            this.annotationCollection = annotationCollection ?? throw new ArgumentNullException(nameof(annotationCollection));
            this.annotationCache = new Dictionary<string, AnnotationPolar>();
        }

        public void RenderAnnotations(IReadOnlyList<AnnotationSpec> specs, ProcessedDataSet dataSet)
        {
            if (specs == null || dataSet == null)
                return;

            // Clear existing annotations
            annotationCollection.Clear();
            annotationCache.Clear();

            // Render each annotation spec
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec is ArrowAnnotationSpec arrowSpec)
                {
                    var annotation = CreatePolarAnnotationFromSpec(arrowSpec, i, dataSet);
                    if (annotation != null)
                    {
                        annotationCollection.Add(annotation);
                        annotationCache[spec.Id] = annotation;
                    }
                }
            }
        }

        private AnnotationPolar? CreatePolarAnnotationFromSpec(ArrowAnnotationSpec spec, int index, ProcessedDataSet dataSet)
        {
            if (viewPolar == null || viewPolar.Axes == null || viewPolar.Axes.Count == 0)
                return null;

            // Get the data point from the dataset
            if (index >= dataSet.DataPoints.Count)
                return null;

            var dataPoint = dataSet.DataPoints[index];

            // Convert 3D coordinates to polar coordinates
            // For polar chart: angle is azimuth, amplitude is distance from origin in XY plane
            var (azimuth, elevation, radius) = dataPoint.ToSpherical();
            
            // Use XY plane projection for amplitude
            double amplitude = Math.Sqrt(dataPoint.X * dataPoint.X + dataPoint.Y * dataPoint.Y);

            var annotation = new AnnotationPolar(viewPolar, viewPolar.Axes[0]);
            
            // Configure arrow from origin to point
            annotation.Style = AnnotationStyle.Arrow;
            annotation.LocationCoordinateSystem = CoordinateSystem.AxisValues;
            annotation.LocationAxisValues.Angle = 0;
            annotation.LocationAxisValues.Amplitude = 0;
            annotation.TargetAxisValues.Angle = azimuth;
            annotation.TargetAxisValues.Amplitude = amplitude;
            
            // Configure styling
            annotation.ArrowStyleBegin = ArrowStyle.None;
            annotation.ArrowStyleEnd = ArrowStyle.Arrow;
            annotation.ArrowLineStyle.Color = spec.Color;
            annotation.AllowUserInteraction = false;

            // Configure line width based on selection/hover state
            if (spec.IsSelected)
            {
                annotation.ArrowLineStyle.Width = 6;
                annotation.ArrowLineStyle.Color = Colors.Yellow;
            }
            else if (spec.IsHovered)
            {
                annotation.ArrowLineStyle.Width = 4;
            }
            else
            {
                annotation.ArrowLineStyle.Width = 2;
            }

            // Configure text label
            if (!string.IsNullOrEmpty(spec.Label))
            {
                annotation.TextStyle.Visible = true;
                annotation.Text = spec.Label;
                annotation.TextStyle.Color = spec.IsSelected ? Colors.Yellow : spec.Color;
                
                if (spec.IsSelected)
                {
                    annotation.TextStyle.Font = new WpfFont("Segoe UI", 12, true, false);
                }
            }
            else
            {
                annotation.TextStyle.Visible = false;
            }

            return annotation;
        }

        public int? FindNearestAnnotation(double mouseAngle, double mouseAmplitude, double angleThreshold = 15.0, double amplitudeThreshold = 20.0)
        {
            if (annotationCollection == null || annotationCollection.Count == 0)
                return null;

            int nearestIndex = -1;
            double minDistance = double.MaxValue;

            for (int i = 0; i < annotationCollection.Count; i++)
            {
                var annotation = annotationCollection[i];
                double targetAngle = annotation.TargetAxisValues.Angle;
                double targetAmplitude = annotation.TargetAxisValues.Amplitude;

                // Calculate angular distance (handle wrapping around 360)
                double angleDiff = Math.Abs(mouseAngle - targetAngle);
                if (angleDiff > 180)
                    angleDiff = 360 - angleDiff;

                double amplitudeDiff = Math.Abs(mouseAmplitude - targetAmplitude);

                // Calculate combined distance (weighted)
                double distance = Math.Sqrt(angleDiff * angleDiff + amplitudeDiff * amplitudeDiff);

                if (angleDiff < angleThreshold && amplitudeDiff < amplitudeThreshold && distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex >= 0 ? nearestIndex : null;
        }
    }
}


