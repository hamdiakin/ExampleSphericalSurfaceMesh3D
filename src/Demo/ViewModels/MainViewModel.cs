using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Common.Annotations;
using Common.Commands;
using Common.Domain;
using Demo.Services;
using PolarChartLib.ViewModels;
using SurfaceChartLib.ViewModels;

namespace Demo.ViewModels
{
    public class MainViewModel : Common.ViewModels.BaseViewModel
    {
        private readonly SharedDataService sharedDataService;
        private readonly SharedDataSetProvider sharedDataSetProvider;
        private SurfaceChartViewModel? surfaceChartViewModel;
        private PolarChartViewModel? polarChartViewModel;
        
        private DispatcherTimer? animationTimer;
        private DateTime lastUpdateTime;
        private bool isMouseTrackingEnabled = true;

        public MainViewModel()
        {
            sharedDataService = new SharedDataService();
            sharedDataSetProvider = new SharedDataSetProvider(sharedDataService);
            
            // Subscribe to dataset changes
            sharedDataService.DataSetChanged += OnDataSetChanged;
            sharedDataService.DataPointDeleted += OnDataPointDeleted;
            
            GenerateDatasetCommand = new Common.Commands.RelayCommand(parameter => GenerateDataset());
        }

        #region Properties

        public SurfaceChartViewModel? SurfaceChartViewModel
        {
            get => surfaceChartViewModel;
            set => SetProperty(ref surfaceChartViewModel, value);
        }

        public PolarChartViewModel? PolarChartViewModel
        {
            get => polarChartViewModel;
            set => SetProperty(ref polarChartViewModel, value);
        }

        public int DataPointCount => sharedDataService.DataPointCount;

        public bool IsMouseTrackingEnabled
        {
            get => isMouseTrackingEnabled;
            set
            {
                if (SetProperty(ref isMouseTrackingEnabled, value))
                {
                    if (surfaceChartViewModel != null)
                    {
                        surfaceChartViewModel.IsMouseTrackingEnabled = value;
                    }
                }
            }
        }

        #endregion

        #region Commands

        public ICommand GenerateDatasetCommand { get; }

        #endregion

        #region Chart Initialization

        public void InitializeSurfaceChartViewModel()
        {
            if (surfaceChartViewModel == null)
            {
                var annotationFactory = new SphereAnnotationFactory();
                surfaceChartViewModel = new SurfaceChartViewModel(sharedDataSetProvider, annotationFactory);
                surfaceChartViewModel.IsMouseTrackingEnabled = isMouseTrackingEnabled;
            }
        }

        public void InitializePolarChartViewModel()
        {
            if (polarChartViewModel == null)
            {
                var annotationFactory = new SphereAnnotationFactory();
                polarChartViewModel = new PolarChartViewModel(sharedDataSetProvider, annotationFactory);
            }
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
            surfaceChartViewModel?.RefreshData();
        }

        private void RefreshPolarChart()
        {
            polarChartViewModel?.RefreshData();
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
                RefreshBothCharts();
                OnPropertyChanged(nameof(DataPointCount));
            });
        }

        public void HandleSurfaceChartDeletion()
        {
            if (surfaceChartViewModel?.SelectedAnnotationIndex.HasValue == true)
            {
                int indexToDelete = surfaceChartViewModel.SelectedAnnotationIndex.Value;
                if (sharedDataService.DeleteDataPoint(indexToDelete))
                {
                    // Selection will be cleared when charts refresh
                }
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

            // Surface chart animation is handled by SurfaceChartViewModel internally
            // Polar chart animation is handled by PolarChartViewModel internally
        }

        #endregion


        public void Dispose()
        {
            animationTimer?.Stop();
            animationTimer = null;
            
            surfaceChartViewModel?.Dispose();
            
            sharedDataService.DataSetChanged -= OnDataSetChanged;
            sharedDataService.DataPointDeleted -= OnDataPointDeleted;
        }
    }
}

