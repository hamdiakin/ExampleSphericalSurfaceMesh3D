using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Common.Annotations;
using Common.Domain;
using Common.Providers;
using Common.Commands;
using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Annotations;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;
using PolarChartLib.Services;
using PolarChartLib.Models;

namespace PolarChartLib.ViewModels
{
    /// <summary>
    /// ViewModel for the reusable polar chart control, based on the original PoC ViewModel.
    /// </summary>
    public class PolarChartViewModel : DependencyObject
    {
        private DispatcherTimer timer;
        private ICommand startCommand;
        private ICommand stopCommand;

        private readonly IDataSetProvider dataProvider;
        private readonly IAnnotationFactory annotationFactory;
        private IPolarChartRenderer? chartRenderer;
        private ProcessedDataSet? currentDataSet;

        private int? selectedIndex = null;
        private int? hoveredIndex = null;
        private bool isMouseTrackingEnabledField = true;
        private bool isAltitudeAnnotationsVisibleField = false;
        private DateTime lastUpdateTime;

        private LightningChartLib.WPF.ChartingMVVM.LightningChart? chart;
        private ViewPolar? viewPolar;

        public ICommand StartCommand => startCommand;
        public ICommand StopCommand => stopCommand;

        internal bool IsRunning => timer?.IsEnabled ?? false;

        public static readonly DependencyProperty AxesProperty =
            DependencyProperty.Register(
                "axes",
                typeof(AxisPolarCollection),
                typeof(PolarChartViewModel));

        public AxisPolarCollection axes
        {
            get => (AxisPolarCollection)GetValue(AxesProperty);
            set => SetValue(AxesProperty, value);
        }

        public static readonly DependencyProperty annotationProperty =
            DependencyProperty.Register(
                "annotation",
                typeof(AnnotationPolarCollection),
                typeof(PolarChartViewModel));

        public AnnotationPolarCollection annotation
        {
            get => (AnnotationPolarCollection)GetValue(annotationProperty);
            set => SetValue(annotationProperty, value);
        }

        public static readonly DependencyProperty AnnotationCountProperty =
            DependencyProperty.Register(
                "annotationCount",
                typeof(int),
                typeof(PolarChartViewModel),
                new PropertyMetadata(50));

        public int annotationCount
        {
            get => (int)GetValue(AnnotationCountProperty);
            set => SetValue(AnnotationCountProperty, value);
        }

        public static readonly DependencyProperty textBoxIntervalProperty =
            DependencyProperty.Register(
                "textBoxInterval",
                typeof(int),
                typeof(PolarChartViewModel),
                new PropertyMetadata(16));

        public int textBoxInterval
        {
            get => (int)GetValue(textBoxIntervalProperty);
            set => SetValue(textBoxIntervalProperty, value);
        }

        public static readonly DependencyProperty StartProperty =
            DependencyProperty.Register(
                "start",
                typeof(bool),
                typeof(PolarChartViewModel),
                new PropertyMetadata(true));

        public bool start
        {
            get => (bool)GetValue(StartProperty);
            set => SetValue(StartProperty, value);
        }

        public static readonly DependencyProperty StopProperty =
            DependencyProperty.Register(
                "stop",
                typeof(bool),
                typeof(PolarChartViewModel),
                new PropertyMetadata(false));

        public bool stop
        {
            get => (bool)GetValue(StopProperty);
            set => SetValue(StopProperty, value);
        }

        public static readonly DependencyProperty IsMouseTrackingEnabledProperty =
            DependencyProperty.Register(
                "isMouseTrackingEnabled",
                typeof(bool),
                typeof(PolarChartViewModel),
                new PropertyMetadata(true, OnIsMouseTrackingEnabledChanged));

        public bool isMouseTrackingEnabled
        {
            get => (bool)GetValue(IsMouseTrackingEnabledProperty);
            set => SetValue(IsMouseTrackingEnabledProperty, value);
        }

        private static void OnIsMouseTrackingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PolarChartViewModel vm && e.NewValue is bool enabled)
            {
                vm.isMouseTrackingEnabledField = enabled;
                vm.RefreshAnnotations();
            }
        }

        public static readonly DependencyProperty IsAltitudeAnnotationsVisibleProperty =
            DependencyProperty.Register(
                "isAltitudeAnnotationsVisible",
                typeof(bool),
                typeof(PolarChartViewModel),
                new PropertyMetadata(false, OnIsAltitudeAnnotationsVisibleChanged));

        public bool isAltitudeAnnotationsVisible
        {
            get => (bool)GetValue(IsAltitudeAnnotationsVisibleProperty);
            set => SetValue(IsAltitudeAnnotationsVisibleProperty, value);
        }

        private static void OnIsAltitudeAnnotationsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PolarChartViewModel vm && e.NewValue is bool visible)
            {
                vm.isAltitudeAnnotationsVisibleField = visible;
                vm.RefreshAnnotations();
            }
        }
        public PolarChartViewModel() : this(null, null)
        {
        }

        public PolarChartViewModel(IDataSetProvider? dataProvider, IAnnotationFactory? annotationFactory)
        {
            this.dataProvider = dataProvider ?? new SphereDataSetProvider();
            this.annotationFactory = annotationFactory ?? new SphereAnnotationFactory();

            startCommand = new RelayCommand(_ => StartMethod(null));
            stopCommand = new RelayCommand(_ => StopMethod(null));

            axes = new AxisPolarCollection();
            var model = new PolarChartModel();
            axes.Add(model.GetAxisPolar());

            annotation = new AnnotationPolarCollection();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(textBoxInterval);
            timer.Tick += DispatcherTimerTick;
        }

        public void AttachChart(LightningChartLib.WPF.ChartingMVVM.LightningChart chart, ViewPolar viewPolar)
        {
            this.chart = chart;
            this.viewPolar = viewPolar;

            if (annotation == null)
            {
                annotation = new AnnotationPolarCollection();
            }
            else
            {
                annotation.Clear();
            }

            var model = new PolarChartModel();
            annotation.AddRange(model.GetAnnotation(axes, viewPolar));

            chartRenderer = new PolarChartRenderer(viewPolar, annotation);
        }

        private void StopMethod(object? obj)
        {
            if (IsRunning)
            {
                timer.Stop();
                stop = false;
                start = true;
            }
        }

        private void StartMethod(object? obj)
        {
            if (IsRunning)
                return;

            try
            {
                int count = annotationCount;
                int interval = textBoxInterval;

                if (count < 1 || count > 1000)
                {
                    MessageBox.Show("Annotation count must be between 1 and 1000", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (viewPolar == null || chart == null)
                {
                    MessageBox.Show("Chart not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (annotation == null)
                {
                    annotation = new AnnotationPolarCollection();
                }

                chartRenderer ??= new PolarChartRenderer(viewPolar, annotation);

                currentDataSet = dataProvider.GenerateDataSet(count);
                lastUpdateTime = DateTime.Now;

                RefreshAnnotations();

                timer.Interval = TimeSpan.FromMilliseconds(interval);
                timer.Start();

                stop = true;
                start = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting animation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void Start() => StartMethod(null);

        public void Stop() => StopMethod(null);

        private void DispatcherTimerTick(object? sender, EventArgs e)
        {
            UpdateDataPoints();
        }
        private void UpdateDataPoints()
        {
            if (currentDataSet == null || chartRenderer == null)
                return;

            DateTime currentTime = DateTime.Now;
            double deltaTimeSeconds = (currentTime - lastUpdateTime).TotalSeconds;
            lastUpdateTime = currentTime;

            foreach (var point in currentDataSet.DataPoints)
            {
                double angularDistance = point.Pace * deltaTimeSeconds;
                point.MoveClockwise(angularDistance);
            }

            RefreshAnnotations();
        }

        /// <summary>
        /// Refreshes the chart data from the external data provider.
        /// Called when shared data changes to synchronize the chart.
        /// </summary>
        public void RefreshData()
        {
            if (chartRenderer == null || viewPolar == null)
                return;

            var dataSet = dataProvider.GenerateDataSet(0);
            currentDataSet = dataSet;
            
            RefreshAnnotations();
        }

        private void RefreshAnnotations()
        {
            if (currentDataSet == null || chartRenderer == null)
                return;

            var specs = annotationFactory.CreateAnnotations(
                currentDataSet,
                selectedIndex,
                hoveredIndex,
                !isMouseTrackingEnabledField,
                isAltitudeAnnotationsVisibleField);

            chartRenderer.RenderAnnotations(specs, currentDataSet);
        }

        public void HandleMouseMove(Point mousePosition, double chartWidth, double chartHeight)
        {
            if (!isMouseTrackingEnabledField || currentDataSet == null || chartRenderer == null || viewPolar == null)
                return;

            var (angle, amplitude) = PolarCoordinateMapper.ToPolar(
                mousePosition.X - chartWidth / 2.0,
                chartWidth / 2.0 - mousePosition.Y,
                0);

            hoveredIndex = chartRenderer.FindNearestAnnotation(angle, amplitude);
            RefreshAnnotations();
        }

        public void HandleMouseClick(Point mousePosition)
        {
            if (currentDataSet == null || chartRenderer == null || viewPolar == null)
                return;

            var (angle, amplitude) = PolarCoordinateMapper.ToPolar(
                mousePosition.X - (chart?.ActualWidth ?? 0) / 2.0,
                (chart?.ActualWidth ?? 0) / 2.0 - mousePosition.Y,
                0);

            selectedIndex = chartRenderer.FindNearestAnnotation(angle, amplitude);
            RefreshAnnotations();
        }
    }
}

