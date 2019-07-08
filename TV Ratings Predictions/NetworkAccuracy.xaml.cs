using Microsoft.Toolkit.Uwp.UI.Controls;
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
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TV_Ratings_Predictions
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NetworkAccuracy : Page
    {
        Network network;
        ObservableCollection<PredictionContainer> Predictions;

        public NetworkAccuracy()
        {
            this.InitializeComponent();
            
        }

        private void FilteredShows_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Predictions.Clear();
            UpdateList();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = (Network)e.Parameter;
            network.FilteredShows.CollectionChanged += FilteredShows_CollectionChanged;
            Predictions = new ObservableCollection<PredictionContainer>();
            ShowsList.ItemsSource = Predictions;
            UseOdds.IsChecked = NetworkDatabase.UseOdds;

            UpdateList();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.canGoBack = false;
        }

        void UpdateList()
        {
            int total = 0, count = 0;

            foreach (Show s in network.FilteredShows.OrderBy(x => x.PredictedOdds).Reverse())
                if (s.Renewed || s.Canceled)
                {
                    var container = new PredictionContainer(s, network, true);
                    Predictions.Add(container);
                    if (container.Accuracy == "✔")
                        total++;

                    count++;
                }

            foreach (DataGridColumn c in ShowsList.Columns)
                c.Width = DataGridLength.Auto;

            double percent = (double)total / count;

            Calculation.Text = "Network Accuracy: " + total + "/" + count + " (" + percent.ToString("P0") + ")";
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
                c.Width = DataGridLength.Auto;
        }
    }
}
