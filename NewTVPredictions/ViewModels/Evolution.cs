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
using MathNet.Numerics.Distributions;

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

        public PredictionModel TopModel => TopModels[0][0];

        /// <summary>
        /// Whether the top model has changed this generation
        /// </summary>
        public bool TopModelChanged = true;

        [DataMember]
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
        /// Represent the margins of error for each (CurrentEpisode, TotalEpisode) episode pair
        /// </summary>
        public ConcurrentDictionary<EpisodePair, double> MarginOfError = new();

        double? _oldAccuracy = null;
        /// <summary>
        /// The Accuracy of the Evolution model at the start of training
        /// </summary>
        public double? OldAccuracy
        {
            get => _oldAccuracy;
            set
            {
                _oldAccuracy = value;
                OnPropertyChanged(nameof(AccuracyChange));
                OnPropertyChanged(nameof(ActualChange));
            }
        }

        /// <summary>
        /// How much the Accuracy of the model has improved since the start of training
        /// </summary>
        public string? AccuracyChange
        {
            get
            {
                var change = Accuracy - OldAccuracy;

                if (change is null || change == 0 || Math.Round(change.Value * 100, 2) == 0)
                    return null;
                else
                {
                    var changestring = change.Value.ToString("P2");

                    if (change > 0)
                        changestring = "+" + changestring;

                    return changestring;
                }
            }
        }

        /// <summary>
        /// A double value representative of AccuracyChange
        /// </summary>
        public double? ActualChange => (Accuracy - OldAccuracy) * 100;

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

                if (Time.TotalSeconds > 60 && TopModelLocked == false)
                {
                    TopModelLocked = true;
                    OnPropertyChanged(nameof(Checkmark));
                }
                    

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
        /// This represents whether a model has remained consistent for at least 60 seconds at any time during training, suggesting that the model is settling on a possible optimal state
        /// </summary>
        public bool? TopModelLocked = null;

        /// <summary>
        /// Displays a checkmark next to the network name on HomePage if TopModelLocked is ever set to true
        /// </summary>
        public string? Checkmark => TopModelLocked == true && OldAccuracy is not null ? "✔️" : null;

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
            NetworkName = network.Name;
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
        public bool ResetSecondary = false;

        /// <summary>
        /// Whenever a model from the second branch is better than the worst model from the first, 
        /// add all better performing models and then trim to NumberOfModels
        /// </summary>
        public void Crossover()
        {
            ResetSecondary = false;
            var ModelToBeat = FamilyTrees[0][1];
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

                    if (TopModelLocked is null)
                        TopModelLocked = false;
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
        /// Update the Accuracy and Error of the top performing model for the UI
        /// </summary>
        public void UpdateAccuracy()
        {
            if (TopModel is not null)
            {
                Accuracy = TopModel.Accuracy;
                Error = TopModel.Error;

                OnPropertyChanged(nameof(AccuracyChange));
                OnPropertyChanged(nameof(ActualChange));
            }            
        }

        /// <summary>
        /// Generate predictions for a given year
        /// </summary>
        /// <param name="year">The year to predict</param>
        /// <param name="parallel">Whether to set show parameters in parallel (Important to avoid updating UI in non-UI thread)</param>
        public void GeneratePredictions(int year, bool parallel)
        {

            if (TopModel is not null)
            {
                var Shows = Network.Shows.Where(x => x.Year == year && x.ActualOdds is null && x.Ratings.Any() && x.Viewers.Any());
                var Predictions = new ConcurrentDictionary<Show, PredictionContainer>();

                Dictionary<int, double>
                    RatingsAverages = Network.GetAverageRatingPerYear(0),
                    ViewerAverages = Network.GetAverageRatingPerYear(1);

                double[]
                    RatingsOffsets = Network.GetEpisodeOffsets(RatingsAverages, 0),
                    ViewerOffsets = Network.GetEpisodeOffsets(ViewerAverages, 1);

                Parallel.ForEach(Shows, x =>
                {
                    List<double>
                        Ratings = x.Ratings.Select((rating, i) =>
                        {
                            double currentrating = rating is null || rating == 0 ?
                                Math.Log10(0.004) :
                                Math.Log10(rating.Value);

                            return currentrating - RatingsAverages[year] - RatingsOffsets[i];
                        }).ToList(),

                        Viewers = x.Viewers.Select((rating, i) =>
                        {
                            double currentrating = rating is null || rating == 0 ?
                                Math.Log10(0.0004) :
                                Math.Log10(rating.Value);

                            return currentrating - ViewerAverages[year] - ViewerOffsets[i];
                        }).ToList();

                    var outputs = TopModel.GetOutputs(x, x.Episodes, Ratings, Viewers);

                    double
                        ExpectedRatings = Network.GetProjectedRating(RatingsOffsets.Take(x.Episodes).ToList(), x.Episodes),
                        ExpectedViewers = Network.GetProjectedRating(ViewerOffsets.Take(x.Episodes).ToList(), x.Episodes),
                        RatingsProjection = Ratings.Count > 1 ? Network.GetProjectedRating(Ratings, x.Episodes) : Ratings[0] + (ExpectedRatings - RatingsOffsets[0]),
                        ViewersProjection = Viewers.Count > 1 ? Network.GetProjectedRating(Viewers, x.Episodes) : Viewers[0] + (ExpectedViewers - ViewerOffsets[0]);

                    if (x.CurrentEpisodes > x.Episodes)
                        x.Episodes = x.CurrentEpisodes;

                    double
                        CurrentRating = outputs[0] + RatingsProjection,
                        CurrentViewers = outputs[1] + ViewersProjection,
                        TargetRating = outputs[2],
                        TargetViewers = outputs[3],
                        Blend = outputs[4],
                        BlendedPerformance = CurrentRating * Blend + CurrentViewers * (1 - Blend),
                        BlendedThreshold = TargetRating * Blend + TargetViewers * (1 - Blend),
                        CurrentOdds = GetOdds(BlendedPerformance, BlendedThreshold, new EpisodePair(x.CurrentEpisodes, x.Episodes)),
                        CurrentPerformance = TopModel.GetCurrentPerformance(BlendedPerformance - BlendedThreshold, Blend);


                    CurrentRating = TopModel.GetRatingsPerformance(CurrentRating, RatingsAverages[year], 0);

                    CurrentViewers = TopModel.GetRatingsPerformance(CurrentViewers, ViewerAverages[year], 1);

                    TargetRating = TopModel.GetRatingsPerformance(TargetRating, RatingsAverages[year], 0);

                    TargetViewers = TopModel.GetRatingsPerformance(TargetViewers, ViewerAverages[year], 1);


                    CurrentRating = Math.Pow(10,CurrentRating);
                    CurrentViewers = Math.Pow(10,CurrentViewers);
                    TargetRating = Math.Pow(10,TargetRating);
                    TargetViewers = Math.Pow(10,TargetViewers);


                    if (parallel)
                    {
                        x.CurrentRating = CurrentRating;
                        x.CurrentViewers = CurrentViewers;
                        x.TargetRating = TargetRating;
                        x.TargetViewers = TargetViewers;
                        x.CurrentPerformance = CurrentPerformance;
                        x.CurrentOdds = CurrentOdds;
                    }
                    else
                        Predictions[x] = new PredictionContainer(CurrentRating, CurrentViewers, CurrentPerformance, TargetRating, TargetViewers, CurrentOdds);
                });

                if (!parallel)
                    foreach (var Show in Shows)
                    {
                        var Prediction = Predictions[Show];
                        Show.CurrentRating = Prediction.CurrentRating;
                        Show.CurrentViewers = Prediction.CurrentViewers;
                        Show.TargetRating = Prediction.TargetRating;
                        Show.TargetViewers = Prediction.TargetViewers;
                        Show.CurrentPerformance = Prediction.CurrentPerformance;
                        Show.CurrentOdds = Prediction.CurrentOdds;
                    }
            }            
        }

        /// <summary>
        /// Calculate a margin of error for a given EpisodePair (Current/Total episodes)
        /// </summary>
        /// <param name="WeightedShows">All shows from the network marked as renewed or canceled, weighted by year</param>
        /// <param name="Stats">A collection of statistics necessary for predictions</param>
        /// <param name="CurrentEpisode">the current episode # being tested</param>
        /// <param name="TotalEpisodes">the total number of episodes for a show</param>
        public void CalculateMarginOfError(IEnumerable<WeightedShow> WeightedShows, PredictionStats Stats, int CurrentEpisode, int TotalEpisodes)
        {
            if (TopModel is not null)
            {
                var Episodes = new EpisodePair(CurrentEpisode, TotalEpisodes);

                var Margin = TestAccuracy(TopModel, Stats, WeightedShows, true, CurrentEpisode, TotalEpisodes)[0];

                if (double.IsNaN(Margin) || double.IsInfinity(Margin) || Margin == 0)
                    Margin = 100;

                MarginOfError[Episodes] = Margin;
            }         
        }

        /// <summary>
        /// Deep clone other Evolution model
        /// </summary>
        public Evolution(Evolution other)
        {
            Accuracy = other.Accuracy;
            Error = other.Error;
            NetworkName = other.NetworkName;
            LastUpdate = other.LastUpdate;

            for (int i = 0; i < NumberOfTrees; i++)
            {
                FamilyTrees[i] = new();

                foreach(var model in other.FamilyTrees[i].ToList())
                {
                    FamilyTrees[i].Add(model);
                }

                TopModels[i] = new PredictionModel[2];
                TopModels[i][0] = other.TopModels[i][0];
                TopModels[i][1] = other.TopModels[i][1];
            }

            MarginOfError = other.MarginOfError is null ? new() : new ConcurrentDictionary<EpisodePair, double>(other.MarginOfError);
        }

        /// <summary>
        /// Get the renewal odds of a show, given the Performance and Threshold.
        /// MarginOfError needs to have been calculated already
        /// </summary>
        /// <param name="outputs">Output of GetPerformanceAndThreshold</param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(double ShowPerformance, double ShowThreshold, EpisodePair Episodes)
        {
            var Normal = new Normal(ShowThreshold, MarginOfError[Episodes]);

            return Normal.CumulativeDistribution(ShowPerformance);
        }
    }
}
