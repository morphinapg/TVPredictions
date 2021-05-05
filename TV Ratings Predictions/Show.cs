using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace TV_Ratings_Predictions
{
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
        public List<double> ratings, viewers;
        public double AverageRating, ShowIndex, PredictedOdds, AverageViewers;
        public double OldRating, OldOdds, FinalPrediction, OldViewers;
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

        /// <summary>
        /// Please declare properies manually
        /// </summary>
        public Show()
        {

        }

        public Show(string ShowName, Network n, int season, ObservableCollection<bool> FactorList, int EpisodeCount, bool isHalfHour, ObservableCollection<string> names, double avgR = 0, double index = 1, string status = "", bool ren = false, bool can = false, double avgV = 0)
        {
            Name = ShowName;
            factorValues = FactorList;
            _episodes = EpisodeCount;
            _halfhour = isHalfHour;
            year = NetworkDatabase.CurrentYear;
            ratings = new List<double>();
            viewers = new List<double>();
            factorNames = names;
            AverageRating = avgR;
            AverageViewers = avgV;
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
            double CalculatedAverage = CalculateAverage(ratings.Count),
                CurrentDrop = (CalculatedAverage > 0) ? CalculatedAverage / ratings[0] : 1;
            AverageRating = CalculatedAverage * network.AdjustAverage(ratings.Count, Episodes, CurrentDrop);

            if (viewers.Count > 0)
            {
                CalculatedAverage = CalculateAverage(viewers.Count, true);
                CurrentDrop = (CalculatedAverage > 0) ? CalculatedAverage / viewers[0] : 1;
                AverageViewers = CalculatedAverage * network.AdjustAverage(viewers.Count, Episodes, CurrentDrop, true);
            }            
        }

        public void UpdateAllAverages(int start)
        {
            Parallel.For(start, 26, i => ratingsAverages[i] = CalculateAverage(i + 1));
        }

        public double CalculateAverage(int EpisodeNumber, bool view = false)
        {
            double
                total = 0,
                weights = 0;

            if (view)
                for (int i = 0; i < Math.Min(EpisodeNumber, viewers.Count); i++)
                {
                    double w = Math.Pow(i + 1, 2);

                    total += viewers[i] * w;
                    weights += w;
                }
            else
                for (int i = 0; i < Math.Min(EpisodeNumber, ratings.Count); i++)
                {
                    double w = Math.Pow(i + 1, 2);

                    total += ratings[i] * w;
                    weights += w;
                }

            if (ratings.Count > Episodes) Episodes = ratings.Count;


            return (weights > 0 ? total / weights : 0);
        }

        public double CurrentAverage(bool Viewers = false)
        {
            //return AverageRating / network.AdjustAverage(ratings.Count, Episodes);
            return CalculateAverage(ratings.Count, Viewers);
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
}
