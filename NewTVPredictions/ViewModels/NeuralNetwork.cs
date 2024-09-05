using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    internal class NeuralNetwork : Breedable
    {
        [DataMember]
        double[] inputbias, inputweight;                                                                                //Values used to pre-normalize the input values

        [DataMember]
        int InputCount, OutputCount;                                                                                    //How many inputs and outputs the neural network will be capable of handling

        [DataMember]
        List<int> LayerSizes;                                                                                           //Defined number of neurons in each hidden layer

        [DataMember]
        List<Neuron[]> HiddenLayers;                                                                                    //A list including every hidden layer

        [DataMember]
        Neuron[] OutputLayer;                                                                                           //Array of neurons used for the output(s)

        [DataMember]
        double mutationrate, mutationintensity;                                                                         //values for controlling evolution

        [DataMember]
        bool IsMutated;

        /// <summary>
        /// Creates a new Neural Network
        /// </summary>
        /// <param name="inputs">Number of Inputs</param>
        /// <param name="bias">bias value for each input</param>
        /// <param name="weight">weight value for each input</param>
        /// <param name="outputs">(optional) Number of output values</param>
        public NeuralNetwork(int inputs, double[] bias, double[] weight, int outputs = 1)
        {
            inputbias = bias;
            inputweight = weight;

            InputCount = inputs;
            OutputCount = outputs;

            int CurrentLayer = (int)Math.Round(InputCount * 2.0 / 3 + OutputCount, 0);

            LayerSizes = new();

            while (CurrentLayer != OutputCount)
            {
                LayerSizes.Add(CurrentLayer);

                var CurrentAvg = (CurrentLayer + OutputCount) / 2.0;

                CurrentLayer = CurrentLayer > OutputCount ? (int)CurrentAvg : (int)Math.Ceiling(CurrentAvg);
            }

            HiddenLayers = new();
            int PreviousSize = InputCount;
            foreach (var LayerSize in LayerSizes)
            {
                var NewLayer = new Neuron[LayerSize];
                for (int i = 0; i < LayerSize; i++)
                {
                    NewLayer[i] = new Neuron(PreviousSize);
                }

                HiddenLayers.Add(NewLayer);

                PreviousSize = LayerSize;
            }

            OutputLayer = new Neuron[PreviousSize];

            var r = Random.Shared;
            mutationrate = r.NextDouble();
            mutationintensity = r.NextDouble();

            IsMutated = false;
        }

        /// <summary>
        /// Deep cloning an existing NeuralNetwork
        /// </summary>
        public NeuralNetwork(NeuralNetwork other)
        {
            InputCount = other.InputCount;
            OutputCount = other.OutputCount;

            inputbias = new double[InputCount];
            inputweight = new double[InputCount];
            for (int i = 0; i < InputCount; i++)
            {
                inputbias[i] = other.inputbias[i];
                inputweight[i] = other.inputweight[i];
            }

            LayerSizes = new();
            HiddenLayers = new();
            for (int LayerIndex = 0; LayerIndex < other.LayerSizes.Count; LayerIndex++)
            {
                var LayerSize = other.LayerSizes[LayerIndex];
                LayerSizes.Add(LayerSize);

                var NewLayer = new Neuron[LayerSize];
                for (int i = 0; i < LayerSize; i++)
                    NewLayer[i] = new Neuron(other.HiddenLayers[LayerIndex][i]);

                HiddenLayers.Add(NewLayer);
            }

            OutputLayer = new Neuron[OutputCount];
            for (int i = 0; i < OutputCount; i++)
                OutputLayer[i] = new Neuron(other.OutputLayer[i]);

            mutationrate = other.mutationrate;
            mutationintensity = other.mutationintensity;

            IsMutated = other.IsMutated;
        }

        /// <summary>
        /// Run a series of inputs through the nerual network and achieve an output
        /// </summary>
        /// <param name="inputs">The input values</param>
        /// <param name="smallerinput">If the inputs are smaller than the full amount expected, set to true</param>
        /// <returns>double[] representing the output of the neural network</returns>
        public double[] GetOutput(double[] inputs, bool smallerinput = false)
        {
            //For the purpose of recurrent neural networks, a lower number of inputs is allowed
            var CurrentInputSize = smallerinput ? inputs.Length : InputCount;

            //Preformat inputs with bias and weights
            for (int i = 0; i < CurrentInputSize; i++)
                inputs[i] = (inputs[i] + inputbias[i]) * inputweight[i];

            //Run through every hidden layer
            double[] CurrentOutputs, CurrentInputs;
            CurrentInputs = inputs;

            for (int i = 0; i < HiddenLayers.Count; i++)
            {
                var CurrentSize = LayerSizes[i];
                CurrentOutputs = new double[CurrentSize];

                for (int x = 0; x < CurrentSize; x++)
                    CurrentOutputs[x] = HiddenLayers[i][x].GetOutput(CurrentInputs);

                CurrentInputs = CurrentOutputs;

                CurrentInputSize = CurrentSize;
            }

            //Calculate output layer results
            CurrentOutputs = new double[OutputCount];
            for (int i = 0; i < OutputCount; i++)
                CurrentOutputs[i] = OutputLayer[i].GetOutput(CurrentInputs, true);

            return CurrentOutputs;
        }

        /// <summary>
        /// Mutate any double value, for use in model parameters
        /// </summary>
        /// <param name="d">input value</param>
        /// <returns>output value, either original or mutated</returns>
        double MutateValue(double d)
        {
            var r = Random.Shared;

            if (r.NextDouble() < mutationrate)
            {
                IsMutated = true;

                if (d == mutationrate)
                {
                    var p = r.NextDouble();
                    return r.NextDouble() * p + d * (1 - p);
                }
                else if (d == mutationintensity)
                    return d + Math.Abs(r.NextDouble() * 2 - 1);
                else
                    return d + (r.NextDouble() * 2 - 1) * mutationintensity;
            }
            else
                return d;
        }

        /// <summary>
        /// Perform mutation on the model
        /// </summary>
        public void MutateModel()
        {
            var MutationActions = GetMutationActions();

            Parallel.ForEach(MutationActions, x => x());

            CheckIfNeuronsMutated();
        }

        /// <summary>
        /// Evolve the mutationrate and mutationintensity parameters, and then return the actions necessary to mutate the rest of the model.
        /// Remember to use CheckIfNeuronsMutated() after running these actions!
        /// </summary>
        /// <returns>An IEnumerable with all of the actions necessary to mutate the model</returns>
        public IEnumerable<Action> GetMutationActions()
        {
            //First, we need to set the mutation rate and intensity, as they are necessary for every other step
            mutationrate = MutateValue(mutationrate);
            mutationintensity = MutateValue(mutationintensity);

            //Now, mutate the input bias and weight values
            var InputActions = Enumerable.Range(0, InputCount).SelectMany(i => new[]
            {
                new Action(() => inputbias[i] = MutateValue(inputbias[i])),
                new Action(() => inputweight[i] = MutateValue(inputweight[i]))
            });

            //Now, mutate the neurons in the hidden and output layers
            var NeuronActions = HiddenLayers.SelectMany(x => x).Concat(OutputLayer).Select(x => x.GetMutationActions(mutationrate, mutationintensity)).SelectMany(x => x);

            return InputActions.Concat(NeuronActions);
        }

        /// <summary>
        /// This needs to be run after the mutation actions are run. Sets IsMutated if any neurons have IsMutated as true
        /// </summary>
        public void CheckIfNeuronsMutated()
        {
            if (!IsMutated)
            {
                var AllNeurons = HiddenLayers.SelectMany(x => x).Concat(OutputLayer);

                if (AllNeurons.Where(x => x.IsMutated).Any())
                    IsMutated = true;
            }            
        }

        /// <summary>
        /// Breed two neural networks together. After doing this, you should mutate the resulting model.
        /// </summary>
        /// <param name="x">First Model</param>
        /// <param name="y">Second Model</param>
        public NeuralNetwork(NeuralNetwork x, NeuralNetwork y)
        {
            InputCount = x.InputCount;
            OutputCount = x.OutputCount;

            inputbias = new double[InputCount];
            inputweight = new double[InputCount];
            for (int i = 0; i < InputCount; i++)
            {
                inputbias[i] = Breed(x.inputbias[i], y.inputbias[i]);
                inputweight[i] = Breed(x.inputweight[i], y.inputweight[i]);
            }

            LayerSizes = new();
            HiddenLayers = new();
            for (int LayerIndex = 0; LayerIndex < x.LayerSizes.Count; LayerIndex++)
            {
                var LayerSize = x.LayerSizes[LayerIndex];
                LayerSizes.Add(LayerSize);

                var NewLayer = new Neuron[LayerSize];
                for (int i = 0; i < LayerSize; i++)
                    NewLayer[i] = x.HiddenLayers[LayerIndex][i] + y.HiddenLayers[LayerIndex][i];

                HiddenLayers.Add(NewLayer);
            }

            OutputLayer = new Neuron[OutputCount];
            for (int i = 0; i < OutputCount; i++)
                OutputLayer[i] = x.OutputLayer[i] + y.OutputLayer[i];

            mutationrate = Breed(x.mutationrate,y.mutationrate);
            mutationintensity = Breed(x.mutationintensity, y.mutationintensity);

            IsMutated = false;
        }

        public static NeuralNetwork operator +(NeuralNetwork x, NeuralNetwork y)
        {
            return new NeuralNetwork(x, y);
        }

        
    }
}
