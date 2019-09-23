using Microsoft.Toolkit.Uwp.UI.Controls;
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

        private void ShowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = (Show)ShowSelector.SelectedItem;
            
            if (s != null)
            {
                details.Clear();

                bool SyndicationFinished = false, OwnedFinished = false, PremiereFinished = false, SummerFinished = false;
                string detailName;
                double CurrentOdds = network.model.GetOdds(s), NewOdds, detailValue;

                var tempList = network.shows.OrderBy(x => x.Episodes).ToList();
                int LowestEpisode = tempList.First().Episodes, HighestEpisode = tempList.Last().Episodes;

                var AllOdds = new List<double>();

                for (int i = LowestEpisode - 1; i < HighestEpisode; i++)
                {
                    var tShow = new Show(s.Name, network, s.factorValues, i, s.Halfhour, s.factorNames)
                    {
                        ShowIndex = s.ShowIndex
                    };

                    AllOdds.Add(network.model.GetOdds(tShow, false, true, -1));
                }

                var BaseOdds = AllOdds.Sum() / AllOdds.Count;

                


                for (int i = 0; i < network.factors.Count; i++)
                {
                    if ((network.factors[i] == "Syndication" || network.factors[i] == "Post-Syndication"))
                    {
                        if (!SyndicationFinished)
                        {
                            bool Syndication = false;
                            bool PostSyndication = false;
                            for (int x = 0; x < network.factors.Count; x++)
                            {
                                if (network.factors[x] == "Syndication")
                                    Syndication = s.factorValues[x];
                                else if (network.factors[x] == "Post-Syndication")
                                    PostSyndication = s.factorValues[x];
                            }

                            ObservableCollection<bool> factors1 = new ObservableCollection<bool>(), factors2 = new ObservableCollection<bool>();

                            if (Syndication)
                            {
                                detailName = "Will be syndicated next season";

                                for (int x = 0; x < network.factors.Count; x++)
                                {
                                    if (network.factors[x] == "Syndication")
                                    {
                                        factors1.Add(false);
                                        factors2.Add(false);
                                    }
                                    else if (network.factors[x] == "Post-Syndication")
                                    {
                                        factors1.Add(false);
                                        factors2.Add(true);
                                    }
                                    else
                                    {
                                        factors1.Add(s.factorValues[x]);
                                        factors2.Add(s.factorValues[x]);
                                    }
                                }
                            }
                            else if (PostSyndication)
                            {
                                detailName = "Has already been syndicated";

                                for (int x = 0; x < network.factors.Count; x++)
                                {
                                    if (network.factors[x] == "Syndication")
                                    {
                                        factors1.Add(false);
                                        factors2.Add(true);
                                    }
                                    else if (network.factors[x] == "Post-Syndication")
                                    {
                                        factors1.Add(false);
                                        factors2.Add(false);
                                    }
                                    else
                                    {
                                        factors1.Add(s.factorValues[x]);
                                        factors2.Add(s.factorValues[x]);
                                    }
                                }
                            }
                            else
                            {
                                detailName = "Not syndicated yet";

                                for (int x = 0; x < network.factors.Count; x++)
                                {
                                    if (network.factors[x] == "Syndication")
                                    {
                                        factors1.Add(true);
                                        factors2.Add(false);
                                    }
                                    else if (network.factors[x] == "Post-Syndication")
                                    {
                                        factors1.Add(false);
                                        factors2.Add(true);
                                    }
                                    else
                                    {
                                        factors1.Add(s.factorValues[x]);
                                        factors2.Add(s.factorValues[x]);
                                    }
                                }
                            }

                            int index = -1, index2 = -1;
                            if (s.factorNames.Contains("Syndication"))
                            {
                                index = s.factorNames.IndexOf("Syndication");
                                if (s.factorNames.Contains("Post-Syndication"))
                                    index2 = s.factorNames.IndexOf("Post-Syndication");
                            }
                            else
                            {
                                index = s.factorNames.IndexOf("Post-Syndication");
                            }

                            NewOdds = network.model.GetOdds(s, false, true, index, index2);

                            detailValue = CurrentOdds - NewOdds;

                            details.Add(new DetailsContainer(detailName, detailValue));

                            SyndicationFinished = true;
                        }
                    }
                    else if ((network.factors[i] == "Spring" || network.factors[i] == "Summer" || network.factors[i] == "Fall"))
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
                                detailName = !Spring ? "Premiered in the Fall" : "Fall Preview with a Premiere in the Spring";
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

                            NewOdds = network.model.GetOdds(s, false, true, index1, index2, index3);

                            detailValue = CurrentOdds - NewOdds;

                            details.Add(new DetailsContainer(detailName, detailValue));
                        }

                        if (network.factors[i] == "Summer" && !SummerFinished)
                        {
                            if (s.factorValues[i])
                                detailName = "Aired in the Summer";
                            else
                                detailName = "Did not air in the Summer";
                            NewOdds = network.model.GetOdds(s, false, true, i);

                            detailValue = CurrentOdds - NewOdds;

                            details.Add(new DetailsContainer(detailName, detailValue));

                            SummerFinished = true;
                        }
                    }
                    else if ((network.factors[i] == "Not Original" || network.factors[i] == "CBS Show"))
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

                                NewOdds = network.model.GetOdds(s, false, true, index, index2);
                            }
                            else
                            {
                                if (s.factorValues[i])
                                    detailName = "Show is not owned by the network";
                                else
                                    detailName = "Show is owned by the network";

                                NewOdds = network.model.GetOdds(s, false, true, i);
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
                            case "Limited Series":
                                {
                                    if (s.factorValues[i])
                                        detailName = "Limited Series";
                                    else
                                        detailName = "Not a Limited Series";

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

                        NewOdds = network.model.GetOdds(s, false, true, i);

                        detailValue = CurrentOdds - NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));
                    }
                }

                


                if (s.Halfhour)
                    detailName = "Half hour show";
                else
                    detailName = "Hour long show";

                NewOdds = network.model.GetOdds(s, false, true, s.factorNames.Count + 1);
                detailValue = CurrentOdds - NewOdds;
                details.Add(new DetailsContainer(detailName, detailValue));
                double max = 0;
                int peak = 0;

                var OddsByEpisode = new double[26];
                for (int i = LowestEpisode - 1; i < HighestEpisode - 1; i++)
                {
                    var tShow = new Show(s.Name, network, s.factorValues, i, s.Halfhour, s.factorNames)
                    {
                        ShowIndex = s.ShowIndex
                    };

                    OddsByEpisode[i] = network.model.GetOdds(tShow);

                    if (OddsByEpisode[i] >= max)
                    {
                        max = OddsByEpisode[i];
                        peak = i + 1;
                    }
                }

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

                double total = 0;
                int count = 0;
                for (int i = 0; i < 26; i++)
                    if (i < low - 1 || i >= high)
                    {
                        total += OddsByEpisode[i];
                        count++;
                    }

                NewOdds = (count > 0) ? total / count : CurrentOdds;

                if ((low == 1 && high == 26) || (low == high) || (NewOdds == CurrentOdds))
                    detailName = s.Episodes + " episodes ordered";
                else if (low == 1)
                    detailName = "Less than " + (high + 1) + " episodes ordered";
                else if (high == 26)
                    detailName = "More than " + (low - 1) + " episodes ordered";
                else
                    detailName = s.Episodes + " episodes ordered (between " + low + " and " + high + " episodes)";

                
                Optimal.Text = "Optimal # of episodes for " + s.Name + ": " + peak;


                detailValue = CurrentOdds - NewOdds;

                details.Add(new DetailsContainer(detailName, detailValue));

                double change = 0;
                foreach (DetailsContainer d in details)
                    change += d.Value;

                double multiplier;

                if (change != 0 && change != (CurrentOdds - BaseOdds))
                {
                    multiplier = (CurrentOdds - BaseOdds) / change;

                    if (multiplier < 0)
                    {
                        double ex = Math.Log(CurrentOdds) / Math.Log(BaseOdds);
                        BaseOdds = Math.Pow(CurrentOdds, ex);
                    }
                        //foreach (DetailsContainer d in details)
                        //    d.Value *= -1;
                }

                Base.Text = "Base Odds: " + BaseOdds.ToString("P");

                double oldEx = 0.01, exponent = 0.01;
                double oldChange = 0;
                foreach (DetailsContainer d in details)
                {
                    if (d.Value > 0)
                        oldChange += Math.Pow(d.Value, 0.01);
                    else
                        oldChange -= Math.Pow(-d.Value, 0.01);
                }

                bool found = false;

                while (!found)
                {
                    //oldEx = newEx;
                    change = 0;
                    oldEx = exponent;
                    exponent += 0.01;
                    foreach (DetailsContainer d in details)
                    {
                        if (d.Value > 0)
                            change += Math.Pow(d.Value, exponent);
                        else
                            change -= Math.Pow(-d.Value, exponent);
                    }


                    if (change != 0 && change != (CurrentOdds - BaseOdds))
                        multiplier = (CurrentOdds - BaseOdds) / change;
                    else
                        multiplier = 1;

                    if (Math.Abs(oldChange - (CurrentOdds - BaseOdds)) < Math.Abs(change - (CurrentOdds - BaseOdds)))
                    {
                        found = true;
                        exponent = oldEx;
                    }
                    else
                        oldChange = change;

                    //if (multiplier > 1)
                    //    newEx = -0.01;
                    //else if (multiplier < 1)
                    //    newEx = 0.01;
                    //else
                    //    newEx = 0;

                    //if (oldEx == 0)
                    //    oldEx = newEx;

                    //exponent += 0.01;
                }

                foreach (DetailsContainer d in details)
                {
                    if (d.Value > 0)
                        d.Value = Math.Pow(d.Value, exponent);
                    else
                        d.Value = -Math.Pow(-d.Value, exponent);
                }                    

                //change = 0;
                //foreach (DetailsContainer d in details)
                //    change += d.Value;

                //if (change != 0 && change != (CurrentOdds - BaseOdds))
                //{
                //    multiplier = (CurrentOdds - BaseOdds) / change;

                //    foreach (DetailsContainer d in details)
                //        d.Value *= multiplier;
                //}

                ShowName.Text = s.Name;
                Odds.Text = "Predicted Odds: " + s.PredictedOdds.ToString("P");

                if (s.Renewed || s.Canceled)
                {
                    if ((s.Renewed && CurrentOdds > 0.5) || (s.Canceled && CurrentOdds < 0.5))
                        Odds.Text += " ✔";
                    else
                        Odds.Text += " ❌";
                }
            }            

        }

        private async void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
            picker.SuggestedFileName = ((Show)ShowSelector.SelectedItem).Name;

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
                return Math.Round(_value, 4);
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


}
