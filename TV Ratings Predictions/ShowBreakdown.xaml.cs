using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            details.Clear();

            bool SyndicationFinished = false;
            string detailName;
            double CurrentOdds = network.model.GetOdds(s), NewOdds, detailValue;

            Show tempShow;

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

                        Show
                            tempshow1 = new Show(s.Name, network, factors1, s.Episodes, s.Halfhour, s.factorNames)
                            {
                                ShowIndex = s.ShowIndex
                            },
                            tempshow2 = new Show(s.Name, network, factors2, s.Episodes, s.Halfhour, s.factorNames)
                            {
                                ShowIndex = s.ShowIndex
                            };

                        NewOdds = (network.model.GetOdds(tempshow1) + network.model.GetOdds(tempshow2)) / 2;

                        detailValue = CurrentOdds - NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));

                        SyndicationFinished = true;
                    }                    
                }
                else if ((network.factors[i] == "Spring" || network.factors[i] == "Summer"))
                {
                    bool Spring = false;
                    bool Summer = false;

                    for (int x = 0; x < network.factors.Count; x++)
                    {
                        if (network.factors[x] == "Spring")
                            Spring = s.factorValues[x];
                        else if (network.factors[x] == "Summer")
                            Summer = s.factorValues[x];
                    }

                    if (network.factors[i] == "Spring")
                    {
                        if (Spring)
                            detailName = "Premiered in the Spring";
                        else if (Summer)
                            detailName = "Did not premiere in the Spring";
                        else
                            detailName = "Premiered in the Fall";
                    }
                    else
                    {
                        if (Summer)
                            detailName = "Aired in the Summer";
                        else
                            detailName = "Did not air in the Summer";
                    }

                    var factors = new ObservableCollection<bool>();
                    for (int x = 0; x < s.factorValues.Count; x++)
                    {
                        if (x == i)
                            factors.Add(!s.factorValues[i]);
                        else
                            factors.Add(s.factorValues[i]);
                    }

                    tempShow = new Show(s.Name, network, factors, s.Episodes, s.Halfhour, s.factorNames)
                    {
                        ShowIndex = s.ShowIndex
                    };

                    NewOdds = network.model.GetOdds(tempShow);

                    detailValue = CurrentOdds - NewOdds;

                    details.Add(new DetailsContainer(detailName, detailValue));
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
                        case "Not Original":
                            {
                                if (s.factorValues[i])
                                    detailName = "Show is not owned by the network";
                                else
                                    detailName = "Show is owned by the network";

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
                        case "CBS Show":
                            {
                                if (s.factorValues[i])
                                    detailName = "Show is owned by CBS";
                                else
                                    detailName = "Show is not owned by CBS";

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

                    var factors = new ObservableCollection<bool>();
                    for (int x = 0; x < s.factorValues.Count; x++)
                    {
                        if (x == i)
                            factors.Add(!s.factorValues[i]);
                        else
                            factors.Add(s.factorValues[i]);
                    }

                    tempShow = new Show(s.Name, network, factors, s.Episodes, s.Halfhour, s.factorNames)
                    {
                        ShowIndex = s.ShowIndex
                    };

                    NewOdds = network.model.GetOdds(tempShow);

                    detailValue = CurrentOdds - NewOdds;

                    details.Add(new DetailsContainer(detailName, detailValue));
                }
            }

            if (s.Halfhour)
                detailName = "Half hour show";
            else
                detailName = "Hour long show";

            tempShow = new Show(s.Name, network, s.factorValues, s.Episodes, !s.Halfhour, s.factorNames)
            {
                ShowIndex = s.ShowIndex
            };

            NewOdds = network.model.GetOdds(tempShow);
            detailValue = CurrentOdds - NewOdds;
            details.Add(new DetailsContainer(detailName, detailValue));

            var OddsByEpisode = new double[26];
            Parallel.For(0, 26, i =>
            {
                var tShow = new Show(s.Name, network, s.factorValues, i, s.Halfhour, s.factorNames)
                {
                    ShowIndex = s.ShowIndex
                };

                OddsByEpisode[i] = network.model.GetOdds(tShow);
            });

            int low = s.Episodes, high = s.Episodes;
            bool foundLow = false, foundHigh = false;

            for (int i = s.Episodes - 1; i < 26 && !foundHigh; i++)
            {
                if (OddsByEpisode[i] == OddsByEpisode[s.Episodes - 1])
                    high = i + 1;
                else
                    foundHigh = true;
            }
            for (int i = s.Episodes - 1; i > -1 && !foundLow; i--)
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
                detailName = s.Episodes + " episodes ordered (betwwen " + low + " and " + high + " episodes)";

            
            detailValue = CurrentOdds - NewOdds;
            details.Add(new DetailsContainer(detailName, detailValue));

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

    public class DetailsContainer
    {
        public String Name;
        double _value;
        public double Value
        {
            get
            {
                return Math.Round(_value, 4);
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
