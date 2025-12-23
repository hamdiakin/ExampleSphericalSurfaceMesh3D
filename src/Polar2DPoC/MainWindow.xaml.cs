using System;
using System.Windows;
using System.Windows.Input;
using Common.Annotations;
using Common.Providers;
using PolarChartLib.ViewModels;
using PolarChartLib.Views;

namespace Polar2DPoC
{
    /// <summary>
    /// Polar 2D Chart PoC - Standalone application using PolarChartLib
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private PolarChartViewModel? viewModel;
        private readonly IDataSetProvider dataSetProvider;

        public MainWindow()
        {
            InitializeComponent();
            
            dataSetProvider = new SphereDataSetProvider();
            
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize PolarChartViewModel and wire it to the view
            var annotationFactory = new SphereAnnotationFactory();
            viewModel = new PolarChartViewModel(dataSetProvider, annotationFactory);
            
            if (gridPolarChart?.Children.Count > 0 && gridPolarChart.Children[0] is PolarChartView chartView)
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
            viewModel?.Start();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            viewModel?.Stop();
        }

        private void ChkMouseTracking_Checked(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.isMouseTrackingEnabled = true;
            }
        }

        private void ChkMouseTracking_Unchecked(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.isMouseTrackingEnabled = false;
            }
        }

        private void UpdatePointCountLabel()
        {
            if (viewModel != null && lblPointCount != null)
            {
                lblPointCount.Content = $"Points: {viewModel.annotationCount}";
            }
        }

        // Handle keyboard delete key for selected annotation
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (e.Key == Key.Delete && viewModel?.hasSelectedAnnotation == true)
            {
                // Delete the selected annotation
                viewModel.DeleteSelectedCommand?.Execute(null);
                UpdatePointCountLabel();
            }
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}

