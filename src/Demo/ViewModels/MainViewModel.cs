using Common.Annotations;
using Common.Commands;
using Demo.Services;
using PolarChartLib.ViewModels;
using SurfaceChartLib.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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
        private bool isAnimationRunning = true;

        public MainViewModel()
        {
            sharedDataService = new SharedDataService();
            sharedDataSetProvider = new SharedDataSetProvider(sharedDataService);
            
            // Subscribe to dataset changes
            sharedDataService.DataSetChanged += OnDataSetChanged;
            sharedDataService.DataPointDeleted += OnDataPointDeleted;
            
            GenerateDatasetCommand = new RelayCommand(parameter => GenerateDataset());
            StartAnimationCommand = new RelayCommand(_ => StartAllAnimations(), _ => !IsAnimationRunning);
            StopAnimationCommand = new RelayCommand(_ => StopAllAnimations(), _ => IsAnimationRunning);
        }


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



        public ICommand GenerateDatasetCommand { get; }
        public ICommand StartAnimationCommand { get; }
        public ICommand StopAnimationCommand { get; }

        /// <summary>
        /// Gets or sets whether animations are running on both charts.
        /// </summary>
        public bool IsAnimationRunning
        {
            get => isAnimationRunning;
            private set
            {
                if (SetProperty(ref isAnimationRunning, value))
                {
                    // Notify commands to re-evaluate CanExecute
                    (StartAnimationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StopAnimationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }



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
                
                // Subscribe to deletion requests from polar chart to sync with shared data
                polarChartViewModel.DataPointDeleteRequested += OnPolarChartDeleteRequested;
            }
        }
        
        private void OnPolarChartDeleteRequested(object? sender, int indexToDelete)
        {
            // Delete from shared data service - this will notify both charts
            sharedDataService.DeleteDataPoint(indexToDelete);
        }



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

        /// <summary>
        /// Starts animations on both charts.
        /// </summary>
        public void StartAllAnimations()
        {
            surfaceChartViewModel?.StartAnimation();
            polarChartViewModel?.Start();
            IsAnimationRunning = true;
        }

        /// <summary>
        /// Stops animations on both charts.
        /// </summary>
        public void StopAllAnimations()
        {
            surfaceChartViewModel?.StopAnimation();
            polarChartViewModel?.Stop();
            IsAnimationRunning = false;
        }

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

        public void Dispose()
        {
            animationTimer?.Stop();
            animationTimer = null;
            
            surfaceChartViewModel?.Dispose();
            
            if (polarChartViewModel != null)
            {
                polarChartViewModel.DataPointDeleteRequested -= OnPolarChartDeleteRequested;
            }
            
            sharedDataService.DataSetChanged -= OnDataSetChanged;
            sharedDataService.DataPointDeleted -= OnDataPointDeleted;
        }
    }
}

