using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Demo.ViewModels;
using PolarChartLib.Views;
using SurfaceChartLib.Views;

namespace Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private MainViewModel? viewModel;

        public MainWindow()
        {
            InitializeComponent();
            
            viewModel = new MainViewModel();
            DataContext = viewModel;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (viewModel == null) return;

            // Initialize SurfaceChartViewModel and wire it to the view
            viewModel.InitializeSurfaceChartViewModel();
            if (viewModel.SurfaceChartViewModel != null)
            {
                var surfaceChartView = FindName("gridSurfaceChart") as Grid;
                if (surfaceChartView?.Children.Count > 0 && surfaceChartView.Children[0] is SurfaceChartLib.Views.SurfaceChartView chartView)
                {
                    chartView.ChartViewModel = viewModel.SurfaceChartViewModel;
                }
            }

            // Initialize PolarChartViewModel and wire it to the view
            viewModel.InitializePolarChartViewModel();
            if (viewModel.PolarChartViewModel != null)
            {
                var polarChartView = FindName("gridPolarChart") as Grid;
                if (polarChartView?.Children.Count > 0 && polarChartView.Children[0] is PolarChartLib.Views.PolarChartView chartView)
                {
                    chartView.ChartViewModel = viewModel.PolarChartViewModel;
                }
            }

            // Update point count label
            UpdatePointCountLabel();
            
            // Subscribe to PropertyChanged for point count updates
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.DataPointCount))
                {
                    UpdatePointCountLabel();
                }
            };
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            viewModel?.GenerateDatasetCommand.Execute(null);
            UpdatePointCountLabel();
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
                lblPointCount.Content = $"Points: {viewModel.DataPointCount}";
            }
        }

        // Handle keyboard delete key for surface chart
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (e.Key == Key.Delete && viewModel != null)
            {
                viewModel.HandleSurfaceChartDeletion();
                UpdatePointCountLabel();
            }
        }

        public void Dispose()
        {
            viewModel?.Dispose();
        }
    }
}

