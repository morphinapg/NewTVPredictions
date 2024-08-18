using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract (IsReference =true)]
    public class Network : ViewModelBase                                        //A television network, including all of the shows for all years
    {
        [DataMember]
        string _name ="";
        public string Name                                                      //The Name of the Network
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

        public ObservableCollection<Factor> Factors => _factors;                //A factor is a true/false property of a show that can affect renewal

        public Network()
        {

        }

        public Network (Network n)
        {
            _name = n.Name;

            Factors.Clear();
            foreach (Factor factor in n.Factors)
                Factors.Add(factor);
        }

        string _currentFactor = "";
        public string CurrentFactor                                             //On the Add Network page, this is the current string typed into the "Add a Factor" textbox
        {
            get => _currentFactor;
            set
            {
                _currentFactor = value; 
                OnPropertyChanged(nameof(CurrentFactor));
            }
        }

        public CommandHandler Add_Factor => new CommandHandler(AddFactor);      //Add CurrentFactor to the Factors collection

        void AddFactor()
        {
            if (!string.IsNullOrEmpty(CurrentFactor))
                Factors.Add(new Factor(CurrentFactor, Factors));

            CurrentFactor = "";

            FactorFocused?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? FactorFocused;

        public override string ToString()                                       //Display the network name. Useful when debugging.
        {
            return Name;
        }
    }
}
