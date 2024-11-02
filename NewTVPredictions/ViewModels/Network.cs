using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ColorTextBlock.Avalonia;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Concurrent;
using Avalonia.Media;

namespace NewTVPredictions.ViewModels
{
    /// <summary>
    /// A television network, including all of the shows for all years
    /// </summary>
    [DataContract (IsReference =true)]
    public class Network : ViewModelBase                                                                                            
    {
        /// <summary>
        /// The Name of the network
        /// </summary>
        [DataMember]
        string _name ="";
        public string Name                                                                                                          
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [DataMember]
        ObservableCollection<Factor> _factors = new();

        /// <summary>
        /// List of all of the shows on the network
        /// </summary>
        [DataMember]
        public ObservableCollection<Show> Shows = new();

        [DataMember]
        public double? RatingsDev, ViewersDev;

        public Evolution? Evolution;

        /// <summary>
        /// A factor is a true/false property of a show that can affect renewal
        /// </summary>
        public ObservableCollection<Factor> Factors => _factors;                                                                    

        /// <summary>
        /// Default constructor
        /// </summary>
        public Network()
        {
            Shows.CollectionChanged += Shows_CollectionChanged;
        }

        string _currentFactor = "";
        /// <summary>
        /// On the Add Network page, this is the current string typed into the "Add a Factor" textbox
        /// </summary>
        public string CurrentFactor                                                                                                 
        {
            get => _currentFactor;
            set
            {
                _currentFactor = value; 
                OnPropertyChanged(nameof(CurrentFactor));
            }
        }

        //Add CurrentFactor to the Factors collection
        public CommandHandler Add_Factor => new CommandHandler(AddFactor);                                                          

        void AddFactor()
        {
            if (!string.IsNullOrEmpty(CurrentFactor))
                Factors.Add(new Factor(CurrentFactor, Factors));

            CurrentFactor = "";

            FactorFocused?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? FactorFocused;

        //Display the network name. Useful when debugging.
        public override string ToString()                                                                                           
        {
            return Name;
        }                                                                                   

        ObservableCollection<Show> _filteredShows = new();
        /// <summary>
        /// This will display only the shows that exist as part of the current TV Season year
        /// </summary>
        public ObservableCollection<Show> FilteredShows                                                                             
        {
            get => _filteredShows;
            set
            {
                _filteredShows = value; 
                OnPropertyChanged(nameof(FilteredShows));
            }
        }

        ObservableCollection<Show> _alphabeticalShows = new();
        /// <summary>
        /// An alphabetical version of FilteredShows
        /// </summary>
        public ObservableCollection<Show> AlphabeticalShows                                                                         
        {
            get => _alphabeticalShows;
            set
            {
                _alphabeticalShows = value;
                OnPropertyChanged(nameof(AlphabeticalShows));
            }
        }

        //This will need to run when a show is added to the Shows collection, in order to update the FilteredShows collection
        private void Shows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)                                    
        {
            if (CurrentYear is not null)
                UpdateFilter();

            SubscribeToShows();

            Database_Modified?.Invoke(this, e);
        }

        Show _currentShow = new();
        /// <summary>
        /// The currently selected show. Used with AddShow and ModifyShow views
        /// </summary>
        public Show CurrentShow                                                                                                    
        {
            get => _currentShow;
            set
            {
                _currentShow = value;
                CurrentModifyShow = value;
                OnPropertyChanged(nameof(CurrentShow));
            }
        }

        public CommandHandler ResetShow_Clicked => new CommandHandler(ResetShow);

        /// <summary>
        /// After adding factors, make sure to reset them all to false
        /// </summary>
        public void ResetShow()                                                                                                     
        {
            CurrentShow = new();
            Parallel.ForEach(Factors, x => x.IsTrue = false);
            OnPropertyChanged(nameof(Factors));

            CurrentModifyShow = new();
        }

        Show? _currentModifyShow = null;
        /// <summary>
        /// Current show selected in the ModifyShows page
        /// </summary>
        public Show? CurrentModifyShow
        {
            get => _currentModifyShow;
            set
            {
                if (value is Show s)
                    _currentModifyShow = string.IsNullOrEmpty(s.Name) ? null : new Show(s);
                else
                    _currentModifyShow = null;
                
                OnPropertyChanged(nameof(CurrentModifyShow));
                OnPropertyChanged(nameof(ModifyEnabled));
            }
        }

        /// <summary>
        /// disable the ModifyShow panel if CurrentModifyShow is null
        /// </summary>
        public bool ModifyEnabled => CurrentModifyShow is not null;

        //Save the current ModifyShow changes, replacing the original show in the list
        public CommandHandler Save_Modify => new CommandHandler(SaveModifyShow);                                                    
        void SaveModifyShow()
        {
            if (CurrentModifyShow is not null)
            {
                var OriginalShow = Shows.Where(x => x.Name == CurrentModifyShow.Name && x.Season == CurrentModifyShow.Season && x.Year == CurrentModifyShow.Year).FirstOrDefault();

                if (OriginalShow is not null)
                {
                    Shows.Remove(OriginalShow);
                    Shows.Add(CurrentModifyShow);
                }

                Database_Modified?.Invoke(this, EventArgs.Empty);
            }

            ResetShow();
        }

        public CommandHandler AddShow_Clicked => new CommandHandler(Add_Show);

        //Add current show to Shows collection
        async void Add_Show()                                                                                                       
        {
            if (CurrentShow is not null)
            {
                if (string.IsNullOrEmpty(CurrentShow.Name))
                {
                    await MessageBoxManager.GetMessageBoxStandard("Incomplete data", "Please give the show a name!", ButtonEnum.Ok).ShowAsync();
                    return;
                }
                else if (Shows.Where(x => x.Name == CurrentShow.Name && x.Season == CurrentShow.Season && x.Year == CurrentYear).Any())
                {
                    await MessageBoxManager.GetMessageBoxStandard("Error", "This show already exists on the network!", ButtonEnum.Ok).ShowAsync();
                    return;
                }
                else if (CurrentShow.Season > 1 && CurrentShow.PreviousEpisodes == 0)
                {
                    await MessageBoxManager.GetMessageBoxStandard("Error", "Please double check the previously aired episodes!", ButtonEnum.Ok).ShowAsync();
                    return;
                }

                if (CurrentShow.Parent is null)
                    CurrentShow.Parent = this;

                CurrentShow.Factors = new ObservableCollection<Factor>(Factors.Select(x => new Factor(x)));

                CurrentShow.Year = CurrentYear;

                Shows.Add(CurrentShow);

                Database_Modified?.Invoke(this, EventArgs.Empty);
            }            

            ResetShow();
        }

        int? _currentYear;
        /// <summary>
        /// Update the FilteredShows when the current year changes
        /// </summary>
        public int? CurrentYear                                                                                                     
        {
            get => _currentYear;
            set
            {
                _currentYear = value;
                //UpdateFilter();
            }
        }

        //Update FilteredShows and AlphabeticalShows by the current year
        public async void UpdateFilter()                                                                                                   
        {
            var tmpShows = (CurrentYear is null ? Shows.AsParallel() : Shows.AsParallel().Where(x => x.Year == CurrentYear)).OrderByDescending(x => x.ActualOdds).ThenByDescending(x => x.CurrentPerformance).ThenBy(x => x.Name).ThenBy(x => x.Season);
            var alphabetical = tmpShows.OrderBy(x => x.Name).ThenBy(x => x.Season);

            await Dispatcher.UIThread.InvokeAsync( () =>
            {
                FilteredShows = new ObservableCollection<Show>(tmpShows);
                AlphabeticalShows = new ObservableCollection<Show>(alphabetical);
            });
        }

        Queue<Factor> SubscribedFactors = new();

        /// <summary>
        /// When searching for shows by factor, factor toggle events must be subscribed. This keeps track of them.
        /// </summary>
        public void SubscribeToFactors()                                                                                            
        {

            while (SubscribedFactors.Any())
            {
                var factor = SubscribedFactors.Dequeue();
                factor.Toggled -= Factor_Toggled;
            }

            foreach (var factor in Factors)
            {
                SubscribedFactors.Enqueue(factor);
                factor.Toggled += Factor_Toggled;
            }

            ShowsFilteredByFactor.Clear();
        }

        /// <summary>
        /// When factors are toggled, update ShowsFilteredByFactor
        /// </summary>
        public void Factor_Toggled(object? sender, EventArgs e)                                                                    
        {
            var SelectedFactors = Factors.Where(x => x.IsTrue);

            if (SelectedFactors.Any())
            {
                var shows = ShowAllYears ?
                Shows.Where(x => SelectedFactors.All(y => x.Factors.Contains(y))) :
                Shows.Where(x => x.Year == CurrentYear && SelectedFactors.All(y => x.Factors.Contains(y)));

                shows = ShowAllYears ? shows.OrderBy(x => x.Name).ThenBy(x => x.Year).ThenBy(x => x.Season) : shows.OrderBy(x => x.Name).ThenBy(x => x.Season);

                var ShowNames = ShowAllYears ?
                    shows.Select(x => x.Name + " (Season " + x.Season + ")") :
                    shows.Select(x => x.ToString());

                ShowsFilteredByFactor = new ObservableCollection<string>(ShowNames);
            }
            else if (ShowsFilteredByFactor.Any())
                ShowsFilteredByFactor?.Clear();            
        }

        ObservableCollection<string> _showsFilteredByFactor = new();
        /// <summary>
        /// List of shows to display when searching for shows by factor
        /// </summary>
        public ObservableCollection<string> ShowsFilteredByFactor                                                                   
        {
            get => _showsFilteredByFactor;
            set
            {
                _showsFilteredByFactor = value;
                OnPropertyChanged(nameof(ShowsFilteredByFactor));
            }
        }

        bool _showAllYears;
        /// <summary>
        /// Allows the user to display all years when searching for shows by factor
        /// </summary>
        public bool ShowAllYears                                                                                                    
        {
            get => _showAllYears;
            set
            {
                _showAllYears = value;
                OnPropertyChanged(nameof(ShowAllYears));

                Factor_Toggled(this, new EventArgs());
            }
        }

        /// <summary>
        /// Get all shows, weighted by year
        /// </summary>
        public IEnumerable<WeightedShow> GetWeightedShows()
        {
            var now = DateTime.Now;
            double NextYear = now.Month < 9 ? now.Year : now.Year + 1;
            return Shows.Where(x => x.Year.HasValue && x.Ratings.Count > 0 && x.Viewers.Count > 0 && x.Renewed || x.Canceled).Select(x =>
            {
                List<double>
                Ratings = x.Ratings.Select(x =>
                {
                    return x is null || x == 0 ?
                        Math.Log10(0.004) :
                        Math.Log10(x.Value);
                }).ToList(),

                Viewers = x.Viewers.Select(x =>
                {
                    return x is null || x == 0 ?
                        Math.Log10(0.0004) :
                        Math.Log10(x.Value);
                }).ToList();

                return new WeightedShow(x, 1 / (NextYear - x.Year!.Value), Ratings, Viewers);
            }).ToList();
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        /// <summary>
        /// Get all combinations of Current Episode and Total Episodes
        /// </summary>
        public IEnumerable<EpisodePair> GetEpisodePairs()
        {
            return Shows.Select(x => x.Episodes).Distinct().Select(Total => Enumerable.Range(1, Total).Select(Current => new EpisodePair(Current, Total))).SelectMany(x => x).ToList();
        }

        /// <summary>
        /// Get a dictionary describing the average rating (in Log10) per year
        /// calculated with a weighted average
        /// </summary>
        public Dictionary<int, double> GetAverageRatingPerYear(int InputType)
        {
            var AverageRatings = new Dictionary<int, double>();

            var Offsets = new List<ShowAvg>();

            double NextYear = CurrentApp.CurrentYear + 1;

            double sumWeights = 0;
            double sumX = 0;
            double sumY = 0;
            double sumXY = 0;
            double sumX2 = 0;


            foreach (var year in Shows.Select(x => x.Year).Distinct())
            {
                if (year is not null)
                {
                    double totalratings = 0;
                    int showcount = 0;

                    foreach (var x in Shows.Where(x => x.Year == year))
                    {
                        var Ratings = InputType == 0 ? x.Ratings : x.Viewers;
                        double weight = 0, total = 0, currentWeight = 0;
                        int count = 0;
                        showcount++;

                        foreach (var rating in Ratings)
                        {                           
                            count++;
                            weight += count * count;

                            var currentrating = rating is null || rating == 0 ?
                                (InputType == 0 ? 0.004 : 0.0004) :
                                rating.Value;

                            currentrating = Math.Log10(currentrating);

                            total += currentrating * count * count;
                        }

                        var showavg = total / weight;

                        currentWeight = 1 / (NextYear - year.Value) * x.CurrentEpisodes;

                        sumWeights += currentWeight;
                        sumX += year.Value * currentWeight;
                        sumY += showavg * currentWeight;
                        sumXY = year.Value * showavg * currentWeight;
                        sumX2 = year.Value * year.Value * currentWeight;

                        totalratings += showavg;

                        Offsets.Add(new ShowAvg(showavg, year.Value, 1 / (NextYear - year.Value)));
                    }

                    var avg = totalratings / showcount;

                    AverageRatings[year.Value] = avg;
                }                
            }            

            var Trend = new Dictionary<int, double>();
            double 
                slope = (sumWeights * sumXY - sumX * sumY) / (sumWeights * sumX2 - sumX * sumX),
                intercept = (sumY - slope * sumX) / sumWeights;

            foreach (var key in AverageRatings.Keys)
                Trend[key] = slope * key + intercept;

            if (!AverageRatings.Keys.Contains(CurrentApp.CurrentYear))
                Trend[CurrentApp.CurrentYear] = slope * CurrentApp.CurrentYear + intercept;

            var dev = Math.Sqrt(Offsets.Sum(x => Math.Pow(x.avg - Trend[x.year], 2) * x.weight) / Offsets.Sum(x => x.weight));

            if (InputType == 0)
                RatingsDev = dev;
            else
                ViewersDev = dev;

            return Trend;
        }

        /// <summary>
        /// Get an array describing the average offset of each episode compared to the average season rating
        /// </summary>
        public double[] GetEpisodeOffsets(Dictionary<int, double> AverageRatings, int InputType)
        {
            var MaxEpisodes = Shows.Max(x => x.Episodes);
            var EpisodeOffsets = new double[26];

            double weight, total, NextYear = CurrentApp.CurrentYear + 1, offset, currentweight;

            for (int i = 0; i < MaxEpisodes; i++)
            {
                weight = 0;
                total = 0;

                foreach (var x in Shows.Where(x => (InputType == 0 ? x.Ratings.Count : x.Viewers.Count) > i && x.Year is not null))
                {
                    var Ratings = InputType == 0 ? x.Ratings : x.Viewers;
                    var currentrating = Ratings[i] is null || Ratings[i] == 0 ?
                                (InputType == 0 ? 0.004 : 0.0004) :
                                Ratings[i]!.Value;
                    currentrating = Math.Log10(currentrating);

                    offset = currentrating - AverageRatings[x.Year!.Value];
                    currentweight = 1 / (NextYear - x.Year.Value);

                    weight += currentweight;
                    total += offset * currentweight;
                }

                EpisodeOffsets[i] = total / weight;
            }

            return EpisodeOffsets;
        }

        /// <summary>
        /// This will
        /// </summary>
        /// <param name="AverageRatings">A dictionary of Average ratings per year (Log10)</param>
        /// <param name="EpisodeOffsets">An array of average offset per episode compared to season average</param>
        /// <param name="InputType">0 = Ratings, 1 = Viewers</param>
        /// <returns></returns>
        public double[] GetDeviations(Dictionary<int, double> AverageRatings, double[] EpisodeOffsets, int InputType)
        {
            var Deviations = new double[26];

            for (int i = 0; i < 26; i++)
            {
                var matchedshows = Shows.Where(x => (InputType == 0 ? x.Ratings.Count : x.Viewers.Count) > i && x.Year.HasValue && AverageRatings.ContainsKey(x.Year.Value));

                if (matchedshows.Any())
                {
                    Deviations[i] = Math.Sqrt(matchedshows.Select(x =>
                    {
                        var Ratings = InputType == 0 ? x.Ratings : x.Viewers;

                        var currentrating = Ratings[i] is null || Ratings[i] == 0 ?
                                    (InputType == 0 ? 0.004 : 0.0004) :
                                    Ratings[i]!.Value;

                        currentrating = Math.Log10(currentrating);

                        return Math.Pow(currentrating - AverageRatings[x.Year!.Value], 2);
                    }).Average());
                }
                else
                    Deviations[i] = 1;
            }

            return Deviations;
        }

        /// <summary>
        /// Projects an episode's rating, given a list of current episodes
        /// </summary>
        /// <param name="Episodes">Current episodes</param>
        /// <param name="ProjectedEpisode">The calculated value</param>
        /// <returns></returns>
        public double GetProjectedRating(List<double> Episodes, int ProjectedEpisode)
        {
            double sumWeights = 0, sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

            double RunningTotal = 0, CurrentAverage = 0;

            for (int i = 0; i < Episodes.Count; i++)
            {
                int episode = i + 1;
                double weight = episode * episode;

                RunningTotal += weight * Episodes[i];
                sumWeights += weight;

                CurrentAverage = RunningTotal / sumWeights;
                
                sumX += episode * weight;
                sumY += CurrentAverage * weight;
                sumXY += episode * CurrentAverage * weight;
                sumX2 += episode * episode * weight;
            }

            
            double 
                slope = (sumWeights * sumXY - sumX * sumY) / (sumWeights * sumX2 - sumX * sumX),
                intercept = (sumY - slope * sumX) / sumWeights;

            if (Episodes.Count == 2)
            {
                slope = 0;
                intercept = sumY / sumWeights;
            }

            double projectedRating = slope * ProjectedEpisode + intercept;

            return projectedRating;
        }


        public event EventHandler? Database_Modified;


        HashSet<Show> SubscribedShows = new();
        public void SubscribeToShows()
        {
            if (SubscribedShows is null)
                SubscribedShows = new();

            foreach (var show in Shows.Except(SubscribedShows))
            {
                show.RatingsChanged += Show_RatingsChanged;
                SubscribedShows.Add(show);
            }
             
            foreach (var show in SubscribedShows.Except(Shows))
            {
                show.RatingsChanged -= Show_RatingsChanged;
                SubscribedShows.Remove(show);
            }                
        }

        private void Show_RatingsChanged(object? sender, EventArgs e)
        {
            Database_Modified?.Invoke(this, EventArgs.Empty);
        }

        public Network (Network other)
        {
            Name = other.Name;
            foreach (var factor in other.Factors)
                Factors.Add(new Factor(factor));

            var tempShows = other.Shows.Select(x => new Show(x)).ToList();

            Shows = new ObservableCollection<Show>(tempShows);
            RatingsDev = other.RatingsDev;
            ViewersDev = other.ViewersDev;
        }

        public CommandHandler Delete_Show => new CommandHandler(DeleteShow);

        async void DeleteShow()
        {
            var msg = MessageBoxManager.GetMessageBoxStandard("Are you sure?", "Are you sure you want to delete the current show? This cannot be undone!", ButtonEnum.YesNo);

            var result = await msg.ShowAsync();

            if (result == ButtonResult.Yes)
            {
                if (CurrentModifyShow is not null)
                {
                    var OriginalShow = Shows.Where(x => x.Name == CurrentModifyShow.Name && x.Season == CurrentModifyShow.Season && x.Year == CurrentModifyShow.Year).FirstOrDefault();

                    if (OriginalShow is not null)
                        Shows.Remove(OriginalShow);

                    Database_Modified?.Invoke(this, EventArgs.Empty);
                }

                ResetShow();
            }
        }
    }
}
