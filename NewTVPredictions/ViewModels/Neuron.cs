using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Neuron : Breedable
    {
        [DataMember]
        double bias = 0, outputbias = 0;

        [DataMember]
        double[] weights;

        [DataMember]
        int InputSize;

        [DataMember]
        public bool IsMutated = false;

        /// <summary>
        /// Initialize Neuron with just InputSize
        /// </summary>
        /// <param name="inputs">The number of inputs</param>
        public Neuron(int inputs)                                                           
        {
            weights = new double[inputs];
            InputSize = inputs;
        }

        /// <summary>
        /// Clone existing Neuron
        /// </summary>
        /// <param name="other">Neuron to clone</param>
        public Neuron(Neuron other)                                                        
        {
            bias = other.bias;
            outputbias = other.outputbias;
            InputSize = other.InputSize;

            weights = new double[InputSize];
            for (int i = 0; i < InputSize; i++)
                weights[i] = other.weights[i];
        }

        /// <summary>
        /// Breed two neurons together
        /// </summary>
        /// <param name="x">First Neuron</param>
        /// <param name="y">Second Neuron</param>
        public Neuron(Neuron x, Neuron y)                                         
        {
            var r = Random.Shared;
            IsMutated = false;
            bias = Breed(x.bias, y.bias);
            outputbias = Breed(x.outputbias, y.outputbias);

            InputSize = x.InputSize;

            weights = new double[x.InputSize];
            for (int i = 0; i < InputSize; i++)
                weights[i] = Breed(x.weights[i], y.weights[i]);
        }

        public static Neuron operator +(Neuron x, Neuron y)
        {
            return new Neuron(x, y);
        }

        /// <summary>
        /// Activation function
        /// </summary>
        /// <param name="d">Value to modify</param>
        /// <returns>modified value</returns>
        double Activation(double d)                                                        
        {
            return (2 / (1 + Math.Exp(-1 * d))) - 1;
        }

        /// <summary>
        /// Produce an output given a series of inputs
        /// </summary>
        /// <param name="inputs">Inputs for the Neuron to process</param>
        /// <param name="output">Whether the Neuron is on the output layer or not</param>
        /// <returns>The output of the neuron's processing</returns>
        public double GetOutput(double[] inputs, bool output = false)                       
        {
            double total = 0;

            for (int i = 0; i < InputSize; i++)
                total += inputs[i] * weights[i];

            total += bias;

            return output ? total : Activation(total) + outputbias;
        }

        /// <summary>
        /// Mutate the neuron
        /// </summary>
        /// <param name="mutationrate">The mutation rate</param>
        /// <param name="mutationintensity">The mutation intensity</param>
        public void Mutate(double mutationrate, double mutationintensity)                   
        {
            var MutationActions = GetMutationActions(mutationrate, mutationintensity);

            Parallel.ForEach(MutationActions, x => x());
        }

        /// <summary>
        /// Retrieve the actions necessary to mutate the model
        /// </summary>
        /// <param name="mutationrate">The mutation rate</param>
        /// <param name="mutationintensity">The mutation intensity</param>
        /// <returns>An IEnumerable with the actions necessary to mutate the model</returns>
        public IEnumerable<Action> GetMutationActions(double mutationrate, double mutationintensity)
        {
            var r = Random.Shared;

            return Enumerable.Range(0, InputSize + 2).Select<int, Action>(i => () =>
            {
                if (i < InputSize)
                {
                    if (r.NextDouble() < mutationrate)
                    {
                        weights[i] += mutationintensity * (r.NextDouble() * 2 - 1);
                        IsMutated = true;
                    }
                }                    
                else if (i == InputSize)
                {
                    if (r.NextDouble() < mutationrate)
                    {
                        bias += mutationintensity * (r.NextDouble() * 2 - 1);
                        IsMutated = true;
                    }
                }
                else
                {
                    if (r.NextDouble() < mutationrate)
                    {
                        outputbias += mutationintensity * (r.NextDouble() * 2 - 1);
                        IsMutated = true;
                    }
                }
            });            
        }
    }
}
