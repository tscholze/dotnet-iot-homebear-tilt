using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using HomeBear.Tilt.ViewModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

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

        #endregion

        #region Properties 

        /// <summary>
        /// Underlying view model of the view / page.
        /// </summary>
        private readonly MainPageViewModel viewModel;

        /// <summary>
        /// Underlying media capture instance for webcam.
        /// </summary>
        private MediaCapture mediaCapture;

        /// <summary>
        /// Underlying preview stream properties of the ui control.
        /// </summary>
        private VideoEncodingProperties previewProperties;

        /// <summary>
        /// Underlying face detection.
        /// </summary>
        private FaceDetectionEffect faceDetectionEffect;

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
        /// <returns>Task.</returns>
        private async Task InitializeCameraAsync()
        {
            // Ensure that the media capture hasn't been init, yet.
            if (mediaCapture != null)
            {
                return;
            }

            // Get all camera devices.
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Ensure there has been exactly one camera found.
            if (devices.Count != 1)
            {
                throw new InvalidOperationException("There are no or more than one camera attached to the Pi. Please attach only one.");
            }

            // Create new media capture instance.
            mediaCapture = new MediaCapture();

            // Setup callbacks.
            mediaCapture.Failed += MediaCapture_Failed;

            // Init the actual capturing.
            var settings = new MediaCaptureInitializationSettings { VideoDeviceId = devices[0].Id };
            await mediaCapture.InitializeAsync(settings);

            // Setup face detection
            var definition = new FaceDetectionEffectDefinition
            {
                SynchronousDetectionEnabled = false,
                DetectionMode = FaceDetectionMode.HighPerformance
            };

            // Get effect.
            faceDetectionEffect = (FaceDetectionEffect)await mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);
            faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(33);

            // Setup callbacks.
            faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;

            // Get properties.
            previewProperties = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

            // Setup ui.
            PreviewControl.Source = mediaCapture;
            PreviewingButton.IsEnabled = true;
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
        /// Gets to the Ui control dimension scaled rects for given faces.
        /// </summary>
        /// <param name="faces">List of faces to scale.</param>
        /// <returns>Scaled list of rects.</returns>
        private IEnumerable<Rect> ScaledFaceRects(IReadOnlyList<DetectedFace> faces)
        {
            // Get the rectangle of the preview control.
            var previewRect = ScaleStreamToPreviewDimensions(previewProperties, PreviewControl);

            // Get preview stream properties.
            double previewWidth = previewProperties.Width;
            double previewHeight = previewProperties.Height;

            // Map FaceBox to a scaled rect.
            return faces.Select(face =>
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
        }

        /// <summary>
        /// Transforms values from given dimensions to actual dimensions.
        /// </summary>
        /// <param name="previewResolution">Preview / Stream resolution.</param>
        /// <param name="previewControl">Underlying preview ui control.</param>
        /// <returns></returns>
        private Rect ScaleStreamToPreviewDimensions(VideoEncodingProperties previewResolution, CaptureElement previewControl)
        {
            // Calculate scale by width.
            // This property is hard set by the xaml file.
            var scale = previewControl.ActualWidth / previewResolution.Width;

            // Calculate scaled properties.
            var resultingWidth = previewControl.ActualWidth;
            var resultingHeight = previewResolution.Height * scale;
            var resultingY = (previewControl.ActualHeight - resultingHeight) / 2.0;

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
        private async void StartPreviewing()
        {
            await mediaCapture.StartPreviewAsync();
            faceDetectionEffect.Enabled = true;
            isPreviewing = true;
            PreviewingButton.Content = "Stop";
        }

        /// <summary>
        /// Stops previewing and updates the Ui.
        /// </summary>
        private async void StopPreviewing()
        {
            await mediaCapture.StopPreviewAsync();
            faceDetectionEffect.Enabled = false;
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
            await InitializeCameraAsync();
            StartPreviewing();
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
            // Create stream.
            var stream = new InMemoryRandomAccessStream();

            // Fill stream with captured photo information.
            await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

            // Store picture.
            viewModel.SavePictureAsync(stream);
        }

        /// <summary>
        /// Raised in case of an failed attempt to start media capturing.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="errorEventArgs">Event args.</param>
        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            System.Diagnostics.Debug.WriteLine($"Something failed: {errorEventArgs.Message}");
            PreviewingButton.IsEnabled = false;
        }

        /// <summary>
        /// Raised in case of an recognized face.
        /// Will trigger an ui and arm updated if required.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="args">Event args.</param>
        private async void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            // Get back to the main thread to access and updated the ui.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Reset existing effects.
                FacesCanvas.Children.Clear();

                // Get faces from arguments.
                var faces = args.ResultFrame.DetectedFaces;

                // Ensure at least one face has been detected.
                if (faces.Count == 0)
                {
                    return;
                }

                // Transform found faces to scaled face rects.
                var scaledFaceRects = ScaledFaceRects(faces);

                // Update ui.
                DrawFaceRects(scaledFaceRects);

                // Update robo arm position.
                UpdateArmPosition(scaledFaceRects);
            });
        }

        #endregion
    }
}
