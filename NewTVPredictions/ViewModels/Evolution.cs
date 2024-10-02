using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Evolution : Predictable
    {
        public const int NumberOfModels = 30, NumberOfTrees = 2;

        //All family trees, each with a large number of models
        [DataMember]
        public List<PredictionModel>[] FamilyTrees = new List<PredictionModel>[NumberOfTrees];

        //The top performing model currently
        [DataMember]
        public PredictionModel[] TopModels = new PredictionModel[NumberOfTrees];

        [DataMember]
        double? _averageAccuracy, _averageError;

        public PredictionModel TopModel => TopModels[0];

        public double? AverageAccuracy
        {
            get => _averageAccuracy;
            set
            {
                _averageAccuracy = value;
                OnPropertyChanged(nameof(AverageAccuracy));
            }
        }
        public double? AverageError
        {
            get => _averageError;
            set
            {
                _averageError = value;
                OnPropertyChanged(nameof(AverageError));
            }
        }

        public bool TopModelChanged = true;

        DateTime? _lastUpdate = null;
        public DateTime? LastUpdate
        {
            get => _lastUpdate;
            set
            {
                _lastUpdate = value;
                OnPropertyChanged(nameof(LastUpdate));
            }
        }

        public string? LastUpdateText
        {
            get
            {
                if (LastUpdate is null)
                    return null;

                var Time = DateTime.Now - LastUpdate.Value;

                if (Time.TotalSeconds < 2)
                    return "(Updated 1 second ago)";
                else if (Time.TotalSeconds < 60)
                    return "(Updated " + Time.Seconds + " seconds ago)";
                else if (Time.TotalMinutes  < 2)
                    return "(Updated 1 minute ago)";
                else if (Time.TotalMinutes < 60)
                    return "(Updated " + Time.Minutes + " minutes ago)";
                else if (Time.TotalHours < 2)
                    return "(Updated 1 hour ago)";
                else if (Time.TotalHours < 24)
                    return "(Updated " + Time.Hours + " hours ago)";
                else if (Time.TotalDays < 2)
                    return "(Updated 1 day ago)";
                else
                    return "(Updated " + Time.Days + " days ago)";
            }
        }

        public void UpdateText()
        {
            OnPropertyChanged(nameof(LastUpdateText));
        }

        /// <summary>
        /// Initialize Evolution model
        /// </summary>
        /// <param name="network">Parent Network</param>
        public Evolution(Network network)
        {
            Network = network;
            OnPropertyChanged(nameof(Name));

            var WeightedShows = Network.GetWeightedShows();

            //initialize each family tree
            Parallel.For(0, NumberOfTrees, i =>
            {
                FamilyTrees[i] = Enumerable.Range(0, NumberOfModels).Select(x => new PredictionModel(Network)).ToList();
                TopModels[i] = FamilyTrees[i][0];

                //TopModels[i].TestAccuracyNew(WeightedShows);
            });
        }

        /// <summary>
        /// Get all shows on the network, weighted by year
        /// </summary>
        public IEnumerable<WeightedShow> GetWeightedShows()
        {
            return Network.GetWeightedShows();
        }

        /// <summary>
        /// The uniqueness of the Evolution model is based on the parent network
        /// </summary>
        public override int GetHashCode()
        {
            return Network.GetHashCode();
        }

        /// <summary>
        /// For simplicity in debugging, ToString will return the name of the parent network
        /// </summary>
        public override string ToString()
        {
            return Network.Name;
        }

        public string Name => Network.Name;
        bool ResetSecondary = false;

        public void Crossover()
        {
            ResetSecondary = false;
            var ModelToBeat = FamilyTrees[0].Last();
            var ModelsToAdd = FamilyTrees[1].Where(x => x > ModelToBeat);
            FamilyTrees[0].AddRange(ModelsToAdd);
            if (FamilyTrees[0].Count > NumberOfModels)
            {
                var NumberToRemove = FamilyTrees[0].Count - NumberOfModels;
                FamilyTrees[0].RemoveRange(30, NumberToRemove);
                ResetSecondary = true;
            }
        }

        public void UpdateTopModel(int i)
        {
            if (FamilyTrees[i][0] > TopModels[i])
            {
                TopModels[i] = FamilyTrees[i][0];
                if (i == 0)
                {
                    TopModelChanged = true;
                    LastUpdate = DateTime.Now;
                }
                    
            }
            else
            {
                FamilyTrees[i].Insert(0, TopModels[i]);
                FamilyTrees[i].RemoveAt(NumberOfModels);
            }                
        }

        public List<PredictionModel>[] GetLastGeneration()
        {
            var LastGeneration = new List<PredictionModel>[NumberOfTrees];
            for (int i = 0; i < NumberOfTrees; i++)
                LastGeneration[i] = FamilyTrees[i].ToList();

            return LastGeneration;
        }

        public double GetPeak()
        {
            return Math.Log10(NumberOfModels + 1);
        }

        public void Breed(int x, int y, List<PredictionModel>[] LastGeneration)
        {
            if (x == 1 && ResetSecondary)
            {
                FamilyTrees[x][y] = new PredictionModel(Network);
            }
            else
            {
                var r = Random.Shared;

                if (y == NumberOfModels - 1)
                {
                    //The final model in the next generation should be randomized, to introduce occasional added variation
                    FamilyTrees[x][y] = new PredictionModel(Network);
                }
                else
                {
                    //The rest of the models should be breeded by selecting two parents
                    //The better performing the mode, the more likely to be chosen as a parent
                    //int
                    //    Parent1 = (int)(Math.Pow(10, r.NextDouble() * Peak)) - 1,
                    //    Parent2 = (int)(Math.Pow(10, r.NextDouble() * Peak)) - 1;

                    int
                        Peak1 = r.Next(NumberOfModels),
                        Peak2 = r.Next(NumberOfModels),
                        Parent1 = r.Next(Peak1),
                        Parent2 = r.Next(Peak2);

                    if (Parent1 == Parent2)
                        FamilyTrees[x][y] = LastGeneration[x][Parent1];
                    else
                        FamilyTrees[x][y] = LastGeneration[x][Parent1] + LastGeneration[x][Parent2];
                }
            }            
        }

        /// <summary>
        /// Get the predicted Odds for a given show in its current state
        /// </summary>
        /// <param name="Show">Show to be tested</param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(Show Show)
        {
            var AllOutputs = TopModel.GetPerformanceAndThreshold(Show);

            var Episodes = new EpisodePair(Math.Min(Show.Ratings.Count, Show.Viewers.Count), Show.Episodes);

            return GetOdds(AllOutputs, Episodes);
        }

        public void UpdateAccuracy()
        {
            Accuracy = TopModel.Accuracy;
            Error = TopModel.Error;
        }
    }
}
