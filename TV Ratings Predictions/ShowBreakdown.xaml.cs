using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
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
    public sealed partial class ShowBreakdown : Page
    {
        Network network;
        ObservableCollection<Show> shows;
        ObservableCollection<DetailsContainer> details;

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
            ShowDetails.ItemsSource = details;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            NetworkDatabase.canGoBack = false;
        }

        DetailsCombo GenerateDetails(Show s, Dictionary<int, double> Adjustments, bool AllFactors = false)
        {
            var details = new List<DetailsContainer>();

            bool SyndicationFinished = false, OwnedFinished = false, PremiereFinished = false, SummerFinished = false;
            string detailName;

            double CurrentOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year]), NewOdds, detailValue;

            var tempList = network.shows.OrderBy(x => x.Episodes).ToList();
            int LowestEpisode = tempList.First().Episodes, HighestEpisode = tempList.Last().Episodes;

            var BaseOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, -1);


            for (int i = 0; i < network.factors.Count; i++)
            {
                if ((network.factors[i] == "Syndication" || network.factors[i] == "Post-Syndication") && !AllFactors)
                {
                    if (!SyndicationFinished)
                    {
                        bool Syndication = false;
                        bool PostSyndication = false;
                        int SyndicationIndex = -1, PostIndex = -1;
                        for (int x = 0; x < network.factors.Count; x++)
                        {
                            if (network.factors[x] == "Syndication")
                            {
                                Syndication = s.factorValues[x];
                                SyndicationIndex = x;
                            }
                            else if (network.factors[x] == "Post-Syndication")
                            {
                                PostSyndication = s.factorValues[x];
                                PostIndex = x;
                            }
                        }

                        if (Syndication)
                            detailName = "Will be syndicated next season";
                        else if (PostSyndication)
                            detailName = "Has already been syndicated";
                        else
                            detailName = "Not syndicated yet";

                        int index = -1, index2 = -1;
                        if (s.factorNames.Contains("Syndication"))
                        {
                            index = s.factorNames.IndexOf("Syndication");
                            if (s.factorNames.Contains("Post-Syndication"))
                                index2 = s.factorNames.IndexOf("Post-Syndication");
                        }
                        else
                            index = s.factorNames.IndexOf("Post-Syndication");

                        NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, index, index2);

                        detailValue = CurrentOdds - NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));

                        SyndicationFinished = true;
                    }
                }
                else if ((network.factors[i] == "Spring" || network.factors[i] == "Summer" || network.factors[i] == "Fall") && !AllFactors)
                {
                    if (!PremiereFinished)
                    {
                        bool Spring = false, Summer = false, Fall = false;
                        int FallIndex = -1, SpringIndex = -1, SummerIndex = -1;

                        for (int x = 0; x < network.factors.Count; x++)
                        {
                            if (network.factors[x] == "Spring")
                            {
                                Spring = s.factorValues[x];
                                SpringIndex = x;
                            }
                            else if (network.factors[x] == "Summer")
                            {
                                Summer = s.factorValues[x];
                                SummerIndex = x;
                            }
                            else if (network.factors[x] == "Fall")
                            {
                                Fall = s.factorValues[x];
                                FallIndex = x;
                            }
                        }

                        int index1 = -1, index2 = -1, index3 = -1;

                        if (FallIndex > -1)
                        {
                            index1 = FallIndex;
                            index2 = SpringIndex;
                        }
                        else if (SpringIndex > -1)
                            index1 = SpringIndex;
                        else
                            index1 = SummerIndex;

                        if (Fall)
                            detailName = Spring ? "Fall Preview with a Premiere in the Spring" : "Premiered in the Fall";
                        else if (Spring)
                            detailName = "Premiered in the Spring";
                        else if (Summer)
                        {
                            detailName = "Premiered in the Summer";
                            SummerFinished = true;
                            if (index1 > -1 && index2 > -1)
                                index3 = SummerIndex;
                            else if (index1 > -1)
                                index2 = SummerIndex;
                            else
                                index1 = SummerIndex;
                        }
                        else
                            detailName = (FallIndex > -1) ? "Unknown Premiere Date" : "Premiered in the Fall";

                        PremiereFinished = true;

                        NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, index1, index2, index3);

                        detailValue = CurrentOdds - NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));
                    }

                    if (network.factors[i] == "Summer" && !SummerFinished)
                    {

                        if (s.factorValues[i])
                            detailName = "Aired in the Summer";
                        else
                            detailName = "Did not air in the Summer";

                        NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, i);

                        detailValue = CurrentOdds - NewOdds;

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

                            if (NotOriginal)
                                detailName = "Show is not owned by the network";
                            else if (CBSShow)
                                detailName = "Show is owned by CBS";
                            else
                                detailName = "Show is owned by WB";

                            NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, index, index2);
                        }
                        else
                        {
                            if (s.factorValues[i])
                                detailName = "Show is not owned by the network";
                            else
                                detailName = "Show is owned by the network";

                            NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, i);
                        }

                        detailValue = CurrentOdds - NewOdds;
                        details.Add(new DetailsContainer(detailName, detailValue));
                        OwnedFinished = true;
                    }
                }
                else
                {
                    switch (network.factors[i])
                    {
                        case "Friday":
                            {
                                if (s.factorValues[i])
                                    detailName = "Airs on Friday (or Saturday)";
                                else
                                    detailName = "Does not air on Friday (or Saturday)";

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
                        case "New Show":
                            {
                                if (s.factorValues[i])
                                    detailName = "New Series";
                                else
                                    detailName = "Returning Series";

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

                    if (detailName == "New Series")
                        NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, i, s.factorNames.Count + 2);
                    else
                        NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, i);

                    detailValue = CurrentOdds - NewOdds;

                    details.Add(new DetailsContainer(detailName, detailValue));
                }
            }




            if (s.Halfhour)
                detailName = "Half hour show";
            else
                detailName = "Hour long show";

            //var tempodds = network.model.GetOdds(new Show(s.Name, s.network, s.factorValues, s.Episodes, !s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);
            //int CurrentCount = network.shows.Where(x => x.Halfhour == s.Halfhour).Count(),
                //NewCount = network.shows.Where(x => x.Halfhour == !s.Halfhour).Count();

            //NewOdds = (tempodds * NewCount + CurrentOdds * CurrentCount) / (NewCount + CurrentCount);
            NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, s.factorNames.Count + 1);


            detailValue = CurrentOdds - NewOdds;
            details.Add(new DetailsContainer(detailName, detailValue));
            double max = 0;
            int peak = 0;

            var OddsByEpisode = new double[26];
            double total = 0;
            int count = 0;

            for (int i = LowestEpisode - 1; i < HighestEpisode; i++)
            {
                var tShow = new Show(s.Name, network, s.Season, s.factorValues, i, s.Halfhour, s.factorNames)
                {
                    AverageRating = s.AverageRating,
                    year = s.year,
                    ShowIndex = s.ShowIndex
                };

                OddsByEpisode[i] = network.model.GetOdds(tShow, network.FactorAverages, Adjustments[tShow.year]);
                var c = network.shows.Where(x => x.Episodes == i + 1).Count();
                total += OddsByEpisode[i] * c;
                count += c;

                if (OddsByEpisode[i] >= max)
                {
                    max = OddsByEpisode[i];
                    peak = i + 1;
                }
            }

            NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, s.factorNames.Count);


            int low = s.Episodes, high = s.Episodes;
            bool foundLow = false, foundHigh = false;


            for (int i = s.Episodes - 1; i < HighestEpisode && !foundHigh; i++)
            {

                if (OddsByEpisode[i] == OddsByEpisode[s.Episodes - 1])
                    high = i + 1;
                else
                    foundHigh = true;
            }
            for (int i = s.Episodes - 1; i >= LowestEpisode - 1 && !foundLow; i--)
            {

                if (OddsByEpisode[i] == OddsByEpisode[s.Episodes - 1])
                    low = i + 1;
                else
                    foundLow = true;
            }


            if ((low == 1 && high == 26) || (low == high) || (NewOdds == CurrentOdds))
                detailName = s.Episodes + " episodes ordered";
            else if (low == 1)
                detailName = "Less than " + (high + 1) + " episodes ordered";
            else if (high == 26)
                detailName = "More than " + (low - 1) + " episodes ordered";
            else
                detailName = s.Episodes + " episodes ordered (between " + low + " and " + high + " episodes)";


            detailValue = CurrentOdds - NewOdds;

            details.Add(new DetailsContainer(detailName, detailValue));

            if (s.Season > 1)
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

                NewOdds = network.model.GetOdds(s, network.FactorAverages, Adjustments[s.year], false, true, s.factorNames.Count + 2);
                detailValue = CurrentOdds - NewOdds;
                details.Add(new DetailsContainer(detailName, detailValue));
            }            

            double change = 0;
            foreach (DetailsContainer d in details)
                change += d.Value;

            double multiplier = change != 0 ? (CurrentOdds - BaseOdds) / change : 1;
            if (Math.Round(Math.Abs(CurrentOdds - BaseOdds), 4) == 0) multiplier = 0;
            bool BaseReverse = false;

            if (change != 0 && change != (CurrentOdds - BaseOdds))
            {
                if (multiplier < 0)
                {
                    double ex = Math.Log(CurrentOdds) / Math.Log(BaseOdds);
                    BaseOdds = Math.Pow(CurrentOdds, ex);
                    multiplier = change != 0 ? (CurrentOdds - BaseOdds) / change : 1;
                    BaseReverse = true;
                }
            }

            if (BaseReverse)
            {
                var hashes = network.shows.Select(x => x.FactorHash).Distinct().ToList();
                var totalOdds = new double[hashes.Count];
                var counts = new int[hashes.Count];
                Parallel.For(0, hashes.Count, x =>
                {
                    var list = network.shows.Where(y => y.FactorHash == hashes[x]);
                    var show = list.First();
                    var c = list.Count();
                    counts[x] = c;
                    totalOdds[x] = network.model.GetOdds(new Show(s.Name, s.network, s.Season, s.factorValues, s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex, year = s.year, AverageRating = s.AverageRating }, network.FactorAverages, Adjustments[s.year]);
                });

                var bo = totalOdds.Sum() / counts.Sum();
                if ((CurrentOdds - bo) / change > 0)
                {
                    BaseOdds = bo;
                    BaseReverse = false;
                }
                else
                {
                    var o = CurrentOdds - change;
                    if (o > 0 && o < 1)
                    {
                        BaseOdds = o;
                        BaseReverse = false;
                    }
                }
            }


            multiplier = change != 0 ? (CurrentOdds - BaseOdds) / change : 1;
            if (Math.Round(Math.Abs(CurrentOdds - BaseOdds), 4) == 0) multiplier = 0;
            double oldEx = 1, exponent = 1, increment = (multiplier < 1) ? 0.01 : -0.01;

            double oldChange = change;

            change = 0;
            foreach (DetailsContainer d in details)
            {
                if (d.Value > 0)
                    change += Math.Pow(d.Value, oldEx + increment);
                else
                    change -= Math.Pow(-d.Value, oldEx + increment);
            }
            if (Math.Abs(oldChange - (CurrentOdds - BaseOdds)) < Math.Abs(change - (CurrentOdds - BaseOdds)))
                increment *= -1;

            bool found = false;

            while (!found && multiplier != 0)
            {
                //oldEx = newEx;
                change = 0;
                oldEx = exponent;
                exponent += increment;
                foreach (DetailsContainer d in details)
                {
                    if (d.Value > 0)
                        change += Math.Pow(d.Value, exponent);
                    else
                        change -= Math.Pow(-d.Value, exponent);
                }

                if (Math.Abs(oldChange - (CurrentOdds - BaseOdds)) < Math.Abs(change - (CurrentOdds - BaseOdds)))
                {
                    found = true;
                    exponent = oldEx;
                }
                else
                    oldChange = change;

                if (exponent == 0.01) found = true;
            }

            foreach (DetailsContainer d in details)
            {
                if (d.Value > 0)
                    d.Value = Math.Pow(d.Value, exponent);
                else
                    d.Value = -Math.Pow(-d.Value, exponent);
            }

            change = 0;
            foreach (DetailsContainer d in details)
                change += d.Value;

            if (change != 0 && change != (CurrentOdds - BaseOdds))
            {
                multiplier = change != 0 ? (CurrentOdds - BaseOdds) / change : 1;
                if (Math.Round(Math.Abs(CurrentOdds - BaseOdds), 4) == 0) multiplier = 0;

                foreach (DetailsContainer d in details)
                    d.Value *= multiplier;
            }

            if (details.Select(x => Math.Abs(x.Value)).Sum() == 0) BaseOdds = CurrentOdds;

            return new DetailsCombo(details, BaseOdds, CurrentOdds, peak);
        }

        private void ShowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = (Show)ShowSelector.SelectedItem;
            
            if (s != null)
            {
                details.Clear();

                var Adjustments = network.model.GetAdjustments(true);
                var Results = GenerateDetails(s, Adjustments);
                foreach (DetailsContainer d in Results.details)
                    details.Add(d);             

                for (int i = details.Count - 1; i >= 0; i--)
                    if (Math.Round(details[i].Value, 4) == 0)
                        details.RemoveAt(i);

                ShowName.Text = s.Name;
                Odds.Text = "Predicted Odds: " + Results.CurrentOdds.ToString("P");
                Base.Text = "Base Odds: " + Results.BaseOdds.ToString("P");
                Optimal.Text = "Optimal # of episodes for " + s.Name + ": " + Results.OptimalEpisodes;

                if (s.Renewed || s.Canceled)
                {
                    if ((s.Renewed && Results.CurrentOdds > 0.5) || (s.Canceled && Results.CurrentOdds < 0.5))
                        Odds.Text += " ✔";
                    else
                        Odds.Text += " ❌";
                }
            }            

        }

        private async void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Desktop, SuggestedFileName = ((Show)ShowSelector.SelectedItem).Name };
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

        private async void OptimalSearch_Click(object sender, RoutedEventArgs e)
        {
            var s = (Show)ShowSelector.SelectedItem;

            if (s != null)
            {
                var Factors = new bool[s.factorNames.Count];
                for (int i = 0; i < s.factorNames.Count; i++)
                    Factors[i] = s.factorValues[i];
                var HalfHour = s.Halfhour;
                var EpisodeCount = s.Episodes;
                var Adjustments = network.model.GetAdjustments(true);

                var NewShow = new Show(s.Name, network, s.Season, new ObservableCollection<bool>(Factors), EpisodeCount, HalfHour, s.factorNames) { ShowIndex = s.ShowIndex, year = s.year, AverageRating = s.AverageRating };
                string TextResult = "Optimal factors for " + s.Name + "\r\n\r\n\r\n";
                bool IsOptimal = false;
                var hashes = new List<long>();

                var AlreadyProcessed = new bool[s.factorNames.Count + 2];
                while (!IsOptimal)
                {
                    var Results = GenerateDetails(NewShow, Adjustments, true);
                    double minimum = 0;
                    int minIndex = 0;
                    IsOptimal = true;
                    for (int i = 0; i < Results.details.Count; i++)
                    {
                        var d = Results.details[i];
                        if (d.Value < 0)
                        {
                            IsOptimal = false;
                            if (d.Value < minimum)
                            {
                                if (!(i < s.factorNames.Count &&
                                    (s.factorNames[i] == "Syndication"
                                    || s.factorNames[i] == "Post-Syndication"
                                    || s.factorNames[i] == "Not Original"
                                    || s.factorNames[i] == "Extended Universe"
                                    || s.factorNames[i] == "New Show"
                                    || s.factorNames[i] == "CBS Show" || s.factorNames[i] == "Animated"
                                    || (s.factorNames[i] == "Fall" && s.factorValues[i] == true && s.factorValues[s.factorNames.IndexOf("Spring")] == false && s.factorValues[s.factorNames.IndexOf("Summer")] == false)
                                    || (s.factorNames[i] == "Spring" && s.factorValues[i] == true && s.factorValues[s.factorNames.IndexOf("Fall")] == false && s.factorValues[s.factorNames.IndexOf("Summer")] == false)
                                    || (s.factorNames[i] == "Summer" && s.factorValues[i] == true && s.factorValues[s.factorNames.IndexOf("Fall")] == false && s.factorValues[s.factorNames.IndexOf("Spring")] == false))))
                                {
                                    minimum = d.Value;
                                    minIndex = i;
                                }
                            }
                        }
                    }

                    var tempshow = new Show(s.Name, network, s.Season, new ObservableCollection<bool>(Factors), EpisodeCount, HalfHour, s.factorNames) { ShowIndex = s.ShowIndex, year = s.year, AverageRating = s.AverageRating };
                    var hash = tempshow.FactorHash;

                    if (!IsOptimal && !hashes.Contains(hash))
                    {
                        if (minIndex < s.factorNames.Count) //Factors
                            Factors[minIndex] = !Factors[minIndex];
                        else if (minIndex == s.factorNames.Count) //HalfHour
                            HalfHour = !HalfHour;
                        else //Episodes
                            EpisodeCount = Results.OptimalEpisodes;                        

                        AlreadyProcessed[minIndex] = true;

                        NewShow = tempshow;
                        hashes.Add(hash);
                    }
                    else
                    {
                        IsOptimal = true;
                        var details = GenerateDetails(s, Adjustments).details;
                        Results = GenerateDetails(NewShow, Adjustments);
                        var ExistingFactors = details.Select(x => x.Name).ToList();

                        for (int i = 0; i < Results.details.Count; i++)
                            TextResult += (ExistingFactors.Contains(Results.details[i].Name) ? "" : "* ") + Results.details[i].Name + "\r\n\r\n";

                        TextResult += "\r\nNew Odds: " + network.model.GetOdds(NewShow, network.FactorAverages, Adjustments[s.year]).ToString("P");
                    }
                        
                }

                ContentDialog dialog = new ContentDialog
                {
                    PrimaryButtonText = "OK",
                    Content = TextResult
                };

                await dialog.ShowAsync();
            }
        }
    }

    public class DetailsContainer : INotifyPropertyChanged
    {
        public String Name;
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
        public int OptimalEpisodes;
        public double BaseOdds, CurrentOdds;

        public DetailsCombo(List<DetailsContainer> d, double b, double c, int o)
        {
            details = d;
            BaseOdds = b;
            CurrentOdds = c;
            OptimalEpisodes = o;
        }
    }


}
