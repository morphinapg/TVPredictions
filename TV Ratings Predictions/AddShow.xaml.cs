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
    public sealed partial class AddShow : Page
    {
        Network network;
        ObservableCollection<Factor> factors;
        bool halfhour = true;
        int episodes = 13;

        public AddShow()
        {
            this.InitializeComponent();
            factors = new ObservableCollection<Factor>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = (Network)e.Parameter;

            factors.Clear();
            foreach (string s in network.factors)
                factors.Add(new Factor(s, false));
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            network.Filter(NetworkDatabase.CurrentYear);
            NetworkDatabase.canGoBack = false;
        }

        private void AddShowButton_Click(object sender, RoutedEventArgs e)
        {
            var name = ShowName.Text;
            var notExist = true;          

            foreach (Show s in network.shows)
                if (s.Name == name && s.year == NetworkDatabase.CurrentYear) notExist = false;

            var settings = new ObservableCollection<bool>();

            foreach (Factor f in factors)
                settings.Add(f.Setting);

            if (name!="" && notExist)
            {
                network.shows.Add(new Show(name, network, settings, episodes, !halfhour, network.factors));
                ShowName.Text = "";
                HalfHour.IsOn = true;

                factors.Clear();
                foreach (string s in network.factors)
                    factors.Add(new Factor(s, false));

                ShowName.Focus(FocusState.Programmatic);
                //await NetworkDatabase.WriteSettingsAsync();
                NetworkDatabase.pendingSave = true;
            }                
            else
            {
                ShowName.Focus(FocusState.Programmatic);
                ShowName.SelectAll();
            }
                
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ShowName.Focus(FocusState.Programmatic);
        }

        private void HalfHour_Toggled(object sender, RoutedEventArgs e)
        {
            _30Mins.Opacity = HalfHour.IsOn ? 0.3 : 1;
            _60Mins.Opacity = HalfHour.IsOn ? 1 : 0.3;
        }
    }
}
