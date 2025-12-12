using System;
using System.Windows.Media;

namespace InteractiveExamples.Models
{
    public class SphereDataPoint
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public double Value1 { get; set; }

        public double Value2 { get; set; }

        public double Value3 { get; set; }

        public double Value4 { get; set; }

        public Color Color { get; set; }

        public double Pace { get; set; }

        public static SphereDataPoint GenerateRandom(Random? random = null)
        {
            random ??= new Random();

            double x, y, z, distance;
            do
            {
                x = (random.NextDouble() * 2.0 - 1.0) * 100.0;
                y = (random.NextDouble() * 2.0 - 1.0) * 100.0;
                z = (random.NextDouble() * 2.0 - 1.0) * 100.0;
                
                distance = Math.Sqrt(x * x + y * y + z * z);
            }
            while (distance > 100.0);

            Color color = Color.FromRgb(
                (byte)random.Next(256),
                (byte)random.Next(256),
                (byte)random.Next(256)
            );

            double pace = 0.5 + random.NextDouble() * 2.5;

            return new SphereDataPoint
            {
                X = x,
                Y = y,
                Z = z,
                Value1 = (random.NextDouble() * 2.0 - 1.0) * 100.0,
                Value2 = (random.NextDouble() * 2.0 - 1.0) * 100.0,
                Value3 = (random.NextDouble() * 2.0 - 1.0) * 100.0,
                Value4 = (random.NextDouble() * 2.0 - 1.0) * 100.0,
                Color = color,
                Pace = pace
            };
        }

        public bool IsWithinSphereBounds()
        {
            double distance = Math.Sqrt(X * X + Y * Y + Z * Z);
            return distance <= 100.0;
        }

        public (double azimuth, double elevation, double radius) ToSpherical()
        {
            double radius = Math.Sqrt(X * X + Y * Y + Z * Z);
            if (radius == 0) return (0, 0, 0);

            double azimuth = Math.Atan2(Y, X) * 180.0 / Math.PI;
            if (azimuth < 0) azimuth += 360.0;

            double elevation = Math.Asin(Z / radius) * 180.0 / Math.PI;

            return (azimuth, elevation, radius);
        }

        public void FromSpherical(double azimuth, double elevation, double radius)
        {
            double azimuthRad = azimuth * Math.PI / 180.0;
            double elevationRad = elevation * Math.PI / 180.0;

            X = radius * Math.Cos(elevationRad) * Math.Cos(azimuthRad);
            Y = radius * Math.Cos(elevationRad) * Math.Sin(azimuthRad);
            //Z = radius * Math.Sin(elevationRad);
            Z = 0;
        }

        public void MoveClockwise(double angularDistance)
        {
            var (azimuth, elevation, radius) = ToSpherical();
            azimuth += angularDistance;
            azimuth = azimuth % 360.0;
            if (azimuth < 0) azimuth += 360.0;

            FromSpherical(azimuth, elevation, radius);
        }
    }
}
 