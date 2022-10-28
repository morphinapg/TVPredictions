using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
    public sealed partial class SetPrevious : Page
    {
        Network network;
        ObservableCollection<GroupedPreviousEpisodes> AllShows;

        public SetPrevious()
        {
            AllShows = new ObservableCollection<GroupedPreviousEpisodes>();
            this.InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            AllShows.AsParallel().SelectMany(x => x).ForAll(x => x.SetPrevious());


            //var midpoint = network.GetMidpoint();
            //network.model = new NeuralPredictionModel(network, midpoint);
            //network.evolution = new EvolutionTree(network, midpoint);

            NetworkDatabase.pendingSave = true;
            Frame.GoBack();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            network = e.Parameter as Network;
            var tempList = network.shows.Where(x => x.Season > 1).OrderBy(x => x.Name).ThenBy(x => x.Season);
            var showList = tempList.Select(x => x.Name).Distinct().ToList();


            showList.Sort();

            await Task.Run(async () => 
            {
                foreach (string name in showList)
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => AllShows.Add(new GroupedPreviousEpisodes(tempList, name)));
            });           
            
        }
    }
}
