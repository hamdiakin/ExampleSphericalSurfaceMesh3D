using System;
using Common.Domain;

namespace PolarChartLib.Services
{
    /// <summary>
    /// Helper for converting 3D positions to 2D polar coordinates (angle, amplitude).
    /// </summary>
    internal static class PolarCoordinateMapper
    {
        public static (double angle, double amplitude) ToPolar(SphereDataPoint point)
        {
            var (azimuth, _, _) = point.ToSpherical();
            double amplitude = Math.Sqrt(point.X * point.X + point.Y * point.Y);
            return (azimuth, amplitude);
        }

        public static (double angle, double amplitude) ToPolar(double x, double y, double z)
        {
            double amplitude = Math.Sqrt(x * x + y * y);
            double angle = Math.Atan2(y, x) * 180.0 / Math.PI;
            if (angle < 0) angle += 360.0;

            return (angle, amplitude);
        }
    }
}

