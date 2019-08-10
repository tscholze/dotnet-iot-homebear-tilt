using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HomeBear.Tilt.ViewModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
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

        private VideoEncodingProperties previewProperties;

        /// <summary>
        /// Underlying face detection.
        /// </summary>
        private FaceDetectionEffect faceDetectionEffect;

        /// <summary>
        /// Underlying photo storage folder.
        /// </summary>
        private StorageFolder storageFolder;

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
            DataContext = viewModel = new MainPageViewModel();

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
            faceDetectionEffect = (FaceDetectionEffect) await mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);
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
        /// Initializes the storage async.
        /// </summary>
        /// <returns>Task.</returns>
        private async Task InitializeStorageAsync()
        {
            // Get lib folder async.
            storageFolder = (await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures)).SaveFolder;
        }

        /// <summary>
        /// Highlights the detected faces.
        /// </summary>
        /// <param name="faces">Detected faces.</param>
        private void HighlightDetectedFaces(IReadOnlyList<DetectedFace> faces)
        {
            // Remove all other highlights.
            FacesCanvas.Children.Clear();

            // Ensure at least one face is available.
            // The foreach will be triggered also if no
            // valid face with set properties has been found.
            if(faces.Count == 0 || previewProperties == null)
            {
                return;
            }

            // Get the rectangle of the preview control.
            var previewRect = TransformRectDimension(previewProperties, PreviewControl);

            // Get preview stream properties.
            double previewWidth = previewProperties.Width;
            double previewHeight = previewProperties.Height;

            // Draw new highlights.
            foreach (var face in faces)
            {
                // Get scaled position information of the face.
                var faceBox = face.FaceBox;
                var w = (faceBox.Width / previewWidth) * previewRect.Width;
                var h = (faceBox.Height / previewHeight) * previewRect.Height;
                var x = (faceBox.X / previewWidth) * previewRect.Width;
                var y = (faceBox.Y / previewHeight) * previewRect.Height;

                // Create rectangle from face box's properties.
                Rectangle faceRect = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Stroke = FACE_RECT_STROKE_COLOR,
                    StrokeThickness = FACE_RECT_STROKE_THICKNESS
                };

                // Align rectangle.
                Canvas.SetLeft(faceRect, x);
                Canvas.SetTop(faceRect, y);

                // Add rectangle to the canvas view.
                FacesCanvas.Children.Add(faceRect);
            }
        }

        /// <summary>
        /// Transforms values from given dimensions to actual dimensions.
        /// </summary>
        /// <param name="previewResolution">Preview / Stream resolution.</param>
        /// <param name="previewControl">Underlying preview ui control.</param>
        /// <returns></returns>
        private Rect TransformRectDimension(VideoEncodingProperties previewResolution, CaptureElement previewControl)
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
            // Get Storage folder
            await InitializeStorageAsync();

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
        private void SnapshotButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // TODO: Implement feature.
            System.Diagnostics.Debug.WriteLine("Flash! Take snapshot");
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
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="args">Event args.</param>
        private async void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => HighlightDetectedFaces(args.ResultFrame.DetectedFaces));
        }

        #endregion
    }
}
