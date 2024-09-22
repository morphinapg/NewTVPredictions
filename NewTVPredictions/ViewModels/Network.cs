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
            UpdateFilter();   
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
                UpdateFilter();
            }
        }

        //Update FilteredShows and AlphabeticalShows by the current year
        async void UpdateFilter()                                                                                                   
        {
            var tmpShows = CurrentYear is null ? Shows.AsParallel() : Shows.AsParallel().Where(x => x.Year == CurrentYear);
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
            return Shows.Where(x => x.Year.HasValue).Select(x => new WeightedShow(x, 1 / (NextYear - x.Year!.Value))).ToList();
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
            return Shows.Select(x => x.Episodes).Distinct().Select(Total => Enumerable.Range(1, Total).Select(Current => new EpisodePair(Current, Total))).SelectMany(x => x);
        }

        
    }
}
