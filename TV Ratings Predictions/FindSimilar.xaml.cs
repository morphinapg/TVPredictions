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
    public sealed partial class FindSimilar : Page, INotifyPropertyChanged
    {
        Network network;
        ObservableCollection<Show> shows;
        List<Show> AllShows;
        ObservableCollection<SimilarityContainer> details;

        ObservableCollection<SimilarityContainer> Details => details;

        public FindSimilar()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = (Network)e.Parameter;
            shows = network.AlphabeticalShows;
            AllShows = network.shows;
            ShowSelector.ItemsSource = shows;
            details = new ObservableCollection<SimilarityContainer>();
            //ShowDetails.ItemsSource = details;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.canGoBack = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async void ShowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = (Show)ShowSelector.SelectedItem;

            if (s != null)
            {
                details.Clear();

                await Task.Run(async () =>
                {
                    var Baseline = network.model.GetInputsPlusIndex(s);


                    var TempShows = AllShows.AsParallel().Where(x => x != s).Select(x =>
                    {
                        var NewInputs = network.model.GetInputsPlusIndex(x);

                        double subtotal = 0;
                        for (int i = 0; i < NewInputs.Length; i++)
                            subtotal += Math.Pow(NewInputs[i] - Baseline[i], 2);

                        return new {Show = x, Distance = Math.Sqrt(subtotal) };
                    }).OrderBy(x => x.Distance).ToList();

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => TempShows.ForEach(x => details.Add(new SimilarityContainer(x.Show, x.Distance))));
                });

            }
        }
    }

    public class SimilarityContainer
    {
        Show show;
        double _distance;

        public string Name => show.Name;
        public string Season => "Season " + show.Season + " (" + show.year + ")";
        public string Status => show.RenewalStatus;
        public double StatusValue
        {
            get
            {
                if (show.Renewed)
                    return 1;
                else if (show.Canceled)
                    return -1;
                else
                    return 0;
            }
        }
        public double Difference => _distance;

        public SimilarityContainer(Show s, double Difference)
        {
            show = s;
            _distance = Difference;
        }
    }
}
