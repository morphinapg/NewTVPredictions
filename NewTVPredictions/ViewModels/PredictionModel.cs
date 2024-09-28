using Avalonia.Media.Imaging;
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
    public class PredictionModel : Predictable
    {
        //The neural networks core to the function of the PredictionModel
        [DataMember]
        NeuralNetwork RatingsModel, RenewalModel;

        //A reference to the parent network

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

            var AllRatings = WeightedShows.Select(x => x.Show.Ratings.Where(x => x > 0).Select(y => new { Show = x.Show, Weight = x.Weight, Rating = Math.Log10(Convert.ToDouble(y)) })).SelectMany(x => x).Select(x => new StatsContainer(x.Rating, x.Weight));
            double RatingsAverage, RatingsDeviation;
            CalculateStats(AllRatings, out RatingsAverage, out RatingsDeviation);

            var AllViewers = WeightedShows.Select(x => x.Show.Viewers.Where(x => x > 0).Select(y => new { Show = x.Show, Weight = x.Weight, Viewer = Math.Log10(Convert.ToDouble(y)) })).SelectMany(x => x).Select(x => new StatsContainer(x.Viewer, x.Weight));
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
            var RatingsScore = GetPerformance(Show, 0, Episode);

            var ViewersScore = GetPerformance(Show, 1, Episode);

            //Next, we need to calculate renewal threshold and blend level

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
        /// <param name="InputType">0 for Ratings, 1 for Viewers</param>
        /// <param name="Episode">Specify the first x number of episodes</param>
        /// <returns>A number representing the season-wide performance of the show, in Ratings or Viewers (Log10)</returns>
        public double GetPerformance(Show Show, int InputType = 0, int? Episode = null)
        {
            var NumberOfEpisodes = InputType == 0 ? Show.Ratings.Count : Show.Viewers.Count;
            if (Episode is not null)
                NumberOfEpisodes = Math.Min(Episode.Value, NumberOfEpisodes);

            var NumberOfInputs = NumberOfEpisodes + 1;

            var inputs = new double[NumberOfInputs];

            inputs[0] = Show.Episodes;
            for (int i = 0; i < NumberOfEpisodes; i++)
                inputs[i + 1] = InputType == 0 ? Show.Ratings[i]!.Value : Show.Viewers[i]!.Value;

            return RatingsModel.GetOutput(inputs, InputType)[0];
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

        public void TestAccuracy(IEnumerable<WeightedShow>? WeightedShows = null)
        {
            //We need to first generate a weighted selection of the shows
            if (WeightedShows is null)
                WeightedShows = Network.GetWeightedShows();

            var AllEpisodeCounts = WeightedShows.Select(x => x.Show.Episodes).Distinct().ToList();

            double
                ErrorWeight = 0,
                ErrorTotal = 0,
                AccuracyWeight = 0,
                AccuracyTotal = 0;    

            //Optimize RenewalModel for accurate predictions
            //also optimize RatingsModel to generate values within expected range

            //var EpisodeMax = AllEpisodeCounts.Max();
            //List<double>[][] 
            //    CorrectErrors = new List<double>[EpisodeMax][],
            //    IncorrectErrors = new List<double>[EpisodeMax][],
            //    CorrectWeights = new List<double>[EpisodeMax][],
            //    IncorrectWeights = new List<double>[EpisodeMax][];

            //for (int i = 0; i < EpisodeMax; i++)
            //{
            //    if (AllEpisodeCounts.Contains(i + 1))
            //    {
            //        CorrectErrors[i] = new List<double>[i + 1];
            //        IncorrectErrors[i] = new List<double> [i + 1];
            //        CorrectWeights[i] = new List<double>[i + 1];
            //        IncorrectWeights[i] = new List<double>[i + 1];

            //        for (int j = 0; j < i + 1; j++)
            //        {
            //            CorrectErrors[i][j] = new();
            //            IncorrectErrors[i][j] = new();
            //            CorrectWeights[i][j] = new();
            //            IncorrectWeights[i][j] = new();
            //        }
            //    }
            //}

            //The next step is to look for predicted ratings values that fall outside of the expected minimum/maximum range
            //To do this, we need to analyze the range of values that appear in every show in the network

            var RatingRatio = Convert.ToDouble(WeightedShows.Select(x => x.Show.Ratings.Max() - x.Show.Ratings.Min()).Max());
            var ViewerRatio = Convert.ToDouble(WeightedShows.Select(x => x.Show.Viewers.Max() - x.Show.Viewers.Min()).Max());

            double[] outputs;
            double RatingsPerformance, ViewersPerformance, RatingsThreshold, ViewersThreshold, Blend, BlendedPerformance, BlendedThreshold, RatingsMax, RatingsMin, ViewersMin, ViewersMax, RatingsRange, ViewersRange, RatingsError, ViewersError, RatingsAvg, ViewersAvg;
            int Episode;
            IEnumerable<double?> Ratings, Viewers;

            foreach (var Show in WeightedShows)
            {
                
                RatingsAvg = Convert.ToDouble(Show.Show.Ratings.Average());
                ViewersAvg = Convert.ToDouble(Show.Show.Viewers.Average());  

                for (int i = 0; i < Show.Show.CurrentEpisodes; i++)
                {
                    Episode = i + 1;

                    //Get output data
                    outputs = GetOutputs(Show.Show, Episode);
                    RatingsPerformance = outputs[0];
                    ViewersPerformance = outputs[1];
                    RatingsThreshold = outputs[2];
                    ViewersThreshold = outputs[3];
                    Blend = outputs[4];
                    BlendedPerformance = RatingsPerformance * Blend + ViewersPerformance * (1 - Blend);
                    BlendedThreshold = RatingsThreshold * Blend + ViewersThreshold * (1 - Blend);
                    Ratings = Show.Show.Ratings.Take(Episode);
                    Viewers = Show.Show.Viewers.Take(Episode);

                    //First, test if the RatingsModel predicts an accurate value for the average ratings and viewers
                    ErrorWeight += Show.Weight * 2;
                    ErrorTotal += Math.Pow(RatingsPerformance - RatingsAvg, 2) * Show.Weight;
                    ErrorTotal += Math.Pow(ViewersPerformance - ViewersAvg, 2) * Show.Weight;

                    //Check if the ratings/viewer performance is outside the expected bounds
                    RatingsMax = Convert.ToDouble(Ratings.Max());
                    RatingsMin = Convert.ToDouble(Ratings.Min());
                    RatingsRange = Math.Max(RatingsPerformance - RatingsMin, RatingsMax - RatingsPerformance);
                    RatingsError = Math.Max(RatingsRange - RatingRatio, 0);
                    if (RatingsError > 0)
                        ErrorTotal += Math.Pow(RatingsError, 2) * Show.Weight;

                    ViewersMax = Convert.ToDouble(Viewers.Max());
                    ViewersMin = Convert.ToDouble(Viewers.Min());
                    ViewersRange = Math.Max(ViewersPerformance - ViewersMin, ViewersMax - ViewersPerformance);
                    ViewersError = Math.Max(ViewersRange - ViewerRatio, 0);
                    if (ViewersError > 0)
                        ErrorTotal += Math.Pow(ViewersError, 2) * Show.Weight;

                    //Test Accuracy
                    AccuracyWeight += Show.Weight * 2;
                    ErrorWeight += Show.Weight * 2;

                    if (Show.Show.Renewed)
                    {
                        if (RatingsPerformance > RatingsThreshold)
                            AccuracyTotal += 0.5 * Show.Weight;
                        if (ViewersPerformance > ViewersThreshold)
                            AccuracyTotal += 0.5 * Show.Weight;
                        if (BlendedPerformance > BlendedThreshold)
                            AccuracyTotal += Show.Weight;

                        if (Show.Show.Canceled)
                        {
                            ErrorTotal += Math.Pow(RatingsThreshold - RatingsPerformance, 2) * Show.Weight * 0.5;
                            ErrorTotal += Math.Pow(ViewersThreshold - ViewersPerformance, 2) * Show.Weight * 0.5;
                            ErrorTotal += Math.Pow(BlendedThreshold - BlendedPerformance, 2) * Show.Weight;
                        }
                        else
                        {
                            if (RatingsPerformance < RatingsThreshold)
                                ErrorTotal += Math.Pow(RatingsThreshold - RatingsPerformance, 2) * Show.Weight * 0.5;
                            if (ViewersPerformance < ViewersThreshold)
                                ErrorTotal += Math.Pow(ViewersThreshold - ViewersPerformance, 2) * Show.Weight * 0.5;
                            if (BlendedPerformance < BlendedThreshold)
                                ErrorTotal += Math.Pow(BlendedThreshold - BlendedPerformance, 2) * Show.Weight;
                        }
                    }
                    else
                    {
                        if (RatingsPerformance < RatingsThreshold)
                            AccuracyTotal += 0.5 * Show.Weight;
                        else
                            ErrorTotal += Math.Pow(RatingsPerformance - RatingsThreshold, 2) * Show.Weight * 0.5;

                        if (ViewersPerformance < ViewersThreshold)
                            AccuracyTotal += 0.5 * Show.Weight;
                        else
                            ErrorTotal += Math.Pow(ViewersPerformance - ViewersThreshold, 2) * Show.Weight * 0.5;

                        if (BlendedPerformance < BlendedThreshold)
                            AccuracyTotal += 0.5 * Show.Weight;
                        else
                            ErrorTotal += Math.Pow(BlendedPerformance - BlendedThreshold, 2) * Show.Weight;
                    }
                }
            }

            Accuracy = AccuracyTotal / AccuracyWeight;
            Error = ErrorTotal / ErrorWeight;
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
        /// Mutate the PredictionModel
        /// </summary>
        public void MutateModel()
        {
            RatingsModel.MutateModel();
            RenewalModel.MutateModel();
        }     
    }    
}
