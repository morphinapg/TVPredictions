using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TV_Ratings_Predictions
{
    public class DetailsContainer : INotifyPropertyChanged
    {
        /// <summary>
        /// Name of the Factor
        /// </summary>
        public String Name;

        /// <summary>
        /// Value of the Factor
        /// </summary>
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
                return _value;
                //return Math.Round(_value, 4);
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

    class DetailsCombo
    {
        public List<DetailsContainer> details;
        public double BaseOdds, CurrentOdds;

        public DetailsCombo(List<DetailsContainer> d, double b, double c)
        {
            details = d;
            BaseOdds = b;
            CurrentOdds = c;
        }
    }
}
