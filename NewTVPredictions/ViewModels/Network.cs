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
    internal class Network : ViewModelBase
    {
        [DataMember]
        string _name ="";
        public string Name                                                  //The Name of the Network
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        [DataMember]
        public ObservableCollection<string> Factors = new();                        //A factor is a true/false property of a show that can affect renewal

        public Network()
        {

        }

        public Network (Network n)
        {
            _name = n.Name;

            Factors.Clear();
            foreach (string factor in n.Factors)
                Factors.Add (factor);
        }

        public void AddFactor(string factor)
        {
            Factors.Add(factor);
        }

        public void RemoveFactor(string factor)
        {
            Factors.Remove(factor);
        }
    }
}
