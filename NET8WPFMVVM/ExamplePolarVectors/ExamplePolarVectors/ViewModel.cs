using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Annotations;
using LightningChartLib.WPF.ChartingMVVM.SeriesXY;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ExamplePolarVectors
{
    public class ViewModel:DependencyObject
    {
        private DispatcherTimer _timer = null;
        private Random _rand = new Random();
        private DelegateCommand m_startCommand = null;
        private DelegateCommand m_stopCommand = null;
        #region Dependency Property
        public ICommand StartCommand
        {
            get { return m_startCommand; }
        }
        public ICommand StopCommand
        {
            get { return m_stopCommand; }
        }
        internal bool IsRunning
        {
            get
            {
                return _timer.IsEnabled;
            }
        }
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
        public static readonly DependencyProperty textBoxHistoryCountProperty =
         DependencyProperty.Register(
             "textBoxHistoryCount",
             typeof(int),
             typeof(ViewModel),
             new PropertyMetadata(20)
         );
        public int textBoxHistoryCount
        {
            get { return (int)GetValue(textBoxHistoryCountProperty); }
            set { SetValue(textBoxHistoryCountProperty, value); }
        }
        public static readonly DependencyProperty textBoxIntervalProperty =
        DependencyProperty.Register(
            "textBoxInterval",
            typeof(int),
            typeof(ViewModel),
            new PropertyMetadata(50)
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
             new PropertyMetadata(false)
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
            new PropertyMetadata(true)
        );
        public bool stop
        {
            get { return (bool)GetValue(StopProperty); }
            set { SetValue(StopProperty, value); }
        }
        #endregion

        #region Properties
        private Model _model;
        #endregion

        #region Constructor
        public ViewModel()
        {
            m_startCommand = new DelegateCommand(StartMethod);
            m_stopCommand = new DelegateCommand(StopMethod);

            _model = new Model();

            axes = new AxisPolarCollection();
            axes.Add(_model.GetAxisPolar());

            annotation = new AnnotationPolarCollection();

            _timer = new DispatcherTimer();
            _timer.Interval = new TimeSpan(0, 0, 0, 0, 10);
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

        private void StopMethod(object obj)
        {
            if (IsRunning == true)
            {
                // Stop the timer.
                _timer.Stop();

                // Enable/disable buttons.
                stop = false;
                start= true;
               
            }
        }

        private void StartMethod(object obj)
        {
            if (IsRunning == false)
            {
                int iHistoryCount = 0;
                int iInterval = 0;

                try
                {
                    iHistoryCount = textBoxHistoryCount;
                    iInterval = textBoxInterval;
                }
                catch
                {
                    return;
                }

               annotation.Clear();

                //Create vector annotations. One for newest and selected number of old ones. 
                int vectorCount = 1 + iHistoryCount;

                Color oldArrowColor = Colors.DarkGray;
                Color color = (Application.Current.MainWindow as View)._chart.ViewPolar.GraphBackground.Color;
                Color transparentToDark = Color.FromArgb(0, color.R, color.G, color.B);

                for (int iVector = 0; iVector < vectorCount; iVector++)
                {
                    AnnotationPolar vector = new AnnotationPolar((Application.Current.MainWindow as View)._chart.ViewPolar,axes[0]);
                    vector.Style = AnnotationStyle.Arrow;
                    vector.TextStyle.Visible = false;

                    //Location is where the vector starts from
                    vector.LocationCoordinateSystem = CoordinateSystem.AxisValues;
                    vector.LocationAxisValues.Angle = 0;
                    vector.LocationAxisValues.Amplitude = 0;
                    //Target is where the vector points to. All vectors are equal length in this example. 
                    vector.TargetAxisValues.Amplitude = 100;
                    vector.ArrowLineStyle.Width = 3;
                    vector.AllowUserInteraction = false;
                    vector.ArrowStyleBegin = ArrowStyle.None;
                    vector.ArrowLineStyle.Color = ChartTools.CalcGradient(transparentToDark, oldArrowColor,
                        (double)iVector / (double)(vectorCount - 1) * 100.0);

                    //Use thicker line for newest vector
                    if (iVector == vectorCount - 1)
                    {
                        vector.ArrowLineStyle.Width = 6;
                        vector.ArrowLineStyle.Color = Colors.White;
                    }

                    annotation.Add(vector);
                }

                // Start the timer.
                _timer.Interval = new TimeSpan(0, 0, 0, 0, iInterval);
                _timer.IsEnabled = true;

                // Enable/disable buttons.
                stop= true;
                start = false;
                
            }
        }

        public  void Start()
        {
            StartMethod(null);
        }

        public  void Stop()
        {
            StopMethod(null);
        }
        private void _dispatcherTimer_Tick(object sender, EventArgs e)
        {
            UpdateVectors();
        }

        /// <summary>
        /// Update vector data.
        /// </summary>
        private void UpdateVectors()
        {
            //Go through all vectors. Move target and location to previous vector, shift back in history
            IList<AnnotationPolar> listAnnotations = annotation;
            if (listAnnotations == null || listAnnotations.Count < 2)
                return;

            int vectorCount = listAnnotations.Count;

            for (int i = 0; i < vectorCount - 1; i++)
            {
                listAnnotations[i].TargetAxisValues.Angle = listAnnotations[i + 1].TargetAxisValues.Angle;
                listAnnotations[i].LocationAxisValues.Angle = listAnnotations[i + 1].LocationAxisValues.Angle;
            }

            //Randomize a new angle value for the newest vector 
            //Change it +/- 5 degrees from previous value
            double dNewAngle = listAnnotations[vectorCount - 2].TargetAxisValues.Angle + (_rand.NextDouble() - 0.5) * 10.0;
            listAnnotations[vectorCount - 1].TargetAxisValues.Angle = dNewAngle;
            listAnnotations[vectorCount - 1].LocationAxisValues.Angle = dNewAngle;
        }

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
            annotation.AddRange(_model.GetAnnotation(axes));
        }
        #endregion

    }
}
