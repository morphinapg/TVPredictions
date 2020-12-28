using Microsoft.Toolkit.Uwp.Helpers;
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
using Windows.Storage;
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

        string LocksList
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
            OnPropertyChanged("LocksList");
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
            NetworkDatabase.StartTime = DateTime.Now;

            foreach (Network n in NetworkList)
            {
                //var yearlist = n.shows.Select(x => x.year).Distinct();
                //Parallel.ForEach(n.shows, s => s.UpdateAverage());
                //n.shows.Sort();
                //foreach (int i in yearlist)
                //    n.UpdateIndexes(i);

                n.PredictionLocked = false;                

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

            //int g = 0;
            //var startTime = DateTime.Now;

            ExtendedExecutionForegroundSession newSession;
            while (!NetworkDatabase.cancelEvolution)
            {
                //g++;
                newSession = new ExtendedExecutionForegroundSession { Reason = ExtendedExecutionForegroundReason.Unconstrained };
                await newSession.RequestExtensionAsync();

                n.evolution.NextGeneration();              

                newSession.Dispose();

                //if (g == 100 && n.name == "CBS")
                //{
                //    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                //    {
                //        ContentDialog dialog = new ContentDialog
                //        {
                //            PrimaryButtonText = "OK",
                //            Content = "100 generations took " + (DateTime.Now - startTime).TotalMilliseconds + " ms"
                //        };
                //        ContentDialogResult result;
                //        result = await dialog.ShowAsync();
                //    });                    
                //}

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
                //Make a Backup of the current data
                var helper = new LocalObjectStorageHelper();

                if (await helper.FileExistsAsync("Settings"))           //If the Settings exists, read the settings and make a backup
                {
                    StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                    StorageFile newFile = await localFolder.GetFileAsync("Settings");
                    _ = await newFile.CopyAsync(ApplicationData.Current.LocalFolder, "ExportedSettings", NameCollisionOption.ReplaceExisting);
                }

                await NetworkDatabase.WritePredictionsAsync();

                

                NetworkDatabase.pendingSave = true;
            }
        }

        private void AllShows_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AllShowsByRating));
        }
    }
}
