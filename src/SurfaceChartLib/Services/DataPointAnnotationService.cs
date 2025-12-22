using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using Common.Annotations;
using Common.Domain;
using Common.Providers;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Views.View3D;

namespace SurfaceChartLib.Services
{
    /// <summary>
    /// High-performance annotation service optimized for handling thousands of annotations
    /// on the surface chart. Bridges Common-domain annotation specs and LightningChart 3D annotations.
    /// </summary>
    internal class DataPointAnnotationService
    {
        #region Fields

        private readonly View3D view3D;
        private readonly List<SphereDataPoint> dataPoints;
        private readonly List<Annotation3D> annotations;
        private readonly IDataSetProvider dataSetProvider;
        private readonly IAnnotationFactory annotationFactory;

        private readonly StringBuilder stringBuilder = new StringBuilder(64);
        private readonly List<(double x, double y)> cachedScreenPositions = new List<(double x, double y)>();

        private double lastRotX;
        private double lastRotY;
        private double lastChartWidth;
        private double lastChartHeight;
        private LightningChart? lastChart;

        private int? hoveredAnnotationIndex = null;
        private bool isMouseTrackingEnabled = true;
        private int? selectedAnnotationIndex = null;

        private bool isBatchUpdate = false;
        private readonly HashSet<int> pendingTextUpdates = new HashSet<int>();

        private Point lastMousePosition;
        private bool hasLastMousePosition = false;

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
            dataPoints = new List<SphereDataPoint>();
            annotations = new List<Annotation3D>();
        }

        #endregion

        #region Properties

        public IReadOnlyList<SphereDataPoint> DataPoints => dataPoints.AsReadOnly();

        public IReadOnlyList<Annotation3D> Annotations => annotations.AsReadOnly();

        public int AnnotationCount => annotations.Count;

        public int? SelectedAnnotationIndex => selectedAnnotationIndex;

        #endregion

        #region Batch Operations

        public void BeginBatchUpdate()
        {
            isBatchUpdate = true;
            pendingTextUpdates.Clear();
        }

        public void EndBatchUpdate()
        {
            if (!isBatchUpdate) return;

            isBatchUpdate = false;

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

        public void GenerateDataPoints(int n, Random? random = null)
        {
            if (n < 0)
                throw new ArgumentException("Number of data points must be non-negative.", nameof(n));

            ClearExistingData();

            int? seed = random != null ? random.Next() : null;
            var dataSet = dataSetProvider.GenerateDataSet(n, seed);

            dataPoints.Capacity = Math.Max(dataPoints.Capacity, n);
            annotations.Capacity = Math.Max(annotations.Capacity, n);

            foreach (var point in dataSet.DataPoints)
            {
                dataPoints.Add(point);
            }

            var annotationSpecs = annotationFactory.CreateAnnotations(
                dataSet,
                selectedAnnotationIndex,
                hoveredAnnotationIndex,
                !isMouseTrackingEnabled);

            var newAnnotations = new Annotation3D[annotationSpecs.Count];
            for (int i = 0; i < annotationSpecs.Count; i++)
            {
                if (annotationSpecs[i] is ArrowAnnotationSpec spec)
                {
                    Annotation3D annotation = CreateAnnotationFromSpec(spec);
                    annotations.Add(annotation);
                    newAnnotations[i] = annotation;
                }
            }

            foreach (var annotation in newAnnotations)
            {
                if (annotation != null)
                {
                    view3D.Annotations.Add(annotation);
                    cachedScreenPositions.Add((0, 0));
                }
            }
        }

        public void ClearExistingData()
        {
            if (annotations.Count > 0)
            {
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

        #endregion

        #region Animation Updates

        public void UpdateDataPointsClockwise(double deltaTimeSeconds, LightningChart? chart = null)
        {
            if (deltaTimeSeconds <= 0 || dataPoints.Count == 0) return;

            int count = dataPoints.Count;
            bool hasSelection = selectedAnnotationIndex.HasValue;
            int selectionIdx = selectedAnnotationIndex.GetValueOrDefault(-1);
            bool showAllTexts = !isMouseTrackingEnabled;

            for (int i = 0; i < count; i++)
            {
                SphereDataPoint dataPoint = dataPoints[i];
                double angularDistance = dataPoint.Pace * deltaTimeSeconds;

                dataPoint.MoveClockwise(angularDistance);

                Annotation3D annotation = annotations[i];
                annotation.LocationAxisValues.SetValues(dataPoint.X, dataPoint.Y, dataPoint.Z);
            }

            if (chart != null)
            {
                lastChart = chart;
                UpdateAllScreenPositions(chart);

                if (hasLastMousePosition && isMouseTrackingEnabled)
                {
                    ReEvaluateHoverState();
                }
            }

            if (hasSelection && selectionIdx < count)
            {
                UpdateSelectedAnnotationTextOptimized(selectionIdx);
            }

            int hoveredIdx = hoveredAnnotationIndex ?? -1;
            if (showAllTexts)
            {
                for (int i = 0; i < count; i++)
                {
                    if (i != selectionIdx)
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

        private void UpdateAllScreenPositions(LightningChart chart)
        {
            if (chart?.View3D == null) return;

            double chartWidth = chart.ActualWidth;
            double chartHeight = chart.ActualHeight;
            double rotX = chart.View3D.Camera.RotationX * Math.PI / 180.0;
            double rotY = chart.View3D.Camera.RotationY * Math.PI / 180.0;

            double cosY = Math.Cos(rotY);
            double sinY = Math.Sin(rotY);
            double cosX = Math.Cos(rotX);
            double sinX = Math.Sin(rotX);
            double centerX = chartWidth / 2.0;
            double centerY = chartHeight / 2.0;
            double scale = Math.Min(chartWidth, chartHeight) / 300.0;

            lastRotX = rotX;
            lastRotY = rotY;
            lastChartWidth = chartWidth;
            lastChartHeight = chartHeight;

            while (cachedScreenPositions.Count < dataPoints.Count)
            {
                cachedScreenPositions.Add((0, 0));
            }
            while (cachedScreenPositions.Count > dataPoints.Count)
            {
                cachedScreenPositions.RemoveAt(cachedScreenPositions.Count - 1);
            }

            for (int i = 0; i < dataPoints.Count; i++)
            {
                var dp = dataPoints[i];
                double x = dp.X;
                double y = dp.Y;
                double z = dp.Z;

                double x1 = x * cosY + z * sinY;
                double z1 = -x * sinY + z * cosY;

                double y1 = y * cosX - z1 * sinX;

                double screenX = centerX + x1 * scale;
                double screenY = centerY - y1 * scale;

                cachedScreenPositions[i] = (screenX, screenY);
            }
        }

        private void ReEvaluateHoverState(double proximityThreshold = 80.0)
        {
            if (!hasLastMousePosition || cachedScreenPositions.Count == 0) return;

            int? newHoveredIndex = FindNearestAnnotationFromCache(lastMousePosition, proximityThreshold);

            if (newHoveredIndex != hoveredAnnotationIndex)
            {
                if (hoveredAnnotationIndex.HasValue && hoveredAnnotationIndex.Value < annotations.Count)
                {
                    ClearAnnotationText(hoveredAnnotationIndex.Value);
                }

                if (newHoveredIndex.HasValue)
                {
                    ShowAnnotationText(newHoveredIndex.Value);
                }

                hoveredAnnotationIndex = newHoveredIndex;
            }
        }

        #endregion

        #region Annotation Configuration

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

            if (!string.IsNullOrEmpty(spec.Label))
            {
                annotation.Text = spec.Label;
            }

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
        /// Creates a 3D annotation for a single data point using its coordinates and color.
        /// </summary>
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

            return annotation;
        }

        #endregion

        #region Mouse Tracking

        public void SetMouseTrackingEnabled(bool enabled)
        {
            isMouseTrackingEnabled = enabled;

            if (enabled)
            {
                ClearAllAnnotationTexts();
            }
            else
            {
                ShowAllAnnotationTexts();
            }
        }

        public void HandleMouseMove(LightningChart chart, Point mousePosition, double proximityThreshold = 80.0)
        {
            if (chart == null || annotations.Count == 0) return;

            lastMousePosition = mousePosition;
            hasLastMousePosition = true;
            lastChart = chart;

            if (!isMouseTrackingEnabled) return;

            if (cachedScreenPositions.Count != dataPoints.Count)
            {
                UpdateAllScreenPositions(chart);
            }

            int? nearestIndex = FindNearestAnnotationFromCache(mousePosition, proximityThreshold);

            if (nearestIndex != hoveredAnnotationIndex)
            {
                if (hoveredAnnotationIndex.HasValue && hoveredAnnotationIndex.Value < annotations.Count)
                {
                    ClearAnnotationText(hoveredAnnotationIndex.Value);
                }

                if (nearestIndex.HasValue)
                {
                    ShowAnnotationText(nearestIndex.Value);
                }

                hoveredAnnotationIndex = nearestIndex;
            }
        }

        private int? FindNearestAnnotationFromCache(Point mousePosition, double threshold)
        {
            if (cachedScreenPositions.Count == 0) return null;

            int nearestIndex = -1;
            double minDistanceSq = threshold * threshold;
            double mouseX = mousePosition.X;
            double mouseY = mousePosition.Y;

            for (int i = 0; i < cachedScreenPositions.Count; i++)
            {
                if (i >= annotations.Count) break;

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

            return nearestIndex >= 0 ? nearestIndex : (int?)null;
        }

        public void ClearHoverState()
        {
            if (hoveredAnnotationIndex.HasValue && hoveredAnnotationIndex.Value < annotations.Count)
            {
                ClearAnnotationText(hoveredAnnotationIndex.Value);
                hoveredAnnotationIndex = null;
            }
        }

        #endregion

        #region Text Updates (Optimized)

        private void ShowAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

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

        private void ClearAnnotationText(int index)
        {
            if (index < 0 || index >= annotations.Count) return;

            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value == index)
            {
                return;
            }

            Annotation3D annotation = annotations[index];
            annotation.Text = string.Empty;
        }

        private void ShowAllAnnotationTexts()
        {
            int count = Math.Min(annotations.Count, dataPoints.Count);
            int selectionIdx = selectedAnnotationIndex ?? -1;

            for (int i = 0; i < count; i++)
            {
                if (i == selectionIdx)
                {
                    continue;
                }

                UpdateAnnotationTextOptimized(i);
            }
        }

        private void ClearAllAnnotationTexts()
        {
            int selectionIdx = selectedAnnotationIndex ?? -1;

            for (int i = 0; i < annotations.Count; i++)
            {
                if (i == selectionIdx)
                {
                    continue;
                }

                annotations[i].Text = string.Empty;
            }
            hoveredAnnotationIndex = null;
        }

        #endregion

        #region Add/Delete Operations

        /// <summary>
        /// Adds a random annotation to the chart.
        /// </summary>
        public void AddRandomAnnotation(Random? random = null)
        {
            random ??= new Random();

            SphereDataPoint dataPoint = SphereDataPoint.GenerateRandom(random);
            dataPoints.Add(dataPoint);

            Annotation3D annotation = CreateAnnotationForDataPoint(dataPoint);
            annotations.Add(annotation);
            view3D.Annotations.Add(annotation);

            cachedScreenPositions.Add((0, 0));
        }

        /// <summary>
        /// Deletes the last annotation from the chart.
        /// </summary>
        public void DeleteLastAnnotation()
        {
            if (annotations.Count == 0) return;

            int lastIndex = annotations.Count - 1;

            if (selectedAnnotationIndex == lastIndex)
            {
                selectedAnnotationIndex = null;
            }

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

        #endregion

        #region Selection

        public bool SelectAnnotationAtPosition(LightningChart chart, Point mousePosition, double proximityThreshold = 80.0)
        {
            if (chart == null) return false;

            if (cachedScreenPositions.Count != dataPoints.Count)
            {
                UpdateAllScreenPositions(chart);
            }

            int? nearestIndex = annotations.Count > 0
                ? FindNearestAnnotationFromCache(mousePosition, proximityThreshold)
                : null;

            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value < annotations.Count)
            {
                ClearSelectionVisual(selectedAnnotationIndex.Value);
            }

            int? previousSelection = selectedAnnotationIndex;
            selectedAnnotationIndex = nearestIndex;

            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value < annotations.Count)
            {
                ApplySelectionVisual(selectedAnnotationIndex.Value);
            }

            return previousSelection != selectedAnnotationIndex;
        }

        public bool SelectAnnotationByIndex(int index)
        {
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value < annotations.Count)
            {
                ClearSelectionVisual(selectedAnnotationIndex.Value);
            }

            if (index < 0 || index >= annotations.Count)
            {
                selectedAnnotationIndex = null;
                return false;
            }

            selectedAnnotationIndex = index;
            ApplySelectionVisual(index);
            return true;
        }

        public void ClearSelection()
        {
            if (selectedAnnotationIndex.HasValue && selectedAnnotationIndex.Value < annotations.Count)
            {
                ClearSelectionVisual(selectedAnnotationIndex.Value);
            }
            selectedAnnotationIndex = null;
        }

        public bool DeleteSelectedAnnotation()
        {
            if (!selectedAnnotationIndex.HasValue || selectedAnnotationIndex.Value >= annotations.Count)
                return false;

            int indexToDelete = selectedAnnotationIndex.Value;

            if (hoveredAnnotationIndex == indexToDelete)
            {
                hoveredAnnotationIndex = null;
            }
            else if (hoveredAnnotationIndex > indexToDelete)
            {
                hoveredAnnotationIndex--;
            }

            Annotation3D annotation = annotations[indexToDelete];
            view3D.Annotations.Remove(annotation);
            annotations.RemoveAt(indexToDelete);
            dataPoints.RemoveAt(indexToDelete);

            selectedAnnotationIndex = null;

            if (indexToDelete < cachedScreenPositions.Count)
            {
                cachedScreenPositions.RemoveAt(indexToDelete);
            }

            return true;
        }

        public (int index, double x, double y, double z)? GetSelectedAnnotationInfo()
        {
            if (!selectedAnnotationIndex.HasValue || selectedAnnotationIndex.Value >= dataPoints.Count)
                return null;

            var dataPoint = dataPoints[selectedAnnotationIndex.Value];
            return (selectedAnnotationIndex.Value, dataPoint.X, dataPoint.Y, dataPoint.Z);
        }

        private void ApplySelectionVisual(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            annotation.ArrowLineStyle.Width = 4;
            annotation.TextStyle.Font = new WpfFont("Segoe UI", 14, true, false);

            annotation.BorderVisible = true;
            annotation.BorderLineStyle.Color = System.Windows.Media.Colors.Yellow;
            annotation.BorderLineStyle.Width = 2;

            annotation.Shadow.Visible = true;
            annotation.Shadow.Color = System.Windows.Media.Colors.Black;

            UpdateSelectedAnnotationTextOptimized(index);
        }

        private void ClearSelectionVisual(int index)
        {
            if (index < 0 || index >= annotations.Count || index >= dataPoints.Count) return;

            Annotation3D annotation = annotations[index];
            SphereDataPoint dataPoint = dataPoints[index];

            annotation.ArrowLineStyle.Width = 1;
            annotation.TextStyle.Font = new WpfFont("Segoe UI", 10, false, false);

            annotation.TextStyle.Color = dataPoint.Color;
            annotation.ArrowLineStyle.Color = dataPoint.Color;

            annotation.BorderVisible = false;
            annotation.Shadow.Visible = false;

            if (isMouseTrackingEnabled && hoveredAnnotationIndex != index)
            {
                annotation.Text = string.Empty;
            }
            else
            {
                UpdateAnnotationTextOptimized(index);
            }
        }

        #endregion
    }
}

