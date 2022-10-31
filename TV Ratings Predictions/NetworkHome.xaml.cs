using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Storage;
using Windows.Graphics.Display;
using Windows.Storage.Pickers;
using System.Drawing;
using Windows.UI.Composition;
using Microsoft.Toolkit.Uwp.UI.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TV_Ratings_Predictions
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NetworkHome : Page
    {
        Network network;
        ObservableCollection<PredictionContainer> Predictions;

        public NetworkHome()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = (Network)e.Parameter;
            //if (network.refreshPrediction)
            //{
            //    network.refreshPrediction = false;
            //    network.RefreshPredictions();
            //}

            //network.RefreshPredictions();
            //network.SortPredictions();
            NetworkName.Text = network.name;
            HeaderThreshold.Text = "Typical Renewal Threshold: " + Math.Round(network.model.GetNetworkRatingsThreshold(NetworkDatabase.CurrentYear, true, true), 2);
            Adjustment.Text = (NetworkDatabase.CurrentYear == NetworkDatabase.MaxYear) ? "Current Adjustment: " + network.Adjustment : "";
            Predictions = network.Predictions;
            ShowsList.ItemsSource = Predictions;
            Predictions.CollectionChanged += Predictions_CollectionChanged;
            UseOdds.IsChecked = NetworkDatabase.UseOdds;
            AddFactor.IsEnabled = !NetworkDatabase.EvolutionStarted;
            SortFactors.IsEnabled = !NetworkDatabase.EvolutionStarted;

            foreach (DataGridColumn c in ShowsList.Columns)
            {
                c.Width = DataGridLength.Auto;
            }
        }

        private void Predictions_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            HeaderThreshold.Text = "Typical Renewal Threshold: " + Math.Round(network.model.GetNetworkRatingsThreshold(NetworkDatabase.CurrentYear, true, true), 2);
            Adjustment.Text = (NetworkDatabase.CurrentYear == NetworkDatabase.MaxYear) ? "Current Adjustment: " + network.Adjustment : "";
        }

        private void AddShow_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(AddShow), network, new DrillInNavigationTransitionInfo());
        }

        private void EditRatings_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(EditRatings), network, new DrillInNavigationTransitionInfo());
        }

        private void ModifyShows_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(ModifyShows), network, new DrillInNavigationTransitionInfo());
        }

        private void ShowsByRating_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(ShowsByRating), network, new DrillInNavigationTransitionInfo());
        }

        private void ShowsByFactor_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(ShowsByFactor), network, new DrillInNavigationTransitionInfo());
        }

        public async Task SnapShotPNGAsync(UIElement source, StorageFile file)
        {
            //source.UseLayoutRounding = true;

           

            RenderTargetBitmap renderTarget = new RenderTargetBitmap();

            await renderTarget.RenderAsync(source);

            var pixelBuffer = await renderTarget.GetPixelsAsync();
            var pixels = pixelBuffer.ToArray();
            var displayInformation = DisplayInformation.GetForCurrentView();

            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                                     BitmapAlphaMode.Ignore,
                                     (uint)renderTarget.PixelWidth,
                                     (uint)renderTarget.PixelHeight,
                                     displayInformation.LogicalDpi,
                                     displayInformation.LogicalDpi,
                                     pixels);
                await encoder.FlushAsync();
            }
        }

        private async void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Desktop };
            //picker.DefaultFileExtension = "png";
            picker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
            picker.SuggestedFileName = network.name;

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {

                ChartTitle.Text = network.name;
                RenewalThreshold.Text = "Typical Renewal Threshold: " + Math.Round(network.model.GetNetworkRatingsThreshold(NetworkDatabase.CurrentYear, true, true), 2);
                ChartTitle.Visibility = Visibility.Visible;
                RenewalThreshold.Visibility = Visibility.Visible;


                await SnapShotPNGAsync(NetworkChart, file);

                ChartTitle.Visibility = Visibility.Collapsed;
                RenewalThreshold.Visibility = Visibility.Collapsed;
            }
        }

        private void NetworkModel_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(ShowBreakdown), network, new DrillInNavigationTransitionInfo());
        }

        private void UseOdds_Checked(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.UseOdds = (bool)UseOdds.IsChecked;

            foreach (PredictionContainer p in Predictions)
            {
                p.OnPropertyChanged("Prediction");
                p.OnPropertyChanged("PredictionDifference");
            }

            foreach (DataGridColumn c in ShowsList.Columns)
            {
                c.Width = DataGridLength.Auto;
            }
        }

        private void Accuracy_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(NetworkAccuracy), network, new DrillInNavigationTransitionInfo());
        }

        private void AddFactor_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(AddFactor), network, new DrillInNavigationTransitionInfo());
        }

        private void SortFactors_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(SortFactors), network, new DrillInNavigationTransitionInfo());
        }

        private void DeleteFactor_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(DeleteFactor), network, new DrillInNavigationTransitionInfo());
        }

        private void EditViewers_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(EditViewers), network, new DrillInNavigationTransitionInfo());
        }

        private void Similar_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(FindSimilar), network, new DrillInNavigationTransitionInfo());
        }

        private void SetPrevious_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.canGoBack = true;
            Frame.Navigate(typeof(SetPrevious), network, new DrillInNavigationTransitionInfo());
        }
    }
}
