using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Common.Annotations;
using Common.Commands;
using Common.Providers;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Series3D;
using LightningChartLib.WPF.Charting.Views.View3D;
using LightningChartLib.WPF.Charting.Views.ViewPie3D;
using SurfaceChartLib.Services;

namespace SurfaceChartLib.ViewModels
{
    /// <summary>
    /// ViewModel for the reusable surface chart.
    /// Extracted from the SurfaceChartPoC MainViewModel to live in the library.
    /// </summary>
    public class SurfaceChartViewModel : BaseViewModel, IDisposable
    {
        #region Fields - Chart Components

        private LightningChart? chart;
        private SurfaceMeshSeries3D? surfaceSeries;
        private SurfaceMeshSeries3D? sphereGrid;
        private SurfaceMeshSeries3D? headingGrid;
        private Annotation3D? mouseTrackAnnotation;
        private View3DBase? view;

        #endregion

        #region Fields - UI State

        private bool isSphericalGridVisible = true;
        private bool isHeadingGridVisible = true;
        private int selectedCameraIndex = 1;
        private int selectedProjectionIndex = 2;
        private double verticalRotation;
        private double horizontalRotation;
        private double sideRotation;
        private double distance = 100;
        private string verticalRotationText = "0";
        private string horizontalRotationText = "0";
        private string sideRotationText = "0";
        private string distanceText = "100";
        private string widthText = "100";
        private string heightText = "100";
        private string depthText = "100";
        private bool isDistanceEnabled = true;
        private bool bOwnChange = false;
        private PointDoubleXYZ? oldDimensions;
        private PointDoubleXYZ? oldDimensionsLegacy;
        private string annotationCountText = "50";
        private bool isMouseTrackingEnabled = true;
        private string selectedAnnotationText = "None";
        private bool hasSelectedAnnotation = false;
        private string selectIndexText = "";
        private bool isAltitudeAnnotationsVisible = false;

        #endregion

        #region Fields - Services

        private readonly SphericalDataService dataService;
        private readonly ChartSetupService chartSetupService;
        private readonly CameraService cameraService;
        private readonly MouseTrackingService mouseTrackingService;
        private DataPointAnnotationService? dataPointAnnotationService;
        private readonly IDataSetProvider? externalDataProvider;
        private readonly IAnnotationFactory? externalAnnotationFactory;

        #endregion

        #region Fields - Animation Timer

        private DispatcherTimer? dataPointUpdateTimer;
        private DateTime lastUpdateTime;

        #endregion

        #region Constructor

        public SurfaceChartViewModel() : this(null, null)
        {
        }

        public SurfaceChartViewModel(IDataSetProvider? dataProvider, IAnnotationFactory? annotationFactory)
        {
            externalDataProvider = dataProvider;
            externalAnnotationFactory = annotationFactory;
            dataService = new SphericalDataService();
            chartSetupService = new ChartSetupService(dataService);
            cameraService = new CameraService();
            mouseTrackingService = new MouseTrackingService();

            TopView2DCommand = new RelayCommand(_ => ExecuteTopView2D());
            AddAnnotationCommand = new RelayCommand(_ => ExecuteAddAnnotation(), _ => CanExecuteAnnotationCommand());
            DeleteAnnotationCommand = new RelayCommand(_ => ExecuteDeleteAnnotation(), _ => CanExecuteAnnotationCommand());
            ToggleMouseTrackingCommand = new RelayCommand(_ => ExecuteToggleMouseTracking(), _ => CanExecuteAnnotationCommand());
            DeleteSelectedAnnotationCommand = new RelayCommand(_ => ExecuteDeleteSelectedAnnotation(), _ => HasSelectedAnnotation);
            ClearSelectionCommand = new RelayCommand(_ => ExecuteClearSelection(), _ => HasSelectedAnnotation);
            ToggleAltitudeAnnotationsCommand = new RelayCommand(_ => ExecuteToggleAltitudeAnnotations(), _ => CanExecuteAnnotationCommand());
        }

        #endregion

        #region Properties - Chart

        public LightningChart? Chart
        {
            get => chart;
            set
            {
                if (SetProperty(ref chart, value))
                {
                    InitializeChart();
                }
            }
        }

        internal DataPointAnnotationService? DataPointAnnotationService => dataPointAnnotationService;

        /// <summary>
        /// Gets the index of the currently selected annotation, or null if none is selected.
        /// </summary>
        public int? SelectedAnnotationIndex => dataPointAnnotationService?.SelectedAnnotationIndex;

        #endregion

        #region Properties - Grid Visibility

        public bool IsSphericalGridVisible
        {
            get => isSphericalGridVisible;
            set
            {
                if (SetProperty(ref isSphericalGridVisible, value))
                {
                    UpdateGridVisibility(sphereGrid, value);
                }
            }
        }

        public bool IsHeadingGridVisible
        {
            get => isHeadingGridVisible;
            set
            {
                if (SetProperty(ref isHeadingGridVisible, value))
                {
                    UpdateGridVisibility(headingGrid, value);
                }
            }
        }

        #endregion

        #region Properties - Camera Settings

        public int SelectedCameraIndex
        {
            get => selectedCameraIndex;
            set
            {
                if (SetProperty(ref selectedCameraIndex, value))
                {
                    cameraService.SetCameraOrientation(view, value);
                }
            }
        }

        public int SelectedProjectionIndex
        {
            get => selectedProjectionIndex;
            set
            {
                if (SetProperty(ref selectedProjectionIndex, value))
                {
                    cameraService.SetProjection(
                        view,
                        chart,
                        value,
                        ref oldDimensions,
                        ref oldDimensionsLegacy,
                        enabled => IsDistanceEnabled = enabled,
                        dimensions => ResetDimensions(dimensions));
                }
            }
        }

        #endregion

        #region Properties - Camera Rotation

        public double VerticalRotation
        {
            get => verticalRotation;
            set
            {
                if (SetProperty(ref verticalRotation, value))
                {
                    VerticalRotationText = ((int)value).ToString();
                    cameraService.SetRotation(view, chart, verticalRotation, horizontalRotation, sideRotation, ref bOwnChange);
                }
            }
        }

        public double HorizontalRotation
        {
            get => horizontalRotation;
            set
            {
                if (SetProperty(ref horizontalRotation, value))
                {
                    HorizontalRotationText = ((int)value).ToString();
                    cameraService.SetRotation(view, chart, verticalRotation, horizontalRotation, sideRotation, ref bOwnChange);
                }
            }
        }

        public double SideRotation
        {
            get => sideRotation;
            set
            {
                if (SetProperty(ref sideRotation, value))
                {
                    SideRotationText = ((int)value).ToString();
                    cameraService.SetRotation(view, chart, verticalRotation, horizontalRotation, sideRotation, ref bOwnChange);
                }
            }
        }

        public double Distance
        {
            get => distance;
            set
            {
                if (SetProperty(ref distance, value))
                {
                    DistanceText = ((int)value).ToString();
                    cameraService.SetDistance(view, chart, distance, ref bOwnChange);
                }
            }
        }

        #endregion

        #region Properties - Rotation Text Inputs

        public string VerticalRotationText
        {
            get => verticalRotationText;
            set
            {
                if (SetProperty(ref verticalRotationText, value))
                {
                    if (int.TryParse(value, out int parsed) && parsed >= -90 && parsed <= 90)
                    {
                        VerticalRotation = parsed;
                    }
                }
            }
        }

        public string HorizontalRotationText
        {
            get => horizontalRotationText;
            set
            {
                if (SetProperty(ref horizontalRotationText, value))
                {
                    if (int.TryParse(value, out int parsed) && parsed >= -180 && parsed <= 180)
                    {
                        HorizontalRotation = parsed;
                    }
                }
            }
        }

        public string SideRotationText
        {
            get => sideRotationText;
            set
            {
                if (SetProperty(ref sideRotationText, value))
                {
                    if (int.TryParse(value, out int parsed) && parsed >= -180 && parsed <= 180)
                    {
                        SideRotation = parsed;
                    }
                }
            }
        }

        public string DistanceText
        {
            get => distanceText;
            set
            {
                if (SetProperty(ref distanceText, value))
                {
                    if (int.TryParse(value, out int parsed) && parsed >= 10 && parsed <= 2000)
                    {
                        Distance = parsed;
                    }
                }
            }
        }

        #endregion

        #region Properties - Dimensions

        public string WidthText
        {
            get => widthText;
            set
            {
                if (SetProperty(ref widthText, value))
                {
                    cameraService.UpdateDimensions(view, chart, widthText, heightText, depthText);
                }
            }
        }

        public string HeightText
        {
            get => heightText;
            set
            {
                if (SetProperty(ref heightText, value))
                {
                    cameraService.UpdateDimensions(view, chart, widthText, heightText, depthText);
                }
            }
        }

        public string DepthText
        {
            get => depthText;
            set
            {
                if (SetProperty(ref depthText, value))
                {
                    cameraService.UpdateDimensions(view, chart, widthText, heightText, depthText);
                }
            }
        }

        public bool IsDistanceEnabled
        {
            get => isDistanceEnabled;
            set => SetProperty(ref isDistanceEnabled, value);
        }

        #endregion

        #region Commands

        public ICommand TopView2DCommand { get; }
        public ICommand AddAnnotationCommand { get; }
        public ICommand DeleteAnnotationCommand { get; }
        public ICommand ToggleMouseTrackingCommand { get; }
        public ICommand DeleteSelectedAnnotationCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand ToggleAltitudeAnnotationsCommand { get; }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when a data point deletion is requested.
        /// The index parameter indicates which data point should be deleted.
        /// </summary>
        public event EventHandler<int>? DataPointDeleteRequested;

        #endregion

        #region Properties - Annotation Management

        public string AnnotationCountText
        {
            get => annotationCountText;
            set => SetProperty(ref annotationCountText, value);
        }

        public bool IsMouseTrackingEnabled
        {
            get => isMouseTrackingEnabled;
            set
            {
                if (SetProperty(ref isMouseTrackingEnabled, value))
                {
                    if (dataPointAnnotationService != null)
                    {
                        dataPointAnnotationService.SetMouseTrackingEnabled(value);
                    }
                }
            }
        }

        public string SelectedAnnotationText
        {
            get => selectedAnnotationText;
            set => SetProperty(ref selectedAnnotationText, value);
        }

        public bool HasSelectedAnnotation
        {
            get => hasSelectedAnnotation;
            set => SetProperty(ref hasSelectedAnnotation, value);
        }

        public string SelectIndexText
        {
            get => selectIndexText;
            set
            {
                if (SetProperty(ref selectIndexText, value))
                {
                    if (int.TryParse(value, out int index))
                    {
                        SelectAnnotationByIndex(index);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether altitude annotations are visible on the grid lines.
        /// When enabled, displays the altitude/distance values (10, 20, 30, etc.) on the grid circles.
        /// </summary>
        public bool IsAltitudeAnnotationsVisible
        {
            get => isAltitudeAnnotationsVisible;
            set
            {
                if (SetProperty(ref isAltitudeAnnotationsVisible, value))
                {
                    if (chart != null)
                    {
                        chart.BeginUpdate();
                        // Show/hide altitude labels on grid circles
                        chartSetupService.SetAltitudeAnnotationsVisible(value);
                        chart.EndUpdate();
                    }
                }
            }
        }

        #endregion

        #region Chart Initialization

        public void InitializeChart()
        {
            if (chart == null) return;

            chart.BeginUpdate();
            chart.ActiveView = ActiveView.View3D;

            View3D view3D = chart.View3D;
            chartSetupService.ConfigureView3D(view3D);

            sphereGrid = chartSetupService.CreateSphereGrid(view3D, isSphericalGridVisible);
            headingGrid = chartSetupService.CreateHeadingGrid(view3D, isHeadingGridVisible);
            mouseTrackAnnotation = chartSetupService.CreateMouseTrackAnnotation(view3D);

            var dataProvider = externalDataProvider ?? new SphereDataSetProvider();
            var annotationFactory = externalAnnotationFactory ?? new SphereAnnotationFactory();
            dataPointAnnotationService = new DataPointAnnotationService(view3D, dataProvider, annotationFactory);
            
            // Generate initial data points - use count from current dataset if available
            var initialDataSet = dataProvider.GenerateDataSet(50);
            dataPointAnnotationService.GenerateDataPoints(initialDataSet.DataPoints.Count);
            dataPointAnnotationService.SetMouseTrackingEnabled(isMouseTrackingEnabled);
            UpdateAnnotationCountText();

            chart.MouseMove += ChartMouseMove;
            chartSetupService.HideAxesAndWalls(view3D);

            chart.EndUpdate();

            InitializeViewReference();
            SetControlValuesFromChart();
            SetViewPoint();
            StartDataPointAnimation();
        }

        /// <summary>
        /// Refreshes the chart data from the external data provider.
        /// Called when shared data changes to synchronize the chart.
        /// </summary>
        public void RefreshData()
        {
            if (dataPointAnnotationService == null || chart == null) return;

            var dataProvider = externalDataProvider ?? new SphereDataSetProvider();
            var dataSet = dataProvider.GenerateDataSet(0); // Count parameter is ignored by SharedDataSetProvider
            int pointCount = dataSet.DataPoints.Count;
            
            // Regenerate all data points from the current shared dataset
            chart.BeginUpdate();
            dataPointAnnotationService.GenerateDataPoints(pointCount);
            UpdateAnnotationCountText();
            chart.EndUpdate();
        }

        private void InitializeViewReference()
        {
            if (chart == null) return;

            switch (chart.ActiveView)
            {
                case ActiveView.View3D:
                    view = chart.View3D;
                    if (view != null)
                    {
                        ((View3D)view).CameraViewChanged += ViewPointEditorCameraViewChanged;
                    }
                    break;
                case ActiveView.ViewPie3D:
                    view = chart.ViewPie3D;
                    if (view != null)
                    {
                        ((ViewPie3D)view).CameraViewChanged += ViewPointEditorCameraViewChanged;
                    }
                    break;
                default:
                    view = null;
                    break;
            }
        }

        #endregion

        #region Camera Management

        private void SetViewPoint()
        {
            if (chart == null || view == null) return;

            bOwnChange = true;
            chart.BeginUpdate();
            view.Camera.SetEulerAngles(verticalRotation, horizontalRotation, sideRotation);
            view.Camera.ViewDistance = distance;
            chart.EndUpdate();
            bOwnChange = false;
            SetControlValuesFromChart();
        }

        private void SetControlValuesFromChart()
        {
            if (view == null || bOwnChange || chart == null ||
                double.IsNaN(view.Camera.RotationX) || double.IsNaN(view.Camera.RotationZ) ||
                double.IsNaN(view.Camera.RotationY) || double.IsNaN(view.Camera.ViewDistance))
                return;

            UpdateProjectionFromChart();
            UpdateCameraOrientationFromChart();
            UpdateRotationsFromChart();
            UpdateDistanceFromChart();
            UpdateDimensionsFromChart();
        }

        private void UpdateProjectionFromChart()
        {
            if (view == null) return;

            if (view.Camera.Projection == ProjectionType.Orthographic)
            {
                SelectedProjectionIndex = 0;
            }
            else if (view.Camera.Projection == ProjectionType.OrthographicLegacy)
            {
                SelectedProjectionIndex = 1;
            }
            else
            {
                SelectedProjectionIndex = 2;
            }
        }

        private void UpdateCameraOrientationFromChart()
        {
            if (view == null) return;

            if (view.Camera.OrientationMode == OrientationModes.XYZ_Mixed)
            {
                SelectedCameraIndex = 0;
            }
            else
            {
                SelectedCameraIndex = 1;
            }
        }

        private void UpdateRotationsFromChart()
        {
            if (view == null) return;

            double dRotation = NormalizeRotation(view.Camera.RotationX, 89);
            if (dRotation > 90) dRotation = 90;
            if (dRotation < -90) dRotation = -90;
            VerticalRotation = (int)Math.Round(dRotation);

            HorizontalRotation = (int)NormalizeRotation(view.Camera.RotationY, 179);
            SideRotation = (int)NormalizeRotation(view.Camera.RotationZ, 179);
        }

        private void UpdateDistanceFromChart()
        {
            if (view == null) return;

            Distance = (int)view.Camera.ViewDistance;
        }

        private void UpdateDimensionsFromChart()
        {
            if (view == null) return;

            const int minDimension = 10;
            const int maxDimension = 500;

            WidthText = ClampDimension((int)view.Dimensions.Width, minDimension, maxDimension).ToString();
            HeightText = ClampDimension((int)view.Dimensions.Height, minDimension, maxDimension).ToString();
            DepthText = ClampDimension((int)view.Dimensions.Depth, minDimension, maxDimension).ToString();
        }

        private double NormalizeRotation(double rotation, double change)
        {
            double normalized = change * (rotation < 0 ? -1 : 1);
            return (rotation + normalized) % 360D - normalized;
        }

        private int ClampDimension(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void ResetDimensions(PointDoubleXYZ? dimensions)
        {
            cameraService.ResetDimensions(view, chart, dimensions);
            SetControlValuesFromChart();
        }

        #endregion

        #region Event Handlers

        private void ChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mouseTrackingService.HandleMouseMove(surfaceSeries, mouseTrackAnnotation, chart, e);

            if (dataPointAnnotationService != null && chart != null)
            {
                try
                {
                    Point mousePosition = e.GetPosition(e.Source as UIElement);
                    dataPointAnnotationService.HandleMouseMove(chart, mousePosition);
                }
                catch
                {
                    dataPointAnnotationService.ClearHoverState();
                }
            }
        }

        private void ViewPointEditorCameraViewChanged(Camera3D newCameraViewPoint, View3D view, LightningChart chart)
        {
            SetControlValuesFromChart();
        }

        private void ViewPointEditorCameraViewChanged(Camera3D newCameraViewPoint, ViewPie3D view, LightningChart chart)
        {
            SetControlValuesFromChart();
        }

        #endregion

        #region Commands Implementation

        private void ExecuteTopView2D()
        {
            if (view == null || chart == null) return;

            cameraService.SetTopView2D(
                view,
                chart,
                ref oldDimensions,
                ref oldDimensionsLegacy,
                index => SelectedProjectionIndex = index,
                index => SelectedCameraIndex = index,
                value => VerticalRotation = value,
                value => HorizontalRotation = value,
                value => SideRotation = value,
                dimensions => ResetDimensions(dimensions));

            SetViewPoint();
        }

        private bool CanExecuteAnnotationCommand()
        {
            return dataPointAnnotationService != null && chart != null;
        }

        private void ExecuteAddAnnotation()
        {
            if (dataPointAnnotationService == null || chart == null) return;

            chart.BeginUpdate();
            dataPointAnnotationService.AddRandomAnnotation();
            UpdateAnnotationCountText();
            chart.EndUpdate();
        }

        private void ExecuteDeleteAnnotation()
        {
            if (dataPointAnnotationService == null || chart == null) return;

            chart.BeginUpdate();
            dataPointAnnotationService.DeleteLastAnnotation();
            UpdateAnnotationCountText();
            chart.EndUpdate();
        }

        private void UpdateAnnotationCountText()
        {
            if (dataPointAnnotationService != null)
            {
                AnnotationCountText = dataPointAnnotationService.AnnotationCount.ToString();
            }
        }

        private void ExecuteToggleMouseTracking()
        {
            IsMouseTrackingEnabled = !IsMouseTrackingEnabled;
        }

        private void ExecuteToggleAltitudeAnnotations()
        {
            IsAltitudeAnnotationsVisible = !IsAltitudeAnnotationsVisible;
        }

        private void ExecuteDeleteSelectedAnnotation()
        {
            if (dataPointAnnotationService == null || chart == null) return;

            if (!dataPointAnnotationService.SelectedAnnotationIndex.HasValue)
                return;

            int indexToDelete = dataPointAnnotationService.SelectedAnnotationIndex.Value;

            // Fire event so demo can delete from shared data service
            // The shared data service will then notify both charts to refresh
            DataPointDeleteRequested?.Invoke(this, indexToDelete);
            
            // Clear selection - the actual deletion will happen via RefreshData when shared data changes
            dataPointAnnotationService.ClearSelection();
            UpdateSelectedAnnotationInfo();
        }

        private void ExecuteClearSelection()
        {
            if (dataPointAnnotationService == null) return;

            dataPointAnnotationService.ClearSelection();
            UpdateSelectedAnnotationInfo();
        }

        private void UpdateSelectedAnnotationInfo()
        {
            if (dataPointAnnotationService == null)
            {
                SelectedAnnotationText = "None";
                HasSelectedAnnotation = false;
                return;
            }

            var info = dataPointAnnotationService.GetSelectedAnnotationInfo();
            if (info.HasValue)
            {
                SelectedAnnotationText = $"ID: {info.Value.index}";
                HasSelectedAnnotation = true;
            }
            else
            {
                SelectedAnnotationText = "None";
                HasSelectedAnnotation = false;
            }
        }

        public void HandleAnnotationSelection(Point mousePosition)
        {
            if (dataPointAnnotationService == null || chart == null) return;

            chart.BeginUpdate();
            dataPointAnnotationService.SelectAnnotationAtPosition(chart, mousePosition);
            UpdateSelectedAnnotationInfo();
            chart.EndUpdate();
        }

        private void SelectAnnotationByIndex(int index)
        {
            if (dataPointAnnotationService == null || chart == null) return;

            chart.BeginUpdate();
            dataPointAnnotationService.SelectAnnotationByIndex(index);
            UpdateSelectedAnnotationInfo();
            chart.EndUpdate();
        }

        #endregion

        #region Helper Methods

        private void UpdateGridVisibility(SurfaceMeshSeries3D? grid, bool isVisible)
        {
            if (grid != null)
            {
                grid.Visible = isVisible;
            }
        }

        #endregion

        #region Data Point Animation

        /// <summary>
        /// Gets whether the animation is currently running.
        /// </summary>
        public bool IsAnimationRunning => dataPointUpdateTimer != null && dataPointUpdateTimer.IsEnabled;

        /// <summary>
        /// Starts the data point animation.
        /// </summary>
        public void StartAnimation()
        {
            StartDataPointAnimation();
        }

        /// <summary>
        /// Stops the data point animation.
        /// </summary>
        public void StopAnimation()
        {
            StopDataPointAnimation();
        }

        private void StartDataPointAnimation()
        {
            if (dataPointUpdateTimer != null)
            {
                dataPointUpdateTimer.Stop();
                dataPointUpdateTimer = null;
            }

            if (dataPointAnnotationService == null) return;

            dataPointUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            dataPointUpdateTimer.Tick += DataPointUpdateTimer_Tick;
            lastUpdateTime = DateTime.Now;
            dataPointUpdateTimer.Start();
        }

        private void StopDataPointAnimation()
        {
            if (dataPointUpdateTimer != null)
            {
                dataPointUpdateTimer.Stop();
                dataPointUpdateTimer.Tick -= DataPointUpdateTimer_Tick;
                dataPointUpdateTimer = null;
            }
        }

        private void DataPointUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (dataPointAnnotationService == null || chart == null) return;

            DateTime currentTime = DateTime.Now;
            double deltaTimeSeconds = (currentTime - lastUpdateTime).TotalSeconds;
            lastUpdateTime = currentTime;

            chart.BeginUpdate();
            dataPointAnnotationService.UpdateDataPointsClockwise(deltaTimeSeconds, chart);
            chart.EndUpdate();
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            StopDataPointAnimation();
            dataPointAnnotationService?.ClearExistingData();

            if (chart != null)
            {
                chart.Dispose();
                chart = null;
            }
        }

        #endregion
    }
}


