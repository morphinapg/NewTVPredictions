using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    public static class CurrentApp
    {
        public static TopLevel? TopLevel;

        static int? _currentYear;
        public static int CurrentYear
        {
            get
            {
                if (_currentYear is null)
                {
                    var season = CurrentTVSeason;
                    _currentYear = season;
                    return season;
                }
                    
                else
                    return _currentYear.Value;
            }
        }

        static int CurrentTVSeason
        {
            get
            {
                var now = DateTime.Now;
                if (now.Month < 9)
                    return now.Year - 1;
                else
                    return now.Year;
            }
        }
    }
}
