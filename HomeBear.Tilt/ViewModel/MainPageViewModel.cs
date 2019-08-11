using GalaSoft.MvvmLight.Command;
using HomeBear.Tilt.Controller;
using HomeBear.Tilt.Views;
using System;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;

namespace HomeBear.Tilt.ViewModel
{
    /// <summary>
    /// View model class for the MainPage.
    /// </summary>
    class MainPageViewModel : BaseViewModel
    {
        #region Commands

        /// <summary>
        /// This command will update the selected control mode to manual.
        /// </summary>
        public ICommand SelectManualModeCommand { get; private set; }

        /// <summary>
        /// This command will update the selected control mode to camera
        /// based face detection.
        /// </summary>
        public ICommand SelectFaceDetectionModeCommand { get; private set; }

        /// <summary>
        /// This command will update the selected control mode to XBox
        /// controller.
        /// </summary>
        public ICommand SelectXBoxControllerModeCommand { get; private set; }

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

        private HomeBearTiltControlMode selectedMode;
        /// <summary>
        /// Selected control mode.
        /// </summary>
        public HomeBearTiltControlMode SelectedMode
        {
            get
            {
                return selectedMode;
            }

            set
            {
                selectedMode = value;
                OnSelectedModeChanged();
            }
        }

        private bool isManualControlEnabled;
        /// <summary>
        /// Determines if the manual control ui should
        /// be visible.
        /// </summary>
        public bool IsManualControlEnabled
        {
            get
            {
                return isManualControlEnabled;
            }

            set
            {
                isManualControlEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool isXBoxControllerControlEnabled;
        /// <summary>
        /// Determines if the XBox control ui should
        /// be visible.
        /// </summary>
        public bool IsXBoxControllerControlEnabled
        {
            get
            {
                return isXBoxControllerControlEnabled;
            }

            set
            {
                isXBoxControllerControlEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool isFaceDetectionControlEnabled;
        /// <summary>
        /// Determines if the face detection control ui should
        /// be visible.
        /// </summary>
        public bool IsFaceDetectionControlEnabled
        {
            get
            {
                return isFaceDetectionControlEnabled;
            }

            set
            {
                isFaceDetectionControlEnabled = value;
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

        /// <summary>
        /// Underlying photo storage folder.
        /// </summary>
        StorageFolder storageFolder;

        #endregion

        #region Constants

        /// <summary>
        /// Positive delta for panning or tilting in degrees.
        /// </summary>
        static readonly int POSTIVE_DEGREE_DELTA = 10;

        /// <summary>
        /// Negative detla for panning or tilting in degrees.
        /// </summary>
        static readonly int NEGATIVE_DEGREE_DETLA = -1 * POSTIVE_DEGREE_DELTA;

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
            SelectManualModeCommand = new RelayCommand(() =>
            {
                SelectedMode = HomeBearTiltControlMode.MANUAL;
            });

            SelectFaceDetectionModeCommand = new RelayCommand(() =>
            {
                SelectedMode = HomeBearTiltControlMode.FACE_DETECTION;
            });

            SelectXBoxControllerModeCommand = new RelayCommand(() =>
            {
                SelectedMode = HomeBearTiltControlMode.XBOX_CONTROLLER;
            });

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

        #region Public helper

        /// <summary>
        /// SAves given stream as jpg picture to the library.
        /// </summary>
        /// <param name="stream">In memory stream with picture information.</param>
        public async void SavePictureAsync(InMemoryRandomAccessStream stream)
        {
            // Try to store file to the picture library folder of the Pi.
            try
            {
                // Create file in memory.
                var file = await storageFolder.CreateFileAsync(CreatePhotoFilename(), CreationCollisionOption.GenerateUniqueName);
                using (var inputStream = stream)
                {
                    var decoder = await BitmapDecoder.CreateAsync(inputStream);
                    using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        // Write file to disk.
                        var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);
                        await encoder.FlushAsync();
                    }
                }
            }
            // Catch any exeption in debug log.
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception when taking a photo: " + ex.ToString());
            }
        }

        #endregion

        #region Private helper

        /// <summary>
        /// Inits view model async.
        /// </summary>
        private async void InitAsync()
        {
            // Set default values
            IsXBoxControllerControlEnabled = false;
            IsFaceDetectionControlEnabled = false;

            // Init TiltController async.
            await tiltController.InitAsync();

            // Get lib folder async.
            storageFolder = (await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures)).SaveFolder;
        }

        /// <summary>
        /// Generates a nearly unique filename for a
        /// jpeg image.
        /// </summary>
        /// <returns>Generated filename.</returns>
        private string CreatePhotoFilename()
        {
            var sanitizedAppName = SanitizedToLower(AppName);
            var sanitizedTime = DateTime.Now.ToString("yyyymmdd-hhmmss");

            return $"{sanitizedAppName}_{sanitizedTime}.jpg";
        }

        /// <summary>
        /// Sanitizes given string to use as file path.
        /// </summary>
        /// <param name="input">Underlying string.</param>
        /// <returns>Sanitized string.</returns>
        private string SanitizedToLower(string input)
        {
            return input.Replace(" ", "_")
                .Replace(".", "-")
                .Replace(":", "-")
                .Replace(",", "-")
                .ToLower();
        }

        #endregion

        #region Event handler

        /// <summary>
        /// Handels changes of the selectMode property.
        /// </summary>
        private void OnSelectedModeChanged()
        {
            // Update ui.
            switch (selectedMode)
            {
                case HomeBearTiltControlMode.MANUAL:
                    IsManualControlEnabled = true;
                    break;

                case HomeBearTiltControlMode.FACE_DETECTION:
                    IsManualControlEnabled = false;
                    break;

                case HomeBearTiltControlMode.XBOX_CONTROLLER:
                    IsManualControlEnabled = false;
                    break;
            }
        }

        /// <summary>
        /// Will be update the `CurrentTime` member with each tick.
        /// </summary>
        /// <param name="timer">Underyling timer</param>
        private void ClockTimer_Tick(ThreadPoolTimer timer)
        {
            CurrentTime = DateTime.Now.ToShortTimeString();
        }

        #endregion
    }
}
