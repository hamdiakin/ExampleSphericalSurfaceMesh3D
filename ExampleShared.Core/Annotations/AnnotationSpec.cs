using System.Windows.Media;

namespace ExampleShared.Core.Annotations
{
    public abstract record AnnotationSpec
    {
        public string Id { get; init; } = string.Empty;
        public Color Color { get; init; }
    }

    public record PointLabelSpec : AnnotationSpec
    {
        public double X { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }
        public string Text { get; init; } = string.Empty;
        public int DataPointIndex { get; init; }
    }

    public record ArrowAnnotationSpec : AnnotationSpec
    {
        public double StartX { get; init; }
        public double StartY { get; init; }
        public double StartZ { get; init; }
        public double EndX { get; init; }
        public double EndY { get; init; }
        public double EndZ { get; init; }
        public string? Label { get; init; }
        public bool IsSelected { get; init; }
        public bool IsHovered { get; init; }
        public int DataPointIndex { get; init; }
        public double Pace { get; init; }
    }
}

