using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Demo.ViewModels;
using LightningChartLib.WPF.Charting;
using LightningChartMVVM = LightningChartLib.WPF.ChartingMVVM;

namespace Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private MainViewModel? viewModel;
        private LightningChart? surfaceChart;

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

            // Initialize Surface Chart (left side)
            surfaceChart = new LightningChart();
            gridSurfaceChartContainer.Children.Add(surfaceChart);
            viewModel.SurfaceChart = surfaceChart;

            // Initialize Polar Chart (right side) - already in XAML
            if (polarChart != null)
            {
                viewModel.PolarChart = polarChart;
            }

            // Update point count label
            UpdatePointCountLabel();
            
            // Subscribe to PropertyChanged for point count updates
            if (viewModel != null)
            {
                viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.DataPointCount))
                    {
                        UpdatePointCountLabel();
                    }
                };
            }
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

        private void PolarChart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Handle polar chart selection
            if (viewModel != null && polarChart != null)
            {
                Point mousePosition = e.GetPosition(polarChart);
                viewModel.HandlePolarChartSelection(mousePosition);
            }
        }

        private void PolarChart_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && viewModel != null)
            {
                // For polar chart, deletion will be handled by selecting and pressing delete
                // The selected index is tracked in MainViewModel
                if (viewModel.SelectedPolarIndex.HasValue)
                {
                    viewModel.HandlePolarChartDeletion(viewModel.SelectedPolarIndex.Value);
                    UpdatePointCountLabel();
                }
            }
        }

        public void Dispose()
        {
            if (surfaceChart != null)
            {
                gridSurfaceChartContainer.Children.Clear();
            }
            
            viewModel?.Dispose();
        }
    }
}

