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

        public ParallelQuery<Evolution> AllNetworks;
        public ConcurrentDictionary<Evolution, IEnumerable<WeightedShow>> WeightedShows = new();
        public ConcurrentDictionary<Network, IEnumerable<EpisodePair>> EpisodePairs = new();

        public EvolutionController(List<Evolution> allNetworks)
        {
            AllNetworks = allNetworks.AsParallel();
            Parallel.ForEach(AllNetworks, x =>
            {
                WeightedShows[x] = x.GetWeightedShows();
                EpisodePairs[x.Network] = x.Network.GetEpisodePairs();
            });
        }

        public void NextGeneration()
        {
            AllNetworks.ForAll(x => x.TopModelChanged = false);


            // STEP 1 - ACCURACY TESTING //
            //First, we need to test every PredictionModel in every Evolution model for accuracy

            //The first step to do that is to run every action needed to test the RatingsModel of every PredictionModel in parallel
            var RatingsResults = new ConcurrentDictionary<PredictionModel, IEnumerable<ErrorContainer>>();

            AllNetworks.Select(x => x.GetRatingsActions(WeightedShows[x])).SelectMany(x => x).Select(x => x())
            .GroupBy(x => x.Model).ForAll(x => RatingsResults[x.Key] = x.AsEnumerable());

            //Next, run all actions needed to test the RenewalModel of every PredictionModel
            var PredictionResults = new ConcurrentDictionary<PredictionModel, IEnumerable<ShowErrorContainer>>();

            AllNetworks.Select(x => x.GetPredictionActions(WeightedShows[x])).SelectMany(x => x).Select(x => x())
            .GroupBy(x => x.Model).ForAll(x => PredictionResults[x.Key] = x.AsEnumerable());

            //Get all PredictionModel objects, and then run the code that sets the accuracy and error for each model
            RatingsResults.AsParallel().Select(x => x.Key).ForAll(x => x.SetAccuracyAndError(RatingsResults[x], PredictionResults[x], EpisodePairs[x.Network]));

            // STEP 2 - SORTING //
            AllNetworks.Select(x => x.GetSortActions()).SelectMany(x => x).ForAll(x => x());

            // STEP 3 - UPDATE TOP MODELS //
            AllNetworks.Select(x => x.UpdateModelActions()).SelectMany(x => x).ForAll(x => x());

            // STEP 4 - BREEDING //
            AllNetworks.Select(x => x.GetBreedingActions()).SelectMany(x => x).ForAll(x => x());


            // STEP 5 - MUTATION //



            // STEP 6 - UPDATE EVOLUTION ACCURACY, IF TOP MODELS CHANGED //
        }
    }
}
