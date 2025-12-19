using System;
using ExampleShared.Core.Domain;

namespace ExamplePolarVectors.Adapters
{
    public static class PolarCoordinateMapper
    {
        /// <summary>
        /// Converts 3D Cartesian coordinates to 2D polar coordinates
        /// Angle: Azimuth angle in degrees (0-360)
        /// Amplitude: Distance from origin in XY plane
        /// </summary>
        public static (double angle, double amplitude) ToPolar(SphereDataPoint point)
        {
            var (azimuth, elevation, radius) = point.ToSpherical();
            
            // Use XY plane projection for amplitude
            double amplitude = Math.Sqrt(point.X * point.X + point.Y * point.Y);
            
            return (azimuth, amplitude);
        }

        /// <summary>
        /// Converts 3D Cartesian coordinates to 2D polar coordinates
        /// </summary>
        public static (double angle, double amplitude) ToPolar(double x, double y, double z)
        {
            double amplitude = Math.Sqrt(x * x + y * y);
            double angle = Math.Atan2(y, x) * 180.0 / Math.PI;
            if (angle < 0) angle += 360.0;
            
            return (angle, amplitude);
        }
    }
}


