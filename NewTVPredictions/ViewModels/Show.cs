using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Show : ViewModelBase
    {
        [DataMember]
        Network? _parent;
        public Network? Parent                          //Reference to the parent Network, should be set when creating AddShow view
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
        public string Name                              //Name of the show
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
}
