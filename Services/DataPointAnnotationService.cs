using InteractiveExamples.Models;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Views.View3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace InteractiveExamples.Services
{
    public class DataPointAnnotationService
    {
        private readonly View3D view3D;
        private readonly List<SphereDataPoint> dataPoints;
        private readonly List<Annotation3D> annotations;

        public DataPointAnnotationService(View3D view3D)
        {
            this.view3D = view3D ?? throw new ArgumentNullException(nameof(view3D));
            this.dataPoints = new List<SphereDataPoint>();
            this.annotations = new List<Annotation3D>();
        }

        public IReadOnlyList<SphereDataPoint> DataPoints => dataPoints.AsReadOnly();

        public IReadOnlyList<Annotation3D> Annotations => annotations.AsReadOnly();

        public void GenerateDataPoints(int n, Random? random = null)
        {
            if (n < 0)
                throw new ArgumentException("Number of data points must be non-negative.", nameof(n));

            random ??= new Random();

            ClearExistingData();

            for (int i = 0; i < n; i++)
            {
                SphereDataPoint dataPoint = SphereDataPoint.GenerateRandom(random);
                dataPoints.Add(dataPoint);

                Annotation3D annotation = CreateAnnotationForDataPoint(dataPoint);
                annotations.Add(annotation);
                view3D.Annotations.Add(annotation);
            }
        }

        public void ClearExistingData()
        {
            foreach (var annotation in annotations)
            {
                view3D.Annotations.Remove(annotation);
            }

            dataPoints.Clear();
            annotations.Clear();
        }

        public void UpdateDataPoint(int index, SphereDataPoint updatedDataPoint)
        {
            if (index < 0 || index >= dataPoints.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            if (updatedDataPoint == null)
                throw new ArgumentNullException(nameof(updatedDataPoint));

            dataPoints[index] = updatedDataPoint;
            UpdateAnnotationForDataPoint(index, updatedDataPoint);
        }

        public void UpdateDataPoint(SphereDataPoint oldDataPoint, SphereDataPoint newDataPoint)
        {
            if (oldDataPoint == null)
                throw new ArgumentNullException(nameof(oldDataPoint));
            if (newDataPoint == null)
                throw new ArgumentNullException(nameof(newDataPoint));

            int index = dataPoints.IndexOf(oldDataPoint);
            if (index == -1)
                throw new ArgumentException("Data point not found in the collection.", nameof(oldDataPoint));

            UpdateDataPoint(index, newDataPoint);
        }

        public void UpdateAllAnnotations()
        {
            for (int i = 0; i < dataPoints.Count; i++)
            {
                UpdateAnnotationForDataPoint(i, dataPoints[i]);
            }
        }

        /// <summary>
        /// Updates all data points moving clockwise around the sphere, each with its own pace
        /// </summary>
        /// <param name="deltaTimeSeconds">Time elapsed since last update in seconds</param>
        public void UpdateDataPointsClockwise(double deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0) return;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                SphereDataPoint dataPoint = dataPoints[i];
                
                // Calculate angular distance based on this point's pace
                double angularDistance = dataPoint.Pace * deltaTimeSeconds;
                
                // Move the point clockwise
                dataPoint.MoveClockwise(angularDistance);
                
                // Update the annotation position to match the new data point position
                UpdateAnnotationForDataPoint(i, dataPoint);
            }
        }

        public void UpdateAnnotationForDataPoint(int index, SphereDataPoint dataPoint)
        {
            if (index < 0 || index >= annotations.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            if (dataPoint == null)
                throw new ArgumentNullException(nameof(dataPoint));

            Annotation3D annotation = annotations[index];
            ConfigureAnnotation(annotation, dataPoint);
        }

        private Annotation3D CreateAnnotationForDataPoint(SphereDataPoint dataPoint)
        {
            Annotation3D annotation = new Annotation3D(
                view3D,
                Axis3DBinding.Primary,
                Axis3DBinding.Primary,
                Axis3DBinding.Primary)
            {
                TargetCoordinateSystem = AnnotationTargetCoordinates.AxisValues
            };

            ConfigureAnnotation(annotation, dataPoint);

            return annotation;
        }

        private void ConfigureAnnotation(Annotation3D annotation, SphereDataPoint dataPoint)
        {
            annotation.LocationCoordinateSystem = CoordinateSystem.AxisValues;
            annotation.LocationAxisValues.SetValues(0, 0, 0);
            annotation.TargetAxisValues.SetValues(dataPoint.X, dataPoint.Y, dataPoint.Z);
            annotation.Style = AnnotationStyle.Arrow;
            annotation.ArrowStyleBegin = ArrowStyle.Circle;
            annotation.ArrowStyleEnd = ArrowStyle.Arrow;
            annotation.AllowUserInteraction = false;
            annotation.ArrowLineStyle.Color = dataPoint.Color;
            annotation.TextStyle.Color = dataPoint.Color;
            annotation.Visible = true;
            annotation.Anchor.Y = 1;
            annotation.Fill.Style = RectFillStyle.None;
            annotation.BorderVisible = false;
            annotation.Shadow.Visible = false;
        }
    }
}
