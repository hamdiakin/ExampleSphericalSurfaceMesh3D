using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.CoordinateConverters;
using LightningChartLib.WPF.Charting.Series3D;
using System.Windows;
using System.Windows.Input;

namespace SurfaceChartPoC.Services
{
    public class MouseTrackingService
    {
        public void HandleMouseMove(SurfaceMeshSeries3D? surfaceSeries, Annotation3D? mouseTrackAnnotation, 
            LightningChart? chart, MouseEventArgs e)
        {
            if (surfaceSeries == null || mouseTrackAnnotation == null || chart == null) return;

            SurfacePoint nearest;
            int columnIndex;
            int rowIndex;

            Point position = e.GetPosition(e.Source as UIElement);

            if (surfaceSeries.Visible &&
                surfaceSeries.SolveNearestDataPointByCoord((int)position.X, (int)position.Y, out nearest, out columnIndex, out rowIndex))
            {
                ShowAnnotation(chart, mouseTrackAnnotation, nearest);
            }
            else
            {
                HideAnnotation(mouseTrackAnnotation);
            }
        }

        private void ShowAnnotation(LightningChart chart, Annotation3D annotation, SurfacePoint nearest)
        {
            chart.BeginUpdate();

            annotation.Visible = true;
            SphericalPoint sphericalPoint = SphericalCartesian3D.ToSpherical(new PointDouble3D(nearest.X, nearest.Y, nearest.Z));

            annotation.Text = FormatAnnotationText(nearest, sphericalPoint);
            annotation.LocationAxisValues.SetValues(nearest.X, nearest.Y, nearest.Z);

            chart.EndUpdate();
        }

        private void HideAnnotation(Annotation3D annotation)
        {
            if (annotation.Visible)
            {
                annotation.Visible = false;
            }
        }

        private string FormatAnnotationText(SurfacePoint nearest, SphericalPoint sphericalPoint)
        {
            return string.Format("[{0}, {1}, {2}]\n[{3}, {4}, {5}]",
                "X: " + nearest.X.ToString("0.0"), 
                "Y: " + nearest.Y.ToString("0.0"), 
                "Z: " + nearest.Z.ToString("0.0"),
                "Dist: " + sphericalPoint.Distance.ToString("0.0"),
                "Heading: " + sphericalPoint.HeadingAngle.ToString("0.0") + "°",
                "Elevation: " + sphericalPoint.ElevationAngle.ToString("0.0") + "°");
        }
    }
}

