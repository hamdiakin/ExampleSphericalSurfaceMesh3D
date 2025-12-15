using InteractiveExamples.ViewModels;
using LightningChartLib.WPF.Charting;
using System;
using System.Windows;
using System.Windows.Input;

namespace InteractiveExamples
{
    public partial class ExampleSphericalSurfaceMesh3D : Window, IDisposable
    {
        private MainViewModel? viewModel;
        private LightningChart? chart;

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
                chart = new LightningChart();
                chart.MouseLeftButtonDown += Chart_MouseLeftButtonDown;
                gridChart.Children.Add(chart);
                viewModel.Chart = chart;
            }
        }

        private void Chart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (viewModel != null && chart != null)
            {
                Point mousePosition = e.GetPosition(chart);
                viewModel.HandleAnnotationSelection(mousePosition);
            }
        }

        public void Dispose()
        {
            if (chart != null)
            {
                chart.MouseLeftButtonDown -= Chart_MouseLeftButtonDown;
            }
            gridChart.Children.Clear();
            viewModel?.Dispose();
        }
    }
}
