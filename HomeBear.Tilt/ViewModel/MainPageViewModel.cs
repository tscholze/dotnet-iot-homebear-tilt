using GalaSoft.MvvmLight.Command;
using HomeBear.Tilt.Controller;
using System;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.System.Threading;

namespace HomeBear.Tilt.ViewModel
{
    class MainPageViewModel : BaseViewModel
    {
        #region Public properties 

        private string currentTime;
        /// <summary>
        /// Gets the current time.
        /// </summary>
        public string CurrentTime
        {
            get
            {
                return currentTime;
            }

            set
            {
                currentTime = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the personal, formatted greeting.
        /// </summary>
        public string Greeting
        {
            get
            {
                return "Hey ho maker friends!";
            }
        }

        /// <summary>
        /// Gets the app name.
        /// </summary>
        public string AppName
        {
            get
            {
                return Package.Current.DisplayName;
            }
        }

        /// <summary>
        /// Gets the app author's url.
        /// </summary>
        public string AppAuthorUrl
        {
            get
            {
                return "tscholze.github.io";
            }
        }

        /// <summary>
        /// Gets the current formatted app version.
        /// </summary>
        public string AppVersion
        {
            get
            {
                return string.Format("Version: {0}.{1}",
                    Package.Current.Id.Version.Major,
                    Package.Current.Id.Version.Minor);
            }
        }

        /// <summary>
        /// This command will trigger a panning in the positive direction.
        /// </summary>
        public ICommand PositivePanCommand { get; private set; }

        /// <summary>
        /// This command will trigger a panning in the negative direction.
        /// </summary>
        public ICommand NegativePanCommand { get; private set; }

        /// <summary>
        /// This command will trigger a tilting in the negative direction.
        /// </summary>
        public ICommand PositiveTiltCommand { get; private set; }

        /// <summary>
        /// This command will trigger a tilting in the negative direction.
        /// </summary>
        public ICommand NegativeTiltCommand { get; private set; }

        #endregion

        #region Private properties

        /// <summary>
        /// Positive delta for panning or tilting in degrees.
        /// </summary>
        static readonly int POSTIVE_DEGREE_DELTA = 10;

        /// <summary>
        /// Negative detla for panning or tilting in degrees.
        /// </summary>
        static readonly int NEGATIVE_DEGREE_DETLA = -1 * POSTIVE_DEGREE_DELTA;

        /// <summary>
        /// Underlying tilt controller.
        /// </summary>
        readonly PanTiltHAT tiltController = new PanTiltHAT();

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor of the MainPageViewModel.
        /// Will setup timers and commands.
        /// </summary>
        public MainPageViewModel()
        {
            // Setup tilt controller.
            InitAsync();

            // Setup timer.
            ThreadPoolTimer.CreatePeriodicTimer
                (ClockTimer_Tick,
                TimeSpan.FromSeconds(1)
           );

            // Setup the commands.
            PositivePanCommand = new RelayCommand(() =>
            {
                tiltController.Pan(tiltController.PanDegrees() + POSTIVE_DEGREE_DELTA);
            });

            NegativePanCommand = new RelayCommand(() =>
            {
                tiltController.Pan(tiltController.PanDegrees() + NEGATIVE_DEGREE_DETLA);
            });

            PositiveTiltCommand = new RelayCommand(() =>
            {
                tiltController.Tilt(tiltController.TiltDegrees() + POSTIVE_DEGREE_DELTA);
            });

            NegativeTiltCommand = new RelayCommand(() =>
            {
                tiltController.Tilt(tiltController.TiltDegrees() + NEGATIVE_DEGREE_DETLA);
            });
        }

        #endregion

        #region Private helper

        /// <summary>
        /// Will be update the `CurrentTime` member with each tick.
        /// </summary>
        /// <param name="timer">Underyling timer</param>
        private void ClockTimer_Tick(ThreadPoolTimer timer)
        {
            CurrentTime = DateTime.Now.ToShortTimeString();
        }

        /// <summary>
        /// Only debug foo!
        /// </summary>
        /// <summary>
        /// Only debug foo!
        /// </summary>
        private async void InitAsync()
        {
            // Init TiltController async.
            await tiltController.InitAsync();
        }

        #endregion
    }
}
