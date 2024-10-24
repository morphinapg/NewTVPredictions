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
        [DataMember]
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
            IsTrue = other.IsTrue;
        }

        public CommandHandler Remove_Click => new CommandHandler(Remove);       //if the user clicks the X on the Add Network page, remove factor from the list
        void Remove()
        {
            Parent.Remove(this);
        }

        public static implicit operator string(Factor f) => f.Text;             //Automatically treat factor as a string

        [DataMember]
        bool _isTrue;
        public bool IsTrue                                                      //Turn the factor on or off for a particular show
        {
            get => _isTrue;
            set
            {
                _isTrue = value;
                OnPropertyChanged(nameof(IsTrue));

                Toggled?.Invoke(this, EventArgs.Empty);
            }
        }

        public override string ToString()                                       //Display the Factor State in ToString
        {
            return Text + " = " + IsTrue;
        }

        public override bool Equals(object? obj)                                //Factor Equivalence should be determined based on Name of factor, and Value
        {
            if (obj is null) return false;
            if (obj is Factor f)
            {
                if (f.Text == Text && f.IsTrue ==  IsTrue)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return new {Text, IsTrue}.GetHashCode();
        }

        public event EventHandler? Toggled;
        public void Toggle()                                                    //Toggle the IsTrue property
        {
            IsTrue = !IsTrue;
        }

    }
}
