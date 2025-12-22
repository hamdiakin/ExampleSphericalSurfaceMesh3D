using System.Windows.Media;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Axes;
using LightningChartLib.WPF.Charting.CoordinateConverters;
using LightningChartLib.WPF.Charting.Series3D;
using LightningChartLib.WPF.Charting.Views.View3D;

namespace SurfaceChartLib.Services
{
    /// <summary>
    /// Handles initial setup of the 3D view, surface grids, and mouse tracking annotation.
    /// </summary>
    internal class ChartSetupService
    {
        private readonly SphericalDataService dataService;

        public ChartSetupService(SphericalDataService dataService)
        {
            this.dataService = dataService;
        }

        public void ConfigureView3D(View3D view3D)
        {
            view3D.Camera.Target.SetValues(0, 0, 0);
            view3D.Camera.MinimumViewDistance /= 10.0;
            view3D.Dimensions.SetValues(100, 100, 100);
            view3D.XAxisPrimary3D.SetRange(-100, 100);
            view3D.YAxisPrimary3D.SetRange(-100, 100);
            view3D.ZAxisPrimary3D.SetRange(-100, 100);
            view3D.YAxisPrimary3D.Units.Text = "dB";
            view3D.OrientationArrows.Visible = true;
        }

        public SurfaceMeshSeries3D CreateSphereGrid(View3D view3D, bool isVisible)
        {
            SphericalPoint[,] sphereData = dataService.CreateSphereGridData(100, 15, 15);
            SurfacePoint[,] sphereDataXYZ = sphereData.ToCartesian();

            SurfaceMeshSeries3D grid = new SurfaceMeshSeries3D(view3D, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)
            {
                Fill = SurfaceFillStyle.None,
                WireframeType = SurfaceWireframeType3D.Wireframe
            };

            ConfigureSphereGrid(grid, sphereDataXYZ, isVisible);
            view3D.SurfaceMeshSeries3D.Add(grid);

            return grid;
        }

        public SurfaceMeshSeries3D CreateHeadingGrid(View3D view3D, bool isVisible)
        {
            SphericalPoint[,] sphericalHeadingFlatGrid = dataService.CreateHeadingGridData(100, 15, 10);
            SurfacePoint[,] flatGridData = sphericalHeadingFlatGrid.ToCartesian();

            SurfaceMeshSeries3D grid = new SurfaceMeshSeries3D(view3D, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)
            {
                Fill = SurfaceFillStyle.None,
                WireframeType = SurfaceWireframeType3D.Wireframe
            };

            ConfigureHeadingGrid(grid, flatGridData, isVisible);
            view3D.SurfaceMeshSeries3D.Add(grid);

            return grid;
        }

        public Annotation3D CreateMouseTrackAnnotation(View3D view3D)
        {
            Annotation3D annotation = new(view3D, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)
            {
                TargetCoordinateSystem = AnnotationTargetCoordinates.AxisValues
            };

            ConfigureAnnotation(annotation);
            view3D.Annotations.Add(annotation);

            return annotation;
        }

        public void HideAxesAndWalls(View3D view3D)
        {
            foreach (Axis3DBase axis in view3D.GetAxes())
            {
                axis.Visible = false;
            }

            foreach (WallBase wall in view3D.GetWalls())
            {
                wall.Visible = false;
            }
        }

        private void ConfigureSphereGrid(SurfaceMeshSeries3D grid, SurfacePoint[,] data, bool isVisible)
        {
            grid.WireframeOffset.SetValues(0, 0, 0);
            grid.ContourLineType = ContourLineType3D.None;
            grid.AllowUserInteraction = false;
            grid.WireframeLineStyle.Color = Color.FromArgb(30, 169, 169, 169);
            grid.Data = data;
            grid.Title.Text = "Sphere grid";
            grid.ShowInLegendBox = false;
            grid.Visible = isVisible;
        }

        private void ConfigureHeadingGrid(SurfaceMeshSeries3D grid, SurfacePoint[,] data, bool isVisible)
        {
            grid.WireframeOffset.SetValues(0, 0, 0);
            grid.ContourLineType = ContourLineType3D.None;
            grid.AllowUserInteraction = false;
            grid.WireframeLineStyle.Color = Color.FromArgb(50, 255, 255, 255);
            grid.Title.Text = "Heading grid";
            grid.ShowInLegendBox = false;
            grid.Data = data;
            grid.Visible = isVisible;
        }

        private void ConfigureAnnotation(Annotation3D annotation)
        {
            annotation.TargetAxisValues.SetValues(0, 0, 0);
            annotation.LocationCoordinateSystem = CoordinateSystem.AxisValues;
            annotation.Style = AnnotationStyle.Arrow;
            annotation.ArrowStyleBegin = ArrowStyle.Arrow;
            annotation.ArrowStyleEnd = ArrowStyle.Circle;
            annotation.AllowUserInteraction = false;
            annotation.ArrowLineStyle.Color = Color.FromArgb(255, 30, 30, 30);
            annotation.TextStyle.Color = Colors.White;
            annotation.Anchor.Y = 1;
            annotation.Fill.Style = RectFillStyle.None;
            annotation.BorderVisible = false;
            annotation.Shadow.Visible = false;
            annotation.Visible = false;
        }
    }
}

