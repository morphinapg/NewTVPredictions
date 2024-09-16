﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Predictable : ViewModelBase
    {
        [DataMember]
        public double? MarginOfError, RatingMargin, ViewerMargin;

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

        /// <summary>
        /// Calculate the Margin of Error for these prediction results
        /// </summary>
        public void CalculateMarginOfError(IEnumerable<ShowErrorContainer> PredictionResults)
        {
            //First, we find the Margin Of Error for blended predictions
            var Incorrect = PredictionResults.Where(x => x.PredictionCorrect == 0).OrderBy(x => x.BlendedDistance).Select(x => new StatsContainer(x.BlendedDistance, x.Weight)).ToList();
            var Correct = PredictionResults.Where(x => x.PredictionCorrect == 1).OrderBy(x => x.BlendedDistance).Select(x => new StatsContainer(x.BlendedDistance, x.Weight)).ToList();

            MarginOfError = GetMargin(Incorrect, Correct);

            //Next, find the margin of error for ratings predictions
            Incorrect = PredictionResults.Where(x => !x.RatingCorrect).OrderBy(x => x.RatingDistance).Select(x => new StatsContainer(x.RatingDistance, x.Weight)).ToList();
            Correct = PredictionResults.Where(x => x.RatingCorrect).OrderBy(x => x.RatingDistance).Select(x => new StatsContainer(x.RatingDistance, x.Weight)).ToList();

            RatingMargin = GetMargin(Incorrect, Correct);

            //Finally, find the margin of error for viewers predictions
            Incorrect = PredictionResults.Where(x => !x.ViewerCorrect).OrderBy(x => x.ViewerDistance).Select(x => new StatsContainer(x.ViewerDistance, x.Weight)).ToList();
            Correct = PredictionResults.Where(x => x.ViewerCorrect).OrderBy(x => x.ViewerDistance).Select(x => new StatsContainer(x.ViewerDistance, x.Weight)).ToList();

            ViewerMargin = GetMargin(Incorrect, Correct);
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
        /// <param name="RatingsActions">Actions needed to calculate error on the RatingsModel</param>
        /// <param name="PredictionActions">Actions needed to calculate error on the PredictionModel</param>
        public void SetAccuracyAndError(IEnumerable<Func<ErrorContainer>> RatingsActions, IEnumerable<Func<ShowErrorContainer>> PredictionActions)
        {
            var PredictionResults = PredictionActions.AsParallel().Select(x => x());
            var PredictionWeights = PredictionResults.Sum(x => x.Weight);
            var PredictionTotals = PredictionResults.Select(x => x.Error == 0 ? x.Weight : 0).Sum();

            Accuracy = PredictionTotals / PredictionWeights;

            PredictionTotals = PredictionResults.Sum(x => x.Error * x.Weight);

            //Now we can get the total error
            var RatingsResults = RatingsActions.AsParallel().Select(x => x());
            var RatingsWeights = RatingsResults.Sum(x => x.Weight);
            var RatingsTotals = RatingsResults.Sum(x => x.Error * x.Weight);

            var AllWeights = PredictionWeights + RatingsWeights;
            var AllTotals = PredictionTotals + RatingsTotals;

            Error = AllTotals / AllWeights;

            CalculateMarginOfError(PredictionResults);
        }
    }   
}
