using System;

namespace TV_Ratings_Predictions
{
    [Serializable]
    public class Neuron
    {
        double bias, outputbias;
        double[] weights;
        int inputSize;
        public bool isMutated;

        public Neuron(int inputs)
        {
            isMutated = false;

            Random r = new Random();
            bias = r.NextDouble() * 2 - 1;
            outputbias = 0;

            weights = new double[inputs];

            for (int i = 0; i < inputs; i++)
                weights[i] = r.NextDouble() * 2 - 1;

            inputSize = inputs;
        }

        public Neuron(int inputs, double midpoint, bool skip)
        {
            isMutated = false;

            midpoint = midpoint * 2 - 1;

            bias = skip ? 0 : ReverseActivation(midpoint);
            outputbias = 0;

            weights = new double[inputs];

            for (int i = 0; i < inputs; i++)
                weights[i] = 0;

            inputSize = inputs;
        }

        public Neuron(Neuron n)
        {
            isMutated = false;

            bias = n.bias;
            outputbias = n.outputbias;
            inputSize = n.inputSize;

            weights = new double[inputSize];

            for (int i = 0; i < inputSize; i++)
                weights[i] = n.weights[i];
        }

        private double Breed(double x, double y, Random r)
        {
            //var r = new Random();
            var p = r.NextDouble();

            return (x * p) + (y * (1 - p));

            //return p > 0.5 ? x : y;
        }

        public Neuron(Neuron x, Neuron y, Random r)
        {
            //var r = new Random();
            isMutated = false;
            bias = Breed(x.bias, y.bias, r);
            outputbias = Breed(x.outputbias, y.outputbias, r);

            inputSize = x.inputSize;

            weights = new double[inputSize];

            for (int i = 0; i < inputSize; i++)
                weights[i] = Breed(x.weights[i], y.weights[i], r);
        }

        public double GetOutput(double[] inputs, bool output = false)
        {
            double total = 0;

            for (int i = 0; i < inputSize; i++)
                total += inputs[i] * weights[i];

            total += bias;

            return output ? Activation(total) : Activation(total) + outputbias;
        }

        double Activation(double d)
        {
            //if (outputNeuron)
            return (2 / (1 + Math.Exp(-1 * d))) - 1;
            //else
            //return (d > 0) ? d : 0;
            //return Math.Log(Math.Exp(d-3) + 1);
            //return (2 / (1 + Math.Exp(-1 * d))) - 1;
        }

        double ReverseActivation(double d)
        {
            return Math.Log((-d - 1) / (d - 1));
        }

        public void Mutate(double mutationrate, double neuralintensity, double mutationintensity, Random r)
        {

            for (int i = 0; i < inputSize; i++)
            {
                if (r.NextDouble() < mutationrate)
                {
                    weights[i] += neuralintensity * (r.NextDouble() * 2 - 1);
                    isMutated = true;
                }

            }

            if (r.NextDouble() < mutationrate)
            {
                bias += neuralintensity * (r.NextDouble() * 2 - 1);
                isMutated = true;
            }

            if (r.NextDouble() < mutationrate)
            {
                var d = (outputbias + 1) / 2;

                double p = r.NextDouble();

                double low = d * (1 - mutationintensity), high = 1 - (1 - d) * (1 - mutationintensity);

                var tempBias = p * (high - low) + low;
                outputbias = tempBias * 2 - 1;
                isMutated = true;
            }
        }
    }
}
