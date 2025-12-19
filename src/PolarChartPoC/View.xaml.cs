using System.Windows;
using LightningChartLib.WPF.ChartingMVVM;

namespace PolarChartPoC
{
    public partial class View : Window
    {
        public View()
        {
            InitializeComponent();
            this.Loaded += ViewLoaded;
            
            chart.MouseMove += ChartMouseMove;
            chart.MouseLeftButtonDown += ChartMouseLeftButtonDown;
        }

        private void ViewLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void ChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is ViewModel viewModel)
            {
                Point mousePosition = e.GetPosition(chart);
                viewModel.HandleMouseMove(mousePosition, chart.ActualWidth, chart.ActualHeight);
            }
        }

        private void ChartMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ViewModel viewModel)
            {
                Point mousePosition = e.GetPosition(chart);
                viewModel.HandleMouseClick(mousePosition);
            }
        }
    }
}
