using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    public class RatingsInfo : ViewModelBase
    {
        List<double?> Ratings;

        string _header;
        public string Header
        {
            get => _header;
            set
            {
                _header = value;
                OnPropertyChanged(nameof(Header));
            }
        }

        public RatingsInfo(List<double?> ratings, string header)
        {
            Ratings = ratings;
            _header = header;
        }

        double? GetRating(int i)
        {
            if (Ratings.Count > i)
                return Ratings[i];
            else
                return null;
        }

        void SetRating(int i, double? value)
        {
            if (i > Ratings.Count - 1)
                Ratings.Add(value);
            else
                Ratings[i] = value;

            if (i > Ratings.Count - 1 || value is null)
            {
                if (Ratings.Contains(null) || Ratings.Contains(0))
                    Ratings = Ratings.Where(x => x is not null && x != 0).ToList();

                var max = Math.Min(Ratings.Count+1, 26);

                for (int j = 0; j < max; j++)
                    OnPropertyChanged("Episode" + (j + 1));
            }
            else
                OnPropertyChanged("Episode" + (i + 1));
        }

        public double? Episode1 { get => GetRating(0); set => SetRating(0, value); }
        public double? Episode2 { get => GetRating(1); set => SetRating(1, value); }
        public double? Episode3 { get => GetRating(2); set => SetRating(2, value); }
        public double? Episode4 { get => GetRating(3); set => SetRating(3, value); }
        public double? Episode5 { get => GetRating(4); set => SetRating(4, value); }
        public double? Episode6 { get => GetRating(5); set => SetRating(5, value); }
        public double? Episode7 { get => GetRating(6); set => SetRating(6, value); }
        public double? Episode8 { get => GetRating(7); set => SetRating(7, value); }
        public double? Episode9 { get => GetRating(8); set => SetRating(8, value); }
        public double? Episode10 { get => GetRating(9); set => SetRating(9, value); }
        public double? Episode11 { get => GetRating(10); set => SetRating(10, value); }
        public double? Episode12 { get => GetRating(11); set => SetRating(11, value); }
        public double? Episode13 { get => GetRating(12); set => SetRating(12, value); }
        public double? Episode14 { get => GetRating(13); set => SetRating(13, value); }
        public double? Episode15 { get => GetRating(14); set => SetRating(14, value); }
        public double? Episode16 { get => GetRating(15); set => SetRating(15, value); }
        public double? Episode17 { get => GetRating(16); set => SetRating(16, value); }
        public double? Episode18 { get => GetRating(17); set => SetRating(17, value); }
        public double? Episode19 { get => GetRating(18); set => SetRating(18, value); }
        public double? Episode20 { get => GetRating(19); set => SetRating(19, value); }
        public double? Episode21 { get => GetRating(20); set => SetRating(20, value); }
        public double? Episode22 { get => GetRating(21); set => SetRating(21, value); }
        public double? Episode23 { get => GetRating(22); set => SetRating(22, value); }

        public double? Episode24 { get => GetRating(23); set => SetRating(23, value); }
        public double? Episode25 { get => GetRating(24); set => SetRating(24, value); }
        public double? Episode26 { get => GetRating(25); set => SetRating(25, value); }

    }
}
