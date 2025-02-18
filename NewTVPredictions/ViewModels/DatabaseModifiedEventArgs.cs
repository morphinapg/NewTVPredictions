using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    public class DatabaseModifiedEventArgs : EventArgs
    {
        public bool MissingEpisodes = false;
    }
}
