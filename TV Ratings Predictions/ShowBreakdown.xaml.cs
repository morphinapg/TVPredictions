using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Timers;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TV_Ratings_Predictions
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ShowBreakdown : Page, INotifyPropertyChanged
    {
        Network network;
        ObservableCollection<Show> shows;
        ObservableCollection<DetailsContainer> details;

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        ObservableCollection<DetailsContainer> Details => details;

        public ShowBreakdown()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            network = (Network)e.Parameter;
            shows = network.AlphabeticalShows;
            ShowSelector.ItemsSource = shows;
            details = new ObservableCollection<DetailsContainer>();
            //ShowDetails.ItemsSource = details;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.canGoBack = false;
        }

        DetailsCombo GenerateDetails(Show s, int[] FactorOrder, bool AllFactors = false) //Generate Factor details, but change the order of the search
        {
            //Needed: Code needs to start with a blank slate of average factor values, and one by one, modify that to the actual values, following the order given by FactorOrder

            var details = new List<DetailsContainer>();

            bool SyndicationFinished = false, OwnedFinished = false, PremiereFinished = false, SummerFinished = false, SeasonFinished = false;
            string detailName;

            

            var tempList = network.shows.OrderBy(x => x.Episodes).ToList();
            int LowestEpisode = tempList.First().Episodes, HighestEpisode = tempList.Last().Episodes;

            var BaseOdds = network.model.GetOdds(s, network.FactorAverages, false, true, -1);
            double CurrentOdds = BaseOdds, NewOdds, detailValue;

            var FactorCount = network.factors.Count;

            var RealOdds = network.model.GetOdds(s, network.FactorAverages);


            var CurrentFactors = new double[FactorCount + 3];
            //for (int i = 0; i < FactorCount + 2; i++)
            //    CurrentFactors[i] = network.FactorAverages[i];


            foreach (int i in FactorOrder)
            {
                //Need code to handle episode # and half hour here before other factors

                if (i == FactorCount + 2 || (i < FactorCount && network.factors[i] == "New Show")) //Season #
                {
                    if (!SeasonFinished)
                    {
                        CurrentFactors[FactorCount+2] = (s.Season - network.FactorAverages[FactorCount + 2]) / network.SeasonDeviation;

                        bool NewShow = false;
                        var NewShowIndex = network.factors.IndexOf("New Show");
                        if (NewShowIndex > -1)
                        {
                            NewShow = s.factorValues[NewShowIndex];
                            CurrentFactors[NewShowIndex] = (NewShow ? 1 : -1) - network.FactorAverages[NewShowIndex];
                        }

                        var hundredpart = s.Season / 100;
                        var remainder = s.Season - hundredpart * 100;
                        var tenpart = remainder / 10;
                        if (tenpart == 1)
                            detailName = s.Season + "th Season";
                        else
                        {
                            switch (s.Season % 10)
                            {
                                case 1:
                                    detailName = s.Season + "st Season";
                                    break;
                                case 2:
                                    detailName = s.Season + "nd Season";
                                    break;
                                case 3:
                                    detailName = s.Season + "rd Season";
                                    break;
                                default:
                                    detailName = s.Season + "th Season";
                                    break;
                            }
                        }

                        if (s.Season == 1 && !NewShow)
                            detailName += " (Re-aired from another network)";

                        NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);

                        detailValue = NewOdds - CurrentOdds;

                        CurrentOdds = NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));

                        SeasonFinished = true;
                    }
                }
                else if (i == FactorCount) //Episode Count
                {
                    detailName = s.Episodes + " Episodes Ordered";

                    CurrentFactors[i] = s.Episodes / 26.0 * 2 - 1 - network.FactorAverages[i];

                    NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);

                    detailValue = NewOdds - CurrentOdds;

                    CurrentOdds = NewOdds;

                    details.Add(new DetailsContainer(detailName, detailValue));
                }
                else if (i == FactorCount+1) //Half Hour
                {
                    detailName = s.Halfhour ? "Half Hour Show" : "Hour Long Show";

                    CurrentFactors[i] = (s.Halfhour ? 1 : -1) - network.FactorAverages[i];

                    NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);

                    detailValue = NewOdds - CurrentOdds;

                    CurrentOdds = NewOdds;

                    details.Add(new DetailsContainer(detailName, detailValue));
                }
                else if ((network.factors[i] == "Syndication" || network.factors[i] == "Post-Syndication") && !AllFactors)
                {
                    if (!SyndicationFinished)
                    {
                        bool Syndication = false;
                        bool PostSyndication = false;
                        int SyndicationIndex = network.factors.IndexOf("Syndication"), PostIndex = network.factors.IndexOf("Post-Syndication");

                        if (SyndicationIndex > -1)
                        { 
                            Syndication = s.factorValues[SyndicationIndex];
                            CurrentFactors[SyndicationIndex] = (Syndication ? 1 : -1) - network.FactorAverages[SyndicationIndex];
                        }
                        if (PostIndex > -1) 
                        { 
                            PostSyndication = s.factorValues[PostIndex];
                            CurrentFactors[PostIndex] = (PostSyndication ? 1 : -1) - network.FactorAverages[PostIndex];
                        }


                        if (Syndication)
                            detailName = "Will be syndicated next season";
                        else if (PostSyndication)
                            detailName = "Has already been syndicated";
                        else
                            detailName = "Not syndicated yet";

                        NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);

                        detailValue = NewOdds - CurrentOdds;

                        CurrentOdds = NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));

                        SyndicationFinished = true;
                    }
                }
                else if ((network.factors[i] == "Spring" || network.factors[i] == "Summer" || network.factors[i] == "Fall") && !AllFactors)
                {
                    if (!PremiereFinished)
                    {
                        bool Spring = false, Summer = false, Fall = false;
                        int FallIndex = network.factors.IndexOf("Fall"), SpringIndex = network.factors.IndexOf("Spring"), SummerIndex = network.factors.IndexOf("Summer");
                        if (FallIndex > -1)
                        {
                            Fall = s.factorValues[FallIndex];
                            CurrentFactors[FallIndex] = (Fall ? 1 : -1) - network.FactorAverages[FallIndex];
                        }
                        if (SpringIndex > -1)
                        {
                            Spring = s.factorValues[SpringIndex];
                            CurrentFactors[SpringIndex] = (Spring ? 1 : -1) - network.FactorAverages[SpringIndex];
                        }                            
                        if (SummerIndex > -1)
                            Summer = s.factorValues[SummerIndex];  

                        if (Fall)
                            detailName = Spring ? "Fall Preview with a Premiere in the Spring" : "Premiered in the Fall";
                        else if (Spring)
                            detailName = "Premiered in the Spring";
                        else if (Summer)
                        {
                            detailName = "Premiered in the Summer";
                            SummerFinished = true;
                            CurrentFactors[SummerIndex] = 1 - network.FactorAverages[SummerIndex];
                        }
                        else
                            detailName = (FallIndex > -1) ? "Unknown Premiere Date" : "Premiered in the Fall";

                        PremiereFinished = true;

                        NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);

                        detailValue = NewOdds - CurrentOdds;

                        CurrentOdds = NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));
                    }

                    if (network.factors[i] == "Summer" && !SummerFinished)
                    {
                        CurrentFactors[i] = (s.factorValues[i] ? 1 : -1) - network.FactorAverages[i];

                        if (s.factorValues[i])
                            detailName = "Aired in the Summer";
                        else
                            detailName = "Did not air in the Summer";

                        
                        NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);

                        detailValue = NewOdds - CurrentOdds;

                        CurrentOdds = NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));

                        SummerFinished = true;
                    }
                }
                else if ((network.factors[i] == "Not Original" || network.factors[i] == "CBS Show") && !AllFactors)
                {
                    if (!OwnedFinished)
                    {
                        if (s.factorNames.Contains("CBS Show") && s.factorNames.Contains("Not Original"))
                        {
                            int index = s.factorNames.IndexOf("Not Original"), index2 = s.factorNames.IndexOf("CBS Show");
                            bool NotOriginal = s.factorValues[index], CBSShow = s.factorValues[index2];
                            CurrentFactors[index] = (NotOriginal ? 1 : -1) - network.FactorAverages[index];
                            CurrentFactors[index2] = (CBSShow ? 1 : -1) - network.FactorAverages[index2];

                            if (NotOriginal)
                                detailName = "Show is not owned by the network";
                            else if (CBSShow)
                                detailName = "Show is owned by CBS";
                            else
                                detailName = "Show is owned by WB";

                            NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);
                        }
                        else
                        {
                            CurrentFactors[i] = (s.factorValues[i] ? 1 : -1) - network.FactorAverages[i];

                            if (s.factorValues[i])
                                detailName = "Show is not owned by the network";
                            else
                                detailName = "Show is owned by the network";

                            NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);
                        }

                        detailValue = NewOdds - CurrentOdds;
                        CurrentOdds = NewOdds;
                        details.Add(new DetailsContainer(detailName, detailValue));
                        OwnedFinished = true;
                    }
                }
                else
                {
                    CurrentFactors[i] = (s.factorValues[i] ? 1 : -1) - network.FactorAverages[i];

                    switch (network.factors[i])
                    {
                        case "Friday":
                            {
                                if (s.factorValues[i])
                                    detailName = "Airs on Friday or Saturday";
                                else
                                    detailName = "Does not air on Friday or Saturday";

                                break;
                            }
                        case "10pm":
                            {
                                if (s.factorValues[i])
                                    detailName = "Airs at 10pm";
                                else
                                    detailName = "Airs before 10pm";

                                break;
                            }
                        case "Animated":
                            {
                                if (s.factorValues[i])
                                    detailName = "Animated show";
                                else
                                    detailName = "Non-animated show";

                                break;
                            }
                        case "Extended Universe":
                            {
                                if (s.factorValues[i])
                                    detailName = "Part of an Extended Universe";
                                else
                                    detailName = "Not part of an Extended Universe";

                                break;
                            }
                        default:
                            {
                                if (s.factorValues[i])
                                    detailName = "'" + s.factorNames[i] + "' is True";
                                else
                                    detailName = "'" + s.factorNames[i] + "' is False";

                                break;
                            }
                    }

                    NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);

                    detailValue = NewOdds - CurrentOdds;

                    CurrentOdds = NewOdds;

                    details.Add(new DetailsContainer(detailName, detailValue));
                }
            }

            return new DetailsCombo(details, BaseOdds, CurrentOdds);
        }

        private async void ShowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = (Show)ShowSelector.SelectedItem;
            
            if (s != null)
            {
                details.Clear();

                //var Adjustments = network.model.GetAdjustments(true);

                var FactorCount = network.factors.Count + 3;
                int Iterations = 20000; //Enough for precision within 0.01%

                var AllResults = new DetailsCombo[Iterations];
                var Random = new Random();

                var Numbers = new int[FactorCount];
                Numbers[0] = FactorCount - 1;
                Numbers[1] = FactorCount - 3;
                Numbers[2] = FactorCount - 2;

                for (int i = 3; i < FactorCount; i++)
                    Numbers[i] = i - 3;

                BreakdownProgress.Value = 0;
                BreakdownProgress.Maximum = Iterations;
                BreakdownProgress.Visibility = Visibility.Visible;
                var CompletedProgress = new int[Iterations];


                var ProgressTimer = new Timer(100) { AutoReset = true };
                ProgressTimer.Elapsed += async (se, ee) => await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => BreakdownProgress.Value = CompletedProgress.Sum());
                ProgressTimer.Start();

                await Task.Run(() =>
                {
                    AllResults[0] = GenerateDetails(s, Numbers);
                    CompletedProgress[0] = 1;

                    Parallel.For(1, Iterations, i =>
                    {
                        var OrderedNumbers = Numbers.OrderBy(x => Random.NextDouble()).ToArray();
                        AllResults[i] = GenerateDetails(s, OrderedNumbers);
                        CompletedProgress[i] = 1;
                    });
                });

                ProgressTimer.Stop();
                BreakdownProgress.Visibility = Visibility.Collapsed;


                var FactorNames = AllResults[0].details.Select(x => x.Name).ToList();
                var count = FactorNames.Count;
                var FactorValues = new double[count];

                Parallel.For(0, count, i => FactorValues[i] = AllResults.SelectMany(x => x.details).Where(x => x.Name == FactorNames[i]).Select(x => x.Value).Average());

                var DetailsList = new List<DetailsContainer>();
                for (int i = 0; i < count; i++)
                    DetailsList.Add(new DetailsContainer(FactorNames[i], FactorValues[i]));

                var Results = new DetailsCombo(DetailsList, AllResults[0].BaseOdds, AllResults[0].CurrentOdds);
                foreach (DetailsContainer d in Results.details)
                    details.Add(d);             

                for (int i = details.Count - 1; i >= 0; i--)
                    if (Math.Round(details[i].Value, 4) == 0)
                    {
                        details.RemoveAt(i);
                        DetailsList.RemoveAt(i);
                    }
                        

                ShowName.Text = s.NameWithSeason;
                Odds.Text = "Predicted Odds: " + Results.CurrentOdds.ToString("P");
                Base.Text = "Base Odds: " + Results.BaseOdds.ToString("P");

                if (s.Renewed || s.Canceled)
                {
                    if ((s.Renewed && Results.CurrentOdds >= 0.5) || (s.Canceled && Results.CurrentOdds <= 0.5))
                        Odds.Text += " ✔";
                    else
                        Odds.Text += " ❌";
                }

                ShowDetails.ItemsSource = DetailsList;
            }            

        }

        private async void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Desktop, SuggestedFileName = ((Show)ShowSelector.SelectedItem).NameWithSeason };
            picker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var n = new NetworkHome();
                ShowName.Visibility = Visibility.Visible;
                Disclaimer.Visibility = Visibility.Visible;

                await n.SnapShotPNGAsync(Breakdown, file);

                ShowName.Visibility = Visibility.Collapsed;
                Disclaimer.Visibility = Visibility.Collapsed;
            }
        }
    }

    public class DetailsContainer : INotifyPropertyChanged
    {
        /// <summary>
        /// Name of the Factor
        /// </summary>
        public String Name;

        /// <summary>
        /// Value of the Factor
        /// </summary>
        double _value;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public double Value
        {
            get
            {
                return _value;
                //return Math.Round(_value, 4);
            }
            set
            {
                _value = value;
                OnPropertyChanged("Value");
                OnPropertyChanged("FormattedValue");
            }
        }
        public string FormattedValue
        {
            get
            {
                if (Value == 0)
                    return "No Change";
                else
                    return _value.ToString("+0.00%; -0.00%");
            }
        }

        public DetailsContainer(string s, double d)
        {
            Name = s;
            _value = d;
        }
    }

    class DetailsCombo
    {
        public List<DetailsContainer> details;
        public double BaseOdds, CurrentOdds;

        public DetailsCombo(List<DetailsContainer> d, double b, double c)
        {
            details = d;
            BaseOdds = b;
            CurrentOdds = c;
        }
    }


}
