using System.Collections.Generic;
using System.ComponentModel;

namespace TV_Ratings_Predictions
{
    public class RatingsContainer
    {
        List<double> Ratings = new();

        public string ShowName { get; } = "";

    }
}
