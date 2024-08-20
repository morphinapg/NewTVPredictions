using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;

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

        private void Shows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)                                    //This will need to run when a show is added to the Shows collection, in order to update the FilteredShows collection
        {
            
        }

        Show _currentShow = new();
        public Show CurrentShow                                                                                                     //The currently selected show. Used with AddShow and ModifyShow views
        {
            get => _currentShow;
            set
            {
                _currentShow = value;
                OnPropertyChanged(nameof(CurrentShow));
            }
        }

        public void ResetShow()                                                                                                         //After adding factors, make sure to reset them all to false
        {
            CurrentShow = new();
            Parallel.ForEach(Factors, x => x.IsEnabled = false);
            OnPropertyChanged(nameof(Factors));
        }

        public CommandHandler AddShow_Clicked => new CommandHandler(Add_Show);                                                      

        void Add_Show()                                                                                                             //Add current show to Shows collection
        {
            if (CurrentShow.Parent is null)
                CurrentShow.Parent = this;

            CurrentShow.Factors = new ObservableCollection<Factor>(Factors.Select(x => new Factor(x)));

            Shows.Add(CurrentShow);

            ResetShow();
        }
    }
}
