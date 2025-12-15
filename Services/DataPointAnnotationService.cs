using InteractiveExamples.Models;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Views.View3D;
using System;
using System.Collections.Generic;
using System.Windows;

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

        public int AnnotationCount => annotations.Count;

        private int? hoveredAnnotationIndex = null;
        private bool isMouseTrackingEnabled = true;
        private int? selectedAnnotationIndex = null;

        public int? SelectedAnnotationIndex => selectedAnnotationIndex;

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
                
                // Update text based on selection and mouse tracking state
                bool isSelected = selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value == i;
                
                if (isSelected)
                {
                    // Always update selected annotation text with special format
                    UpdateSelectedAnnotationText(i);
                }
                else if (!isMouseTrackingEnabled)
                {
                    // When mouse tracking is disabled, update all texts
                    ShowAnnotationText(i);
                }
                else if (hoveredAnnotationIndex == i)
                {
                    // When mouse tracking is enabled, only update hovered annotation text
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
            bool isSelected = selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value == index;
            
            // For selected annotation, only update position, preserve all styling
            if (isSelected)
            {
                // Only update position coordinates
                annotation.LocationAxisValues.SetValues(dataPoint.X, dataPoint.Y, dataPoint.Z);
                return;
            }
            
            // Preserve text if this annotation is currently being hovered
            string? preservedText = null;
            if (hoveredAnnotationIndex == index)
            {
                preservedText = annotation.Text;
            }
            
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
            // Text appears at Location, so set Location to the data point (tip) and Target to center
            annotation.LocationAxisValues.SetValues(dataPoint.X, dataPoint.Y, dataPoint.Z);
            annotation.TargetAxisValues.SetValues(0, 0, 0);
            annotation.Style = AnnotationStyle.Arrow;
            // Swap arrow styles: circle at center (Target/end), arrow at data point (Location/begin)
            annotation.ArrowStyleBegin = ArrowStyle.Arrow;
            annotation.ArrowStyleEnd = ArrowStyle.Circle;
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
        /// Sets whether mouse tracking is enabled for showing annotation text
        /// </summary>
        /// <param name="enabled">True to enable mouse tracking (show text on hover), false to show all texts</param>
        public void SetMouseTrackingEnabled(bool enabled)
        {
            isMouseTrackingEnabled = enabled;
            
            if (enabled)
            {
                // Clear all texts when enabling mouse tracking
                ClearAllAnnotationTexts();
            }
            else
            {
                // Show all texts when disabling mouse tracking
                ShowAllAnnotationTexts();
            }
        }

        /// <summary>
        /// Handles mouse move to detect proximity to annotations and show/hide X, Y values
        /// </summary>
        /// <param name="chart">The LightningChart instance</param>
        /// <param name="mousePosition">Mouse position in screen coordinates</param>
        /// <param name="proximityThreshold">Distance threshold in pixels for considering mouse "near" an annotation</param>
        public void HandleMouseMove(LightningChart chart, Point mousePosition, double proximityThreshold = 80.0)
        {
            if (chart == null || annotations.Count == 0) return;

            // If mouse tracking is disabled, don't process mouse movement
            if (!isMouseTrackingEnabled) return;

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
        /// Uses camera rotation to improve 3D to 2D projection accuracy
        /// </summary>
        private int? FindNearestAnnotation(LightningChart chart, Point mousePosition, double threshold)
        {
            if (chart?.View3D == null) return null;

            int nearestIndex = -1;
            double minDistance = double.MaxValue;

            // Get chart dimensions
            double chartWidth = chart.ActualWidth;
            double chartHeight = chart.ActualHeight;
            double chartCenterX = chartWidth / 2.0;
            double chartCenterY = chartHeight / 2.0;

            // Get camera rotation angles in radians
            double rotX = chart.View3D.Camera.RotationX * Math.PI / 180.0;
            double rotY = chart.View3D.Camera.RotationY * Math.PI / 180.0;
            double rotZ = chart.View3D.Camera.RotationZ * Math.PI / 180.0;

            for (int i = 0; i < annotations.Count; i++)
            {
                Annotation3D annotation = annotations[i];
                if (!annotation.Visible || i >= dataPoints.Count) continue;

                // Get the data point's position in 3D space
                SphereDataPoint dataPoint = dataPoints[i];
                double x = dataPoint.X;
                double y = dataPoint.Y;
                double z = dataPoint.Z;

                // Apply rotation transformations (simplified - rotate around Y axis first, then X)
                // Rotate around Y axis
                double cosY = Math.Cos(rotY);
                double sinY = Math.Sin(rotY);
                double x1 = x * cosY + z * sinY;
                double z1 = -x * sinY + z * cosY;

                // Rotate around X axis
                double cosX = Math.Cos(rotX);
                double sinX = Math.Sin(rotX);
                double y1 = y * cosX - z1 * sinX;
                double z2 = y * sinX + z1 * cosX;

                // Project to screen (orthographic-like projection)
                double scale = Math.Min(chartWidth, chartHeight) / 300.0;
                double screenX = chartCenterX + x1 * scale;
                double screenY = chartCenterY - y1 * scale;

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
        /// Shows index, X and Y values as text on the annotation
        /// </summary>
        private void ShowAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;
            
            // Don't overwrite selected annotation's special styling
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value == index)
            {
                return;
            }

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            // Update text with index and current X, Y values
            annotation.Text = $"[{index}]\nX: {dataPoint.X:F1}\nY: {dataPoint.Y:F1}";
            
            // Update text position to be at the tip of the arrow (Target end)
            annotation.Anchor.Y = 1; // End of annotation (arrow tip)
        }
        
        /// <summary>
        /// Updates text for selected annotation (with special format, preserves styling)
        /// </summary>
        private void UpdateSelectedAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            // Update text with special selected format and current X, Y values
            annotation.Text = $">>> [{index}] <<<\nX: {dataPoint.X:F1}\nY: {dataPoint.Y:F1}";
            annotation.Anchor.Y = 1;
        }

        /// <summary>
        /// Clears the annotation text (but not for selected annotation)
        /// </summary>
        private void ClearAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count) return;
            
            // Don't clear text for selected annotation - it should always be visible
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value == index)
            {
                return;
            }

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

        /// <summary>
        /// Adds a random annotation to the chart
        /// </summary>
        /// <param name="random">Optional random number generator</param>
        public void AddRandomAnnotation(Random? random = null)
        {
            random ??= new Random();

            SphereDataPoint dataPoint = SphereDataPoint.GenerateRandom(random);
            dataPoints.Add(dataPoint);

            Annotation3D annotation = CreateAnnotationForDataPoint(dataPoint);
            annotations.Add(annotation);
            view3D.Annotations.Add(annotation);
        }

        /// <summary>
        /// Deletes the last annotation from the chart
        /// </summary>
        public void DeleteLastAnnotation()
        {
            if (annotations.Count == 0) return;

            int lastIndex = annotations.Count - 1;

            // Clear hover state if the last annotation is hovered
            if (hoveredAnnotationIndex == lastIndex)
            {
                ClearAnnotationText(lastIndex);
                hoveredAnnotationIndex = null;
            }
            else if (hoveredAnnotationIndex > lastIndex)
            {
                hoveredAnnotationIndex = null;
            }

            Annotation3D annotation = annotations[lastIndex];
            view3D.Annotations.Remove(annotation);
            annotations.RemoveAt(lastIndex);
            dataPoints.RemoveAt(lastIndex);
        }

        /// <summary>
        /// Deletes all annotations from the chart
        /// </summary>
        public void DeleteAllAnnotations()
        {
            ClearExistingData();
            hoveredAnnotationIndex = null;
        }

        /// <summary>
        /// Shows text on all annotations (preserving selected annotation's special styling)
        /// </summary>
        private void ShowAllAnnotationTexts()
        {
            for (int i = 0; i < annotations.Count && i < dataPoints.Count; i++)
            {
                // Skip selected annotation - it has its own special styling
                if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value == i)
                {
                    continue;
                }
                
                Annotation3D annotation = annotations[i];
                SphereDataPoint dataPoint = dataPoints[i];
                annotation.Text = $"[{i}]\nX: {dataPoint.X:F1}\nY: {dataPoint.Y:F1}";
                annotation.Anchor.Y = 1;
            }
        }

        /// <summary>
        /// Clears text on all annotations (but not for selected annotation)
        /// </summary>
        private void ClearAllAnnotationTexts()
        {
            for (int i = 0; i < annotations.Count; i++)
            {
                // Skip selected annotation - it should always be visible
                if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value == i)
                {
                    continue;
                }
                
                annotations[i].Text = string.Empty;
            }
            hoveredAnnotationIndex = null;
        }

        /// <summary>
        /// Selects an annotation by clicking near it
        /// </summary>
        /// <param name="chart">The LightningChart instance</param>
        /// <param name="mousePosition">Mouse position in screen coordinates</param>
        /// <param name="proximityThreshold">Distance threshold in pixels</param>
        /// <returns>True if selection state changed, false otherwise</returns>
        public bool SelectAnnotationAtPosition(LightningChart chart, Point mousePosition, double proximityThreshold = 80.0)
        {
            if (chart == null) return false;

            int? nearestIndex = annotations.Count > 0 
                ? FindNearestAnnotation(chart, mousePosition, proximityThreshold) 
                : null;

            // Clear previous selection visual
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value < annotations.Count)
            {
                ClearSelectionVisual(selectedAnnotationIndex.Value);
            }

            int? previousSelection = selectedAnnotationIndex;
            selectedAnnotationIndex = nearestIndex;

            // Apply selection visual to new selection
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value < annotations.Count)
            {
                ApplySelectionVisual(selectedAnnotationIndex.Value);
            }

            // Return true if selection changed (including from something to nothing or nothing to something)
            return previousSelection != selectedAnnotationIndex;
        }

        /// <summary>
        /// Selects an annotation by its index
        /// </summary>
        /// <param name="index">Index of the annotation to select (-1 or out of range clears selection)</param>
        /// <returns>True if selection was successful, false if index is invalid</returns>
        public bool SelectAnnotationByIndex(int index)
        {
            // Clear previous selection visual
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value < annotations.Count)
            {
                ClearSelectionVisual(selectedAnnotationIndex.Value);
            }

            // Check if index is valid
            if (index < 0 || index >= annotations.Count)
            {
                selectedAnnotationIndex = null;
                return false;
            }

            selectedAnnotationIndex = index;
            ApplySelectionVisual(index);
            return true;
        }

        /// <summary>
        /// Clears the current selection
        /// </summary>
        public void ClearSelection()
        {
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value < annotations.Count)
            {
                ClearSelectionVisual(selectedAnnotationIndex.Value);
            }
            selectedAnnotationIndex = null;
        }

        /// <summary>
        /// Deletes the currently selected annotation
        /// </summary>
        /// <returns>True if an annotation was deleted, false if nothing was selected</returns>
        public bool DeleteSelectedAnnotation()
        {
            if (!selectedAnnotationIndex.HasValue || selectedAnnotationIndex.Value >= annotations.Count)
                return false;

            int indexToDelete = selectedAnnotationIndex.Value;

            // Clear hover state if the selected annotation is hovered
            if (hoveredAnnotationIndex == indexToDelete)
            {
                ClearAnnotationText(indexToDelete);
                hoveredAnnotationIndex = null;
            }
            else if (hoveredAnnotationIndex > indexToDelete)
            {
                hoveredAnnotationIndex--;
            }

            // Remove the annotation
            Annotation3D annotation = annotations[indexToDelete];
            view3D.Annotations.Remove(annotation);
            annotations.RemoveAt(indexToDelete);
            dataPoints.RemoveAt(indexToDelete);

            // Clear selection
            selectedAnnotationIndex = null;

            return true;
        }

        /// <summary>
        /// Gets information about the selected annotation
        /// </summary>
        public (int index, double x, double y, double z)? GetSelectedAnnotationInfo()
        {
            if (!selectedAnnotationIndex.HasValue || selectedAnnotationIndex.Value >= dataPoints.Count)
                return null;

            var dataPoint = dataPoints[selectedAnnotationIndex.Value];
            return (selectedAnnotationIndex.Value, dataPoint.X, dataPoint.Y, dataPoint.Z);
        }

        /// <summary>
        /// Applies visual feedback for selected annotation (bold text, thicker line, visible text, highlight color)
        /// </summary>
        private void ApplySelectionVisual(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            // Make the line much thicker for selected annotation
            annotation.ArrowLineStyle.Width = 4;
            
            // Make text bold and larger
            annotation.TextStyle.Font = new WpfFont("Segoe UI", 14, true, false);
            
            // Set highlight color (bright yellow for visibility)
            annotation.TextStyle.Color = System.Windows.Media.Colors.Yellow;
            annotation.ArrowLineStyle.Color = System.Windows.Media.Colors.Yellow;
            
            // Add visible border
            annotation.BorderVisible = true;
            annotation.BorderLineStyle.Color = System.Windows.Media.Colors.Yellow;
            annotation.BorderLineStyle.Width = 2;
            
            // Add shadow for more visibility
            annotation.Shadow.Visible = true;
            annotation.Shadow.Color = System.Windows.Media.Colors.Black;
            
            // Always show text for selected annotation
            annotation.Text = $">>> [{index}] <<<\nX: {dataPoint.X:F1}\nY: {dataPoint.Y:F1}";
            annotation.Anchor.Y = 1;
        }

        /// <summary>
        /// Clears visual feedback for selected annotation
        /// </summary>
        private void ClearSelectionVisual(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];
            
            // Reset line width to default
            annotation.ArrowLineStyle.Width = 1;
            
            // Reset text to normal weight
            annotation.TextStyle.Font = new WpfFont("Segoe UI", 10, false, false);
            
            // Restore original color
            annotation.TextStyle.Color = dataPoint.Color;
            annotation.ArrowLineStyle.Color = dataPoint.Color;
            
            // Remove border and shadow
            annotation.BorderVisible = false;
            annotation.Shadow.Visible = false;
            
            // Clear text if mouse tracking is enabled and not hovered
            if (isMouseTrackingEnabled && hoveredAnnotationIndex != index)
            {
                annotation.Text = string.Empty;
            }
            else
            {
                // Restore normal text format
                annotation.Text = $"[{index}]\nX: {dataPoint.X:F1}\nY: {dataPoint.Y:F1}";
            }
        }
    }
}
