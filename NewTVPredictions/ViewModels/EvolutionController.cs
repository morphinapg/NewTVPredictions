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
        public bool UpdateAccuacy = true;

        public EvolutionController(List<Evolution> allNetworks)
        {
            AllNetworks = allNetworks;
            Parallel.ForEach(AllNetworks, x =>
            {
                WeightedShows[x.Network] = x.GetWeightedShows();
                EpisodePairs[x] = x.Network.GetEpisodePairs().AsParallel();
            });
        }

        public void NextGeneration()
        {
            // STEP 1 - ACCURACY TESTING //
            //First, we need to test every PredictionModel in every Evolution model for accuracy
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees).SelectMany(x => x), x => x.TestAccuracy(WeightedShows[x.Network]));
            

            // STEP 2 - SORTING //
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees), x => x.Sort());

            // STEP 3 - UPDATE TOP MODELS //
            Parallel.ForEach(AllNetworks.Select(x => Enumerable.Range(0, Evolution.NumberOfTrees).Select(y => new { Evolution = x, FamilyTree = y })).SelectMany(x => x), x => x.Evolution.UpdateTopModel(x.FamilyTree));

            // STEP 4 - BREEDING //
            var LastGeneration = new ConcurrentDictionary<Evolution, List<PredictionModel>[]>();
            var Peak = new ConcurrentDictionary<Evolution, double>();
            Parallel.ForEach(AllNetworks, x => 
            {
                LastGeneration[x] = x.GetLastGeneration();
                Peak[x] = x.GetPeak();
            });

            Parallel.ForEach(AllNetworks.Select(z => Enumerable.Range(0, Evolution.NumberOfTrees).Select(x => Enumerable.Range(0, Evolution.NumberOfModels).Select(y => new {Evolution = z, x, y }))).SelectMany(x => x).SelectMany(x => x), model => model.Evolution.Breed(model.x, model.y, LastGeneration[model.Evolution], Peak[model.Evolution]));

            // STEP 5 - MUTATION //
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees).SelectMany(x => x), x => x.MutateModel());

            // STEP 6 - UPDATE EVOLUTION ACCURACY, IF TOP MODELS CHANGED //
            //these steps should only run after 100ms has passed, to avoid updating the UI too often

            Parallel.ForEach(AllNetworks.Where(x => x.TopModelChanged), x => x.UpdateAccuracy());
        }
    }
}
