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
    [DataContract (IsReference =true)]
    public class Network : ViewModelBase                                                                                            //A television network, including all of the shows for all years
    {
        [DataMember]
        string _name ="";
        public string Name                                                                                                          //The Name of the Network
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

        public ObservableCollection<Factor> Factors => _factors;                                                                    //A factor is a true/false property of a show that can affect renewal

        public Network()
        {
            Shows.CollectionChanged += Shows_CollectionChanged;
        }        

        public Network (Network n)
        {
            _name = n.Name;

            Factors.Clear();
            foreach (Factor factor in n.Factors)
                Factors.Add(factor);
        }

        string _currentFactor = "";
        public string CurrentFactor                                                                                                 //On the Add Network page, this is the current string typed into the "Add a Factor" textbox
        {
            get => _currentFactor;
            set
            {
                _currentFactor = value; 
                OnPropertyChanged(nameof(CurrentFactor));
            }
        }

        public CommandHandler Add_Factor => new CommandHandler(AddFactor);                                                          //Add CurrentFactor to the Factors collection

        void AddFactor()
        {
            if (!string.IsNullOrEmpty(CurrentFactor))
                Factors.Add(new Factor(CurrentFactor, Factors));

            CurrentFactor = "";

            FactorFocused?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? FactorFocused;

        public override string ToString()                                                                                           //Display the network name. Useful when debugging.
        {
            return Name;
        }

        [DataMember]
        public ObservableCollection<Show> Shows = new();                                                                            //List of all of the shows on the network

        ObservableCollection<Show> _filteredShows = new();
        public ObservableCollection<Show> FilteredShows                                                                             //This will display only the shows that exist as part of the current TV Season year
        {
            get => _filteredShows;
            set
            {
                _filteredShows = value; 
                OnPropertyChanged(nameof(FilteredShows));
            }
        }

        ObservableCollection<Show> _alphabeticalShows = new();
        public ObservableCollection<Show> AlphabeticalShows                                                                         //An alphabetical version of FilteredShows
        {
            get => _alphabeticalShows;
            set
            {
                _alphabeticalShows = value;
                OnPropertyChanged(nameof(AlphabeticalShows));
            }
        }

        private void Shows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)                                    //This will need to run when a show is added to the Shows collection, in order to update the FilteredShows collection
        {
            UpdateFilter();   
        }

        Show _currentShow = new();
        public Show CurrentShow                                                                                                    //The currently selected show. Used with AddShow and ModifyShow views
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

        public void ResetShow()                                                                                                     //After adding factors, make sure to reset them all to false
        {
            CurrentShow = new();
            Parallel.ForEach(Factors, x => x.IsTrue = false);
            OnPropertyChanged(nameof(Factors));

            CurrentModifyShow = new();
        }

        Show? _currentModifyShow = null;
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

        public bool ModifyEnabled => CurrentModifyShow is not null;                                                                 //disable the ModifyShow panel if CurrentModifyShow is null


        public CommandHandler Save_Modify => new CommandHandler(SaveModifyShow);                                                    //Save the current ModifyShow changes, replacing the original show in the list
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

        async void Add_Show()                                                                                                       //Add current show to Shows collection
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
        public int? CurrentYear                                                                                                     //Update the FilteredShows when the current year changes
        {
            get => _currentYear;
            set
            {
                _currentYear = value;
                UpdateFilter();
            }
        }

        async void UpdateFilter()                                                                                                   //Update FilteredShows and AlphabeticalShows by the current year
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

        public void SubscribeToFactors()                                                                                            //When searching for shows by factor, factor toggle events must be subscribed. This keeps track of them.
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

        public void Factor_Toggled(object? sender, EventArgs e)                                                                    //When factors are toggled, update ShowsFilteredByFactor
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
        public ObservableCollection<string> ShowsFilteredByFactor                                                                   //List of shows to display when searching for shows by factor
        {
            get => _showsFilteredByFactor;
            set
            {
                _showsFilteredByFactor = value;
                OnPropertyChanged(nameof(ShowsFilteredByFactor));
            }
        }

        bool _showAllYears;
        public bool ShowAllYears                                                                                                    //Allows the user to display all years when searching for shows by factor
        {
            get => _showAllYears;
            set
            {
                _showAllYears = value;
                OnPropertyChanged(nameof(ShowAllYears));

                Factor_Toggled(this, new EventArgs());
            }
        }


    }
}
