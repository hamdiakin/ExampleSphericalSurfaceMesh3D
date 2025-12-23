using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LightningChartLib.WPF.Charting;
using SurfaceChartLib.ViewModels;

namespace SurfaceChartLib.Views
{
    /// <summary>
    /// Reusable surface chart view hosting a LightningChart surface chart.
    /// Code-behind is limited to view wiring; all behavior lives in the viewmodel and services.
    /// </summary>
    public partial class SurfaceChartView : UserControl, IDisposable
    {
        private SurfaceChartViewModel? viewModel;
        private LightningChart? chart;

        public SurfaceChartView()
        {
            InitializeComponent();

            viewModel = DataContext as SurfaceChartViewModel ?? new SurfaceChartViewModel();
            DataContext = viewModel;

            Loaded += OnLoaded;
        }

        /// <summary>
        /// Gets or sets the ViewModel for this view.
        /// If set externally, replaces the default ViewModel.
        /// </summary>
        public SurfaceChartViewModel? ChartViewModel
        {
            get => viewModel;
            set
            {
                if (viewModel != value)
                {
                    viewModel?.Dispose();
                    viewModel = value;
                    DataContext = viewModel;
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (viewModel != null && chart == null)
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
                var mousePosition = e.GetPosition(chart);
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

