using System;
using System.Windows;
using System.Windows.Input;
using Common.Annotations;
using Common.Providers;
using SurfaceChartLib.ViewModels;
using SurfaceChartLib.Views;

namespace Surface3DPoC
{
    /// <summary>
    /// Surface 3D Chart PoC - Standalone application using SurfaceChartLib
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private SurfaceChartViewModel? viewModel;
        private readonly IDataSetProvider dataSetProvider;

        public MainWindow()
        {
            InitializeComponent();
            
            dataSetProvider = new SphereDataSetProvider();
            
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize SurfaceChartViewModel and wire it to the view
            var annotationFactory = new SphereAnnotationFactory();
            viewModel = new SurfaceChartViewModel(dataSetProvider, annotationFactory);
            
            if (gridSurfaceChart?.Children.Count > 0 && gridSurfaceChart.Children[0] is SurfaceChartView chartView)
            {
                chartView.ChartViewModel = viewModel;
            }

            UpdatePointCountLabel();
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel == null) return;

            if (int.TryParse(txtDataPointCount.Text, out int count))
            {
                if (count < 1 || count > 1000)
                {
                    MessageBox.Show("Count must be between 1 and 1000", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Generate new data using the data provider
                var dataSet = dataSetProvider.GenerateDataSet(count);
                
                // Refresh the chart with the new data
                viewModel.RefreshData();
                UpdatePointCountLabel();
            }
            else
            {
                MessageBox.Show("Please enter a valid number", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            viewModel?.StartAnimation();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            viewModel?.StopAnimation();
        }

        private void ChkMouseTracking_Checked(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.IsMouseTrackingEnabled = true;
            }
        }

        private void ChkMouseTracking_Unchecked(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.IsMouseTrackingEnabled = false;
            }
        }

        private void UpdatePointCountLabel()
        {
            if (viewModel != null && lblPointCount != null)
            {
                lblPointCount.Content = $"Points: {viewModel.DataPointAnnotationService?.AnnotationCount ?? 0}";
            }
        }

        // Handle keyboard delete key for selected annotation
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (e.Key == Key.Delete && viewModel?.SelectedAnnotationIndex.HasValue == true)
            {
                // Delete the selected annotation
                viewModel.DataPointAnnotationService?.DeleteSelectedAnnotation();
                UpdatePointCountLabel();
            }
        }

        public void Dispose()
        {
            viewModel?.Dispose();
        }
    }
}

