using System;
using System.Collections.Generic;
using System.Windows.Media;
using Common.Annotations;
using Common.Domain;
using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Annotations;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;

namespace PolarChartLib.Services
{
    /// <summary>
    /// Default implementation of IPolarChartRenderer using LightningChart MVVM.
    /// </summary>
    internal class PolarChartRenderer : IPolarChartRenderer
    {
        private readonly ViewPolar viewPolar;
        private readonly AnnotationPolarCollection annotationCollection;
        private readonly Dictionary<string, AnnotationPolar> annotationCache;

        public PolarChartRenderer(ViewPolar viewPolar, AnnotationPolarCollection annotationCollection)
        {
            this.viewPolar = viewPolar ?? throw new ArgumentNullException(nameof(viewPolar));
            this.annotationCollection = annotationCollection ?? throw new ArgumentNullException(nameof(annotationCollection));
            annotationCache = new Dictionary<string, AnnotationPolar>();
        }

        public void RenderAnnotations(IReadOnlyList<AnnotationSpec> specs, ProcessedDataSet dataSet)
        {
            if (specs == null || dataSet == null)
                return;

            // Ensure collection can hold the new count
            // We do NOT clear the collection here, we reuse existing items.
            
            // First, hide any extra annotations if we have fewer specs than current annotations
            for (int i = specs.Count; i < annotationCollection.Count; i++)
            {
                annotationCollection[i].Visible = false;
            }

            // Update existing or add new annotations
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec is ArrowAnnotationSpec arrowSpec)
                {
                    if (i < annotationCollection.Count)
                    {
                        // Reuse existing annotation
                        var annotation = annotationCollection[i];
                        UpdateCallbackAnnotation(annotation, arrowSpec, i, dataSet);
                        annotation.Visible = true;
                    }
                    else
                    {
                        // Create new annotation
                        var annotation = CreatePolarAnnotationFromSpec(arrowSpec, i, dataSet);
                        if (annotation != null)
                        {
                            annotationCollection.Add(annotation);
                        }
                    }
                }
            }
        }

        private void UpdateCallbackAnnotation(AnnotationPolar annotation, ArrowAnnotationSpec spec, int index, ProcessedDataSet dataSet)
        {
             if (index >= dataSet.DataPoints.Count)
             {
                 annotation.Visible = false;
                 return;
             }

            var dataPoint = dataSet.DataPoints[index];
            var (azimuth, _, _) = dataPoint.ToSpherical();
            double amplitude = Math.Sqrt(dataPoint.X * dataPoint.X + dataPoint.Y * dataPoint.Y);

            // Update positions
            // LightningChart polar chart has 0° at top (12 o'clock), but our azimuth has 0° at right (3 o'clock)
            // Adjust by -90° to align: 0° (right) → -90° → 270° (top in polar chart)
            double polarAngle = (azimuth - 90.0 + 360.0) % 360.0;
            annotation.TargetAxisValues.Angle = polarAngle;
            annotation.TargetAxisValues.Amplitude = amplitude;

            // Update styling
            annotation.ArrowLineStyle.Color = spec.Color;

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

            if (!string.IsNullOrEmpty(spec.Label))
            {
                annotation.TextStyle.Visible = true;
                annotation.Text = spec.Label;
                annotation.TextStyle.Color = spec.IsSelected ? Colors.Yellow : spec.Color;

                if (spec.IsSelected)
                {
                    annotation.TextStyle.Font = new WpfFont("Segoe UI", 12, true, false);
                }
                else
                {
                     // Reset font if needed, or keep default
                     annotation.TextStyle.Font = new WpfFont("Segoe UI", 10, false, false);
                }
            }
            else
            {
                annotation.TextStyle.Visible = false;
                annotation.Text = string.Empty; // Clear any default text
            }
        }

        private AnnotationPolar? CreatePolarAnnotationFromSpec(ArrowAnnotationSpec spec, int index, ProcessedDataSet dataSet)
        {
            if (viewPolar == null || viewPolar.Axes == null || viewPolar.Axes.Count == 0)
                return null;

            // Reuse the update logic for consistency
            var annotation = new AnnotationPolar(viewPolar, viewPolar.Axes[0]);
            
            annotation.Style = AnnotationStyle.Arrow;
            annotation.LocationCoordinateSystem = CoordinateSystem.AxisValues;
            
            // Fixed origin for vector style
            annotation.LocationAxisValues.Angle = 0;
            annotation.LocationAxisValues.Amplitude = 0;
            
            annotation.ArrowStyleBegin = ArrowStyle.None;
            annotation.ArrowStyleEnd = ArrowStyle.Arrow;
            annotation.AllowUserInteraction = false;

            UpdateCallbackAnnotation(annotation, spec, index, dataSet);
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
                if (!annotation.Visible) continue; // Skip hidden pooled items

                double targetAngle = annotation.TargetAxisValues.Angle;
                double targetAmplitude = annotation.TargetAxisValues.Amplitude;

                double angleDiff = Math.Abs(mouseAngle - targetAngle);
                if (angleDiff > 180)
                    angleDiff = 360 - angleDiff;

                double amplitudeDiff = Math.Abs(mouseAmplitude - targetAmplitude);

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

