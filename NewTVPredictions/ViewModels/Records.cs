using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    public record StatsContainer(double Value, double Weight);
    public record WeightedShow(Show Show, double Weight, List<double> Ratings, List<double> Viewers);
    public record ErrorContainer(Predictable Model, double Error, double Weight);
    public record ShowErrorContainer(Predictable Model, bool PredictionCorrect, double Error, double Weight, double CurrentPosition, bool RatingCorrect, bool ViewerCorrect, double RatingDistance, double ViewerDistance, double BlendedDistance);
    public record EpisodePair(int Current, int Total);

    public record PredictionContainer(double CurrentRating, double CurrentViewers, double CurrentPerformance, double TargetRating, double TargetViewers, double CurrentOdds);

    public record PredictionStats(
        ConcurrentDictionary<(Show, int), double> RatingsProjections,
        ConcurrentDictionary<(Show, int), double> ViewerProjections,
        Dictionary<int, double> RatingsAverages,
        Dictionary<int, double> ViewerAverages,
        double[] RatingsOffsets,
        double[] ViewerOffsets
        );

    public record ShowAvg(double avg, int year, double weight);
}
