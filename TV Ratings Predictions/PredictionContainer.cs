using System;
using System.ComponentModel;
using System.Linq;

namespace TV_Ratings_Predictions
{
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

        double _viewers;
        public string Viewers
        {
            get
            {
                return (show.viewers.Count > 0) ? Math.Round(_viewers, 2).ToString("F2") : "";
            }
        }

        public double RatingsDiff
        {
            get
            {
                return (show.OldRating == 0) ? 0 : Math.Round(_rating - show.OldRating, 2);
            }
        }

        public double ViewersDiff
        {
            get
            {
                return (show.OldViewers == 0) ? 0 : Math.Round(_viewers - show.OldViewers, 2);
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
                    return (show.OldRating == 0 && show.OldOdds == 0 && show.OldViewers == 0) ? 0 : Math.Round(odds - show.OldOdds, 2);
                else
                    return (show.OldRating == 0 && show.OldOdds == 0 && show.OldViewers == 0) ? 0 : Math.Round((odds - show.OldOdds) * 2, 2);
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
                if (show.OldRating == 0 && show.OldOdds == 0 && show.OldViewers == 0)
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
                    return (odds >= 0.5) ? "✔" : "❌";
                else if (show.Canceled)
                    return (odds <= 0.5) ? "✔" : "❌";
                else
                    return "";
            }
        }

        public PredictionContainer(Show s, Network n, bool a = false)
        {
            network = n;
            show = s;
            Show = s.NameWithSeason;
            odds = s.PredictedOdds;
            _rating = s.AverageRating;
            _viewers = s.AverageViewers;
            Status = s.RenewalStatus;

            //var Adjustments = n.model.GetAdjustments(true);
            var threshold = n.model.GetThreshold(s);
            if (s.year == NetworkDatabase.MaxYear && !(s.Renewed || s.Canceled))
                threshold = Math.Pow(threshold, s.network.Adjustment);

            _targetrating = n.model.GetTargetRating(s.year, threshold);
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
}
