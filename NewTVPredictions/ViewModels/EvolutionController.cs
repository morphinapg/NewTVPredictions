﻿using Avalonia.Threading;
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
            Parallel.ForEach(AllNetworks, x => x.TopModelChanged = false);

            // STEP 1 - ACCURACY TESTING //
            //First, we need to test every PredictionModel in every Evolution model for accuracy
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees).SelectMany(x => x).Where(x => x.Accuracy is null), x => x.TestAccuracy(WeightedShows[x.Network]));            

            // STEP 2 - SORTING //
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees), x => x.Sort());

            // STEP 3 - CROSSOVER //
            Parallel.ForEach(AllNetworks, x => x.Crossover());

            // STEP 4 - UPDATE TOP MODELS //
            Parallel.ForEach(AllNetworks.Select(x => Enumerable.Range(0, Evolution.NumberOfTrees).Select(y => new { Evolution = x, FamilyTree = y })).SelectMany(x => x), x => x.Evolution.UpdateTopModel(x.FamilyTree));

            // STEP 5 - BREEDING //
            var LastGeneration = new ConcurrentDictionary<Evolution, List<PredictionModel>[]>();
            Parallel.ForEach(AllNetworks, x => LastGeneration[x] = x.GetLastGeneration());

            Parallel.ForEach(AllNetworks.Select(z => Enumerable.Range(0, Evolution.NumberOfTrees).Select(x => Enumerable.Range(0, Evolution.NumberOfModels).Select(y => new {Evolution = z, x, y }))).SelectMany(x => x).SelectMany(x => x), model => model.Evolution.Breed(model.x, model.y, LastGeneration[model.Evolution]));

            // STEP 6 - MUTATION //
            Parallel.ForEach(AllNetworks.SelectMany(x => x.FamilyTrees).SelectMany(x => x), x => x.MutateModel());

            // STEP 7 - UPDATE EVOLUTION ACCURACY, IF TOP MODELS CHANGED //
            //these steps should only run after 100ms has passed, to avoid updating the UI too often

            Parallel.ForEach(AllNetworks.Where(x => x.TopModelChanged), x => x.UpdateAccuracy());
        }

        public void UpdateMargins()
        {

        }
    }
}
