using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    public record StatsContainer(double Value, double Weight);
    public record WeightedShow(Show Show, double Weight);
    public record ErrorContainer(PredictionModel Model, double Error, double Weight);
    public record ShowErrorContainer(PredictionModel Model, double PredictionCorrect, double Error, double Weight, bool RatingCorrect, bool ViewerCorrect, double RatingDistance, double ViewerDistance, double BlendedDistance);
}
