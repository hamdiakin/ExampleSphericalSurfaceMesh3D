using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LightningChartLib.WPF.ChartingMVVM;
using PolarChartLib.ViewModels;

namespace PolarChartLib.Views
{
    /// <summary>
    /// Reusable polar chart view hosting a LightningChart MVVM polar chart.
    /// Code-behind only wires the chart control to the viewmodel.
    /// </summary>
    public partial class PolarChartView : UserControl
    {
        private PolarChartViewModel? viewModel;

        public PolarChartView()
        {
            InitializeComponent();

            viewModel = DataContext as PolarChartViewModel ?? new PolarChartViewModel();
            DataContext = viewModel;

            Loaded += OnLoaded;
        }

        /// <summary>
        /// Gets or sets the ViewModel for this view.
        /// If set externally, replaces the default ViewModel.
        /// </summary>
        public PolarChartViewModel? ChartViewModel
        {
            get => viewModel;
            set
            {
                if (viewModel != value)
                {
                    viewModel = value;
                    DataContext = viewModel;
                    if (IsLoaded && viewModel != null && chart.ViewPolar != null)
                    {
                        viewModel.AttachChart(chart, chart.ViewPolar);
                    }
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (viewModel != null && chart.ViewPolar != null)
            {
                viewModel.AttachChart(chart, chart.ViewPolar);
                chart.MouseMove += Chart_MouseMove;
                chart.MouseLeftButtonDown += Chart_MouseLeftButtonDown;
            }
        }

        private void Chart_MouseMove(object sender, MouseEventArgs e)
        {
            if (viewModel == null) return;
            var pos = e.GetPosition(chart);
            viewModel.HandleMouseMove(pos, chart.ActualWidth, chart.ActualHeight);
        }

        private void Chart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (viewModel == null) return;
            var pos = e.GetPosition(chart);
            viewModel.HandleMouseClick(pos);
        }
    }
}

