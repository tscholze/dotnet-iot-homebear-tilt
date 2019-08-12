using GalaSoft.MvvmLight.Command;
using HomeBear.Tilt.Controller;
using HomeBear.Tilt.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
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

        private MediaCapture mediaCapture;
        /// <summary>
        /// Underlying media capture instance for webcam.
        /// </summary>
        public MediaCapture MediaCapture
        {
            get
            {
                return mediaCapture;
            }

            set
            {
                mediaCapture = value;
                OnMediaCaptureSet();
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

        private bool isManualControlAvailable;
        /// <summary>
        /// Determines if the manual control ui should
        /// be visible.
        /// </summary>
        public bool IsManualControlAvailable
        {
            get
            {
                return isManualControlAvailable;
            }

            set
            {
                isManualControlAvailable = value;
                OnPropertyChanged();
            }
        }

        private bool isXBoxControllerControlAvailable;
        /// <summary>
        /// Determines if the XBox control ui should
        /// be visible.
        /// </summary>
        public bool IsXBoxControllerControlAvailable
        {
            get
            {
                return isXBoxControllerControlAvailable;
            }

            set
            {
                isXBoxControllerControlAvailable = value;
                OnPropertyChanged();
            }
        }

        private bool isFaceDetectionControlAvailable;
        /// <summary>
        /// Determines if the face detection control ui should
        /// be visible.
        /// </summary>
        public bool IsFaceDetectionControlAvailable
        {
            get
            {
                return isFaceDetectionControlAvailable;
            }

            set
            {
                isFaceDetectionControlAvailable = value;
                OnPropertyChanged();
            }
        }

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

        #region Public events

        /// <summary>
        /// Event that will be called if faces has been detected.
        /// </summary>
        public event EventHandler<FaceRectsDetectedEvent> FaceRectsDetected;

        #endregion

        #region Private properties

        /// <summary>
        /// Underlying tilt controller.
        /// </summary>
        private readonly PanTiltHAT tiltController = new PanTiltHAT();

        /// <summary>
        /// Underlying photo storage folder.
        /// </summary>
        private StorageFolder storageFolder;

        /// <summary>
        /// Underlying preview stream properties of the ui control.
        /// </summary>
        private VideoEncodingProperties previewProperties;

        /// <summary>
        /// Underlying face detection.
        /// </summary>
        private FaceDetectionEffect faceDetectionEffect;

        #endregion

        #region Private constants

        /// <summary>
        /// Positive delta for panning or tilting in degrees.
        /// </summary>
        private static readonly int POSTIVE_DEGREE_DELTA = 10;

        /// <summary>
        /// Negative detla for panning or tilting in degrees.
        /// </summary>
        private static readonly int NEGATIVE_DEGREE_DETLA = -1 * POSTIVE_DEGREE_DELTA;

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

        public async void StartPreviewing()
        {
            await mediaCapture.StartPreviewAsync();
            IsFaceDetectionControlAvailable = true;
            faceDetectionEffect.Enabled = true;
        }

        public async void StopPreviewing()
        {
            await mediaCapture.StopPreviewAsync();
            IsFaceDetectionControlAvailable = false;
            faceDetectionEffect.Enabled = false;
        }

        /// <summary>
        /// Saves given stream as jpg picture to the library.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task<bool> SavePictureAsync()
        {
            // Create stream.
            var stream = new InMemoryRandomAccessStream();

            // Fill stream with captured photo information.
            await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

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

                // Return success.
                return true;
            }
            // Catch any exeption in debug log.
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception when taking a photo: " + ex.ToString());

                // Return failure.
                return false;
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
            IsXBoxControllerControlAvailable = false;
            IsFaceDetectionControlAvailable = false;

            // Init TiltController async.
            await tiltController.InitAsync();

            // Get lib folder async.
            storageFolder = (await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures)).SaveFolder;
        }

        /// <summary>
        /// Transforms values from given dimensions to actual dimensions.
        /// </summary>
        /// <param name="previewResolution">Preview / Stream resolution.</param>
        /// <param name="width">Width of the target parent rect.</param>
        /// <param name="height">Height of the target parent rect.</param>
        /// <returns>Scaled rect. </returns>
        private Rect ScaleStreamToPreviewDimensions(VideoEncodingProperties previewResolution, float width, float height)
        {
            // Calculate scale by width.
            // This property is hard set by the xaml file.
            var scale = width / previewResolution.Width;

            // Calculate scaled properties.
            var resultingWidth = width;
            var resultingHeight = previewResolution.Height * scale;
            var resultingY = (height - resultingHeight) / 2.0;

            // Create rect from calculated properties.
            var result = new Rect
            {
                Height = resultingHeight,
                Width = resultingWidth,
                Y = resultingY
            };

            return result;
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
        /// Handles changes of the mediaCapture property.
        /// </summary>
        private async void OnMediaCaptureSet()
        {
            // Updated preview properties from mediaCapture.
            previewProperties = mediaCapture
                .VideoDeviceController
                .GetMediaStreamProperties(MediaStreamType.VideoPreview)
                as VideoEncodingProperties;

            // Update effect
            // Setup face detection
            var definition = new FaceDetectionEffectDefinition
            {
                SynchronousDetectionEnabled = false,
                DetectionMode = FaceDetectionMode.HighPerformance
            };

            faceDetectionEffect = (FaceDetectionEffect)await mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);
            faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(33);
            faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;
        }

        /// <summary>
        /// Raised in case of an recognized face.
        /// Will trigger an ui and arm updated if required.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="args">Event args.</param>
        private void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            // Get faces from arguments.
            var faces = args.ResultFrame.DetectedFaces;

            // Ensure at least one face has been detected.
            if (faces.Count == 0)
            {
                return;
            }

            float width = 3;
            float height =4;
            // Get the rectangle of the preview control.
            var previewRect = ScaleStreamToPreviewDimensions(previewProperties, width, height);

            // Get preview stream properties.
            double previewWidth = previewProperties.Width;
            double previewHeight = previewProperties.Height;

            // Map FaceBox to a scaled rect.
            var faceRects = faces.Select(face =>
            {
                // Get scaled position information of the face.
                var faceBox = face.FaceBox;
                var resultingWidth = (faceBox.Width / previewWidth) * previewRect.Width;
                var resultingHeight = (faceBox.Height / previewHeight) * previewRect.Height;
                var resultingX = (faceBox.X / previewWidth) * previewRect.Width;
                var resultingY = (faceBox.Y / previewHeight) * previewRect.Height;

                // Init new rect.
                var rect = new Rect(resultingX, resultingY, resultingWidth, resultingHeight);
                return rect;
            });

            // Call event.
            FaceRectsDetected(this, new FaceRectsDetectedEvent(faceRects));
        }

        /// <summary>
        /// Handels changes of the selectMode property.
        /// </summary>
        private void OnSelectedModeChanged()
        {
            // Update ui.
            switch (selectedMode)
            {
                case HomeBearTiltControlMode.MANUAL:
                    IsManualControlAvailable = true;
                    break;

                case HomeBearTiltControlMode.FACE_DETECTION:
                    IsManualControlAvailable = false;
                    break;

                case HomeBearTiltControlMode.XBOX_CONTROLLER:
                    IsManualControlAvailable = false;
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
