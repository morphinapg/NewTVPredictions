using Avalonia.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TV_Ratings_Predictions;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class PredictionModel : Predictable
    {
        //The neural networks core to the function of the PredictionModel
        [DataMember]
        NeuralNetwork RatingsModel, RenewalModel;

        [DataMember]
        public bool IsMutated = false;

        [DataMember]
        public double? RatingsAvg, RatingsDev, ViewersAvg, ViewersDev;

        //A reference to the parent network

        public PredictionModel(Network network)
        {
            Network = network;
            NetworkName = network.Name;
            var Shows = Network.Shows;

            ///Model 1 - Ratings Model
            ///Input 0: Number of Episodes in the season
            ///Input 1: Current number of episodes being tested
            ///Inputs 2-27: Ratings/Viewers for that episode
            ///Output: Value representative of the expected final season performance
            ///Output is relative to the projected final season performance
            ///
            ///Model will be optimized to output values that match ratings/viewers range

            //Calculate statistics for all shows
            var WeightedShows = Network.GetWeightedShows();

            var AllEpisodes = WeightedShows.Select(x => new StatsContainer(x.Show.Episodes, x.Weight));

            //Define starting input average and deviation values, as well as output bias

            List<double[]>
                InputAverage = new(),
                InputDeviation = new(),
                OutputBias = new();

            double[]
                RatingAverage = new double[28],
                RatingDeviation = new double[28],
                ViewerAverage = new double[28],
                ViewerDeviation = new double[28];

            //Set default values
            CalculateStats(AllEpisodes, out RatingAverage[0], out RatingDeviation[0]);
            AllEpisodes = WeightedShows.Select(x => Enumerable.Range(0, x.Show.Episodes).Select(ep => new StatsContainer(ep, x.Weight))).SelectMany(x => x);
            CalculateStats(AllEpisodes, out RatingAverage[1], out RatingDeviation[1]);

            ViewerAverage[0] = RatingAverage[0];
            ViewerDeviation[0] = RatingDeviation[0];
            ViewerAverage[1] = RatingAverage[1];
            ViewerDeviation[1] = RatingDeviation[1];

            Dictionary<int, double>
                AverageRatings = Network.GetAverageRatingPerYear(0),
                AverageViewers = Network.GetAverageRatingPerYear(1);

            double[]
                RatingOffsets = Network.GetEpisodeOffsets(AverageRatings, 0),
                ViewerOffsets = Network.GetEpisodeOffsets(AverageViewers, 1),
                RatingDeviations = Network.GetDeviations(AverageRatings, RatingOffsets, 0),
                ViewerDeviations = Network.GetDeviations(AverageViewers, ViewerOffsets, 1);

            for (int i = 0; i < 26; i++)
            {
                var EpisodeIndex = i + 2;

                RatingAverage[EpisodeIndex] = 0;
                RatingDeviation[EpisodeIndex] = RatingDeviations[i];
                ViewerAverage[EpisodeIndex] = 0;
                ViewerDeviation[EpisodeIndex] = ViewerDeviations[i];
            }

            InputAverage.Add(RatingAverage);
            InputAverage.Add(ViewerAverage);

            InputDeviation.Add(RatingDeviation);
            InputDeviation.Add(ViewerDeviation);

            OutputBias.Add(new double[1] { 0 });
            OutputBias.Add(new double[1] { 0 });

            RatingsModel = new NeuralNetwork(InputAverage, InputDeviation, OutputBias);

            ///Model 2: Renewal model
            ///Input 0: Season #
            ///Input 1: PreviousEpisodes
            ///Input 2: Episodes per season
            ///Input 3: Show is Half Hour length or not
            ///Input 4: The broadcast year
            ///Input 5+: True/False factors (different for every network)
            ///
            ///Output 0: Renewal Threshold: Ratings
            ///Output 1: Renewal Threshold: Viewers
            ///Output 2: Blend value, will become 0 to 1 with sigmoid applied
            ///
            ///Blend value will allow the PredictionModel to blend both renewal thresholds
            ///Blend value will also blend the ratings/viewer performance values from Model 1
            ///
            ///Comparing the blended renewal threshold to the blended ratings/viewer number
            ///allows for assessing show performance, as well as assessing prediction error


            //Define input bias and weight for renewal model
            InputAverage = new();
            InputDeviation = new();
            OutputBias = new();

            var NumberOfFactors = network.Factors.Count;
            var NumberOfInputs = NumberOfFactors + 5;
            var average = new double[NumberOfInputs];
            var deviation = new double[NumberOfInputs];
            var outputbias = new double[3];

            // Array of selectors to simplify the initial calculations
            var selectors = new Func<IEnumerable<StatsContainer>>[]
            {
                () => WeightedShows.Select(x => new StatsContainer( x.Show.Season, x.Weight)),
                () => WeightedShows.Select(x => new StatsContainer(x.Show.PreviousEpisodes, x.Weight)),
                () => WeightedShows.Select(x => new StatsContainer(x.Show.Episodes, x.Weight)),
                () => WeightedShows.Select(x => new StatsContainer( x.Show.HalfHour ? 1 : -1, x.Weight)),
                () => WeightedShows.Where(x => x.Show.Year.HasValue).Select(x => new StatsContainer( x.Show.Year!.Value,x.Weight))
            };

            // Loop through the initial selectors and calculate stats
            for (int i = 0; i < selectors.Length; i++)
            {
                CalculateStats(selectors[i](), out average[i], out deviation[i]);
            }

            // Loop through the factors and calculate stats
            for (int i = 5; i < NumberOfInputs; i++)
            {
                var factorIndex = i - 5;
                CalculateStats(WeightedShows.Select(x => new StatsContainer(x.Show.Factors[factorIndex].IsTrue ? 1 : -1, x.Weight)),
                               out average[i], out deviation[i]);
            }

            InputAverage.Add(average);
            InputDeviation.Add(deviation);

            outputbias[0] = 0;
            outputbias[1] = 0;
            outputbias[2] = 0;
            OutputBias.Add(outputbias);

            //Find expected renewal threshold in both ratings and viewer numbers
            RenewalModel = new NeuralNetwork(InputAverage, InputDeviation, OutputBias);
        }

        public PredictionModel(PredictionModel other)
        {
            Network = other.Network;
            NetworkName = other.NetworkName;
            RatingsModel = new NeuralNetwork(other.RatingsModel);
            RenewalModel = new NeuralNetwork(other.RenewalModel);
            Accuracy = other.Accuracy;
            Error = other.Error;
        }

        // Helper method to calculate weighted average and deviation
        void CalculateStats(IEnumerable<StatsContainer> stats, out double average, out double deviation)
        {
            var WeightSum = stats.Sum(x => x.Weight);
            var avg = stats.Sum(x => x.Value * x.Weight) / WeightSum;
            average = avg;
            deviation = Math.Sqrt(stats.Select(x => Math.Pow(x.Value - avg, 2) * x.Weight).Sum() / WeightSum);
        }

        /// <summary>
        /// Predict two outputs for a given show. Formatted in Log10.
        /// </summary>
        /// <param name="Show">The Show to be predicted</param>
        /// <returns>
        /// Output 0: Show performance in Ratings,
        /// Output 1: Show performance in Viewers,
        /// Output 2: Renewal threshold in Ratings,
        /// Output 3: Renewal threshold in Viewers,
        /// Output 4: Blend factor
        /// </returns>
        public double[] GetOutputs(Show Show, int Episode, List<double> Ratings, List<double> Viewers)
        {

            var RatingsScore = GetPerformance(Show, 0, Episode, Ratings);

            var ViewersScore = GetPerformance(Show, 1, Episode, Viewers);

            //Next, we need to calculate renewal threshold and blend level

            //List<double>
            //    AdjustedRatings = Ratings.Select((x, i) => x - AverageRating - RatingsOffsets[i]).ToList(),
            //    AdjustedViewers = Viewers.Select((x, i) => x - AverageViewers - ViewerOffsets[i]).ToList();

            //double
            //    ProjectedRatingsOffset = GetProjectedRating(RatingsOffsets.ToList(), Show.Episodes),
            //    ProjectedViewersOffset = GetProjectedRating(ViewerOffsets.ToList(), Show.Episodes),
            //    ProjectedRating = GetProjectedRating(AdjustedRatings, Show.Episodes) + ProjectedRatingsOffset + AverageRating,
            //    ProjectedViewers = GetProjectedRating(AdjustedViewers, Show.Episodes) + ProjectedViewersOffset + AverageViewers;

            var output = GetThresholds(Show);
            var RatingThreshold = output[0];
            var ViewerThreshold = output[1];
            var Blend = 1 / (1 + Math.Exp(-1 * output[2]));

            return new double[] { RatingsScore, ViewersScore, RatingThreshold, ViewerThreshold, Blend };
        }

        /// <summary>
        /// Get the show Ratings/Viewers performance for the season
        /// </summary>
        /// <param name="Show">The show to be predicted</param>
        /// <param name="SeasonAverage">The expected average season rating for this year, Log10</param>
        /// <param name="InputType">0 for Ratings, 1 for Viewers</param>
        /// <param name="Episode">Specify the first x number of episodes</param>
        /// <param name="Ratings">List of current ratings</param>
        /// <returns>A number representing the season-wide performance of the show, in Ratings or Viewers (Log10)</returns>
        public double GetPerformance(Show Show, int InputType, int Episode, List<double> Ratings)
        {
            if (Ratings is null)
                Ratings = InputType == 0 ?
                    Show.Ratings.Where(x => x is not null).Select(x => Math.Log10(Math.Max(x!.Value, 0.004))).ToList() :
                    Show.Viewers.Where(x => x is not null).Select(x => Math.Log10(Math.Max(x!.Value, 0.0004))).ToList();
            var NumberOfEpisodes = Ratings.Count;
            NumberOfEpisodes = Math.Min(Episode, NumberOfEpisodes);

            var NumberOfInputs = NumberOfEpisodes + 2;

            var inputs = new double[NumberOfInputs];

            inputs[0] = Show.Episodes;
            inputs[1] = NumberOfEpisodes;
            for (int i = 0; i < NumberOfEpisodes; i++)
                inputs[i + 2] = Ratings[i];

            var output = RatingsModel.GetOutput(inputs, InputType)[0];

            return output;
        }

        /// <summary>
        /// Get the renewal thresholds and blend factor for the show
        /// </summary>
        /// <param name="Show">The show being predicted</param>
        /// <returns>
        /// Output 0: Renewal Threshold for Ratings (Log10 adjustment to expected season average),
        /// Output 1: Renewal Threshold for Viewers (Log10 adjustment to expected season average),
        /// Output 2: Blend factor
        /// </returns>
        public double[] GetThresholds(Show Show)
        {
            if (Show.Year is null)
                throw new Exception("The Show is missing a Year value!");

            var NumberOfInputs = Show.Factors.Count + 5;
            var inputs = new double[NumberOfInputs];

            inputs[0] = Show.Season;
            inputs[1] = Show.PreviousEpisodes;
            inputs[2] = Show.Episodes;
            inputs[3] = Show.HalfHour ? 1 : -1;
            inputs[4] = Show.Year.Value;
            for (int i = 5; i < NumberOfInputs; i++)
            {
                var factorIndex = i - 5;
                inputs[i] = Show.Factors[factorIndex].IsTrue ? 1 : -1;
            }

            return RenewalModel.GetOutput(inputs);
        }

        public void TestAccuracy(PredictionStats Stats, IEnumerable<WeightedShow> WeightedShows)
        {
            var outputs = TestAccuracy(this, Stats, WeightedShows);

            Accuracy = outputs[0];
            Error = outputs[1];

            RatingsAvg = outputs[2];
            RatingsDev = outputs[3];

            ViewersAvg = outputs[4];
            ViewersDev = outputs[5];
        }

        /// <summary>
        /// Blend two PredictionModels together
        /// </summary>
        /// <param name="x">First PredictionModel</param>
        /// <param name="y">Second PredictionModel</param>
        public PredictionModel(PredictionModel x, PredictionModel y)
        {
            RatingsModel = x.RatingsModel + y.RatingsModel;
            RenewalModel = x.RenewalModel + y.RenewalModel;
            Network = x.Network;
            NetworkName = x.NetworkName;

            //The margins of error will ultimately be rewritten when TestAccuracy is calculated
            //But because they are needed to calculate the error value of renanceled shows (renewed for final season)
            //a temporary estimate will be retained which is the average of the two values
            //In all likelihood, this value should be pretty close to the final calculated margins

            //var AllKeys =
            //    x.MarginOfError.Keys
            //    .Concat(y.MarginOfError.Keys)
            //    .Concat(x.RatingMargin.Keys)
            //    .Concat(y.RatingMargin.Keys)
            //    .Concat(x.ViewerMargin.Keys)
            //    .Concat(y.ViewerMargin.Keys)
            //    .Distinct();

            //foreach (var Key in AllKeys)
            //{
            //    //First, set MarginOfError
            //    double? value1 = null, value2 = null;
            //    if (x.MarginOfError.ContainsKey(Key))
            //        value1 = x.MarginOfError[Key];

            //    if (y.MarginOfError.ContainsKey(Key))
            //        value2 = y.MarginOfError[Key];

            //    var avg = new[] { value1, value2 }.Average();

            //    if (avg is not null)
            //        MarginOfError[Key] = avg.Value;

            //    //Next, set RatingMargin

            //    value1 = null;
            //    value2 = null;

            //    if (x.RatingMargin.ContainsKey(Key))
            //        value1 = x.RatingMargin[Key];

            //    if (y.RatingMargin.ContainsKey(Key))
            //        value2 = y.RatingMargin[Key];

            //    avg = new[] { value1, value2 }.Average();

            //    if (avg is not null)
            //        RatingMargin[Key] = avg.Value;

            //    //Finally, set ViewerMargin

            //    value1 = null;
            //    value2 = null;

            //    if (x.ViewerMargin.ContainsKey(Key))
            //        value1 = x.ViewerMargin[Key];

            //    if (y.ViewerMargin.ContainsKey(Key))
            //        value2 = y.ViewerMargin[Key];

            //    avg = new[] { value1, value2 }.Average();

            //    if (avg is not null)
            //        ViewerMargin[Key] = avg.Value;
            //}
        }

        public static PredictionModel operator+(PredictionModel x, PredictionModel y)
        {
            return new PredictionModel(x, y);
        }

        /// <summary>
        /// Mutate the PredictionModel
        /// </summary>
        public void MutateModel()
        {
            RatingsModel.MutateModel();
            RenewalModel.MutateModel();

            if (RatingsModel.IsMutated || RenewalModel.IsMutated)
                IsMutated = true;

            if (IsMutated)
            {
                Accuracy = null;
                Error = null;
            }
        }

        double GetRatingsError(int Episodes, double SeasonAverage, double Rating, int InputType)
        {
            var NumberOfInputs = Episodes + 1;
            var inputs = new double[NumberOfInputs];

            inputs[0] = Episodes;
            inputs[1] = Episodes;
            for (int i = 0; i < Episodes; i++)
                inputs[i + 2] = Rating - SeasonAverage;

            var output = RatingsModel.GetOutput(inputs, InputType)[0];

            return output - Rating;
        }

        public void IncreaseMutationRate()
        {
            var r = Random.Shared;

            if (r.NextDouble() < 0.5)
                RatingsModel.IncreaseMutationRate();
            else
                RenewalModel.IncreaseMutationRate();
        }

        public  double GetRatingsPerformance(double Rating, double RatingsAverage, int InputType)
        {
            double?
                ModelAvg = InputType == 0 ? RatingsAvg : ViewersAvg,
                ModelDev = InputType == 0 ? RatingsDev : ViewersDev,
                NetworkDev = InputType == 0 ? Network.RatingsDev : Network.ViewersDev;

            var RatingDev = InputType == 0 ? (Rating - ModelAvg) / ModelDev : (Rating - ModelAvg) / ModelDev;

            return (RatingDev is not null && NetworkDev is not null) ?
                        (RatingDev.Value * NetworkDev.Value) + RatingsAverage :
                        Rating + RatingsAverage;
        }

        public double GetCurrentPerformance(double BlendedDifference, double Blend)
        {
            double? scale = (Network.RatingsDev / RatingsDev) * Blend + (Network.ViewersDev / ViewersDev) * (1 - Blend);

            if (scale.HasValue)
                return Math.Pow(10, BlendedDifference * scale.Value);
            else
                return Math.Pow(10, BlendedDifference);
        }
    }    
}
