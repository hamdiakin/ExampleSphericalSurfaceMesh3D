using System.Collections.Generic;
using System.Windows.Media;
using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Annotations;
using LightningChartLib.WPF.ChartingMVVM.Axes;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;

namespace PolarChartLib.Models
{
    /// <summary>
    /// Configures the polar axis and history trail annotations for the polar chart.
    /// Adapted from the original PoC Model but independent of Application.Current.MainWindow.
    /// </summary>
    internal class PolarChartModel
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

        public List<AnnotationPolar> GetAnnotation(AxisPolarCollection axes, ViewPolar viewPolar)
        {
            var annotList = new List<AnnotationPolar>();
            int historyCount = 20;
            int vectorCount = 1 + historyCount;

            if (viewPolar == null || axes == null || axes.Count == 0)
            {
                return annotList;
            }

            Color oldArrowColor = Colors.DarkGray;
            Color color = viewPolar.GraphBackground.Color;
            Color transparentToDark = Color.FromArgb(0, color.R, color.G, color.B);

            for (int iVector = 0; iVector < vectorCount; iVector++)
            {
                AnnotationPolar vector = new AnnotationPolar(viewPolar, axes[0]);
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

