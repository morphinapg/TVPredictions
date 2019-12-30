using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TV_Ratings_Predictions
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        ObservableCollection<Network> NetworkCollection;
        DispatcherTimer timer;

        public MainPage()
        {
            this.InitializeComponent();
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            ReadSettingsAsync();    
        }

        private void Timer_Tick(object sender, object e)
        {
            if (NetworkDatabase.onHomePage)
                foreach (Network n in NetworkDatabase.NetworkList)
                    n.OnPropertyChangedAsync("LastUpdate");

            if (NetworkDatabase.pendingSave)
            {
                NetworkDatabase.WriteSettings();
                NetworkDatabase.pendingSave = false;
                foreach (Network n in NetworkDatabase.NetworkList)
                {
                    if (n.refreshPrediction)
                    {
                        n.RefreshPredictions(true);
                        n.refreshPrediction = false;
                    }

                }
            }

            
        }

        async void ReadSettingsAsync()
        {
            var year = DateTime.Today.Month < 9 ? DateTime.Today.Year - 1 : DateTime.Today.Year;
            NetworkDatabase.MaxYear = year;
            NetworkDatabase.CurrentYear = year;

            await NetworkDatabase.ReadSettings();

            NetworkCollection = NetworkDatabase.NetworkList;           
            

            int count = 0;

            foreach (Network n in NetworkCollection)
                count += n.shows.Where(x => x.year == year).Count();

            if (count == 0)
                year--;

            NetworkDatabase.CurrentYear = year;

            
            TVSeason.Date = new DateTime(year, 9, 1);
            TVSeason.MaxYear = new DateTime(NetworkDatabase.MaxYear, 9, 1);


            timer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 1) };
            timer.Tick += Timer_Tick;
            timer.Start();

            MainFrame.Navigate(typeof(HomePage));
        }

        

        private async void ListView_ItemClickAsync(object sender, ItemClickEventArgs e)
        {
            
            if (sender == AddItem)
            {
                ContentDialog dialog = new ContentDialog
                {
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No",
                    Content = "Discard current network?"
                };
                ContentDialogResult result;

                if (NetworkDatabase.isAddPage)
                {
                    result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        NetworkSelectionMenu.SelectedIndex = -1;
                        MainFrame.Navigate(typeof(AddNetwork), NetworkDatabase.NetworkList);
                        NetworkDatabase.isAddPage = false;
                    }

                }
                else
                {
                    NetworkDatabase.isAddPage = false;
                    NetworkSelectionMenu.SelectedIndex = -1;
                    MainFrame.Navigate(typeof(AddNetwork), NetworkDatabase.NetworkList);
                }     
                
            }
            else
            {
                
                AddItem.SelectedIndex = -1;
                MainFrame.Navigate(typeof(NetworkHome), e.ClickedItem);
            }
                
        }

        private void TVSeason_DateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            var year = TVSeason.Date.Year;

            NetworkDatabase.CurrentYear = year;

            NetworkDatabase.SortNetworks();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(typeof(HomePage));
            NetworkSelectionMenu.SelectedIndex = -1;
            AddItem.SelectedIndex = -1;
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            Back.Visibility = NetworkDatabase.canGoBack ? Visibility.Visible : Visibility.Collapsed;
            Home.Visibility = NetworkDatabase.canGoBack ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.GoBack();
        }
    }
}
