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

        ObservableCollection<Factor> Parent;

        public CommandHandler Remove_Click => new CommandHandler(Remove);

        public Factor(string text, ObservableCollection<Factor> parent)
        {
            Text = text;
            Parent = parent;
        }

        void Remove()
        {
            Parent.Remove(this);
        }

        public static implicit operator string(Factor f) => f.Text;
    }
}
