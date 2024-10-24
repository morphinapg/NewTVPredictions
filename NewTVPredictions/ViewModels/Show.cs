using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.Reflection.Metadata.Ecma335;
using System.Collections;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using Avalonia.Media;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Show : ViewModelBase
    {
        [DataMember]
        Network? _parent;
        /// <summary>
        /// Reference to the parent Network, should be set when creating AddShow view
        /// </summary>
        public Network? Parent
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
        /// <summary>
        /// Name of the show
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string NameWithSeason => Parent is not null && Parent.Shows.Where(x => x.Name == Name && x.Year == Year).Count() > 1 ? Name + " (Season " + Season + ")" : Name;

        [DataMember]
        int _season = 1;
        /// <summary>
        /// Which season the show is in for this year
        /// </summary>
        public int Season
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
        /// <summary>
        /// How many episodes of the show aired before the current season
        /// Useful for the model to determine syndication status
        /// </summary>
        public int PreviousEpisodes
        {
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
        /// <summary>
        /// How many episodes the current season will air (or likely air if unknown)
        /// </summary>
        public int Episodes
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
        /// <summary>
        /// The list of factors this show has. 
        /// Will need to be updated if the Parent network factors change.
        /// </summary>
        public ObservableCollection<Factor> Factors
        {
            get => _factors;
            set
            {
                _factors = value;
                OnPropertyChanged(nameof(Factors));
            }
        }

        [DataMember]
        bool _halfHour;
        /// <summary>
        /// If a show is 30 minutes long
        /// </summary>
        public bool HalfHour
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
        /// <summary>
        /// The year this TV season aired
        /// </summary>
        public int? Year
        {
            get => _year;
            set
            {
                _year = value;
                OnPropertyChanged(nameof(Year));
            }
        }

        /// <summary>
        /// Ratings/viewers for each episode that has currently aired
        /// </summary>
        [DataMember]
        public List<double?>
            Ratings = new(),
            Viewers = new();

        List<RatingsInfo> _ratingsContainer = new();
        public List<RatingsInfo> RatingsContainer => _ratingsContainer;

        /// <summary>
        /// Initialize RatingsInfo with every new Show
        /// </summary>
        public Show()
        {
            RatingsContainer.Add(new RatingsInfo(Ratings, "Ratings"));
            RatingsContainer.Add(new RatingsInfo(Viewers, "Viewers"));
        }

        [DataMember]
        double? _currentRating, _currentViewers, _currentPerformance, _currentOdds, _targetRating, _targetViewers;

        [DataMember]
        public double OldRating, OldViewers, OldPerformance, OldOdds;

        [DataMember]
        public double? FinalPrediction;

        /// <summary>
        /// The Projected Rating for the entire season
        /// based on existing ratings
        /// </summary>
        public double? CurrentRating
        {
            get => _currentRating;
            set
            {
                _currentRating = value;
                OnPropertyChanged(nameof(CurrentRating));
            }
        }

        /// <summary>
        /// The projected number of viewers for the entire season
        /// based on existing viewers
        /// </summary>
        public double? CurrentViewers
        {
            get => _currentViewers;
            set
            {
                _currentViewers = value;
                OnPropertyChanged(nameof(CurrentViewers));
                OnPropertyChanged(nameof(CurrentViewersString));
            }
        }

        /// <summary>
        /// The projected number of viewers for the entire season
        /// based on existing viewers
        /// Formatted to describe them as millions (M) or thousands (K) of viewers
        /// </summary>
        public string? CurrentViewersString
        {
            get
            {
                if (CurrentViewers is null)
                    return null;
                else if (CurrentViewers >= 1)
                    return CurrentViewers.Value.ToString("N2") + "M";
                else
                    return Math.Round(CurrentViewers.Value, 3) * 1000 + "K";
            }
        }

        /// <summary>
        /// Represents the current performance rating of the show, relative to the renewal threshold.
        /// 50 = ratings/viewers are half of what they need to be for renewal.
        /// 100 = right on the renewal threshold.
        /// 200 = ratings/viewers are twice what they need to be for renewal.
        /// </summary>
        public double? CurrentPerformance
        {
            get => _currentPerformance;
            set
            {
                _currentPerformance = value;
                OnPropertyChanged(nameof(CurrentPerformance));
            }
        }

        /// <summary>
        /// The predicted odds of renewal. Value between 0-100%.
        /// 50% means right on the renewal threshold.
        /// </summary>
        public double? CurrentOdds
        {
            get => string.IsNullOrWhiteSpace(RenewalStatus) ? _currentOdds : null;
            set
            {
                _currentOdds = value;
                OnPropertyChanged(nameof(CurrentOdds));
                OnPropertyChanged(nameof(PredictionStatus));
            }
        }

        public double? ActualOdds => _currentOdds;

        /// <summary>
        /// The ideal rating value for the season
        /// </summary>
        public double? TargetRating
        {
            get => _targetRating;
            set
            {
                _targetRating = value;
                OnPropertyChanged(nameof(TargetRating));
            }
        }

        /// <summary>
        /// The ideal number of viewers for the season (millions)
        /// </summary>
        public double? TargetViewers
        {
            get => _targetViewers;
            set
            {
                _targetViewers = value;
                OnPropertyChanged(nameof(TargetViewers));
                OnPropertyChanged(nameof(TargetViewersString));
            }
        }

        /// <summary>
        /// Represets TargetViewers as a string formatted by Millions (M) or thousands (K) of viewers
        /// </summary>
        public string? TargetViewersString
        {
            get
            {
                if (TargetViewers is null)
                    return null;
                else if (TargetViewers >= 1)
                    return Math.Round(TargetViewers.Value, 2) + "M";
                else
                    return Math.Round(TargetViewers.Value, 3) * 1000 + "K";
            }
        }

        /// <summary>
        /// A text string representing either the current renewal status of the show,
        /// or the prediction category
        /// </summary>
        public string? PredictionStatus
        {
            get
            {
                if (string.IsNullOrEmpty(RenewalStatus) && CurrentOdds is not null)
                {
                    if (CurrentOdds < 0.2)
                        return "Certain Cancellation";
                    else if (CurrentOdds < 0.4)
                        return "Likely Cancellation";
                    else if (CurrentOdds < 0.5)
                        return "Leaning Towards Cancellation";
                    else if (CurrentOdds < 0.6)
                        return "Leaning Towards Renewal";
                    else if (CurrentOdds < 0.8)
                        return "Likely Renewal";
                    else
                        return "Certain Renewal";
                }
                else
                    return RenewalStatus;
            }
        }

        /// <summary>
        /// Color to be used with PredictionStatus in the UI
        /// </summary>
        public IBrush RenewalColor
        {
            get
            {
                if (Renewed)
                    return Brushes.Green;
                else if (Canceled)
                    return Brushes.Red;
                else if (string.IsNullOrEmpty(RenewalStatus))
                    return Brushes.White;
                else
                    return Brushes.Gray;
            }
        }

        public double? RatingChange => CurrentRating - OldRating;
        public double? ViewerChange
        {
            get
            {
                var change = CurrentViewers - OldViewers;

                if (CurrentViewers < 1)
                {
                    change *= 1000;
                    if (change.HasValue)
                        change = Math.Round(change.Value, 0);
                }
                    

                return change;
            }
        }



        public double? PerformanceChange => CurrentPerformance - OldPerformance;
        public double? OddsChange => CurrentOdds - OldOdds;


        /// <summary>
        /// ToString should display the Show name
        /// </summary>
        public override string ToString()                                   
        {
            if (Parent is not null && Parent.Shows.Where(x => x.Name == Name && x.Year == Year).Count() > 1)
                return Name + " (Season " + Season + ")";

            return Name;
        }

        /// <summary>
        /// Create a clone of another show
        /// </summary>
        public Show(Show other)                                             
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
            CurrentRating = other.CurrentRating;
            CurrentViewers = other.CurrentViewers;
            OldRating = other.OldRating;
            OldViewers = other.OldViewers;
            CurrentOdds = other.CurrentOdds;
            OldOdds = other.OldOdds;
            CurrentPerformance = other.CurrentPerformance;
            OldPerformance = other.OldPerformance;
            TargetRating = other.TargetRating;
            TargetViewers = other.TargetViewers;
            FinalPrediction = other.FinalPrediction;

            if (other._renewalStatus is not null)
                RenewalStatus = other.RenewalStatus;

            foreach (var rating in other.Ratings)
                Ratings.Add(rating);

            foreach (var viewer in other.Viewers)
                Viewers.Add(viewer);

            RatingsContainer.Add(new RatingsInfo(Ratings, "Ratings"));
            RatingsContainer.Add(new RatingsInfo(Viewers, "Viewers"));

            if (this != other)
            {
                MessageBoxManager.GetMessageBoxStandard("Error", "Please update the copy constructor to support '" + MissingMember + "'", ButtonEnum.Ok).ShowAsync();
            }
        }

        [DataMember]
        bool _canceled;
        /// <summary>
        /// Sets show as being cenceled
        /// </summary>
        public bool Canceled                                                
        {
            get => _canceled;
            set
            {
                _canceled = value;
                OnPropertyChanged(nameof(Canceled));
                OnPropertyChanged(nameof(RenewalStatus));
            }
        }

        [DataMember]
        bool _renewed;
        /// <summary>
        /// sets show as being renewed
        /// </summary>
        public bool Renewed                                                 
        {
            get => _renewed;
            set
            {
                _renewed = value;
                OnPropertyChanged(nameof(Renewed));
                OnPropertyChanged(nameof(RenewalStatus));
            }
        }


        [DataMember]
        string? _renewalStatus;
        /// <summary>
        /// Either default or custom renewal status string
        /// </summary>
        public string? RenewalStatus                                         
        {
            get => _renewalStatus is null ? DefaultString : _renewalStatus;
            set
            {
                if (value == DefaultString)
                    _renewalStatus = null;
                else
                    _renewalStatus = value;
                OnPropertyChanged(nameof(RenewalStatus));
            }
        }

        /// <summary>
        /// The default RenewalStatus if one has not been entered yet.
        /// if both Renewed and Canceled are selected, then the show has been renewed for the final season
        /// </summary>
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

        string? MissingMember;

        /// <summary>
        /// Equivalency check, mainly used to verify the copy constructor handles every property
        /// </summary>
        /// <param name="x">First Show</param>
        /// <param name="y">Second Show</param>
        public static bool operator ==(Show x, Show y)                      
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            if (ReferenceEquals(x, y)) return true;

            // Combine properties and fields into a single collection of MemberInfo
            var members = x.GetType().GetProperties().Where(x => x.GetSetMethod() is not null)
                           .Cast<MemberInfo>()
                           .Concat(x.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

            foreach (var member in members)
            {
                if (member.Name != "RatingsContainer" && member.Name != "_ratingsContainer" && member.Name != "MissingProperty")
                {
                    var value1 = member is PropertyInfo prop1 ? prop1.GetValue(x) : ((FieldInfo)member).GetValue(x);
                    var value2 = member is PropertyInfo prop2 ? prop2.GetValue(y) : ((FieldInfo)member).GetValue(y);
                    var type = member is PropertyInfo prop3 ? prop3.PropertyType : ((FieldInfo)member).FieldType;

                    if (type.IsValueType) // Check for value type
                    {
                        if (!Equals(value1, value2))
                        {
                            x.MissingMember = member.Name;
                            return false;
                        }
                    }
                    else // reference type
                    {
                        if (!ReferenceEquals(value1, value2))
                        {
                            var enumerable1 = value1 as IEnumerable;
                            var enumerable2 = value2 as IEnumerable;

                            if (enumerable1 is not null && enumerable2 is not null)
                            {
                                if (!enumerable1.Cast<object>().SequenceEqual(enumerable2.Cast<object>()))
                                {
                                    x.MissingMember = member.Name;
                                    return false;
                                }
                            }
                            else if (value1 is not null && value2 is not null && !value1.Equals(value2))
                            {
                                x.MissingMember = member.Name;
                                MessageBoxManager.GetMessageBoxStandard("Error", "'" + member.Name + "' requires custom code to check equivalency.", ButtonEnum.Ok).ShowAsync();
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        public static bool operator !=(Show x, Show y) => !(x == y);

        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return new { Name, Season, Year}.GetHashCode();
        }

        /// <summary>
        /// The current number of episodes that have aired
        /// </summary>
        public int CurrentEpisodes => Math.Max(Ratings.Count, Viewers.Count);
    }
}
