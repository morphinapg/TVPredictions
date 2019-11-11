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
    public sealed partial class DeleteFactor : Page
    {
        ObservableCollection<string> factors;
        Network network;

        public DeleteFactor()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = e.Parameter as Network;
            factors = network.factors;
        }

        private async void DeleteFactorButton_Click(object sender, RoutedEventArgs e)
        {
            if (FactorList.SelectedIndex > -1)
            {
                ContentDialog dialog = new ContentDialog
                {
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No",
                    Content = "Are you absolutely sure you want to delete '" + FactorList.SelectedItem + "' as a factor? This will reset the prediction model for " + network.name + "."
                };
                ContentDialogResult result;
                result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    //First, determine the index of the factor you're removing
                    var index = factors.IndexOf(FactorList.SelectedItem as string);

                    //Remove this factor from the network
                    network.factors.RemoveAt(index);

                    //Remove this factor from every show on the network
                    Parallel.ForEach(network.shows.ToList(), s => s.factorValues.RemoveAt(index));

                    //Reset the prediction model
                    network.model = new NeuralPredictionModel(network);
                    network.evolution = new EvolutionTree(network);

                    NetworkDatabase.pendingSave = true;
                    Frame.GoBack();
                }
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.canGoBack = false;
        }
    }
}
