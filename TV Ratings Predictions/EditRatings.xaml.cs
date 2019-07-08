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
    public sealed partial class EditRatings : Page
    {
        ObservableCollection<RatingsContainer> NetworkRatings;
        Network network;

        public EditRatings()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = (Network)e.Parameter;

            NetworkRatings = network.NetworkRatings;
            RatingsList.ItemsSource = NetworkRatings;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            network.Filter(NetworkDatabase.CurrentYear);
            NetworkDatabase.pendingSave = true;
            NetworkDatabase.canGoBack = false;
        }
    }
}
