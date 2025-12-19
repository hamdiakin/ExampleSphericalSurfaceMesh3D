using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Demo.Services;
using SurfaceChartPoC.Services;
using SurfaceChartPoC.ViewModels;
using PolarChartPoC.Adapters;
using Common.Annotations;
using Common.Domain;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Views.View3D;
using LightningChartMVVM = LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;
using LightningChartLib.WPF.ChartingMVVM.Axes;
using LightningChartLib.WPF.ChartingMVVM.Annotations;

namespace Demo.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly SharedDataService sharedDataService;
        private readonly SphericalDataService sphericalDataService;
        private readonly ChartSetupService chartSetupService;
        private DataPointAnnotationService? surfaceAnnotationService;
        private PolarChartRenderer? polarChartRenderer;
        
        private LightningChart? surfaceChart;
        private LightningChartMVVM.LightningChart? polarChart;
        private View3D? view3D;
        private ViewPolar? viewPolar;
        
        private DispatcherTimer? animationTimer;
        private DateTime lastUpdateTime;
        private bool isMouseTrackingEnabled = true;
        private int? selectedIndex = null;
        private int? hoveredIndex = null;
        private int? selectedPolarIndex = null;

        public MainViewModel()
        {
            sharedDataService = new SharedDataService();
            sphericalDataService = new SphericalDataService();
            chartSetupService = new ChartSetupService(sphericalDataService);
            
            // Subscribe to dataset changes
            sharedDataService.DataSetChanged += OnDataSetChanged;
            sharedDataService.DataPointDeleted += OnDataPointDeleted;
            
            GenerateDatasetCommand = new RelayCommand(parameter => GenerateDataset());
        }

        #region Properties

        public LightningChart? SurfaceChart
        {
            get => surfaceChart;
            set
            {
                if (SetProperty(ref surfaceChart, value))
                {
                    InitializeSurfaceChart();
                }
            }
        }

        public LightningChartMVVM.LightningChart? PolarChart
        {
            get => polarChart;
            set
            {
                if (SetProperty(ref polarChart, value))
                {
                    InitializePolarChart();
                }
            }
        }

        public int DataPointCount => sharedDataService.DataPointCount;

        public int? SelectedPolarIndex
        {
            get => selectedPolarIndex;
            private set
            {
                if (SetProperty(ref selectedPolarIndex, value))
                {
                    RefreshPolarChart();
                }
            }
        }

        public bool IsMouseTrackingEnabled
        {
            get => isMouseTrackingEnabled;
            set
            {
                if (SetProperty(ref isMouseTrackingEnabled, value))
                {
                    surfaceAnnotationService?.SetMouseTrackingEnabled(value);
                }
            }
        }

        #endregion

        #region Commands

        public ICommand GenerateDatasetCommand { get; }

        #endregion

        #region Chart Initialization

        private void InitializeSurfaceChart()
        {
            if (surfaceChart == null) return;

            surfaceChart.BeginUpdate();
            surfaceChart.ActiveView = ActiveView.View3D;
            view3D = surfaceChart.View3D;
            
            chartSetupService.ConfigureView3D(view3D);
            chartSetupService.HideAxesAndWalls(view3D);
            
            // Create grids (optional, can be hidden)
            var sphereGrid = chartSetupService.CreateSphereGrid(view3D, true);
            var headingGrid = chartSetupService.CreateHeadingGrid(view3D, true);
            
            // Create annotation service with shared data provider
            var sharedProvider = new SharedDataSetProvider(sharedDataService);
            var annotationFactory = new Common.Annotations.SphereAnnotationFactory();
            surfaceAnnotationService = new DataPointAnnotationService(view3D, sharedProvider, annotationFactory);
            surfaceAnnotationService.SetMouseTrackingEnabled(isMouseTrackingEnabled);
            
            // Handle mouse events
            surfaceChart.MouseMove += SurfaceChart_MouseMove;
            surfaceChart.MouseLeftButtonDown += SurfaceChart_MouseLeftButtonDown;
            
            surfaceChart.EndUpdate();
            
            // Generate initial dataset
            GenerateDataset();
            StartAnimation();
        }

        private void InitializePolarChart()
        {
            if (polarChart == null || polarChart.ViewPolar == null) return;

            viewPolar = polarChart.ViewPolar;
            
            // Set up axes
            var axis = new AxisPolar();
            axis.MinAmplitude = 0;
            axis.MaxAmplitude = 100.0;
            axis.Title.Visible = false;
            axis.AllowScaling = false;
            
            viewPolar.Axes.Clear();
            viewPolar.Axes.Add(axis);
            
            // Create polar chart renderer
            polarChartRenderer = new PolarChartRenderer(viewPolar, viewPolar.Annotations);
            
            // Generate initial dataset will be called from GenerateDataset
        }

        #endregion

        #region Dataset Management

        public void GenerateDataset()
        {
            int count = 50;
            var mainWindow = Application.Current?.MainWindow as MainWindow;
            if (mainWindow?.FindName("txtDataPointCount") is System.Windows.Controls.TextBox textBox)
            {
                if (int.TryParse(textBox.Text, out int parsedCount))
                {
                    count = parsedCount;
                }
            }
            
            if (count < 1 || count > 1000)
            {
                MessageBox.Show("Count must be between 1 and 1000", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            sharedDataService.GenerateDataSet(count);
            // RefreshBothCharts will be called by OnDataSetChanged event
        }

        private void RefreshBothCharts()
        {
            RefreshSurfaceChart();
            RefreshPolarChart();
        }

        private void RefreshSurfaceChart()
        {
            if (surfaceAnnotationService == null) return;

            var dataSet = sharedDataService.CurrentDataSet;
            
            // Generate data points from current dataset
            // The SharedDataSetProvider will return the current dataset regardless of count
            surfaceAnnotationService.GenerateDataPoints(dataSet.DataPoints.Count);
        }

        private void RefreshPolarChart()
        {
            if (polarChartRenderer == null || viewPolar == null) return;

            var dataSet = sharedDataService.CurrentDataSet;
            var annotationFactory = new Common.Annotations.SphereAnnotationFactory();
            
            // Use selectedPolarIndex for polar chart, but sync with selectedIndex
            int? effectiveSelectedIndex = selectedPolarIndex ?? selectedIndex;
            
            var annotationSpecs = annotationFactory.CreateAnnotations(
                dataSet,
                effectiveSelectedIndex,
                hoveredIndex,
                !isMouseTrackingEnabled
            );
            
            polarChartRenderer.RenderAnnotations(annotationSpecs, dataSet);
        }

        #endregion

        #region Event Handlers

        private void OnDataSetChanged(object? sender, DataSetChangedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RefreshBothCharts();
                OnPropertyChanged(nameof(DataPointCount));
            });
        }

        private void OnDataPointDeleted(object? sender, DataPointDeletedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Adjust selection/hover indices if needed
                if (selectedIndex.HasValue && selectedIndex.Value > e.DeletedIndex)
                {
                    selectedIndex--;
                }
                else if (selectedIndex == e.DeletedIndex)
                {
                    selectedIndex = null;
                }
                
                if (hoveredIndex.HasValue && hoveredIndex.Value > e.DeletedIndex)
                {
                    hoveredIndex--;
                }
                else if (hoveredIndex == e.DeletedIndex)
                {
                    hoveredIndex = null;
                }
                
                RefreshBothCharts();
                OnPropertyChanged(nameof(DataPointCount));
            });
        }

        private void SurfaceChart_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (surfaceChart == null || surfaceAnnotationService == null) return;
            
            Point mousePosition = e.GetPosition(surfaceChart);
            surfaceAnnotationService.HandleMouseMove(surfaceChart, mousePosition);
        }

        private void SurfaceChart_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (surfaceChart == null || surfaceAnnotationService == null) return;
            
            Point mousePosition = e.GetPosition(surfaceChart);
            bool selectionChanged = surfaceAnnotationService.SelectAnnotationAtPosition(surfaceChart, mousePosition);
            
            if (selectionChanged && surfaceAnnotationService.SelectedAnnotationIndex.HasValue)
            {
                selectedIndex = surfaceAnnotationService.SelectedAnnotationIndex.Value;
                RefreshPolarChart();
            }
        }

        public void HandleSurfaceChartDeletion()
        {
            if (surfaceAnnotationService?.SelectedAnnotationIndex.HasValue == true)
            {
                int indexToDelete = surfaceAnnotationService.SelectedAnnotationIndex.Value;
                if (sharedDataService.DeleteDataPoint(indexToDelete))
                {
                    // Selection will be cleared when charts refresh
                    selectedIndex = null;
                }
            }
        }

        public void HandlePolarChartSelection(Point mousePosition)
        {
            if (polarChartRenderer == null || viewPolar == null || polarChart == null) return;

            var dataSet = sharedDataService.CurrentDataSet;
            if (dataSet.DataPoints.Count == 0) return;

            // Convert mouse position to polar chart coordinates
            // This is a simplified approach - calculate angle and amplitude from mouse position
            double centerX = polarChart.ActualWidth / 2;
            double centerY = polarChart.ActualHeight / 2;
            double dx = mousePosition.X - centerX;
            double dy = mousePosition.Y - centerY;
            
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (angle < 0) angle += 360;
            
            double amplitude = Math.Sqrt(dx * dx + dy * dy);
            double maxAmplitude = Math.Min(centerX, centerY);
            amplitude = (amplitude / maxAmplitude) * 100.0; // Scale to chart's max amplitude

            // Find nearest annotation
            int? nearestIndex = polarChartRenderer.FindNearestAnnotation(angle, amplitude, 15.0, 20.0);
            
            if (nearestIndex.HasValue)
            {
                SelectedPolarIndex = nearestIndex.Value;
                selectedIndex = nearestIndex.Value; // Sync with surface chart selection
                RefreshPolarChart();
            }
        }

        public void HandlePolarChartDeletion(int index)
        {
            if (sharedDataService.DeleteDataPoint(index))
            {
                // Clear selection
                SelectedPolarIndex = null;
                selectedIndex = null;
                // RefreshBothCharts will be called by OnDataPointDeleted event
            }
        }

        #endregion

        #region Animation

        private void StartAnimation()
        {
            if (animationTimer != null) return;
            
            animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
            lastUpdateTime = DateTime.Now;
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            DateTime currentTime = DateTime.Now;
            double deltaTimeSeconds = (currentTime - lastUpdateTime).TotalSeconds;
            lastUpdateTime = currentTime;

            // Update surface chart animation
            if (surfaceChart != null && surfaceAnnotationService != null)
            {
                surfaceAnnotationService.UpdateDataPointsClockwise(deltaTimeSeconds, surfaceChart);
            }

            // Update polar chart animation
            var dataSet = sharedDataService.CurrentDataSet;
            foreach (var point in dataSet.DataPoints)
            {
                double angularDistance = point.Pace * deltaTimeSeconds;
                point.MoveClockwise(angularDistance);
            }
            
            RefreshPolarChart();
        }

        #endregion

        #region Commands Implementation

        private class RelayCommand : ICommand
        {
            private readonly Action<object?> execute;
            private readonly Func<object?, bool>? canExecute;

            public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
                this.canExecute = canExecute;
            }

            public event EventHandler? CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object? parameter)
            {
                return canExecute == null || canExecute(parameter);
            }

            public void Execute(object? parameter)
            {
                execute(parameter);
            }
        }

        #endregion

        public void Dispose()
        {
            animationTimer?.Stop();
            animationTimer = null;
            
            if (surfaceChart != null)
            {
                surfaceChart.MouseMove -= SurfaceChart_MouseMove;
                surfaceChart.MouseLeftButtonDown -= SurfaceChart_MouseLeftButtonDown;
            }
            
            sharedDataService.DataSetChanged -= OnDataSetChanged;
            sharedDataService.DataPointDeleted -= OnDataPointDeleted;
        }
    }
}

