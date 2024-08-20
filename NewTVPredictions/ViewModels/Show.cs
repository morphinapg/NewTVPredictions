using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Show : ViewModelBase
    {
        [DataMember]
        Network? _parent;
        public Network? Parent                                  //Reference to the parent Network, should be set when creating AddShow view
        {
            get => _parent;
            set
            {
                _parent = value;
                OnPropertyChanged(nameof(Parent));
            }
        }

        [DataMember]
        string _name = "";
        public string Name                                      //Name of the show
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [DataMember]
        int _season = 1;
        public int Season                                       //Which season the show is in for this year
        {
            get => _season;
            set
            {
                _season = value;
                OnPropertyChanged(nameof(Season));
                OnPropertyChanged(nameof(PreviousEnabled));
            }
        }

        [DataMember]
        int _previousEpisodes;
        public int PreviousEpisodes                             //How many episodes of the show aired before the current season
        {                                                       //Useful for the model to determine syndication status
            get => _previousEpisodes;
            set
            {
                _previousEpisodes = value;
                OnPropertyChanged(nameof(PreviousEpisodes));
            }
        }

        public bool PreviousEnabled => Season > 1;

        [DataMember]
        int _episodes = 13;
        public int Episodes                                     //How many episodes the current season will air (or likely air if unknown)
        {
            get => _episodes;
            set
            {
                _episodes = value;
                OnPropertyChanged(nameof(Episodes));
            }
        }

        [DataMember]
        ObservableCollection<Factor> _factors = new();
        public ObservableCollection<Factor> Factors                             //The list of factors this show has. 
        {                                                       //Will need to be updated if the Parent network factors change.
            get => _factors;
            set
            {
                _factors = value;
                OnPropertyChanged(nameof(Factors));
            }
        }

        [DataMember]
        bool _halfHour;
        public bool HalfHour                                    //If a show is 30 minutes long
        {
            get => _halfHour;
            set
            {
                _halfHour = value;
                OnPropertyChanged(nameof(HalfHour));
                OnPropertyChanged(nameof(HourLong));
            }
        }
        public bool HourLong => !HalfHour;
    }
}
