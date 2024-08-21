using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    public class RatingsInfo : ViewModelBase
    {
        List<decimal?> Ratings;

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

        public RatingsInfo(List<decimal?> ratings, string header)
        {
            Ratings = ratings;
            _header = header;
        }

        decimal? GetRating(int i)
        {
            if (Ratings.Count > i)
                return Ratings[i];
            else
                return null;
        }

        void SetRating(int i, decimal? value)
        {
            if (i > Ratings.Count - 1)
                Ratings.Add(value);
            else
                Ratings[i] = value;

            if (i > Ratings.Count - 1)
            {
                if (Ratings.Contains(null))
                    Ratings = Ratings.Where(x => x is not null).ToList();

                for (int j = 0; j < 26; j++)
                    OnPropertyChanged("Episode" + (j + 1));
            }
            else
                OnPropertyChanged("Episode" + (i + 1));
        }

        public decimal? Episode1 { get => GetRating(0); set => SetRating(0, value); }
        public decimal? Episode2 { get => GetRating(1); set => SetRating(1, value); }
        public decimal? Episode3 { get => GetRating(2); set => SetRating(2, value); }
        public decimal? Episode4 { get => GetRating(3); set => SetRating(3, value); }
        public decimal? Episode5 { get => GetRating(4); set => SetRating(4, value); }
        public decimal? Episode6 { get => GetRating(5); set => SetRating(5, value); }
        public decimal? Episode7 { get => GetRating(6); set => SetRating(6, value); }
        public decimal? Episode8 { get => GetRating(7); set => SetRating(7, value); }
        public decimal? Episode9 { get => GetRating(8); set => SetRating(8, value); }
        public decimal? Episode10 { get => GetRating(9); set => SetRating(9, value); }
        public decimal? Episode11 { get => GetRating(10); set => SetRating(10, value); }
        public decimal? Episode12 { get => GetRating(11); set => SetRating(11, value); }
        public decimal? Episode13 { get => GetRating(12); set => SetRating(12, value); }
        public decimal? Episode14 { get => GetRating(13); set => SetRating(13, value); }
        public decimal? Episode15 { get => GetRating(14); set => SetRating(14, value); }
        public decimal? Episode16 { get => GetRating(15); set => SetRating(15, value); }
        public decimal? Episode17 { get => GetRating(16); set => SetRating(16, value); }
        public decimal? Episode18 { get => GetRating(17); set => SetRating(17, value); }
        public decimal? Episode19 { get => GetRating(18); set => SetRating(18, value); }
        public decimal? Episode20 { get => GetRating(19); set => SetRating(19, value); }
        public decimal? Episode21 { get => GetRating(20); set => SetRating(20, value); }
        public decimal? Episode22 { get => GetRating(21); set => SetRating(21, value); }
        public decimal? Episode23 { get => GetRating(22); set => SetRating(22, value); }

        public decimal? Episode24 { get => GetRating(23); set => SetRating(23, value); }
        public decimal? Episode25 { get => GetRating(24); set => SetRating(24, value); }
        public decimal? Episode26 { get => GetRating(25); set => SetRating(25, value); }

    }
}
