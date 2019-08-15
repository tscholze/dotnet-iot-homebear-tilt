using System;
using System.Collections.Generic;
using System.Diagnostics;
using HomeBear.Tilt.ViewModel;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml;
using Windows.Gaming.Input;
using HomeBear.Tilt.Utils;

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

        #endregion

        #region Properties 

        /// <summary>
        /// Underlying view model of the view / page.
        /// </summary>
        private readonly MainPageViewModel viewModel;

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

            // Setup global event handler.
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Gamepad.GamepadAdded += Gamepad_GamepadAdded;
            Gamepad.GamepadRemoved += Gamepad_GamepadRemoved;

            // Setup page event handler.
            Loaded += MainPage_Loaded;

            // Setup camera event handler.
            viewModel.CameraInitSucceeded += ViewModel_CameraInitSucceeded;
            viewModel.CameraInitFailed += ViewModel_CameraInitFailed;

            // Setup saving snapshot event handler.
            viewModel.SavingSnapshotSucceeded += ViewModel_SavingSnapshotSucceeded;
            viewModel.SavingSnapshotFailed += ViewModel_SavingSnapshotFailed;

            // Setup preview status changed event handler.
            viewModel.PreviewingStarted += ViewModel_PreviewingStarted;
            viewModel.PreviewingEnded += ViewModel_PreviewingEnded;

            // Setup face detected event handler.
            viewModel.FaceRectsDetected += ViewModel_FaceRectsDetected;
        }

        #endregion

        #region Private helper

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

        #endregion

        #region Event handler

        /// <summary>
        /// Raised when page has been loaded.
        /// Will trigger async init operations.
        /// </summary>
        /// <param name="sender">Underlying control.</param>
        /// <param name="e">Event args.</param>
        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Default state.
            viewModel.SelectedMode = HomeBearTiltControlMode.MANUAL;

            // Init view model ui related operations.
            await viewModel.InitializeCameraAsync(PreviewControl.RenderSize);

            // Ensure everything required is available.
            if (!viewModel.IsFaceDetectionControlAvailable)
            {
                return;
            }

            // Evaluate why this does not work with Binding xaml.
            PreviewControl.Source = viewModel.MediaCapture;
            viewModel.StartPreviewing();
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
            viewModel.OnKeyDown(args);
        }

        /// <summary>
        /// Raised in case of saving the snapshot was successful.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private void ViewModel_SavingSnapshotSucceeded(object sender, EventArgs e)
        {
            Debug.WriteLine("Snapshot saving succeeded");
        }

        /// <summary>
        /// Raised in case of saving the snapshot failed.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private void ViewModel_SavingSnapshotFailed(object sender, EventArgs e)
        {
            Debug.WriteLine("Snapshot saving failed");
        }

        /// <summary>
        /// Raised in case of initing the camera was successful.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private void ViewModel_CameraInitSucceeded(object sender, EventArgs e)
        {
            Debug.WriteLine("Camera init succeeded");
        }

        /// <summary>
        /// Raised in case of initing the camera failed.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private void ViewModel_CameraInitFailed(object sender, EventArgs e)
        {
            Debug.WriteLine("Camera init failed");
        }

        /// <summary>
        /// Raised in case of an failed attempt to start media capturing.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="errorEventArgs">Event args.</param>
        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine($"Something failed: {errorEventArgs.Message}");
        }

        /// <summary>
        /// Raised in case of the previewing has started.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private void ViewModel_PreviewingStarted(object sender, EventArgs e)
        {
            Debug.WriteLine("Previewing started");
        }

        /// <summary>
        /// Raised in case of the previewing has ended
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private void ViewModel_PreviewingEnded(object sender, EventArgs e)
        {
            FacesCanvas.Children.Clear();
        }

        /// <summary>
        /// Raised in case of an faces got detected.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="e">Event args.</param>
        private void ViewModel_FaceRectsDetected(object sender, FaceRectsDetectedEventArgs e)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
              {
                // Reset existing effects.
                FacesCanvas.Children.Clear();

                // Update ui.
                DrawFaceRects(e.faceRects);
              });
        }

        #endregion
    }
}
