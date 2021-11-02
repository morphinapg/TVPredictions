using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace TV_Ratings_Predictions
{
    [Serializable]
    public class NeuralPredictionModel : IComparable<NeuralPredictionModel>
    {
        [NonSerialized]
        public List<Show> shows;

        int NeuronCount, InputCount;
        Neuron[] FirstLayer, SecondLayer;
        Neuron Output;

        public double mutationrate, mutationintensity, neuralintensity, SeasonDeviation;

        [NonSerialized]
        public double _accuracy, _ratingstheshold, _score, _error, _targeterror;

        [NonSerialized]
        public bool isMutated;

        public double[] FactorBias;


        public NeuralPredictionModel(Network n) //New Random Prediction Model
        {
            shows = n.shows;
            isMutated = false;

            InputCount = n.factors.Count + 2;
            NeuronCount = Convert.ToInt32(Math.Round((InputCount + 1) * 2.0 / 3.0 + 1, 0));

            FirstLayer = new Neuron[NeuronCount];
            SecondLayer = new Neuron[NeuronCount];

            for (int i = 0; i < NeuronCount; i++)
            {
                FirstLayer[i] = new Neuron(InputCount + 1);
                SecondLayer[i] = new Neuron(NeuronCount);
            }

            Output = new Neuron(NeuronCount);

            Random r = new Random();
            mutationrate = r.NextDouble();
            mutationintensity = r.NextDouble();
            neuralintensity = r.NextDouble();

            FactorBias = GetAverages(n.factors);
            SeasonDeviation = GetSeasonDeviation(n.factors);
        }

        public double GetSeasonDeviation(ObservableCollection<string> factors)
        {
            var yearlist = shows.Select(x => x.year).Distinct();
            var tempList = shows.Where(x => x.ratings.Count > 0).ToList();
            var SeasonAverage = GetSeasonAverage(factors);
            double weights = 0;
            double totals = 0;

            foreach (int i in yearlist)
            {
                var segment = tempList.Where(x => x.year == i);
                var w = 1.0 / (NetworkDatabase.MaxYear - i + 1);
                totals += segment.AsParallel().Select(x => Math.Pow(x.Season - SeasonAverage, 2)).Sum() * w;
                weights += segment.Count() * w;
            }

            return Math.Sqrt(totals / weights);
        }

        public NeuralPredictionModel(Network n, double midpoint) //New Prediction Model based on midpoint
        {
            shows = n.shows;
            isMutated = false;

            InputCount = n.factors.Count + 2;
            NeuronCount = Convert.ToInt32(Math.Round((InputCount + 1) * 2.0 / 3.0 + 1, 0));

            FirstLayer = new Neuron[NeuronCount];
            SecondLayer = new Neuron[NeuronCount];

            for (int i = 0; i < NeuronCount; i++)
            {
                FirstLayer[i] = new Neuron(InputCount + 1, midpoint, true);
                SecondLayer[i] = new Neuron(NeuronCount, midpoint, true);
            }

            Output = new Neuron(NeuronCount, midpoint, false);

            Random r = new Random();
            mutationrate = r.NextDouble();
            mutationintensity = r.NextDouble();
            neuralintensity = r.NextDouble();

            FactorBias = GetAverages(n.factors);
            SeasonDeviation = GetSeasonDeviation(n.factors);
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

            FactorBias = new double[InputCount + 1];
            for (int i = 0; i < InputCount + 1; i++)
                FactorBias[i] = Breed(x.FactorBias[i], y.FactorBias[i], r);

            SeasonDeviation = Breed(x.SeasonDeviation, y.SeasonDeviation, r);
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

            _accuracy = n._accuracy;
            _score = n._score;
            _error = n._error;
            _targeterror = n._targeterror;

            FactorBias = n.FactorBias;
            SeasonDeviation = n.SeasonDeviation;
        }

        public void SetElite()
        {
            _accuracy = 0;
        }

        public double GetThreshold(Show s)
        {
            //if (averages is null) averages = new double[InputCount + 1];

            //var averages = (FactorBias is null) ? GetAverages(s.factorNames) : FactorBias;

            //var inputs = new double[InputCount + 1];

            var inputs = GetInputs(s);

            double[]
                FirstLayerOutputs = new double[NeuronCount],
                SecondLayerOutputs = new double[NeuronCount];

            //for (int i = 0; i < InputCount - 2; i++)
            //    inputs[i] = (s.factorValues[i] ? 1 : -1) - averages[i];

            //inputs[InputCount - 2] = (s.Episodes / 26.0 * 2 - 1) - averages[InputCount - 2];
            //inputs[InputCount - 1] = (s.Halfhour ? 1 : -1) - averages[InputCount - 1];
            //inputs[InputCount] = (s.Season - averages[InputCount]) / SeasonDeviation;


            for (int i = 0; i < NeuronCount; i++)
                FirstLayerOutputs[i] = FirstLayer[i].GetOutput(inputs);

            for (int i = 0; i < NeuronCount; i++)
                SecondLayerOutputs[i] = SecondLayer[i].GetOutput(FirstLayerOutputs);

            s._calculatedThreshold = Math.Min(Math.Max((Output.GetOutput(SecondLayerOutputs, true) + 1) / 2, 0.000001), 0.999999);

            return s._calculatedThreshold;
        }

        public double[] GetInputsPlusIndex(Show s)
        {
            var BaseInputs = GetInputs(s);
            var Size = BaseInputs.Length;
            var NewInputs = new double[Size + 1];
            for (int i = 0; i < Size; i++)
                NewInputs[i] = BaseInputs[i];

            NewInputs[Size] = s.ShowIndex * 2 - 1;

            return NewInputs;
        }

        public double[] GetInputs(Show s)
        {
            var averages = (FactorBias is null) ? GetAverages(s.factorNames) : FactorBias;

            var inputs = new double[InputCount + 1];

            for (int i = 0; i < InputCount - 2; i++)
                inputs[i] = (s.factorValues[i] ? 1 : -1) - averages[i];

            inputs[InputCount - 2] = (s.Episodes / 26.0 * 2 - 1) - averages[InputCount - 2];
            inputs[InputCount - 1] = (s.Halfhour ? 1 : -1) - averages[InputCount - 1];
            inputs[InputCount] = (s.Season - averages[InputCount]) / SeasonDeviation;

            return inputs;
        }


        public double[] GetBaseInputs()
        {
            var inputs = (double[])shows[0].network.RealAverages.Clone();
            var averages = (FactorBias is null) ? GetAverages(shows[0].factorNames) : FactorBias;

            for (int i = 0; i < InputCount; i++)
                inputs[i] -= averages[i];

            inputs[InputCount] = (inputs[InputCount] - averages[InputCount]) / SeasonDeviation;

            return inputs;
        }

        public double GetModifiedThreshold(double[] inputs)
        {
            double[]
                FirstLayerOutputs = new double[NeuronCount],
                SecondLayerOutputs = new double[NeuronCount];

            for (int i = 0; i < NeuronCount; i++)
                FirstLayerOutputs[i] = FirstLayer[i].GetOutput(inputs);

            for (int i = 0; i < NeuronCount; i++)
                SecondLayerOutputs[i] = SecondLayer[i].GetOutput(FirstLayerOutputs);

            return Math.Min(Math.Max((Output.GetOutput(SecondLayerOutputs, true) + 1) / 2, 0.000001), 0.999999);
        }

        public double GetModifiedThreshold(Show s,int index, int index2 = -1, int index3 = -1)
        {
            //if (averages is null) averages = new double[InputCount + 1];

            var averages = (FactorBias is null) ? GetAverages(s.factorNames) : FactorBias;

            var inputs = GetBaseInputs();

            double[]
                FirstLayerOutputs = new double[NeuronCount],
                SecondLayerOutputs = new double[NeuronCount];

            if (index > -1)
            {
                inputs = GetInputs(s);
                //for (int i = 0; i < InputCount - 2; i++)
                //    inputs[i] = (s.factorValues[i] ? 1 : -1) - averages[i];

                //inputs[InputCount - 2] = (s.Episodes / 26.0 * 2 - 1) - averages[InputCount - 2];
                //inputs[InputCount - 1] = (s.Halfhour ? 1 : -1) - averages[InputCount - 1];
                //inputs[InputCount] = (s.Season - averages[InputCount]) / SeasonDeviation;

                inputs[index] = 0;  //GetScaledAverage(s, index);
                if (index2 > -1)
                {
                    inputs[index2] = (index2 == InputCount) ? (inputs[InputCount] - averages[InputCount]) / SeasonDeviation : s.network.RealAverages[index2] - averages[index2] ; // GetScaledAverage(s, index2);
                    if (index3 > -1) inputs[index3] = (index3 == InputCount) ? (inputs[InputCount] - averages[InputCount]) / SeasonDeviation : s.network.RealAverages[index3] - averages[index3]; // GetScaledAverage(s, index3);
                }
            }


            for (int i = 0; i < NeuronCount; i++)
                FirstLayerOutputs[i] = FirstLayer[i].GetOutput(inputs);

            for (int i = 0; i < NeuronCount; i++)
                SecondLayerOutputs[i] = SecondLayer[i].GetOutput(FirstLayerOutputs);

            s._calculatedThreshold = Math.Min(Math.Max((Output.GetOutput(SecondLayerOutputs, true) + 1) / 2, 0.000001), 0.999999);

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

                var RenewedShows = shows.Where(x => x.year == year && x.Renewed);
                var CanceledShows = shows.Where(x => x.year == year && x.Canceled);
                var NoStatus = shows.Where(x => x.year == year && !x.Renewed && !x.Canceled);

                if (index < factors.Count)
                {
                    var RenewedScore = RenewedShows.Count(x => x.factorValues[index]) * 1.0 + RenewedShows.Count(x => !x.factorValues[index]) * -1.0;
                    var CanceledScore = CanceledShows.Count(x => x.factorValues[index]) * 1.0 + CanceledShows.Count(x => !x.factorValues[index]) * -1.0;
                    var NoStatusScore = NoStatus.Count(x => x.factorValues[index]) * 1.0 + NoStatus.Count(x => !x.factorValues[index]) * -1.0;

                    if (RenewedShows.Count() > 0 && CanceledShows.Count() > 0)
                    {
                        RenewedScore /= RenewedShows.Count();
                        CanceledScore /= CanceledShows.Count();
                        var TrueAverage = (RenewedScore + CanceledScore) / 2;
                        score = TrueAverage * (RenewedShows.Count() + CanceledShows.Count()) + NoStatusScore;
                    }
                    else
                        score = RenewedScore + CanceledScore + NoStatusScore;
                }
                else if (index == factors.Count)
                {
                    //score = shows.Where(x => x.year == year).Select(x => x.Episodes).Average() / 26 * 2 - 1;

                    if (RenewedShows.Count() > 0 && CanceledShows.Count() > 0)
                    {
                        var RenewedScore = RenewedShows.Select(x => x.Episodes).Average() / 26 * 2 - 1;
                        var CanceledScore = CanceledShows.Select(x => x.Episodes).Average() / 26 * 2 - 1;

                        var TrueAverage = (RenewedScore + CanceledScore) / 2;

                        var NoStatusScore = (NoStatus.Count() > 0) ? NoStatus.Select(x => x.Episodes).Average() / 26 * 2 - 1 : 0;

                        score = TrueAverage * (RenewedShows.Count() + CanceledShows.Count()) + NoStatusScore * NoStatus.Count();
                    }
                    else
                    {
                        score = (count > 0) ? shows.Where(x => x.year == year).Select(x => x.Episodes).Average() / 26 * 2 - 1 : 0;
                        score *= count;
                    }
                }
                else if (index == factors.Count + 1)
                {
                    //score = shows.Where(x => x.year == year && x.Halfhour).Count() * 1.0 + shows.Where(x => x.year == year && !x.Halfhour).Count() * -1.0;

                    var RenewedScore = RenewedShows.Where(x => x.Halfhour).Count() * 1.0 + RenewedShows.Where(x => !x.Halfhour).Count() * -1.0;
                    var CanceledScore = CanceledShows.Where(x => x.Halfhour).Count() * 1.0 + CanceledShows.Where(x => !x.Halfhour).Count() * -1.0;
                    var NoStatusScore = NoStatus.Where(x => x.Halfhour).Count() * 1.0 + NoStatus.Where(x => !x.Halfhour).Count() * -1.0;

                    if (RenewedShows.Count() > 0 && CanceledShows.Count() > 0)
                    {
                        RenewedScore /= RenewedShows.Count();
                        CanceledScore /= CanceledShows.Count();
                        var TrueAverage = (RenewedScore + CanceledScore) / 2;
                        score = TrueAverage * (RenewedShows.Count() + CanceledShows.Count()) + NoStatusScore;
                    }
                    else
                        score = RenewedScore + CanceledScore + NoStatusScore;
                }
                else
                {
                    if (RenewedShows.Count() > 0 && CanceledShows.Count() > 0)
                    {
                        var RenewedScore = RenewedShows.Select(x => x.Season).Average();
                        var CanceledScore = CanceledShows.Select(x => x.Season).Average();

                        var TrueAverage = (RenewedScore + CanceledScore) / 2;

                        var NoStatusScore = (NoStatus.Count() > 0) ? NoStatus.Select(x => x.Season).Average() : 0;

                        score = TrueAverage * (RenewedShows.Count() + CanceledShows.Count()) + NoStatusScore * NoStatus.Count();
                    }
                    else
                    {
                        score = (count > 0) ? shows.Where(x => x.year == year).Select(x => x.Season).Average() : 0;
                        score *= count;
                    }
                }

                total += score * w;
            }

            return total / weight;
        }

        public double GetSeasonAverage(ObservableCollection<string> factors)
        {
            return GetScaledAverage(factors, InputCount);
        }

        public double[] GetAverages(ObservableCollection<string> factors)
        {
            var averages = new double[InputCount + 1];
            for (int i = 0; i < InputCount + 1; i++)
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
            //if (averages is null) averages = new double[InputCount + 1];

            if (parallel)
            {
                double[]
                    totals = new double[tempList.Count],
                    counts = new double[tempList.Count];

                Parallel.For(0, tempList.Count, i =>
                {
                    double weight = 1.0 / (year - tempList[i].year + 1);
                    totals[i] = GetThreshold(tempList[i]) * weight;
                    counts[i] = weight;
                });

                total = totals.Sum();
                count = counts.Sum();
            }
            else
                foreach (Show s in tempList)
                {
                    double weight = 1.0 / (year - s.year + 1);
                    total += GetThreshold(s) * weight;
                    count += weight;
                }

            return total / count;
        }

        public double GetSeasonAverageThreshold(int year)
        {
            double total = 0;

            year = CheckYear(year);

            var tempList = shows.Where(x => x.year == year && x.ratings.Count > 0).ToList();
            var count = tempList.Count;
            var totals = new double[count];

            Parallel.For(0, count, i => totals[i] = GetThreshold(tempList[i]));

            total = totals.Sum();

            return total / count;
        }

        private int CheckYear(int year)
        {
            var YearList = shows.Where(x => x.ratings.Count > 0).Select(x => x.year).Distinct().ToList();
            YearList.Sort();

            if (!YearList.Contains(year))
            {
                if (YearList.Contains(year - 1))
                    year--;
                else if (YearList.Contains(year + 1))
                    year++;
                else if (YearList.Where(x => x < year).Count() > 0)
                    year = YearList.Where(x => x < year).Last();
                else
                    year = YearList.Where(x => x > year).First();
            }

            return year;
        }

        //private double GetAdjustment(double NetworkAverage, double SeasonAverage)
        //{
        //    if (NetworkAverage * SeasonAverage == 0 || NetworkAverage == SeasonAverage)
        //        return 1;

        //    return Math.Log(NetworkAverage) / Math.Log(SeasonAverage);
        //}

        //public Dictionary<int, double> GetAdjustments(bool parallel = false)
        //{
        //    double average = GetAverageThreshold(parallel);
        //    var Adjustments = new Dictionary<int, double>();
        //    var years = shows.Select(x => x.year).ToList().Distinct();
        //    foreach (int y in years)
        //    {
        //        Adjustments[y] = 1;
        //        //Adjustments[y] = (y == NetworkDatabase.MaxYear) ? GetAdjustment(average, GetSeasonAverageThreshold(y)) : 1;
        //    }

        //    return Adjustments;
        //}

        public double GetModifiedOdds(Show s, double[] ModifiedFactors, bool raw = false)
        {
            var threshold = GetModifiedThreshold(ModifiedFactors);

            if (s.year == NetworkDatabase.MaxYear && !(s.Renewed || s.Canceled))
                threshold = Math.Pow(threshold, s.network.Adjustment);

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

            if (_targeterror == 0 || Double.IsNaN(_targeterror)) GetTargetError(s.factorNames);

            //The more overlap there is, the less confidence you can have in the prediction
            var Overlap = AreaOfOverlap(Math.Log(s.AverageRating), deviation, Math.Log(target), _targeterror);

            deviation = _targeterror;

            var zscore = variance / deviation;

            var normal = new Normal();

            var baseOdds = normal.CumulativeDistribution(zscore);

            //var exponent = Math.Log(0.5) / Math.Log(threshold);
            //var baseOdds = Math.Pow(s.ShowIndex, exponent);

            if (raw)
                return baseOdds;

            //var accuracy = _accuracy;
            var accuracy = 1 - Overlap;

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

        public double GetOdds(Show s, bool raw = false, bool modified = false, int index = -1, int index2 = -1, int index3 = -1)
        {
            var threshold = modified ? GetModifiedThreshold(s, index, index2, index3) : GetThreshold(s);

            if (s.year == NetworkDatabase.MaxYear && !(s.Renewed || s.Canceled))
                threshold = Math.Pow(threshold, s.network.Adjustment);

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

            if (_targeterror == 0) 
                GetTargetError(s.factorNames);

            //The more overlap there is, the less confidence you can have in the prediction
            var Overlap = AreaOfOverlap(Math.Log(s.AverageRating), deviation, Math.Log(target), _targeterror);

            deviation = _targeterror;

            var zscore = variance / deviation;

            var normal = new Normal();

            var baseOdds = normal.CumulativeDistribution(zscore);



            //var exponent = Math.Log(0.5) / Math.Log(threshold);
            //var baseOdds = Math.Pow(s.ShowIndex, exponent);

            if (raw)
                return baseOdds;

            //var accuracy = _accuracy;
            var accuracy = 1 - Overlap;

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

        public double GetTargetErrorParallel(ObservableCollection<string> factors)
        {
            GetAdjustment();
            var Averages = FactorBias; //GetAverages(factors);
            //var Adjustments = GetAdjustments();

            var ShowErrors = shows.AsParallel().Where(x => (x.Renewed || x.Canceled) && x.AverageRating > 0).Select(x =>
            {
                var weight = 1.0 / (NetworkDatabase.MaxYear - x.year + 1);
                var threshold = GetThreshold(x);
                var TargetRating = GetTargetRating(x.year, threshold);
                var Difference = Math.Abs(Math.Log(TargetRating) - Math.Log(x.AverageRating));
                double right, wrong;

                if ((x.Renewed && x.AverageRating > TargetRating) || (x.Canceled && x.AverageRating < TargetRating))
                {
                    right = 0;
                    wrong = Difference;
                }
                else
                {
                    right = Difference;
                    wrong = 0;
                }

                return new { Weight = weight, WrongValue = Math.Pow(wrong, 2) * weight, RightValue = Math.Pow(right, 2) * weight };
            });

            var TotalWeight = ShowErrors.Select(x => x.Weight).Sum();

            //var normal = new Normal();

            //var RightZ = normal.InverseCumulativeDistribution(0.682689492);
            //var WrongZ = normal.InverseCumulativeDistribution(0.317310508);

            //var RightWeights = ShowErrors.Select(x =>
            //{
            //    var Percentile = ShowErrors.Where(y => y.RightValue <= x.RightValue).Select(y => y.Weight).Sum() / TotalWeight;

            //    double weight = 0;
            //    if (Percentile > 0 && Percentile < 1)
            //    {
            //        var CurrentZ = normal.InverseCumulativeDistribution(Percentile) - RightZ;
            //        weight = normal.Density(CurrentZ);
            //    }

            //    return new { RightValue = x.RightValue, Weight = weight };
            //});

            //var WrongWeights = ShowErrors.Select(x =>
            //{
            //    var Percentile = ShowErrors.Where(y => y.WrongValue <= x.WrongValue).Select(y => y.Weight).Sum() / TotalWeight;

            //    double weight = 0;
            //    if (Percentile > 0 && Percentile < 1)
            //    {
            //        var CurrentZ = normal.InverseCumulativeDistribution(Percentile) - WrongZ;
            //        weight = normal.Density(CurrentZ);
            //    }

            //    return new { WrongValue = x.WrongValue, Weight = weight };
            //});

            //var RightDeviation = RightWeights.Select(x => x.RightValue * x.Weight).Sum() / RightWeights.Select(x => x.Weight).Sum();
            //var WrongDeviation = WrongWeights.Select(x => x.WrongValue * x.Weight).Sum() / WrongWeights.Select(x => x.Weight).Sum();

            var RightDeviation = Math.Sqrt(ShowErrors.Select(x => x.RightValue).Sum() / TotalWeight);
            var WrongDeviation = Math.Sqrt(ShowErrors.Select(x => x.WrongValue).Sum() / TotalWeight) * 0.408795841;


            _targeterror = (RightDeviation + WrongDeviation) / 2;

            

            return _targeterror;
        }

        public double GetAdjustment()
        {
            var ThisYear = shows.Where(x => x.year == NetworkDatabase.MaxYear).OrderBy(x => x.AverageRating).ToList();
            if (ThisYear.Count == 0)
                return 1;
            else
            {
                var tempList = shows.Where(x => x.Renewed || x.Canceled);
                var years = tempList.Select(x => x.year).Distinct();

                double total, weight, midpoint, maximum, currentweight;
                total = 0;
                weight = 0;

                foreach (int year in years)
                {
                    midpoint = GetNetworkRatingsThreshold(year, true);
                    maximum = GetTargetRating(year, 1);

                    currentweight = 1.0 / (NetworkDatabase.MaxYear - year + 1) * tempList.Where(x => x.year == year && (x.Renewed || x.Canceled)).Count();
                    total += midpoint / maximum * currentweight;
                    weight += currentweight;
                }

                maximum = GetTargetRating(NetworkDatabase.MaxYear, 1);
                var TargetRating = total / weight * maximum;

                double PreviousIndex = 0, CurrentTotal = 0, GrandTotal = ThisYear.Select(x => x.AverageRating * (x.Halfhour ? 0.5 : 1)).Sum(), NewIndex, PreviousRating = 0, CurrentRating, TargetIndex = -1, CurrentSegment;

                for (int i = 0; TargetIndex == -1 && i < ThisYear.Count; i++)
                {
                    CurrentRating = ThisYear[i].AverageRating;
                    CurrentSegment = CurrentRating * (ThisYear[i].Halfhour ? 0.5 : 1);

                    CurrentTotal += CurrentSegment / 2;

                    NewIndex = CurrentTotal / GrandTotal;

                    if (CurrentRating > TargetRating)
                        TargetIndex = (TargetRating - PreviousRating) / (CurrentRating - PreviousRating) * (NewIndex - PreviousIndex) + PreviousIndex;

                    CurrentTotal += CurrentSegment / 2;

                    PreviousIndex = NewIndex;
                    PreviousRating = CurrentRating;
                }

                if (TargetIndex == -1)
                    TargetIndex = (TargetRating - PreviousRating) / (maximum - PreviousRating) * (1 - PreviousIndex) + PreviousIndex;

                var OriginalIndex = GetModifiedThreshold(shows.First(), -1);

                var adjustment = Math.Log(TargetIndex) / Math.Log(OriginalIndex);


                var decided = ThisYear.Where(x => x.Renewed || x.Canceled).Count();
                var undecided = ThisYear.Where(x => !(x.Renewed || x.Canceled) && x.RenewalStatus == "").Count();

                var percentage = (undecided + decided > 0) ? undecided / (double)(decided + undecided) : 0;

                if (decided > 1)
                    percentage = Math.Pow(percentage, Math.Sqrt(decided+1)); ///The more shows have been decided, the less important the adjustment should be, to an exponential level

                int z = 0;
                if (double.IsNaN(adjustment * percentage + (1 - percentage)))
                    z = 1;


                return adjustment * percentage + (1 - percentage);
            }
        }

        public double GetTargetError(ObservableCollection<string> factors, bool parallel = false)
        {
            //if (averages is null) averages = new double[InputCount + 1];

            var averages = (FactorBias is null) ? new double[InputCount + 1] : FactorBias;
            //var Adjustments = GetAdjustments();

            var ShowErrors = shows.Where(x => (x.Renewed || x.Canceled) && x.AverageRating > 0).Select(x =>
            {
                var weight = 1.0 / (NetworkDatabase.MaxYear - x.year + 1);
                var threshold = GetThreshold(x);
                var TargetRating = GetTargetRating(x.year, threshold);
                var Difference = Math.Abs(Math.Log(TargetRating) - Math.Log(x.AverageRating));

                double right, wrong;

                if ((x.Renewed && x.AverageRating > TargetRating) || (x.Canceled && x.AverageRating < TargetRating))
                {
                    right = 0;
                    wrong = Difference;
                }
                else
                {
                    right = Difference;
                    wrong = 0;
                }

                return new { Weight = weight, WrongValue = Math.Pow(wrong, 2) * weight, RightValue = Math.Pow(right, 2) * weight };
            });

            var TotalWeight = ShowErrors.Select(x => x.Weight).Sum();

            //var normal = new Normal();

            //var RightZ = normal.InverseCumulativeDistribution(0.682689492);
            //var WrongZ = normal.InverseCumulativeDistribution(0.317310508);

            //var RightWeights = ShowErrors.Select(x =>
            //{
            //    var Percentile = ShowErrors.Where(y => y.RightValue <= x.RightValue).Select(y => y.Weight).Sum() / TotalWeight;

            //    double weight = 0;
            //    if (Percentile > 0 && Percentile < 1)
            //    {
            //        var CurrentZ = normal.InverseCumulativeDistribution(Percentile) - RightZ;
            //        weight = normal.Density(CurrentZ);
            //    }

            //    return new { RightValue = x.RightValue, Weight = weight };
            //});

            //var WrongWeights = ShowErrors.Select(x =>
            //{
            //    var Percentile = ShowErrors.Where(y => y.WrongValue <= x.WrongValue).Select(y => y.Weight).Sum() / TotalWeight;

            //    double weight = 0;
            //    if (Percentile > 0 && Percentile < 1)
            //    {
            //        var CurrentZ = normal.InverseCumulativeDistribution(Percentile) - WrongZ;
            //        weight = normal.Density(CurrentZ);
            //    }

            //    return new { WrongValue = x.WrongValue, Weight = weight };
            //});

            //var RightDeviation = RightWeights.Select(x => x.RightValue * x.Weight).Sum() / RightWeights.Select(x => x.Weight).Sum();
            //var WrongDeviation = WrongWeights.Select(x => x.WrongValue * x.Weight).Sum() / WrongWeights.Select(x => x.Weight).Sum();


            var RightDeviation = Math.Sqrt(ShowErrors.Select(x => x.RightValue).Sum() / TotalWeight);
            var WrongDeviation = Math.Sqrt(ShowErrors.Select(x => x.WrongValue).Sum() / TotalWeight) * 0.408795841;                

            _targeterror = (RightDeviation + WrongDeviation) / 2;
            return _targeterror;
        }

        public double TestAccuracy(bool parallel = false)
        {
            if (_targeterror == 0) GetTargetErrorParallel(shows[0].factorNames);

            //double average = GetAverageThreshold(parallel);

            //double weightAverage = Math.Max(average, 1 - average);

            double errors = 0;
            double totals = 0;
            double weights = 0;
            int year = NetworkDatabase.MaxYear;


            //var Adjustments = GetAdjustments(parallel);

            var tempList = shows.Where(x => x.Renewed || x.Canceled).ToList();

            double lowest = 1, scorehigh = 0;

            if (parallel)
            {
                double[]
                    t = new double[tempList.Count],
                    w = new double[tempList.Count],
                    error = new double[tempList.Count],
                    score = new double[tempList.Count];

                Parallel.For(0, tempList.Count, i =>
                {
                    Show s = tempList[i];
                    double threshold = GetThreshold(s);
                    int prediction = (s.ShowIndex > threshold) ? 1 : 0;
                    double odds = GetOdds(s, true);

                    if (s.Renewed)
                    {
                        int accuracy = (prediction == 1) ? 1 : 0;
                        double weight = 1;

                        weight /= year - s.year + 1;

                        if (s.Canceled)
                        {
                            score[i] = Math.Abs(odds - 0.55);
                            if ((odds < 0.4) || (odds > 0.5 && odds < 0.6))
                                weight /= 2;
                            else
                                weight /= 4;
                        }
                        else if (accuracy == 0)
                            error[i] = Math.Abs(odds - 0.5);

                        t[i] = accuracy * weight;
                        w[i] = weight;
                    }
                    else if (s.Canceled)
                    {
                        int accuracy = (prediction == 0) ? 1 : 0;
                        double weight = 1;

                        weight /= year - s.year + 1;
                        if (accuracy == 0)
                            error[i] = Math.Abs(odds - 0.5);

                        t[i] = accuracy * weight;
                        w[i] = weight;
                    }
                });

                Parallel.For(0, w.Length, i =>
                {
                    if (error[i] > 0) error[i] *= w[i];
                    if (score[i] > 0) score[i] *= w[i];
                });

                errors = error.Sum() + score.Sum();
                var smallError = error.Where(x => x > 0);
                var smallScore = score.Where(x => x > 0);
                var errorMin = smallError.Count() > 0 ? smallError.Min() : 0;
                var scoreMax = smallScore.Count() > 0 ? smallScore.Max() : 0;

                lowest = (errorMin > 0) ? errorMin : scoreMax;
                totals = t.Sum();
                weights = w.Sum();


            }
            else
            {
                foreach (Show s in tempList)
                {
                    double threshold = GetThreshold(s);
                    int prediction = (s.ShowIndex > threshold) ? 1 : 0;
                    double odds = GetOdds(s, true);

                    if (s.Renewed)
                    {
                        int accuracy = (prediction == 1) ? 1 : 0;
                        double weight = 1;

                        weight /= year - s.year + 1;

                        if (s.Canceled)
                        {
                            var dif = Math.Abs(odds - 0.55);
                            if (dif > scorehigh) scorehigh = dif;

                            //weight /= (odds < 0.6 && odds > 0.4) ? 4 : 2;
                            if ((odds < 0.4) || (odds > 0.5 && odds < 0.6))
                                weight /= 2;
                            else
                                weight /= 4;

                            errors += dif * weight;
                        }
                        else if (accuracy == 0)
                        {
                            var dif = Math.Abs(odds - 0.5);
                            if (dif < lowest) lowest = dif;
                            errors += dif * weight;
                        }


                        totals += accuracy * weight;
                        weights += weight;
                    }
                    else if (s.Canceled)
                    {
                        int accuracy = (prediction == 0) ? 1 : 0;
                        double weight = 1;

                        weight /= year - s.year + 1;
                        if (accuracy == 0)
                        {
                            var dif = Math.Abs(odds - 0.5);
                            if (dif < lowest) lowest = dif;
                            errors += dif * weight;
                        }

                        totals += accuracy * weight;
                        weights += weight;
                    }
                }

                if (lowest == 1) lowest = scorehigh;
            }

            _accuracy = (weights == 0) ? 0.0 : (totals / weights);
            _score = errors;
            _error = lowest;

            return _accuracy;
        }

        public double GetNetworkRatingsThreshold(int year, bool parallel)
        {

            //_ratingstheshold = GetTargetRating(year, GetAverageThreshold(parallel));
            var s = shows.First();

            year = CheckYear(year);

            //var Adjustment = GetAdjustments(parallel)[year];
            var threshold = GetModifiedThreshold(s, -1);

            if (year == NetworkDatabase.MaxYear)
                threshold = Math.Pow(threshold, s.network.Adjustment);

            _ratingstheshold = GetTargetRating(year, threshold);
            return _ratingstheshold;
        }

        public double GetTargetRating(int year, double targetindex)
        {

            var tempShows = (shows[0].network.ShowsPerYear is null) ? 
                shows.Where(x => x.year == year && x.ratings.Count > 0).OrderByDescending(x => x.ShowIndex).ToList() : 
                shows[0].network.ShowsPerYear[year];

            //var tempShows = shows.Where(x => x.year == year && x.ratings.Count > 0).OrderByDescending(x => x.ShowIndex).ToList();
            if (tempShows.Count == 0)
            {
                //var yearList = shows.Where(x => x.ratings.Count > 0).Select(x => x.year).ToList();
                //yearList.Sort();
                //if (yearList.Contains(year - 1))
                //    year--;
                //else if (yearList.Contains(year + 1))
                //    year++;
                //else if (yearList.Where(x => x < year).Count() > 0)
                //    year = yearList.Where(x => x < year).Last();
                //else
                //    year = yearList.Where(x => x > year).First();

                //year = yearList.Last();

                year = CheckYear(year);
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

            for (int i = 0; i < InputCount + 1; i++)
            {
                if (r.NextDouble() < mutationrate)
                {
                    FactorBias[i] += neuralintensity * (r.NextDouble() * 2 - 1);
                    isMutated = true;
                }
            }

            if (r.NextDouble() < mutationrate)
            {
                SeasonDeviation += neuralintensity * (r.NextDouble() * 2 - 1);
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
                return thisWeight.CompareTo(otherWeight);
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
            if (x._accuracy == y._accuracy && x._score == y._score && x._targeterror == y._targeterror)
                return true;
            else
                return false;
        }

        public static bool operator !=(NeuralPredictionModel x, NeuralPredictionModel y)
        {
            if (x._accuracy == y._accuracy && x._score == y._score && x._targeterror == y._targeterror)
                    return false;
            else
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
                    if (x._score < y._score)
                        return true;
                    else if (x._targeterror < y._targeterror)
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
                    if (x._score > y._score)
                        return true;
                    else if (x._targeterror > y._targeterror)
                        return true;
                    else
                        return false;
                }
                else
                    return false;
            }
        }
        /// <summary>
        /// Finds the percentage of potential ratings that are above the potential renewal thresholds. Use Log values.
        /// </summary>
        /// <param name="Mean1">Projected Rating</param>
        /// <param name="SD1">Standard Deviation for Projected Rating</param>
        /// <param name="Mean2">Renewal Threshold Rating</param>
        /// <param name="SD2">Standard Deviation for Renewal Thresholds</param>
        /// <returns></returns>
        double AreaOfOverlap(double Mean1, double SD1, double Mean2, double SD2, double baseOdds = -1)
        {
            //Find intersection points
            double point1, point2;

            var Distribution1 = new Normal(Mean1, SD1);
            var Distribution2 = new Normal(Mean2, SD2);

            if (SD1 * SD2 == 0)
                return 0;

            if (SD1 == SD2)
            {
                point1 = (Mean1 + Mean2) / 2;
                point2 = point1;
            }
            else
            {
                //Find Square of each SD
                double
                    Squared1 = Math.Pow(SD1, 2),
                    Squared2 = Math.Pow(SD2, 2);

                //Then find part that will be positive and negative radical

                var radical = Math.Sqrt(Math.Pow(Mean1 - Mean2, 2) + 2 * (Squared1 - Squared2) * Math.Log(SD1 / SD2));

                point1 = (Mean2 * Squared1 - SD2 * (Mean1 * SD2 + SD1 * radical)) / (Squared1 - Squared2);
                point2 = (Mean2 * Squared1 - SD2 * (Mean1 * SD2 + SD1 * -radical)) / (Squared1 - Squared2);
            }

            if (point1 == point2)
                return (1 - Distribution1.CumulativeDistribution(point1)) * 2;
            else
            {
                var min = Math.Min(point1, point2);
                var max = Math.Max(point1, point2);

                var beginning1 = Distribution1.CumulativeDistribution(min);
                var beginning2 = Distribution2.CumulativeDistribution(min);


                var middle1 = Distribution1.CumulativeDistribution(max) - Distribution1.CumulativeDistribution(min);
                var middle2 = Distribution2.CumulativeDistribution(max) - Distribution2.CumulativeDistribution(min);

                var end1 = 1 - Distribution1.CumulativeDistribution(max);
                var end2 = 1 - Distribution2.CumulativeDistribution(max);

                return Math.Min(beginning1, beginning2) + Math.Min(middle1, middle2) + Math.Min(end1, end2);
            }
        }
    }
}
