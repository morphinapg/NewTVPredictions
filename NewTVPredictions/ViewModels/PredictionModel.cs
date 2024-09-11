using Avalonia.Media.Imaging;
using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TV_Ratings_Predictions;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class PredictionModel : ViewModelBase
    {
        [DataMember]
        NeuralNetwork RatingsModel, RenewalModel;           //The neural networks core to the function of the PredictionModel

        [DataMember]
        double _error;                                      //Representation of how many incorrect predictions there were, and by how much
        public double Error                                 //Some additional error values may be added as well, to optimize the RatingsModel
        {
            get => _error;
            set
            {
                _error = value;
                OnPropertyChanged(nameof(Error));
            }
        }

        [DataMember]
        double _accuracy;
        public double Accuracy                              //Represents what % of shows the model predicts correctly
        {
            get => _accuracy;
            set
            {
                _accuracy = value;
                OnPropertyChanged(nameof(Accuracy));
            }
        }

        [DataMember]
        Network Network;

        public record StatsContainer(double Value, double Weight);
        public record WeightedShow(Show Show, double Weight);

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
            var WeightedShows = GetWeightedShows();

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
        /// Get the renewal odds of a Show
        /// </summary>
        /// <param name="Show">Show to be predicted</param>
        /// <param name="MarginOfError">
        /// Precalculated Margin of error.
        /// This will be based on the accuracy of the model for all shows around the same % episodes completed.
        /// </param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(Show Show, double MarginOfError)
        {
            var outputs = GetPerformanceAndThreshold(Show);
            return GetOdds(outputs, MarginOfError);
        }

        /// <summary>
        /// Get the renewal odds of a show, given the Performance and Threshold
        /// </summary>
        /// <param name="outputs">Output of GetPerformanceAndThreshold</param>
        /// <param name="MarginOfError">
        /// Precalculated Margin of error.
        /// This will be based on the accuracy of the model for all shows around the same % episodes completed.
        /// </param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(double[] outputs, double MarginOfError)
        {
            var ShowPerformance = outputs[0];
            var ShowThreshold = outputs[1];

            var Normal = new Normal(ShowThreshold, MarginOfError);

            return Normal.CumulativeDistribution(ShowPerformance);
        }

        public void TestAccuracy()
        {
            //First, calculate the error of the RatingsModel
            //The first error check tests the RatingsModel by feeding it the same rating number for every episode for every possible ratings count
            //If the RatingsModel is accurate, the output of the model should be identical to the numbers fed into the model
            //This basically ensures that the average prediction of RatingsModel is in the correct range

            //We need to first generate a weighted selection of the shows
            var WeightedShows = GetWeightedShows();

            //Next, we need to check for errors in the RatingsModel
            var RatingsActions = GetRatingsActions(WeightedShows);

            //Finally, we need to check for whether the RenewalModel predicts renewal thresholds correctly for Renewed and Canceled shows
            var PredictionActions = GetPredictionActions(WeightedShows);

            //Now that we have the actions necessary, we need to calculate prediction accuracy:
            var PredictionResults = PredictionActions.AsParallel().Select(x => x());
            var PredictionWeights = PredictionResults.Sum(x => x.Weight);
            var PredictionTotals = PredictionResults.Select(x => x.Value == 0 ? x.Weight : 0).Sum();

            Accuracy = PredictionTotals / PredictionWeights;

            PredictionTotals = PredictionResults.Sum(x => x.Value * x.Weight);

            //Now we can get the total error
            var RatingsResults = RatingsActions.AsParallel().Select(x => x());
            var RatingsWeights = RatingsResults.Sum(x => x.Weight);
            var RatingsTotals = RatingsResults.Sum(x => x.Value * x.Weight);

            var AllWeights = PredictionWeights + RatingsWeights;
            var AllTotals = PredictionTotals + RatingsTotals;

            Error = AllTotals / AllWeights;
        }

        /// <summary>
        /// Get all actions necessary to calculate the error for RatingsModel
        /// </summary>
        /// <param name="WeightedShows">Optionally provide a collection of WeightedShows</param>
        /// <returns>IEnumerable of all the actions needed to calculate the errors</returns>
        public IEnumerable<Func<StatsContainer>> GetRatingsActions(IEnumerable<WeightedShow>? WeightedShows = null)
        {
            if (WeightedShows is null)
                WeightedShows = GetWeightedShows();

            //Now, we need to select all possible ratings values, and group them with the weight for that year
            var WeightedRatings = WeightedShows.Select(x => x.Show.Ratings.Select(y => new StatsContainer(Convert.ToDouble(y), x.Weight))).SelectMany(x => x).Distinct();
            var WeightedViewers = WeightedShows.Select(x => x.Show.Viewers.Select(y => new StatsContainer(Convert.ToDouble(y), x.Weight))).SelectMany(x => x).Distinct();
            var AllEpisodeCounts = WeightedShows.Select(x => x.Show.Episodes).Distinct();

            //Now we need to generate the tasks needed to calculate the errors resulting from the RatingsModel not matching the correct values
            var RatingsActions_Middle = AllEpisodeCounts.Select(x => new IEnumerable<Func<StatsContainer>>[]
            {
                WeightedRatings.Select(y => new Func<StatsContainer>( () => GetWeightedRatingsError_Episodes(x, y.Value, y.Weight, 0))),
                WeightedViewers.Select(y => new Func<StatsContainer>( () => GetWeightedRatingsError_Episodes(x, y.Value, y.Weight, 1)))
            }).SelectMany(x => x.SelectMany(y => y));

            //The next step is to look for predicted ratings values that fall outside of the expected minimum/maximum range
            //To do this, we need to analyze the range of values that appear in every show in the network

            var RatingRatio = Convert.ToDouble(WeightedShows.Select(x => x.Show.Ratings.Max() - x.Show.Ratings.Min()).Max());
            var ViewerRatio = Convert.ToDouble(WeightedShows.Select(x => x.Show.Viewers.Max() - x.Show.Viewers.Min()).Max());

            var RatingActions_Bounds = WeightedShows.Select(x => Enumerable.Range(1, x.Show.Ratings.Count).Select(y => new Func<StatsContainer>(() => GetWeightedRatingsError_Show(x.Show, x.Weight, 0, y, RatingRatio)))).SelectMany(x => x);
            var ViewerActions_Bounds = WeightedShows.Select(x => Enumerable.Range(1, x.Show.Viewers.Count).Select(y => new Func<StatsContainer>(() => GetWeightedRatingsError_Show(x.Show, x.Weight, 1, y, ViewerRatio)))).SelectMany(x => x);

            return RatingsActions_Middle.Concat(RatingActions_Bounds).Concat(ViewerActions_Bounds);
        }

        public IEnumerable<Func<StatsContainer>> GetPredictionActions(IEnumerable<WeightedShow>? WeightedShows = null)
        {
            if (WeightedShows is null)
                WeightedShows = GetWeightedShows();

            return WeightedShows.Where(x => x.Show.Renewed || x.Show.Canceled).Select(x => Enumerable.Range(1, Math.Max(x.Show.Ratings.Count, x.Show.Viewers.Count)).Select(y => new Func<StatsContainer>(() => GetWeightedShowError(x.Show, x.Weight, y)))).SelectMany(x => x);
        }

        /// <summary>
        /// Generates a current list of shows weighted by year
        /// </summary>
        /// <returns>Enumerable of WeightedShow objects</returns>
        IEnumerable<WeightedShow> GetWeightedShows()
        {
            var now = DateTime.Now;
            double NextYear = now.Month < 9 ? now.Year : now.Year + 1;
            return Network.Shows.Where(x => x.Year.HasValue).Select(x => new WeightedShow(x, 1 / (NextYear - x.Year!.Value)));
        }

        /// <summary>
        /// Returns an error and weight associated with a target ratings/weight pair
        /// </summary>
        /// <param name="Episodes">Number of episodes in a season</param>
        /// <param name="Rating">The target rating to test</param>
        /// <param name="Weight">The weight of the rating being tested</param>
        /// <param name="InputType">0 = Ratings, 1 = Viewers</param>
        /// <returns>A StatsContainer with the Error and Weight</returns>
        StatsContainer GetWeightedRatingsError_Episodes(int Episodes, double Rating, double Weight, int InputType)
        {
            var NumberOfInputs = Episodes + 1;

            var inputs = new double[NumberOfInputs];

            inputs[0] = Episodes;
            for (int i = 0; i < Episodes; i++)
                inputs[i + 1] = Rating;

            var output = RatingsModel.GetOutput(inputs, InputType)[0];
            var Error = Math.Abs(output - Rating);

            return new StatsContainer(Error, Weight);
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
        StatsContainer GetWeightedRatingsError_Show(Show Show, double Weight, int InputType, int EpisodeNumber, double MaxRange)
        {
            var output = GetPerformance(Show, InputType, EpisodeNumber)[0];

            var Ratings = InputType == 0 ? Show.Ratings.Take(EpisodeNumber) : Show.Viewers.Take(EpisodeNumber);

            var RatingsMax = Convert.ToDouble(Ratings.Max());
            var RatingsMin = Convert.ToDouble(Ratings.Min());

            var CalculatedRange = Math.Max(output - RatingsMin, RatingsMax - output);

            var CalculatedError = Math.Max(CalculatedRange - MaxRange, 0);

            return new StatsContainer(CalculatedError, Weight);            
        }

        /// <summary>
        /// Checks if the show was predicted correctly, and if not, returns an error
        /// </summary>
        /// <param name="Show">Show to be tested</param>
        /// <param name="Weight">The weight of the show</param>
        /// <param name="EpisodeNumber">The number of episodes to test</param>
        /// <returns></returns>
        StatsContainer GetWeightedShowError(Show Show, double Weight, int EpisodeNumber)
        {
            var outputs = GetPerformanceAndThreshold(Show, EpisodeNumber);
            var Performance = outputs[0];
            var Threshold = outputs[1];

            if (Show.Renewed && Performance > Threshold || Show.Canceled && Performance < Threshold)
                return new StatsContainer(0, Weight);
            else
                return new StatsContainer(Math.Abs(Performance - Threshold), Weight);
        }
    }    
}
