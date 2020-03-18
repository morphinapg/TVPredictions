﻿using System;
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
    public sealed partial class ModifyShows : Page
    {
        Network network;
        public ObservableCollection<Show> shows;
        private Show show;
        private Show nochanges;
        List<double> ratings;
        bool itemSelected;

        public ModifyShows()
        {
            this.InitializeComponent();
            itemSelected = false;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = (Network)e.Parameter;
            shows = network.AlphabeticalShows;
            ShowSelector.ItemsSource = shows;
            
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            SaveChanges_Click(Cancel, new RoutedEventArgs());
            NetworkDatabase.canGoBack = false; 
        }

        private void ShowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (shows.Count > 0)
            {
                itemSelected = true;

                var tempShow = (Show)ShowSelector.SelectedItem;

                var factors = new ObservableCollection<bool>();
                foreach (bool b in tempShow.factorValues)
                    factors.Add(b);

                ratings = new List<double>();
                foreach (double d in tempShow.ratings)
                    ratings.Add(d);

                show = new Show(tempShow.Name, network, tempShow.Season, factors, tempShow.Episodes, tempShow.Halfhour, network.factors, tempShow.AverageRating, tempShow.ShowIndex, tempShow.RenewalStatus, tempShow.Renewed, tempShow.Canceled)
                {
                    year = tempShow.year,
                    OldOdds = tempShow.OldOdds,
                    OldRating = tempShow.OldRating,
                    FinalPrediction = tempShow.FinalPrediction,
                    ShowIndex = tempShow.ShowIndex,
                    ratingsAverages = tempShow.ratingsAverages
                    
                };
                nochanges = new Show(tempShow.Name, network, tempShow.Season, factors, tempShow.Episodes, tempShow.Halfhour, network.factors, tempShow.AverageRating, tempShow.ShowIndex, tempShow.RenewalStatus, tempShow.Renewed, tempShow.Canceled)
                {
                    year = tempShow.year,
                    OldOdds = tempShow.OldOdds,
                    OldRating = tempShow.OldRating,
                    FinalPrediction = tempShow.FinalPrediction,
                    ShowIndex = tempShow.ShowIndex,
                    ratingsAverages = tempShow.ratingsAverages
                };

                ShowEditor.Navigate(typeof(ShowEditor), show);
            }
            
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (itemSelected)
            {
                //find index
                int index = 0;
                bool found = false;

                for (int i = 0; i < network.shows.Count && !found; i++)
                    if (network.shows[i].Name == nochanges.Name && network.shows[i].year == nochanges.year)
                    {
                        found = true;
                        index = i;
                    }

                if (found)
                {
                    network.shows.RemoveAt(index);

                    if ((Button)sender == SaveChanges)
                    {
                        var factors = new ObservableCollection<bool>();
                        foreach (bool b in show.factorValues)
                            factors.Add(b);

                        var newShow = new Show(show.Name, network, show.Season, factors, show.Episodes, show.Halfhour, network.factors, show.AverageRating, show.ShowIndex, show.RenewalStatus, show.Renewed, show.Canceled)
                        {
                            ratings = ratings,
                            year = show.year,
                            OldOdds = show.OldOdds,
                            OldRating = show.OldRating,
                            FinalPrediction = show.FinalPrediction,
                            ShowIndex = show.ShowIndex,
                            ratingsAverages = show.ratingsAverages
                        };

                        network.shows.Add(newShow);
                        itemSelected = false;
                        network.refreshEvolution = true;

                    }
                    else if ((Button)sender == Cancel)
                    {
                        itemSelected = false;
                        var factors = new ObservableCollection<bool>();
                        foreach (bool b in nochanges.factorValues)
                            factors.Add(b);

                        var newShow = new Show(nochanges.Name, network, nochanges.Season, factors, nochanges.Episodes, nochanges.Halfhour, network.factors, nochanges.AverageRating, nochanges.ShowIndex, nochanges.RenewalStatus, nochanges.Renewed, nochanges.Canceled)
                        {
                            ratings = ratings,
                            year = nochanges.year,
                            OldOdds = nochanges.OldOdds,
                            OldRating = nochanges.OldRating,
                            FinalPrediction = nochanges.FinalPrediction,
                            ShowIndex = nochanges.ShowIndex,
                            ratingsAverages = nochanges.ratingsAverages
                        };

                        network.shows.Add(newShow);
                        itemSelected = false;
                    }

                    network.Filter(NetworkDatabase.CurrentYear);
                    NetworkDatabase.pendingSave = true;

                    ShowEditor.Content = null;
                }
            }            
        }
    }
}
