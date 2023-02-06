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
                    network.RealAverages = network.model.GetAverages(network.factors);
                    network.FactorAverages = network.RealAverages;

                    NetworkDatabase.pendingSave = true;
                    Frame.GoBack();
                }                
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            network = e.Parameter as Network;
            //var tempList = network.shows.OrderBy(x => x.year).ThenBy(x => x.Name).ThenBy(x => x.Season);
            //var yearList = new List<int>();

            //foreach (Show s in tempList)
            //    if (!yearList.Contains(s.year))
            //        yearList.Add(s.year);

            //yearList.Sort();

            //foreach (int year in yearList)
            //    AllShows.Add(new GroupedAddingFactor(tempList, year));

            var tempList = network.shows.OrderBy(x => x.Name).ThenBy(x => x.Season);
            var showList = tempList.Select(x => x.Name).Distinct().ToList();


            showList.Sort();

            await Task.Run(async () => 
            {
                foreach (string name in showList)
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => AllShows.Add(new GroupedAddingFactor(tempList, name)));
            });        
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
                //return show.NameWithSeason;
                return "Season " + show.Season + " (" + show.year + " - " + (show.year + 1) + ")";
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

    public class EpisodeFactor : INotifyPropertyChanged
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
                return "Season " + show.Season + " (" + show.year + " - " + (show.year + 1) + ")";
            }
        }

        int _previousepisodes;
        public int PreviousEpisodes
        {
            get
            {
                return _previousepisodes;
            }
            set
            {
                _previousepisodes = value;
                OnPropertyChanged("PreviousEpisodes");
            }
        }


        public EpisodeFactor(Show s)
        {
            show = s;
            PreviousEpisodes = s.PreviousEpisodes;
        }

        public void SetPrevious()
        {
            show.PreviousEpisodes = PreviousEpisodes;
        }
    }

    public class GroupedAddingFactor : List<AddingFactor>
    {
        public string _name;
        public string Name
        { get { return _name; } }

        public List<AddingFactor> ListOfAddFactor => this;

        public GroupedAddingFactor(IOrderedEnumerable<Show> shows, string name)
        {
            var tempList = shows.Where(x => x.Name == name);

            foreach (Show s in tempList)
                ListOfAddFactor.Add(new AddingFactor(s));

            _name = name;
        }
    }

    public class GroupedPreviousEpisodes : List<EpisodeFactor>
    {
        public string _show;
        public string Show
        {
            get
            {
                return _show;
            }
        }
        public List<EpisodeFactor> ListOfEpisodes => this;

        public GroupedPreviousEpisodes(IOrderedEnumerable<Show> shows, string Show)
        {
            var tempList = shows.Where(x => x.Name == Show);

            foreach (Show s in tempList)
                ListOfEpisodes.Add(new EpisodeFactor(s));

            _show = Show;
        }
    }
}
