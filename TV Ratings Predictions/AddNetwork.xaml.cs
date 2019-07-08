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
    public sealed partial class AddNetwork : Page
    {
        ObservableCollection<string> factors = new ObservableCollection<string>();
        ObservableCollection<Network> NetworkList;

        public AddNetwork()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            NetworkList = (ObservableCollection<Network>)e.Parameter;
        }

        private void AddFactor_Click(object sender, RoutedEventArgs e)
        {
            var name = FactorName.Text;
            if (name != "" && factors.IndexOf(name)==-1)
            {
                factors.Add(FactorName.Text);
                FactorName.Text = "";
            }

            FactorName.Focus(FocusState.Programmatic);
        }

        private void FactorName_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                AddFactor_Click(sender, new RoutedEventArgs());
        }

        private void DeleteFactor_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement).DataContext;
            var index = FactorList.Items.IndexOf(item);
            factors.RemoveAt(index);

            NetworkDatabase.isAddPage = (NetworkName.Text != "" || FactorName.Text != "" || factors.Count > 0);
        }

        private void AddNetworkButton_Click(object sender, RoutedEventArgs e)
        {
            var name = NetworkName.Text;
            var exists = false;

            foreach (Network n in NetworkDatabase.NetworkList)
                if (n.name == name) exists = true;

            if (name !="" && !exists)
            {
                var newfactors = new ObservableCollection<string>();
                foreach (string s in factors)
                    newfactors.Add(s);

                NetworkList.Add(new Network(name, newfactors));
                NetworkDatabase.SortNetworks();
                NetworkName.Text = "";
                FactorName.Text = "";
                factors.Clear();
                NetworkDatabase.isAddPage = false;

                //await NetworkDatabase.WriteSettingsAsync();
                NetworkDatabase.pendingSave = true;
            }            

            NetworkName.Focus(FocusState.Programmatic);
            NetworkName.SelectAll();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            NetworkName.Focus(FocusState.Programmatic);
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NetworkDatabase.isAddPage = (NetworkName.Text != "" || FactorName.Text != "" || factors.Count > 0);
        }
    }
}
