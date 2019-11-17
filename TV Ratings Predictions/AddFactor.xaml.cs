using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public sealed partial class AddFactor : Page
    {
        Network network;
        ObservableCollection<GroupedAddingFactor> AllShows;

        public AddFactor()
        {
            AllShows = new ObservableCollection<GroupedAddingFactor>();
            this.InitializeComponent();
        }

        private async void AddFactor_Click(object sender, RoutedEventArgs e)
        {
            if (FactorName.Text == "")
                FactorName.Focus(FocusState.Programmatic);
            else
            {
                ContentDialog dialog = new ContentDialog
                {
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No",
                    Content = "Are you absolutely sure you want to add '" + FactorName.Text + "' as a factor? This will reset the prediction model for " + network.name + ". \r\n\r\nMake sure you check the values for all shows before clicking yes."
                };
                ContentDialogResult result;
                result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var name = FactorName.Text;
                    network.factors.Add(name);

                    Parallel.ForEach(AllShows, g =>
                    {
                        foreach (AddingFactor s in g)
                            s.AddFactor();
                    });


                    var midpoint = network.GetMidpoint();
                    network.model = new NeuralPredictionModel(network, midpoint);
                    network.evolution = new EvolutionTree(network, midpoint);

                    NetworkDatabase.pendingSave = true;
                    Frame.GoBack();
                }                
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = e.Parameter as Network;
            var tempList = network.shows.OrderBy(x => x.year).ThenBy(x => x.Name);
            var yearList = new List<int>();

            foreach (Show s in tempList)
                if (!yearList.Contains(s.year))
                    yearList.Add(s.year);

            yearList.Sort();

            foreach (int year in yearList)
                AllShows.Add(new GroupedAddingFactor(tempList, year));
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.canGoBack = false;
        }
    }

    public class AddingFactor : INotifyPropertyChanged
    {
        Show show;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string Name
        {
            get
            {
                return show.Name;
            }
        }

        bool _factorvalue;
        public bool FactorValue
        {
            get
            {
                return _factorvalue;
            }
            set
            {
                _factorvalue = value;
                OnPropertyChanged("FactorValue");
            }
        }

        public AddingFactor(Show s)
        {
            show = s;
            FactorValue = false;
        }

        public void AddFactor()
        {
            show.factorValues.Add(FactorValue);
        }
    }

    public class GroupedAddingFactor : List<AddingFactor>
    {
        public int _year;
        public string Year
        {
            get
            {
                return _year + " - " + (_year+1);
            }
        }
        public List<AddingFactor> ListOfAddFactor => this;

        public GroupedAddingFactor(IOrderedEnumerable<Show> shows, int year)
        {
            var tempList = shows.Where(x => x.year == year);

            foreach (Show s in tempList)
                ListOfAddFactor.Add(new AddingFactor(s));

            _year = year;
        }
    }
}
