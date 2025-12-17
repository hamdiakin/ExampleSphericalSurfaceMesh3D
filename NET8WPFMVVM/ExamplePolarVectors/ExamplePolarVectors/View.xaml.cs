// ----------------------------------------------------------------------------------------------------------
// LightningChart® example code: Demo shows Polar plot with vectors presented as Annotations.
//
// If you need any assistance, or notice error in this example code, please contact support@lightningchart.com. 
//
// Permission to use this code in your application comes with LightningChart® license. 
//
// https://lightningchart.com | support@lightningchart.com | sales@lightningchart.com
//
// © LightningChart Ltd 2009-2025. All rights reserved.  
// ----------------------------------------------------------------------------------------------------------
using System.Windows;
using LightningChartLib.WPF.ChartingMVVM;

namespace ExamplePolarVectors
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class View : Window
    {
        public View()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            // Note: If you see a D3DImage timeout message, this is normal and harmless.
            // LightningChart automatically switches to a fallback WPF image presentation method
            // when DirectX/D3DImage encounters timeout issues. The chart will continue to work
            // normally, just with slightly slower rendering performance.
            // 
            // This typically happens on:
            // - Systems with older graphics drivers
            // - Virtual machines or remote desktop connections
            // - Systems with limited DirectX support
            //
            // The chart functionality is not affected - it's just using a different rendering path.
        }
    }
}
