using System;
using System.Windows.Media;
using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.CoordinateConverters;
using LightningChartLib.WPF.Charting.Series3D;

namespace SurfaceChartLib.Services
{
    /// <summary>
    /// Generates spherical surface and grid data and configures color palettes.
    /// </summary>
    internal class SphericalDataService
    {
        public SphericalPoint[,] CreateSurfaceData()
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
                double headingDistanceFactor = CalculateHeadingDistanceFactor(heading);

                for (int elevationIndex = 0; elevationIndex < elevationCount; elevationIndex++)
                {
                    double elevation = elevationStartAngle + elevationIndex * elevationStep;
                    SphericalPoint sphericalPoint;
                    sphericalPoint.HeadingAngle = heading;
                    sphericalPoint.ElevationAngle = elevation;

                    distance = CalculateDistance(heading, elevation, headingDistanceFactor);
                    sphericalPoint.Distance = distance;
                    data[headingIndex, elevationIndex] = sphericalPoint;
                }
            }

            return data;
        }

        public SphericalPoint[,] CreateSphereGridData(double radius, double headingStep, double elevationStep)
        {
            int headingCount = (int)Math.Ceiling(360.0 / headingStep + 1.0);
            int elevationCount = (int)Math.Ceiling(180.0 / elevationStep + 1.0);
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

        public SphericalPoint[,] CreateHeadingGridData(double radius, double headingStep, int distanceStep)
        {
            int headingCount = (int)Math.Ceiling(360.0 / headingStep + 1.0);
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

        public ValueRangePalette CreatePalette(SurfaceMeshSeries3D ownerSeries, double totalRange)
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

        private double CalculateHeadingDistanceFactor(double heading)
        {
            if (heading <= 45)
                return 50;
            else if (heading <= 135)
                return 20;
            else if (heading <= 225)
                return 30;
            else if (heading < 315)
                return 10;
            else
                return 50;
        }

        private double CalculateDistance(double heading, double elevation, double headingDistanceFactor)
        {
            double distance = 20 + Math.Abs(headingDistanceFactor * (1.0 + Math.Cos(Math.PI * heading / 180.0 * 4.0)));
            distance *= Math.Abs(Math.Sin(Math.PI * elevation / 180.0 * 2.0)) * 0.8;
            return distance;
        }
    }
}

