using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

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
            var now = DateTime.Now;
            double NextYear = now.Month < 9 ? now.Year : now.Year + 1;
            var WeightedShows = Shows.Where(x => x.Year.HasValue).Select(x => new {Show = x, Weight = 1 / (NextYear - x.Year!.Value)});

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
        /// <returns>Output 0: Show performance, Output 1: Renewal Threshold</returns>
        public double[] GetOutputs(Show Show)
        {
            if (Show.Year is null)
                throw new Exception("The Show is missing a Year value!");

            //First, we need to calculate the Show's current performance in both ratings and viewers
            var NumberOfInputs = Show.Ratings.Count + 1;

            var inputs = new double[NumberOfInputs];

            inputs[0] = Show.Episodes;
            for (int i = 0; i < Show.Ratings.Count; i++)
                inputs[i + 1] = Convert.ToDouble(Show.Ratings[i]);

            var output = RatingsModel.GetOutput(inputs);
            var RatingsScore = output[0];

            NumberOfInputs = Show.Viewers.Count + 1;
            inputs = new double[NumberOfInputs];
            inputs[0] = Show.Episodes;
            for (int i = 0; i < Show.Viewers.Count; i++)
                inputs[i + 1] = Convert.ToDouble(Show.Viewers[i]);

            output = RatingsModel.GetOutput(inputs, 1);
            var ViewersScore = output[0];

            //Next, we need to calculate renewal threshold and blend level
            NumberOfInputs = Show.Factors.Count + 5;
            inputs = new double[NumberOfInputs];

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

            output = RenewalModel.GetOutput(inputs);
            var RatingThreshold = output[0];
            var ViewerThreshold = output[1];
            var Blend = 1 / (1 + Math.Exp(-1 * output[2]));

            var ShowPerformance = RatingsScore * Blend + ViewersScore * (1 - Blend);
            var RenewalThreshold = RatingThreshold * Blend + ViewerThreshold * (1 - Blend);

            return new double[] { ShowPerformance, RenewalThreshold };
        }
    }    
}
