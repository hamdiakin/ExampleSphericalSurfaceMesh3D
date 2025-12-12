using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Axes;
using LightningChartLib.WPF.Charting.CoordinateConverters;
using LightningChartLib.WPF.Charting.Series3D;
using LightningChartLib.WPF.Charting.Views.View3D;
using LightningChartLib.WPF.Charting.Views.ViewPie3D;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InteractiveExamples
{
    public partial class ExampleSphericalSurfaceMesh3D : Window, IDisposable
    {
        private LightningChart chart;

        private SurfaceMeshSeries3D surfaceSeries;

        private SurfaceMeshSeries3D sphereGrid;

        private SurfaceMeshSeries3D headingGrid;

        private Annotation3D mouseTrackAnnotation;


        public ExampleSphericalSurfaceMesh3D()
        {
            InitializeComponent();
            CreateChart();

            mComboBoxCameraValueChanged = new SelectionChangedEventHandler(ComboBoxCameraSelectionChanged);
            mComboBoxProjectionValueChanged = new SelectionChangedEventHandler(ComboBoxProjectionSelectionChanged);
            mDistanceSliderValueChanged = new RoutedPropertyChangedEventHandler<double>(SliderDistanceValueChanged);
            mHorizontalSliderValueChanged = new RoutedPropertyChangedEventHandler<double>(SliderHorizontalRotationValueChanged);
            mSideSliderValueChanged = new RoutedPropertyChangedEventHandler<double>(SliderSideRotationValueChanged);
            mVerticalSliderValueChanged = new RoutedPropertyChangedEventHandler<double>(SliderVerticalRotationValueChanged);

            EnabledChangedEvents();

            if (chart != null)
            {
                switch (chart.ActiveView)
                {
                    case ActiveView.View3D:
                        mView = chart.View3D;
                        if (mView != null)
                        {
                            ((View3D)mView).CameraViewChanged += new View3D.CameraViewChangedHandler(ViewPointEditorCameraViewChanged);
                        }

                        break;
                    case ActiveView.ViewPie3D:
                        mView = chart.ViewPie3D;
                        if (mView != null)
                        {
                            ((ViewPie3D)mView).CameraViewChanged += new ViewPie3D.CameraViewChangedHandler(ViewPointEditorCameraViewChanged);
                        }

                        break;
                    default:
                        mView = null;
                        break;
                }
            }

            SetControlValuesFromChart();
            SetViewPoint();
        }


        private void CreateChart()
        {
            chart = new LightningChart();

            chart.BeginUpdate();

            chart.ActiveView = ActiveView.View3D;


            View3D view = chart.View3D;

            view.Camera.Target.SetValues(0, 0, 0);
            view.Camera.MinimumViewDistance /= 10.0;

            view.Dimensions.SetValues(100, 100, 100);

            view.XAxisPrimary3D.SetRange(-100, 100);
            view.YAxisPrimary3D.SetRange(-100, 100);
            view.ZAxisPrimary3D.SetRange(-100, 100);

            view.YAxisPrimary3D.Units.Text = "dB";

            view.OrientationArrows.Visible = true;


            SphericalPoint[,] sphericalData = CreateSurfaceData();

            SurfacePoint[,] xyzData = sphericalData.ToCartesian();

            int colCount = xyzData.GetLength(0);
            int rowCount = xyzData.GetLength(1);
            for (int col = 0; col < colCount; col++)
            {
                for (int row = 0; row < rowCount; row++)
                {
                    xyzData[col, row].Value = sphericalData[col, row].Distance;
                }
            }

            surfaceSeries = new SurfaceMeshSeries3D(view, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)
            {
                Data = xyzData,
                Fill = SurfaceFillStyle.PalettedByValue,
                WireframeType = SurfaceWireframeType3D.Wireframe
            };
            surfaceSeries.WireframeOffset.SetValues(0, 0, 0);
            surfaceSeries.WireframeLineStyle.Color = Color.FromArgb(40, 0, 0, 0);
            surfaceSeries.ContourLineType = ContourLineType3D.None;
            surfaceSeries.ContourPalette = CreatePalette(surfaceSeries, 100);
            surfaceSeries.ColorSaturation = 80;
            surfaceSeries.Title.Text = "Field density";
            view.SurfaceMeshSeries3D.Add(surfaceSeries);

            SphericalPoint[,] sphereData = CreateSphereGridData(100, 15, 15);

            sphereGrid = new SurfaceMeshSeries3D(view, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)
            {
                Fill = SurfaceFillStyle.None,
                WireframeType = SurfaceWireframeType3D.Wireframe
            };
            sphereGrid.WireframeOffset.SetValues(0, 0, 0);
            sphereGrid.ContourLineType = ContourLineType3D.None;
            sphereGrid.AllowUserInteraction = false;
            sphereGrid.WireframeLineStyle.Color = Color.FromArgb(30, 169, 169, 169);
            SurfacePoint[,] sphereDataXYZ = sphereData.ToCartesian();
            sphereGrid.Data = sphereDataXYZ;
            sphereGrid.Title.Text = "Sphere grid";
            sphereGrid.ShowInLegendBox = false;
            view.SurfaceMeshSeries3D.Add(sphereGrid);


            SphericalPoint[,] sphericalHeadingFlatGrid = CreateHeadingGridData(100, 15, 10);

            headingGrid = new SurfaceMeshSeries3D(view, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)
            {
                Fill = SurfaceFillStyle.None,
                WireframeType = SurfaceWireframeType3D.Wireframe
            };
            headingGrid.WireframeOffset.SetValues(0, 0, 0);
            headingGrid.ContourLineType = ContourLineType3D.None;
            headingGrid.AllowUserInteraction = false;
            headingGrid.WireframeLineStyle.Color = Color.FromArgb(50, 255, 255, 255);
            headingGrid.Title.Text = "Heading grid";
            SurfacePoint[,] flatGridData = sphericalHeadingFlatGrid.ToCartesian();
            headingGrid.ShowInLegendBox = false;
            headingGrid.Data = flatGridData;
            view.SurfaceMeshSeries3D.Add(headingGrid);

            mouseTrackAnnotation = new Annotation3D(view, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)
            {
                TargetCoordinateSystem = AnnotationTargetCoordinates.AxisValues
            };
            mouseTrackAnnotation.TargetAxisValues.SetValues(0, 0, 0);
            mouseTrackAnnotation.LocationCoordinateSystem = CoordinateSystem.AxisValues;
            mouseTrackAnnotation.Style = AnnotationStyle.Arrow;
            mouseTrackAnnotation.ArrowStyleBegin = ArrowStyle.Circle;
            mouseTrackAnnotation.ArrowStyleEnd = ArrowStyle.Circle;
            mouseTrackAnnotation.AllowUserInteraction = false;
            mouseTrackAnnotation.ArrowLineStyle.Color = Color.FromArgb(255, 30, 30, 30);
            mouseTrackAnnotation.TextStyle.Color = Colors.White;
            mouseTrackAnnotation.Anchor.Y = 1;
            mouseTrackAnnotation.Fill.Style = RectFillStyle.None;
            mouseTrackAnnotation.BorderVisible = false;
            mouseTrackAnnotation.Shadow.Visible = false;
            mouseTrackAnnotation.Visible = false;
            view.Annotations.Add(mouseTrackAnnotation);

            chart.MouseMove += chartMouseMove;

            foreach (Axis3DBase axis in view.GetAxes())
            {
                axis.Visible = false;
            }

            foreach (WallBase wall in view.GetWalls())
            {
                wall.Visible = false;
            }

            chart.EndUpdate();

            gridChart.Children.Add(chart);


        }

        private void chartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SurfacePoint nearest;
            int columnIndex;
            int rowIndex;

            Point position = e.GetPosition(e.Source as UIElement);

            if (surfaceSeries.Visible &&
                surfaceSeries.SolveNearestDataPointByCoord((int)position.X, (int)position.Y, out nearest, out columnIndex, out rowIndex))
            {
                chart.BeginUpdate();

                mouseTrackAnnotation.Visible = true;
                SphericalPoint sphericalPoint = SphericalCartesian3D.ToSpherical(new PointDouble3D(nearest.X, nearest.Y, nearest.Z));

                mouseTrackAnnotation.Text = string.Format("[{0}, {1}, {2}]\n[{3}, {4}, {5}]",
                    "X: " + nearest.X.ToString("0.0"), "Y: " + nearest.Y.ToString("0.0"), "Z: " + nearest.Z.ToString("0.0"),
                    "Dist: " + sphericalPoint.Distance.ToString("0.0"),
                    "Heading: " + sphericalPoint.HeadingAngle.ToString("0.0") + "°",
                    "Elevation: " + sphericalPoint.ElevationAngle.ToString("0.0") + "°");

                mouseTrackAnnotation.LocationAxisValues.SetValues(nearest.X, nearest.Y, nearest.Z);

                chart.EndUpdate();
            }
            else
            {
                if (mouseTrackAnnotation.Visible)
                {
                    mouseTrackAnnotation.Visible = false;
                }
            }
        }


        private SphericalPoint[,] CreateSurfaceData()
        {
            int headingCount = 360 + 1;
            int elevationCount = 50;
            double headingStep = 360 / (double)(headingCount - 1);
            double elevationStep = 180 / (double)(elevationCount - 1);
            double headingStartAngle = 0;
            double elevationStartAngle = -90;
            double distance;
            SphericalPoint[,] data = new SphericalPoint[headingCount, elevationCount];

            for (int headingIndex = 0; headingIndex < headingCount; headingIndex++)
            {
                double heading = headingStartAngle + headingIndex * headingStep;
                double headingDistanceFactor = 20;
                if (heading <= 45)
                {
                    headingDistanceFactor = 50;
                }
                else if (heading <= 135)
                {
                    headingDistanceFactor = 20;
                }
                else if (heading <= 225)
                {
                    headingDistanceFactor = 30;
                }
                else if (heading < 315)
                {
                    headingDistanceFactor = 10;
                }
                else
                {
                    headingDistanceFactor = 50;
                }

                for (int elevationIndex = 0; elevationIndex < elevationCount; elevationIndex++)
                {
                    double elevation = elevationStartAngle + elevationIndex * elevationStep;
                    SphericalPoint sphericalPoint;
                    sphericalPoint.HeadingAngle = heading;
                    sphericalPoint.ElevationAngle = elevation;

                    distance = 20 + Math.Abs(headingDistanceFactor * (1.0 + Math.Cos(Math.PI * heading / 180.0 * 4.0)));
                    distance *= Math.Abs(Math.Sin(Math.PI * elevation / 180.0 * 2.0)) * 0.8;
                    sphericalPoint.Distance = distance;
                    data[headingIndex, elevationIndex] = sphericalPoint;
                }
            }

            return data;
        }


        private SphericalPoint[,] CreateSphereGridData(double radius, double headingStep, double elevationStep)
        {
            int headingCount = (int)Math.Ceiling(360.0 / (double)headingStep + 1.0);
            int elevationCount = (int)Math.Ceiling(180.0 / (double)elevationStep + 1.0);
            double headingStartAngle = 0;
            double elevationStartAngle = -90;

            SphericalPoint[,] data = new SphericalPoint[headingCount, elevationCount];

            for (int headingIndex = 0; headingIndex < headingCount; headingIndex++)
            {
                double heading = headingStartAngle + headingIndex * headingStep;

                for (int elevationIndex = 0; elevationIndex < elevationCount; elevationIndex++)
                {
                    double elevation = elevationStartAngle + elevationIndex * elevationStep;
                    SphericalPoint sphericalPoint;
                    sphericalPoint.HeadingAngle = heading;
                    sphericalPoint.ElevationAngle = elevation;
                    sphericalPoint.Distance = radius;
                    data[headingIndex, elevationIndex] = sphericalPoint;
                }
            }
            return data;
        }

        private SphericalPoint[,] CreateHeadingGridData(double radius, double headingStep, int distanceStep)
        {
            int headingCount = (int)Math.Ceiling(360.0 / (double)headingStep + 1.0);
            int distanceCount = (int)Math.Ceiling(radius / distanceStep + 1.0);

            double headingStartAngle = 0;

            SphericalPoint[,] data = new SphericalPoint[headingCount, distanceCount];

            for (int headingIndex = 0; headingIndex < headingCount; headingIndex++)
            {
                double heading = headingStartAngle + headingIndex * headingStep;

                for (int distanceIndex = 0; distanceIndex < distanceCount; distanceIndex++)
                {
                    SphericalPoint sphericalPoint;
                    sphericalPoint.HeadingAngle = heading;
                    sphericalPoint.ElevationAngle = 0;
                    sphericalPoint.Distance = (double)distanceIndex * distanceStep;
                    data[headingIndex, distanceIndex] = sphericalPoint;
                }
            }
            return data;
        }



        private ValueRangePalette CreatePalette(SurfaceMeshSeries3D ownerSeries, double totalRange)
        {
            ValueRangePalette palette = new ValueRangePalette(ownerSeries);
            palette.Steps.DisposeAllAndClear();

            palette.Steps.Add(new PaletteStep(palette, Colors.DarkMagenta, 0));
            palette.Steps.Add(new PaletteStep(palette, Colors.Blue, 0.25 * totalRange));
            palette.Steps.Add(new PaletteStep(palette, Colors.Lime, 0.5 * totalRange));
            palette.Steps.Add(new PaletteStep(palette, Colors.Yellow, 0.7 * totalRange));
            palette.Steps.Add(new PaletteStep(palette, Colors.Red, 1.0 * totalRange));
            palette.Type = PaletteType.Gradient;

            return palette;
        }

        private void CheckBoxSphericalGridCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sphereGrid != null)
            {
                sphereGrid.Visible = (bool)checkBoxSphericalGrid.IsChecked;
            }
        }

        private void CheckBoxHeadingGridCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (headingGrid != null)
            {
                headingGrid.Visible = (bool)checkBoxHeadingGrid.IsChecked;
            }
        }

        public void Dispose()
        {
            gridChart.Children.Clear();

            if (chart != null)
            {
                chart.Dispose();
                chart = null;
            }
        }


        private View3DBase mView;

        private bool mBOwnChange = false;
        private SelectionChangedEventHandler mComboBoxProjectionValueChanged;
        private SelectionChangedEventHandler mComboBoxCameraValueChanged;
        private RoutedPropertyChangedEventHandler<double> mDistanceSliderValueChanged;
        private RoutedPropertyChangedEventHandler<double> mHorizontalSliderValueChanged;
        private RoutedPropertyChangedEventHandler<double> mSideSliderValueChanged;
        private RoutedPropertyChangedEventHandler<double> mVerticalSliderValueChanged;





        private void DisableChangedEvents()
        {
            comboBoxCamera.SelectionChanged -= mComboBoxCameraValueChanged;
            comboBoxProjection.SelectionChanged -= mComboBoxProjectionValueChanged;
            sliderDistance.ValueChanged -= mDistanceSliderValueChanged;
            sliderHorizontalRotation.ValueChanged -= mHorizontalSliderValueChanged;
            sliderSideRotation.ValueChanged -= mSideSliderValueChanged;
            sliderVerticalRotation.ValueChanged -= mVerticalSliderValueChanged;
            textBoxExDepth.TextChanged -= TextBoxExDepthTextChanged;
            textBoxExHeight.TextChanged -= TextBoxExHeightTextChanged;
            textBoxExWidth.TextChanged -= TextBoxExWidthTextChanged;
        }

        private void EnabledChangedEvents()
        {
            comboBoxCamera.SelectionChanged += mComboBoxCameraValueChanged;
            comboBoxProjection.SelectionChanged += mComboBoxProjectionValueChanged;
            sliderDistance.ValueChanged += mDistanceSliderValueChanged;
            sliderHorizontalRotation.ValueChanged += mHorizontalSliderValueChanged;
            sliderSideRotation.ValueChanged += mSideSliderValueChanged;
            sliderVerticalRotation.ValueChanged += mVerticalSliderValueChanged;
            textBoxExDepth.TextChanged += TextBoxExDepthTextChanged;
            textBoxExHeight.TextChanged += TextBoxExHeightTextChanged;
            textBoxExWidth.TextChanged += TextBoxExWidthTextChanged;
        }

        private void SetControlValuesFromChart()
        {
            if (mView != null && !mBOwnChange && !double.IsNaN(mView.Camera.RotationX) && !double.IsNaN(mView.Camera.RotationZ) && !double.IsNaN(mView.Camera.RotationY) && !double.IsNaN(mView.Camera.ViewDistance))
            {
                DisableChangedEvents();

                if (mView.Camera.Projection == ProjectionType.Orthographic)
                {
                    comboBoxProjection.SelectedIndex = 0;
                }
                else if (mView.Camera.Projection == ProjectionType.OrthographicLegacy)
                {
                    comboBoxProjection.SelectedIndex = 1;
                }
                else
                {
                    comboBoxProjection.SelectedIndex = 2;
                }

                if (mView.Camera.OrientationMode == OrientationModes.XYZ_Mixed)
                {
                    comboBoxCamera.SelectedIndex = 0;
                }
                else
                {
                    comboBoxCamera.SelectedIndex = 1;
                }

                double dRotation = mView.Camera.RotationX;
                double dChange = 89 * (dRotation < 0 ? -1 : 1);
                dRotation = (int)((dRotation + dChange) % 360D - dChange);
                if (dRotation > sliderVerticalRotation.Maximum)
                {
                    dRotation = sliderVerticalRotation.Maximum;
                }
                if (dRotation < sliderVerticalRotation.Minimum)
                {
                    dRotation = sliderVerticalRotation.Minimum;
                }
                sliderVerticalRotation.Value = (int)Math.Round(dRotation);
                tbVerticalRotation.Text = sliderVerticalRotation.Value.ToString();

                dRotation = mView.Camera.RotationY;
                dChange = 179 * (dRotation < 0 ? -1 : 1);
                sliderHorizontalRotation.Value = (int)((dRotation + dChange) % 360D - dChange);
                tbHorizontalRotation.Text = sliderHorizontalRotation.Value.ToString();

                dRotation = mView.Camera.RotationZ;
                dChange = 179 * (dRotation < 0 ? -1 : 1);
                sliderSideRotation.Value = (int)((dRotation + dChange) % 360D - dChange);
                tbSideRotation.Text = sliderSideRotation.Value.ToString();

                sliderDistance.Value = (int)mView.Camera.ViewDistance;
                tbDistance.Text = sliderDistance.Value.ToString();

                int iBoxMin = 10, iBoxMax = 500;
                if ((int)mView.Dimensions.Width < iBoxMin)
                {
                    textBoxExWidth.Text = iBoxMin.ToString();
                }
                else if ((int)mView.Dimensions.Width > iBoxMax)
                {
                    textBoxExWidth.Text = iBoxMax.ToString();
                }
                else
                {
                    textBoxExWidth.Text = ((int)mView.Dimensions.Width).ToString();
                }

                if ((int)mView.Dimensions.Height < iBoxMin)
                {
                    textBoxExHeight.Text = iBoxMin.ToString();
                }
                else if ((int)mView.Dimensions.Height > iBoxMax)
                {
                    textBoxExHeight.Text = iBoxMax.ToString();
                }
                else
                {
                    textBoxExHeight.Text = ((int)mView.Dimensions.Height).ToString();
                }

                if ((int)mView.Dimensions.Depth < iBoxMin)
                {
                    textBoxExDepth.Text = iBoxMin.ToString();
                }
                else if ((int)mView.Dimensions.Depth > iBoxMax)
                {
                    textBoxExDepth.Text = iBoxMax.ToString();
                }
                else
                {
                    textBoxExDepth.Text = ((int)mView.Dimensions.Depth).ToString();
                }

                EnabledChangedEvents();
            }
        }

        private void SetViewPoint()
        {
            if (chart == null || mView == null)
            {
                return;
            }

            mBOwnChange = true;

            chart.BeginUpdate();

            mView.Camera.SetEulerAngles(sliderVerticalRotation.Value,
                                         sliderHorizontalRotation.Value,
                                         sliderSideRotation.Value);

            mView.Camera.ViewDistance = sliderDistance.Value;

            chart.EndUpdate();

            mBOwnChange = false;
            SetControlValuesFromChart();
        }

        public void SetViewDistance(int distance)
        {
            sliderDistance.Value = distance;

            SetViewPoint();
        }

        private void ViewPointEditorCameraViewChanged(Camera3D newCameraViewPoint, View3D view, LightningChart chart)
        {
            SetControlValuesFromChart();
        }

        private void ViewPointEditorCameraViewChanged(Camera3D newCameraViewPoint, ViewPie3D view, LightningChart chart)
        {
            SetControlValuesFromChart();
        }

        private void ButtonTopView2DClick(object sender, RoutedEventArgs e)
        {
            DisableChangedEvents();

            if (mView.Camera.Projection != ProjectionType.OrthographicLegacy)
            {
                oldDimensions = saveDimensions();
            }
            else if (mView.Camera.Projection == ProjectionType.OrthographicLegacy)
            {
                oldDimensionsLegacy = saveDimensions();
            }

            resetDimensions(oldDimensions);

            mView.Camera.Projection = ProjectionType.Orthographic;
            comboBoxProjection.SelectedIndex = 0;

            mView.Camera.OrientationMode = OrientationModes.XYZ_Mixed;
            comboBoxCamera.SelectedIndex = 0;

            sliderVerticalRotation.Value = 90;
            sliderHorizontalRotation.Value = 0;
            sliderSideRotation.Value = 90;

            SetViewPoint();

            EnabledChangedEvents();
        }

        private void SliderVerticalRotationValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if ((int)mView.Camera.RotationX != sliderVerticalRotation.Value)
            {
                mView.Camera.RotationX = sliderVerticalRotation.Value;
                tbVerticalRotation.Text = sliderVerticalRotation.Value.ToString();
            }
        }

        private void SliderHorizontalRotationValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if ((int)mView.Camera.RotationY != sliderHorizontalRotation.Value)
            {
                mView.Camera.RotationY = sliderHorizontalRotation.Value;
                tbHorizontalRotation.Text = sliderHorizontalRotation.Value.ToString();
            }
        }

        private void SliderSideRotationValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if ((int)mView.Camera.RotationZ != sliderSideRotation.Value)
            {
                mView.Camera.RotationZ = sliderSideRotation.Value;
                tbSideRotation.Text = sliderSideRotation.Value.ToString();
            }
        }

        private void SliderDistanceValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mView != null)
            {
                if ((int)mView.Camera.ViewDistance != sliderDistance.Value)
                {
                    mView.Camera.ViewDistance = sliderDistance.Value;
                    tbDistance.Text = sliderDistance.Value.ToString();
                }
            }
        }

        private PointDoubleXYZ oldDimensions;
        private PointDoubleXYZ oldDimensionsLegacy;

        private void ComboBoxProjectionSelectionChanged(object sender, EventArgs e)
        {
            if (mView != null)
            {
                if (mView.Camera.Projection != ProjectionType.OrthographicLegacy)
                {
                    oldDimensions = saveDimensions();
                }
                else if (mView.Camera.Projection == ProjectionType.OrthographicLegacy)
                {
                    oldDimensionsLegacy = saveDimensions();
                }

                if (comboBoxProjection.SelectedIndex >= 0)
                {
                    mView.Camera.Projection = (ProjectionType)comboBoxProjection.SelectedIndex;
                    if (mView.Camera.Projection != ProjectionType.OrthographicLegacy)
                    {
                        sliderDistance.IsEnabled = true;
                        resetDimensions(oldDimensions);
                    }
                    else if (mView.Camera.Projection == ProjectionType.OrthographicLegacy)
                    {
                        sliderDistance.IsEnabled = false;
                        if (oldDimensionsLegacy != null)
                        {
                            resetDimensions(oldDimensionsLegacy);
                        }
                    }
                }
            }
        }

        private PointDoubleXYZ saveDimensions()
        {
            if (mView != null)
            {
                PointDoubleXYZ points = new PointDoubleXYZ
                {
                    X = mView.Dimensions.Width,
                    Y = mView.Dimensions.Height,
                    Z = mView.Dimensions.Depth
                };

                return points;
            }
            else
            {
                return null;
            }
        }

        private void resetDimensions(PointDoubleXYZ dimensions)
        {
            if (mView != null)
            {
                chart.BeginUpdate();
                mView.Dimensions.Width = dimensions.X;
                mView.Dimensions.Height = dimensions.Y;
                mView.Dimensions.Depth = dimensions.Z;
                chart.EndUpdate();
            }

            SetControlValuesFromChart();
        }

        private void TextBoxExWidthTextChanged(string oldValue)
        {
            if (mView != null)
            {
                try
                {
                    float value = float.Parse(textBoxExWidth.Text);
                    mView.Dimensions.Width = value;
                }
                catch
                {
                    return;
                }
            }
        }

        private void TextBoxExHeightTextChanged(string oldValue)
        {
            if (mView != null)
            {
                try
                {
                    float value = float.Parse(textBoxExHeight.Text);
                    mView.Dimensions.Height = value;
                }
                catch
                {
                    return;
                }
            }
        }

        private void TextBoxExDepthTextChanged(string oldValue)
        {
            if (mView != null)
            {
                try
                {
                    float value = float.Parse(textBoxExDepth.Text);
                    mView.Dimensions.Depth = value;
                }
                catch
                {
                    return;
                }
            }
        }

        private void TbHorizontalRotationTextChanged(object sender, TextChangedEventArgs e)
        {
            int iParsed;
            bool success = int.TryParse(tbHorizontalRotation.Text, out iParsed);
            if (success && iParsed < sliderHorizontalRotation.Maximum + 1 && iParsed > sliderHorizontalRotation.Minimum - 1)
            {
                sliderHorizontalRotation.Value = iParsed;
            }
        }

        private void TbVerticalRotationTextChanged(object sender, TextChangedEventArgs e)
        {
            int iParsed;
            bool success = int.TryParse(tbVerticalRotation.Text, out iParsed);
            if (success && iParsed < sliderVerticalRotation.Maximum + 1 && iParsed > sliderVerticalRotation.Minimum - 1)
            {
                sliderVerticalRotation.Value = iParsed;
            }
        }

        private void TbSideRotationTextChanged(object sender, TextChangedEventArgs e)
        {
            int iParsed;
            bool success = int.TryParse(tbSideRotation.Text, out iParsed);
            if (success && iParsed < sliderSideRotation.Maximum + 1 && iParsed > sliderSideRotation.Minimum - 1)
            {
                sliderSideRotation.Value = iParsed;
            }
        }

        private void TbDistanceTextChanged(object sender, TextChangedEventArgs e)
        {
            int iParsed;
            bool success = int.TryParse(tbDistance.Text, out iParsed);
            if (success && iParsed < sliderDistance.Maximum + 1 && iParsed > sliderDistance.Minimum - 1)
            {
                sliderDistance.Value = iParsed;
            }
        }


        private void ComboBoxCameraSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mView != null)
            {
                if (comboBoxCamera.SelectedIndex == 0 && mView.Camera.OrientationMode != OrientationModes.XYZ_Mixed)
                {
                    mView.Camera.OrientationMode = OrientationModes.XYZ_Mixed;
                }
                else if (comboBoxCamera.SelectedIndex == 1 && mView.Camera.OrientationMode != OrientationModes.ZXY_Extrinsic)
                {
                    mView.Camera.OrientationMode = OrientationModes.ZXY_Extrinsic;
                }
            }
        }

        private void TextBoxExWidthTextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void TextBoxExHeightTextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void TextBoxExDepthTextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
