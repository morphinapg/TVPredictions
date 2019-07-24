using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.Storage.Pickers;
using System.Xml.Serialization;

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
            Parallel.ForEach(NetworkList, n => n.model.GetNetworkRatingsThreshold(CurrentYear));    //Get the Average Renewal Threshold rating value for each network

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
                StorageFile copiedFile = await newFile.CopyAsync(ApplicationData.Current.LocalFolder, "Settings_bak", NameCollisionOption.ReplaceExisting);
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

                //n.model = new NeuralPredictionModel(n);                               //This commented code is here if I ever need to test
                //n.evolution = new EvolutionTree(n);                                   //changes to the predictions with a fresh model

                n.model.shows = n.shows;
                n.PredictionAccuracy = n.model.TestAccuracy() * 100;

                n.evolution.network = n;

                Parallel.ForEach(n.shows, s =>
                {
                    s.factorNames = n.factors;
                    s.network = n;
                });

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
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
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
                                                                        //This array describes that pattern for the network, based on ratings data for all shows ever tracked on the network.

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
            model = m;                                          
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
            model = new NeuralPredictionModel(this);
            AlphabeticalShows = new ObservableCollection<Show>();
            Predictions = new ObservableCollection<PredictionContainer>();
            Averages = new ObservableCollection<AverageContainer>();
            PredictionAccuracy = model.TestAccuracy() * 100;
            evolution = new EvolutionTree(this);
        }

        public void Filter(int year)                        //The Filter method, as mentioned earlier, is a very important part of this app's functionality
        {                                                   
            FilteredShows.Clear();
            NetworkRatings.Clear();
            AlphabeticalShows.Clear();
            
            foreach (Show s in CustomFilter(year))          //Filter shows by year and sort by Average Rating
                FilteredShows.Add(s);            

            UpdateIndexes(true);                            //Update ratings indexes, and then populate the various collections used to display the data across the app
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
            Parallel.ForEach(tempList, s => s.UpdateAverage());
            tempList.Sort();

            return tempList;
        }

        public void UpdateIndexes(bool parallel = false)                //The ShowIndex value represents a value between 0 to 1 for every show in a particular year
        {                                                               //This is a cumulative weighted percentile of rating, exclusive of 0 and 1
            var tempList = FilteredShows.ToList();

            double total = 0;

            if (parallel)                                               //First calculate the total of all average ratings, this can be done in parallel or not
            {
                double[] totals = new double[tempList.Count];
                Parallel.For(0, tempList.Count, i =>
                {
                    totals[i] = tempList[i].AverageRating * (tempList[i].Halfhour ? 0.5 : 1);   //half hour shows are weighted half as much
                });                                                                             //as the same ratings contribute half as much money to the network
                total = totals.Sum();
            }
            else
                foreach (Show s in tempList)
                    total += s.AverageRating * (s.Halfhour ? 0.5 : 1);


            double cumulativeTotal = 0;

            foreach (Show s in tempList)                                //Now determine the cumulative total of each show, from lowest to highest rating
                if (s.ratings.Count > 0)
                {
                    s.ShowIndex = (cumulativeTotal + (s.AverageRating * (s.Halfhour ? 0.25 : 0.5))) / total;    //The ShowIndex is the cumulative total of the previous shows, plus half of the current show's weighted rating
                    cumulativeTotal += s.AverageRating * (s.Halfhour ? 0.5 : 1);                                //This allows ShowIndex to be representative of midpoints, rather that beginning/endpoints, resulting in a more balanced prediction
                }
        }

        public void UpdateIndexes(int year)                             //Update Indexes for a custom year
        {
            var tempList = shows.Where(x => x.year == year).OrderBy(x => x.AverageRating).ToList();

            double total = 0;
            double[] totals = new double[tempList.Count];
            Parallel.For(0, tempList.Count, i =>
            {
                totals[i] = tempList[i].AverageRating * (tempList[i].Halfhour ? 0.5 : 1);   
            });                                                                             
            total = totals.Sum();

            double cumulativeTotal = 0;

            foreach (Show s in tempList)                                
                if (s.ratings.Count > 0)
                {
                    s.ShowIndex = (cumulativeTotal + (s.AverageRating * (s.Halfhour ? 0.25 : 0.5))) / total;    
                    cumulativeTotal += s.AverageRating * (s.Halfhour ? 0.5 : 1);                                
                }
        }

        public void UpdateAverages()                                //This method updates the ratings falloff values
        {                                                           //This is run whenever ratings numbers are changed
            ratingsAverages = new double[26];                       //The more shows there are, the longer this can take
                                                                    
            for (int i = 0; i < 26; i++)
            {
                double total = 0, start = 0;
                int weight = 0;
                double[] totals = new double[shows.Count], starts = new double[shows.Count];          
                int[] weights = new int[shows.Count];               

                Parallel.For(0, shows.Count, x =>                        //Because there can be a lot of shows, we're going to iterate through each show in parallel and then sum the values
                {
                    if (shows[x].ratings.Count > 0)                      //We are averaging the ratings falloff for every show on the network, for every year
                    {
                        starts[x] = shows[x].ratingsAverages[0];
                        totals[x] = shows[x].ratingsAverages[i];
                        weights[x] = 1;
                    }
                });

                total = totals.Sum();
                start = starts.Sum();
                weight = weights.Sum();

                if (weight > 0)
                    ratingsAverages[i] = total / start;
                else if (i == 0)
                    ratingsAverages[i] = 1;                         //If there are literally zero shows with ratings data, then it's just a flat ratings falloff at 1
                else
                    ratingsAverages[i] = ratingsAverages[i - 1];    //If there aren't enough episodes from any show, it simply stores the previous episode's value
            }
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

            var tempList = new List<PredictionContainer>();
            foreach (Show s in FilteredShows)
                tempList.Add(new PredictionContainer(s, this));

            tempList.Sort();

            foreach (PredictionContainer p in tempList)
                Predictions.Add(p);
        }

        public void UpdateOdds(bool parallel = false)       //Calculate model accuracy, and then update the odds for every show in FilteredShows
        {
            PredictionAccuracy = model.TestAccuracy(parallel) * 100;
            Parallel.ForEach(FilteredShows, s => s.PredictedOdds = model.GetOdds(s));
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
        public double OldRating, OldOdds;
        public string RenewalStatus;
        public bool Renewed, Canceled;

        private int _episodes;
        public int Episodes
        {
            get { return _episodes; }
            set
            {
                _episodes = value;
                OnPropertyChanged("episodes");
            }
        }

        private bool _halfhour;
        public bool Halfhour
        {
            get { return _halfhour; }
            set
            {
                _halfhour = value;
                OnPropertyChanged("halfhour");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            //NetworkDatabase.WriteSettings();
        }

        public Show(string ShowName, Network n, ObservableCollection<bool> FactorList, int EpisodeCount, bool isHalfHour, ObservableCollection<string> names, double avg = 0, double index = 1, string status = "", bool ren = false, bool can = false)
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
        }

        public void UpdateAverage()
        {
            AverageRating = CalculateAverage(ratings.Count) * network.AdjustAverage(ratings.Count, Episodes);
        }

        public void UpdateAllAverages(int start)
        {
            //for (int i = start; i < 26; i++)
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
        Network network;

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

        public double _ratingsDiff
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
                    return (_ratingsDiff != 0) ? _ratingsDiff.ToString("+0.00; -0.00") : "";
                else
                    return "";
            }
        }

        double _targetrating;
        public string TargetRating { get; }

        public string Status { get; }

        public int _StatusIndex
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

        public double _predictionDiff
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
                    return (_predictionDiff != 0 && Status == "") ? _predictionDiff.ToString("↑0%; ↓0%") : "";
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

            _targetrating = n.model.GetTargetRating(s.year, n.model.GetThreshold(s));
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
        Network network;
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

        private double Breed(double x, double y)
        {
            var r = new Random();
            var p = r.NextDouble();

            return p > 0.5 ? x : y;
        }

        private NeuralPredictionModel(NeuralPredictionModel x, NeuralPredictionModel y)
        {
            shows = x.shows;
            isMutated = false;

            InputCount = x.InputCount;
            NeuronCount = x.NeuronCount;

            FirstLayer = new Neuron[NeuronCount];
            SecondLayer = new Neuron[NeuronCount];

            for (int i = 0; i < NeuronCount; i++)
            {
                FirstLayer[i] = new Neuron(x.FirstLayer[i], y.FirstLayer[i]);
                SecondLayer[i] = new Neuron(x.SecondLayer[i], y.SecondLayer[i]);
            }

            Output = new Neuron(x.Output, y.Output);
            mutationrate = Breed(x.mutationrate, y.mutationrate);
            mutationintensity = Breed(x.mutationintensity, y.mutationintensity);
            neuralintensity = Breed(x.neuralintensity, y.neuralintensity);
        }

        public void SetElite()
        {
            _accuracy = 0;
        }

        public double GetThreshold(Show s)
        {
            var inputs = new double[InputCount];
            double[]
                FirstLayerOutputs = new double[NeuronCount],
                SecondLayerOutputs = new double[NeuronCount];

            for (int i = 0; i < InputCount - 2; i++)
                inputs[i] = s.factorValues[i] ? 1 : -1;

            inputs[InputCount - 2] = s.Episodes / 26.0 * 2 - 1;
            inputs[InputCount - 1] = s.Halfhour ? 1 : -1;

            for (int i = 0; i < NeuronCount; i++)
                FirstLayerOutputs[i] = FirstLayer[i].GetOutput(inputs);

            for (int i = 0; i < NeuronCount; i++)
                SecondLayerOutputs[i] = SecondLayer[i].GetOutput(FirstLayerOutputs);

            s._calculatedThreshold = (Output.GetOutput(SecondLayerOutputs, true) + 1) / 2;



            return s._calculatedThreshold;
        }

        public double GetModifiedThreshold(Show s, int index, int index2 = -1)
        {
            var inputs = new double[InputCount];
            double[]
                FirstLayerOutputs = new double[NeuronCount],
                SecondLayerOutputs = new double[NeuronCount];

            if (index > -1)
            {
                for (int i = 0; i < InputCount - 2; i++)
                    inputs[i] = s.factorValues[i] ? 1 : -1;

                inputs[index] = 0;
                if (index2 > -1)
                    inputs[index2] = 0;
                
                inputs[InputCount - 1] = s.Halfhour ? 1 : -1;

            }
            else
            {
                for (int i = 0; i < InputCount - 2; i++)
                    inputs[i] = 0;

                inputs[InputCount - 1] = 0;
            }                

            inputs[InputCount - 2] = s.Episodes / 26.0 * 2 - 1;



            for (int i = 0; i < NeuronCount; i++)
                FirstLayerOutputs[i] = FirstLayer[i].GetOutput(inputs);

            for (int i = 0; i < NeuronCount; i++)
                SecondLayerOutputs[i] = SecondLayer[i].GetOutput(FirstLayerOutputs);

            s._calculatedThreshold = (Output.GetOutput(SecondLayerOutputs, true) + 1) / 2;

            return s._calculatedThreshold;
        }

        public double GetAverageThreshold(bool parallel = false)
        {
            double total = 0;
            int count = 0;

            if (parallel)
            {
                var tempList = shows.ToList();
                double[] totals = new double[tempList.Count];
                int[] counts = new int[tempList.Count];

                Parallel.For(0, tempList.Count, i =>
                {
                    if (tempList[i].ratings.Count > 0)
                    {
                        totals[i] = GetThreshold(tempList[i]);
                        counts[i] = 1;
                    }
                });

                total = totals.Sum();
                count = counts.Sum();
            }
            else
                foreach (Show s in shows.ToList())
                    if (s.ratings.Count > 0)
                    {
                        total += GetThreshold(s);
                        count++;
                    }

            return total / count;
        }

        public double GetOdds(Show s, bool raw = false, bool modified = false, int index = -1, int index2 = -1)
        {
            var threshold = modified ? GetModifiedThreshold(s, index, index2) : GetThreshold(s);
            var exponent = Math.Log(0.5) / Math.Log(threshold);
            var baseOdds = Math.Pow(s.ShowIndex, exponent);

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

            List<double> debugweights = new List<double>();

            if (parallel)
            {
                double[] t = new double[shows.Count], w = new double[shows.Count], score = new double[shows.Count];
                var tempList = shows.ToList();
                Parallel.For(0, tempList.Count, i =>
                {
                    Show s = tempList[i];

                    if (s.Renewed || s.Canceled)
                    {
                        double threshold = GetThreshold(s);
                        int prediction = (s.ShowIndex > threshold) ? 1 : 0;
                        double distance = Math.Abs(s.ShowIndex - threshold);

                        if (s.Renewed)
                        {
                            int accuracy = (prediction == 1) ? 1 : 0;
                            double weight;

                            if (accuracy == 1)
                                weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;
                            else
                                weight = (distance + weightAverage) / weightAverage;

                            weight /= year - s.year + 1;

                            if (s.Canceled)
                            {
                                double odds = GetOdds(s, true);

                                score[i] = (1 - Math.Abs(odds - 0.55)) * 4 / 3;

                                if (odds < 0.6 && odds > 0.4)
                                {
                                    accuracy = 1;

                                    weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;

                                    weight *= score[i];

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
                                weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;
                            else
                                weight = (distance + weightAverage) / weightAverage;

                            weight /= year - s.year + 1;

                            t[i] = accuracy * weight;
                            w[i] = weight;
                        }
                    }
                });

                scores = score.Sum();
                totals = t.Sum();
                weights = w.Sum();
            }
            else
            {
                foreach (Show s in shows.ToList())
                {
                    if (s.Renewed || s.Canceled)
                    {
                        double threshold = GetThreshold(s);
                        int prediction = (s.ShowIndex > threshold) ? 1 : 0;
                        double distance = Math.Abs(s.ShowIndex - threshold);

                        if (s.Renewed)
                        {
                            int accuracy = (prediction == 1) ? 1 : 0;
                            double weight;

                            if (accuracy == 1)
                                weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;
                            else
                                weight = (distance + weightAverage) / weightAverage;

                            weight /= year - s.year + 1;

                            if (s.Canceled)
                            {
                                double odds = GetOdds(s, true);
                                scores += (1 - Math.Abs(odds - 0.55)) * 4 / 3;

                                if (odds < 0.6 && odds > 0.4)
                                {
                                    accuracy = 1;
                                    weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;
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
                                weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;
                            else
                                weight = (distance + weightAverage) / weightAverage;

                            weight /= year - s.year + 1;

                            totals += accuracy * weight;
                            weights += weight;
                        }
                    }
                }
            }

            _accuracy = (weights == 0) ? 0.0 : (totals / weights);
            _score = scores;

            return _accuracy;
        }

        public double GetNetworkRatingsThreshold(int year)
        {
            _ratingstheshold = GetTargetRating(year, GetAverageThreshold());
            return _ratingstheshold;
        }

        public double GetTargetRating(int year, double targetindex)
        {

            var tempShows = new ObservableCollection<Show>();
            shows.Sort();

            foreach (Show s in shows)
                if (s.year == year && s.ratings.Count > 0)
                    tempShows.Add(s);

            if (tempShows.Count == 0)
            {
                tempShows.Clear();
                foreach (Show s in shows)
                    if (s.ratings.Count > 0) tempShows.Add(s);
            }

            if (tempShows.Count > 1) //make sure list is sorted
            {
                bool sorted = false;
                while (!sorted)
                {
                    sorted = true;
                    for (int i = 1; i < tempShows.Count; i++)
                        if (tempShows[i].ShowIndex > tempShows[i - 1].ShowIndex)
                        {
                            sorted = false;
                            tempShows.Move(i, i - 1);
                        }
                }
            }

            bool found = false;
            int upper = 0, lower = 1;
            double maxIndex = 1, minIndex = 0;
            double maxRating = 1, minRating = 0;

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
                if (lower != 0 && lower > upper && tempShows.Count > 1) //match is between two values
                {
                    maxIndex = tempShows[upper].ShowIndex;
                    maxRating = tempShows[upper].AverageRating;
                    minIndex = tempShows[lower].ShowIndex;
                    minRating = tempShows[lower].AverageRating;
                }
                else if (lower == 0 && tempShows.Count > 1) //match is at the beginning of a multiple item list
                {
                    maxIndex = tempShows[0].ShowIndex;
                    maxRating = tempShows[0].AverageRating;
                    minIndex = tempShows[1].ShowIndex;
                    minRating = tempShows[1].AverageRating;
                }
                else if (upper > 0) //match is at the end of a multiple item list
                {
                    maxIndex = tempShows[upper].ShowIndex;
                    maxRating = tempShows[upper].AverageRating;
                    minIndex = tempShows[upper - 1].ShowIndex;
                    minRating = tempShows[upper - 1].AverageRating;
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

            var p = r.NextDouble();

            double low = d * (1 - mutationintensity * p), high = 1 - (1 - d) * (1 - mutationintensity * p);

            if (increase) low = d;

            if (r.NextDouble() < mutationrate)
            {
                isMutated = true;
                return r.NextDouble() * (high - low) + low;
            }

            return d;
        }

        double Activation(double d)
        {
            return (2 / (1 + Math.Exp(-1 * d))) - 1;
        }

        double ReverseActivation(double d)
        {
            return Math.Log((-d - 1) / (d - 1));
        }

        public void MutateModel()
        {
            var r = new Random();
            isMutated = false;

            if (r.NextDouble() > 0.5)
                mutationrate = MutateValue(mutationrate);
            else
                mutationintensity = MutateValue(mutationintensity);

            if (r.NextDouble() > 0.5)
                neuralintensity = Math.Abs(neuralintensity + (r.NextDouble() * 2 - 1));

            Parallel.For(0, NeuronCount, i =>
            {
                FirstLayer[i].isMutated = false;
                FirstLayer[i].Mutate(mutationrate, neuralintensity, mutationintensity);

                SecondLayer[i].isMutated = false;
                SecondLayer[i].Mutate(mutationrate, neuralintensity, mutationintensity);


                if (FirstLayer[i].isMutated || SecondLayer[i].isMutated)
                    isMutated = true;
            });

            Output.isMutated = false;
            Output.Mutate(mutationrate, neuralintensity, mutationintensity);
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

        //public double GetAverageExponent(int i, bool isTrue)
        //{
        //    double trueExponent = GetExponent(trueIndex[i]), falseExponent = GetExponent(falseIndex[i]);

        //    double total = 0;
        //    int count = 0;

        //    if (i == trueIndex.Length - 2)
        //    {
        //        foreach (Show s in shows)
        //            if (s.ratings.Count > 0)
        //            {
        //                total += (s.Episodes > EpisodeThreshold) ? trueExponent : falseExponent;
        //                count++;
        //            }
        //    }
        //    else if (i == trueIndex.Length - 1)
        //    {
        //        foreach (Show s in shows)
        //            if (s.ratings.Count > 0)
        //            {
        //                total += s.Halfhour ? trueExponent : falseExponent;
        //                count++;
        //            }
        //    }
        //    else
        //    {
        //        foreach (Show s in shows)
        //            if (s.ratings.Count > 0)
        //            {
        //                total += s.factorValues[i] ? trueExponent : falseExponent;
        //                count++;
        //            }
        //    }

        //    double average = total / count;
        //    average = isTrue ? trueExponent / average : falseExponent / average;
        //    average = average * weight[i] + 1 - weight[i];

        //    return average;
        //}
    }

    [Serializable]
    public class EvolutionTree
    {
        List<NeuralPredictionModel> Primary, Randomized;
        public long Generations, CleanGenerations, RandomGenerations;

        [NonSerialized]
        public Network network;

        [NonSerialized]
        bool Mutations, RandomMutations;

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
            //CleanSlate = new List<NeuralPredictionModel>();
            Randomized = new List<NeuralPredictionModel>();

            for (int i = 0; i < 30; i++)
            {
                Primary.Add(new NeuralPredictionModel(n));
                //CleanSlate.Add(new NeuralPredictionModel(n));
                Randomized.Add(new NeuralPredictionModel(n));
            }

            Generations = 1;
            CleanGenerations = 1;
            RandomGenerations = 1;
        }

        public void NextGeneration()
        {
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
                    Primary[i].SetElite();
                else
                    Primary[i].TestAccuracy(true);
                Randomized[i].TestAccuracy(true);
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


            //CHeck if any models in CleanSlate or Randomized beat any of the top 4 in Primary
            //If so, add them to Primary
            bool randomUpdate = false;

            bool finished = false;

            //Randomized
            finished = false;
            for (int i = 0; i < 4 && !finished; i++)
            {
                if (Randomized[i] > Primary[3])
                {

                    if (Randomized[i] > Primary[0])
                    {
                        Primary.Insert(0, Randomized[i]);
                        randomUpdate = true;
                    }
                    else if (Randomized[i] > Primary[1])
                    {
                        Primary.Insert(1, Randomized[i]);
                        randomUpdate = true;
                    }
                    else if (Randomized[i] > Primary[2])
                    {
                        Primary.Insert(2, Randomized[i]);
                    }
                    else
                    {
                        Primary.Insert(3, Randomized[i]);
                    }
                    Primary.RemoveAt(30);
                }
                else
                {
                    finished = true;
                }
            }

            //If model has improved, replace model in network with current best model
            if (Primary[0] > network.model)
            {
                network.ModelUpdate(Primary[0]);
                network.refreshEvolution = false;
                network.refreshPrediction = true;
                randomUpdate = true;
            }
            else
            {
                if (network.refreshEvolution)
                {
                    network.refreshEvolution = false;
                    //cleanUpdate = true;
                    randomUpdate = true;
                }
            }

            Mutations = false;
            //CleanMutations = false;
            RandomMutations = false;


            //If models were chosen from Randomized, then reset that branch based on the above rules
            //If not, perform normal evolution rules            
            if (randomUpdate)
            {
                for (int i = 0; i < 30; i++)
                    Randomized[i] = new NeuralPredictionModel(network);

                RandomGenerations = 1;
            }
            else
            {
                while (!RandomMutations)
                {
                    Random r = new Random();

                    for (int i = 4; i < 30; i++)
                    {
                        int Parent1 = r.Next(4), Parent2 = r.Next(4);
                        while (Parent1 == Parent2)
                            Parent2 = r.Next(4);

                        Randomized[i] = Randomized[Parent1] + Randomized[Parent2];

                        if (Randomized[i].isMutated) RandomMutations = true;
                    }

                    if (!RandomMutations)
                        Randomized[r.Next(4)].IncreaseMutationRate();
                }


                RandomGenerations++;
            }

            //Evolve Primary Branch

            while (!Mutations)
            {
                Random r = new Random();

                for (int i = 4; i < 30; i++)
                {

                    int Parent1 = r.Next(4), Parent2 = r.Next(4);
                    while (Parent1 == Parent2)
                        Parent2 = r.Next(4);

                    Primary[i] = Primary[Parent1] + Primary[Parent2];

                    if (Primary[i].isMutated) Mutations = true;
                }

                if (!Mutations)
                    Primary[r.Next(4)].IncreaseMutationRate();
            }

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

        public MiniNetwork(Network n)
        {
            List<int> yearlist = new List<int>();    

            name = n.name;
            factors = n.factors;
            shows = n.shows;
            model = n.model;

            Parallel.ForEach(shows, s =>
            {
                s.UpdateAverage();
                if (!yearlist.Contains(s.year))
                    yearlist.Add(s.year);
            });

            shows.Sort();

            foreach (int i in yearlist)
                n.UpdateIndexes(i);

            Parallel.ForEach(shows, s => s.PredictedOdds = model.GetOdds(s));
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

            Parallel.For(0, inputs, i => weights[i] = r.NextDouble() * 2 - 1);  

            inputSize = inputs;
        }

        private double Breed(double x, double y)
        {
            var r = new Random();
            var p = r.NextDouble();

            return p > 0.5 ? x : y;
        }

        public Neuron(Neuron x, Neuron y)
        {
            isMutated = false;
            bias = Breed(x.bias, y.bias);
            outputbias = Breed(x.outputbias, y.outputbias);

            inputSize = x.inputSize;

            weights = new double[inputSize];

            for (int i = 0; i < inputSize; i++)
                weights[i] = Breed(x.weights[i],y.weights[i]);            
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

        public void Mutate(double mutationrate, double neuralintensity, double mutationintensity)
        {

            Random r = new Random();

            for (int i = 0; i < inputSize; i++)
            //Parallel.For(0, inputSize, i =>
            {
                if (r.NextDouble() < mutationrate)
                {
                    weights[i] += neuralintensity * (r.NextDouble() * 2 - 1);
                    isMutated = true;
                }
                    
            }

            if (r.NextDouble() < mutationrate)
            {
                bias += neuralintensity * r.NextDouble();
                isMutated = true;
            }

            if (r.NextDouble() < mutationrate)
            {
                var d = (outputbias + 1) / 2;

                double p = r.NextDouble();

                double low = d * (1 - mutationintensity * p), high = 1 - (1 - d) * (1 - mutationintensity * p);
                
                var tempBias = r.NextDouble() * (high - low) + low;
                outputbias = tempBias * 2 - 1;
                isMutated = true;
            }
        }
    }

    [Serializable]
    public class  PredictionModel : IComparable<PredictionModel>
    {
        [NonSerialized]
        public List<Show> shows;

        public double NetworkAverageIndex, EpisodeThreshold;

        #pragma warning disable IDE0044 // Add readonly modifier
        public double[] trueIndex, falseIndex;
        public double mutationrate, mutationintensity;
        #pragma warning restore IDE0044 // Add readonly modifier

        public double[] weight;

        [NonSerialized]
        public double _accuracy;

        [NonSerialized]
        public bool isMutated;

        public PredictionModel(Network n) //Brand New Prediction Model
        {
            Random r = new Random();
            isMutated = false;

            NetworkAverageIndex = r.NextDouble();
            EpisodeThreshold = r.NextDouble() * 25 + 1;

            trueIndex = new double[n.factors.Count+2];
            falseIndex = new double[n.factors.Count+2];
            weight = new double[n.factors.Count+2];

            //for (int i = 0; i < n.factors.Count+2; i++)
            Parallel.For(0, n.factors.Count + 2, i =>
            {
                trueIndex[i] = r.NextDouble();
                falseIndex[i] = r.NextDouble();
                weight[i] = r.NextDouble();
            });

            shows = n.shows;

            mutationrate = r.NextDouble();
            mutationintensity = r.NextDouble();
        }

        //public PredictionModel(PredictionModel m, Network n)
        //{
        //    isMutated = false;

        //    NetworkAverageIndex = m.NetworkAverageIndex;
        //    EpisodeThreshold = m.EpisodeThreshold;

        //    trueIndex = new double[n.factors.Count + 2];
        //    falseIndex = new double[n.factors.Count + 2];
        //    weight = new double[n.factors.Count + 2];

        //    //for (int i = 0; i < n.factors.Count+2; i++)
        //    Parallel.For(0, n.factors.Count + 2, i =>
        //    {
        //        trueIndex[i] = m.trueIndex[i];
        //        falseIndex[i] = m.falseIndex[i];
        //        weight[i] = m.weight[i];
        //    });

        //    shows = n.shows;

        //    mutationrate = m.mutationrate;
        //    mutationintensity = m.mutationintensity;
        //}

        public PredictionModel(Network n, double average, double thresh) //Clean Slate prediction model based on average
        {
            isMutated = false;
            NetworkAverageIndex = average;
            EpisodeThreshold = thresh;

            trueIndex = new double[n.factors.Count+2];
            falseIndex = new double[n.factors.Count+2];
            weight = new double[n.factors.Count+2];

            //for (int i = 0; i < n.factors.Count+2; i++)
            Parallel.For(0, n.factors.Count + 2, i =>
            {
                trueIndex[i] = NetworkAverageIndex;
                falseIndex[i] = NetworkAverageIndex;
                weight[i] = 0;
            });

            shows = n.shows;

            Random r = new Random();

            mutationrate = r.NextDouble();
            mutationintensity = r.NextDouble();
        }    
        
        private PredictionModel(PredictionModel x, PredictionModel y)
        {
            isMutated = false;

            shows = x.shows;

            NetworkAverageIndex = Breed(x.NetworkAverageIndex, y.NetworkAverageIndex);
            EpisodeThreshold = Breed(x.EpisodeThreshold, y.EpisodeThreshold);

            trueIndex = new double[x.trueIndex.Length];
            falseIndex = new double[x.falseIndex.Length];
            weight = new double[x.weight.Length];

            //for (int i = 0; i < x.trueIndex.Length; i++)
            Parallel.For(0, x.trueIndex.Length, i =>
            {
                trueIndex[i] = Breed(x.trueIndex[i], y.trueIndex[i]);
                falseIndex[i] = Breed(x.falseIndex[i], y.falseIndex[i]);
                weight[i] = Breed(x.weight[i], y.weight[i]);
            });

            mutationrate = Breed(x.mutationrate, y.mutationrate);
            mutationintensity = Breed(x.mutationintensity, y.mutationintensity);
        }

        private double Breed(double x, double y)
        {
            var r = new Random();
            var p = r.NextDouble();

            //return (x * p) + (y * (1 - p));

            return p > 0.5 ? x : y;
        }

        double GetExponent(double value)
        {
            return Math.Log(value) / Math.Log(NetworkAverageIndex);
        }

        public double GetAverageExponent(int i, bool isTrue)
        {
            double trueExponent = GetExponent(trueIndex[i]), falseExponent = GetExponent(falseIndex[i]);

            double total = 0;
            int count = 0;

            if (i == trueIndex.Length - 2)
            {
                foreach (Show s in shows)
                    if (s.ratings.Count > 0)
                    {
                        total += (s.Episodes > EpisodeThreshold) ? trueExponent : falseExponent;
                        count++;
                    }   
            }
            else if (i == trueIndex.Length - 1)
            {
                foreach (Show s in shows)
                    if (s.ratings.Count > 0)
                    {
                        total += s.Halfhour ? trueExponent : falseExponent;
                        count++;
                    }
            }
            else
            {
                foreach (Show s in shows)
                    if (s.ratings.Count > 0)
                    {
                        total += s.factorValues[i] ? trueExponent : falseExponent;
                        count++;
                    }
            }

            double average = total / count;
            average = isTrue ? trueExponent / average : falseExponent / average;
            average = average * weight[i] + 1 - weight[i];

            return average;
        }

        public void SetElite()
        {
            _accuracy = 0;
        }

        public double TestAccuracy()
        {

            double average = GetAverageThreshold();
            double weightAverage = Math.Max(average, 1 - average);

            double total = 0, odds = 0.5;
            double weights = 0, weight = 0;
            int prediction = 0, accuracy = 0, year = NetworkDatabase.MaxYear;

            List<double> debugweights = new List<double>();

            foreach (Show s in shows.ToList())
            {
                if (s.Renewed || s.Canceled)
                {
                    prediction = (s.ShowIndex > GetThreshold(s)) ? 1 : 0;

                    if (s.Renewed)
                    {
                        
                        accuracy = (prediction == 1) ? 1 : 0;

                        

                        if (accuracy == 1)
                            weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;
                        else
                            weight = 1;

                        weight /= year - s.year + 1;

                        if (s.Canceled)
                        {
                            odds = GetOdds(s,true);

                            if (odds < 0.6 && odds > 0.4)
                            {
                                accuracy = 1;

                                weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;

                                if (prediction == 1)
                                    weight *= 2;
                                else
                                    weight /= 2;
                            }
                            else if (prediction == 0)
                                weight /= 2;
                        }

                        total += accuracy * weight;
                        weights += weight;
                    }
                    else if (s.Canceled)
                    {
                        accuracy = (prediction == 0) ? 1 : 0;

                        if (accuracy == 1)
                            weight = 1 - Math.Abs(weightAverage - s.ShowIndex) / weightAverage;
                        else
                            weight = 1;

                        weight /= year - s.year + 1;

                        total += accuracy * weight;
                        weights += weight;
                    }     
                }
            }

            _accuracy = (weights == 0) ? 0.0 : (total / weights);

            return _accuracy;
        }

        public double GetAverageThreshold()
        {
            double total = 0;
            int count = 0;

            foreach (Show s in shows.ToList())
                if (s.ratings.Count > 0)
                {
                    total += GetThreshold(s);
                    count++;
                }          

            return total / count;
        }

        public double GetThreshold(Show s)
        {
            double exponent = 1;

            for (int i = 0; i < s.factorValues.Count; i++)
                exponent *= (s.factorValues[i] ? GetExponent(trueIndex[i]) : GetExponent(falseIndex[i])) * weight[i] + 1 - weight[i];

            int x = s.factorValues.Count;
            exponent *= (s.Episodes > EpisodeThreshold ? GetExponent(trueIndex[x]) : GetExponent(falseIndex[x])) * weight[x] + 1 - weight[x];

            x++;
            exponent *= (s.Halfhour ? GetExponent(trueIndex[x]) : GetExponent(falseIndex[x])) * weight[x] + 1 - weight[x];

            return Math.Pow(NetworkAverageIndex, exponent);
        }

        public double GetOdds(Show s, bool raw = false)
        {
            var threshold = GetThreshold(s);
            var exponent = Math.Log(0.5) / Math.Log(threshold);
            var baseOdds = Math.Pow(s.ShowIndex, exponent);

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

        public double GetNetworkRatingsThreshold(int year)
        {
            return GetTargetRating(year, GetAverageThreshold());
        }

        public double GetTargetRating(int year, double targetindex)
        {

            var tempShows = new ObservableCollection<Show>();
            shows.Sort();

            foreach (Show s in shows)
                if (s.year == year && s.ratings.Count > 0)
                    tempShows.Add(s);

            if (tempShows.Count == 0)
            {
                tempShows.Clear();
                foreach (Show s in shows)
                    if (s.ratings.Count > 0) tempShows.Add(s);
            }

            if (tempShows.Count > 1) //make sure list is sorted
            {
                bool sorted = false;
                while (!sorted)
                {
                    sorted = true;
                    for (int i = 1; i < tempShows.Count; i++)
                        if (tempShows[i].ShowIndex > tempShows[i - 1].ShowIndex)
                        {
                            sorted = false;
                            tempShows.Move(i, i - 1);
                        }
                }
            }

            bool found = false;
            int upper = 0, lower = 1;
            double maxIndex = 1, minIndex = 0;
            double maxRating = 1, minRating = 0;

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
                if (lower != 0 && lower > upper && tempShows.Count > 1) //match is between two values
                {
                    maxIndex = tempShows[upper].ShowIndex;
                    maxRating = tempShows[upper].AverageRating;
                    minIndex = tempShows[lower].ShowIndex;
                    minRating = tempShows[lower].AverageRating;
                }
                else if (lower == 0 && tempShows.Count > 1) //match is at the beginning of a multiple item list
                {
                    maxIndex = tempShows[0].ShowIndex;
                    maxRating = tempShows[0].AverageRating;
                    minIndex = tempShows[1].ShowIndex;
                    minRating = tempShows[1].AverageRating;
                }
                else
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

        public void MutateModel()
        {
            var r = new Random();

            if (r.NextDouble() > 0.5)
                mutationrate = MutateValue(mutationrate);
            else
                mutationintensity = MutateValue(mutationintensity);

            NetworkAverageIndex = MutateValue(NetworkAverageIndex);
            EpisodeThreshold = MutateValue((EpisodeThreshold / 26.0)) * 26.0;

            //for (int i = 0; i < trueIndex.Length; i++)
            Parallel.For(0, trueIndex.Length, i =>
            {
                trueIndex[i] = MutateValue(trueIndex[i]);
                falseIndex[i] = MutateValue(falseIndex[i]);
                weight[i] = MutateValue(weight[i]);
            });
        }

        private double MutateValue(double d, bool increase = false)
        {
            var r = new Random();

            var p = r.NextDouble();

            double low = d * (1 - mutationintensity * p), high = 1 - (1 - d) * (1 - mutationintensity * p);

            if (increase) low = d;

            if (r.NextDouble() < mutationrate)
            {
                isMutated=true;
                return r.NextDouble() * (high - low) + low;
            }
                

            return d;
        }

        public void IncreaseMutationRate()
        {
            mutationrate = MutateValue(mutationrate, true);
        }

        public int CompareTo(PredictionModel other)
        {
            double otherAcc = other._accuracy;
            double thisAcc = _accuracy;

            return otherAcc.CompareTo(thisAcc);

            //if (otherAcc > thisAcc) return -1;
            //else if (thisAcc < otherAcc) return 1;
            //else return 0;
        }

        public static PredictionModel operator+(PredictionModel x, PredictionModel y)
        {
            
            var model = new PredictionModel(x, y);
            model.MutateModel();

            return model;
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
