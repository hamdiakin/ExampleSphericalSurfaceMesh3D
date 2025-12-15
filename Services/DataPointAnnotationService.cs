using InteractiveExamples.Models;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Views.View3D;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace InteractiveExamples.Services
{
    /// <summary>
    /// High-performance annotation service optimized for handling thousands of annotations.
    /// Key optimizations:
    /// - Spatial grid for O(1) average-case mouse proximity detection
    /// - Batch operations to minimize chart redraws
    /// - Object pooling and StringBuilder reuse to reduce GC pressure
    /// - Cached projections to avoid redundant calculations
    /// </summary>
    public class DataPointAnnotationService
    {
        #region Fields

        private readonly View3D view3D;
        private readonly List<SphereDataPoint> dataPoints;
        private readonly List<Annotation3D> annotations;

        // Performance optimization: Spatial grid for fast proximity detection
        private readonly SpatialGrid spatialGrid;
        
        // Performance optimization: StringBuilder pool to reduce string allocations
        private readonly StringBuilder stringBuilder = new StringBuilder(64);
        
        // Performance optimization: Cached projection data
        private double cachedChartWidth;
        private double cachedChartHeight;
        private double cachedCenterX;
        private double cachedCenterY;
        private double cachedCosY;
        private double cachedSinY;
        private double cachedCosX;
        private double cachedSinX;
        private double cachedScale;
        private bool projectionCacheValid = false;

        // State tracking
        private int? hoveredAnnotationIndex = null;
        private bool isMouseTrackingEnabled = true;
        private int? selectedAnnotationIndex = null;

        // Performance optimization: Batch update tracking
        private bool isBatchUpdate = false;
        private readonly HashSet<int> pendingTextUpdates = new HashSet<int>();

        #endregion

        #region Constructor

        public DataPointAnnotationService(View3D view3D)
        {
            this.view3D = view3D ?? throw new ArgumentNullException(nameof(view3D));
            this.dataPoints = new List<SphereDataPoint>();
            this.annotations = new List<Annotation3D>();
            this.spatialGrid = new SpatialGrid(cellSize: 50.0); // 50 pixel cells for spatial hashing
        }

        #endregion

        #region Properties

        public IReadOnlyList<SphereDataPoint> DataPoints => dataPoints.AsReadOnly();

        public IReadOnlyList<Annotation3D> Annotations => annotations.AsReadOnly();

        public int AnnotationCount => annotations.Count;

        public int? SelectedAnnotationIndex => selectedAnnotationIndex;

        #endregion

        #region Batch Operations

        /// <summary>
        /// Begins a batch update operation. Use this when performing multiple modifications
        /// to minimize chart redraws and improve performance.
        /// </summary>
        public void BeginBatchUpdate()
        {
            isBatchUpdate = true;
            pendingTextUpdates.Clear();
        }

        /// <summary>
        /// Ends a batch update operation and applies all pending changes.
        /// </summary>
        public void EndBatchUpdate()
        {
            if (!isBatchUpdate) return;
            
            isBatchUpdate = false;
            
            // Apply any pending text updates
            foreach (int index in pendingTextUpdates)
            {
                if (index >= 0 && index < annotations.Count)
                {
                    ApplyTextUpdate(index);
                }
            }
            pendingTextUpdates.Clear();
        }

        #endregion

        #region Data Point Management

        /// <summary>
        /// Generates data points with optimized batch operations.
        /// For 1000+ annotations, this uses pre-allocation and batch chart operations.
        /// </summary>
        public void GenerateDataPoints(int n, Random? random = null)
        {
            if (n < 0)
                throw new ArgumentException("Number of data points must be non-negative.", nameof(n));

            random ??= new Random();

            ClearExistingData();

            // Pre-allocate capacity to avoid resizing
            dataPoints.Capacity = Math.Max(dataPoints.Capacity, n);
            annotations.Capacity = Math.Max(annotations.Capacity, n);

            // Create all annotations first (without adding to chart)
            var newAnnotations = new Annotation3D[n];
            for (int i = 0; i < n; i++)
            {
                SphereDataPoint dataPoint = SphereDataPoint.GenerateRandom(random);
                dataPoints.Add(dataPoint);

                Annotation3D annotation = CreateAnnotationForDataPoint(dataPoint);
                annotations.Add(annotation);
                newAnnotations[i] = annotation;
            }

            // Add all annotations to chart (LightningChart doesn't have AddRange, but 
            // adding them all after BeginUpdate minimizes redraws)
            foreach (var annotation in newAnnotations)
            {
                view3D.Annotations.Add(annotation);
            }
            
            // Invalidate spatial grid (will be rebuilt on next mouse move)
            InvalidateProjectionCache();
        }

        /// <summary>
        /// Clears all data with optimized batch operations.
        /// </summary>
        public void ClearExistingData()
        {
            // Remove annotations from chart
            if (annotations.Count > 0)
            {
                // Remove each annotation (we only remove our annotations, not all chart annotations)
                foreach (var annotation in annotations)
                {
                    view3D.Annotations.Remove(annotation);
                }
            }

            dataPoints.Clear();
            annotations.Clear();
            spatialGrid.Clear();
            hoveredAnnotationIndex = null;
            selectedAnnotationIndex = null;
            InvalidateProjectionCache();
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
            InvalidateProjectionCache();
        }

        #endregion

        #region Animation Updates

        /// <summary>
        /// Updates all data points moving clockwise around the sphere, each with its own pace.
        /// Optimized for thousands of annotations with minimal allocations.
        /// </summary>
        /// <param name="deltaTimeSeconds">Time elapsed since last update in seconds</param>
        public void UpdateDataPointsClockwise(double deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0 || dataPoints.Count == 0) return;

            int count = dataPoints.Count;
            bool hasSelection = selectedAnnotationIndex.HasValue;
            int selectionIdx = selectedAnnotationIndex.GetValueOrDefault(-1);
            bool showAllTexts = !isMouseTrackingEnabled;
            int hoveredIdx = hoveredAnnotationIndex ?? -1;

            // Update all positions first (position updates are cheap)
            for (int i = 0; i < count; i++)
            {
                SphereDataPoint dataPoint = dataPoints[i];
                
                // Calculate angular distance based on this point's pace
                double angularDistance = dataPoint.Pace * deltaTimeSeconds;
                
                // Move the point clockwise
                dataPoint.MoveClockwise(angularDistance);
                
                // Update only the position (fast path for most annotations)
                Annotation3D annotation = annotations[i];
                annotation.LocationAxisValues.SetValues(dataPoint.X, dataPoint.Y, dataPoint.Z);
            }

            // Update texts only for annotations that need it (selected, hovered, or all if tracking disabled)
            if (hasSelection && selectionIdx < count)
            {
                UpdateSelectedAnnotationTextOptimized(selectionIdx);
            }

            if (showAllTexts)
            {
                // Only update text for visible annotations (optimization: could add frustum culling)
                for (int i = 0; i < count; i++)
                {
                    if (i != selectionIdx) // Skip selected - already updated
                    {
                        UpdateAnnotationTextOptimized(i);
                    }
                }
            }
            else if (hoveredIdx >= 0 && hoveredIdx < count && hoveredIdx != selectionIdx)
            {
                UpdateAnnotationTextOptimized(hoveredIdx);
            }

            // Invalidate spatial grid since positions changed
            InvalidateProjectionCache();
        }

        #endregion

        #region Annotation Configuration

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

        /// <summary>
        /// Configures annotation properties. Optimized to set only necessary properties.
        /// </summary>
        private void ConfigureAnnotation(Annotation3D annotation, SphereDataPoint dataPoint)
        {
            annotation.LocationCoordinateSystem = CoordinateSystem.AxisValues;
            annotation.LocationAxisValues.SetValues(dataPoint.X, dataPoint.Y, dataPoint.Z);
            annotation.TargetAxisValues.SetValues(0, 0, 0);
            annotation.Style = AnnotationStyle.Arrow;
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

        #endregion

        #region Mouse Tracking

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
        /// Handles mouse move to detect proximity to annotations and show/hide X, Y values.
        /// Optimized with spatial hashing for O(1) average-case complexity.
        /// </summary>
        /// <param name="chart">The LightningChart instance</param>
        /// <param name="mousePosition">Mouse position in screen coordinates</param>
        /// <param name="proximityThreshold">Distance threshold in pixels for considering mouse "near" an annotation</param>
        public void HandleMouseMove(LightningChart chart, Point mousePosition, double proximityThreshold = 80.0)
        {
            if (chart == null || annotations.Count == 0) return;

            // If mouse tracking is disabled, don't process mouse movement
            if (!isMouseTrackingEnabled) return;

            // Update projection cache if needed
            UpdateProjectionCache(chart);

            int? nearestIndex = FindNearestAnnotationOptimized(mousePosition, proximityThreshold);

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
        /// Updates the projection cache and rebuilds spatial grid if needed.
        /// This caches the expensive camera calculations.
        /// </summary>
        private void UpdateProjectionCache(LightningChart chart)
        {
            if (chart?.View3D == null) return;

            double chartWidth = chart.ActualWidth;
            double chartHeight = chart.ActualHeight;
            double rotX = chart.View3D.Camera.RotationX * Math.PI / 180.0;
            double rotY = chart.View3D.Camera.RotationY * Math.PI / 180.0;

            // Check if cache is valid
            if (projectionCacheValid && 
                Math.Abs(cachedChartWidth - chartWidth) < 0.1 &&
                Math.Abs(cachedChartHeight - chartHeight) < 0.1)
            {
                return; // Cache is still valid
            }

            // Update cache
            cachedChartWidth = chartWidth;
            cachedChartHeight = chartHeight;
            cachedCenterX = chartWidth / 2.0;
            cachedCenterY = chartHeight / 2.0;
            cachedCosY = Math.Cos(rotY);
            cachedSinY = Math.Sin(rotY);
            cachedCosX = Math.Cos(rotX);
            cachedSinX = Math.Sin(rotX);
            cachedScale = Math.Min(chartWidth, chartHeight) / 300.0;

            // Rebuild spatial grid
            RebuildSpatialGrid();
            projectionCacheValid = true;
        }

        /// <summary>
        /// Invalidates the projection cache, forcing a rebuild on next mouse move.
        /// </summary>
        private void InvalidateProjectionCache()
        {
            projectionCacheValid = false;
        }

        /// <summary>
        /// Rebuilds the spatial grid for fast proximity detection.
        /// </summary>
        private void RebuildSpatialGrid()
        {
            spatialGrid.Clear();

            for (int i = 0; i < dataPoints.Count; i++)
            {
                var screenPos = Project3DTo2D(dataPoints[i]);
                spatialGrid.Insert(i, screenPos.x, screenPos.y);
            }
        }

        /// <summary>
        /// Projects a 3D data point to 2D screen coordinates using cached values.
        /// </summary>
        private (double x, double y) Project3DTo2D(SphereDataPoint dataPoint)
        {
            double x = dataPoint.X;
            double y = dataPoint.Y;
            double z = dataPoint.Z;

            // Rotate around Y axis
            double x1 = x * cachedCosY + z * cachedSinY;
            double z1 = -x * cachedSinY + z * cachedCosY;

            // Rotate around X axis
            double y1 = y * cachedCosX - z1 * cachedSinX;

            // Project to screen
            double screenX = cachedCenterX + x1 * cachedScale;
            double screenY = cachedCenterY - y1 * cachedScale;

            return (screenX, screenY);
        }

        /// <summary>
        /// Finds the nearest annotation using spatial grid for O(1) average case.
        /// Falls back to linear search only for nearby cells.
        /// </summary>
        private int? FindNearestAnnotationOptimized(Point mousePosition, double threshold)
        {
            // Get candidates from spatial grid (only annotations in nearby cells)
            var candidates = spatialGrid.GetNearby(mousePosition.X, mousePosition.Y, threshold);

            int nearestIndex = -1;
            double minDistanceSq = threshold * threshold;

            foreach (int i in candidates)
            {
                if (i >= dataPoints.Count) continue;
                
                Annotation3D annotation = annotations[i];
                if (!annotation.Visible) continue;

                var screenPos = Project3DTo2D(dataPoints[i]);
                
                double dx = screenPos.x - mousePosition.X;
                double dy = screenPos.y - mousePosition.Y;
                double distanceSq = dx * dx + dy * dy; // Avoid sqrt for performance

                if (distanceSq < minDistanceSq)
                {
                    minDistanceSq = distanceSq;
                    nearestIndex = i;
                }
            }

            return nearestIndex >= 0 ? nearestIndex : null;
        }

        /// <summary>
        /// Legacy method - finds the nearest annotation to the mouse position.
        /// Used as fallback when spatial grid is not available.
        /// </summary>
        private int? FindNearestAnnotation(LightningChart chart, Point mousePosition, double threshold)
        {
            if (chart?.View3D == null) return null;

            UpdateProjectionCache(chart);
            return FindNearestAnnotationOptimized(mousePosition, threshold);
        }

        #endregion

        #region Text Updates (Optimized)

        /// <summary>
        /// Shows index, X and Y values as text on the annotation.
        /// Uses StringBuilder to reduce string allocations.
        /// </summary>
        private void ShowAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;
            
            // Don't overwrite selected annotation's special styling
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value == index)
            {
                return;
            }

            if (isBatchUpdate)
            {
                pendingTextUpdates.Add(index);
                return;
            }

            ApplyTextUpdate(index);
        }

        /// <summary>
        /// Optimized text update using StringBuilder to avoid allocations.
        /// </summary>
        private void UpdateAnnotationTextOptimized(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            stringBuilder.Clear();
            stringBuilder.Append('[').Append(index).Append("]\nX: ");
            stringBuilder.Append(dataPoint.X.ToString("F1"));
            stringBuilder.Append("\nY: ");
            stringBuilder.Append(dataPoint.Y.ToString("F1"));
            
            annotation.Text = stringBuilder.ToString();
            annotation.Anchor.Y = 1;
        }

        /// <summary>
        /// Optimized selected annotation text update using StringBuilder.
        /// </summary>
        private void UpdateSelectedAnnotationTextOptimized(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            stringBuilder.Clear();
            stringBuilder.Append(">>> [").Append(index).Append("] <<<\nX: ");
            stringBuilder.Append(dataPoint.X.ToString("F1"));
            stringBuilder.Append("\nY: ");
            stringBuilder.Append(dataPoint.Y.ToString("F1"));
            
            annotation.Text = stringBuilder.ToString();
            annotation.Anchor.Y = 1;
        }

        /// <summary>
        /// Applies a pending text update to an annotation.
        /// </summary>
        private void ApplyTextUpdate(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            stringBuilder.Clear();
            stringBuilder.Append('[').Append(index).Append("]\nX: ");
            stringBuilder.Append(dataPoint.X.ToString("F1"));
            stringBuilder.Append("\nY: ");
            stringBuilder.Append(dataPoint.Y.ToString("F1"));
            
            annotation.Text = stringBuilder.ToString();
            annotation.Anchor.Y = 1;
        }
        
        /// <summary>
        /// Updates text for selected annotation (with special format, preserves styling)
        /// </summary>
        private void UpdateSelectedAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            stringBuilder.Clear();
            stringBuilder.Append(">>> [").Append(index).Append("] <<<\nX: ");
            stringBuilder.Append(dataPoint.X.ToString("F1"));
            stringBuilder.Append("\nY: ");
            stringBuilder.Append(dataPoint.Y.ToString("F1"));

            annotation.Text = stringBuilder.ToString();
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

        #endregion

        #region Add/Delete Operations

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
            
            InvalidateProjectionCache();
        }

        /// <summary>
        /// Adds multiple random annotations in a batch for better performance.
        /// </summary>
        /// <param name="count">Number of annotations to add</param>
        /// <param name="random">Optional random number generator</param>
        public void AddRandomAnnotationsBatch(int count, Random? random = null)
        {
            if (count <= 0) return;
            random ??= new Random();

            var newAnnotations = new Annotation3D[count];
            for (int i = 0; i < count; i++)
            {
                SphereDataPoint dataPoint = SphereDataPoint.GenerateRandom(random);
                dataPoints.Add(dataPoint);

                Annotation3D annotation = CreateAnnotationForDataPoint(dataPoint);
                annotations.Add(annotation);
                newAnnotations[i] = annotation;
            }

            foreach (var annotation in newAnnotations)
            {
                view3D.Annotations.Add(annotation);
            }
            InvalidateProjectionCache();
        }

        /// <summary>
        /// Deletes the last annotation from the chart
        /// </summary>
        public void DeleteLastAnnotation()
        {
            if (annotations.Count == 0) return;

            int lastIndex = annotations.Count - 1;

            // Clear selection if the last annotation is selected
            if (selectedAnnotationIndex == lastIndex)
            {
                selectedAnnotationIndex = null;
            }

            // Clear hover state if the last annotation is hovered
            if (hoveredAnnotationIndex == lastIndex)
            {
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
            
            InvalidateProjectionCache();
        }

        /// <summary>
        /// Deletes all annotations from the chart
        /// </summary>
        public void DeleteAllAnnotations()
        {
            ClearExistingData();
        }

        #endregion

        #region Bulk Text Operations

        /// <summary>
        /// Shows text on all annotations (preserving selected annotation's special styling).
        /// Optimized to reduce string allocations.
        /// </summary>
        private void ShowAllAnnotationTexts()
        {
            int count = Math.Min(annotations.Count, dataPoints.Count);
            int selectionIdx = selectedAnnotationIndex ?? -1;

            for (int i = 0; i < count; i++)
            {
                // Skip selected annotation - it has its own special styling
                if (i == selectionIdx)
                {
                    continue;
                }
                
                UpdateAnnotationTextOptimized(i);
            }
        }

        /// <summary>
        /// Clears text on all annotations (but not for selected annotation)
        /// </summary>
        private void ClearAllAnnotationTexts()
        {
            int selectionIdx = selectedAnnotationIndex ?? -1;
            
            for (int i = 0; i < annotations.Count; i++)
            {
                // Skip selected annotation - it should always be visible
                if (i == selectionIdx)
                {
                    continue;
                }
                
                annotations[i].Text = string.Empty;
            }
            hoveredAnnotationIndex = null;
        }

        #endregion

        #region Selection

        /// <summary>
        /// Selects an annotation by clicking near it.
        /// Uses optimized spatial search for O(1) average-case complexity.
        /// </summary>
        /// <param name="chart">The LightningChart instance</param>
        /// <param name="mousePosition">Mouse position in screen coordinates</param>
        /// <param name="proximityThreshold">Distance threshold in pixels</param>
        /// <returns>True if selection state changed, false otherwise</returns>
        public bool SelectAnnotationAtPosition(LightningChart chart, Point mousePosition, double proximityThreshold = 80.0)
        {
            if (chart == null) return false;

            UpdateProjectionCache(chart);

            int? nearestIndex = annotations.Count > 0 
                ? FindNearestAnnotationOptimized(mousePosition, proximityThreshold) 
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
            
            InvalidateProjectionCache();

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
            
            //// Set highlight color (bright yellow for visibility)
            //annotation.TextStyle.Color = System.Windows.Media.Colors.Yellow;
            //annotation.ArrowLineStyle.Color = System.Windows.Media.Colors.Yellow;
            
            // Add visible border
            annotation.BorderVisible = true;
            annotation.BorderLineStyle.Color = System.Windows.Media.Colors.Yellow;
            annotation.BorderLineStyle.Width = 2;
            
            // Add shadow for more visibility
            annotation.Shadow.Visible = true;
            annotation.Shadow.Color = System.Windows.Media.Colors.Black;
            
            // Always show text for selected annotation
            UpdateSelectedAnnotationTextOptimized(index);
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
                // Restore normal text format using optimized method
                UpdateAnnotationTextOptimized(index);
            }
        }

        #endregion
    }

    #region Spatial Grid Helper Class

    /// <summary>
    /// A simple spatial hash grid for O(1) average-case nearest neighbor queries.
    /// Divides 2D space into cells of fixed size for fast proximity lookups.
    /// </summary>
    internal class SpatialGrid
    {
        private readonly double cellSize;
        private readonly Dictionary<long, List<int>> cells;
        private readonly Dictionary<int, (double x, double y)> positions;

        public SpatialGrid(double cellSize = 50.0)
        {
            this.cellSize = cellSize;
            this.cells = new Dictionary<long, List<int>>();
            this.positions = new Dictionary<int, (double x, double y)>();
        }

        /// <summary>
        /// Gets the cell key for a given position.
        /// </summary>
        private long GetCellKey(double x, double y)
        {
            int cellX = (int)Math.Floor(x / cellSize);
            int cellY = (int)Math.Floor(y / cellSize);
            // Combine into a single long key
            return ((long)cellX << 32) | (uint)cellY;
        }

        /// <summary>
        /// Inserts an item at the given position.
        /// </summary>
        public void Insert(int index, double x, double y)
        {
            long key = GetCellKey(x, y);
            
            if (!cells.TryGetValue(key, out var list))
            {
                list = new List<int>();
                cells[key] = list;
            }
            
            list.Add(index);
            positions[index] = (x, y);
        }

        /// <summary>
        /// Gets all items within the given radius of the specified position.
        /// Returns items from the current cell and all neighboring cells within range.
        /// </summary>
        public IEnumerable<int> GetNearby(double x, double y, double radius)
        {
            int cellRadius = (int)Math.Ceiling(radius / cellSize);
            int centerCellX = (int)Math.Floor(x / cellSize);
            int centerCellY = (int)Math.Floor(y / cellSize);

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    int cellX = centerCellX + dx;
                    int cellY = centerCellY + dy;
                    long key = ((long)cellX << 32) | (uint)cellY;

                    if (cells.TryGetValue(key, out var list))
                    {
                        foreach (int index in list)
                        {
                            yield return index;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clears all items from the grid.
        /// </summary>
        public void Clear()
        {
            cells.Clear();
            positions.Clear();
        }

        /// <summary>
        /// Gets the number of items in the grid.
        /// </summary>
        public int Count => positions.Count;
    }

    #endregion
}
