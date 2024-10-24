using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace TV_Ratings_Predictions
{
    [Serializable]
    public class Show //Contains all of the information necessary to describe an entire season of a show
    {
        string _name = "";
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }
        public ObservableCollection<bool> factorValues = new();

        public int year, PreviousEpisodes;
        public List<double> ratings = new(), viewers = new();
        public string RenewalStatus ="";
        public bool Renewed, Canceled;
        public double OldRating, OldOdds, FinalPrediction, OldViewers;

        private int _episodes;
        public int Episodes
        {
            get { return _episodes; }
            set
            {
                _episodes = value;
            }
        }

        private bool _halfhour;
        public bool Halfhour
        {
            get { return _halfhour; }
            set
            {
                _halfhour = value;
            }
        }

        private int _season;
        public int Season
        {
            get { return _season; }
            set
            {
                _season = value;
            }
        }
    }
}
