﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    internal class NeuralNetwork : Breedable
    {
        [DataMember]
        List<double[]> InputAverage, InputDeviation, OutputBias;                                                        //Values used to pre-normalize the input values, and adjust output values

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
        public bool IsMutated = false;

        /// <summary>
        /// Creates a new Neural Network
        /// </summary>
        /// <param name="inputs">Number of Inputs</param>
        /// <param name="average">average value for each input, with a list for each input type</param>
        /// <param name="deviation">deviation value for each input, with a list for each input type</param>
        /// <param name="outputbias">average value of what the outputs should be, with a list for each input type</param>
        public NeuralNetwork(List<double[]> average, List<double[]> deviation, List<double[]> outputbias)
        {

            if (average.Count == 0 || deviation.Count == 0)
                throw new Exception("Average and/or Deviation values missing!");
            if (average.Count != deviation.Count)
                throw new Exception("Input Types mismatch on Average and Deviation!");
            if (average.Concat(deviation).Where(x => x.Length != average[0].Length).Any())
                throw new Exception("Number of inputs does not match on every instance of average/deviation!");
            if (average.Count != outputbias.Count)
                throw new Exception("Input and output types do not match!");
            if (outputbias.Where(x => x.Length != outputbias[0].Length).Any())
                throw new Exception("Number of outputs does not match for every outputbias!");

            InputCount = average[0].Length;
            OutputCount = outputbias[0].Length;

            InputAverage = average;
            InputDeviation = deviation;
            OutputBias = outputbias;

            int CurrentLayer = (int)Math.Round(InputCount * 2.0 / 3 + OutputCount, 0);

            LayerSizes = new();

            while (CurrentLayer != OutputCount)
            {
                LayerSizes.Add(CurrentLayer);

                var CurrentSize = (CurrentLayer * 1.0 / (LayerSizes.Count + 1)) + (OutputCount * (double)LayerSizes.Count / (LayerSizes.Count + 1));

                var fraction = CurrentSize / OutputCount;

                var multiplier = (int)Math.Round(fraction, 0);

                if (fraction - (int)fraction == 0.5)
                {
                    if (CurrentLayer > OutputCount)
                        multiplier = (int)fraction;
                    else
                        multiplier = (int)(Math.Ceiling(fraction));
                }

                CurrentLayer = multiplier * OutputCount;
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

            OutputLayer = new Neuron[OutputCount];
            for (int i = 0; i < OutputCount; i++)
                OutputLayer[i] = new Neuron(PreviousSize);

            var r = Random.Shared;
            mutationrate = r.NextDouble();
            mutationintensity = r.NextDouble();
        }

        /// <summary>
        /// Deep cloning an existing NeuralNetwork
        /// </summary>
        public NeuralNetwork(NeuralNetwork other)
        {
            InputCount = other.InputCount;
            OutputCount = other.OutputCount;

            InputAverage = new();
            InputDeviation = new();
            OutputBias = new();
            var InputTypes = other.InputAverage.Count;

            for (int i = 0; i < InputTypes; i++)
            {
                var bias = new double[InputCount];
                var weight = new double[InputCount];
                var outputbias = new double[OutputCount];

                for (int x = 0; x < InputCount; x++)
                {
                    bias[x] = other.InputAverage[i][x];
                    weight[x] = other.InputDeviation[i][x];
                }

                for (int x = 0; x < OutputCount; x++)
                    outputbias[x] = other.OutputBias[i][x];

                InputAverage.Add(bias);
                InputDeviation.Add(weight);
                OutputBias.Add(outputbias);
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
        }

        /// <summary>
        /// Run a series of inputs through the nerual network and achieve an output
        /// </summary>
        /// <param name="inputs">The input values</param>
        /// <param name="InputType">If there are multiple input types, this allows input data to be preformatted in different ways</param>
        /// <returns>double[] representing the output of the neural network</returns>
        public double[] GetOutput(double[] inputs, int InputType = 0)
        {
            //For the purpose of recurrent neural networks, a lower number of inputs is allowed
            var CurrentInputSize = inputs.Length;

            //Choose the correct Input Bias and Weight based on InputType
            var bias = InputAverage[InputType];
            var deviation = InputDeviation[InputType];

            //Preformat inputs with bias and weights
            for (int i = 0; i < CurrentInputSize; i++)
            {
                inputs[i] = (inputs[i] - bias[i]) / deviation[i];
            }
                

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
            {
                CurrentOutputs[i] = OutputLayer[i].GetOutput(CurrentInputs, true) + OutputBias[InputType][i];
            }
                

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
                    //var p = r.NextDouble();
                    //return r.NextDouble() * p + d * (1 - p);

                    double
                        min = Math.Max(d - mutationintensity, 0),
                        max = Math.Min(d + mutationintensity, 1),
                        range = max - min;

                    return r.NextDouble() * range + min;
                }
                else if (d == mutationintensity)
                    return Math.Abs(d + r.NextDouble() * 2 - 1);
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
            //First, we need to set the mutation rate and intensity, as they are necessary for every other step
            mutationrate = MutateValue(mutationrate);
            mutationintensity = MutateValue(mutationintensity);

            for (int InputType = 0; InputType < InputAverage.Count; InputType++)
            {
                for (int i = 0; i < InputCount; i++)
                {
                    InputAverage[InputType][i] = MutateValue(InputAverage[InputType][i]);

                    var dev = Math.Abs(MutateValue(InputDeviation[InputType][i]));
                    if (dev > 0)
                        InputDeviation[InputType][i] = dev;
                }

                for (int i = 0; i < OutputCount; i++)
                    OutputBias[InputType][i] = MutateValue(OutputBias[InputType][i]);
            }

            var Neurons = HiddenLayers.SelectMany(x => x).Concat(OutputLayer);
            foreach (var Neuron in Neurons)
            {
                Neuron.Mutate(mutationrate, mutationintensity);
            }

            if (Neurons.Where(x => x.IsMutated).Any())
                IsMutated = true;
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

            InputAverage = new();
            InputDeviation = new();
            OutputBias = new();
            var InputTypes = x.InputAverage.Count;

            for (int i = 0; i < InputTypes; i++)
            {
                var avg = new double[InputCount];
                var dev = new double[InputCount];                

                for (int j = 0; j < InputCount; j++)
                {
                    avg[j] = Breed(x.InputAverage[i][j], y.InputAverage[i][j]);
                    dev[j] = Breed(x.InputDeviation[i][j], y.InputDeviation[i][j]);
                }

                var outputbias = new double[OutputCount];
                for (int j = 0; j < OutputCount; j++)
                    outputbias[j] = Breed(x.OutputBias[i][j], y.OutputBias[i][j]);

                InputAverage.Add(avg);
                InputDeviation.Add(dev);
                OutputBias.Add(outputbias);
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
        }

        public static NeuralNetwork operator +(NeuralNetwork x, NeuralNetwork y)
        {
            return new NeuralNetwork(x, y);
        }

        public void IncreaseMutationRate()
        {
            var r = Random.Shared;

            //var p = r.NextDouble();
            //var rate = r.NextDouble() * (1 - mutationrate) + mutationrate;

            //mutationrate = rate * p + (1 - p) * mutationrate;

            double
                        min = mutationrate,
                        max = Math.Min(mutationrate + mutationintensity, 1),
                        range = max - min;

            mutationrate = r.NextDouble() * range + min;
        }
    }
}
