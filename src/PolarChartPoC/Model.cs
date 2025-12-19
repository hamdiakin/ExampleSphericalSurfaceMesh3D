using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Annotations;
using LightningChartLib.WPF.ChartingMVVM.Axes;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PolarChartPoC
{
    public class Model
    {
        public double MaxAmplitude = 100.0;

        public AxisPolar GetAxisPolar()
        {
            AxisPolar axis = new AxisPolar();
            axis.MinAmplitude = 0;
            axis.MaxAmplitude = MaxAmplitude;
            axis.Title.Visible = false;
            axis.AllowScaling = false;
            return axis;
        }
        public List<AnnotationPolar> GetAnnotation(AxisPolarCollection Axes)
        {
            List<AnnotationPolar> annotList = new List<AnnotationPolar>();
            int iHistoryCount = 20;
            int vectorCount = 1 + iHistoryCount;

            Color oldArrowColor = Colors.DarkGray;
            Color color = Colors.Black;
            var mainWindow = Application.Current?.MainWindow as View;
            if (mainWindow?.chart?.ViewPolar != null)
            {
                color = mainWindow.chart.ViewPolar.GraphBackground.Color;
            }
            else if (mainWindow?.chart?.ViewPolar == null || Axes == null || Axes.Count == 0)
            {
                return annotList;
            }
            Color transparentToDark = Color.FromArgb(0, color.R, color.G, color.B);

            for (int iVector = 0; iVector < vectorCount; iVector++)
            {
                AnnotationPolar vector = new AnnotationPolar(mainWindow.chart.ViewPolar, Axes[0]);
                vector.Style = AnnotationStyle.Arrow;
                vector.TextStyle.Visible = false;

                vector.LocationCoordinateSystem = CoordinateSystem.AxisValues;
                vector.LocationAxisValues.Angle = 0;
                vector.LocationAxisValues.Amplitude = 0;
                vector.TargetAxisValues.Amplitude = MaxAmplitude;
                vector.ArrowLineStyle.Width = 3;
                vector.AllowUserInteraction = false;
                vector.ArrowStyleBegin = ArrowStyle.None;
                vector.ArrowLineStyle.Color = ChartTools.CalcGradient(transparentToDark, oldArrowColor,
                    (double)iVector / (double)(vectorCount - 1) * 100.0);

                if (iVector == vectorCount - 1)
                {
                    vector.ArrowLineStyle.Width = 6;
                    vector.ArrowLineStyle.Color = Colors.White;
                }

                annotList.Add(vector);
            }
            return annotList;
        }
    }
}
