using System;
using System.ComponentModel;

namespace TV_Ratings_Predictions
{
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
}
