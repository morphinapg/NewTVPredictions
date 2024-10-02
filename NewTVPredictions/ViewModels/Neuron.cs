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

        /// <summary>
        /// Initialize Neuron with just InputSize
        /// </summary>
        /// <param name="inputs">The number of inputs</param>
        public Neuron(int inputs)                                                           
        {
            var r = Random.Shared;
            weights = new double[inputs];
            for (int i = 0; i < inputs; i++)
                weights[i] = r.NextDouble() * 2 - 1;

            bias = r.NextDouble() * 2 - 1;
            outputbias = r.NextDouble() * 2 - 1;
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

            for (int i = 0; i < inputs.Length; i++)
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
            var r = Random.Shared;

            for (int i = 0; i < InputSize; i++)
                if (r.NextDouble() < mutationrate)
                    weights[i] += mutationintensity * (r.NextDouble() * 2 - 1);

            if (r.NextDouble() < mutationrate)
                bias += mutationintensity * (r.NextDouble() * 2 - 1);

            if (r.NextDouble() < mutationrate)
                outputbias += mutationintensity * (r.NextDouble() * 2 - 1);
        }
    }
}
