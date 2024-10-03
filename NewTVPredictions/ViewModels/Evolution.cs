﻿using Avalonia.Input;
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

        /// <summary>
        /// All family trees, each with a number of models
        /// </summary>
        [DataMember]
        public List<PredictionModel>[] FamilyTrees = new List<PredictionModel>[NumberOfTrees];

        /// <summary>
        /// The top performing models of all time
        /// </summary>
        [DataMember]
        public PredictionModel[][] TopModels = new PredictionModel[NumberOfTrees][];

        public PredictionModel TopModel1 => TopModels[0][0];
        public PredictionModel TopModel2 => TopModels[0][1];

        /// <summary>
        /// Whether the top model has changed this generation
        /// </summary>
        public bool TopModelChanged = true;

        DateTime? _lastUpdate = null;
        /// <summary>
        /// The last time the top model was updated
        /// </summary>
        public DateTime? LastUpdate
        {
            get => _lastUpdate;
            set
            {
                _lastUpdate = value;
                OnPropertyChanged(nameof(LastUpdate));
            }
        }

        /// <summary>
        /// Text representing how long it's been since the model updated
        /// </summary>
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

        /// <summary>
        /// Forces the UI to refresh the Last Update Text
        /// </summary>
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
                TopModels[i] = new PredictionModel[2];

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

        /// <summary>
        /// Whenever a model from the second branch is better than the worst model from the first, 
        /// add all better performing models and then trim to NumberOfModels
        /// </summary>
        public void Crossover()
        {
            ResetSecondary = false;
            var ModelToBeat = FamilyTrees[0].Last();
            var ModelsToAdd = FamilyTrees[1].Where(x => x > ModelToBeat);
            FamilyTrees[0].AddRange(ModelsToAdd);
            FamilyTrees[0].Sort();
            if (FamilyTrees[0].Count > NumberOfModels)
            {
                var NumberToRemove = FamilyTrees[0].Count - NumberOfModels;
                FamilyTrees[0].RemoveRange(30, NumberToRemove);
                ResetSecondary = true;
                TopModels[1] = new PredictionModel[2];
            }
        }

        /// <summary>
        /// Check if the top two performing models have improved in either family tree
        /// </summary>
        /// <param name="i">Family Tree index</param>
        public void UpdateTopModel(int i)
        {
            if (FamilyTrees[i][0] > TopModels[i][0])
            {
                //Move to second place
                TopModels[i][1] = TopModels[i][0];

                //Update top model
                TopModels[i][0] = FamilyTrees[i][0];
                if (i == 0)
                {
                    TopModelChanged = true;
                    LastUpdate = DateTime.Now;
                }

                //check if second child beats new second place as well
                if (FamilyTrees[i][1] > TopModels[i][1])
                    TopModels[i][1] = FamilyTrees[i][1];
            }
            else if (FamilyTrees[i][0] > TopModels[i][1])
                TopModels[i][1] = FamilyTrees[i][1];
        }

        /// <summary>
        /// Retrieve a selection of 4 parent candidates for breeding
        /// The first two are the top two performing models of all time
        /// The second two are the top two performing models
        /// </summary>
        public List<PredictionModel>[] GetParents()
        {
            var Parents = new List<PredictionModel>[NumberOfTrees];
            for (int i = 0; i < NumberOfTrees; i++)
            {
                Parents[i] = new();

                //Skip choosing parents on the secondary branch if ResetSecondary is true
                if (i == 0 || !ResetSecondary)
                {
                    if (TopModels[i][0] is not null)
                        Parents[i].Add(TopModels[i][0]);
                    if (TopModels[i][1] is not null) 
                        Parents[i].Add(TopModels[i][1]);

                    for (int j = 0; j < NumberOfModels && Parents[i].Count < 4; j++)
                        if (FamilyTrees[i][j] < Parents[i].Last())
                            Parents[i].Add(FamilyTrees[i][j]);

                    //In the rare case where there are not two unique models from FamilyTrees in addition to the top two models
                    while (Parents[i].Count < 4)
                        Parents[i].Add(new PredictionModel(Network));
                }                
            }

            return Parents;
        }

        /// <summary>
        /// Choose two parents from 4 possibilities, and combine the two to produce a child
        /// </summary>
        /// <param name="x">Family Tree index</param>
        /// <param name="y">Model Index</param>
        /// <param name="Parents">The 4 parents to choose from</param>
        public void Breed(int x, int y, List<PredictionModel>[] Parents)
        {
            if (x == 1 && ResetSecondary)
            {
                //Reset the secondary branch if models have crossed over to the first branch
                FamilyTrees[x][y] = new PredictionModel(Network);
            }
            else
            {
                //Create a new child by breeding two parents
                var r = Random.Shared;

                int
                    Parent1 = r.Next(4),
                    Parent2 = r.Next(4);

                //if (Parent2 >= Parent1)
                //    Parent2++;

                if (Parent1 == Parent2)
                    FamilyTrees[x][y] = new PredictionModel(Parents[x][Parent1]);
                else
                    FamilyTrees[x][y] = Parents[x][Parent1] + Parents[x][Parent2];
            }            
        }

        /// <summary>
        /// Get the predicted Odds for a given show in its current state
        /// </summary>
        /// <param name="Show">Show to be tested</param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(Show Show)
        {
            var AllOutputs = TopModel1.GetPerformanceAndThreshold(Show);

            var Episodes = new EpisodePair(Math.Min(Show.Ratings.Count, Show.Viewers.Count), Show.Episodes);

            return GetOdds(AllOutputs, Episodes);
        }

        /// <summary>
        /// Update the Accuracy and Error of the top performing model for the UI
        /// </summary>
        public void UpdateAccuracy()
        {
            if (TopModel1 is not null)
            {
                Accuracy = TopModel1.Accuracy;
                Error = TopModel1.Error;
            }            
        }
    }
}
