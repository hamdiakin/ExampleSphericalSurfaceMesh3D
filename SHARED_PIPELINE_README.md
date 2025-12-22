# Shared Data Pipeline Architecture

## Overview

This solution demonstrates a shared data pipeline architecture where two different chart visualizations (3D Spherical Surface and 2D Polar) consume the same dataset, transform pipeline, and annotation definitions.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│           ExampleShared.Core                        │
│  ┌─────────────────────────────────────────────┐  │
│  │  Domain Models                               │  │
│  │  - SphereDataPoint (X, Y, Z, Color, Pace)   │  │
│  │  - ProcessedDataSet                          │  │
│  └─────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────┐  │
│  │  Providers                                   │  │
│  │  - IDataSetProvider                          │  │
│  │  - SphereDataSetProvider                     │  │
│  └─────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────┐  │
│  │  Annotations (Chart-Agnostic)                │  │
│  │  - AnnotationSpec                            │  │
│  │  - ArrowAnnotationSpec                       │  │
│  │  - IAnnotationFactory                        │  │
│  │  - SphereAnnotationFactory                   │  │
│  └─────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
           ▲                           ▲
           │                           │
┌──────────┴────────────┐   ┌─────────┴──────────────┐
│ ExampleSphericalSur-  │   │ ExamplePolarVectors    │
│ faceMesh3D            │   │                        │
│  - 3D Chart Renderer  │   │  - Polar Chart Renderer│
│  - DataPointAnnot...  │   │  - PolarChartRenderer  │
└───────────────────────┘   └────────────────────────┘
```

## Projects

### ExampleShared.Core
The shared library containing:

#### Domain Models
- **SphereDataPoint**: Represents a data point in 3D space with color and animation properties
  - Properties: X, Y, Z, Value1-4, Color, Pace
  - Methods: `ToSpherical()`, `FromSpherical()`, `MoveClockwise()`, `GenerateRandom()`

- **ProcessedDataSet**: Container for a collection of data points
  - Properties: `DataPoints`, `GeneratedAt`

#### Providers
- **IDataSetProvider**: Interface for dataset generation
  - Method: `GenerateDataSet(count, seed?)`

- **SphereDataSetProvider**: Generates random sphere data points within 100-unit radius

#### Annotations (Chart-Agnostic)
- **AnnotationSpec**: Base record for annotation specifications
  - Properties: `Id`, `Color`

- **ArrowAnnotationSpec**: Specification for arrow annotations pointing from data points to origin
  - Properties: `StartX/Y/Z`, `EndX/Y/Z`, `Label`, `IsSelected`, `IsHovered`, `DataPointIndex`, `Pace`

- **IAnnotationFactory**: Interface for creating annotation specifications
  - Method: `CreateAnnotations(dataSet, selectedIndex?, hoveredIndex?, showAllLabels?)`

- **SphereAnnotationFactory**: Creates arrow annotations for each data point with state-aware labels

### ExampleSphericalSurfaceMesh3D
3D visualization using LightningChart.WPF.Charting.NET8

#### Key Components
- **DataPointAnnotationService**: Renders `AnnotationSpec` to 3D `Annotation3D` objects
  - Consumes `IDataSetProvider` and `IAnnotationFactory`
  - Performance optimizations: cached screen positions, batch updates, StringBuilder reuse
  - Supports hover detection, selection, and real-time animation

- **MainViewModel**: Orchestrates chart setup, camera control, and animation (~60 FPS)

### ExamplePolarVectors
2D polar visualization using LightningChart.WPF.ChartingMVVM.NET8

#### Key Components
- **PolarChartRenderer**: Adapter that converts `AnnotationSpec` to `AnnotationPolar`
  - Converts 3D Cartesian → 2D Polar coordinates
  - Angle = azimuth (0-360°)
  - Amplitude = distance in XY plane
  - Maintains visual styling (color, width, selection state)

- **PolarCoordinateMapper**: Helper utilities for coordinate conversion

- **ViewModel**: Manages dataset, animation, and user interaction
  - Uses same `IDataSetProvider` and `IAnnotationFactory` as 3D demo
  - Animation: rotates all points clockwise at individual paces

## Data Flow

1. **Generation**: `IDataSetProvider.GenerateDataSet(n)` → Creates n random `SphereDataPoint`s
2. **Processing**: Optional pipeline transforms (currently minimal filtering)
3. **Annotation Creation**: `IAnnotationFactory.CreateAnnotations(dataSet, ...)` → Produces `AnnotationSpec[]`
4. **Rendering**: 
   - **3D**: `DataPointAnnotationService` → `Annotation3D`
   - **Polar**: `PolarChartRenderer` → `AnnotationPolar`
5. **Animation**: Each point rotates clockwise based on its `Pace` property

## Adding a New Annotation

To add a new annotation that appears in both charts:

1. **Update the factory** (`ExampleShared.Core/Annotations/SphereAnnotationFactory.cs`):
   ```csharp
   public IReadOnlyList<AnnotationSpec> CreateAnnotations(...)
   {
       // Add new AnnotationSpec to the list
       var newSpec = new ArrowAnnotationSpec { /* properties */ };
       specs.Add(newSpec);
   }
   ```

2. **Update 3D renderer** (`Services/DataPointAnnotationService.cs`):
   ```csharp
   private Annotation3D CreateAnnotationFromSpec(ArrowAnnotationSpec spec)
   {
       // Handle new annotation type
   }
   ```

3. **Update polar renderer** (`Adapters/PolarChartRenderer.cs`):
   ```csharp
   private AnnotationPolar CreatePolarAnnotationFromSpec(ArrowAnnotationSpec spec, ...)
   {
       // Handle new annotation type in polar coordinates
   }
   ```

Both charts will automatically display the new annotation!

## Key Features

### Shared Features (Both Charts)
- Same dataset (50 data points by default, configurable 1-1000)
- Same annotation text format: `[index]\nX: value\nY: value`
- Selection state with visual feedback (bold, yellow highlight)
- Hover state with proximity detection
- Mouse tracking toggle (show all vs. hover-only)
- Real-time animation (~60 FPS)
- Individual rotation paces per point

### Chart-Specific Features
- **3D Spherical**:
  - Full 3D camera control
  - Selection via click (proximity-based)
  - Cached screen positions for performance
  
- **Polar**:
  - 2D polar coordinate projection
  - Simpler interaction model
  - Same animation behavior as 3D

## Performance Considerations

### ExampleShared.Core
- Lightweight domain models
- No chart-specific dependencies
- Fast coordinate transformations

### 3D Renderer
- Pre-allocated collections
- Cached screen positions (updated per frame)
- StringBuilder reuse for text formatting
- Batch chart updates

### Polar Renderer
- Efficient coordinate mapping
- Minimal overhead for 2D projection

## Testing

1. **Run ExampleSphericalSurfaceMesh3D**:
   - Verify 50 annotations appear
   - Check hover shows labels
   - Verify selection works (click near point)
   - Confirm smooth clockwise rotation

2. **Run ExamplePolarVectors**:
   - Click Start
   - Verify 50 annotations appear as vectors
   - Confirm same rotation behavior
   - Check annotation count matches 3D demo

## Future Enhancements

Potential additions while maintaining shared pipeline:
- More annotation types (labels, markers, bands)
- Filtering/search by value ranges
- Export/import dataset
- Custom color schemes
- Performance profiling dashboard
- Additional chart types (heatmap, contour)



