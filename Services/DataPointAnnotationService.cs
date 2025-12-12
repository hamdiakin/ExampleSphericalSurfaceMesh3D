using InteractiveExamples.Models;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Views.View3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace InteractiveExamples.Services
{
    public class DataPointAnnotationService
    {
        private readonly View3D view3D;
        private readonly List<SphereDataPoint> dataPoints;
        private readonly List<Annotation3D> annotations;

        public DataPointAnnotationService(View3D view3D)
        {
            this.view3D = view3D ?? throw new ArgumentNullException(nameof(view3D));
            this.dataPoints = new List<SphereDataPoint>();
            this.annotations = new List<Annotation3D>();
        }

        public IReadOnlyList<SphereDataPoint> DataPoints => dataPoints.AsReadOnly();

        public IReadOnlyList<Annotation3D> Annotations => annotations.AsReadOnly();

        private int? hoveredAnnotationIndex = null;

        public void GenerateDataPoints(int n, Random? random = null)
        {
            if (n < 0)
                throw new ArgumentException("Number of data points must be non-negative.", nameof(n));

            random ??= new Random();

            ClearExistingData();

            for (int i = 0; i < n; i++)
            {
                SphereDataPoint dataPoint = SphereDataPoint.GenerateRandom(random);
                dataPoints.Add(dataPoint);

                Annotation3D annotation = CreateAnnotationForDataPoint(dataPoint);
                annotations.Add(annotation);
                view3D.Annotations.Add(annotation);
            }
        }

        public void ClearExistingData()
        {
            foreach (var annotation in annotations)
            {
                view3D.Annotations.Remove(annotation);
            }

            dataPoints.Clear();
            annotations.Clear();
        }

        public void UpdateDataPoint(int index, SphereDataPoint updatedDataPoint)
        {
            if (index < 0 || index >= dataPoints.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            if (updatedDataPoint == null)
                throw new ArgumentNullException(nameof(updatedDataPoint));

            dataPoints[index] = updatedDataPoint;
            UpdateAnnotationForDataPoint(index, updatedDataPoint);
        }

        public void UpdateDataPoint(SphereDataPoint oldDataPoint, SphereDataPoint newDataPoint)
        {
            if (oldDataPoint == null)
                throw new ArgumentNullException(nameof(oldDataPoint));
            if (newDataPoint == null)
                throw new ArgumentNullException(nameof(newDataPoint));

            int index = dataPoints.IndexOf(oldDataPoint);
            if (index == -1)
                throw new ArgumentException("Data point not found in the collection.", nameof(oldDataPoint));

            UpdateDataPoint(index, newDataPoint);
        }

        public void UpdateAllAnnotations()
        {
            for (int i = 0; i < dataPoints.Count; i++)
            {
                UpdateAnnotationForDataPoint(i, dataPoints[i]);
            }
        }

        /// <summary>
        /// Updates all data points moving clockwise around the sphere, each with its own pace
        /// </summary>
        /// <param name="deltaTimeSeconds">Time elapsed since last update in seconds</param>
        public void UpdateDataPointsClockwise(double deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0) return;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                SphereDataPoint dataPoint = dataPoints[i];
                
                // Calculate angular distance based on this point's pace
                double angularDistance = dataPoint.Pace * deltaTimeSeconds;
                
                // Move the point clockwise
                dataPoint.MoveClockwise(angularDistance);
                
                // Update the annotation position to match the new data point position
                UpdateAnnotationForDataPoint(i, dataPoint);
                
                // If this annotation is currently hovered, update its text with new X, Y values
                if (hoveredAnnotationIndex == i)
                {
                    ShowAnnotationText(i);
                }
            }
        }

        public void UpdateAnnotationForDataPoint(int index, SphereDataPoint dataPoint)
        {
            if (index < 0 || index >= annotations.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            if (dataPoint == null)
                throw new ArgumentNullException(nameof(dataPoint));

            Annotation3D annotation = annotations[index];
            
            // Preserve text if this annotation is currently being hovered
            string preservedText = (hoveredAnnotationIndex == index) ? annotation.Text : null;
            
            ConfigureAnnotation(annotation, dataPoint);
            
            // Restore text if it was preserved
            if (preservedText != null && hoveredAnnotationIndex == index)
            {
                annotation.Text = preservedText;
            }
        }

        private Annotation3D CreateAnnotationForDataPoint(SphereDataPoint dataPoint)
        {
            Annotation3D annotation = new Annotation3D(
                view3D,
                Axis3DBinding.Primary,
                Axis3DBinding.Primary,
                Axis3DBinding.Primary)
            {
                TargetCoordinateSystem = AnnotationTargetCoordinates.AxisValues
            };

            ConfigureAnnotation(annotation, dataPoint);

            return annotation;
        }

        private void ConfigureAnnotation(Annotation3D annotation, SphereDataPoint dataPoint)
        {
            annotation.LocationCoordinateSystem = CoordinateSystem.AxisValues;
            annotation.LocationAxisValues.SetValues(0, 0, 0);
            annotation.TargetAxisValues.SetValues(dataPoint.X, dataPoint.Y, dataPoint.Z);
            annotation.Style = AnnotationStyle.Arrow;
            annotation.ArrowStyleBegin = ArrowStyle.Circle;
            annotation.ArrowStyleEnd = ArrowStyle.Arrow;
            annotation.AllowUserInteraction = false;
            annotation.ArrowLineStyle.Color = dataPoint.Color;
            annotation.TextStyle.Color = dataPoint.Color;
            annotation.Visible = true;
            annotation.Anchor.Y = 1;
            annotation.Fill.Style = RectFillStyle.None;
            annotation.BorderVisible = false;
            annotation.Shadow.Visible = false;
        }

        /// <summary>
        /// Handles mouse move to detect proximity to annotations and show/hide X, Y values
        /// </summary>
        /// <param name="chart">The LightningChart instance</param>
        /// <param name="mousePosition">Mouse position in screen coordinates</param>
        /// <param name="proximityThreshold">Distance threshold in pixels for considering mouse "near" an annotation</param>
        public void HandleMouseMove(LightningChart chart, Point mousePosition, double proximityThreshold = 50.0)
        {
            if (chart == null || annotations.Count == 0) return;

            int? nearestIndex = FindNearestAnnotation(chart, mousePosition, proximityThreshold);

            // If hovering over a different annotation or no annotation, update accordingly
            if (nearestIndex != hoveredAnnotationIndex)
            {
                // Clear previous hovered annotation text
                if (hoveredAnnotationIndex.HasValue && hoveredAnnotationIndex.Value < annotations.Count)
                {
                    ClearAnnotationText(hoveredAnnotationIndex.Value);
                }

                // Set new hovered annotation text
                if (nearestIndex.HasValue)
                {
                    ShowAnnotationText(nearestIndex.Value);
                }

                hoveredAnnotationIndex = nearestIndex;
            }
        }

        /// <summary>
        /// Finds the nearest annotation to the mouse position
        /// Uses a simplified approach: calculates which annotation's 3D position projects closest to mouse
        /// </summary>
        private int? FindNearestAnnotation(LightningChart chart, Point mousePosition, double threshold)
        {
            if (chart?.View3D == null) return null;

            int nearestIndex = -1;
            double minDistance = double.MaxValue;

            // Get chart center and dimensions for projection calculation
            double chartCenterX = chart.ActualWidth / 2.0;
            double chartCenterY = chart.ActualHeight / 2.0;

            for (int i = 0; i < annotations.Count; i++)
            {
                Annotation3D annotation = annotations[i];
                if (!annotation.Visible || i >= dataPoints.Count) continue;

                // Get the data point's position in 3D space
                SphereDataPoint dataPoint = dataPoints[i];
                
                // Simplified projection: map 3D coordinates to screen space
                // This is an approximation - for accurate results, you'd need the proper coordinate conversion API
                // We'll use the X,Y coordinates scaled to screen space as an approximation
                double screenX = chartCenterX + (dataPoint.X / 100.0) * (chart.ActualWidth / 4.0);
                double screenY = chartCenterY - (dataPoint.Y / 100.0) * (chart.ActualHeight / 4.0);
                
                // Adjust for Z depth (points further away appear smaller)
                double zScale = 1.0 + (dataPoint.Z / 200.0);
                screenX *= zScale;
                screenY *= zScale;

                // Calculate distance from mouse to projected annotation position
                double distance = Math.Sqrt(
                    Math.Pow(screenX - mousePosition.X, 2) +
                    Math.Pow(screenY - mousePosition.Y, 2)
                );

                if (distance < threshold && distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex >= 0 ? nearestIndex : null;
        }

        /// <summary>
        /// Shows X and Y values as text on the annotation
        /// </summary>
        private void ShowAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            // Update text with current X and Y values
            annotation.Text = $"X: {dataPoint.X:F1}\nY: {dataPoint.Y:F1}";
            
            // Update text position to be at the top of the line (arrow end)
            annotation.Anchor.Y = 1; // Top of annotation
        }

        /// <summary>
        /// Clears the annotation text
        /// </summary>
        private void ClearAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count) return;

            Annotation3D annotation = annotations[index];
            annotation.Text = string.Empty;
        }

        /// <summary>
        /// Clears hover state (call when mouse leaves chart area)
        /// </summary>
        public void ClearHoverState()
        {
            if (hoveredAnnotationIndex.HasValue && hoveredAnnotationIndex.Value < annotations.Count)
            {
                ClearAnnotationText(hoveredAnnotationIndex.Value);
                hoveredAnnotationIndex = null;
            }
        }
    }
}
