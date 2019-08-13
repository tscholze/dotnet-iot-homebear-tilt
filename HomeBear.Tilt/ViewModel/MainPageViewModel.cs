using GalaSoft.MvvmLight.Command;
using HomeBear.Tilt.Controller;
using HomeBear.Tilt.Utils;
using HomeBear.Tilt.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;

namespace HomeBear.Tilt.ViewModel
{
    /// <summary>
    /// View model class for the MainPage.
    /// </summary>
    class MainPageViewModel : BaseViewModel
    {
        #region Public events

        /// <summary>
        /// Event that will be called if faces has been detected.
        /// </summary>
        public event EventHandler<FaceRectsDetectedEventArgs> FaceRectsDetected;

        /// <summary>
        /// Event that will be called if the camera init was successful.
        /// </summary>
        public event EventHandler<MessageEventArgs> CameraInitSucceeded;

        /// <summary>
        /// Event that will be called if the camera init failed.
        /// </summary>
        public event EventHandler<MessageEventArgs> CameraInitFailed;

        /// <summary>
        /// Event that will be called if saving a camera snapshot was successful.
        /// </summary>
        public event EventHandler<MessageEventArgs> SavingSnapshotSucceeded;

        /// <summary>
        /// Event that will be called if saving a camera snapshot failed.
        /// </summary>
        public event EventHandler<MessageEventArgs> SavingSnapshotFailed;

        /// <summary>
        /// Event that will be called if camera previewing has started.
        /// </summary>
        public event EventHandler<MessageEventArgs> PreviewingStarted;

        /// <summary>
        /// Event that will be called if camera previewing has ended.
        /// </summary>
        public event EventHandler<MessageEventArgs> PreviewingEnded;

        #endregion

        #region Commands

        /// <summary>
        /// This command will toggle the state of the previewing.
        /// (stop -> start, start -> stop).
        /// </summary>
        public ICommand TogglePreviewingStateCommand { get; private set; }

        /// <summary>
        /// This command will trigger taking and saving a snapshot.
        /// </summary>
        public ICommand SaveSnapshotCommand { get; private set; }

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

        #region Private constants

        /// <summary>
        /// Positive delta for panning or tilting in degrees.
        /// </summary>
        private static readonly int POSTIVE_DEGREE_DELTA = 10;

        /// <summary>
        /// Negative detla for panning or tilting in degrees.
        /// </summary>
        private static readonly int NEGATIVE_DEGREE_DETLA = -1 * POSTIVE_DEGREE_DELTA;

        /// <summary>
        /// Threshold which determines the maximum difference between
        /// the center of the preview and the center of the face after
        /// a movement is required.
        /// </summary>
        private readonly float ARM_RELOCATION_CENTER_THRESHHOLD = 25;

        /// <summary>
        /// Time out after another keydown will be accepted.
        /// </summary>
        private readonly TimeSpan KEYDOWN_COOLDOWN_SECONDS = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Background color of a selected mode button.
        /// </summary>
        private readonly SolidColorBrush SELECTED_MODE_BUTTON_BACKGROUND = new SolidColorBrush(Colors.DarkGray);

        /// <summary>
        /// Background color if a non selected mode button.
        /// </summary>
        private readonly SolidColorBrush UNSELECTED_MODE_BUTTON_BACKGROUND = new SolidColorBrush(Colors.Black);

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

            private set
            {
                mediaCapture = value;
                OnPropertyChanged();
            }
        }


        /// <summary>
        /// Determines if previewing is currently active.
        /// </summary>
        private bool isPreviewing = false;
        public bool IsPreviewing
        {
            get
            {
                return isPreviewing;
            }

            private set
            {
                isPreviewing = value;
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

        private string takeSnapshotButtonContent;
        /// <summary>
        /// Text of the "take snapshot" button.
        /// </summary>
        public string TakeSnapshotButtonContent
        {
            get
            {
                return takeSnapshotButtonContent;
            }

            private set
            {
                takeSnapshotButtonContent = value;
                OnPropertyChanged();
            }
        }

        private SolidColorBrush faceDetectionControlButtonBackground;
        /// <summary>
        /// Background color of the face detection selection mode button.
        /// </summary>
        public SolidColorBrush FaceDetectionControlButtonBackground
        {
            get
            {
                return faceDetectionControlButtonBackground;
            }

            private set
            {
                faceDetectionControlButtonBackground = value;
                OnPropertyChanged();
            }
        }

        private SolidColorBrush xBoxControllerControlBackground;
        /// <summary>
        /// Background color of the XBox controller selection mode button.
        /// </summary>
        public SolidColorBrush XBoxControllerControlButtonBackground
        {
            get
            {
                return xBoxControllerControlBackground;
            }

            private set
            {
                xBoxControllerControlBackground = value;
                OnPropertyChanged();
            }
        }

        private SolidColorBrush manualControlButtonBackground;
        /// <summary>
        /// Background color of the manual selection mode button.
        /// </summary>
        public SolidColorBrush ManualControlButtonBackground
        {
            get
            {
                return manualControlButtonBackground;
            }

            private set
            {
                manualControlButtonBackground = value;
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
        private readonly PanTiltHAT tiltController = new PanTiltHAT();

        /// <summary>
        /// Underlying photo storage folder.
        /// </summary>
        private StorageFolder storageFolder;

        /// <summary>
        /// Underlying preview stream properties of the ui control.
        /// </summary>
        private VideoEncodingProperties previewProperties = new VideoEncodingProperties();

        /// <summary>
        /// Underlying face detection.
        /// </summary>
        private FaceDetectionEffect faceDetectionEffect;

        /// <summary>
        /// Keydown time out timer.
        /// </summary>
        private ThreadPoolTimer keyDownCooldownTimer;

        /// <summary>
        /// Determines if the key down event handler has 
        /// a cool down set.
        /// </summary>
        private bool isKeyDownCooldownActive = false;

        /// <summary>
        /// Contains the size of the ui camera preview control.
        /// </summary>
        private Size previewControlSize;

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

            // Setup default values.
            TakeSnapshotButtonContent = "N/A";

            // Setup the commands.
            TogglePreviewingStateCommand = new RelayCommand(() =>
            {
                TooglePreviewingState();
            });

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
        /// Initializes the camera.
        /// Will raise `CameraInit*` events. 
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeCameraAsync(Size previewControlSize)
        {
            // Set ui-related values.
            this.previewControlSize = previewControlSize;

            // Ensure that the media capture hasn't been init, yet.
            if (MediaCapture != null)
            {
                return;
            }

            // Get all camera devices.
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Ensure there has been exactly one camera found.
            if (devices.Count != 1)
            {
                IsFaceDetectionControlAvailable = false;
                CameraInitFailed(this, new MessageEventArgs("No or more than one camera found. No face detection available."));
            }

            // Create new media capture instance.
            MediaCapture = new MediaCapture();

            // Setup callbacks.
            MediaCapture.Failed += MediaCapture_Failed;

            // Init the actual capturing.
            var settings = new MediaCaptureInitializationSettings { VideoDeviceId = devices[0].Id };
            await MediaCapture.InitializeAsync(settings);

            // Updated preview properties from mediaCapture.
            previewProperties = MediaCapture
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

            faceDetectionEffect = (FaceDetectionEffect)await MediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);
            faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(33);
            faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;

            // Operation was successful.
            IsFaceDetectionControlAvailable = true;
            CameraInitSucceeded(this, new MessageEventArgs("Face detection is now available."));
        }

        /// <summary>
        /// Will start the previewing and updates the ui.
        /// </summary>
        public async void StartPreviewing()
        {
            await MediaCapture.StartPreviewAsync();
            isPreviewing = true;
            IsFaceDetectionControlAvailable = true;
            faceDetectionEffect.Enabled = true;
            TakeSnapshotButtonContent = "Stop";

            PreviewingStarted(this, new MessageEventArgs("Previewing has started."));
        }

        /// <summary>
        /// Will stops the previewing and updates the ui.
        /// </summary>
        public async void StopPreviewing()
        {
            await MediaCapture.StopPreviewAsync();
            IsPreviewing = false;
            IsFaceDetectionControlAvailable = false;
            faceDetectionEffect.Enabled = false;
            TakeSnapshotButtonContent = "Start";

            PreviewingEnded(this, new MessageEventArgs("Previewing has ended."));
        }

        /// <summary>
        /// Saves given stream as jpg picture to the library.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task SavePictureAsync()
        {
            // Ensure operation is valid
            if (!IsFaceDetectionControlAvailable)
            {
                return;
            }

            // Create stream.
            var stream = new InMemoryRandomAccessStream();

            // Fill stream with captured photo information.
            await MediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

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

                SavingSnapshotSucceeded(this, new MessageEventArgs("Snapshot has been successfully saved to the library."));
            }
            // Catch any exeption in debug log.
            catch (Exception ex)
            {
                SavingSnapshotFailed(this, new MessageEventArgs($"Snapshot saving failed: {ex.ToString()}"));
            }
        }

        #endregion

        #region Private helper

        /// <summary>
        /// Inits asnyc properties of the view model.
        /// </summary>
        private async void InitAsync()
        {
            // Init TiltController async.
            await tiltController.InitAsync();

            // Get lib folder async.
            storageFolder = (await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures)).SaveFolder;
        }

        /// <summary>
        /// Toggles the previewing state.
        /// </summary>
        private void TooglePreviewingState()
        {
            if (IsPreviewing)
            {
                StopPreviewing();
            }
            else
            {
                StartPreviewing();
            }
        }

        /// <summary>
        /// Transforms values from given dimensions to actual dimensions.
        /// </summary>
        /// <param name="previewResolution">Preview / Stream resolution.</param>
        /// <param name="width">Width of the target parent rect.</param>
        /// <param name="height">Height of the target parent rect.</param>
        /// <returns>Scaled rect. </returns>
        private Rect ScaleStreamToPreviewDimensions(VideoEncodingProperties previewResolution, double width, double height)
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
        /// Updated if required the position of the arm dependent on
        /// the given rects.
        /// 
        /// Caution:
        ///     - Only the first rect found will be used to update the 
        ///         position.
        ///     - It assumes that the width is specified in the ui and
        ///         the height is dynamic.
        /// </summary>
        /// <param name="rects">List of rects.</param>
        private void UpdateArmPosition(IEnumerable<Rect> rects)
        {
            // Determine if a re-positing of the arm is required.
            var rect = rects.FirstOrDefault();
            var rectCenter = rect.X + (rect.Width / 2);
            var isMovementRequired = Math.Abs(rectCenter - previewControlSize.Width) > ARM_RELOCATION_CENTER_THRESHHOLD;

            // Ensure that a movement is required.
            if (isMovementRequired == false)
            {
                Debug.WriteLine("OK");
                return;
            }

            // If the face is left of the preview center.
            if (rectCenter < previewControlSize.Width)
            {
                // move to the right.
                NegativePanCommand.Execute(null);
            }
            // If the face is left of the preview center.
            else if (rectCenter > previewControlSize.Width)
            {
                // move to the left.
                PositivePanCommand.Execute(null);
            }
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
        /// Should be called externally to listen on key
        /// down events.
        /// 
        /// Caution:
        ///     - It only listens on Gamepad keys.
        /// </summary>
        /// <param name="args"></param>
        public void OnKeyDown(KeyEventArgs args)
        {
            // Ensure that no cool down is active.
            if (isKeyDownCooldownActive || !isXBoxControllerControlAvailable)
            {
                return;
            }

            // Listen on pressed keys. 
            // Use only GamePad ones and trigger actions.
            switch (args.VirtualKey)
            {
                // Stick upwards.
                case VirtualKey.GamepadLeftThumbstickUp:
                    PositiveTiltCommand.Execute(null);
                    break;

                // Stick downwards.
                case VirtualKey.GamepadLeftThumbstickDown:
                    NegativeTiltCommand.Execute(null);
                    break;

                // Stick to the left.
                case VirtualKey.GamepadLeftThumbstickLeft:
                    PositivePanCommand.Execute(null);
                    break;

                // Stick to the right.
                case VirtualKey.GamepadLeftThumbstickRight:
                    NegativePanCommand.Execute(null);
                    break;
            }

            // Start cool down timer.
            keyDownCooldownTimer = ThreadPoolTimer.CreateTimer(CooldownTimer_Tick, KEYDOWN_COOLDOWN_SECONDS);
        }

        /// <summary>
        /// Raised in case of the init of the camera failed.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="errorEventArgs">Event args.</param>
        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            CameraInitFailed(this, new MessageEventArgs($"Camera initializion failed. {errorEventArgs.Message}."));
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
            if (faces.Count == 0 || !IsFaceDetectionControlAvailable)
            {
                FaceRectsDetected(this, new FaceRectsDetectedEventArgs(new List<Rect>()));
                return;
            }

            // Get the rectangle of the preview control.
            var previewRect = ScaleStreamToPreviewDimensions(previewProperties, previewControlSize.Width, previewControlSize.Height);

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

            // Update arm
            UpdateArmPosition(faceRects);

            // Call event.
            FaceRectsDetected(this, new FaceRectsDetectedEventArgs(faceRects));
        }

        /// <summary>
        /// Handels changes of the selectMode property.
        /// </summary>
        private void OnSelectedModeChanged()
        {
            // Reset colors.
            ManualControlButtonBackground = UNSELECTED_MODE_BUTTON_BACKGROUND;
            FaceDetectionControlButtonBackground = UNSELECTED_MODE_BUTTON_BACKGROUND;
            XBoxControllerControlButtonBackground = UNSELECTED_MODE_BUTTON_BACKGROUND;

            // Update ui.
            switch (selectedMode)
            {
                case HomeBearTiltControlMode.MANUAL:
                    IsManualControlAvailable = true;
                    ManualControlButtonBackground = SELECTED_MODE_BUTTON_BACKGROUND;
                    break;

                case HomeBearTiltControlMode.FACE_DETECTION:
                    IsManualControlAvailable = false;
                    FaceDetectionControlButtonBackground = SELECTED_MODE_BUTTON_BACKGROUND;
                    break;

                case HomeBearTiltControlMode.XBOX_CONTROLLER:
                    IsManualControlAvailable = false;
                    XBoxControllerControlButtonBackground = SELECTED_MODE_BUTTON_BACKGROUND;
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

        /// <summary>
        /// Raised in case of the cooldown timer ticked / finished.
        /// It will disable the cool down flag.
        /// </summary>
        /// <param name="timer"></param>
        private void CooldownTimer_Tick(ThreadPoolTimer timer)
        {
            isKeyDownCooldownActive = false;
        }

        #endregion
    }
}
