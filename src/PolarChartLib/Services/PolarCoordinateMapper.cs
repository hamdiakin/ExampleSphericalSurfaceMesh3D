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
            // LightningChart polar chart has 0° at top (12 o'clock), but our azimuth has 0° at right (3 o'clock)
            // Adjust by -90° to align: 0° (right) → -90° → 270° (top in polar chart)
            double polarAngle = (azimuth - 90.0 + 360.0) % 360.0;
            return (polarAngle, amplitude);
        }

        public static (double angle, double amplitude) ToPolar(double x, double y, double z)
        {
            double amplitude = Math.Sqrt(x * x + y * y);
            double angle = Math.Atan2(y, x) * 180.0 / Math.PI;
            if (angle < 0) angle += 360.0;
            // LightningChart polar chart has 0° at top (12 o'clock), but our angle has 0° at right (3 o'clock)
            // Adjust by -90° to align: 0° (right) → -90° → 270° (top in polar chart)
            double polarAngle = (angle - 90.0 + 360.0) % 360.0;
            return (polarAngle, amplitude);
        }
    }
}

