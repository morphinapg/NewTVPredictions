using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    internal class Evolution : Predictable
    {
        const int NumberOfModels = 30, NumberOfTrees = 10;

        List<PredictionModel>[] FamilyTrees = new List<PredictionModel>[NumberOfTrees];        

        Network Network;

        public Evolution(Network network)
        {
            Network = network;

            //initialize each family tree
            Parallel.For(0, NumberOfTrees, i => FamilyTrees[i] = Enumerable.Range(0, NumberOfModels).Select(x => new PredictionModel(Network)).ToList());
        }
    }
}
