using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using MathNet.Numerics.Distributions;
using System.Collections.Concurrent;
using Markdown.Avalonia.Plugins;
using System.Transactions;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Predictable : ViewModelBase, IComparable<Predictable>
    {
        //[DataMember]
        //public double? MarginOfError, RatingMargin, ViewerMargin;

        [DataMember]
        public ConcurrentDictionary<EpisodePair, double>
            MarginOfError = new(),
            RatingMargin = new(),
            ViewerMargin = new();

        [DataMember]
        double? _error;                                     //Representation of how many incorrect predictions there were, and by how much
        public double? Error                                //Some additional error values may be added as well, to optimize the RatingsModel
        {
            get => _error;
            set
            {
                _error = value;
                OnPropertyChanged(nameof(Error));
            }
        }

        [DataMember]
        double? _accuracy;
        public double? Accuracy                              //Represents what % of shows the model predicts correctly
        {
            get => _accuracy;
            set
            {
                _accuracy = value;
                OnPropertyChanged(nameof(Accuracy));
            }
        }

        [DataMember]
        public bool Duplicate = false;

        //A reference to the parent network
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public Network Network;

        [DataMember]
        public string NetworkName;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.


        /// <summary>
        /// Calculate the Margin of Error for these prediction results
        /// </summary>
        public void CalculateMarginOfError(IEnumerable<ShowErrorContainer> PredictionResults, IEnumerable<EpisodePair> EpisodePairs, bool BlendedOnly = false)
        {
            var MarginActions = GetMarginActions(PredictionResults, EpisodePairs, BlendedOnly);

            Parallel.ForEach(MarginActions, x => x());
        }

        /// <summary>
        /// Get all actions needed to calculate the margins of error
        /// </summary>
        public IEnumerable<Action> GetMarginActions(IEnumerable<ShowErrorContainer> PredictionResults, IEnumerable<EpisodePair> EpisodePairs, bool BlendedOnly)
        {
            return EpisodePairs.Select<EpisodePair, Action>(x => () => CalculateMargins(PredictionResults, x, BlendedOnly));
        }

        public void CalculateMargins(IEnumerable<ShowErrorContainer> PredictionResults, EpisodePair Episodes, bool BlendedOnly)
        {
            double
                Lowest = Math.Max((Episodes.Current - 1.0) / Episodes.Total, 0),
                Highest = Math.Min((Episodes.Current + 1.0) / Episodes.Total, 1);

            //First, we find the Margin Of Error for blended predictions
            var Incorrect = PredictionResults.Where(x => !x.PredictionCorrect && x.CurrentPosition > Lowest && x.CurrentPosition < Highest).OrderBy(x => x.BlendedDistance).Select(x => new StatsContainer(x.BlendedDistance, x.Weight)).ToList();
            var Correct = PredictionResults.Where(x => x.PredictionCorrect && x.CurrentPosition > Lowest && x.CurrentPosition < Highest).OrderBy(x => x.BlendedDistance).Select(x => new StatsContainer(x.BlendedDistance, x.Weight)).ToList();

            var margin = GetMargin(Incorrect, Correct);
            MarginOfError[Episodes] = margin != 0 ? margin : 100;

            if (!BlendedOnly)
            {
                //Next, find the margin of error for ratings predictions
                Incorrect = PredictionResults.Where(x => !x.RatingCorrect && x.CurrentPosition > Lowest && x.CurrentPosition < Highest).OrderBy(x => x.RatingDistance).Select(x => new StatsContainer(x.RatingDistance, x.Weight)).ToList();
                Correct = PredictionResults.Where(x => x.RatingCorrect && x.CurrentPosition > Lowest && x.CurrentPosition < Highest).OrderBy(x => x.RatingDistance).Select(x => new StatsContainer(x.RatingDistance, x.Weight)).ToList();

                margin = GetMargin(Incorrect, Correct);
                    RatingMargin[Episodes] = margin != 0 ? margin : 100;

                //Finally, find the margin of error for viewers predictions
                Incorrect = PredictionResults.Where(x => !x.ViewerCorrect && x.CurrentPosition > Lowest && x.CurrentPosition < Highest).OrderBy(x => x.ViewerDistance).Select(x => new StatsContainer(x.ViewerDistance, x.Weight)).ToList();
                Correct = PredictionResults.Where(x => x.ViewerCorrect && x.CurrentPosition > Lowest && x.CurrentPosition < Highest).OrderBy(x => x.ViewerDistance).Select(x => new StatsContainer(x.ViewerDistance, x.Weight)).ToList();

                margin = GetMargin(Incorrect, Correct);
                    ViewerMargin[Episodes] = margin != 0 ? margin : 100;
            }            
        }

        /// <summary>
        /// Calculate a margin of error, by comparing Incorrect and Correct predictions
        /// </summary>
        /// <param name="Incorrect">The distance between the threshold and performance for incorrect predictions</param>
        /// <param name="Correct">The distance between the threshold and performance for correct predictions</param>
        /// <returns></returns>
        double GetMargin(List<StatsContainer> Incorrect, List<StatsContainer> Correct)
        {
            double
                IncorrectTotal = Incorrect.Sum(x => x.Weight),
                CorrectTotal = Correct.Sum(x => x.Weight),
                IncorrectGoal = 0.682689492137086 * IncorrectTotal,
                CorrectGoal = 0.317310507862914 * CorrectTotal,
                RunningTotal = 0, percentage = 0, IncorrectMargin = 0, CorrectMargin = 0;

            int? match = null;

            if (Incorrect.Any())
            {
                for (int i = 0; i < Incorrect.Count - 1 && RunningTotal < IncorrectGoal; i++)
                {
                    var OldTotal = RunningTotal;

                    RunningTotal += Incorrect[i].Weight;
                    if (RunningTotal > IncorrectGoal)
                    {
                        match = i;
                        percentage = (IncorrectGoal - OldTotal) / (RunningTotal - OldTotal);
                    }
                }

                if (match is null)
                {
                    match = Incorrect.Count - 1;
                    percentage = 0;
                }

                var LowMatch = Incorrect[match.Value];
                var HighMatch = match < Incorrect.Count - 1 ? Incorrect[match.Value + 1] : Incorrect[match.Value];

                IncorrectMargin = (LowMatch.Value * (1 - percentage)) + (HighMatch.Value * percentage);

                if (CorrectTotal == 0)
                    return IncorrectMargin;
            }

            if (Correct.Any())
            {
                match = null;
                percentage = 0;

                for (int i = 0; i < Correct.Count - 1 && RunningTotal < CorrectGoal; i++)
                {
                    var OldTotal = RunningTotal;

                    RunningTotal += Correct[i].Weight;
                    if (RunningTotal > CorrectGoal)
                    {
                        match = i;
                        percentage = (CorrectGoal - OldTotal) / (RunningTotal - OldTotal);
                    }
                }

                if (match is null)
                {
                    match = Correct.Count - 1;
                    percentage = 0;
                }

                var LowMatch = Correct[match.Value];
                var HighMatch = match < Correct.Count - 1 ? Correct[match.Value + 1] : Correct[match.Value];

                CorrectMargin = (LowMatch.Value * (1 - percentage)) + (HighMatch.Value * percentage);

                if (IncorrectTotal == 0)
                    return CorrectMargin;

            }


            return (IncorrectMargin * IncorrectTotal + CorrectMargin * CorrectTotal) / (IncorrectTotal + CorrectTotal);
        }

        /// <summary>
        /// Given a series of actions, set the model's Accuracy and Error
        /// </summary>
        /// <param name="RatingResults">Actions needed to calculate error on the RatingsModel</param>
        /// <param name="PredictionResults">Actions needed to calculate error on the PredictionModel</param>
        public void SetAccuracyAndError(IEnumerable<ErrorContainer> RatingsResults, IEnumerable<ShowErrorContainer> PredictionResults, IEnumerable<EpisodePair> EpisodePairs)
        {
            var PredictionWeights = PredictionResults.Sum(x => x.Weight);
            var PredictionTotals = PredictionResults.Select(x => x.PredictionCorrect ? x.Weight : 0).Sum();

            Accuracy = PredictionTotals / PredictionWeights;

            PredictionTotals = PredictionResults.Sum(x => x.Error * x.Weight);

            //Now we can get the total error
            var RatingsWeights = RatingsResults.Sum(x => x.Weight);
            var RatingsTotals = RatingsResults.Sum(x => x.Error * x.Weight);

            var AllWeights = PredictionWeights + RatingsWeights;
            var AllTotals = PredictionTotals + RatingsTotals;

            Error = AllTotals / AllWeights;                
        }

        /// <summary>
        /// Get the renewal odds of a show, given the Performance and Threshold.
        /// MarginOfError needs to have been calculated already
        /// </summary>
        /// <param name="outputs">Output of GetPerformanceAndThreshold</param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(double ShowPerformance, double ShowThreshold, EpisodePair Episodes)
        {
            var Normal = new Normal(ShowThreshold, MarginOfError[Episodes]);

            return Normal.CumulativeDistribution(ShowPerformance);
        }

        public override int GetHashCode() => new {Network.Name, Accuracy, Error, Duplicate}.GetHashCode();

        public static bool operator ==(Predictable x, Predictable y) => x.Equals(y);

        public static bool operator !=(Predictable x, Predictable y) => !x.Equals(y);

        public static bool operator <(Predictable x, Predictable y)
        {
            if (x is not null)
            {
                if (y is null)
                    return false;

                if (y.Error.HasValue && double.IsNaN(y.Error.Value))
                    return false;

                if (x.Duplicate == y.Duplicate)
                    return x.Accuracy < y.Accuracy || (x.Accuracy == y.Accuracy && x.Error > y.Error);
                else if (x.Duplicate && !y.Duplicate)
                    return false;
                else
                    return true;
            }   
            else
                return false;
        }

        public static bool operator >(Predictable x, Predictable y)
        {
            if (x is not null)
            {
                if (y is null)
                    return true;

                if (y.Error.HasValue && double.IsNaN(y.Error.Value))
                    return true;

                if (x.Duplicate == y.Duplicate)
                    return x.Accuracy > y.Accuracy || (x.Accuracy == y.Accuracy && x.Error < y.Error);
                else if (x.Duplicate && !y.Duplicate)
                    return true;
                else
                    return false;
            }     
            else
                return false;
        }

        /// <summary>
        /// Provides the ability to sort Prediction models by most accurate first, then by lowest error
        /// </summary>
        /// <param name="other">PredictionModel to compare to</param>
        public int CompareTo(Predictable? other)
        {
            if (other is null) return 1;

            if (other.Error.HasValue && double.IsNaN(other.Error.Value)) return 1;

            if (other.Duplicate == Duplicate)
            {
                if (other.Accuracy == Accuracy)
                {
                    if (other is null || other.Error is null)
                        return 1;

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
            else if (other.Duplicate && !Duplicate)
                return -1;
            else
                return 1;
        }

        /// <summary>
        /// Check for equality between this PredictionModel and another object
        /// </summary>
        /// <param name="obj">Object to compare</param>
        /// <returns>true or false, whether the objects are equal</returns>
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;

            if (obj is Predictable model && model.Network.Name == Network.Name && model.Accuracy == Accuracy && model.Error == Error && model.Duplicate == Duplicate)
                return true;
            else
                return false;
        }

        public double[] TestAccuracy(PredictionModel Model, PredictionStats Stats, IEnumerable<WeightedShow> WeightedShows, bool CalculateMargin = false, int CurrentEpisode = 0, int TotalEpisodes = 26)
        {
            ConcurrentDictionary<(Show, int), double>
                RatingsProjections = Stats.RatingsProjections,
                ViewerProjections = Stats.ViewerProjections;

            Dictionary<int, double>
                RatingsAverages = Stats.RatingsAverages,
                ViewerAverages = Stats.ViewerAverages;

            double[]
                RatingsOffsets = Stats.RatingsOffsets,
                ViewerOffsets = Stats.ViewerOffsets;

            var AllEpisodeCounts = WeightedShows.Select(x => x.Show.Episodes).Distinct().ToList();

            double
                WeightTotal = 0,
                ErrorTotal = 0,
                AccuracyTotal = 0;

            double[] outputs;
            double RatingsPerformance, ViewersPerformance, RatingsThreshold, ViewersThreshold, Blend, BlendedPerformance, BlendedThreshold;//, RatingsMax, RatingsMin, ViewersMin, ViewersMax, RatingsRange, ViewersRange, RatingsError, ViewersError, RatingsAvg, ViewersAvg;
            double RatingsProjection, ViewersProjection, ExpectedRatings, ExpectedViewers, RatingAvgTotal = 0, RatingDevTotal = 0, ViewerAvgTotal = 0, ViewerDevTotal = 0, StatWeights = 0, difference = 0;
            int Episode;
            List<double> Ratings, Viewers, ShowRatings, ShowViewers;
            (Show, int) key;

            double
                Lowest = CalculateMargin ? (CurrentEpisode - 1.0) / TotalEpisodes : 0,
                Highest = CalculateMargin ? (CurrentEpisode + 1.0) / TotalEpisodes : 2;

            List<StatsContainer>
                Correct = new(),
                Incorrect = new();

            foreach (var Show in WeightedShows)
            {
                ShowRatings = Show.Ratings;
                ShowViewers = Show.Viewers;

                ExpectedRatings = Network.GetProjectedRating(RatingsOffsets.Take(Show.Show.Episodes).ToList(), Show.Show.Episodes);
                ExpectedViewers = Network.GetProjectedRating(ViewerOffsets.Take(Show.Show.Episodes).ToList(), Show.Show.Episodes);

                //for (int i = 0; i < Show.Show.CurrentEpisodes; i++)
                foreach (var i in Enumerable.Range(1, Show.Show.CurrentEpisodes).Where(x => (double)x / Show.Show.Episodes > Lowest && (double)x / Show.Show.Episodes < Highest).Select(x => x-1))
                {
                    WeightTotal += Show.Weight * 2;

                    var year = Show.Show.Year.HasValue ? Show.Show.Year.Value : CurrentApp.CurrentYear;

                    Episode = i + 1;
                    Ratings = ShowRatings.Take(Episode).Select((x, i) => x - RatingsAverages[year] - RatingsOffsets[i]).ToList();
                    Viewers = ShowViewers.Take(Episode).Select((x, i) => x - ViewerAverages[year] - ViewerOffsets[i]).ToList();

                    //Get output data
                    outputs = Model.GetOutputs(Show.Show, Episode, Ratings, Viewers);

                    //check if current show has been projected before                   

                    key = (Show.Show, i);

                    if (RatingsProjections.ContainsKey(key))
                        RatingsProjection = RatingsProjections[key];
                    else
                    {
                        RatingsProjection = Ratings.Count > 1 ? Network.GetProjectedRating(Ratings, Show.Show.Episodes) : Ratings[0] + (ExpectedRatings - RatingsOffsets[0]);
                        RatingsProjections[key] = RatingsProjection;
                    }

                    if (ViewerProjections.ContainsKey(key))
                        ViewersProjection = ViewerProjections[key];
                    else
                    {
                        ViewersProjection = Viewers.Count > 1 ? Network.GetProjectedRating(Viewers, Show.Show.Episodes) : Viewers[0] + (ExpectedViewers - ViewerOffsets[0]);
                        ViewerProjections[key] = ViewersProjection;
                    }

                    RatingsPerformance = RatingsProjection + outputs[0];
                    ViewersPerformance = ViewersProjection + outputs[1];

                    RatingAvgTotal += RatingsPerformance * Show.Weight;
                    ViewerAvgTotal += ViewersPerformance * Show.Weight;
                    RatingDevTotal += Math.Pow(RatingsPerformance - RatingsAverages[year], 2) * Show.Weight;
                    ViewerDevTotal += Math.Pow(ViewersPerformance - ViewerAverages[year], 2) * Show.Weight;
                    StatWeights += Show.Weight;


                    RatingsThreshold = outputs[2];
                    ViewersThreshold = outputs[3];

                    Blend = outputs[4];
                    BlendedPerformance = RatingsPerformance * Blend + ViewersPerformance * (1 - Blend);
                    BlendedThreshold = RatingsThreshold * Blend + ViewersThreshold * (1 - Blend);



                    //Test Accuracy
                    difference = Math.Abs(BlendedPerformance - BlendedThreshold);

                    if (CalculateMargin)
                    {
                        if (Show.Show.Renewed && BlendedPerformance > BlendedThreshold || Show.Show.Canceled && BlendedPerformance < BlendedThreshold)
                            Correct.Add(new StatsContainer(difference, Show.Weight));
                        else
                            Incorrect.Add(new StatsContainer(difference, Show.Weight));
                    }

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
                            AccuracyTotal += Show.Weight;
                        else
                            ErrorTotal += Math.Pow(BlendedPerformance - BlendedThreshold, 2) * Show.Weight;
                    }
                }
            }

            if (CalculateMargin)
            {
                return new double[1] { GetMargin(Incorrect, Correct) };
            }
            else
            {
                var returns = new double[6];

                //Accuracy
                returns[0] = AccuracyTotal / WeightTotal;
                //Error
                returns[1] = ErrorTotal / WeightTotal;

                //RatingsAvg
                returns[2] = RatingAvgTotal / StatWeights;
                //RatingsDev
                returns[3] = Math.Sqrt(RatingDevTotal / StatWeights);

                //ViewersAvg
                returns[4] = ViewerAvgTotal / StatWeights;
                //ViewersDev
                returns[5] = Math.Sqrt(ViewerDevTotal / StatWeights);

                return returns;
            }            
        }
    }   
}
