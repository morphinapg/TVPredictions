using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.ApplicationModel.ExtendedExecution.Foreground;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Threading;
using Windows.UI.Core;
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
    public sealed partial class HomePage : Page, INotifyPropertyChanged
    {
        //DispatcherTimer timer;

        ObservableCollection<Network> NetworkList;
        Thread[] EvolutionWork;

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            //NetworkDatabase.WriteSettings();
        }

        string locks
        {
            get
            {
                return NetworkDatabase.Locks;
            }
        }
        

        public HomePage()
        {
            this.InitializeComponent();
            NetworkList = NetworkDatabase.NetworkList;
            NetworkDatabase.LocksUpdated += NetworkDatabase_LocksUpdated;
        }

        private void NetworkDatabase_LocksUpdated(object sender, EventArgs e)
        {
            OnPropertyChanged("locks");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (NetworkDatabase.NetworkList.Count == 0)
                Message.Visibility = Visibility.Visible;

            NetworkDatabase.onHomePage = true;

            StartEvolution.Visibility = NetworkDatabase.EvolutionStarted ? Visibility.Collapsed : Visibility.Visible;
            StopEvolution.Visibility = NetworkDatabase.EvolutionStarted ? Visibility.Visible : Visibility.Collapsed;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.onHomePage = false;
        }

        private void StartEvolution_Click(object sender, RoutedEventArgs e)
        {
            foreach (Network n in NetworkList)
            {
                //var yearlist = n.shows.Select(x => x.year).Distinct();
                //Parallel.ForEach(n.shows, s => s.UpdateAverage());
                //n.shows.Sort();
                //foreach (int i in yearlist)
                //    n.UpdateIndexes(i);

                NetworkDatabase.StartTime = DateTime.Now;

                if (n.PredictionAccuracy != n.model.TestAccuracy() * 100)
                    n.ModelUpdate(n.model);
            }
                
                

            NetworkDatabase.cancelEvolution = false;
            EvolutionWork = new Thread[NetworkList.Count];            

            for (int i = 0; i < NetworkList.Count; i++)
            {
                EvolutionWork[i] = new Thread(new ParameterizedThreadStart(Background_EvolutionAsync))
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Lowest
                };
                EvolutionWork[i].Start(NetworkList[i]);
            }

            StartEvolution.Visibility = Visibility.Collapsed;
            StopEvolution.Visibility = Visibility.Visible;

            NetworkDatabase.EvolutionStarted = true;
        }

        async void Background_EvolutionAsync(object param)
        {            
            Network n = (Network)param;

            ExtendedExecutionForegroundSession newSession;
            while (!NetworkDatabase.cancelEvolution)
            {
                newSession = new ExtendedExecutionForegroundSession { Reason = ExtendedExecutionForegroundReason.Unconstrained };
                await newSession.RequestExtensionAsync();

                n.evolution.NextGeneration();              

                newSession.Dispose();
            }
        }

        private void StopEvolution_Click(object sender, RoutedEventArgs e)
        {
            NetworkDatabase.cancelEvolution = true;
            NetworkDatabase.EvolutionStarted = false;

            StopEvolution.Visibility = Visibility.Collapsed;
            StartEvolution.Visibility = Visibility.Visible;

            NetworkDatabase.pendingSave = true;            

            NetworkDatabase.SortNetworks();

        }

        //private async void SaveState_ClickAsync(object sender, RoutedEventArgs e)
        //{
        //    ContentDialog dialog = new ContentDialog
        //    {
        //        PrimaryButtonText = "Yes",
        //        CloseButtonText = "No",
        //        Content = "This will replace the previously saved prediction state. Continue?"
        //    };
        //    ContentDialogResult result;

        //    result = await dialog.ShowAsync();
        //    if (result == ContentDialogResult.Primary)
        //    {
        //        Parallel.ForEach(NetworkList, n =>
        //        {
        //            Parallel.ForEach(n.shows, s =>
        //            {
        //                s.OldRating = s.AverageRating;
        //                s.OldOdds = s.PredictedOdds;
        //            });
        //        });

        //        NetworkDatabase.pendingSave = true;
        //    }
        //}

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                Content = "This will replace the previously saved prediction state. Continue?"
            };
            ContentDialogResult result;

            result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await NetworkDatabase.WritePredictionsAsync();

                Parallel.ForEach(NetworkList, n =>
                {
                    foreach (Show s in n.shows)
                    {

                        s.OldRating = s.AverageRating;
                        s.OldOdds = s.PredictedOdds;

                        if (s.RenewalStatus == "")
                            s.FinalPrediction = s.OldOdds;
                    }
                });

                NetworkDatabase.pendingSave = true;
            }
        }

        private void AllShows_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AllShowsByRating));
        }
    }
}
