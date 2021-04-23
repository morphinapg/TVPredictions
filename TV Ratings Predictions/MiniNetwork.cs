using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace TV_Ratings_Predictions
{
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
        public double SeasonDeviation;

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
            SeasonDeviation = n.SeasonDeviation;
        }
    }
}
