using System;
using System.Collections.Concurrent;
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

            bool OwnedFinished = false, PremiereFinished = false, SummerFinished = false, SeasonFinished = false;
            string detailName;

            

            var tempList = network.shows.OrderBy(x => x.Episodes).ToList();
            int LowestEpisode = tempList.First().Episodes, HighestEpisode = tempList.Last().Episodes;

            

            var FactorCount = network.factors.Count;

            var CurrentFactors = network.model.GetBaseInputs();
            CurrentFactors[FactorCount + 4] = (s.year - network.FactorAverages[FactorCount + 4]) / network.YearDeviation; //We want to compare the factors, but not compare how previous years affect the odds

            var BaseOdds = network.model.GetModifiedOdds(s, CurrentFactors);
            double CurrentOdds = BaseOdds, NewOdds, detailValue;


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
                else if (i == FactorCount + 3) // Syndication
                {
                    //detailName = s.PreviousEpisodes + " Episodes aired before this season";
                    detailName = "Syndication";

                    //detailName = "Total # of Episodes: " + (s.PreviousEpisodes + s.Episodes);

                    CurrentFactors[i] = (s.PreviousEpisodes - network.FactorAverages[i]) / network.PreviousEpisodeDeviation;

                    NewOdds = network.model.GetModifiedOdds(s, CurrentFactors);

                    detailValue = NewOdds - CurrentOdds;

                    CurrentOdds = NewOdds;

                    details.Add(new DetailsContainer(detailName, detailValue));
                }
                else if (i < FactorCount && (network.factors[i] == "Spring" || network.factors[i] == "Summer" || network.factors[i] == "Fall") && !AllFactors)
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
                else if (i < FactorCount && (network.factors[i] == "Not Original" || network.factors[i] == "CBS Show" || network.factors[i] == "WB Show") && !AllFactors)
                {
                    if (!OwnedFinished)
                    {
                        if (s.factorNames.Contains("CBS Show") && s.factorNames.Contains("WB Show") && s.factorNames.Contains("Not Original"))
                        {
                            int index = s.factorNames.IndexOf("Not Original"), index2 = s.factorNames.IndexOf("CBS Show"), index3 = s.factorNames.IndexOf("WB Show");
                            bool NotOriginal = s.factorValues[index], CBSShow = s.factorValues[index2], WBShow = s.factorValues[index3];
                            CurrentFactors[index] = (NotOriginal ? 1 : -1) - network.FactorAverages[index];
                            CurrentFactors[index2] = (CBSShow ? 1 : -1) - network.FactorAverages[index2];
                            CurrentFactors[index3] = (WBShow ? 1 : -1) - network.FactorAverages[index3];

                            if (CBSShow)
                                detailName = "Show is owned by CBS";
                            else if (WBShow)
                                detailName = "Show is owned by WB";
                            else if (!NotOriginal)
                                detailName = "Show is owned by the network";
                            else
                                detailName = "Show is not owned by the network";


                            //if (NotOriginal)
                            //    detailName = "Show is not owned by the network";
                            //else if (CBSShow)
                            //    detailName = "Show is owned by CBS";
                            //else
                            //    detailName = "Show is owned by WB";

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
                else if (i < FactorCount)
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
                        case "Foreign":
                            {
                                if (s.factorValues[i])
                                    detailName = "Show is produced outside the United States";
                                else
                                    detailName = "Show is produced in the United States";
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

                var FactorCount = network.factors.Count;
                int Iterations = 20000; //Enough for precision within 0.01%

                var AllResults = new DetailsCombo[Iterations];
                var Random = new Random();

                var Numbers = new int[FactorCount+5];
                Numbers[0] = FactorCount + 2;
                Numbers[1] = FactorCount;
                Numbers[2] = FactorCount + 3;
                Numbers[3] = FactorCount + 1;
                Numbers[4] = FactorCount + 4;

                var InputCount = FactorCount + 5;

                for (int i = 5; i < InputCount; i++)
                    Numbers[i] = i - 5;

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

                ///Determine Syndication Status
                ///
                if (FactorNames.Contains("Syndication"))
                {
                    var SyndicationIndex = FactorNames.IndexOf("Syndication");
                    var inputs = network.model.GetInputs(s);
                    var AvgInputs = network.model.FactorBias;
                    var BaseInputs = inputs.ToArray();
                    BaseInputs[FactorCount + 2] = AvgInputs[FactorCount + 2];
                    BaseInputs[FactorCount] = AvgInputs[FactorCount];
                    BaseInputs[FactorCount + 3] = AvgInputs[FactorCount + 3];

                    //Create new list representing odds for each season
                    var AllSeasonOdds = new List<double>();

                    //First, find the lowest and highest # Season that exists in the database
                    var AllSeasons = network.shows.Where(x => x.Name == s.Name).OrderBy(x => x.Season);
                    var LowestSeason = AllSeasons.First();
                    var HighestSeason = AllSeasons.Last();

                    var EpisodesPerSeason = AllSeasons.Select(x => x.Episodes).Sum() / AllSeasons.Count();
                    var CurrentYear = LowestSeason.year - (LowestSeason.Season - 1);
                    var PreviousEpisodes = 0;

                    //If there are missing seasons, extrapolate episode numbers and years, and determine odds based on that
                    if (LowestSeason.Season > 1)
                    {
                        EpisodesPerSeason = LowestSeason.PreviousEpisodes / (LowestSeason.Season - 1);

                        for (int i = 0; i < LowestSeason.Season - 1; i++)
                        {
                            var ModifiedInputs = inputs.ToArray();
                            ModifiedInputs[FactorCount + 2] = (i + 1 - network.FactorAverages[FactorCount + 2]) / network.SeasonDeviation;
                            ModifiedInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;
                            ModifiedInputs[FactorCount] = (EpisodesPerSeason / 26.0 * 2 - 1) - network.FactorAverages[FactorCount];

                            ModifiedInputs[FactorCount + 3] = (PreviousEpisodes - network.FactorAverages[FactorCount + 3]) / network.PreviousEpisodeDeviation;
                            PreviousEpisodes += EpisodesPerSeason;

                            BaseInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;

                            AllSeasonOdds.Add(network.model.GetModifiedOdds(s, ModifiedInputs) - network.model.GetModifiedOdds(s, BaseInputs));
                            CurrentYear++;
                        }
                    }

                    //Add any existing seasons to the list, interpolating when necessary
                    for (int i = LowestSeason.Season; i < HighestSeason.Season + 1; i++)
                    {
                        var ModifiedInputs = inputs.ToArray();

                        var MatchedSeason = network.shows.Where(x => x.Name == s.Name && x.Season == i);

                        if (MatchedSeason.Count() == 0)
                        {
                            ModifiedInputs[FactorCount + 2] = (i - network.FactorAverages[FactorCount + 2]) / network.SeasonDeviation;
                            ModifiedInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;
                            ModifiedInputs[FactorCount] = (EpisodesPerSeason / 26.0 * 2 - 1) - network.FactorAverages[FactorCount];

                            ModifiedInputs[FactorCount + 3] = (PreviousEpisodes - network.FactorAverages[FactorCount + 3]) / network.PreviousEpisodeDeviation;
                            PreviousEpisodes += EpisodesPerSeason;

                            BaseInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;

                            AllSeasonOdds.Add(network.model.GetModifiedOdds(s, ModifiedInputs) - network.model.GetModifiedOdds(s, BaseInputs));
                            CurrentYear++;
                        }
                        else if (i == s.Season)
                        {
                            BaseInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;

                            //AllSeasonOdds.Add(network.model.GetModifiedOdds(s, ModifiedInputs) - network.model.GetModifiedOdds(s, ModifiedInputs));

                            AllSeasonOdds.Add(Results.CurrentOdds - network.model.GetModifiedOdds(s, BaseInputs));
                            PreviousEpisodes = s.PreviousEpisodes + s.Episodes;
                            EpisodesPerSeason = PreviousEpisodes / i;
                            CurrentYear = s.year + 1;
                        }
                        else
                        {
                            CurrentYear = MatchedSeason.First().year;
                            EpisodesPerSeason = MatchedSeason.First().Episodes;

                            ModifiedInputs[FactorCount + 2] = (i + 1 - network.FactorAverages[FactorCount + 2]) / network.SeasonDeviation;
                            ModifiedInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;
                            ModifiedInputs[FactorCount] = (EpisodesPerSeason / 26.0 * 2 - 1) - network.FactorAverages[FactorCount];

                            PreviousEpisodes = MatchedSeason.First().PreviousEpisodes;
                            ModifiedInputs[FactorCount + 3] = (PreviousEpisodes - network.FactorAverages[FactorCount + 3]) / network.PreviousEpisodeDeviation;
                            PreviousEpisodes += EpisodesPerSeason;

                            BaseInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;

                            AllSeasonOdds.Add(network.model.GetModifiedOdds(s, ModifiedInputs) - network.model.GetModifiedOdds(s, BaseInputs));
                            EpisodesPerSeason = PreviousEpisodes / i;

                            CurrentYear++;
                        }
                    }

                    var MaxEpisodes = network.shows.Select(x => x.PreviousEpisodes).Max();

                    //Check if additional seasons should be added

                    if (PreviousEpisodes + EpisodesPerSeason < MaxEpisodes)
                    //if (PreviousEpisodes + EpisodesPerSeason < 100)
                    {
                        var MaximumSeason = (MaxEpisodes - PreviousEpisodes) / EpisodesPerSeason + AllSeasonOdds.Count;
                        if (MaximumSeason > HighestSeason.Season)
                        {
                            for (int i = HighestSeason.Season + 1; i < MaximumSeason+1 && PreviousEpisodes < MaxEpisodes; i++)
                            {
                                var ModifiedInputs = inputs.ToArray();

                                ModifiedInputs[FactorCount + 2] = (i + 1 - network.FactorAverages[FactorCount + 2]) / network.SeasonDeviation;
                                ModifiedInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;
                                ModifiedInputs[FactorCount] = (EpisodesPerSeason / 26.0 * 2 - 1) - network.FactorAverages[FactorCount];

                                ModifiedInputs[FactorCount + 3] = (PreviousEpisodes - network.FactorAverages[FactorCount + 3]) / network.PreviousEpisodeDeviation;
                                PreviousEpisodes += EpisodesPerSeason;

                                BaseInputs[FactorCount + 4] = (CurrentYear - network.FactorAverages[FactorCount + 4]) / network.YearDeviation;

                                AllSeasonOdds.Add(network.model.GetModifiedOdds(s, ModifiedInputs) - network.model.GetModifiedOdds(s, BaseInputs));
                                CurrentYear++;
                            }
                        }
                    }

                    var SyndicationSeason = 0; 
                    bool SeasonFound = false;
                    for (int i = 0; i < AllSeasonOdds.Count-1 && !SeasonFound; i++)
                        if ((AllSeasonOdds[i] > AllSeasonOdds[i + 1] && AllSeasonOdds[i] > 0) || (i>0 && AllSeasonOdds[i] > AllSeasonOdds[i-1] && (AllSeasonOdds[i]>0 || AllSeasonOdds[i] > AllSeasonOdds[i+1])))
                        {
                            SeasonFound = true;
                            SyndicationSeason = Math.Min(i + 2, AllSeasonOdds.Count);
                        }

                    if (!SeasonFound)
                        SyndicationSeason = Math.Min(AllSeasonOdds.IndexOf(AllSeasonOdds.Max()) + 2, AllSeasonOdds.Count);

                    string SyndicationStatus;

                    if (s.Season == SyndicationSeason)
                        SyndicationStatus = (s.year == NetworkDatabase.MaxYear) ? "Will likely be syndicated this season" : "Was likely syndicated this season";
                    else if (s.Season == SyndicationSeason - 1)
                        SyndicationStatus = "Will likely be syndicated next season";
                    else if (s.Season < SyndicationSeason)
                        SyndicationStatus = "Will likely be syndicated in Season " + SyndicationSeason;
                    else
                        SyndicationStatus = "Was likely syndicated in Season " + SyndicationSeason;

                    Results.details[SyndicationIndex].Name = SyndicationStatus;

                }


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

    


}
