using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace TV_Ratings_Predictions 
{
    [Serializable]
    public class Network
    {
        public string name = "";
        public ObservableCollection<string> factors = new();                    //A factor is anything that might affect the renewability of a TV show. Described with true/false.

        public List<Show> shows = new();                                                          

    }    

}
