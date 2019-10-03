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

        private void ShowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = (Show)ShowSelector.SelectedItem;
            
            if (s != null)
            {
                details.Clear();

                bool SyndicationFinished = false, OwnedFinished = false, PremiereFinished = false, SummerFinished = false;
                string detailName;
                var Adjustments = network.model.GetAdjustments(true);
                double CurrentOdds = network.model.GetOdds(s, Adjustments[s.year]), NewOdds, detailValue;

                var tempList = network.shows.OrderBy(x => x.Episodes).ToList();
                int LowestEpisode = tempList.First().Episodes, HighestEpisode = tempList.Last().Episodes;

                //var AllOdds = new List<double>();

                //for (int i = LowestEpisode - 1; i < HighestEpisode; i++)
                //{
                //    var tShow = new Show(s.Name, network, s.factorValues, i, s.Halfhour, s.factorNames)
                //    {
                //        ShowIndex = s.ShowIndex
                //    };

                //    AllOdds.Add(network.model.GetOdds(tShow, Adjustments[tShow.year], false, true, -1));
                //}

                //var BaseOdds = AllOdds.Sum() / AllOdds.Count;

                var BaseOdds = network.model.GetOdds(s, Adjustments[s.year], false, true, -1);

                double AverageTotal = 0;
                int AverageCount = 0;


                for (int i = 0; i < network.factors.Count; i++)
                {
                    if ((network.factors[i] == "Syndication" || network.factors[i] == "Post-Syndication"))
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

                            var Show1 = new Show(s.Name, s.network, factors1, s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex };

                            var Show2 = new Show(s.Name, s.network, factors2, s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex };


                            double odds1 = network.model.GetOdds(Show1, Adjustments[s.year]),
                                odds2 = network.model.GetOdds(Show2, Adjustments[s.year]);

                            int count1 = network.shows.Where(x => x.factorValues[SyndicationIndex] == factors1[SyndicationIndex] && x.factorValues[PostIndex] == factors1[PostIndex]).Count(),
                                count2 = network.shows.Where(x => x.factorValues[SyndicationIndex] == factors2[SyndicationIndex] && x.factorValues[PostIndex] == factors2[PostIndex]).Count(),
                                count3 = network.shows.Where(x => x.factorValues[SyndicationIndex] == s.factorValues[SyndicationIndex] && x.factorValues[PostIndex] == s.factorValues[PostIndex]).Count();

                            NewOdds = (odds1 * count1 + odds2 * count2 + CurrentOdds * count3) / (count1 + count2 + count3);
                            //NewOdds = network.model.GetOdds(s, Adjustments[s.year], false, true, index, index2);
                            AverageTotal += NewOdds;
                            AverageCount++;

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

                            int count1, count2, count3, count4;
                            double odds1, odds2, odds3;

                            //Define all Factor lists
                            var c = s.factorValues.Count;
                            bool[] factors1 = new bool[c], factors2 = new bool[c], factors3 = new bool[c];

                            for (int x = 0; x < c; x++)
                            {
                                factors1[x] = s.factorValues[x];
                                factors2[x] = s.factorValues[x];
                                factors3[x] = s.factorValues[x];
                            }

                            if (Fall)
                            {
                                if (Spring)
                                {
                                    detailName = "Fall Preview with a Premiere in the Spring";

                                    //Premiered in the summer
                                    factors1[FallIndex] = false;
                                    factors1[SpringIndex] = false;
                                    factors1[SummerIndex] = true;
                                    count1 = network.shows.Where(x => x.factorValues[FallIndex] == true && x.factorValues[SpringIndex] == false && x.factorValues[SummerIndex] == true).Count();
                                    odds1 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors1), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                    //Premiered in the Fall (no spring)
                                    factors2[FallIndex] = true;
                                    factors2[SpringIndex] = false;
                                    count2 = network.shows.Where(x => x.factorValues[FallIndex] == true && x.factorValues[SpringIndex] == false).Count();
                                    odds2 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors2), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                    //Premiered in the Spring
                                    factors3[FallIndex] = false;
                                    factors3[SpringIndex] = true;
                                    count3 = network.shows.Where(x => x.factorValues[FallIndex] == false && x.factorValues[SpringIndex] == true).Count();
                                    odds3 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors3), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                    count4 = network.shows.Where(x => x.factorValues[FallIndex] == true && x.factorValues[SpringIndex] == true).Count();
                                }
                                else
                                {
                                    detailName = "Premiered in the Fall";

                                    //Premiered in the summer
                                    factors1[FallIndex] = false;
                                    factors1[SpringIndex] = false;
                                    factors1[SummerIndex] = true;
                                    count1 = network.shows.Where(x => x.factorValues[FallIndex] == false && x.factorValues[SpringIndex] == false && x.factorValues[SummerIndex] == true).Count();
                                    odds1 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors1), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                    //Premiered in the Spring
                                    factors2[FallIndex] = false;
                                    factors2[SpringIndex] = true;
                                    count2 = network.shows.Where(x => x.factorValues[FallIndex] == false && x.factorValues[SpringIndex] == true).Count();
                                    odds2 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors2), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                    count3 = 0;
                                    odds3 = 0;

                                    count4 = network.shows.Where(x => x.factorValues[FallIndex] == true && x.factorValues[SpringIndex] == false).Count();
                                }
                            }                                
                            else if (Spring)
                            {
                                detailName = "Premiered in the Spring";

                                //Premiered in the summer
                                factors1[FallIndex] = false;
                                factors1[SpringIndex] = false;
                                factors1[SummerIndex] = true;
                                count1 = network.shows.Where(x => x.factorValues[FallIndex] == false && x.factorValues[SpringIndex] == false && x.factorValues[SummerIndex] == true).Count();
                                odds1 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors1), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                //Fall preview for Spring Premiere
                                factors2[FallIndex] = true;
                                factors2[SpringIndex] = true;
                                count2 = network.shows.Where(x => x.factorValues[FallIndex] == true && x.factorValues[SpringIndex] == true).Count();
                                odds2 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors2), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                //Premiered in the fall (no spring)
                                factors3[FallIndex] = true;
                                factors3[SpringIndex] = false;
                                count3 = network.shows.Where(x => x.factorValues[FallIndex] == true && x.factorValues[SpringIndex] == false).Count();
                                odds3 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors3), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                count4 = network.shows.Where(x => x.factorValues[FallIndex] == false && x.factorValues[SpringIndex] == true).Count();
                            }
                            else
                            {
                                detailName = "Premiered in the Summer";
                                SummerFinished = true;
                                if (index1 > -1 && index2 > -1)
                                    index3 = SummerIndex;
                                else if (index1 > -1)
                                    index2 = SummerIndex;
                                else
                                    index1 = SummerIndex;

                                //Premiered in the Fall (no spring)
                                factors1[FallIndex] = true;
                                factors1[SpringIndex] = false;
                                count1 = network.shows.Where(x => x.factorValues[FallIndex] == true && x.factorValues[SpringIndex] == false).Count();
                                odds1 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors1), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                //Fall preview for Spring Premiere
                                factors2[FallIndex] = true;
                                factors2[SpringIndex] = true;
                                count2 = network.shows.Where(x => x.factorValues[FallIndex] == true && x.factorValues[SpringIndex] == true).Count();
                                odds2 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors2), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);
                                
                                //Premiered in the Spring
                                factors3[FallIndex] = false;
                                factors3[SpringIndex] = true;
                                count3 = network.shows.Where(x => x.factorValues[FallIndex] == false && x.factorValues[SpringIndex] == true).Count();
                                odds3 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors3), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                count4 = network.shows.Where(x => x.factorValues[FallIndex] == false && x.factorValues[SpringIndex] == false && x.factorValues[SummerIndex] == true).Count();
                            }                 

                            PremiereFinished = true;

                            //NewOdds = network.model.GetOdds(s, Adjustments[s.year], false, true, index1, index2, index3);
                            NewOdds = (odds1 * count1 + odds2 * count2 + odds3 * count3 + CurrentOdds * count4) / (count1 + count2 + count3 + count4);
                            AverageTotal += NewOdds;
                            AverageCount++;

                            detailValue = CurrentOdds - NewOdds;

                            details.Add(new DetailsContainer(detailName, detailValue));
                        }

                        if (network.factors[i] == "Summer" && !SummerFinished)
                        {

                            if (s.factorValues[i])
                                detailName = "Aired in the Summer";
                            else
                                detailName = "Did not air in the Summer";

                            ObservableCollection<bool> factors = new ObservableCollection<bool>();
                            foreach (bool b in s.factorValues)
                                factors.Add(b);

                            factors[i] = !factors[i];

                            var odds1 = network.model.GetOdds(new Show(s.Name, s.network, factors, s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);
                            int count1 = network.shows.Where(x => x.factorValues[i] == factors[i]).Count(),
                                count2 = network.shows.Where(x => x.factorValues[i] == s.factorValues[i]).Count();

                            //NewOdds = network.model.GetOdds(s, Adjustments[s.year], false, true, i);
                            NewOdds = (odds1 * count1 + CurrentOdds * count2) / (count1 + count2);
                            AverageTotal += NewOdds;
                            AverageCount++;
                            

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

                                //Define all Factor lists
                                var c = s.factorValues.Count;
                                bool[] factors1 = new bool[c], factors2 = new bool[c];

                                for (int x = 0; x < c; x++)
                                {
                                    factors1[x] = s.factorValues[x];
                                    factors2[x] = s.factorValues[x];
                                }

                                if (NotOriginal)
                                {
                                    detailName = "Show is not owned by the network";

                                    //Show is owned by CBS
                                    factors1[index] = false;
                                    factors1[index2] = true;

                                    //Show is owned by WB
                                    factors2[index] = false;
                                    factors2[index2] = false;
                                }                                    
                                else if (CBSShow)
                                {
                                    detailName = "Show is owned by CBS";

                                    //Show is not owned
                                    factors1[index] = true;
                                    factors1[index2] = false;

                                    //Show is owned by WB
                                    factors2[index] = false;
                                    factors2[index2] = false;
                                }                                    
                                else
                                {
                                    detailName = "Show is owned by WB";

                                    //Show is not owned
                                    factors1[index] = true;
                                    factors1[index2] = false;

                                    //Show is owned by CBS
                                    factors2[index] = false;
                                    factors2[index2] = true;
                                }

                                double odds1 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors1), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]),
                                    odds2 = network.model.GetOdds(new Show(s.Name, s.network, new ObservableCollection<bool>(factors2), s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);

                                int count1 = network.shows.Where(x => x.factorValues[index] == factors1[index] && x.factorValues[index2] == factors1[index2]).Count(),
                                    count2 = network.shows.Where(x => x.factorValues[index] == factors2[index] && x.factorValues[index2] == factors2[index2]).Count(),
                                    count3 = network.shows.Where(x => x.factorValues[index] == NotOriginal && x.factorValues[index2] == CBSShow).Count();



                                //NewOdds = network.model.GetOdds(s, Adjustments[s.year], false, true, index, index2);
                                NewOdds = (odds1 * count1 + odds2 * count2 + CurrentOdds * count3) / (count1 + count2 + count3);
                                AverageTotal += NewOdds;
                                AverageCount++;
                            }
                            else
                            {
                                if (s.factorValues[i])
                                    detailName = "Show is not owned by the network";
                                else
                                    detailName = "Show is owned by the network";

                                //NewOdds = network.model.GetOdds(s, Adjustments[s.year], false, true, i);

                                ObservableCollection<bool> factors = new ObservableCollection<bool>();
                                foreach (bool b in s.factorValues)
                                    factors.Add(b);

                                factors[i] = !factors[i];

                                var odds1 = network.model.GetOdds(new Show(s.Name, s.network, factors, s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);
                                int count1 = network.shows.Where(x => x.factorValues[i] == factors[i]).Count(),
                                    count2 = network.shows.Where(x => x.factorValues[i] == s.factorValues[i]).Count();

                                NewOdds = (odds1 * count1 + CurrentOdds * count2) / (count1 + count2);
                                AverageTotal += NewOdds;
                                AverageCount++;
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

                        ObservableCollection<bool> factors = new ObservableCollection<bool>();
                        foreach (bool b in s.factorValues)
                            factors.Add(b);

                        factors[i] = !factors[i];

                        var odds1 = network.model.GetOdds(new Show(s.Name, s.network, factors, s.Episodes, s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);
                        int count1 = network.shows.Where(x => x.factorValues[i] == factors[i]).Count(),
                            count2 = network.shows.Where(x => x.factorValues[i] == s.factorValues[i]).Count();

                        NewOdds = (odds1 * count1 + CurrentOdds * count2) / (count1 + count2);
                        //NewOdds = network.model.GetOdds(s, Adjustments[s.year], false, true, i);
                        AverageTotal += NewOdds;
                        AverageCount++;

                        

                        detailValue = CurrentOdds - NewOdds;

                        details.Add(new DetailsContainer(detailName, detailValue));
                    }
                }

                


                if (s.Halfhour)
                    detailName = "Half hour show";
                else
                    detailName = "Hour long show";

                var tempodds = network.model.GetOdds(new Show(s.Name, s.network, s.factorValues, s.Episodes, !s.Halfhour, s.factorNames) { ShowIndex = s.ShowIndex }, Adjustments[s.year]);
                int CurrentCount = network.shows.Where(x => x.Halfhour == s.Halfhour).Count(),
                    NewCount = network.shows.Where(x => x.Halfhour == !s.Halfhour).Count();

                NewOdds = (tempodds * NewCount + CurrentOdds * CurrentCount) / (NewCount + CurrentCount);
                //NewOdds = network.model.GetOdds(s, Adjustments[s.year], false, true, s.factorNames.Count + 1);
                AverageTotal += NewOdds;
                AverageCount++;

                
                detailValue = CurrentOdds - NewOdds;
                details.Add(new DetailsContainer(detailName, detailValue));
                double max = 0;
                int peak = 0;

                var OddsByEpisode = new double[26];
                double total = 0;
                int count = 0;

                for (int i = LowestEpisode - 1; i < HighestEpisode; i++)
                {
                    var tShow = new Show(s.Name, network, s.factorValues, i, s.Halfhour, s.factorNames)
                    {
                        ShowIndex = s.ShowIndex
                    };

                    OddsByEpisode[i] = network.model.GetOdds(tShow, Adjustments[tShow.year]);
                    var c = network.shows.Where(x => x.Episodes == i + 1).Count();
                    total += OddsByEpisode[i] * c;
                    count += c;

                    if (OddsByEpisode[i] >= max)
                    {
                        max = OddsByEpisode[i];
                        peak = i + 1;
                    }
                }

                NewOdds = (count > 0) ? total / count : CurrentOdds;
                AverageTotal += NewOdds;
                AverageCount++;

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

                //double total = 0;
                //int count = 0;
                //for (int i = 0; i < 26; i++)
                //    if ((i < low - 1 && i >= LowestEpisode) || (i >= high && i < HighestEpisode))
                //    {
                //        total += OddsByEpisode[i];
                //        count++;
                //    }

                

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

                //BaseOdds = AverageTotal / AverageCount;

                double change = 0;
                foreach (DetailsContainer d in details)
                    change += d.Value;

                

                double multiplier = (CurrentOdds - BaseOdds) / change;
                bool BaseReverse = false;

                if (change != 0 && change != (CurrentOdds - BaseOdds))
                {
                    if (multiplier < 0)
                    {
                        double ex = Math.Log(CurrentOdds) / Math.Log(BaseOdds);
                        BaseOdds = Math.Pow(CurrentOdds, ex);
                        multiplier = (CurrentOdds - BaseOdds) / change;
                        BaseReverse = true;
                    }
                        //foreach (DetailsContainer d in details)
                        //    d.Value *= -1;
                }

                if (BaseReverse)
                {
                    var o = CurrentOdds - change;
                    if (o > 0 && o < 1)
                    {
                        BaseOdds = o;
                        BaseReverse = false;
                    }   
                }
                    

                Base.Text = "Base Odds: " + BaseOdds.ToString("P") + (BaseReverse ? "⚠" : "");

                double oldEx = 1, exponent = 1, increment = (multiplier < 1) ? 0.01 : -0.01;

                double oldChange = change;

                change = 0;
                foreach (DetailsContainer d in details)
                {
                    if (d.Value > 0)
                        change += Math.Pow(d.Value, oldEx+increment);
                    else
                        change -= Math.Pow(-d.Value, oldEx+increment);
                }
                if (Math.Abs(oldChange - (CurrentOdds - BaseOdds)) < Math.Abs(change - (CurrentOdds - BaseOdds)))
                    increment *= -1;

                bool found = false;

                while (!found)
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
                    multiplier = (CurrentOdds - BaseOdds) / change;

                    foreach (DetailsContainer d in details)
                        d.Value *= multiplier;
                }

                for (int i = details.Count - 1; i >= 0; i--)
                    if (Math.Round(details[i].Value, 4) == 0)
                        details.RemoveAt(i);

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
