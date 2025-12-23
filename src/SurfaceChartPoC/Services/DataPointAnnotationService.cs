using SurfaceChartPoC.Models;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Views.View3D;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using Common.Providers;
using Common.Annotations;
using Common.Domain;

namespace SurfaceChartPoC.Services
{
    /// <summary>
    /// High-performance annotation service optimized for handling thousands of annotations.
    /// Key optimizations:
    /// - Real-time 3D to 2D projection for accurate mouse tracking
    /// - Batch operations to minimize chart redraws
    /// - StringBuilder reuse to reduce GC pressure
    /// - Cached screen positions updated per-frame for smooth hover
    /// </summary>
    public class DataPointAnnotationService
    {
        #region Fields

        private readonly View3D view3D;
        private readonly List<SphereDataPoint> dataPoints;
        private readonly List<Annotation3D> annotations;
        private readonly IDataSetProvider dataSetProvider;
        private readonly IAnnotationFactory annotationFactory;
        
        // Performance optimization: StringBuilder pool to reduce string allocations
        private readonly StringBuilder stringBuilder = new StringBuilder(64);
        
        // Performance optimization: Cached screen positions (updated each animation frame)
        private readonly List<(double x, double y)> cachedScreenPositions = new List<(double x, double y)>();
        
        // Camera state for projection
        private double lastRotX;
        private double lastRotY;
        private double lastRotZ;
        private double lastChartWidth;
        private double lastChartHeight;
        private double lastViewDistance;
        private LightningChart? lastChart;

        // State tracking
        private int? hoveredAnnotationIndex = null;
        private bool isMouseTrackingEnabled = true;
        private int? selectedAnnotationIndex = null;

        // Performance optimization: Batch update tracking
        private bool isBatchUpdate = false;
        private readonly HashSet<int> pendingTextUpdates = new HashSet<int>();
        
        // Last mouse position for re-evaluation after animation
        private Point lastMousePosition;
        private bool hasLastMousePosition = false;
        
        // Reduced threshold for more precise hit detection
        private const double DefaultProximityThreshold = 35.0;

        #endregion

        #region Constructor

        public DataPointAnnotationService(View3D view3D)
            : this(view3D, new SphereDataSetProvider(), new SphereAnnotationFactory())
        {
        }

        public DataPointAnnotationService(View3D view3D, IDataSetProvider dataSetProvider, IAnnotationFactory annotationFactory)
        {
            this.view3D = view3D ?? throw new ArgumentNullException(nameof(view3D));
            this.dataSetProvider = dataSetProvider ?? throw new ArgumentNullException(nameof(dataSetProvider));
            this.annotationFactory = annotationFactory ?? throw new ArgumentNullException(nameof(annotationFactory));
            this.dataPoints = new List<SphereDataPoint>();
            this.annotations = new List<Annotation3D>();
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

            ClearExistingData();

            // Use shared provider to generate dataset
            int? seed = random != null ? random.Next() : null;
            var dataSet = dataSetProvider.GenerateDataSet(n, seed);

            // Pre-allocate capacity to avoid resizing
            dataPoints.Capacity = Math.Max(dataPoints.Capacity, n);
            annotations.Capacity = Math.Max(annotations.Capacity, n);

            // Add data points to internal list
            foreach (var point in dataSet.DataPoints)
            {
                dataPoints.Add(point);
            }

            // Create annotation specs using shared factory
            var annotationSpecs = annotationFactory.CreateAnnotations(
                dataSet, 
                selectedAnnotationIndex, 
                hoveredAnnotationIndex, 
                !isMouseTrackingEnabled);

            // Create all annotations first (without adding to chart)
            var newAnnotations = new Annotation3D[annotationSpecs.Count];
            for (int i = 0; i < annotationSpecs.Count; i++)
            {
                var spec = annotationSpecs[i] as ArrowAnnotationSpec;
                if (spec != null)
                {
                    Annotation3D annotation = CreateAnnotationFromSpec(spec);
                    annotations.Add(annotation);
                    newAnnotations[i] = annotation;
                }
            }

            // Add all annotations to chart (LightningChart doesn't have AddRange, but 
            // adding them all after BeginUpdate minimizes redraws)
            foreach (var annotation in newAnnotations)
            {
                if (annotation != null)
                {
                    view3D.Annotations.Add(annotation);
                    cachedScreenPositions.Add((0, 0)); // Placeholder, will be updated on next frame
                }
            }
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
            cachedScreenPositions.Clear();
            hoveredAnnotationIndex = null;
            selectedAnnotationIndex = null;
            hasLastMousePosition = false;
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

        #endregion

        #region Animation Updates

        /// <summary>
        /// Updates all data points moving clockwise around the sphere, each with its own pace.
        /// Also updates cached screen positions and re-evaluates hover state for smooth tracking.
        /// </summary>
        /// <param name="deltaTimeSeconds">Time elapsed since last update in seconds</param>
        /// <param name="chart">The chart instance for screen projection (optional but recommended)</param>
        public void UpdateDataPointsClockwise(double deltaTimeSeconds, LightningChart? chart = null)
        {
            if (deltaTimeSeconds <= 0 || dataPoints.Count == 0) return;

            int count = dataPoints.Count;
            bool hasSelection = selectedAnnotationIndex.HasValue;
            int selectionIdx = selectedAnnotationIndex.GetValueOrDefault(-1);
            bool showAllTexts = !isMouseTrackingEnabled;

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

            // Update cached screen positions if we have a chart reference
            if (chart != null)
            {
                lastChart = chart;
                UpdateAllScreenPositions(chart);
                
                // Re-evaluate hover based on last known mouse position
                // This ensures hover tracks the moving annotation smoothly
                if (hasLastMousePosition && isMouseTrackingEnabled)
                {
                    ReEvaluateHoverState();
                }
            }

            // Update texts only for annotations that need it (selected, hovered, or all if tracking disabled)
            if (hasSelection && selectionIdx < count)
            {
                UpdateSelectedAnnotationTextOptimized(selectionIdx);
            }

            int hoveredIdx = hoveredAnnotationIndex ?? -1;
            if (showAllTexts)
            {
                // Only update text for visible annotations
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
        }

        /// <summary>
        /// Updates all cached screen positions using improved 3D to 2D projection.
        /// Uses full rotation matrix including roll (Z rotation) and accounts for view distance.
        /// </summary>
        private void UpdateAllScreenPositions(LightningChart chart)
        {
            if (chart?.View3D == null) return;

            double chartWidth = chart.ActualWidth;
            double chartHeight = chart.ActualHeight;
            
            // Get all rotation angles in radians
            double rotX = chart.View3D.Camera.RotationX * Math.PI / 180.0;  // Pitch
            double rotY = chart.View3D.Camera.RotationY * Math.PI / 180.0;  // Yaw
            double rotZ = chart.View3D.Camera.RotationZ * Math.PI / 180.0;  // Roll
            double viewDistance = chart.View3D.Camera.ViewDistance;

            // Cache trig values for all three rotations
            double cosX = Math.Cos(rotX);
            double sinX = Math.Sin(rotX);
            double cosY = Math.Cos(rotY);
            double sinY = Math.Sin(rotY);
            double cosZ = Math.Cos(rotZ);
            double sinZ = Math.Sin(rotZ);

            double centerX = chartWidth / 2.0;
            double centerY = chartHeight / 2.0;
            
            // Improved scale calculation considering view distance and chart dimensions
            double baseScale = Math.Min(chartWidth, chartHeight) / 200.0;
            double distanceScale = 100.0 / Math.Max(viewDistance, 10.0);
            double scale = baseScale * distanceScale;

            // Store for change detection
            lastRotX = rotX;
            lastRotY = rotY;
            lastRotZ = rotZ;
            lastChartWidth = chartWidth;
            lastChartHeight = chartHeight;
            lastViewDistance = viewDistance;

            // Ensure list has correct capacity
            while (cachedScreenPositions.Count < dataPoints.Count)
            {
                cachedScreenPositions.Add((0, 0));
            }
            while (cachedScreenPositions.Count > dataPoints.Count)
            {
                cachedScreenPositions.RemoveAt(cachedScreenPositions.Count - 1);
            }

            // Get dimensions for proper scaling
            double dimWidth = chart.View3D.Dimensions.Width;
            double dimHeight = chart.View3D.Dimensions.Height;
            double dimDepth = chart.View3D.Dimensions.Depth;
            double maxDim = Math.Max(Math.Max(dimWidth, dimHeight), dimDepth);
            double normScale = maxDim > 0 ? 100.0 / maxDim : 1.0;

            // Update all screen positions
            for (int i = 0; i < dataPoints.Count; i++)
            {
                var dp = dataPoints[i];
                double x = dp.X * normScale;
                double y = dp.Y * normScale;
                double z = dp.Z * normScale;

                // Apply full rotation matrix: Rz * Rx * Ry (matching LightningChart's convention)
                // First rotate around Y (yaw)
                double x1 = x * cosY + z * sinY;
                double z1 = -x * sinY + z * cosY;

                // Then rotate around X (pitch)
                double y2 = y * cosX - z1 * sinX;
                double z2 = y * sinX + z1 * cosX;

                // Finally rotate around Z (roll)
                double x3 = x1 * cosZ - y2 * sinZ;
                double y3 = x1 * sinZ + y2 * cosZ;

                // Project to screen coordinates
                double screenX = centerX + x3 * scale;
                double screenY = centerY - y3 * scale;

                cachedScreenPositions[i] = (screenX, screenY);
            }
        }

        /// <summary>
        /// Re-evaluates hover state based on cached screen positions and last mouse position.
        /// Called after animation updates to keep hover tracking smooth.
        /// </summary>
        private void ReEvaluateHoverState()
        {
            if (!hasLastMousePosition || cachedScreenPositions.Count == 0) return;

            int? newHoveredIndex = FindNearestAnnotationFromCache(lastMousePosition, DefaultProximityThreshold);

            // Only update if hover state changed
            if (newHoveredIndex != hoveredAnnotationIndex)
            {
                // Clear previous hovered annotation text
                if (hoveredAnnotationIndex.HasValue && hoveredAnnotationIndex.Value < annotations.Count)
                {
                    ClearAnnotationText(hoveredAnnotationIndex.Value);
                }

                // Set new hovered annotation text
                if (newHoveredIndex.HasValue)
                {
                    ShowAnnotationText(newHoveredIndex.Value);
                }

                hoveredAnnotationIndex = newHoveredIndex;
            }
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

        private Annotation3D CreateAnnotationFromSpec(ArrowAnnotationSpec spec)
        {
            Annotation3D annotation = new Annotation3D(
                view3D,
                Axis3DBinding.Primary,
                Axis3DBinding.Primary,
                Axis3DBinding.Primary)
            {
                TargetCoordinateSystem = AnnotationTargetCoordinates.AxisValues
            };

            // Configure from spec
            annotation.LocationCoordinateSystem = CoordinateSystem.AxisValues;
            annotation.LocationAxisValues.SetValues(spec.StartX, spec.StartY, spec.StartZ);
            annotation.TargetAxisValues.SetValues(spec.EndX, spec.EndY, spec.EndZ);
            annotation.Style = AnnotationStyle.Arrow;
            annotation.ArrowStyleBegin = ArrowStyle.Arrow;
            annotation.ArrowStyleEnd = ArrowStyle.Circle;
            annotation.AllowUserInteraction = false;
            annotation.ArrowLineStyle.Color = spec.Color;
            annotation.TextStyle.Color = spec.Color;
            annotation.Visible = true;
            annotation.Anchor.Y = 1;
            annotation.Fill.Style = RectFillStyle.None;
            annotation.BorderVisible = false;
            annotation.Shadow.Visible = false;

            // Apply label if present
            if (!string.IsNullOrEmpty(spec.Label))
            {
                annotation.Text = spec.Label;
            }

            // Apply selection/hover visual feedback
            if (spec.IsSelected)
            {
                annotation.ArrowLineStyle.Width = 4;
                annotation.TextStyle.Font = new WpfFont("Segoe UI", 14, true, false);
                annotation.BorderVisible = true;
                annotation.BorderLineStyle.Color = System.Windows.Media.Colors.Yellow;
                annotation.BorderLineStyle.Width = 2;
                annotation.Shadow.Visible = true;
                annotation.Shadow.Color = System.Windows.Media.Colors.Black;
            }

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
        /// Uses cached screen positions for instant response, no recalculation needed.
        /// </summary>
        /// <param name="chart">The LightningChart instance</param>
        /// <param name="mousePosition">Mouse position in screen coordinates</param>
        /// <param name="proximityThreshold">Distance threshold in pixels for considering mouse "near" an annotation</param>
        /// <summary>
        /// Handles mouse move events for hover detection.
        /// Always updates screen positions for accurate hit detection.
        /// </summary>
        public void HandleMouseMove(LightningChart chart, Point mousePosition, double proximityThreshold = -1)
        {
            if (chart == null || annotations.Count == 0) return;

            // Use default threshold if not specified
            if (proximityThreshold < 0)
            {
                proximityThreshold = DefaultProximityThreshold;
            }

            // Store last mouse position for re-evaluation after animation
            lastMousePosition = mousePosition;
            hasLastMousePosition = true;
            lastChart = chart;

            // If mouse tracking is disabled, don't process mouse movement
            if (!isMouseTrackingEnabled) return;

            // Always update screen positions on mouse move for accurate detection
            // This ensures positions are current even when animation is paused or slow
            UpdateAllScreenPositions(chart);

            int? nearestIndex = FindNearestAnnotationFromCache(mousePosition, proximityThreshold);

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
        /// Finds the nearest visible annotation to the mouse position within threshold.
        /// Uses squared distance for performance (avoids sqrt).
        /// </summary>
        private int? FindNearestAnnotationFromCache(Point mousePosition, double threshold)
        {
            int count = Math.Min(cachedScreenPositions.Count, annotations.Count);
            if (count == 0) return null;

            int nearestIndex = -1;
            double minDistanceSq = threshold * threshold;
            double mouseX = mousePosition.X;
            double mouseY = mousePosition.Y;

            for (int i = 0; i < count; i++)
            {
                Annotation3D annotation = annotations[i];
                if (!annotation.Visible) continue;

                var (screenX, screenY) = cachedScreenPositions[i];

                double dx = screenX - mouseX;
                double dy = screenY - mouseY;
                double distanceSq = dx * dx + dy * dy;

                if (distanceSq < minDistanceSq)
                {
                    minDistanceSq = distanceSq;
                    nearestIndex = i;
                }
            }

            return nearestIndex >= 0 ? nearestIndex : null;
        }

        /// <summary>
        /// Projects a single 3D data point to 2D screen coordinates on-demand.
        /// Uses the improved projection with full rotation matrix.
        /// </summary>
        private (double x, double y) Project3DTo2DOnDemand(LightningChart chart, SphereDataPoint dataPoint)
        {
            if (chart?.View3D == null) return (0, 0);

            double chartWidth = chart.ActualWidth;
            double chartHeight = chart.ActualHeight;
            double rotX = chart.View3D.Camera.RotationX * Math.PI / 180.0;
            double rotY = chart.View3D.Camera.RotationY * Math.PI / 180.0;
            double rotZ = chart.View3D.Camera.RotationZ * Math.PI / 180.0;
            double viewDistance = chart.View3D.Camera.ViewDistance;

            double cosX = Math.Cos(rotX);
            double sinX = Math.Sin(rotX);
            double cosY = Math.Cos(rotY);
            double sinY = Math.Sin(rotY);
            double cosZ = Math.Cos(rotZ);
            double sinZ = Math.Sin(rotZ);

            double centerX = chartWidth / 2.0;
            double centerY = chartHeight / 2.0;
            double baseScale = Math.Min(chartWidth, chartHeight) / 200.0;
            double distanceScale = 100.0 / Math.Max(viewDistance, 10.0);
            double scale = baseScale * distanceScale;

            // Get dimensions for proper scaling
            double dimWidth = chart.View3D.Dimensions.Width;
            double dimHeight = chart.View3D.Dimensions.Height;
            double dimDepth = chart.View3D.Dimensions.Depth;
            double maxDim = Math.Max(Math.Max(dimWidth, dimHeight), dimDepth);
            double normScale = maxDim > 0 ? 100.0 / maxDim : 1.0;

            double x = dataPoint.X * normScale;
            double y = dataPoint.Y * normScale;
            double z = dataPoint.Z * normScale;

            // Apply full rotation matrix: Rz * Rx * Ry
            // First rotate around Y (yaw)
            double x1 = x * cosY + z * sinY;
            double z1 = -x * sinY + z * cosY;

            // Then rotate around X (pitch)
            double y2 = y * cosX - z1 * sinX;
            double z2 = y * sinX + z1 * cosX;

            // Finally rotate around Z (roll)
            double x3 = x1 * cosZ - y2 * sinZ;
            double y3 = x1 * sinZ + y2 * cosZ;

            // Project to screen coordinates
            double screenX = centerX + x3 * scale;
            double screenY = centerY - y3 * scale;

            return (screenX, screenY);
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
            
            // Add a placeholder for the new screen position
            cachedScreenPositions.Add((0, 0));
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
                cachedScreenPositions.Add((0, 0));
            }
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
            
            if (lastIndex < cachedScreenPositions.Count)
            {
                cachedScreenPositions.RemoveAt(lastIndex);
            }
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
        /// Uses cached screen positions for instant response.
        /// </summary>
        /// <param name="chart">The LightningChart instance</param>
        /// <param name="mousePosition">Mouse position in screen coordinates</param>
        /// <param name="proximityThreshold">Distance threshold in pixels</param>
        /// <returns>True if selection state changed, false otherwise</returns>
        /// <summary>
        /// Selects the annotation nearest to the given mouse position.
        /// </summary>
        public bool SelectAnnotationAtPosition(LightningChart chart, Point mousePosition, double proximityThreshold = -1)
        {
            if (chart == null) return false;

            // Use default threshold if not specified
            if (proximityThreshold < 0)
            {
                proximityThreshold = DefaultProximityThreshold;
            }

            // Always update positions for accurate selection
            UpdateAllScreenPositions(chart);

            int? nearestIndex = annotations.Count > 0 
                ? FindNearestAnnotationFromCache(mousePosition, proximityThreshold) 
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
            
            // Remove cached screen position
            if (indexToDelete < cachedScreenPositions.Count)
            {
                cachedScreenPositions.RemoveAt(indexToDelete);
            }

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
}
