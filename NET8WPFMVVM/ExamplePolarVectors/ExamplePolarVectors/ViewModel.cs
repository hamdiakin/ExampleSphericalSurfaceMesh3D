using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Annotations;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ExampleShared.Core.Providers;
using ExampleShared.Core.Annotations;
using ExampleShared.Core.Domain;
using ExamplePolarVectors.Adapters;

namespace ExamplePolarVectors
{
    public class ViewModel : DependencyObject
    {
        private DispatcherTimer _timer = null;
        private DelegateCommand m_startCommand = null;
        private DelegateCommand m_stopCommand = null;
        
        // Shared core components
        private readonly IDataSetProvider _dataProvider;
        private readonly IAnnotationFactory _annotationFactory;
        private PolarChartRenderer _chartRenderer;
        private ProcessedDataSet _currentDataSet;
        
        // State tracking
        private int? _selectedIndex = null;
        private int? _hoveredIndex = null;
        private bool _isMouseTrackingEnabled = true;
        private DateTime _lastUpdateTime;

        #region Dependency Properties
        
        public ICommand StartCommand => m_startCommand;
        public ICommand StopCommand => m_stopCommand;
        
        internal bool IsRunning => _timer?.IsEnabled ?? false;
        
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
                _isMouseTrackingEnabled = value;
                RefreshAnnotations();
            }
        }
        
        #endregion

        #region Properties
        
        private Model _model;
        
        #endregion

        #region Constructor
        
        public ViewModel()
        {
            // Initialize shared core components
            _dataProvider = new SphereDataSetProvider();
            _annotationFactory = new SphereAnnotationFactory();
            
            m_startCommand = new DelegateCommand(StartMethod);
            m_stopCommand = new DelegateCommand(StopMethod);

            _model = new Model();

            axes = new AxisPolarCollection();
            axes.Add(_model.GetAxisPolar());

            annotation = new AnnotationPolarCollection();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(textBoxInterval);
            _timer.Tick += new EventHandler(_dispatcherTimer_Tick);
            
            // Subscribe to window loaded event safely
            var mainWindow = Application.Current?.MainWindow as View;
            if (mainWindow != null)
            {
                if (mainWindow.IsLoaded)
                {
                    ViewModel_Loaded(mainWindow, null);
                }
                else
                {
                    mainWindow.Loaded += ViewModel_Loaded;
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
                            ViewModel_Loaded(window, null);
                        }
                        else
                        {
                            window.Loaded += ViewModel_Loaded;
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
                _timer.Stop();
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

                    // Initialize chart renderer
                    var mainWindow = Application.Current?.MainWindow as View;
                    if (mainWindow?._chart?.ViewPolar != null)
                    {
                        _chartRenderer = new PolarChartRenderer(mainWindow._chart.ViewPolar, annotation);
                        
                        // Generate dataset using shared provider
                        _currentDataSet = _dataProvider.GenerateDataSet(count);
                        _lastUpdateTime = DateTime.Now;
                        
                        // Create and render initial annotations
                        RefreshAnnotations();
                        
                        // Start the timer
                        _timer.Interval = TimeSpan.FromMilliseconds(interval);
                        _timer.Start();
                        
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
        
        private void _dispatcherTimer_Tick(object sender, EventArgs e)
        {
            UpdateDataPoints();
        }

        private void UpdateDataPoints()
        {
            if (_currentDataSet == null || _chartRenderer == null)
                return;

            DateTime currentTime = DateTime.Now;
            double deltaTimeSeconds = (currentTime - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = currentTime;

            // Update all data points (rotate clockwise based on individual pace)
            foreach (var point in _currentDataSet.DataPoints)
            {
                double angularDistance = point.Pace * deltaTimeSeconds;
                point.MoveClockwise(angularDistance);
            }

            // Refresh annotations with updated positions
            RefreshAnnotations();
        }
        
        #endregion

        #region Annotation Management
        
        private void RefreshAnnotations()
        {
            if (_currentDataSet == null || _chartRenderer == null)
                return;

            // Create annotation specs using shared factory
            var annotationSpecs = _annotationFactory.CreateAnnotations(
                _currentDataSet,
                _selectedIndex,
                _hoveredIndex,
                !_isMouseTrackingEnabled
            );

            // Render annotations using polar chart renderer
            _chartRenderer.RenderAnnotations(annotationSpecs, _currentDataSet);
        }
        
        #endregion

        #region Mouse Interaction
        
        public void HandleMouseMove(Point mousePosition, double chartWidth, double chartHeight)
        {
            if (!_isMouseTrackingEnabled || _currentDataSet == null || _chartRenderer == null)
                return;

            // Convert screen coordinates to polar coordinates (simplified)
            // This would need more sophisticated conversion based on chart's actual coordinate system
            // For now, we'll use a basic proximity check
            
            var mainWindow = Application.Current?.MainWindow as View;
            if (mainWindow?._chart?.ViewPolar != null)
            {
                // Find nearest annotation
                // Note: Actual implementation would need proper coordinate conversion
                // This is a placeholder for the interaction system
            }
        }

        public void HandleMouseClick(Point mousePosition)
        {
            if (_currentDataSet == null || _chartRenderer == null)
                return;

            // Similar to HandleMouseMove, would need proper coordinate conversion
            // to determine which annotation was clicked
        }
        
        #endregion

        #region Event Handlers
        
        private void ViewModel_Loaded(object sender, RoutedEventArgs e)
        {
            if (annotation == null)
            {
                annotation = new AnnotationPolarCollection();
            }
            else
            {
                annotation.Clear();
            }
            
            // Initialize with sample annotations from the model
            annotation.AddRange(_model.GetAnnotation(axes));
        }
        
        #endregion
    }
}
