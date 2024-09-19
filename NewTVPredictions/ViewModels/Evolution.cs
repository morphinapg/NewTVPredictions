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

        [DataMember]
        List<PredictionModel>[] FamilyTrees = new List<PredictionModel>[NumberOfTrees];

        [DataMember]
        public Network Network;

        /// <summary>
        /// Initialize Evolution model
        /// </summary>
        /// <param name="network">Parent Network</param>
        public Evolution(Network network)
        {
            Network = network;

            //initialize each family tree
            Parallel.For(0, NumberOfTrees, i => FamilyTrees[i] = Enumerable.Range(0, NumberOfModels).Select(x => new PredictionModel(Network)).ToList());
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
            return FamilyTrees.SelectMany(x => x).Select(x => x.GetRatingsActions(WeightedShows)).SelectMany(x => x);
        }

        /// <summary>
        /// Get all actions necessary to test the RenewalModel in each PredictionModel
        /// </summary>
        /// <param name="WeightedShows">A list of all shows on the network, weighted by year</param>
        /// <returns></returns>
        public IEnumerable<Func<ShowErrorContainer>> GetPredictionActions(IEnumerable<WeightedShow> WeightedShows)
        {
            return FamilyTrees.SelectMany(x => x).Select(x => x.GetPredictionActions(WeightedShows)).SelectMany(x => x);
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
    }
}
