using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.Storage.Pickers;
using System.Collections.Concurrent;
using MathNet.Numerics.Distributions;

namespace TV_Ratings_Predictions 
{
    /// <summary>
    /// This file includes nearly all of the classes necesary for the TV Predictions project
    /// </summary>

    static class NetworkDatabase //This class is used to store the database of all TVNetworks, Shows, etc, as well as additional global properties that are needed throughout the app
    {
        public static ObservableCollection<Network> NetworkList = new ObservableCollection<Network>();  //This list will contain all of the Networks loaded into the database

        public static bool isAddPage = false;           //This describes whether the app is currently on the "Add Network" page
                                                        //This is used to prevent accidental loss of data when adding a network

        public static bool canGoBack = false;           //This describes whether the app is currently displaying a page that should display a back button, rather than a home button.
        public static bool pendingSave = false;         //This describes whether the app should save its data on the next Timer tick (once per second)
        public static bool onHomePage = true;           //This describes whether the app is on the home page. If not, there's no reason to update the UI's LastUpdate value
        public static bool cancelEvolution = false;     //When true, this instructs the genetic algorithm threads to stop running
        public static bool EvolutionStarted = false;    //This lets the app know that the genetic algoritm has begun processing, for displaying the correct button on the home page.
        public static bool UseOdds = false;             //This is a togglable value that informs whether to use straight 0-100% odds for predictions, or use Renewed/Canceled with a confidence % instead.

        [NonSerialized]
        public static int MaxYear;                      //This value represents the current TV season start year, and is used to restrict a DatePicker on the MainPage
                                                        //It is also used during the calculation of prediction accuracy, as TV shows closer to current airing get higher weight

        static int _currentyear;                        //Stores the currently selected TV Season year
        public static int CurrentYear                   //When the current year changes, the database needs to update the filtered selection of shows for each network
        {
            get
            {
                return _currentyear;
            }
            set
            {
                _currentyear = value;
                UpdateFilter();
            }
        }

        public static void SortNetworks()               //This sorts the networks according to their Average Renewal Threshold rating value
        {        
            Parallel.ForEach(NetworkList, n => n.model.GetNetworkRatingsThreshold(CurrentYear, false));    //Get the Average Renewal Threshold rating value for each network

            //Because this is an ObservableCollection, we can't use Linq to sort, as that would break bindings, so we have to sort manually.
            bool sorted = false;
            if (NetworkList.Count > 1)  
                while (!sorted)
                {
                    sorted = true;
                    for (int i = 1; i < NetworkList.Count; i++)
                    {
                        if (NetworkList[i].model._ratingstheshold > NetworkList[i - 1].model._ratingstheshold)
                        {
                            sorted = false;
                            NetworkList.Move(i, i - 1);
                        }

                    }
                }
        }

        public static void UpdateFilter()           //The Filter method on a Network updates several collections to only include shows from the currently selected year
        {                                           //It also performs several other actions, such as updating predictions. 
            foreach (Network n in NetworkList)      //The Filter method must be called any time the current year changes
                n.Filter(_currentyear);             //Or any time anything else changes that would affect predictions for the currently filtered list of shows.
        }

        public async static Task ReadSettings()                     //When the app opens, we need to read a large "Settings" file containing all networks, shows, ratings, prediction models and more
        {
            var helper = new LocalObjectStorageHelper();

            if (await helper.FileExistsAsync("Settings"))           //If the Settings exists, read the settings and make a backup
            {
                var settings = ReadFromBinaryFile<NetworkSettings>(ApplicationData.Current.LocalFolder.Path + "\\Settings");
                Read_Settings(settings);

                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile newFile = await localFolder.GetFileAsync("Settings");
                _ = await newFile.CopyAsync(ApplicationData.Current.LocalFolder, "Settings_bak", NameCollisionOption.ReplaceExisting);
            }
            else
            {
                if (await helper.FileExistsAsync("Settings_bak"))   //If not, try to read from the backup settings file
                {
                    var settings = ReadFromBinaryFile<NetworkSettings>(ApplicationData.Current.LocalFolder.Path + "\\Settings_bak");
                    Read_Settings(settings);
                }
            }

        }

        static T ReadFromBinaryFile<T>(string filePath)     //Deserializing the Settings file into a usable format
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }

        static void Read_Settings(NetworkSettings settings)
        {
            NetworkList.Clear();

            Parallel.ForEach(settings.NetworkList, n =>                                 //We need to instantiate all NonSerialized properties
            {
                n.FilteredShows = new ObservableCollection<Show>();                     
                n.NetworkRatings = new ObservableCollection<RatingsContainer>();
                n.AlphabeticalShows = new ObservableCollection<Show>();
                n.Predictions = new ObservableCollection<PredictionContainer>();
                n.Averages = new ObservableCollection<AverageContainer>();

                
                //n.model = new NeuralPredictionModel(n, n.GetMidpoint());                               //This commented code is here if I ever need to test
                //n.evolution = new EvolutionTree(n, n.GetMidpoint());                                   //changes to the predictions with a fresh model

                n.model.shows = n.shows;
                Parallel.ForEach(n.shows, s =>
                {
                    s.factorNames = n.factors;
                    s.network = n;
                });

                n.evolution.network = n;
                n.PredictionAccuracy = n.model.TestAccuracy() * 100;

                


                n.Filter(NetworkDatabase.CurrentYear);                                  //Once the Network is fully restored, perform a filter based on the current TV Season

            }
            );

            foreach (Network n in settings.NetworkList)                                 //After all networks are restored and filtered, add them to the global database
                NetworkList.Add(n);


            NetworkDatabase.SortNetworks();                                             //and sort by Network Average Renewal threshold
        }

        public static void WriteSettings()  //Used to write the current database to the Settings file
        {
            WriteToBinaryFile<NetworkSettings>(ApplicationData.Current.LocalFolder.Path + "\\Settings", new NetworkSettings());
        }

        static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);

            }
        }

        public static async Task WritePredictionsAsync()    //Similar to WriteSettings, but using a condensed version of each Network for use in the mobile app
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Desktop };
            picker.FileTypeChoices.Add("TV Predictions", new List<string>() { ".TVP" });

            StorageFile file = await picker.PickSaveFileAsync();

            if (file != null)
            {
                var Networks = new List<MiniNetwork>();
                foreach (Network n in NetworkList)
                    Networks.Add(new MiniNetwork(n));

                using (Stream stream = await file.OpenStreamForWriteAsync())
                {
                    stream.SetLength(0);
                    var serializer = new DataContractSerializer(typeof(List<MiniNetwork>));
                    serializer.WriteObject(stream, Networks);
                }
            }
        }        
    }


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
        public ObservableCollection<PredictionContainer> Predictions;   //A PredictionContainer contains all of the information needed to display predictions for a Show
                                                                        //This collection includes a PredictionContainer for every show in FilteredShows

        [NonSerialized]
        public ObservableCollection<AverageContainer> Averages;         //An AverageContainer includes information used to display the current ratings averages for a show
                                                                        //This collection includes an AverageContainer for each Show in FilteredShows        

        public double[] ratingsAverages;                                //Typically throughout a TV season, ratings will start out higher and fall throughout the season.
        public double[] FactorAverages;                                 //This array describes that pattern for the network, based on ratings data for all shows ever tracked on the network.


        public double[][] deviations;                                   //The deviation arrays collect statistics on how much the current projected rating deviates from the final rating
        public double[] typicalDeviation;                               //as well as how the projected ratings vary week-to-week.
        public double TargetError;                                      //These statistics drive a Normal Distribution used to calculate odds

        public NeuralPredictionModel model;                             //A NeuralPredictionModel is a Neural Network used for predicting renewal or cancellation of a show.

        [NonSerialized]
        public double PredictionAccuracy;                               //A value used for storing the accuracy of the current model. This value is displayed on the Home Page.

        public EvolutionTree evolution;                                 //This object represents a Genetic Algorithm system used to search for better prediction models.

        [NonSerialized]
        public bool refreshEvolution, refreshPrediction;                //refreshEvolution instructs the EvolutionTree on whether to refresh its secondary branch, for added diversity
                                                                        //refreshPrediction instructs the UI to call OnPropertyChanged for the PredictionAccuracy property                                                                        
                
        public DateTime _lastupdate;                                    //This value represents the last time the prediction model was updated

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
            PredictionAccuracy = model.TestAccuracy() * 100;    
            OnPropertyChangedAsync("PredictionAccuracy");
            _lastupdate = DateTime.Now;                         
            OnPropertyChangedAsync("LastUpdate");

            NetworkDatabase.pendingSave = true;             //This ensures that the app will save the new model to Settings within the next second
        }

        public Network (string n, ObservableCollection<String> f) //Define network by name and factors
        {
            name = n;
            factors = f;
            shows = new List<Show>();
            FilteredShows = new ObservableCollection<Show>();
            NetworkRatings = new ObservableCollection<RatingsContainer>();            
            AlphabeticalShows = new ObservableCollection<Show>();
            Predictions = new ObservableCollection<PredictionContainer>();
            Averages = new ObservableCollection<AverageContainer>();
            PredictionAccuracy = model.TestAccuracy() * 100;

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
            AlphabeticalShows.Clear();
            
            foreach (Show s in CustomFilter(year))          //Filter shows by year and sort by Average Rating
                FilteredShows.Add(s);

            
            //UpdateIndexes();                            //Update ratings indexes, and then populate the various collections used to display the data across the app
            RefreshPredictions(true);
            RefreshAverages();

            var tempList = FilteredShows.OrderBy(x => x.Name);
            foreach(Show s in tempList)
            {
                AlphabeticalShows.Add(s);
                NetworkRatings.Add(new RatingsContainer(this, s));
            }
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
            var tempList = shows.Where(x => x.year == year && x.ratings.Count > 0).OrderBy(x => x.AverageRating).ThenBy(x => x.Name).ToList();

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
                tempList = tempList.OrderBy(x => x.AverageRating).ThenByDescending(x => x.Name).ToList();

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

                    if (count > 1)
                    {
                        double deviation = 0;
                        foreach (Show ss in segment)
                        {
                            double variance = 0;
                            if (ss.ratings.Count > 1)
                            {
                                var average = ss.ratings.Average();
                                for (int e = 0; e < i; e++)
                                    variance += Math.Pow(Math.Log(ss.ratings[e]) - Math.Log(average), 2);
                            }

                            deviation += Math.Pow(Math.Log(ss.ratingsAverages[s] * AdjustAverage(s + 1, i + 1)) - Math.Log(ss.ratingsAverages[i]), 2);
                            deviation += variance / (ss.ratings.Count - 1);
                        }
                            

                        deviations[s][i] = Math.Sqrt(deviation / (count - 1));
                    }
                }

                //fill in missing numbers
                for (int i = 1; i < 26; i++)
                    if (deviations[s][i] == 0 && deviations[s][i - 1] > 0)
                        deviations[s][i] = deviations[s][i - 1];

                for (int i = 24; i >= 0; i--)
                    if (deviations[s][i] == 0 && deviations[s][i + 1] > 0)
                        deviations[s][i] = deviations[s][i + 1];



                //find cumulative deviations
                if (s > 0)
                {
                    var segment = tempList.Where(x => x.ratings.Count > s);

                    double deviation = 0;

                    foreach (Show ss in segment)
                    {
                        //calculate standard deviation
                        double ProjectionVariance = 0;
                        for (int i = 0; i < s; i++)
                            ProjectionVariance += Math.Pow(Math.Log(ss.ratingsAverages[i] * ss.network.AdjustAverage(i + 1, ss.Episodes)) - Math.Log(ss.ratingsAverages[s] * ss.network.AdjustAverage(s + 1, ss.Episodes)), 2);

                        deviation += ProjectionVariance / s;
                    }

                    typicalDeviation[s] = Math.Sqrt(deviation / segment.Count());
                }
            });

            //fill in missing numbers
            for (int i = 1; i < 26; i++)
                if (deviations[i].Sum() == 0 && deviations[i - 1].Sum() > 0)
                    deviations[i] = deviations[i - 1];

            for (int i = 24; i >= 0; i--)
                if (deviations[i].Sum() == 0 && deviations[i + 1].Sum() > 0)
                    deviations[i] = deviations[i + 1];

            for (int i = 1; i < 26; i++)
                if (typicalDeviation[i] == 0 && typicalDeviation[i - 1] > 0)
                    typicalDeviation[i] = typicalDeviation[i - 1];

            for (int i = 24; i >= 0; i--)
                if (typicalDeviation[i] == 0 && typicalDeviation[i + 1] > 0)
                    typicalDeviation[i] = typicalDeviation[i + 1];

            //Determine how much target ratings deviate per year
            var Adjustments = model.GetAdjustments(true);
            var YearlyDeviation = new Dictionary<int, double>();
            double devs = 0;

            foreach (int i in yearlist)
            {
                var segment = tempList.AsParallel().Where(x => x.year == i).Select(x => model.GetTargetRating(i, model.GetThreshold(x, FactorAverages, Adjustments[i])));
                var average = segment.Average();

                foreach (double d in segment)
                    devs += Math.Pow(Math.Log(d) - Math.Log(average), 2);
            }

            //Standard error formula for deviation of target errors
            TargetError = Math.Sqrt(devs / Math.Max((tempList.Count() - 1), 1)) / Math.Sqrt(Math.Max((tempList.Count() - 1), 1));

            //Factor Averages
            FactorAverages = model.GetAverages(factors);
        }

        void RefreshAverages()      //Updates the Averages collection with all of the ratings average data for every show in FilteredShows
        {
            Averages.Clear();
            for (int i = FilteredShows.Count - 1; i >= 0; i--)
                Averages.Add(new AverageContainer(FilteredShows[i], this));
        }        

        public void RefreshPredictions(bool parallel = false)   //Calculate predictions for every show in FilteredShows, and sort by odds (descending)
        {
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
            PredictionAccuracy = model.TestAccuracy(parallel) * 100;

            var Adjustments = model.GetAdjustments(parallel);

            Parallel.ForEach(FilteredShows, s => s.PredictedOdds = model.GetOdds(s, FactorAverages, Adjustments[s.year]));
        }

        public double AdjustAverage(int currentEpisode, int finalEpisode)   //This applies the typical ratings falloff values to the current weighted ratings average for a show
        {                                                                   //The result is a prediction for where the show's weighted ratings average will be at the end of the season
            try                                                             //This allows for more of a fair comparison between shows at different points in their seasons
            {
                return ratingsAverages[finalEpisode - 1] / ratingsAverages[currentEpisode - 1];
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

    [Serializable]
    public class Show : INotifyPropertyChanged, IComparable<Show> //Contains all of the information necessary to describe an entire season of a show
    {
        public double[] ratingsAverages;

        [NonSerialized]
        public double _calculatedThreshold;

        [NonSerialized]
        public Network network;

        string _name;
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                OnPropertyChanged("Name");
            }
        }
        public ObservableCollection<bool> factorValues;

        [NonSerialized]
        public ObservableCollection<string> factorNames;
        public int year;
        public List<double> ratings;
        public double AverageRating, ShowIndex, PredictedOdds;
        public double OldRating, OldOdds, FinalPrediction;
        public string RenewalStatus;
        public bool Renewed, Canceled;

        private int _episodes;
        public int Episodes
        {
            get { return _episodes; }
            set
            {
                _episodes = value;
                OnPropertyChanged("Episodes");
            }
        }

        private bool _halfhour;
        public bool Halfhour
        {
            get { return _halfhour; }
            set
            {
                _halfhour = value;
                OnPropertyChanged("Halfhour");
            }
        }

        private int _season;
        public int Season
        {
            get { return _season; }
            set
            {
                _season = value;
                OnPropertyChanged("Season");
            }
        }

        public int FactorHash
        {
            get
            {
                int hash = 0;
                hash += Episodes;
                hash += Halfhour ? 32 : 0;
                int level = 64;
                foreach (bool b in factorValues)
                {
                    hash += b ? level : 0;
                    level *= 2;
                }

                return hash;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            //NetworkDatabase.WriteSettings();
        }

        public Show(string ShowName, Network n, int season, ObservableCollection<bool> FactorList, int EpisodeCount, bool isHalfHour, ObservableCollection<string> names, double avg = 0, double index = 1, string status = "", bool ren = false, bool can = false)
        {
            Name = ShowName;
            factorValues = FactorList;
            _episodes = EpisodeCount;
            _halfhour = isHalfHour;
            year = NetworkDatabase.CurrentYear;
            ratings = new List<double>();
            factorNames = names;
            AverageRating = avg;
            ShowIndex = index;
            Renewed = false;
            Canceled = false;
            RenewalStatus = status;
            Renewed = ren;
            Canceled = can;
            network = n;
            ratingsAverages = new double[26];
            Season = season;
        }

        public void UpdateAverage()
        {
            AverageRating = CalculateAverage(ratings.Count) * network.AdjustAverage(ratings.Count, Episodes);
        }

        public void UpdateAllAverages(int start)
        {
            Parallel.For(start, 26, i => ratingsAverages[i] = CalculateAverage(i + 1));
        }

        public double CalculateAverage(int EpisodeNumber)
        {
            double
                total = 0,
                weights = 0;

            for (int i = 0; i < Math.Min(EpisodeNumber, ratings.Count); i++)
            {
                double w = Math.Pow(i + 1, 2);

                total += ratings[i] * w;
                weights += w;
            }

            if (ratings.Count > Episodes) Episodes = ratings.Count;

            

            return (weights > 0 ? total / weights : 0);
        }

        public double CurrentAverage()
        {
            return AverageRating / network.AdjustAverage(ratings.Count, Episodes);
        }

        public override string ToString()
        {
            return Name;
        }

        public int CompareTo(Show other)
        {
            return AverageRating.CompareTo(other.AverageRating);
        }
    }

    public class RatingsContainer : INotifyPropertyChanged
    {
        List<double> Ratings;
        Show show;
        private Network network;

        public string ShowName { get; }

        public double? Episode1 { get { return GetEpisode(1); } set { SetEpisode(1, value); } }
        public double? Episode2 { get { return GetEpisode(2); } set { SetEpisode(2, value); } }
        public double? Episode3 { get { return GetEpisode(3); } set { SetEpisode(3, value); } }
        public double? Episode4 { get { return GetEpisode(4); } set { SetEpisode(4, value); } }
        public double? Episode5 { get { return GetEpisode(5); } set { SetEpisode(5, value); } }
        public double? Episode6 { get { return GetEpisode(6); } set { SetEpisode(6, value); } }
        public double? Episode7 { get { return GetEpisode(7); } set { SetEpisode(7, value); } }
        public double? Episode8 { get { return GetEpisode(8); } set { SetEpisode(8, value); } }
        public double? Episode9 { get { return GetEpisode(9); } set { SetEpisode(9, value); } }
        public double? Episode10 { get { return GetEpisode(10); } set { SetEpisode(10, value); } }
        public double? Episode11 { get { return GetEpisode(11); } set { SetEpisode(11, value); } }
        public double? Episode12 { get { return GetEpisode(12); } set { SetEpisode(12, value); } }
        public double? Episode13 { get { return GetEpisode(13); } set { SetEpisode(13, value); } }
        public double? Episode14 { get { return GetEpisode(14); } set { SetEpisode(14, value); } }
        public double? Episode15 { get { return GetEpisode(15); } set { SetEpisode(15, value); } }
        public double? Episode16 { get { return GetEpisode(16); } set { SetEpisode(16, value); } }
        public double? Episode17 { get { return GetEpisode(17); } set { SetEpisode(17, value); } }
        public double? Episode18 { get { return GetEpisode(18); } set { SetEpisode(18, value); } }
        public double? Episode19 { get { return GetEpisode(19); } set { SetEpisode(19, value); } }
        public double? Episode20 { get { return GetEpisode(20); } set { SetEpisode(20, value); } }
        public double? Episode21 { get { return GetEpisode(21); } set { SetEpisode(21, value); } }
        public double? Episode22 { get { return GetEpisode(22); } set { SetEpisode(22, value); } }
        public double? Episode23 { get { return GetEpisode(23); } set { SetEpisode(23, value); } }
        public double? Episode24 { get { return GetEpisode(24); } set { SetEpisode(24, value); } }
        public double? Episode25 { get { return GetEpisode(25); } set { SetEpisode(25, value); } }
        public double? Episode26 { get { return GetEpisode(26); } set { SetEpisode(26, value); } }

        private RatingsContainer() { }

        public RatingsContainer(Network n, Show s)
        {
            network = n;

            Ratings = s.ratings;

            ShowName = s.Name;

            show = s;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            NetworkDatabase.pendingSave = true;
        }

        double? GetEpisode(int ep)
        {
            if (Ratings.Count >= ep)
                return Ratings[ep - 1];
            else
                return null;
        }

        void SetEpisode(int ep, double? value)
        {
            if (value == null)
            {
                Ratings.RemoveAt(ep - 1);
                for (int i = ep - 1; i <= Ratings.Count; i++)
                    OnPropertyChanged("Episode" + (i + 1));
            }
            else
            {
                if (Ratings.Count >= ep)
                {
                    Ratings[ep - 1] = (double)value;
                    OnPropertyChanged("Episode" + ep);
                }
                else
                {
                    Ratings.Add((double)value);
                    OnPropertyChanged("Episode" + ep);
                    OnPropertyChanged("Episode" + Ratings.Count);
                }

                show.UpdateAllAverages(ep - 1);
            }
        }


    }

    public class PredictionContainer : INotifyPropertyChanged, IComparable<PredictionContainer>
    {
        Network network;
        public Show show;

        public string Show;
        public double odds;
        bool showAll;

        double _rating;
        public string Rating
        {
            get
            {
                return (show.ratings.Count > 0) ? Math.Round(_rating, 2).ToString("F2") : "";
            }
        }

        public double RatingsDiff
        {
            get
            {
                return (show.OldRating == 0) ? 0 : Math.Round(_rating - show.OldRating, 2);
            }
        }

        public string RatingDifference
        {
            get
            {
                if (show.ratings.Count > 0)
                    return (RatingsDiff != 0) ? RatingsDiff.ToString("+0.00; -0.00") : "";
                else
                    return "";
            }
        }

        double _targetrating;
        public string TargetRating { get; }

        public string Status { get; }

        public int StatusIndex
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

        public string Prediction
        {
            get
            {
                if ((Status == "" && show.ratings.Count > 0) || showAll)
                {
                    if (NetworkDatabase.UseOdds)
                    {
                        return odds.ToString("P0") + " Odds";
                    }
                    else
                    {
                        if (odds > 0.5)
                        {
                            return "Renewed (" + Math.Round((odds - 0.5) * 200, 0) + "% Confidence)";
                        }
                        else if (odds < 0.5)
                        {
                            return "Canceled (" + Math.Round((0.5 - odds) * 200, 0) + "% Confidence)";
                        }
                        else
                        {
                            if (_rating > _targetrating)
                                return "Renewed (0% Confidence)";
                            else
                                return "Canceled (0% Confidence)";
                        }
                    }
                }
                else
                    return "";
            }
        }

        public double PredictionDiff
        {
            get
            {
                if (NetworkDatabase.UseOdds)
                    return (show.OldRating == 0 && show.OldOdds == 0) ? 0 : Math.Round(odds - show.OldOdds, 2);
                else
                    return (show.OldRating == 0 && show.OldOdds == 0) ? 0 : Math.Round((odds - show.OldOdds) * 2, 2);
            }
        }

        public string PredictionDifference
        {
            get
            {
                if (show.ratings.Count > 0)
                {
                    return (PredictionDiff != 0 && Status == "") ? PredictionDiff.ToString("↑0%; ↓0%") : "";
                }
                else
                    return "";
            }
        }

        public string NewShow
        {
            get
            {
                if (show.OldRating == 0 && show.OldOdds == 0)
                    return "(NEW)";
                else
                    return "";
            }
        }

        public string Category
        {
            get
            {
                if (Status == "" && show.ratings.Count > 0)
                {
                    if (odds > 0.8)
                        return "✔✔✔";
                    else if (odds > 0.6)
                        return "✔✔";
                    else if (odds > 0.5 || _rating > _targetrating)
                        return "✔";
                    else if (odds > 0.4)
                        return "❌";
                    else if (odds > 0.2)
                        return "❌❌";
                    else
                        return "❌❌❌";
                }
                else
                    return "";
            }
        }

        public string Accuracy
        {
            get
            {
                if (show.Renewed)
                    return (odds > 0.5) ? "✔" : "❌";
                else if (show.Canceled)
                    return (odds < 0.5) ? "✔" : "❌";
                else
                    return "";
            }
        }

        public PredictionContainer(Show s, Network n, bool a = false)
        {
            network = n;
            show = s;
            Show = s.Name;
            odds = s.PredictedOdds;
            _rating = s.AverageRating;
            Status = s.RenewalStatus;

            var Adjustments = n.model.GetAdjustments(true);

            _targetrating = n.model.GetTargetRating(s.year, n.model.GetThreshold(s, n.FactorAverages, Adjustments[s.year]));
            TargetRating = Math.Round(_targetrating, 2).ToString("F2");
            showAll = a;
        }

        public void UpdateOdds()
        {
            odds = show.PredictedOdds;
            OnPropertyChanged("Rating");
            OnPropertyChanged("RatingDifference");
            OnPropertyChanged("Status");
            OnPropertyChanged("Prediction");
            OnPropertyChanged("Category");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public int CompareTo(PredictionContainer other)
        {
            //if (other == null) return 0;
            //if (this.odds > other.odds) return -1;
            //if (this.odds == other.odds) return 0;
            //return 1;

            return other.odds.CompareTo(this.odds);
        }
    }

    public class AverageContainer : INotifyPropertyChanged
    {
        private Network network;
        Show show;

        public string Show { get; }

        double _weighted;
        public string PredictedAverage
        {
            get
            {
                return (show.ratings.Count > 0) ? Math.Round(_weighted, 2).ToString("F2") : "";
            }
        }

        public string CurrentAverage
        {
            get
            {
                return (show.ratings.Count > 0) ? Math.Round(show.CurrentAverage(), 2).ToString("F2") : "";
            }
        }

        double _rating;
        public string StraightAverage
        {
            get
            {
                return (show.ratings.Count > 0) ? Math.Round(_rating, 2).ToString("F2") : "";


            }
        }

        double _index;
        public string Index
        {
            get
            {
                return (show.ratings.Count > 0) ? Math.Round(_index, 3).ToString("F3") : "";
            }
        }

        public AverageContainer(Show s, Network n)
        {
            network = n;
            show = s;
            Show = s.Name;
            _rating = 0;
            foreach (double r in s.ratings)
                _rating += r;

            _rating /= s.ratings.Count;

            _weighted = s.AverageRating;

            _index = s.ShowIndex;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    [Serializable]
    public class NeuralPredictionModel : IComparable<NeuralPredictionModel>
    {
        [NonSerialized]
        public List<Show> shows;

        int NeuronCount, InputCount;
        Neuron[] FirstLayer, SecondLayer;
        Neuron Output;

        public double mutationrate, mutationintensity, neuralintensity;

        [NonSerialized]
        public double _accuracy, _ratingstheshold, _score;

        [NonSerialized]
        public bool isMutated;


        public NeuralPredictionModel(Network n) //New Random Prediction Model
        {
            shows = n.shows;
            isMutated = false;

            InputCount = n.factors.Count + 2;
            NeuronCount = Convert.ToInt32(Math.Round(InputCount * 2.0 / 3.0 + 1, 0));

            FirstLayer = new Neuron[NeuronCount];
            SecondLayer = new Neuron[NeuronCount];

            for (int i = 0; i < NeuronCount; i++)
            {
                FirstLayer[i] = new Neuron(InputCount);
                SecondLayer[i] = new Neuron(NeuronCount);
            }

            Output = new Neuron(NeuronCount);

            Random r = new Random();
            mutationrate = r.NextDouble();
            mutationintensity = r.NextDouble();
            neuralintensity = r.NextDouble();
        }

        public NeuralPredictionModel(Network n, double midpoint) //New Prediction Model based on midpoint
        {
            shows = n.shows;
            isMutated = false;

            InputCount = n.factors.Count + 2;
            NeuronCount = Convert.ToInt32(Math.Round(InputCount * 2.0 / 3.0 + 1, 0));

            FirstLayer = new Neuron[NeuronCount];
            SecondLayer = new Neuron[NeuronCount];

            for (int i = 0; i < NeuronCount; i++)
            {
                FirstLayer[i] = new Neuron(InputCount, midpoint, true);
                SecondLayer[i] = new Neuron(NeuronCount, midpoint, true);
            }

            Output = new Neuron(NeuronCount, midpoint, false);

            Random r = new Random();
            mutationrate = r.NextDouble();
            mutationintensity = r.NextDouble();
            neuralintensity = r.NextDouble();
        }

        private double Breed(double x, double y, Random r)
        {
            //var r = new Random();
            var p = r.NextDouble();

            return (x * p) + (y * (1 - p));

            //return p > 0.5 ? x : y;
        }

        private NeuralPredictionModel(NeuralPredictionModel x, NeuralPredictionModel y)
        {
            var r = new Random();
            shows = x.shows;
            isMutated = false;

            InputCount = x.InputCount;
            NeuronCount = x.NeuronCount;

            FirstLayer = new Neuron[NeuronCount];
            SecondLayer = new Neuron[NeuronCount];

            for (int i = 0; i < NeuronCount; i++)
            {
                FirstLayer[i] = new Neuron(x.FirstLayer[i], y.FirstLayer[i], r);
                SecondLayer[i] = new Neuron(x.SecondLayer[i], y.SecondLayer[i], r);
            }

            Output = new Neuron(x.Output, y.Output, r);
            mutationrate = Breed(x.mutationrate, y.mutationrate, r);
            mutationintensity = Breed(x.mutationintensity, y.mutationintensity, r);
            neuralintensity = Breed(x.neuralintensity, y.neuralintensity, r);
        }

        public NeuralPredictionModel(NeuralPredictionModel n)
        {
            shows = n.shows;
            isMutated = false;

            InputCount = n.InputCount;
            NeuronCount = n.NeuronCount;

            FirstLayer = new Neuron[NeuronCount];
            SecondLayer = new Neuron[NeuronCount];

            for (int i = 0; i < NeuronCount; i++)
            {
                FirstLayer[i] = new Neuron(n.FirstLayer[i]);
                SecondLayer[i] = new Neuron(n.SecondLayer[i]);
            }

            Output = new Neuron(n.Output);
            mutationrate = n.mutationrate;
            mutationintensity = n.mutationintensity;
            neuralintensity = n.neuralintensity;

        }

        public void SetElite()
        {
            _accuracy = 0;
        }

        public double GetThreshold(Show s, double[] averages, double adjustment)
        {
            if (averages is null) averages = new double[InputCount];

            var inputs = new double[InputCount];
            if (s.Renewed || s.Canceled) adjustment = 1;

            double[]
                FirstLayerOutputs = new double[NeuronCount],
                SecondLayerOutputs = new double[NeuronCount];

            for (int i = 0; i < InputCount - 2; i++)
                inputs[i] = (s.factorValues[i] ? 1 : -1) - averages[i];

            inputs[InputCount - 2] = (s.Episodes / 26.0 * 2 - 1) - averages[InputCount - 2];
            inputs[InputCount - 1] = (s.Halfhour ? 1 : -1) - averages[InputCount - 1];

            for (int i = 0; i < NeuronCount; i++)
                FirstLayerOutputs[i] = FirstLayer[i].GetOutput(inputs);

            for (int i = 0; i < NeuronCount; i++)
                SecondLayerOutputs[i] = SecondLayer[i].GetOutput(FirstLayerOutputs);

            s._calculatedThreshold = Math.Pow((Output.GetOutput(SecondLayerOutputs, true) + 1) / 2, adjustment);

            return s._calculatedThreshold;
        }

        public double GetModifiedThreshold(Show s, double[] averages, double adjustment, int index, int index2 = -1, int index3 = -1)
        {
            var inputs = new double[InputCount];
            if (s.Renewed || s.Canceled) adjustment = 1;
            double[]
                FirstLayerOutputs = new double[NeuronCount],
                SecondLayerOutputs = new double[NeuronCount];

            if (index > -1)
            {
                for (int i = 0; i < InputCount - 2; i++)
                    inputs[i] = (s.factorValues[i] ? 1 : -1) - averages[i];

                inputs[InputCount - 2] = (s.Episodes / 26.0 * 2 - 1) - averages[InputCount - 2];
                inputs[InputCount - 1] = (s.Halfhour ? 1 : -1) - averages[InputCount - 1];

                inputs[index] = 0;  //GetScaledAverage(s, index);
                if (index2 > -1)
                {
                    inputs[index2] = 0; // GetScaledAverage(s, index2);
                    if (index3 > -1) inputs[index3] = 0; // GetScaledAverage(s, index3);
                }
            }      


            for (int i = 0; i < NeuronCount; i++)
                FirstLayerOutputs[i] = FirstLayer[i].GetOutput(inputs);

            for (int i = 0; i < NeuronCount; i++)
                SecondLayerOutputs[i] = SecondLayer[i].GetOutput(FirstLayerOutputs);

            s._calculatedThreshold = Math.Pow((Output.GetOutput(SecondLayerOutputs, true) + 1) / 2, adjustment);

            return s._calculatedThreshold;
        }

        double GetScaledAverage(ObservableCollection<string> factors, int index)
        {
            double weight = 0, total = 0;
            var yearlist = shows.Select(x => x.year).Distinct().ToList();

            foreach (int year in yearlist)
            {
                var w = 1.0 / (NetworkDatabase.MaxYear - year + 1);
                double score;
                var count = shows.Where(x => x.year == year).Count();
                weight += w * count;
                if (index < factors.Count)
                    score = (shows.Where(x => x.year == year && x.factorValues[index]).Count() * 1.0 + shows.Where(x => x.year == year && !x.factorValues[index]).Count() * -1.0);
                else if (index == factors.Count)
                    score = shows.Where(x => x.year == year).Select(x => x.Episodes).Average() / 26 * 2 - 1;
                else
                    score = shows.Where(x => x.year == year && x.Halfhour).Count() * 1.0 + shows.Where(x => x.year == year && !x.Halfhour).Count() * -1.0;
                total += score * w;
            }

            return total / weight;
        }

        public double[] GetAverages(ObservableCollection<string> factors)
        {
            var averages = new double[InputCount];
            for (int i = 0; i < InputCount; i++)
                averages[i] = GetScaledAverage(factors, i);

            return averages;
        }

        public double GetAverageThreshold(bool parallel = false)
        {
            double total = 0;
            double count = 0;
            int year = NetworkDatabase.MaxYear;

            //var tempList = shows.Where(x => x.ratings.Count > 0 && (x.Renewed || x.Canceled)).ToList();
            var tempList = shows.ToList();
            var averages = tempList.First().network.FactorAverages;

            if (parallel)
            {
                double[]
                    totals = new double[tempList.Count],
                    counts = new double[tempList.Count];

                Parallel.For(0, tempList.Count, i =>
                {
                    double weight = 1.0 / (year - tempList[i].year + 1);
                    totals[i] = GetThreshold(tempList[i], averages, 1) * weight;
                    counts[i] = weight;
                });

                total = totals.Sum();
                count = counts.Sum();
            }
            else
                foreach (Show s in tempList)
                {
                    double weight = 1.0 / (year - s.year + 1);
                    total += GetThreshold(s, averages, 1) * weight;
                    count += weight;
                }

            return total / count;
        }

        public double GetSeasonAverageThreshold(int year)
        {
            double total = 0;

            var tempList = shows.Where(x => x.year == year && x.ratings.Count > 0).ToList();
            var count = tempList.Count;
            var totals = new double[count];
            var averages = tempList.First().network.FactorAverages;

            Parallel.For(0, count, i => totals[i] = GetThreshold(tempList[i], averages, 1));

            total = totals.Sum();

            return total / count;
        }

        private double GetAdjustment(double NetworkAverage, double SeasonAverage)
        {
            return Math.Log(NetworkAverage) / Math.Log(SeasonAverage);
        }

        public Dictionary<int, double> GetAdjustments(bool parallel = false)
        {
            double average = GetAverageThreshold(parallel);
            var Adjustments = new Dictionary<int, double>();
            var years = shows.Select(x => x.year).ToList().Distinct();
            foreach (int y in years)
                Adjustments[y] = GetAdjustment(average, GetSeasonAverageThreshold(y));

            return Adjustments;
        }

        public double GetOdds(Show s, double[] averages, double adjustment, bool raw = false, bool modified = false, int index = -1, int index2 = -1, int index3 = -1)
        {
            var threshold = modified ? GetModifiedThreshold(s, averages, adjustment, index, index2, index3) : GetThreshold(s, averages, adjustment);

            var target = GetTargetRating(s.year, threshold);
            var variance = Math.Log(s.AverageRating) - Math.Log(target);
            double deviation;

            //calculate standard deviation
            if (s.ratings.Count > 1)
            {
                var count = s.ratings.Count - 1;
                double ProjectionVariance = 0;
                for (int i = 0; i < count; i++)
                {
                    ProjectionVariance += Math.Pow(Math.Log(s.ratingsAverages[i] * s.network.AdjustAverage(i + 1, s.Episodes)) - Math.Log(s.AverageRating * s.network.AdjustAverage(count + 1, s.Episodes)), 2);
                }

                deviation = s.network.deviations[s.ratings.Count - 1][s.Episodes - 1] * Math.Sqrt(ProjectionVariance / count) / s.network.typicalDeviation[s.ratings.Count - 1];

            }
            else
            {
                deviation = s.network.deviations[0][s.Episodes - 1];
            }

            deviation += s.network.TargetError;

            var zscore = variance / deviation;

            var normal = new Normal();

            var baseOdds = normal.CumulativeDistribution(zscore);

            //var exponent = Math.Log(0.5) / Math.Log(threshold);
            //var baseOdds = Math.Pow(s.ShowIndex, exponent);

            if (raw)
                return baseOdds;

            var accuracy = _accuracy;

            if (baseOdds > 0.5)
            {
                baseOdds -= 0.5;
                baseOdds *= 2;
                return (baseOdds * accuracy) / 2 + 0.5;
            }
            else
            {
                baseOdds *= 2;
                baseOdds = 1 - baseOdds;
                return (1 - (baseOdds * accuracy)) / 2;
            }
        }

        public double TestAccuracy(bool parallel = false)
        {
            double average = GetAverageThreshold(parallel);

            double weightAverage = Math.Max(average, 1 - average);

            double scores = 0;
            double totals = 0;
            double weights = 0;
            int year = NetworkDatabase.MaxYear;

            
            var Adjustments = GetAdjustments(parallel);
            var averages = shows.First().network.FactorAverages;

            var tempList = shows.Where(x => x.Renewed || x.Canceled).ToList();

            if (parallel)
            {
                double[]
                    t = new double[tempList.Count], 
                    w = new double[tempList.Count], 
                    score = new double[tempList.Count];

                Parallel.For(0, tempList.Count, i =>
                {
                    Show s = tempList[i];
                    double threshold = GetThreshold(s, averages, Adjustments[s.year]);
                    int prediction = (s.ShowIndex > threshold) ? 1 : 0;
                    double distance = Math.Abs(s.ShowIndex - threshold);

                    if (s.Renewed)
                    {
                        int accuracy = (prediction == 1) ? 1 : 0;
                        double weight;

                        if (accuracy == 1)
                            weight = 1 - Math.Abs(average - s.ShowIndex) / weightAverage;
                        else
                            weight = (distance + weightAverage) / weightAverage;

                        weight /= year - s.year + 1;

                        if (s.Canceled)
                        {
                            double odds = GetOdds(s, averages, Adjustments[s.year], true);
                            var tempScore = (1 - Math.Abs(odds - 0.55)) * 4 / 3;

                            score[i] = tempScore;

                            if (odds < 0.6 && odds > 0.4)
                            {
                                accuracy = 1;

                                weight = 1 - Math.Abs(average - s.ShowIndex) / weightAverage;

                                weight *= tempScore;

                                if (prediction == 0)
                                    weight /= 2;
                            }
                            else
                                weight /= 2;
                        }

                        t[i] = accuracy * weight;
                        w[i] = weight;
                    }
                    else if (s.Canceled)
                    {
                        int accuracy = (prediction == 0) ? 1 : 0;
                        double weight;

                        if (accuracy == 1)
                            weight = 1 - Math.Abs(average - s.ShowIndex) / weightAverage;
                        else
                            weight = (distance + weightAverage) / weightAverage;

                        weight /= year - s.year + 1;

                        t[i] = accuracy * weight;
                        w[i] = weight;
                    }
                });

                scores = score.Sum();
                totals = t.Sum();
                weights = w.Sum();
            }
            else
            {
                foreach (Show s in tempList)
                {
                    double threshold = GetThreshold(s, averages, Adjustments[s.year]);
                    int prediction = (s.ShowIndex > threshold) ? 1 : 0;
                    double distance = Math.Abs(s.ShowIndex - threshold);

                    if (s.Renewed)
                    {
                        int accuracy = (prediction == 1) ? 1 : 0;
                        double weight;

                        if (accuracy == 1)
                            weight = 1 - Math.Abs(average - s.ShowIndex) / weightAverage;
                        else
                            weight = (distance + weightAverage) / weightAverage;

                        weight /= year - s.year + 1;

                        if (s.Canceled)
                        {
                            double odds = GetOdds(s, averages, Adjustments[s.year], true);
                            scores += (1 - Math.Abs(odds - 0.55)) * 4 / 3;

                            if (odds < 0.6 && odds > 0.4)
                            {
                                accuracy = 1;
                                weight = 1 - Math.Abs(average - s.ShowIndex) / weightAverage;
                                weight *= (1 - Math.Abs(odds - 0.55)) * 4 / 3;

                                if (prediction == 0)
                                    weight /= 2;
                            }
                            else
                                weight /= 2;

                        }

                        totals += accuracy * weight;
                        weights += weight;
                    }
                    else if (s.Canceled)
                    {
                        int accuracy = (prediction == 0) ? 1 : 0;
                        double weight;

                        if (accuracy == 1)
                            weight = 1 - Math.Abs(average - s.ShowIndex) / weightAverage;
                        else
                            weight = (distance + weightAverage) / weightAverage;

                        weight /= year - s.year + 1;

                        totals += accuracy * weight;
                        weights += weight;
                    }
                }
            }

            _accuracy = (weights == 0) ? 0.0 : (totals / weights);
            _score = scores;

            return _accuracy;
        }

        public double GetNetworkRatingsThreshold(int year, bool parallel)
        {
            //_ratingstheshold = GetTargetRating(year, GetAverageThreshold(parallel));
            var s = shows.First();
            var Adjustment = GetAdjustments(parallel)[year];
            _ratingstheshold = GetTargetRating(year, GetModifiedThreshold(s, s.network.FactorAverages, Adjustment, -1));
            return _ratingstheshold;
        }

        public double GetTargetRating(int year, double targetindex)
        {

            var tempShows = shows.Where(x => x.year == year && x.ratings.Count > 0).OrderByDescending(x => x.ShowIndex).ToList();
            if (tempShows.Count == 0)
            {
                var yearList = shows.Where(x => x.ratings.Count > 0).Select(x => x.year).ToList();
                yearList.Sort();
                if (yearList.Contains(year - 1))
                    year--;
                else if (yearList.Contains(year + 1))
                    year++;
                else if (yearList.Where(x => x < year).Count() > 0)
                    year = yearList.Where(x => x < year).Last();
                else
                    year = yearList.Where(x => x > year).First();

                year = yearList.Last();
                tempShows = shows.Where(x => x.year == year && x.ratings.Count > 0).OrderByDescending(x => x.ShowIndex).ToList();
            }

            bool found = false;
            int upper = 0, lower = 1;
            for (int i = 0; i < tempShows.Count && !found; i++)
            {
                if (tempShows[i].ShowIndex < targetindex)
                {
                    lower = i;
                    found = true;
                }
                else
                    upper = i;
            }

            if (tempShows.Count > 0)
            {
                double maxIndex, minIndex, maxRating, minRating;
                if (lower != 0 && lower > upper && tempShows.Count > 1) //match is between two values
                {
                    maxIndex = tempShows[upper].ShowIndex;
                    maxRating = tempShows[upper].AverageRating;
                    minIndex = tempShows[lower].ShowIndex;
                    minRating = tempShows[lower].AverageRating;

                    while (maxRating == minRating)
                    {
                        lower++;

                        if (lower < tempShows.Count)
                        {
                            minIndex = tempShows[lower].ShowIndex;
                            minRating = tempShows[lower].AverageRating;
                        }
                        else
                        {
                            minIndex = 0;
                            minRating = 0;
                        }                        
                    }
                }
                else if (lower == 0 && tempShows.Count > 1) //match is at the beginning of a multiple item list
                {
                    lower = 1;
                    maxIndex = tempShows[0].ShowIndex;
                    maxRating = tempShows[0].AverageRating;
                    minIndex = tempShows[1].ShowIndex;
                    minRating = tempShows[1].AverageRating;

                    while (maxRating == minRating)
                    {
                        lower++;

                        if (lower < tempShows.Count)
                        {
                            minIndex = tempShows[lower].ShowIndex;
                            minRating = tempShows[lower].AverageRating;
                        }
                        else
                        {
                            minIndex = 0;
                            minRating = 0;
                        }
                    }
                }
                else if (upper > 0) //match is at the end of a multiple item list
                {
                    lower = upper - 1;

                    maxIndex = tempShows[upper].ShowIndex;
                    maxRating = tempShows[upper].AverageRating;
                    minIndex = tempShows[upper - 1].ShowIndex;
                    minRating = tempShows[upper - 1].AverageRating;

                    while (maxRating == minRating)
                    {
                        lower--;

                        if (lower >= 0)
                        {
                            minIndex = tempShows[lower].ShowIndex;
                            minRating = tempShows[lower].AverageRating;
                        }
                        else
                        {
                            minIndex = 0;
                            minRating = 0;
                        }
                    }
                }
                else //one item in list
                {
                    maxIndex = tempShows[upper].ShowIndex;
                    maxRating = tempShows[upper].AverageRating;
                    minIndex = 0;
                    minRating = 0;
                }


                return (targetindex - minIndex) / (maxIndex - minIndex) * (maxRating - minRating) + minRating;
            }

            return 0;
        }

        private double MutateValue(double d, bool increase = false)
        {
            var r = new Random();

            //var p = r.NextDouble();

            //double low = d * (1 - mutationintensity * p), high = 1 - (1 - d) * (1 - mutationintensity * p);

            var intensity = Math.Max(mutationintensity, 0.01);

            double low = Math.Max(d - intensity, 0), high = Math.Min(d + intensity, 1);

            if (increase) low = d;

            if (r.NextDouble() < mutationrate || increase)
            {
                isMutated = true;
                return r.NextDouble() * (high - low) + low;
            }

            return d;
        }

        public void MutateModel()
        {
            var r = new Random();
            isMutated = false;

            if (r.NextDouble() > 0.5)
                mutationrate = MutateValue(mutationrate);
            else
                mutationintensity = MutateValue(mutationintensity);

            if (r.NextDouble() < mutationrate)
                neuralintensity = Math.Abs(neuralintensity + (r.NextDouble() * 2 - 1));

            for (int i = 0; i < NeuronCount; i++)
            {
                FirstLayer[i].isMutated = false;
                FirstLayer[i].Mutate(mutationrate, neuralintensity, mutationintensity, r);

                SecondLayer[i].isMutated = false;
                SecondLayer[i].Mutate(mutationrate, neuralintensity, mutationintensity, r);


                if (FirstLayer[i].isMutated || SecondLayer[i].isMutated)
                    isMutated = true;
            }

            Output.isMutated = false;
            Output.Mutate(mutationrate, neuralintensity, mutationintensity, r);
            if (Output.isMutated)
                isMutated = true;
        }

        public void IncreaseMutationRate()
        {
            mutationrate = MutateValue(mutationrate, true);
        }


        public int CompareTo(NeuralPredictionModel other)
        {
            double otherAcc = other._accuracy;
            double thisAcc = _accuracy;
            double thisWeight = _score;
            double otherWeight = other._score;

            if (thisAcc != otherAcc)
                return otherAcc.CompareTo(thisAcc);
            else
                return otherWeight.CompareTo(thisWeight);
        }

        public static NeuralPredictionModel operator +(NeuralPredictionModel x, NeuralPredictionModel y)
        {
            var model = new NeuralPredictionModel(x, y);
            model.MutateModel();

            return model;
        }

        public override bool Equals(object obj)
        {
            var other = (NeuralPredictionModel)obj;

            if (other._accuracy == _accuracy)
            {
                if (other._score == _score)
                    return true;
                else
                    return false;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(NeuralPredictionModel x, NeuralPredictionModel y)
        {
            if (x._accuracy == y._accuracy)
            {
                if (x._score == y._score)
                    return true;
                else
                    return false;
            }

            return false;
        }

        public static bool operator !=(NeuralPredictionModel x, NeuralPredictionModel y)
        {
            if (x._accuracy == y._accuracy)
            {
                if (x._score == y._score)
                    return false;
                else
                    return true;
            }

            return true;
        }

        public static bool operator >(NeuralPredictionModel x, NeuralPredictionModel y)
        {
            if (x._accuracy > y._accuracy)
                return true;
            else
            {
                if (x._accuracy == y._accuracy)
                {
                    if (x._score > y._score)
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }
        }

        public static bool operator <(NeuralPredictionModel x, NeuralPredictionModel y)
        {
            if (x._accuracy < y._accuracy)
                return true;
            else
            {
                if (x._accuracy == y._accuracy)
                {
                    if (x._score < y._score)
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }
        }
    }

    [Serializable]
    public class EvolutionTree
    {
        List<NeuralPredictionModel> Primary, Randomized;
        public long Generations, CleanGenerations, RandomGenerations;

        [NonSerialized]
        public Network network;

        [NonSerialized]
        bool IncreasePrimary, IncreaseRandom, Mutations, RandomMutations;

        [NonSerialized]
        public long ticks;

        //Primary:
        //First 4 entries will be the top 4 best performing models out of all 3 branches
        //The rest of the entries will be mutations based on those 4 best.

        //CleanSlate:
        //This branch, when created, will set NetworkAverageIndex to GetAverageThreshold() for the top model in the Primary Branch
        //Every TrueIndex and FalseIndex wil be set to be the same as NetworkAverageIndex
        //EpisodeThreshold will be set to the EpisodeThreshold in the Primary Branch
        //All weight values will be set to 0
        //These will define the primary parent, and the remaining children will be mutations based on that
        //On future generations, the top 4 models from this branch will be selected as the parents (first 4 indexed)
        //children will be mutated based on those 4 parents

        //Randomized:
        //This branch will start out with all 30 entries being totally randomized.
        //On the next generation, the top 4 models from this branch will be selected as parents (first 4 indexes)
        //children will be mutated from these 4 parents

        //All 3 branche, when first created, will start out following the same behavior as the Randomized Branch
        //All 3 branches will continue to propogate their own evolution every generation
        //If any model from the CleanSlate or Randomized branches is selected to become a parent in the primary branch:
        //then that branch will start over, following the rules outlined above


        public EvolutionTree(Network n)
        {
            network = n;

            Primary = new List<NeuralPredictionModel>();
            Randomized = new List<NeuralPredictionModel>();

            for (int i = 0; i < 30; i++)
            {
                Primary.Add(new NeuralPredictionModel(n));
                Randomized.Add(new NeuralPredictionModel(n));
            }

            Generations = 1;
            CleanGenerations = 1;
            RandomGenerations = 1;
        }

        public EvolutionTree(Network n, double midpoint)
        {
            network = n;

            Primary = new List<NeuralPredictionModel>();;
            Randomized = new List<NeuralPredictionModel>();

            for (int i = 0; i < 30; i++)
            {
                Primary.Add(new NeuralPredictionModel(n, midpoint));
                Randomized.Add(new NeuralPredictionModel(n));
            }

            Generations = 1;
            CleanGenerations = 1;
            RandomGenerations = 1;
        }

        public void NextGeneration()
        {
            var r = new Random();

            //Update shows list
            for (int i = 0; i < 30; i++)
            {
                Primary[i].shows = network.shows;
                //CleanSlate[i].shows = network.shows;
                Randomized[i].shows = network.shows;
            }

            //Sort all 3 Branches from Highest to lowest

            for (int i = 0; i < 30; i++)
            {
                if (i == 2 || i == 3)
                {
                    Primary[i].SetElite();
                    Randomized[i].SetElite();
                }
                else
                {
                    Primary[i].TestAccuracy();
                    Randomized[i].TestAccuracy();
                }
            }

            Primary.Sort();
            Randomized.Sort();

            for (int i = 29; i > 0; i--)
            {
                if (Primary[i] == Primary[i - 1])
                    Primary[i].SetElite();
                if (Randomized[i] == Randomized[i - 1])
                    Randomized[i].SetElite();
            }            

            Primary.Sort();
            Randomized.Sort();

            if (IncreasePrimary)
                Primary[r.Next(4)].IncreaseMutationRate();
            if (IncreaseRandom)
                Randomized[r.Next(4)].IncreaseMutationRate();

            IncreasePrimary = false;
            IncreaseRandom = false;

            //CHeck if any models in CleanSlate or Randomized beat any of the top 4 in Primary
            //If so, add them to Primary
            bool randomUpdate = false;

            bool finished = false;

            

            //Randomized
            for (int i = 0; i < 4 && !finished; i++)
            {
                if (Randomized[i] > Primary[3])
                {

                    if (Randomized[i] > Primary[0])
                        Primary.Insert(0, new NeuralPredictionModel(Randomized[i]));
                    else if (Randomized[i] > Primary[1])
                        Primary.Insert(1, new NeuralPredictionModel(Randomized[i]));
                    else if (Randomized[i] > Primary[2])
                        Primary.Insert(2, new NeuralPredictionModel(Randomized[i]));
                    else
                        Primary.Insert(3, new NeuralPredictionModel(Randomized[i]));

                    Primary.RemoveAt(30);
                }
                else
                    finished = true;
                
            }

            

            //If model has improved, replace model in network with current best model
            if (Primary[0] > network.model)
            {
                network.ModelUpdate(Primary[0]);
                network.refreshEvolution = false;
                network.refreshPrediction = true;
                randomUpdate = true;
            }
            else if (network.refreshEvolution)
            {
                network.refreshEvolution = false;
                //cleanUpdate = true;
                randomUpdate = true;
            }

            Mutations = false;
            RandomMutations = false;
            

            //If models were chosen from Randomized, then reset that branch based on the above rules
            //If not, perform normal evolution rules            
            if (randomUpdate)
            {
                //for (int i = 0; i < 30; i++)
                Parallel.For(0, 30, i => Randomized[i] = new NeuralPredictionModel(network));

                RandomGenerations = 1;
            }
            else
            {
                Parallel.For(4, 30, i =>
                {
                    int Parent1 = r.Next(4), Parent2 = r.Next(4);

                    Randomized[i] = Randomized[Parent1] + Randomized[Parent2];

                    if (Randomized[i].isMutated) RandomMutations = true;
                });

                if (!RandomMutations)
                    IncreaseRandom = true;

                RandomGenerations++;
            }

            //Evolve Primary Branch
            Parallel.For(4, 30, i =>
            {
                int Parent1 = r.Next(4), Parent2 = r.Next(4);

                Primary[i] = Primary[Parent1] + Primary[Parent2];

                if (Primary[i].isMutated) Mutations = true;
            });

            if (!Mutations)
                IncreasePrimary = true;

            Generations++;
        }
    }

    [Serializable]
    public class MiniNetwork //Smaller version of Network class to use for storing predictions for mobile app
    {
        public string name;
        public ObservableCollection<string> factors;
        public List<Show> shows;
        public NeuralPredictionModel model;
        public Dictionary<int, double> Adjustments;
        public double[] RatingsAverages, FactorAverages;
        public DateTime PredictionTime;

        public double[][] deviations;                                   
        public double[] typicalDeviation;                               
        public double TargetError;

        public MiniNetwork(Network n)
        {
            var yearlist = n.shows.Select(x => x.year).Distinct().ToList();  

            name = n.name;
            factors = n.factors;
            shows = n.shows;
            model = n.model;
            Adjustments = model.GetAdjustments(true);
            RatingsAverages = n.ratingsAverages;
            FactorAverages = n.FactorAverages;

            Parallel.ForEach(shows, s => s.UpdateAverage());

            shows.Sort();

            //foreach (int i in yearlist)
            //    n.UpdateIndexes(i);

            Parallel.ForEach(shows, s => s.PredictedOdds = model.GetOdds(s, FactorAverages, Adjustments[s.year]));

            PredictionTime = DateTime.Now;
            deviations = n.deviations;
            typicalDeviation = n.typicalDeviation;
            TargetError = n.TargetError;
        }
    }

    

    class Factor : INotifyPropertyChanged
    {
        public string name;

        

        bool _setting;

        public bool Setting
        {
            get { return _setting; }
            set
            {
                _setting = value;
                RaisePropertyChanged("setting");
            }
        }

        public Factor(string n, bool s)
        {
            name = n;
            Setting = s;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    

    [Serializable]
    public class NetworkSettings
    {
        public ObservableCollection<Network> NetworkList;

        public NetworkSettings()
        {
            NetworkList = NetworkDatabase.NetworkList;
        }
    }

    

    public class NullableValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value == null ? string.Empty : value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            string s = value as string;

            if (!string.IsNullOrWhiteSpace(s) && Double.TryParse(s, out double result))
            {
                return result;
            }

            return null;
        }
    }

    

    [Serializable]
    public class Neuron
    {
        double bias, outputbias;
        double[] weights;
        int inputSize;
        public bool isMutated;

        public Neuron(int inputs)
        {
            isMutated = false;

            Random r = new Random();
            bias = r.NextDouble() * 2 - 1;
            outputbias = 0;

            weights = new double[inputs];

            for (int i = 0; i < inputs; i++)
                weights[i] = r.NextDouble() * 2 - 1;

            inputSize = inputs;
        }

        public Neuron(int inputs, double midpoint, bool skip)
        {
            isMutated = false;

            midpoint = midpoint * 2 - 1;

            bias = skip ? 0 : ReverseActivation(midpoint);
            outputbias = 0;

            weights = new double[inputs];

            for (int i = 0; i < inputs; i++)
                weights[i] = 0;

            inputSize = inputs;
        }

        public Neuron (Neuron n)
        {
            isMutated = false;

            bias = n.bias;
            outputbias = n.outputbias;
            inputSize = n.inputSize;

            weights = new double[inputSize];

            for (int i = 0; i < inputSize; i++)
                weights[i] = n.weights[i];
        }

        private double Breed(double x, double y, Random r)
        {
            //var r = new Random();
            var p = r.NextDouble();

            return (x * p) + (y * (1 - p));

            //return p > 0.5 ? x : y;
        }

        public Neuron(Neuron x, Neuron y, Random r)
        {
            //var r = new Random();
            isMutated = false;
            bias = Breed(x.bias, y.bias, r);
            outputbias = Breed(x.outputbias, y.outputbias, r);

            inputSize = x.inputSize;

            weights = new double[inputSize];

            for (int i = 0; i < inputSize; i++)
                weights[i] = Breed(x.weights[i],y.weights[i], r);            
        }

        public double GetOutput(double[] inputs, bool output = false)
        {
            double total = 0;

            for (int i = 0; i < inputSize; i++)
                total += inputs[i] * weights[i];

            total += bias;

            return output ? Activation(total) : Activation(total) + outputbias;
        }

        double Activation(double d)
        {
            //if (outputNeuron)
                return (2 / (1 + Math.Exp(-1 * d))) - 1;
            //else
                //return (d > 0) ? d : 0;
                //return Math.Log(Math.Exp(d-3) + 1);
            //return (2 / (1 + Math.Exp(-1 * d))) - 1;
        }

        double ReverseActivation(double d)
        {
            return Math.Log((-d - 1) / (d - 1));
        }

        public void Mutate(double mutationrate, double neuralintensity, double mutationintensity, Random r)
        {

            for (int i = 0; i < inputSize; i++)
            {
                if (r.NextDouble() < mutationrate)
                {
                    weights[i] += neuralintensity * (r.NextDouble() * 2 - 1);
                    isMutated = true;
                }
                    
            }

            if (r.NextDouble() < mutationrate)
            {
                bias += neuralintensity * (r.NextDouble() * 2 - 1);
                isMutated = true;
            }

            if (r.NextDouble() < mutationrate)
            {
                var d = (outputbias + 1) / 2;

                double p = r.NextDouble();

                double low = d * (1 - mutationintensity), high = 1 - (1 - d) * (1 - mutationintensity);

                var tempBias = p * (high - low) + low;
                outputbias = tempBias * 2 - 1;
                isMutated = true;
            }
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }
    }

    public class NullableBooleanToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool?)
            {
                return (bool)value;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool)
                return (bool)value;
            return false;
        }
    }

    

    

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool && (bool)value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }

    public class DoubleToPercent : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((double)value).ToString("N1") + "%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return ((double)value).ToString("N1") + "%";
        }
    }

    public class DoubleToPercentUnrounded : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((double)value * 100) + "%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return ((double)value * 100) + "%";
        }
    }

    public class LastUpdateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter == null)
                return "";
            else
            {
                TimeSpan diference = (DateTime.Now - (DateTime)parameter);

                if (diference.TotalHours > 24)
                {
                    return " (updated " + diference.Days + " Days ago)";
                }
                else if (diference.TotalMinutes > 60)
                {
                    return " (updated " + diference.Hours + " Hours ago)";
                }
                else if (diference.TotalSeconds > 60)
                {
                    return " (updated " + diference.Minutes + " Minutes ago)";
                }
                else
                    return " (updated " + diference.Seconds + " Seconds ago)";
            }            
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    

    public class NumberColor : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            else if ((double)value > 0)
                return new SolidColorBrush(Color.FromArgb(255, 0, 176, 80));
            else if ((double)value < 0)
                return new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
            else
                return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusColor : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int number;

            if (value is double)
            {
                if (value == null || (double)value == 0)
                    number = 0;
                else if ((double)value > 0)
                    number = 1;
                else
                    number = -1;
            }
            else
                number = (int)value;


            if (number > 0)
                return new SolidColorBrush(Color.FromArgb(255, 0, 176, 80));
            else if (number < 0)
                return new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
            else
                return new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class FactorContainer
    {
        public string Show;
        public double Index;        
        public String Status;
        public int StatusIndex;

        public FactorContainer(Show s, bool AllYears = false)
        {
            Show = s.Name;
            if (AllYears)
                Show += " (" + s.year + "-" + (s.year + 1) + ")";
            Index = s.ShowIndex;
            Status = s.RenewalStatus;
            if (s.Renewed)
                StatusIndex = 1;
            else if (s.Canceled)
                StatusIndex = -1;
            else
                StatusIndex = 0;
        }
    }



}
