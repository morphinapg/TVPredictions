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
    public sealed partial class ShowsByFactor : Page
    {
        Network network;
        public ObservableCollection<string> factors;
        public ObservableCollection<FactorContainer> shows;
        ObservableCollection<Show> showList;

        public ShowsByFactor()
        {
            this.InitializeComponent();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = (Network)e.Parameter;
            factors = network.factors;
            FactorSelector.ItemsSource = factors;
            shows = new ObservableCollection<FactorContainer>();
            showList = network.FilteredShows;
            showList.CollectionChanged += ShowList_CollectionChanged;
            AllYears.IsChecked = false;
        }

        private void ShowList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (showList.Count > 0 && FactorSelector.SelectedIndex > -1)
                Update_Selection();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.canGoBack = false;
        }

        private void FactorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (showList.Count > 0)
                Update_Selection();
        }

        void Update_Selection()
        {
            var i = FactorSelector.SelectedIndex;

            //shows.Clear();
            shows.Clear();
            bool allYears = (bool)AllYears.IsChecked;

            foreach (Show s in showList)
            {
                if (s.factorValues[i])
                    shows.Insert(0, new FactorContainer(s, allYears));
            }

            foreach (DataGridColumn c in ShowsList.Columns)
            {
                c.Width = DataGridLength.Auto;
            }
        }

        private void AllYears_Checked(object sender, RoutedEventArgs e)
        {
            showList = new ObservableCollection<Show>(network.shows.OrderBy(s => s.ShowIndex));
            Update_Selection();
        }

        private void AllYears_Unchecked(object sender, RoutedEventArgs e)
        {
            showList = network.FilteredShows;
            Update_Selection();
        }
    }
}
