using System;
using System.Threading.Tasks;
using HomeBear.Tilt.ViewModel;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.UI.Xaml.Controls;

namespace HomeBear.Tilt.Views
{
    /// <summary>
    /// Entry page of the app. 
    /// It provides an navigation point to all other functionality of HomeBear.
    /// </summary>
    public sealed partial class MainPage : Page
    {
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

            // Try to init the actual capturing.
            try
            {
                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = devices[0].Id };
                await mediaCapture.InitializeAsync(settings);
            }
            // Catch upcoming exceptions.
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine("The app was denied access to the camera.");
                return;
            }

            // Setup ui.
            PreviewControl.Source = mediaCapture;
            PreviewingButton.IsEnabled = true;
        }

        /// <summary>
        /// Starts previewing and updates the Ui.
        /// </summary>
        private async void StartPreviewing()
        {
            await mediaCapture.StartPreviewAsync();
            isPreviewing = true;
            PreviewingButton.Content = "Stop camera";
        }

        /// <summary>
        /// Stops previewing and updates the Ui.
        /// </summary>
        private async void StopPreviewing()
        {
            await mediaCapture.StopPreviewAsync();
            isPreviewing = false;
            PreviewingButton.Content = "Start camera";
        }

        #endregion

        #region Event handler

        /// <summary>
        /// Raised when page has been loaded.
        /// Will init async operations.
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
        /// Raised in case of an failed attempt to start media capturing.
        /// </summary>
        /// <param name="sender">Underlying instance.</param>
        /// <param name="errorEventArgs">Event args.</param>
        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            System.Diagnostics.Debug.WriteLine($"Something failed: {errorEventArgs.Message}");
            PreviewingButton.IsEnabled = false;
        }

        #endregion
    }
}
