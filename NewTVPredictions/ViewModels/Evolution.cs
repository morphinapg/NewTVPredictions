using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    internal class Evolution : Predictable
    {
        const int NumberOfModels = 30, NumberOfTrees = 10;

        //All family trees, each with a large number of models
        [DataMember]
        List<PredictionModel>[] FamilyTrees = new List<PredictionModel>[NumberOfTrees];

        //The top performing model currently
        [DataMember]
        PredictionModel[] TopModels = new PredictionModel[NumberOfTrees];

        [DataMember]
        public Network Network;

        public bool TopModelChanged = false;

        /// <summary>
        /// Initialize Evolution model
        /// </summary>
        /// <param name="network">Parent Network</param>
        public Evolution(Network network)
        {
            Network = network;

            //initialize each family tree
            Parallel.For(0, NumberOfTrees, i =>
            {
                FamilyTrees[i] = Enumerable.Range(0, NumberOfModels).Select(x => new PredictionModel(Network)).ToList();
                TopModels[i] = FamilyTrees[i][0];
                TopModels[i].TestAccuracy();
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
        /// Get the actions necessary to test the RatingsModel in each PredictionModel
        /// </summary>
        /// <param name="WeightedShows">a list of all shows on the network, weighted by year</param>
        /// <returns></returns>
        public IEnumerable<Func<ErrorContainer>> GetRatingsActions(IEnumerable<WeightedShow> WeightedShows)
        {
            return FamilyTrees.SelectMany(x => x).Where(x => x.Accuracy is null || x.Error is null).Select(x => x.GetRatingsActions(WeightedShows)).SelectMany(x => x);
        }

        /// <summary>
        /// Get all actions necessary to test the RenewalModel in each PredictionModel
        /// </summary>
        /// <param name="WeightedShows">A list of all shows on the network, weighted by year</param>
        /// <returns></returns>
        public IEnumerable<Func<ShowErrorContainer>> GetPredictionActions(IEnumerable<WeightedShow> WeightedShows)
        {
            return FamilyTrees.SelectMany(x => x).Where(x => x.Accuracy is null || x.Error is null).Select(x => x.GetPredictionActions(WeightedShows)).SelectMany(x => x);
        }

        /// <summary>
        /// Get all actions necessary to sort all Family Trees
        /// </summary>
        public IEnumerable<Action> GetSortActions()
        {
            return FamilyTrees.Select<List<PredictionModel>, Action>(x => () => x.Sort());
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

        /// <summary>
        /// If the current generation has created a new model that performs better than the current
        /// top performing model, replace it. Otherwise, replace the first model in the FamilyTree
        /// with the existing top performing model, for breeding purposes
        /// </summary>
        public IEnumerable<Action> UpdateModelActions()
        {
            return Enumerable.Range(0, NumberOfTrees).Select(i => new Action(() =>
            {
                if (FamilyTrees[i][0] > TopModels[i])
                {
                    TopModels[i] = FamilyTrees[i][0];
                    TopModelChanged = true;
                }                    
                else
                    FamilyTrees[i][0] = TopModels[i];
            }));
        }

        /// <summary>
        /// Get all Actions necessary to breed the existing parent models for a new generation
        /// </summary>
        public IEnumerable<Action> GetBreedingActions()
        {
            //First, we need to make a temporary copy of all existing family trees
            var LastGeneration = new List<PredictionModel>[NumberOfTrees];
            for (int i = 0; i < NumberOfTrees; i++)
                LastGeneration[i] = FamilyTrees[i].ToList();

            var r = Random.Shared;
            var Peak = Math.Log10(NumberOfModels + 1);

            return Enumerable.Range(0, NumberOfTrees).Select(x => Enumerable.Range(0, NumberOfModels).Select(y => new { x, y })).SelectMany(x => x).Select(model => new Action(() =>
            {
                if (model.y == NumberOfModels - 1)
                {
                    //The final model in the next generation should be randomized, to introduce occasional added variation
                    FamilyTrees[model.x][model.y] = new PredictionModel(Network);
                }
                else
                {
                    //The rest of the models should be breeded by selecting two parents
                    //The better performing the mode, the more likely to be chosen as a parent
                    int
                        Parent1 = (int)(Math.Pow(10, r.NextDouble() * Peak)) - 1,
                        Parent2 = (int)(Math.Pow(10, r.NextDouble() * Peak)) - 1;

                    if (Parent1 == Parent2)
                        FamilyTrees[model.x][model.y] = LastGeneration[model.x][Parent1];
                    else
                        FamilyTrees[model.x][model.y] = LastGeneration[model.x][Parent1] + LastGeneration[model.x][Parent2];
                }
            }));

        }
    }
}
