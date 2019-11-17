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
    public sealed partial class SortFactors : Page
    {
        Network network;
        public ObservableCollection<string> Factors;

        public SortFactors()
        {
            Factors = new ObservableCollection<string>();
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = e.Parameter as Network;
            
            foreach (string s in network.factors)
                Factors.Add(s);

        }

        private async void SaveFactors_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                Content = "Are you sure you want to save the new sort order for these factors? This will reset the prediction model for " + network.name + "!"
            };
            ContentDialogResult result;
            result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                foreach (string f in Factors)
                {
                    int oldIndex = network.factors.IndexOf(f), newIndex = Factors.IndexOf(f);

                    //First, sort factors in the network itself
                    network.factors.Move(oldIndex, newIndex);

                    //Then do the same for every single show in the network
                    Parallel.ForEach(network.shows, s => s.factorValues.Move(oldIndex, newIndex));
                }

                var midpoint = network.GetMidpoint();
                network.model = new NeuralPredictionModel(network, midpoint);
                network.evolution = new EvolutionTree(network, midpoint);

                NetworkDatabase.pendingSave = true;
                Frame.GoBack();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.canGoBack = false;
        }
    }
}
