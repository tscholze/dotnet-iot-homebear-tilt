using HomeBear.Tilt.Controller;
using System;
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

        #endregion

        #region Private properties

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
            // Setup timer.
            ThreadPoolTimer.CreatePeriodicTimer
                (ClockTimer_Tick,
                TimeSpan.FromSeconds(1)
           );

            FooAsync();
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
        private async void FooAsync()
        {
            // Init
            await tiltController.InitAsync();

            // Pan
            tiltController.Tilt(0);

            // Read pan value, should match the value which's set before.
            ThreadPoolTimer.CreateTimer((ThreadPoolTimer threadPoolTimer)  => { tiltController.TiltDegrees(); }, TimeSpan.FromSeconds(5));
        }

        #endregion
    }
}
