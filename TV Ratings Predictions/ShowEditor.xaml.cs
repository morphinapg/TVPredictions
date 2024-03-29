﻿using System;
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
    public sealed partial class ShowEditor : Page
    {
        public Show show;
        ObservableCollection<Factor> factors;

        public ShowEditor()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            show = (Show)e.Parameter;
            factors = new ObservableCollection<Factor>();
            for (int i = 0; i < show.factorNames.Count; i++)
            {
                factors.Add(new Factor(show.factorNames[i], show.factorValues[i]));
                factors[i].PropertyChanged += ShowEditor_PropertyChanged;
            }       
        }

        private void ShowEditor_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            for (int i = 0; i < factors.Count; i++)
                show.factorValues[i] = factors[i].Setting;
        }

        private void HalfHour_Toggled(object sender, RoutedEventArgs e)
        {
            _30Mins.Opacity = HalfHour.IsOn ? 0.3 : 1;
            _60Mins.Opacity = HalfHour.IsOn ? 1 : 0.3;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            RenewalStatus.IsEnabled = !((bool)Renewed.IsChecked || (bool)Canceled.IsChecked);

            var PresetStatus = new string[] { "", "Renewed for Final Season", "Renewed", "Canceled" };

            if (PresetStatus.Contains(show.RenewalStatus))
            {
                if ((bool)Renewed.IsChecked && (bool)Canceled.IsChecked)
                    RenewalStatus.Text = PresetStatus[1];
                else if ((bool)Renewed.IsChecked)
                    RenewalStatus.Text = PresetStatus[2];
                else if ((bool)Canceled.IsChecked)
                    RenewalStatus.Text = PresetStatus[3];
                else
                    RenewalStatus.Text = PresetStatus[0];
            }            

            if (!RenewalStatus.IsEnabled)
                EditStatus.Visibility = Visibility.Visible;
            else
                EditStatus.Visibility = Visibility.Collapsed;

            show.RenewalStatus = RenewalStatus.Text;

        }

        private void EditStatus_Click(object sender, RoutedEventArgs e)
        {
            RenewalStatus.IsEnabled = true;
            EditStatus.Visibility = Visibility.Collapsed;
        }

        private void RenewalStatus_TextChanged(object sender, TextChangedEventArgs e)
        {
            show.RenewalStatus = RenewalStatus.Text;
        }
    }
}
