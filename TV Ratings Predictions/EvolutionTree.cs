using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TV_Ratings_Predictions
{
    [Serializable]
    public class EvolutionTree
    {
        public List<NeuralPredictionModel> Primary, Randomized;
        public long Generations, CleanGenerations, RandomGenerations;

        [NonSerialized]
        public Network network;

        [NonSerialized]
        bool IncreasePrimary, IncreaseRandom, Mutations, RandomMutations;

        [NonSerialized]
        public long ticks;

        //Primary:
        //First 4 entries will be the top 4 best performing models out of all 3 branches
        //The rest of the entries will be mutations based on those 4 best.

        //CleanSlate:
        //This branch, when created, will set NetworkAverageIndex to GetAverageThreshold() for the top model in the Primary Branch
        //Every TrueIndex and FalseIndex wil be set to be the same as NetworkAverageIndex
        //EpisodeThreshold will be set to the EpisodeThreshold in the Primary Branch
        //All weight values will be set to 0
        //These will define the primary parent, and the remaining children will be mutations based on that
        //On future generations, the top 4 models from this branch will be selected as the parents (first 4 indexed)
        //children will be mutated based on those 4 parents

        //Randomized:
        //This branch will start out with all 30 entries being totally randomized.
        //On the next generation, the top 4 models from this branch will be selected as parents (first 4 indexes)
        //children will be mutated from these 4 parents

        //All 3 branche, when first created, will start out following the same behavior as the Randomized Branch
        //All 3 branches will continue to propogate their own evolution every generation
        //If any model from the CleanSlate or Randomized branches is selected to become a parent in the primary branch:
        //then that branch will start over, following the rules outlined above


        public EvolutionTree(Network n)
        {
            network = n;

            Primary = new List<NeuralPredictionModel>();
            Randomized = new List<NeuralPredictionModel>();

            for (int i = 0; i < 30; i++)
            {
                Primary.Add(new NeuralPredictionModel(n));
                Randomized.Add(new NeuralPredictionModel(n));
            }

            Generations = 1;
            CleanGenerations = 1;
            RandomGenerations = 1;
        }

        public EvolutionTree(Network n, double midpoint)
        {
            network = n;

            Primary = new List<NeuralPredictionModel>(); ;
            Randomized = new List<NeuralPredictionModel>();

            for (int i = 0; i < 30; i++)
            {
                Primary.Add(new NeuralPredictionModel(n, midpoint));
                Randomized.Add(new NeuralPredictionModel(n));
            }

            Generations = 1;
            CleanGenerations = 1;
            RandomGenerations = 1;
        }

        public EvolutionTree(EvolutionTree e, Network n)
        {
            network = n;

            Primary = new List<NeuralPredictionModel>();
            foreach (NeuralPredictionModel m in e.Primary.ToArray())
                Primary.Add(new NeuralPredictionModel(m));
            Randomized = new List<NeuralPredictionModel>();
            foreach (NeuralPredictionModel m in e.Randomized.ToArray())
                Randomized.Add(new NeuralPredictionModel(m));

            Generations = e.Generations;
            CleanGenerations = e.CleanGenerations;
            RandomGenerations = e.RandomGenerations;
        }

        public void NextGeneration()
        {
            var r = new Random();

            //Update shows list
            for (int i = 0; i < 30; i++)
            {
                Primary[i].shows = network.shows;
                //CleanSlate[i].shows = network.shows;
                Randomized[i].shows = network.shows;
            }

            //Sort all 3 Branches from Highest to lowest

            for (int i = 0; i < 30; i++)
            {
                if (i == 2 || i == 3)
                {
                    Primary[i].SetElite();
                    Randomized[i].SetElite();
                }
                else
                {
                    Primary[i].TestAccuracy(true);
                    Randomized[i].TestAccuracy(true);
                }
            }

            Primary.Sort();
            Randomized.Sort();

            for (int i = 29; i > 0; i--)
            {
                if (Primary[i] == Primary[i - 1])
                    Primary[i].SetElite();
                if (Randomized[i] == Randomized[i - 1])
                    Randomized[i].SetElite();
            }

            Primary.Sort();
            Randomized.Sort();

            if (IncreasePrimary)
                Primary[r.Next(4)].IncreaseMutationRate();
            if (IncreaseRandom)
                Randomized[r.Next(4)].IncreaseMutationRate();

            IncreasePrimary = false;
            IncreaseRandom = false;

            //CHeck if any models in CleanSlate or Randomized beat any of the top 4 in Primary
            //If so, add them to Primary
            bool randomUpdate = false;

            bool finished = false;



            //Randomized
            for (int i = 0; i < 4 && !finished; i++)
            {
                if (Randomized[i] > Primary[3])
                {

                    if (Randomized[i] > Primary[0])
                        Primary.Insert(0, new NeuralPredictionModel(Randomized[i]));
                    else if (Randomized[i] > Primary[1])
                        Primary.Insert(1, new NeuralPredictionModel(Randomized[i]));
                    else if (Randomized[i] > Primary[2])
                        Primary.Insert(2, new NeuralPredictionModel(Randomized[i]));
                    else
                        Primary.Insert(3, new NeuralPredictionModel(Randomized[i]));

                    Primary.RemoveAt(30);
                }
                else
                    finished = true;

            }



            //If model has improved, replace model in network with current best model
            if (Primary[0] > network.model)
            {
                network.ModelUpdate(Primary[0]);
                network.refreshEvolution = false;
                network.refreshPrediction = true;
                randomUpdate = true;
            }
            else if (network.refreshEvolution)
            {
                network.refreshEvolution = false;
                //cleanUpdate = true;
                randomUpdate = true;
            }

            Mutations = false;
            RandomMutations = false;


            //If models were chosen from Randomized, then reset that branch based on the above rules
            //If not, perform normal evolution rules            
            if (randomUpdate)
            {
                //for (int i = 0; i < 30; i++)
                Parallel.For(0, 30, i => Randomized[i] = new NeuralPredictionModel(network));

                RandomGenerations = 1;
            }
            else
            {
                Parallel.For(4, 30, i =>
                {
                    int Parent1 = r.Next(4), Parent2 = r.Next(4);

                    Randomized[i] = Randomized[Parent1] + Randomized[Parent2];

                    if (Randomized[i].isMutated) RandomMutations = true;
                });

                if (!RandomMutations)
                    IncreaseRandom = true;

                RandomGenerations++;
            }

            //Evolve Primary Branch
            Parallel.For(4, 30, i =>
            {
                int Parent1 = r.Next(4), Parent2 = r.Next(4);

                Primary[i] = Primary[Parent1] + Primary[Parent2];

                if (Primary[i].isMutated) Mutations = true;
            });

            if (!Mutations)
                IncreasePrimary = true;

            Generations++;
        }
    }
}
