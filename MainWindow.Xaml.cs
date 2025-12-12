using InteractiveExamples.ViewModels;
using LightningChartLib.WPF.Charting;
using System;
using System.Windows;

namespace InteractiveExamples
{
    public partial class ExampleSphericalSurfaceMesh3D : Window, IDisposable
    {
        private MainViewModel? viewModel;

        public ExampleSphericalSurfaceMesh3D()
        {
            InitializeComponent();
            
            viewModel = new MainViewModel();
            DataContext = viewModel;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                LightningChart chart = new LightningChart();
            gridChart.Children.Add(chart);
                viewModel.Chart = chart;
            }
        }

        public void Dispose()
        {
            gridChart.Children.Clear();
            viewModel?.Dispose();
        }
    }
}
