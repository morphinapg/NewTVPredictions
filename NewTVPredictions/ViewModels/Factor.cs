using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Factor : ViewModelBase                                         //A Factor is a true/false property of every show on a network, things that could potentially impact how many viewers a show needs to be renewed
    {
        string _text = "";
        public string Text                                                      //The Factor Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged(nameof(Text));
            }
        }

        ObservableCollection<Factor> Parent;                                    //The original network factor list

               

        public Factor(string text, ObservableCollection<Factor> parent)         //Create a new factor
        {
            Text = text;
            Parent = parent;
        }

        public Factor(Factor other)
        {
            Text = other.Text;
            Parent = other.Parent;
            IsEnabled = other.IsEnabled;
        }

        public CommandHandler Remove_Click => new CommandHandler(Remove);       //if the user clicks the X on the Add Network page, remove factor from the list
        void Remove()
        {
            Parent.Remove(this);
        }

        public static implicit operator string(Factor f) => f.Text;             //Automatically treat factor as a string

        bool _isEnabled;
        public bool IsEnabled                                                   //Turn the factor on or off for a particular show
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }
}
