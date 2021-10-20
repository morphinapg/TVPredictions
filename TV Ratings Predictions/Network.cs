using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Core;
using System.Collections.Concurrent;

namespace TV_Ratings_Predictions 
{
    [Serializable]
    public class Network : INotifyPropertyChanged
    {
        public string name;
        public ObservableCollection<string> factors;                    //A factor is anything that might affect the renewability of a TV show. Described with true/false.

        public List<Show> shows;                                        //A Show object contains all of the information necessary to describe an entire season of a show.
                                                                        //This list tracks all seasons of all shows ever added to this network, for all years.

        [NonSerialized]
        public ObservableCollection<Show> FilteredShows;                //This collection will include only the shows that apply to the currently selected tv season year, sorted by rating index

        [NonSerialized]
        public ObservableCollection<Show> AlphabeticalShows;            //AlphabeticalShows is just FilteredShows, sorted by Show Name.

        [NonSerialized]
        public ObservableCollection<RatingsContainer> NetworkRatings;   //A RatingsContainer includes all of the episode ratings for a season of a show
                                                                        //This collection includes a RatingsContainer for every Show in AlphabeticalShows

        [NonSerialized]
        public ObservableCollection<RatingsContainer> NetworkViewers;

        [NonSerialized]
        public ObservableCollection<PredictionContainer> Predictions;   //A PredictionContainer contains all of the information needed to display predictions for a Show
                                                                        //This collection includes a PredictionContainer for every show in FilteredShows

        [NonSerialized]
        public ObservableCollection<AverageContainer> Averages;         //An AverageContainer includes information used to display the current ratings averages for a show
                                                                        //This collection includes an AverageContainer for each Show in FilteredShows        

        [NonSerialized]
        public Dictionary<int, List<Show>> ShowsPerYear;

        public double[] ratingsAverages;                                //Typically throughout a TV season, ratings will start out higher and fall throughout the season.
        public double[] FactorAverages;                                 //This array describes that pattern for the network, based on ratings data for all shows ever tracked on the network.
        
        public double[][] deviations;                                   //The deviation arrays collect statistics on how much the current projected rating deviates from the final rating
        public double[] typicalDeviation;                               //as well as how the projected ratings vary week-to-week.
        public double TargetError;                                      //These statistics drive a Normal Distribution used to calculate odds
        public double SeasonDeviation;
        public double Adjustment;

        public NeuralPredictionModel model;                             //A NeuralPredictionModel is a Neural Network used for predicting renewal or cancellation of a show.

        public double PredictionAccuracy, PredictionError, LowestError; //A value used for storing the accuracy of the current model. This value is displayed on the Home Page.

        public EvolutionTree evolution;                                 //This object represents a Genetic Algorithm system used to search for better prediction models.

        [NonSerialized]
        public bool refreshEvolution, refreshPrediction;                //refreshEvolution instructs the EvolutionTree on whether to refresh its secondary branch, for added diversity
                                                                        //refreshPrediction instructs the UI to call OnPropertyChanged for the PredictionAccuracy property                                                                        
                
        public DateTime _lastupdate;                                    //This value represents the last time the prediction model was updated

        [NonSerialized]
        public bool PredictionLocked = false;

        public string LastUpdate                                        //Displaying the _lastupdate property as a string, for display on the Home Page
        {
            get
            {
                TimeSpan diference = (DateTime.Now - _lastupdate);

                if (diference.TotalHours > 24)
                {
                    return " (updated " + diference.Days + (diference.Days > 1 ? " Days ago)" : " Day ago)");
                }
                else if (diference.TotalMinutes > 60)
                {
                    return " (updated " + diference.Hours + (diference.Hours > 1 ? " Hours ago)" : " Hour ago)");
                }
                else if (diference.TotalSeconds > 60)
                {
                    return " (updated " + diference.Minutes + (diference.Minutes > 1 ? " Minutes ago)" : " Minute ago)");
                }
                else
                    return " (updated " + diference.Seconds + (diference.Seconds > 1 ? " Seconds ago)" : " Second ago)");
            }
        }

        public string ToolTip
        {
            get
            {
                return "Lowest Error: " + LowestError + "\r\n" +
                    "Margin of Error: " + model._targeterror;
            }
        }

        [field:NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;   //This allows the UI to know when certain properties have changed
        public async void OnPropertyChangedAsync(string name) 
        {
             await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
             });            
        }

        public void ModelUpdate(NeuralPredictionModel m)    //Update the Prediction Model with the new model, and let the UI know changes have happened
        {           

            model = new NeuralPredictionModel(m);

            FactorAverages = model.FactorBias;
            SeasonDeviation = model.SeasonDeviation;
            Adjustment = model.GetAdjustment();

            //PredictionAccuracy = model.TestAccuracy(true) * 100;
            PredictionAccuracy = model._accuracy * 100;
            PredictionError = model._score;
            LowestError = model._error;
            //TargetError = GetMarginOfError();
            OnPropertyChangedAsync("PredictionAccuracy");
            OnPropertyChangedAsync("PredictionError");
            OnPropertyChangedAsync("LowestError");
            _lastupdate = DateTime.Now;                         
            OnPropertyChangedAsync("LastUpdate");
            OnPropertyChangedAsync("ToolTip");

            NetworkDatabase.pendingSave = true;             //This ensures that the app will save the new model to Settings within the next second
        }

        public Network (string n, ObservableCollection<String> f) //Define network by name and factors
        {
            name = n;
            factors = f;
            shows = new List<Show>();
            FilteredShows = new ObservableCollection<Show>();
            NetworkRatings = new ObservableCollection<RatingsContainer>();
            NetworkViewers = new ObservableCollection<RatingsContainer>();
            AlphabeticalShows = new ObservableCollection<Show>();
            Predictions = new ObservableCollection<PredictionContainer>();
            Averages = new ObservableCollection<AverageContainer>();
            PredictionAccuracy = model.TestAccuracy(true) * 100;

            model = new NeuralPredictionModel(this, 0.5);
            evolution = new EvolutionTree(this, 0.5);

            ratingsAverages = new double[26];

            deviations = new double[26][];
            for (int i = 0; i < 26; i++)
                deviations[i] = new double[26];

            typicalDeviation = new double[26];
        }

        public void Filter(int year)                        //The Filter method, as mentioned earlier, is a very important part of this app's functionality
        {                                                   
            FilteredShows.Clear();
            NetworkRatings.Clear();
            NetworkViewers.Clear();
            AlphabeticalShows.Clear();
            
            
            foreach (Show s in CustomFilter(year))          //Filter shows by year and sort by Average Rating
                FilteredShows.Add(s);


            //UpdateIndexes();                            //Update ratings indexes, and then populate the various collections used to display the data across the app
            model.TestAccuracy(true);
            PredictionAccuracy = model._accuracy * 100;
            PredictionError = model._score;
            LowestError = model._error;

            //TargetError = GetMarginOfError();
            RefreshPredictions(true);
            RefreshAverages();            

            var tempList = FilteredShows.OrderBy(x => x.NameWithSeason);
            foreach(Show s in tempList)
            {
                AlphabeticalShows.Add(s);
                NetworkRatings.Add(new RatingsContainer(this, s));
                NetworkViewers.Add(new RatingsContainer(this, s, true));
            }

            ShowsPerYear = new Dictionary<int, List<Show>>();
            var years = shows.Select(x => x.year).Distinct();
            foreach(int y in years)
                ShowsPerYear[y] = shows.Where(x => x.year == y && x.ratings.Count > 0).OrderByDescending(x => x.ShowIndex).ToList();
        }

        public List<Show> CustomFilter(int year)            //Returns a filtered list representing every show for a custom chosen year, sorted by rating
        {
            var tempList = shows.Where(x => x.year == year).ToList();

            foreach (Show s in tempList)
                s.UpdateAverage();
            tempList.Sort();

            return tempList;
        }

        public void UpdateIndexes()                //The ShowIndex value represents a value between 0 to 1 for every show in a particular year
        {
            UpdateIndexes(NetworkDatabase.CurrentYear);
        }

        public void UpdateIndexes(int year)                             //Update Indexes for a custom year
        {
            var tempList = shows.Where(x => x.year == year && x.ratings.Count > 0).OrderBy(x => x.AverageRating).ThenBy(x => x.Name).ThenBy(x => x.Season).ToList();

            double total = 0;
            var totals = new double[tempList.Count];
            bool duplicate = false;
            Parallel.For(0, tempList.Count, i =>
            {
                if (i > 0 && tempList[i - 1].AverageRating == tempList[i].AverageRating)
                    duplicate = true;
                totals[i] =tempList[i].AverageRating * (tempList[i].Halfhour ? 0.5 : 1);
            });                                                                             
            total = totals.Sum();

            double cumulativeTotal = 0;
            foreach (Show s in tempList)
            {
                s.ShowIndex = (cumulativeTotal + (s.AverageRating * (s.Halfhour ? 0.25 : 0.5))) / total;
                cumulativeTotal += s.AverageRating * (s.Halfhour ? 0.5 : 1);
            }                    

            if (duplicate)  //If there are duplicate rating scores, then perform the process again with the duplicates reverssed, then average the indexes
            {
                cumulativeTotal = 0;
                tempList = tempList.OrderBy(x => x.AverageRating).ThenByDescending(x => x.Name).ThenBy(x => x.Season).ToList();

                foreach (Show s in tempList)
                {
                    var newindex = (cumulativeTotal + (s.AverageRating * (s.Halfhour ? 0.25 : 0.5))) / total;
                    s.ShowIndex = (s.ShowIndex + newindex) / 2;
                    cumulativeTotal += s.AverageRating * (s.Halfhour ? 0.5 : 1);
                }
            }
        }

        public void UpdateAverages()                                //This method updates the ratings falloff values
        {                                                           //This is run whenever ratings numbers are changed
            ratingsAverages = new double[26];                       //The more shows there are, the longer this can take
            var tempList = shows.Where(x => x.ratings.Count > 0).ToList();

            Parallel.ForEach(tempList, s =>
            {
                if (s.ratingsAverages.Contains(0))
                    s.UpdateAllAverages(0);
            });

            for (int i = 0; i < 26; i++)
            {
                double total = 0, start = 0;
                int weight = 0;

                for (int x = 0; x < tempList.Count; x++)
                {
                    start+= tempList[x].ratingsAverages[0];
                    total+= tempList[x].ratingsAverages[i];
                    weight++;
                }

                if (weight > 0)
                    ratingsAverages[i] = total / start;
                else if (i == 0)
                    ratingsAverages[i] = 1;                         //If there are literally zero shows with ratings data, then it's just a flat ratings falloff at 1
                else
                    ratingsAverages[i] = ratingsAverages[i - 1];    //If there aren't enough episodes from any show, it simply stores the previous episode's value
            }

            var yearlist = shows.Select(x => x.year).Distinct();
            Parallel.ForEach(shows, s => s.UpdateAverage());
            shows.Sort();
            foreach (int i in yearlist)
                UpdateIndexes(i);

            //Also update startingDeviation
            deviations = new double[26][];
            for (int i = 0; i < 26; i++)
                deviations[i] = new double[26];
            typicalDeviation = new double[26];

            //Find minimum and maximum episode counts
            var episodeCounts = tempList.AsParallel().Where(x => x.ratings.Count > 1).Select(x => x.Episodes).Distinct();
            int min = episodeCounts.Min() - 1, max = episodeCounts.Max();

            //Interate through episode counts, determining deviation between starting episode and episode #

            Parallel.For(0, max - 1, s =>
            //for (int s = 0; s < max-1; s++)
            {
                for (int i = s + 1; i < max; i++)
                {
                    var segment = tempList.Where(x => x.ratings.Count > i);
                    var count = segment.Count();

                    if (count > 0)
                    {
                        double deviation = 0;
                        foreach (Show ss in segment)
                        {
                            //double variance = 0;
                            //if (ss.ratings.Count > 1)
                            //{
                            //    var average = ss.ratings.Average();
                            //    for (int e = 0; e < i; e++)
                            //        variance += Math.Pow(Math.Log(ss.ratings[e]) - Math.Log(average), 2);
                            //}

                            deviation += Math.Pow(Math.Log(ss.ratingsAverages[s] * AdjustAverage(s + 1, i + 1)) - Math.Log(ss.ratingsAverages[i]), 2);
                            //deviation += variance / (ss.ratings.Count - 1);
                        }

                        deviations[s][i] = Math.Sqrt(deviation / count);
                    }
                }

                //fill in missing numbers
                //for (int i = 1; i < 26; i++)
                //    if (deviations[s][i] == 0 && deviations[s][i - 1] > 0)
                //        deviations[s][i] = deviations[s][i - 1];

                //for (int i = 24; i >= 0; i--)
                //    if (deviations[s][i] == 0 && deviations[s][i + 1] > 0)
                //        deviations[s][i] = deviations[s][i + 1];



                //find cumulative deviations
                if (s > 0)
                {
                    var segment = tempList.Where(x => x.ratings.Count > s);

                    double deviation = 0;

                    if (segment.Count() > 0)
                    {
                        foreach (Show ss in segment)
                        {
                            //calculate standard deviation
                            double ProjectionVariance = 0;
                            for (int i = 0; i < s; i++)
                                ProjectionVariance += Math.Pow(Math.Log(ss.ratingsAverages[i] * ss.network.AdjustAverage(i + 1, ss.Episodes)) - Math.Log(ss.ratingsAverages[s] * ss.network.AdjustAverage(s + 1, ss.Episodes)), 2);

                            deviation += ProjectionVariance / s;
                        }
                    }                    

                    typicalDeviation[s] = Math.Sqrt(deviation / segment.Count());
                }
            });

            //fill in missing numbers
            //for (int i = 1; i < 26; i++)
            //    if (deviations[i].Sum() == 0 && deviations[i - 1].Sum() > 0)
            //        deviations[i] = deviations[i - 1];

            //for (int i = 24; i >= 0; i--)
            //    if (deviations[i].Sum() == 0 && deviations[i + 1].Sum() > 0)
            //        deviations[i] = deviations[i + 1];

            for (int i = 1; i < 26; i++)
                if (typicalDeviation[i] == 0 && typicalDeviation[i - 1] > 0)
                    typicalDeviation[i] = typicalDeviation[i - 1];

            for (int i = 24; i >= 0; i--)
                if (typicalDeviation[i] == 0 && typicalDeviation[i + 1] > 0)
                    typicalDeviation[i] = typicalDeviation[i + 1];

            //Determine how much target ratings deviate per year
            //var Adjustments = model.GetAdjustments(true);
            //double devs = 0;

            //foreach (int i in yearlist)
            //{
            //    var segment = tempList.AsParallel().Where(x => x.year == i).Select(x => model.GetTargetRating(i, model.GetThreshold(x, FactorAverages, Adjustments[i])));
            //    var average = segment.Average();

            //    foreach (double d in segment)
            //        devs += Math.Pow(Math.Log(d) - Math.Log(average), 2);
            //}

            //Standard error formula for deviation of target errors
            //TargetError = GetMarginOfError();
            //TargetError = Math.Sqrt(devs / Math.Max((tempList.Count() - 1), 1)) / Math.Sqrt(Math.Max((tempList.Count() - 1), 1));

            //Factor Averages
            FactorAverages = (model.FactorBias is null) ? new double[factors.Count] : model.FactorBias;

            //Find Standard Deviation For Season #
            //var SeasonAverage = model.GetSeasonAverage(factors);
            //double weights = 0;
            //double totals = 0;

            //foreach (int i in yearlist)
            //{
            //    var segment = tempList.Where(x => x.year == i);
            //    var w = 1.0 / (NetworkDatabase.MaxYear - i + 1);
            //    totals += segment.AsParallel().Select(x => Math.Pow(x.Season - SeasonAverage, 2)).Sum() * w;
            //    weights += segment.Count() * w;
            //}

            //SeasonDeviation = Math.Sqrt(totals / weights);
            SeasonDeviation = model.SeasonDeviation;

            Adjustment = model.GetAdjustment();
        }

        void RefreshAverages()      //Updates the Averages collection with all of the ratings average data for every show in FilteredShows
        {
            Averages.Clear();
            for (int i = FilteredShows.Count - 1; i >= 0; i--)
                Averages.Add(new AverageContainer(FilteredShows[i], this));
        }        

        public void RefreshPredictions(bool parallel = false)   //Calculate predictions for every show in FilteredShows, and sort by odds (descending)
        {
            //if (initialize)
            UpdateOdds(parallel);

            Predictions.Clear();

            //var tempList = new List<PredictionContainer>();
            //foreach (Show s in FilteredShows)
            //    tempList.Add(new PredictionContainer(s, this));

            var tempList = FilteredShows.Select(x => new PredictionContainer(x, this)).ToList();
            tempList.Sort();

            

            foreach (PredictionContainer p in tempList)
                Predictions.Add(p);
        }

        public void UpdateOdds(bool parallel = false)       //Calculate model accuracy, and then update the odds for every show in FilteredShows
        {
            //PredictionAccuracy = model._accuracy * 100;
            //PredictionError = model._score;
            //LowestError = model._error;

            //var Adjustments = model.GetAdjustments(parallel);

            TargetError = model.GetTargetErrorParallel(factors);
            Parallel.ForEach(FilteredShows, s => s.PredictedOdds = model.GetOdds(s));
        }

        public double AdjustAverage(int currentEpisode, int finalEpisode, double currentDrop = -1, bool viewers = false)   //This applies the typical ratings falloff values to the current weighted ratings average for a show
        {                                                                   //The result is a prediction for where the show's weighted ratings average will be at the end of the season
                                                                            //This allows for more of a fair comparison between shows at different points in their seasons            

            try
            {
                double ExpectedDrop = (currentDrop == -1 || currentEpisode == 1) ? 1 : ratingsAverages[currentEpisode - 1] / ratingsAverages[0];
                double slope = (currentDrop == -1 || currentEpisode == 1) ? 1 : Math.Log10(currentDrop) / Math.Log10(ExpectedDrop);

                if (currentEpisode == 2) slope = (slope + 1) / 2;

                double PredictedDrop = Math.Log10(ratingsAverages[finalEpisode - 1] / ratingsAverages[currentEpisode - 1]);

                //return ratingsAverages[finalEpisode - 1] / ratingsAverages[currentEpisode - 1];
                return Math.Pow(10, PredictedDrop * slope);
            }
            catch
            {
                return 1;
            }

        }

        public double GetMidpoint()
        {
            //First, make a list of all shows canceled or renewed
            var tmpList = shows.Where(x => x.Renewed || x.Canceled);

            //Next, make a list of all possible midpoints
            var indexes = tmpList.Select(x => x.ShowIndex).OrderBy(x => x).ToList();
            var midpoints = new List<double>();
            for (int i = 1; i < indexes.Count; i++)
                midpoints.Add((indexes[i - 1] + indexes[i]) / 2);
            midpoints = midpoints.Distinct().ToList();

            //Next, test how many errors for each midpoint
            var errors = new ConcurrentDictionary<double, int>();
            midpoints.AsParallel().ForAll(x =>
            {
                var Rerrors = tmpList.Where(s => s.Renewed && s.ShowIndex < x).Count();
                var Cerrors = tmpList.Where(s => s.Canceled && s.ShowIndex > x).Count();
                errors[x] = Rerrors + Cerrors;
            });

            //Find minimum
            var mininmum = errors.Values.Min();

            //Return average of all midpoints matching that minimum 
            return errors.Where(x => x.Value == mininmum).Select(x => x.Key).Average();
        }

    }    

}
