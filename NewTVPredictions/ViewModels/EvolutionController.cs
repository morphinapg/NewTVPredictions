using Avalonia.Threading;
using MsBox.Avalonia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    internal class EvolutionController
    {

        public List<Evolution> AllNetworks;
        public ConcurrentDictionary<Network, IEnumerable<WeightedShow>> WeightedShows = new();
        public ConcurrentDictionary<Predictable, IEnumerable<EpisodePair>> EpisodePairs = new();
        public ConcurrentDictionary<Network, PredictionStats> Stats = new();
        public bool UpdateAccuacy = true;
        //double Peak;

        public EvolutionController(List<Evolution> allNetworks)
        {
            AllNetworks = allNetworks;
            Parallel.ForEach(AllNetworks, x =>
            {
                WeightedShows[x.Network] = x.GetWeightedShows();
                EpisodePairs[x] = x.Network.GetEpisodePairs().AsParallel();
                x.TopModelChanged = false;

                ConcurrentDictionary<(Show, int), double>
                   RatingsProjections = new(),
                   ViewerProjections = new();

                Dictionary<int, double>
                    RatingsAverages = x.Network.GetAverageRatingPerYear(0),
                    ViewerAverages = x.Network.GetAverageRatingPerYear(1);

                double[]
                    RatingsOffsets = x.Network.GetEpisodeOffsets(RatingsAverages, 0),
                    ViewerOffsets = x.Network.GetEpisodeOffsets(ViewerAverages, 1);

                Stats[x.Network] = new PredictionStats(RatingsProjections, ViewerProjections, RatingsAverages, ViewerAverages, RatingsOffsets, ViewerOffsets);
            });

            //Reset secondary branch for each Evolution model
            Parallel.ForEach(AllNetworks.Select(x => Enumerable.Range(0, Evolution.NumberOfModels).Select(i => new { Model = x, Index = i })).SelectMany(x => x), x => x.Model.FamilyTrees[1][x.Index] = new PredictionModel(x.Model.Network));
        }

        public void NextGeneration()
        {
            // STEP 1 - ACCURACY TESTING //
            //First, we need to test every PredictionModel in every Evolution model for accuracy

            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees).SelectMany(x => x).Where(x => x.Accuracy is null), x => x.TestAccuracy(Stats[x.Network], WeightedShows[x.Network]));
            

            //foreach (var x in AllNetworks.SelectMany(x => x.FamilyTrees).SelectMany(x => x).Where(x => x.Accuracy is null))
            //    x.TestAccuracy(Stats[x.Network], WeightedShows[x.Network]);

            // STEP 2 - SORTING //
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees), x => x.Sort());

            // STEP 3 - CROSSOVER //
            Parallel.ForEach(AllNetworks, x => x.Crossover());

            // STEP 4 - UPDATE TOP MODELS //
            Parallel.ForEach(AllNetworks.Select(x => Enumerable.Range(0, Evolution.NumberOfTrees).Select(y => new { Evolution = x, FamilyTree = y })).SelectMany(x => x), x => x.Evolution.UpdateTopModel(x.FamilyTree));

            // STEP 5 - BREEDING //
            var Parents = new ConcurrentDictionary<Evolution, List<PredictionModel>[]>();
            Parallel.ForEach(AllNetworks, x => Parents[x] = x.GetParents());

            Parallel.ForEach(AllNetworks.Select(z => Enumerable.Range(0, Evolution.NumberOfTrees).Select(x => Enumerable.Range(0, Evolution.NumberOfModels).Select(y => new {Evolution = z, x, y }))).SelectMany(x => x).SelectMany(x => x), model => model.Evolution.Breed(model.x, model.y, Parents[model.Evolution]));
            
            // STEP 6 - MUTATION //
            var r = Random.Shared;
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees).SelectMany(x => x), x => x.MutateModel());
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees).Where(x => !x.Where(y => y.IsMutated).Any()), x => x[r.Next(Evolution.NumberOfModels)].IncreaseMutationRate());
        }

        public void UpdateMargins()
        {
            var EpisodePairs = Enumerable.Range(1, 26).Select(TotalEpisodes => Enumerable.Range(1, TotalEpisodes).Select(CurrentEpisode => new { CurrentEpisode, TotalEpisodes })).SelectMany(x => x);

            Parallel.ForEach( AllNetworks.Select(Evolution => EpisodePairs.Select(x => new { Evolution, x.CurrentEpisode, x.TotalEpisodes })).SelectMany(x => x), x => x.Evolution.CalculateMarginOfError(WeightedShows[x.Evolution.Network], Stats[x.Evolution.Network],x.CurrentEpisode, x.TotalEpisodes));
        }
    }
}
