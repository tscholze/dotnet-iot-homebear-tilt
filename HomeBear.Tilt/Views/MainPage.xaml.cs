using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using HomeBear.Tilt.ViewModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml;
using Windows.Gaming.Input;
using Windows.System;
using Windows.System.Threading;

namespace HomeBear.Tilt.Views
{
    /// <summary>
    /// Entry page of the app. 
    /// It provides an navigation point to all other functionality of HomeBear.
    /// 
    /// Links:
    ///     - Microsoft camera face detection sample.
    ///         https://github.com/microsoft/Windows-universal-samples/blob/master/Samples/CameraFaceDetection/cs/MainPage.xaml.cs
    ///     
    ///     - Microsoft Docs capture example.
    ///         https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/basic-photo-video-and-audio-capture-with-mediacapture
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Constants

        /// <summary>
        /// Gets the default face rect stroke color.
        /// </summary>
        private readonly SolidColorBrush FACE_RECT_STROKE_COLOR = new SolidColorBrush(Colors.Yellow);

        /// <summary>
        /// Gets the default face rect stroke thickness.
        /// </summary>
        private readonly int FACE_RECT_STROKE_THICKNESS = 3;

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

        #endregion

        #region Properties 

        /// <summary>
        /// Underlying view model of the view / page.
        /// </summary>
        private readonly MainPageViewModel viewModel;

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
        /// Determines if previewing is currently active.
        /// </summary>
        private bool isPreviewing = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor of the Main Page.
        /// Will initialize the data context.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            // Init and setup view model.
            DataContext = viewModel = new MainPageViewModel();
            viewModel.SelectedMode = HomeBearTiltControlMode.MANUAL;  

            // Setup event handler
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Gamepad.GamepadAdded += Gamepad_GamepadAdded;
            Gamepad.GamepadRemoved += Gamepad_GamepadRemoved;
            viewModel.FaceRectsDetected += ViewModel_FaceRectsDetected;

            // Pre setup ui
            PreviewingButton.IsEnabled = false;

            // Setup callbacks.
            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
        }
        #endregion

        #region Private helper

        /// <summary>
        /// Initializes the camera and previews.
        /// Will throw an exception if no camera access has been granted.
        /// </summary>
        /// <returns>True if init was successful.</returns>
        private async Task<bool> InitializeCameraAsync()
        {
            // Ensure that the media capture hasn't been init, yet.
            if (viewModel.MediaCapture != null)
            {
                return false;
            }

            // Get all camera devices.
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Ensure there has been exactly one camera found.
            if (devices.Count != 1)
            {
                return false;
            }

            // Create new media capture instance.
            viewModel.MediaCapture = new MediaCapture();

            // Setup callbacks.
            viewModel.MediaCapture.Failed += MediaCapture_Failed;

            // Init the actual capturing.
            var settings = new MediaCaptureInitializationSettings { VideoDeviceId = devices[0].Id };
            await viewModel.MediaCapture.InitializeAsync(settings);

            // Setup ui.
            PreviewControl.Source = viewModel.MediaCapture;
            PreviewingButton.IsEnabled = true;

            return true;
        }

        /// <summary>
        /// Highlights the detected faces.
        /// </summary>
        /// <param name="rects">Scaled rects of detected faces.</param>
        private void DrawFaceRects(IEnumerable<Rect> rects)
        {
            // Iterate over all scalled face rects.
            foreach (var rect in rects)
            {
                // Create rectangle from face box's properties.
                Rectangle faceRect = new Rectangle
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Stroke = FACE_RECT_STROKE_COLOR,
                    StrokeThickness = FACE_RECT_STROKE_THICKNESS
                };

                // Align rectangle.
                Canvas.SetLeft(faceRect, rect.X);
                Canvas.SetTop(faceRect, rect.Y);

                // Add rectangle to the canvas view.
                FacesCanvas.Children.Add(faceRect);
            }
        }

        /// <summary>
        /// Updated if required the position of the arm dependent on
        /// the given rects.
        /// 
        /// Caution:
        /// Only the first rect found will be used to update the 
        /// position.
        /// </summary>
        /// <param name="rects">List of rects.</param>
        private void UpdateArmPosition(IEnumerable<Rect> rects)
        {
            // Determine if a re-positing of the arm is required.
            var rect = rects.FirstOrDefault();
            var rectCenter = rect.X + (rect.Width / 2);
            var controlCenter = PreviewControl.Width / 2;
            var isMovementRequired = Math.Abs(rectCenter - controlCenter) > ARM_RELOCATION_CENTER_THRESHHOLD;

            // Ensure that a movement is required.
            if (isMovementRequired == false)
            {
                System.Diagnostics.Debug.WriteLine("OK");
                return;
            }

            // If the face is left of the preview center.
            if (rectCenter < controlCenter)
            {
                // move to the right.
                viewModel.PositivePanCommand.Execute(null);
            }
            // If the face is left of the preview center.
            else if (rectCenter > controlCenter)
            {
                // move to the left.
                viewModel.NegativePanCommand.Execute(null);
            }
        }

        /// <summary>
        /// Starts previewing and updates the Ui.
        /// </summary>
        private void StartPreviewing()
        {
            viewModel.StartPreviewing();
            isPreviewing = true;
            PreviewingButton.Content = "Stop";
        }

        /// <summary>
        /// Stops previewing and updates the Ui.
        /// </summary>
        private void StopPreviewing()
        {
            viewModel.StopPreviewing();
            FacesCanvas.Children.Clear();
            isPreviewing = false;
            PreviewingButton.Content = "Start";
        }

        #endregion

        #region Event handler

        /// <summary>
        /// Raised when page has been loaded.
        /// Will trigger async init operations.
        /// </summary>
        /// <param name="sender">Underlying control.</param>
        /// <param name="e">Event args.</param>
        private async void MainPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Init and start previewing.
            viewModel.IsFaceDetectionControlAvailable = await InitializeCameraAsync();

            if(viewModel.IsFaceDetectionControlAvailable)
            {
                StartPreviewing();
            }
        }

        /// <summary>
        /// Raised when page has been unloaded.
        /// Will clear controls.
        /// </summary>
        /// <param name="sender">Underlying control.</param>
        /// <param name="e">Event args.</param>
        private void MainPage_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Stop previewing if page gets unloaded.
            StopPreviewing();

            // Stop timers
            keyDownCooldownTimer.Cancel();
        }

        /// <summary>
        /// Raised when a Gamepad has been added.
        /// </summary>
        /// <param name="sender">Underlying sender.</param>
        /// <param name="e">Event args.</param>
        private void Gamepad_GamepadAdded(object sender, Gamepad e)
        {
            viewModel.IsXBoxControllerControlAvailable = true;
        }

        /// <summary>
        /// Raised when a Gamepad has been removed.
        /// </summary>
        /// <param name="sender">Underlying sender.</param>
        /// <param name="e">Event args.</param>
        private void Gamepad_GamepadRemoved(object sender, Gamepad e)
        {
            viewModel.IsXBoxControllerControlAvailable = false;
        }

        /// <summary>
        /// Raised for each key down of the user.
        /// Used to get GamePad events.
        /// </summary>
        /// <param name="sender">Underlying control.</param>
        /// <param name="args">Event args.</param>
        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            // Ensure that no cool down is active.
            if(isKeyDownCooldownActive)
            {
                return;
            }

            // Listen on pressed keys. 
            // Use only GamePad ones and trigger actions.
            switch(args.VirtualKey)
            {
                // Stick upwards.
                case VirtualKey.GamepadLeftThumbstickUp:
                    viewModel.PositiveTiltCommand.Execute(null);
                    break;

                // Stick downwards.
                case VirtualKey.GamepadLeftThumbstickDown:
                    viewModel.NegativeTiltCommand.Execute(null);
                    break;

                // Stick to the left.
                case VirtualKey.GamepadLeftThumbstickLeft:
                    viewModel.PositivePanCommand.Execute(null);
                    break;

                // Stick to the right.
                case VirtualKey.GamepadLeftThumbstickRight:
                    viewModel.PositivePanCommand.Execute(null);
                    break;
            }

            // Start cool down timer.
            keyDownCooldownTimer = ThreadPoolTimer.CreateTimer(CooldownTimer_Tick, KEYDOWN_COOLDOWN_SECONDS);
        }

        /// <summary>
        /// Raised in case on a user's button tap.
        /// </summary>
        /// <param name="sender">Underlying control.</param>
        /// <param name="e">Event args.</param>
        private void PreviewingButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Raise method dependent on the previewing state.
            if (isPreviewing)
            {
                StopPreviewing();
            }
            else
            {
                StartPreviewing();
            }
        }

        /// <summary>
        /// Raised in case on a user's button snapshot tap.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private async void SnapshotButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Call view model to save picture.
            _ = await viewModel.SavePictureAsync();
        }

        /// <summary>
        /// Raised in case of an failed attempt to start media capturing.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="errorEventArgs">Event args.</param>
        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine($"Something failed: {errorEventArgs.Message}");
            PreviewingButton.IsEnabled = false;
        }

        /// <summary>
        /// Raised in case of an faces got detected.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private void ViewModel_FaceRectsDetected(object sender, FaceRectsDetectedEvent e)
        {
            // Reset existing effects.
            FacesCanvas.Children.Clear();

            // Update ui.
            DrawFaceRects(e.faceRects);

            // Update robo arm position.
            UpdateArmPosition(e.faceRects);
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
