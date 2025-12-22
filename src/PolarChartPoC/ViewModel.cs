using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Annotations;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Common.Providers;
using Common.Annotations;
using Common.Domain;
using Common.Commands;
using PolarChartPoC.Adapters;

namespace PolarChartPoC
{
    public class ViewModel : DependencyObject
    {
        private DispatcherTimer timer = null;
        private RelayCommand mStartCommand = null;
        private RelayCommand mStopCommand = null;
        
        private readonly IDataSetProvider dataProvider;
        private readonly IAnnotationFactory annotationFactory;
        private PolarChartRenderer chartRenderer;
        private ProcessedDataSet currentDataSet;
        
        private int? selectedIndex = null;
        private int? hoveredIndex = null;
        private bool isMouseTrackingEnabledField = true;
        private DateTime lastUpdateTime;

        #region Dependency Properties
        
        public ICommand StartCommand => mStartCommand;
        public ICommand StopCommand => mStopCommand;
        
        internal bool IsRunning => timer?.IsEnabled ?? false;
        
        public static readonly DependencyProperty AxesProperty =
            DependencyProperty.Register(
                "axes",
                typeof(AxisPolarCollection),
                typeof(ViewModel)
            );
        
        public AxisPolarCollection axes
        {
            get { return GetValue(AxesProperty) as AxisPolarCollection; }
            set { SetValue(AxesProperty, value); }
        }
        
        public static readonly DependencyProperty annotationProperty =
            DependencyProperty.Register(
                "annotation",
                typeof(AnnotationPolarCollection),
                typeof(ViewModel)
            );
        
        public AnnotationPolarCollection annotation
        {
            get { return GetValue(annotationProperty) as AnnotationPolarCollection; }
            set { SetValue(annotationProperty, value); }
        }
        
        public static readonly DependencyProperty AnnotationCountProperty =
            DependencyProperty.Register(
                "annotationCount",
                typeof(int),
                typeof(ViewModel),
                new PropertyMetadata(50)
            );
        
        public int annotationCount
        {
            get { return (int)GetValue(AnnotationCountProperty); }
            set { SetValue(AnnotationCountProperty, value); }
        }
        
        public static readonly DependencyProperty textBoxIntervalProperty =
            DependencyProperty.Register(
                "textBoxInterval",
                typeof(int),
                typeof(ViewModel),
                new PropertyMetadata(16) // ~60 FPS
            );
        
        public int textBoxInterval
        {
            get { return (int)GetValue(textBoxIntervalProperty); }
            set { SetValue(textBoxIntervalProperty, value); }
        }
        
        public static readonly DependencyProperty StartProperty =
            DependencyProperty.Register(
                "start",
                typeof(bool),
                typeof(ViewModel),
                new PropertyMetadata(true)
            );
        
        public bool start
        {
            get { return (bool)GetValue(StartProperty); }
            set { SetValue(StartProperty, value); }
        }
        
        public static readonly DependencyProperty StopProperty =
            DependencyProperty.Register(
                "stop",
                typeof(bool),
                typeof(ViewModel),
                new PropertyMetadata(false)
            );
        
        public bool stop
        {
            get { return (bool)GetValue(StopProperty); }
            set { SetValue(StopProperty, value); }
        }
        
        public static readonly DependencyProperty IsMouseTrackingEnabledProperty =
            DependencyProperty.Register(
                "isMouseTrackingEnabled",
                typeof(bool),
                typeof(ViewModel),
                new PropertyMetadata(true)
            );
        
        public bool isMouseTrackingEnabled
        {
            get { return (bool)GetValue(IsMouseTrackingEnabledProperty); }
            set 
            { 
                SetValue(IsMouseTrackingEnabledProperty, value);
                isMouseTrackingEnabledField = value;
                RefreshAnnotations();
            }
        }
        
        #endregion

        #region Properties
        
        private Model model;
        
        #endregion

        #region Constructor
        
        public ViewModel()
        {
            dataProvider = new SphereDataSetProvider();
            annotationFactory = new SphereAnnotationFactory();
            
            mStartCommand = new RelayCommand(StartMethod);
            mStopCommand = new RelayCommand(StopMethod);

            model = new Model();

            axes = new AxisPolarCollection();
            axes.Add(model.GetAxisPolar());

            annotation = new AnnotationPolarCollection();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(textBoxInterval);
            timer.Tick += new EventHandler(DispatcherTimerTick);
            
            var mainWindow = Application.Current?.MainWindow as View;
            if (mainWindow != null)
            {
                if (mainWindow.IsLoaded)
                {
                    ViewModelLoaded(mainWindow, null);
                }
                else
                {
                    mainWindow.Loaded += ViewModelLoaded;
                }
            }
            else
            {
                // If MainWindow is not available yet, try to get it from the dispatcher
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var window = Application.Current?.MainWindow as View;
                    if (window != null)
                    {
                        if (window.IsLoaded)
                        {
                            ViewModelLoaded(window, null);
                        }
                        else
                        {
                            window.Loaded += ViewModelLoaded;
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        #endregion

        #region Command Methods
        
        private void StopMethod(object obj)
        {
            if (IsRunning)
            {
                timer.Stop();
                stop = false;
                start = true;
            }
        }

        private void StartMethod(object obj)
        {
            if (!IsRunning)
            {
                try
                {
                    int count = annotationCount;
                    int interval = textBoxInterval;
                    
                    if (count < 1 || count > 1000)
                    {
                        MessageBox.Show("Annotation count must be between 1 and 1000", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var mainWindow = Application.Current?.MainWindow as View;
                    if (mainWindow?.chart?.ViewPolar != null)
                    {
                        chartRenderer = new PolarChartRenderer(mainWindow.chart.ViewPolar, annotation);
                        
                        currentDataSet = dataProvider.GenerateDataSet(count);
                        lastUpdateTime = DateTime.Now;
                        
                        RefreshAnnotations();
                        
                        timer.Interval = TimeSpan.FromMilliseconds(interval);
                        timer.Start();
                        
                        // Enable/disable buttons
                        stop = true;
                        start = false;
                    }
                    else
                    {
                        MessageBox.Show("Chart not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting animation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void Start()
        {
            StartMethod(null);
        }

        public void Stop()
        {
            StopMethod(null);
        }
        
        #endregion

        #region Animation Updates
        
        private void DispatcherTimerTick(object sender, EventArgs e)
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
        
        #endregion

        #region Annotation Management
        
        private void RefreshAnnotations()
        {
            if (currentDataSet == null || chartRenderer == null)
                return;

            var annotationSpecs = annotationFactory.CreateAnnotations(
                currentDataSet,
                selectedIndex,
                hoveredIndex,
                !isMouseTrackingEnabledField
            );

            chartRenderer.RenderAnnotations(annotationSpecs, currentDataSet);
        }
        
        #endregion

        #region Mouse Interaction
        
        public void HandleMouseMove(Point mousePosition, double chartWidth, double chartHeight)
        {
            if (!isMouseTrackingEnabledField || currentDataSet == null || chartRenderer == null)
                return;

            var mainWindow = Application.Current?.MainWindow as View;
            if (mainWindow?.chart?.ViewPolar != null)
            {
            }
        }

        public void HandleMouseClick(Point mousePosition)
        {
            if (currentDataSet == null || chartRenderer == null)
                return;
        }
        
        #endregion

        #region Event Handlers
        
        private void ViewModelLoaded(object sender, RoutedEventArgs e)
        {
            if (annotation == null)
            {
                annotation = new AnnotationPolarCollection();
            }
            else
            {
                annotation.Clear();
            }
            
            annotation.AddRange(model.GetAnnotation(axes));
        }
        
        #endregion
    }
}
