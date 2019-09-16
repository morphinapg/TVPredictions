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
    public sealed partial class AllShowsByRating : Page
    {
        ObservableCollection<PredictionContainer> FilteredShows;
        List<Show> AllShows;

        public AllShowsByRating()
        {
            FilteredShows = new ObservableCollection<PredictionContainer>();
            AllShows = new List<Show>();
            foreach (Network n in NetworkDatabase.NetworkList)
                foreach (Show s in n.shows)
                    if (s.year == NetworkDatabase.CurrentYear) AllShows.Add(s);            

            this.InitializeComponent();

            FilterShows();
        }

        private void CheckedUnchecked(object sender, RoutedEventArgs e)
        {
            FilterShows();
        }

        void FilterShows()
        {
            FilteredShows.Clear();
            var isFiltered = (bool)Renewed.IsChecked || (bool)Canceled.IsChecked || (bool)PredictedRenewed.IsChecked || (bool)PredictedCanceled.IsChecked;
            var tmpList = AllShows.Where(x =>
            {
                if (isFiltered)
                    return
                    ((bool)Renewed.IsChecked && x.Renewed) ||
                    ((bool)Canceled.IsChecked && x.Canceled) ||
                    ((bool)PredictedRenewed.IsChecked && x.PredictedOdds > 0.5) ||
                    ((bool)PredictedCanceled.IsChecked && x.PredictedOdds < 0.5);
                else
                    return true;
            }).OrderByDescending(x => x.AverageRating);

            foreach (Show s in tmpList)
                FilteredShows.Add(new PredictionContainer(s, s.network));
        }
    }
}
