using Avalonia.Media.Imaging;
using MathNet.Numerics.Distributions;
using System;
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
    public class PredictionModel : Predictable, IComparable<PredictionModel>
    {
        //The neural networks core to the function of the PredictionModel
        [DataMember]
        NeuralNetwork RatingsModel, RenewalModel;           

        //A reference to the parent network
        [DataMember]
        public Network Network;        

        public PredictionModel(Network network)
        {
            Network = network;
            var Shows = Network.Shows;

            ///Model 1 - Ratings Model
            ///Input 0: Number of Episodes in the season
            ///Inputs 1-26: Ratings/Viewers for that episode
            ///Output: Value representative of the expected final season performance
            ///
            ///Model will be optimized to output values that match ratings/viewers range

            //Calculate statistics for all shows
            var WeightedShows = Network.GetWeightedShows();

            var AllEpisodes = WeightedShows.Select(x => new StatsContainer(x.Show.Episodes, x.Weight));

            var AllRatings = WeightedShows.Select(x => x.Show.Ratings.Select(y => new { Show = x.Show, Weight = x.Weight, Rating = Math.Log10(Convert.ToDouble(y)) })).SelectMany(x => x).Select(x => new StatsContainer(x.Rating, x.Weight));
            double RatingsAverage, RatingsDeviation;
            CalculateStats(AllRatings, out RatingsAverage, out RatingsDeviation);

            var AllViewers = WeightedShows.Select(x => x.Show.Viewers.Select(y => new { Show = x.Show, Weight = x.Weight, Viewer = Math.Log10(Convert.ToDouble(y)) })).SelectMany(x => x).Select(x => new StatsContainer(x.Viewer, x.Weight));
            double ViewersAverage, ViewersDeviation;
            CalculateStats(AllViewers, out ViewersAverage, out ViewersDeviation);

            //Define starting input average and deviation values, as well as output bias

            List<double[]>
                InputAverage = new(),
                InputDeviation = new(),
                OutputBias = new();

            double[]
                RatingAverage = new double[27],
                RatingDeviation = new double[27],
                ViewerAverage = new double[27],
                ViewerDeviation = new double[27];

            //Set default values
            CalculateStats(AllEpisodes, out RatingAverage[0], out RatingDeviation[0]);

            ViewerAverage[0] = RatingAverage[0];
            ViewerDeviation[0] = RatingDeviation[0];

            for (int i = 0; i < 26; i++)
            {
                var EpisodeIndex = i + 1;

                RatingAverage[EpisodeIndex] = RatingsAverage;
                RatingDeviation[EpisodeIndex] = RatingsDeviation;
                ViewerAverage[EpisodeIndex] = ViewersAverage;
                ViewerDeviation[EpisodeIndex] = ViewersDeviation;
            }

            InputAverage.Add(RatingAverage);
            InputAverage.Add(ViewerAverage);

            InputDeviation.Add(RatingDeviation);
            InputDeviation.Add(ViewerDeviation);

            OutputBias.Add(new double[1] { RatingsAverage });
            OutputBias.Add(new double[1] { ViewersAverage });

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

            outputbias[0] = RatingsAverage;
            outputbias[1] = ViewersAverage;
            outputbias[2] = 0;
            OutputBias.Add(outputbias);

            //Find expected renewal threshold in both ratings and viewer numbers
            RenewalModel = new NeuralNetwork(InputAverage, InputDeviation, OutputBias);
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
        public double[] GetOutputs(Show Show, int? Episode = null)
        {
            //First, we need to calculate the Show's current performance in both ratings and viewers
            var output = GetPerformance(Show, 0, Episode);
            var RatingsScore = output[0];

            output = GetPerformance(Show, 1, Episode);
            var ViewersScore = output[0];

            //Next, we need to calculate renewal threshold and blend level

            output = GetThresholds(Show);
            var RatingThreshold = output[0];
            var ViewerThreshold = output[1];
            var Blend = 1 / (1 + Math.Exp(-1 * output[2]));

            return new double[] { RatingsScore, ViewersScore, RatingThreshold, ViewerThreshold, Blend };
        }

        /// <summary>
        /// Get the show Ratings/Viewers performance for the season
        /// </summary>
        /// <param name="Show">The show to be predicted</param>
        /// <param name="InputType">0 for Ratings, 1 for Viewers</param>
        /// <param name="Episode">Specify the first x number of episodes</param>
        /// <returns>A number representing the season-wide performance of the show, in Ratings or Viewers (Log10)</returns>
        public double[] GetPerformance(Show Show, int InputType = 0, int? Episode = null)
        {
            var NumberOfEpisodes = InputType == 0 ? Show.Ratings.Count : Show.Viewers.Count;
            if (Episode is not null)
                NumberOfEpisodes = Math.Min(Episode.Value, NumberOfEpisodes);

            var NumberOfInputs = NumberOfEpisodes + 1;

            var inputs = new double[NumberOfInputs];

            inputs[0] = Show.Episodes;
            for (int i = 0; i < NumberOfEpisodes; i++)
                inputs[i + 1] = Convert.ToDouble(Show.Ratings[i]);

            return RatingsModel.GetOutput(inputs, InputType);
        }

        /// <summary>
        /// Get the renewal thresholds and blend factor for the show
        /// </summary>
        /// <param name="Show">The show being predicted</param>
        /// <returns>
        /// Output 0: Renewal Threshold in Ratings (Log10),
        /// Output 1: Renewal Threshold in Viewers (Log10),
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

        /// <summary>
        /// Return blended performance and renewal for a Show at current # of episodes
        /// </summary>
        /// <param name="Show">Show to be predicted</param>
        /// <returns>
        /// Output 0: Show Performance (blended, Log10 value),
        /// Output 1: Renewal Threshold (blended, Log10 value)
        /// </returns>
        public double[] GetPerformanceAndThreshold(Show Show, int? Episode = null)
        {
            var outputs = GetOutputs(Show, Episode);

            var Blend = outputs[4];
            var ShowPerformance = outputs[0] * Blend + outputs[1] * (1 - Blend);
            var ShowThreshold = outputs[2] * Blend + outputs[3] * (1 - Blend);

            return new double[] { ShowPerformance, ShowThreshold };
        }

        /// <summary>
        /// Get the renewal odds of a Show. Make sure MarginOfError is already set
        /// </summary>
        /// <param name="Show">Show to be predicted</param>
        /// </param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(Show Show)
        {
            var outputs = GetPerformanceAndThreshold(Show);
            var Episodes = new EpisodePair(Math.Min(Show.Ratings.Count, Show.Viewers.Count), Show.Episodes);
            return GetOdds(outputs, Episodes);
        }

        /// <summary>
        /// Get the renewal odds of a show, given the Performance and Threshold.
        /// MarginOfError needs to have been calculated already
        /// </summary>
        /// <param name="outputs">Output of GetPerformanceAndThreshold</param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(double[] outputs, EpisodePair Episodes)
        {
            var ShowPerformance = outputs[0];
            var ShowThreshold = outputs[1];

            var Normal = new Normal(ShowThreshold, MarginOfError[Episodes]);

            return Normal.CumulativeDistribution(ShowPerformance);
        }

        public void TestAccuracy()
        {
            //First, calculate the error of the RatingsModel
            //The first error check tests the RatingsModel by feeding it the same rating number for every episode for every possible ratings count
            //If the RatingsModel is accurate, the output of the model should be identical to the numbers fed into the model
            //This basically ensures that the average prediction of RatingsModel is in the correct range

            //We need to first generate a weighted selection of the shows
            var WeightedShows = Network.GetWeightedShows();

            //Next, we need to check for errors in the RatingsModel
            var RatingsActions = GetRatingsActions(WeightedShows);

            //Finally, we need to check for whether the RenewalModel predicts renewal thresholds correctly for Renewed and Canceled shows
            var PredictionActions = GetPredictionActions(WeightedShows);

            //Now that we have the actions necessary, we need to calculate prediction accuracy:
            var EpisodePairs = Network.GetEpisodePairs();
            var RatingsResults = RatingsActions.AsParallel().Select(x => x());
            var PredictionResults = PredictionActions.AsParallel().Select(x => x());
            
            SetAccuracyAndError(RatingsResults, PredictionResults, EpisodePairs);
        }

        /// <summary>
        /// Get all actions necessary to calculate the error for RatingsModel
        /// </summary>
        /// <param name="WeightedShows">Optionally provide a collection of WeightedShows</param>
        /// <returns>IEnumerable of all the actions needed to calculate the errors</returns>
        public IEnumerable<Func<ErrorContainer>> GetRatingsActions(IEnumerable<WeightedShow>? WeightedShows = null)
        {
            if (WeightedShows is null)
                WeightedShows = Network.GetWeightedShows();

            //Now, we need to select all possible ratings values, and group them with the weight for that year
            var WeightedRatings = WeightedShows.Select(x => x.Show.Ratings.Select(y => new StatsContainer(Convert.ToDouble(y), x.Weight))).SelectMany(x => x).Distinct();
            var WeightedViewers = WeightedShows.Select(x => x.Show.Viewers.Select(y => new StatsContainer(Convert.ToDouble(y), x.Weight))).SelectMany(x => x).Distinct();
            var AllEpisodeCounts = WeightedShows.Select(x => x.Show.Episodes).Distinct();

            //Now we need to generate the tasks needed to calculate the errors resulting from the RatingsModel not matching the correct values
            var RatingsActions_Middle = AllEpisodeCounts.Select(x => new IEnumerable<Func<ErrorContainer>>[]
            {
                WeightedRatings.Select(y => new Func<ErrorContainer>( () => GetWeightedRatingsError_Episodes(x, y.Value, y.Weight, 0))),
                WeightedViewers.Select(y => new Func<ErrorContainer>( () => GetWeightedRatingsError_Episodes(x, y.Value, y.Weight, 1)))
            }).SelectMany(x => x.SelectMany(y => y));

            //The next step is to look for predicted ratings values that fall outside of the expected minimum/maximum range
            //To do this, we need to analyze the range of values that appear in every show in the network

            var RatingRatio = Convert.ToDouble(WeightedShows.Select(x => x.Show.Ratings.Max() - x.Show.Ratings.Min()).Max());
            var ViewerRatio = Convert.ToDouble(WeightedShows.Select(x => x.Show.Viewers.Max() - x.Show.Viewers.Min()).Max());

            var RatingActions_Bounds = WeightedShows.Select(x => Enumerable.Range(1, x.Show.Ratings.Count).Select(y => new Func<ErrorContainer>(() => GetWeightedRatingsError_Show(x.Show, x.Weight, 0, y, RatingRatio)))).SelectMany(x => x);
            var ViewerActions_Bounds = WeightedShows.Select(x => Enumerable.Range(1, x.Show.Viewers.Count).Select(y => new Func<ErrorContainer>(() => GetWeightedRatingsError_Show(x.Show, x.Weight, 1, y, ViewerRatio)))).SelectMany(x => x);

            return RatingsActions_Middle.Concat(RatingActions_Bounds).Concat(ViewerActions_Bounds);
        }

        public IEnumerable<Func<ShowErrorContainer>> GetPredictionActions(IEnumerable<WeightedShow>? WeightedShows = null)
        {
            if (WeightedShows is null)
                WeightedShows = Network.GetWeightedShows();

            return WeightedShows.Where(x => x.Show.Renewed || x.Show.Canceled).Select(x => Enumerable.Range(1, Math.Max(x.Show.Ratings.Count, x.Show.Viewers.Count)).Select(y => new Func<ShowErrorContainer>(() => GetWeightedShowError(x.Show, x.Weight, y)))).SelectMany(x => x);
        }

        /// <summary>
        /// Returns an error and weight associated with a target ratings/weight pair
        /// </summary>
        /// <param name="Episodes">Number of episodes in a season</param>
        /// <param name="Rating">The target rating to test</param>
        /// <param name="Weight">The weight of the rating being tested</param>
        /// <param name="InputType">0 = Ratings, 1 = Viewers</param>
        /// <returns>A StatsContainer with the Error and Weight</returns>
        ErrorContainer GetWeightedRatingsError_Episodes(int Episodes, double Rating, double Weight, int InputType)
        {
            var NumberOfInputs = Episodes + 1;

            var inputs = new double[NumberOfInputs];

            inputs[0] = Episodes;
            for (int i = 0; i < Episodes; i++)
                inputs[i + 1] = Rating;

            var output = RatingsModel.GetOutput(inputs, InputType)[0];
            var Error = Math.Abs(output - Rating);

            return new ErrorContainer(this, Error, Weight);
        }

        /// <summary>
        /// Checks if RatingsModel output is outside of Max/Min range compared to existing ratings
        /// </summary>
        /// <param name="Show">Show to be checked</param>
        /// <param name="Weight">Weight of the show</param>
        /// <param name="InputType">0 = Ratings, 1 = Viewers</param>
        /// <param name="EpisodeNumber">How many episodes to test in this iteration</param>
        /// <param name="MaxRange">The Maximum allowed range between the lowest or highest rating, and the output of the model</param>
        /// <returns>A StatsContainer containing the error and weight for this test</returns>
        ErrorContainer GetWeightedRatingsError_Show(Show Show, double Weight, int InputType, int EpisodeNumber, double MaxRange)
        {
            var output = GetPerformance(Show, InputType, EpisodeNumber)[0];

            var Ratings = InputType == 0 ? Show.Ratings.Take(EpisodeNumber) : Show.Viewers.Take(EpisodeNumber);

            var RatingsMax = Convert.ToDouble(Ratings.Max());
            var RatingsMin = Convert.ToDouble(Ratings.Min());

            var CalculatedRange = Math.Max(output - RatingsMin, RatingsMax - output);

            var CalculatedError = Math.Max(CalculatedRange - MaxRange, 0);

            return new ErrorContainer(this, CalculatedError, Weight);            
        }

        /// <summary>
        /// Checks if the show was predicted correctly, and if not, returns an error
        /// </summary>
        /// <param name="Show">Show to be tested</param>
        /// <param name="Weight">The weight of the show</param>
        /// <param name="EpisodeNumber">The number of episodes to test</param>
        /// <returns></returns>
        ShowErrorContainer GetWeightedShowError(Show Show, double Weight, int EpisodeNumber)
        {
            //Retrieve ratings and viewer performance and thresholds
            var Outputs = GetOutputs(Show, EpisodeNumber);
            double Error = 0,
                RatingPerformance = Outputs[0],
                ViewerPerformance = Outputs[1],
                RatingThreshold = Outputs[2],
                ViewerThreshold = Outputs[3],
                Blend = Outputs[4],
                BlendedPerformance = RatingPerformance * Blend + ViewerPerformance * (1 - Blend),
                BlendedThreshold = RatingThreshold * Blend + ViewerThreshold * (1 - Blend),
                RatingDistance = Math.Abs(RatingPerformance - RatingThreshold),
                ViewerDistance = Math.Abs(ViewerPerformance - ViewerThreshold),
                BlendedDistance = Math.Abs(BlendedPerformance - BlendedThreshold),
                CurrentPosition = EpisodeNumber / (double)Show.Episodes;

            //First, check if the prediction was accurate

            bool
                PredictionCorrect = Show.Renewed && BlendedPerformance > BlendedThreshold || Show.Canceled && BlendedPerformance < BlendedThreshold,
                RatingCorrect = Show.Renewed && RatingPerformance > RatingThreshold || Show.Canceled && RatingPerformance < RatingThreshold,
                ViewerCorrect = Show.Canceled && ViewerPerformance > ViewerThreshold || Show.Canceled && ViewerPerformance < ViewerThreshold;

            var EpisodePair = new EpisodePair(EpisodeNumber, Show.Episodes);

            //Next, determine Error.
            if (Show.Renewed && Show.Canceled && MarginOfError.ContainsKey(EpisodePair))
            {
                //If the show is Renewed && Canceled(renewed for final season) and if there is a MarginOfError set
                //then a show in this position should ideally have a renewal probability of 55%
                //Error will be the distance between the show's performance and a target performance level that would achieve 55% odds

                double
                    Margin = MarginOfError[EpisodePair],
                    RMargin = RatingMargin.ContainsKey(EpisodePair) ? RatingMargin[EpisodePair] : Margin,
                    VMargin = ViewerMargin.ContainsKey(EpisodePair) ? ViewerMargin[EpisodePair] : Margin,
                    TargetRating = RatingThreshold + RMargin * 0.125661346855074,
                    TargetViewer = ViewerThreshold + VMargin * 0.125661346855074,
                    TargetBlended = BlendedPerformance + Margin * 0.125661346855074;

                Error += Math.Abs(RatingPerformance - TargetRating) * 0.5;
                Error += Math.Abs(ViewerPerformance - TargetViewer) * 0.5;
                Error += Math.Abs(BlendedPerformance - TargetBlended);
            }
            else
            {
                //Otherwise, Error should be 0 if the show was predicted correctly
                //If not, it should be the difference between the renewalthreshold and performance
                //Do this for ratings, viewers, and blended

                if (!RatingCorrect)
                    Error += RatingDistance * 0.5;

                if (!ViewerCorrect)
                    Error += ViewerDistance * 0.5;

                if (!PredictionCorrect)
                    Error += BlendedDistance;
            }

            return new ShowErrorContainer(this, PredictionCorrect, Error, Weight, CurrentPosition, RatingCorrect, ViewerCorrect, RatingDistance, ViewerDistance, BlendedDistance);

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

            //The margins of error will ultimately be rewritten when TestAccuracy is calculated
            //But because they are needed to calculate the error value of renanceled shows (renewed for final season)
            //a temporary estimate will be retained which is the average of the two values
            //In all likelihood, this value should be pretty close to the final calculated margins

            var AllKeys =
                x.MarginOfError.Keys
                .Concat(y.MarginOfError.Keys)
                .Concat(x.RatingMargin.Keys)
                .Concat(y.RatingMargin.Keys)
                .Concat(x.ViewerMargin.Keys)
                .Concat(y.ViewerMargin.Keys)
                .Distinct();

            foreach (var Key in AllKeys)
            {
                //First, set MarginOfError
                double? value1 = null, value2 = null;
                if (x.MarginOfError.ContainsKey(Key))
                    value1 = x.MarginOfError[Key];

                if (y.MarginOfError.ContainsKey(Key))
                    value2 = y.MarginOfError[Key];

                var avg = new[] { value1, value2 }.Average();

                if (avg is not null)
                    MarginOfError[Key] = avg.Value;

                //Next, set RatingMargin

                value1 = null;
                value2 = null;

                if (x.RatingMargin.ContainsKey(Key))
                    value1 = x.RatingMargin[Key];

                if (y.RatingMargin.ContainsKey(Key))
                    value2 = y.RatingMargin[Key];

                avg = new[] { value1, value2 }.Average();

                if (avg is not null)
                    RatingMargin[Key] = avg.Value;

                //Finally, set ViewerMargin

                value1 = null;
                value2 = null;

                if (x.ViewerMargin.ContainsKey(Key))
                    value1 = x.ViewerMargin[Key];

                if (y.ViewerMargin.ContainsKey(Key))
                    value2 = y.ViewerMargin[Key];

                avg = new[] { value1, value2 }.Average();

                if (avg is not null)
                    ViewerMargin[Key] = avg.Value;
            }
        }

        public static PredictionModel operator+(PredictionModel x, PredictionModel y)
        {
            return new PredictionModel(x, y);
        }

        /// <summary>
        /// Get all actions needed to mutate both NeuralNetworks in the PredictionModel
        /// </summary>
        /// <returns>IEnumerable of all actions needed to mutate the model</returns>
        public IEnumerable<Action> GetMutationActions()
        {
            return RatingsModel.GetMutationActions().Concat(RenewalModel.GetMutationActions());
        }

        /// <summary>
        /// Mutate the PredictionModel
        /// </summary>
        public void MutateModel(bool parallel = true)
        {
            var MutationActions = GetMutationActions();
            if (parallel)
                Parallel.ForEach(MutationActions, x => x());
            else
                foreach (var action in MutationActions)
                    action();

            RatingsModel.CheckIfNeuronsMutated();
            RenewalModel.CheckIfNeuronsMutated();
        }

        /// <summary>
        /// Provides the ability to sort Prediction models by most accurate first, then by lowest error
        /// </summary>
        /// <param name="other">PredictionModel to compare to</param>
        public int CompareTo(PredictionModel? other)
        {
            if (other is null) return 1;

            if (other.Accuracy == Accuracy)
            {
                if (other.Error == Error)
                    return 0;
                else if (other.Error < Error)
                    return 1;
                else
                    return -1;
            }
            else if (other.Accuracy > Accuracy)
                return 1;
            else
                return -1;
        }

        /// <summary>
        /// Check for equality between this PredictionModel and another object
        /// </summary>
        /// <param name="obj">Object to compare</param>
        /// <returns>true or false, whether the objects are equal</returns>
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;

            if (obj is PredictionModel model && model.Accuracy == Accuracy && model.Error == Error)
                return true;
            else
                return false;
        }

        public override int GetHashCode() => (Accuracy, Error).GetHashCode();

        public static bool operator ==(PredictionModel x, PredictionModel y) => x.Equals(y);

        public static bool operator !=(PredictionModel x, PredictionModel y) => !x.Equals(y);

        public static bool operator <(PredictionModel x, PredictionModel y) => x.Accuracy < y.Accuracy || (x.Accuracy == y.Accuracy && x.Error > y.Error);

        public static bool operator >(PredictionModel x, PredictionModel y) => x.Accuracy > y.Accuracy || (x.Accuracy == y.Accuracy && x.Error < y.Error);
    }    
}
