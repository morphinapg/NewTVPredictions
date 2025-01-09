using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public record StatsContainer(double Value, double Weight);
    [DataContract]
    public record WeightedShow(Show Show, double Weight, List<double> Ratings, List<double> Viewers);
    [DataContract]
    public record ErrorContainer(Predictable Model, double Error, double Weight);
    [DataContract]
    public record ShowErrorContainer(Predictable Model, bool PredictionCorrect, double Error, double Weight, double CurrentPosition, bool RatingCorrect, bool ViewerCorrect, double RatingDistance, double ViewerDistance, double BlendedDistance);

    [DataContract]
    public record EpisodePair(int Current, int Total);

    [DataContract]
    public record PredictionContainer(double CurrentRating, double CurrentViewers, double CurrentPerformance, double TargetRating, double TargetViewers, double CurrentOdds, double ProjectedRating, double ProjectedViewers, double? OldRatings, double? OldViewers);

    [DataContract]
    public record PredictionStats(
        ConcurrentDictionary<(Show, int), double> RatingsProjections,
        ConcurrentDictionary<(Show, int), double> ViewerProjections,
        Dictionary<int, double> RatingsAverages,
        Dictionary<int, double> ViewerAverages,
        double[] RatingsOffsets,
        double[] ViewerOffsets
        );

    [DataContract]
    public record ShowAvg(double avg, int year, double weight);
}
