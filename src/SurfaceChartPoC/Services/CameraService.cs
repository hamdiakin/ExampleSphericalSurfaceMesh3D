using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Views.View3D;
using LightningChartLib.WPF.Charting.Views.ViewPie3D;
using System;

namespace SurfaceChartPoC.Services
{
    public class CameraService
    {
        public void SetCameraOrientation(View3DBase view, int selectedCameraIndex)
        {
            if (view == null) return;

            if (selectedCameraIndex == 0 && view.Camera.OrientationMode != OrientationModes.XYZ_Mixed)
            {
                view.Camera.OrientationMode = OrientationModes.XYZ_Mixed;
            }
            else if (selectedCameraIndex == 1 && view.Camera.OrientationMode != OrientationModes.ZXY_Extrinsic)
            {
                view.Camera.OrientationMode = OrientationModes.ZXY_Extrinsic;
            }
        }

        public void SetProjection(View3DBase view, LightningChart chart, int selectedProjectionIndex, 
            ref PointDoubleXYZ? oldDimensions, ref PointDoubleXYZ? oldDimensionsLegacy, 
            Action<bool> setIsDistanceEnabled, Action<PointDoubleXYZ?> resetDimensions)
        {
            if (view == null || chart == null) return;

            SaveDimensionsIfNeeded(view, ref oldDimensions, ref oldDimensionsLegacy);

            if (selectedProjectionIndex >= 0)
            {
                view.Camera.Projection = (ProjectionType)selectedProjectionIndex;
                if (view.Camera.Projection != ProjectionType.OrthographicLegacy)
                {
                    setIsDistanceEnabled(true);
                    resetDimensions(oldDimensions);
                }
                else if (view.Camera.Projection == ProjectionType.OrthographicLegacy)
                {
                    setIsDistanceEnabled(false);
                    if (oldDimensionsLegacy != null)
                    {
                        resetDimensions(oldDimensionsLegacy);
                    }
                }
            }
        }

        public void SetRotation(View3DBase view, LightningChart chart, double verticalRotation, 
            double horizontalRotation, double sideRotation, ref bool bOwnChange)
        {
            if (view == null || bOwnChange) return;

            bOwnChange = true;
            if (chart != null)
            {
                chart.BeginUpdate();
            }

            view.Camera.SetEulerAngles(verticalRotation, horizontalRotation, sideRotation);

            if (chart != null)
            {
                chart.EndUpdate();
            }

            bOwnChange = false;
        }

        public void SetDistance(View3DBase view, LightningChart chart, double distance, ref bool bOwnChange)
        {
            if (view == null || chart == null || bOwnChange) return;

            bOwnChange = true;
            chart.BeginUpdate();
            view.Camera.ViewDistance = distance;
            chart.EndUpdate();
            bOwnChange = false;
        }

        public void SetTopView2D(View3DBase view, LightningChart chart, 
            ref PointDoubleXYZ? oldDimensions, ref PointDoubleXYZ? oldDimensionsLegacy,
            Action<int> setSelectedProjectionIndex, Action<int> setSelectedCameraIndex,
            Action<double> setVerticalRotation, Action<double> setHorizontalRotation, Action<double> setSideRotation,
            Action<PointDoubleXYZ?> resetDimensions)
        {
            if (view == null || chart == null) return;

            SaveDimensionsIfNeeded(view, ref oldDimensions, ref oldDimensionsLegacy);
            resetDimensions(oldDimensions);

            view.Camera.Projection = ProjectionType.Orthographic;
            setSelectedProjectionIndex(0);

            view.Camera.OrientationMode = OrientationModes.XYZ_Mixed;
            setSelectedCameraIndex(0);

            setVerticalRotation(90);
            setHorizontalRotation(0);
            setSideRotation(90);
        }

        public PointDoubleXYZ? SaveDimensions(View3DBase view)
        {
            if (view == null) return null;

            return new PointDoubleXYZ
            {
                X = view.Dimensions.Width,
                Y = view.Dimensions.Height,
                Z = view.Dimensions.Depth
            };
        }

        public void ResetDimensions(View3DBase view, LightningChart chart, PointDoubleXYZ? dimensions)
        {
            if (view == null || chart == null || dimensions == null) return;

            chart.BeginUpdate();
            view.Dimensions.Width = dimensions.X;
            view.Dimensions.Height = dimensions.Y;
            view.Dimensions.Depth = dimensions.Z;
            chart.EndUpdate();
        }

        public void UpdateDimensions(View3DBase view, LightningChart chart, string widthText, string heightText, string depthText)
        {
            if (view == null || chart == null) return;

            UpdateDimension(view, chart, widthText, value => view.Dimensions.Width = value);
            UpdateDimension(view, chart, heightText, value => view.Dimensions.Height = value);
            UpdateDimension(view, chart, depthText, value => view.Dimensions.Depth = value);
        }

        private void SaveDimensionsIfNeeded(View3DBase view, ref PointDoubleXYZ? oldDimensions, ref PointDoubleXYZ? oldDimensionsLegacy)
        {
            if (view.Camera.Projection != ProjectionType.OrthographicLegacy)
            {
                oldDimensions = SaveDimensions(view);
            }
            else if (view.Camera.Projection == ProjectionType.OrthographicLegacy)
            {
                oldDimensionsLegacy = SaveDimensions(view);
            }
        }

        private void UpdateDimension(View3DBase view, LightningChart chart, string textValue, Action<float> setDimension)
        {
            try
            {
                if (float.TryParse(textValue, out float value))
                {
                    chart.BeginUpdate();
                    setDimension(value);
                    chart.EndUpdate();
                }
            }
            catch { }
        }
    }
}

