using System.ComponentModel;

namespace TV_Ratings_Predictions
{
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

    public class FactorContainer
    {
        public string Show;
        public double Index;
        public string Status;
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
