using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.Reflection.Metadata.Ecma335;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Show : ViewModelBase
    {
        [DataMember]
        Network? _parent;
        public Network? Parent                                              //Reference to the parent Network, should be set when creating AddShow view
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
        public string Name                                                  //Name of the show
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
        public int Season                                                   //Which season the show is in for this year
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
        public int PreviousEpisodes                                         //How many episodes of the show aired before the current season
        {                                                                   //Useful for the model to determine syndication status
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
        public int Episodes                                                 //How many episodes the current season will air (or likely air if unknown)
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
        public ObservableCollection<Factor> Factors                         //The list of factors this show has. 
        {                                                                   //Will need to be updated if the Parent network factors change.
            get => _factors;
            set
            {
                _factors = value;
                OnPropertyChanged(nameof(Factors));
            }
        }

        [DataMember]
        bool _halfHour;
        public bool HalfHour                                                //If a show is 30 minutes long
        {
            get => _halfHour;
            set
            {
                _halfHour = value;
                OnPropertyChanged(nameof(HalfHour));
                OnPropertyChanged(nameof(HourLong));
            }
        }
        public bool HourLong
        {
            get => !HalfHour;
            set => HalfHour = !value;
        }

        [DataMember]
        int? _year;
        public int? Year                                                    //The year this TV season aired
        {
            get => _year;
            set
            {
                _year = value;
                OnPropertyChanged(nameof(Year));
            }
        }

        [DataMember]
        List<decimal?>
            Ratings = new(),
            Viewers = new();

        List<RatingsInfo> _ratingsContainer = new();
        public  List<RatingsInfo> RatingsContainer => _ratingsContainer;

        public Show()                                                       //Initialize RatingsInfo with every new Show
        {
            RatingsContainer.Add(new RatingsInfo(Ratings, "Ratings"));
            RatingsContainer.Add(new RatingsInfo(Viewers, "Viewers"));
        }

        public override string ToString()                                   //ToString should display the Show name
        {
            return Name;
        }

        public Show(Show other)                                             //Create a clone of another show
        {
            Parent = other.Parent;
            Name = other.Name;
            Season = other.Season;
            PreviousEpisodes = other.PreviousEpisodes;
            Episodes = other.Episodes;
            foreach (var item in other.Factors)
                Factors.Add(new Factor(item));
            HalfHour = other.HalfHour;
            Year = other.Year;
            Canceled = other.Canceled;
            Renewed = other.Renewed;
            if (other._renewalStatus is not null)
                RenewalStatus = other.RenewalStatus;

            RatingsContainer.Add(new RatingsInfo(Ratings, "Ratings"));
            RatingsContainer.Add(new RatingsInfo(Viewers, "Viewers"));
        }

        bool _canceled;
        public bool Canceled                                                //Sets show as being cenceled
        {
            get => _canceled;
            set
            {
                _canceled = value;
                OnPropertyChanged(nameof(Canceled));
                OnPropertyChanged(nameof(RenewalStatus));
            }
        }

        bool _renewed;
        public bool Renewed                                                 //sets show as being renewed
        {
            get => _renewed;
            set
            {
                _renewed = value;
                OnPropertyChanged(nameof(Renewed));
                OnPropertyChanged(nameof(RenewalStatus));
            }
        }

        //if both Renewed and Canceled are selected, then the show has been renewed for the final season

        string? _renewalStatus;
        public string? RenewalStatus                                         //Either default or custom renewal status string
        {
            get => _renewalStatus is null ? DefaultString : _renewalStatus;
            set
            {
                _renewalStatus = value;
                OnPropertyChanged(nameof(RenewalStatus));
            }
        }

        string DefaultString
        {
            get
            {
                if (Renewed && Canceled)
                    return "Renewed for the Final Season";
                else if (Renewed)
                    return "Renewed";
                else if (Canceled)
                    return "Canceled";
                else
                    return "";
            }
        }
    }
}
