using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    public record StatsContainer(double Value, double Weight);
    public record WeightedShow(Show Show, double Weight);
    public record ErrorContainer(Predictable Model, double Error, double Weight);
    public record ShowErrorContainer(Predictable Model, bool PredictionCorrect, double Error, double Weight, double CurrentPosition, bool RatingCorrect, bool ViewerCorrect, double RatingDistance, double ViewerDistance, double BlendedDistance);
    public record EpisodePair(int Current, int Total);
}
