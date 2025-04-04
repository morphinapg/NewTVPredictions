﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using MathNet.Numerics.Distributions;
using System.Collections.Concurrent;
using System.Transactions;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Predictable : ViewModelBase, IComparable<Predictable>
    {
        

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

        /// <summary>
        /// Test the accuracy of a model, optionally filtered by a specific CurrentEpisode/TotalEpisodes range for MarginOfError calculation
        /// </summary>
        /// <param name="Model">The PredictionModel to test</param>
        /// <param name="Stats">Statistics needed during accuracy testing</param>
        /// <param name="WeightedShows">A list of all shows marked as renewed or canceled from a network, with their associated weights</param>
        /// <param name="ReturnMode">Determines which output mode to use. 0 = normal Accuracy/Error, 1 = Margin of Error, 2 = Statistics Calculations</param>
        /// <param name="CurrentEpisode">Represents how many episodes into a TV show that is being tested</param>
        /// <param name="TotalEpisodes">Represents total number of episodes a TV show will have for the season</param>
        /// <returns></returns>
        public double[] TestAccuracy(PredictionModel Model, PredictionStats Stats, IEnumerable<WeightedShow> WeightedShows, int ReturnMode = 0, int CurrentEpisode = 0, int TotalEpisodes = 26)
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
                Lowest = ReturnMode == 1 ? (CurrentEpisode - 1.0) / TotalEpisodes : 0,
                Highest = ReturnMode == 1 ? (CurrentEpisode + 1.0) / TotalEpisodes : 2;

            List<StatsContainer>
                Correct = new(),
                Incorrect = new(),
                RatingsValues = new(),
                ViewersValues = new();

            double
                WeightedRatings = 0, WeightedViewers = 0;

            foreach (var Show in WeightedShows)
            {
                ShowRatings = Show.Ratings;
                ShowViewers = Show.Viewers;

                ExpectedRatings = Network.GetProjectedRating(RatingsOffsets.Take(Show.Show.Episodes).ToList());
                ExpectedViewers = Network.GetProjectedRating(ViewerOffsets.Take(Show.Show.Episodes).ToList());

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
                        RatingsProjection = Ratings.Count > 1 ? Network.GetProjectedRating(Ratings) : Ratings[0] + (ExpectedRatings - RatingsOffsets[0]);
                        RatingsProjections[key] = RatingsProjection;
                    }

                    if (ViewerProjections.ContainsKey(key))
                        ViewersProjection = ViewerProjections[key];
                    else
                    {
                        ViewersProjection = Viewers.Count > 1 ? Network.GetProjectedRating(Viewers) : Viewers[0] + (ExpectedViewers - ViewerOffsets[0]);
                        ViewerProjections[key] = ViewersProjection;
                    }

                    RatingsPerformance = RatingsProjection + outputs[0];
                    ViewersPerformance = ViewersProjection + outputs[1];

                    //RatingsPerformance = outputs[0];
                    //ViewersPerformance = outputs[1];

                    if (ReturnMode == 2)
                    {
                        RatingsValues.Add(new StatsContainer(RatingsPerformance, Show.Weight));
                        ViewersValues.Add(new StatsContainer(ViewersPerformance, Show.Weight));
                        WeightedRatings += RatingsPerformance * Show.Weight;
                        WeightedViewers += ViewersPerformance * Show.Weight;
                        StatWeights += Show.Weight;
                    }   

                    RatingsThreshold = outputs[2];
                    ViewersThreshold = outputs[3];

                    Blend = outputs[4];
                    BlendedPerformance = RatingsPerformance * Blend + ViewersPerformance * (1 - Blend);
                    BlendedThreshold = RatingsThreshold * Blend + ViewersThreshold * (1 - Blend);



                    //Test Accuracy
                    difference = Math.Abs(BlendedPerformance - BlendedThreshold);

                    if (ReturnMode == 1)
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

            if (ReturnMode == 0)
            {
                var returns = new double[2];

                //Accuracy
                returns[0] = AccuracyTotal / WeightTotal;
                //Error
                returns[1] = ErrorTotal / WeightTotal;

                return returns;
            }
            else if (ReturnMode == 1)
            {
                return new double[]{ GetMargin(Incorrect, Correct)};
            }
            else 
            {
                var returns = new double[4];

                returns[0] = WeightedRatings / StatWeights; // RatingsAvg
                returns[1] = Math.Sqrt(RatingsValues.Sum(x => Math.Pow(x.Value - returns[0], 2) * x.Weight) / StatWeights); //RatingsDev
                returns[2] = WeightedViewers / StatWeights; //ViewersAvg
                returns[3] = Math.Sqrt(ViewersValues.Sum(x => Math.Pow(x.Value - returns[2], 2) * x.Weight) / StatWeights); //ViewersDev

                return returns;
            }            
        }
    }   
}
