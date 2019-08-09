using System;
using System.Threading.Tasks;
using HomeBear.Tilt.ViewModel;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

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

        private MediaCapture mediaCapture;

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
        }

        #endregion

        #region Private helper

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

            // Start previewing.
            StartPreviewing();
        }

        private async void StartPreviewing()
        {
            await mediaCapture.StartPreviewAsync();
            isPreviewing = true;
            PreviewingButton.Content = "Stop camera";
        }

        private async void StopPreviewing()
        {
            await mediaCapture.StopPreviewAsync();
            isPreviewing = false;
            PreviewingButton.Content = "Start camera";
        }

        #endregion

        #region Event handler

        private async void MainPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await InitializeCameraAsync();
        }

        private void PreviewingButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (isPreviewing)
            {
                StopPreviewing();
            }
            else
            {
                StartPreviewing();
            }
        }

        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            System.Diagnostics.Debug.WriteLine($"Something failed: {errorEventArgs.Message}");
            PreviewingButton.IsEnabled = false;
        }

        #endregion
    }
}
