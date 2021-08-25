using System.Collections.Generic;
using System.ComponentModel;

namespace TV_Ratings_Predictions
{
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

        public RatingsContainer(Network n, Show s, bool Viewers = false)
        {
            network = n;

            Ratings = Viewers ? s.viewers : s.ratings;

            ShowName = s.NameWithSeason;

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
}
