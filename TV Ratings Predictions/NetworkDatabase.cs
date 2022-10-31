using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

using System.Linq;

namespace TV_Ratings_Predictions
{
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
        public static DateTime StartTime;               //Defines the time when the "Start Evolution" button was pressed
        private static string locks = "";

        public static bool written = false;

        public static MainPage mainpage;
        public static string Locks
        {
            get
            {
                return locks;
            }
            set
            {
                locks = value;
                LocksUpdated?.Invoke(locks, new EventArgs());
            }
        }
        public static event EventHandler LocksUpdated;

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
            Parallel.ForEach(NetworkList, n => n.model.GetNetworkRatingsThreshold(CurrentYear, false, true));    //Get the Average Renewal Threshold rating value for each network

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
                n.NetworkViewers = new ObservableCollection<RatingsContainer>();
                n.AlphabeticalShows = new ObservableCollection<Show>();
                n.Predictions = new ObservableCollection<PredictionContainer>();
                n.Averages = new ObservableCollection<AverageContainer>();

                //n.model = new NeuralPredictionModel(n, n.GetMidpoint());                               //This commented code is here if I ever need to test
                //n.evolution = new EvolutionTree(n, n.GetMidpoint());                                   //changes to the predictions with a fresh model
                //n.RealAverages = n.model.GetAverages(n.factors);

                n.model.shows = n.shows;
                Parallel.ForEach(n.shows, s =>
                {
                    s.factorNames = n.factors;
                    s.network = n;
                    //if (s.viewers is null) s.viewers = new List<double>();
                });

                n.evolution.network = n;
                //n.PredictionAccuracy = n.model.TestAccuracy() * 100;


                //n.RealAverages = n.model.GetAverages(n.factors);
                //n.Adjustment = n.model.GetAdjustment();
                n.Filter(CurrentYear);                                  //Once the Network is fully restored, perform a filter based on the current TV Season

                //n.model.FactorBias = n.model.GetAverages(n.factors);
                //n.model.SeasonDeviation = n.SeasonDeviation;
                //foreach (NeuralPredictionModel m in n.evolution.Primary)
                //{
                //    m.shows = n.shows;
                //    m.FactorBias = m.GetAverages(n.factors);
                //    m.SeasonDeviation = m.GetSeasonDeviation(n.factors);
                //}

                //foreach (NeuralPredictionModel m in n.evolution.Randomized)
                //{
                //    m.shows = n.shows;
                //    m.FactorBias = m.GetAverages(n.factors);
                //    m.SeasonDeviation = m.GetSeasonDeviation(n.factors);
                //}

                

            }
            );

            foreach (Network n in settings.NetworkList)                                 //After all networks are restored and filtered, add them to the global database
                NetworkList.Add(n);

            SortNetworks();                                             //and sort by Network Average Renewal threshold
                
        }

        public static void WriteSettings()  //Used to write the current database to the Settings file
        {
            var settings = new NetworkSettings(); //Full clone of all database data

            WriteToBinaryFile<NetworkSettings>(ApplicationData.Current.LocalFolder.Path + "\\Settings", settings);

            //var folder = ApplicationData.Current.LocalFolder;
            //var file = await folder.FileExistsAsync("Settings2") ? await folder.GetFileAsync("Settings2") : await folder.CreateFileAsync("Settings2");
            

            //await WriteToDataFileAsync<NetworkSettings>(file, settings);
        }

        static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        static async Task WriteToDataFileAsync<T>(StorageFile file, T objectToWrite)
        {
            if (file != null)
            {
                using (Stream stream = await file.OpenStreamForWriteAsync())
                {
                    stream.SetLength(0);
                    var serializer = new DataContractSerializer(typeof(T));
                    serializer.WriteObject(stream, objectToWrite);
                }
            }
        }

        static async Task<T> ReadDataFileAsync<T>(StorageFile file)
        {
            if (file != null)
            {
                using (Stream stream = await file.OpenStreamForReadAsync())
                {
                    stream.SetLength(0);
                    var serializer = new DataContractSerializer(typeof(T));
                    return (T)serializer.ReadObject(stream);
                }
            }
            else
                return default(T);
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

                Parallel.ForEach(NetworkList, n =>
                {
                    foreach (Show s in n.shows)
                    {
                        s.OldRating = s.AverageRating;
                        s.OldViewers = s.AverageViewers;
                        s.OldOdds = s.PredictedOdds;

                        if (s.RenewalStatus == "")
                            s.FinalPrediction = s.OldOdds;
                    }
                    n.UpdateOdds(true);
                });
            }
        }

        public static async void WriteObjectAsync<T>(object obj, string filename)
        {
            await DispatcherHelper.ExecuteOnUIThreadAsync(async () =>
            {
                var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Desktop, SuggestedFileName = filename };
                picker.FileTypeChoices.Add("XML Data File", new List<string>() { ".XML" });

                StorageFile file = await picker.PickSaveFileAsync();

                if (file != null)
                {
                    using (Stream stream = await file.OpenStreamForWriteAsync())
                    {
                        stream.SetLength(0);
                        var serializer = new DataContractSerializer(typeof(T));
                        serializer.WriteObject(stream, obj);
                    }
                }
            });           
        }
    }

    

    [Serializable]
    public class NetworkSettings
    {
        public ObservableCollection<Network> NetworkList;

        public NetworkSettings()
        {
            NetworkList = new ObservableCollection<Network>();
            foreach (Network n in NetworkDatabase.NetworkList)
                NetworkList.Add(new Network(n));
        }
    }
}
