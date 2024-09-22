using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    [DataContract]
    public class Evolution : Predictable
    {
        const int NumberOfModels = 30, NumberOfTrees = 10;

        //All family trees, each with a large number of models
        [DataMember]
        List<PredictionModel>[] FamilyTrees = new List<PredictionModel>[NumberOfTrees];

        //The top performing model currently
        [DataMember]
        public PredictionModel[] TopModels = new PredictionModel[NumberOfTrees];

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

        /// <summary>
        /// Get all actions necessary to perform mutation on all PredictionModels
        /// </summary>
        public IEnumerable<Action> GetMutationActions()
        {
            return FamilyTrees.SelectMany(x => x).Select(x => x.GetMutationActions()).SelectMany(x => x);
        }

        /// <summary>
        /// Get the RatingActions for the blended predictions of all top models
        /// </summary>
        public IEnumerable<Func<ErrorContainer>> TopRatingsActions(IEnumerable<WeightedShow>? WeightedShows = null)
        {
            if (WeightedShows is null)
                WeightedShows = Network.GetWeightedShows();

            //Now, we need to select all possible ratings values, and group them with the weight for that year
            var WeightedRatings = WeightedShows.Select(x => x.Show.Ratings.Select(y => new StatsContainer(Convert.ToDouble(y), x.Weight))).SelectMany(x => x).Distinct();
            var WeightedViewers = WeightedShows.Select(x => x.Show.Viewers.Select(y => new StatsContainer(Convert.ToDouble(y), x.Weight))).SelectMany(x => x).Distinct();
            var AllEpisodeCounts = WeightedShows.Select(x => x.Show.Episodes).Distinct();

            //Now we need to generate the tasks needed to calculate the errors resulting from the RatingsModel not matching the correct values
            var RatingsActions_Middle = AllEpisodeCounts.Select(x => new IEnumerable<Func<ErrorContainer>>[]
            {
                WeightedRatings.Select(y => new Func<ErrorContainer>( () => GetWeightedRatingsError_Episodes(x, y.Value, y.Weight, 0))),
                WeightedViewers.Select(y => new Func<ErrorContainer>( () => GetWeightedRatingsError_Episodes(x, y.Value, y.Weight, 1)))
            }).SelectMany(x => x.SelectMany(y => y));

            //The next step is to look for predicted ratings values that fall outside of the expected minimum/maximum range
            //To do this, we need to analyze the range of values that appear in every show in the network

            var RatingRatio = Convert.ToDouble(WeightedShows.Select(x => x.Show.Ratings.Max() - x.Show.Ratings.Min()).Max());
            var ViewerRatio = Convert.ToDouble(WeightedShows.Select(x => x.Show.Viewers.Max() - x.Show.Viewers.Min()).Max());

            var RatingActions_Bounds = WeightedShows.Select(x => Enumerable.Range(1, x.Show.Ratings.Count).Select(y => new Func<ErrorContainer>(() => GetWeightedRatingsError_Show(x.Show, x.Weight, 0, y, RatingRatio)))).SelectMany(x => x);
            var ViewerActions_Bounds = WeightedShows.Select(x => Enumerable.Range(1, x.Show.Viewers.Count).Select(y => new Func<ErrorContainer>(() => GetWeightedRatingsError_Show(x.Show, x.Weight, 1, y, ViewerRatio)))).SelectMany(x => x);

            return RatingsActions_Middle.Concat(RatingActions_Bounds).Concat(ViewerActions_Bounds);
        }

        /// <summary>
        /// Returns an error and weight associated with a target ratings/weight pair
        /// </summary>
        /// <param name="Episodes">Number of episodes in a season</param>
        /// <param name="Rating">The target rating to test</param>
        /// <param name="Weight">The weight of the rating being tested</param>
        /// <param name="InputType">0 = Ratings, 1 = Viewers</param>
        /// <returns>A StatsContainer with the Error and Weight</returns>
        ErrorContainer GetWeightedRatingsError_Episodes(int Episodes, double Rating, double Weight, int InputType)
        {
            var output = GetThresholdByRating(Episodes, Rating, InputType);
            var Error = Math.Abs(output - Rating);

            return new ErrorContainer(this, Error, Weight);
        }

        /// <summary>
        /// Get the predicted threshold, given an expected Rating value, and number of episodes
        /// </summary>
        /// <param name="Episodes">Number of episodes to test</param>
        /// <param name="Rating">Expected rating</param>
        /// <param name="InputType">0 = Ratings, 1 = Viewers</param>
        /// <returns></returns>
        public double GetThresholdByRating(int Episodes, double Rating, int InputType)
        {
            return TopModels.Select(x => x.GetThresholdByRating(Episodes, Rating, InputType)).Average();
        }

        /// <summary>
        /// Checks if RatingsModel output is outside of Max/Min range compared to existing ratings
        /// </summary>
        /// <param name="Show">Show to be checked</param>
        /// <param name="Weight">Weight of the show</param>
        /// <param name="InputType">0 = Ratings, 1 = Viewers</param>
        /// <param name="EpisodeNumber">How many episodes to test in this iteration</param>
        /// <param name="MaxRange">The Maximum allowed range between the lowest or highest rating, and the output of the model</param>
        /// <returns>A StatsContainer containing the error and weight for this test</returns>
        ErrorContainer GetWeightedRatingsError_Show(Show Show, double Weight, int InputType, int EpisodeNumber, double MaxRange)
        {
            var output = TopModels.Select(x => x.GetPerformance(Show, InputType, EpisodeNumber)[0]).Average();

            var Ratings = InputType == 0 ? Show.Ratings.Take(EpisodeNumber) : Show.Viewers.Take(EpisodeNumber);

            var RatingsMax = Convert.ToDouble(Ratings.Max());
            var RatingsMin = Convert.ToDouble(Ratings.Min());

            var CalculatedRange = Math.Max(output - RatingsMin, RatingsMax - output);

            var CalculatedError = Math.Max(CalculatedRange - MaxRange, 0);

            return new ErrorContainer(this, CalculatedError, Weight);
        }

        /// <summary>
        /// Get PredictionActions for the blended output of the TopModels
        /// </summary>
        public IEnumerable<Func<ShowErrorContainer>> TopPredictionActions(IEnumerable<WeightedShow>? WeightedShows = null)
        {
            if (WeightedShows is null)
                WeightedShows = Network.GetWeightedShows();

            return WeightedShows.Where(x => x.Show.Renewed || x.Show.Canceled).Select(x => Enumerable.Range(1, Math.Max(x.Show.Ratings.Count, x.Show.Viewers.Count)).Select(y => new Func<ShowErrorContainer>(() => GetWeightedShowError(x.Show, x.Weight, y)))).SelectMany(x => x);
        }

        /// <summary>
        /// Checks if the show was predicted correctly, and if not, returns an error
        /// </summary>
        /// <param name="Show">Show to be tested</param>
        /// <param name="Weight">The weight of the show</param>
        /// <param name="EpisodeNumber">The number of episodes to test</param>
        /// <returns></returns>
        ShowErrorContainer GetWeightedShowError(Show Show, double Weight, int EpisodeNumber)
        {
            //Retrieve ratings and viewer performance and thresholds
            var Outputs = TopModels.Select(x => x.GetOutputs(Show, EpisodeNumber));
            double Error = 0,
                RatingPerformance = Outputs.Average(x => x[0]),
                ViewerPerformance = Outputs.Average(x => x[1]),
                RatingThreshold = Outputs.Average(x => x[2]),
                ViewerThreshold = Outputs.Average(x => x[3]),
                Blend = Outputs.Average(x => x[4]),
                BlendedPerformance = RatingPerformance * Blend + ViewerPerformance * (1 - Blend),
                BlendedThreshold = RatingThreshold * Blend + ViewerThreshold * (1 - Blend),
                RatingDistance = Math.Abs(RatingPerformance - RatingThreshold),
                ViewerDistance = Math.Abs(ViewerPerformance - ViewerThreshold),
                BlendedDistance = Math.Abs(BlendedPerformance - BlendedThreshold),
                CurrentPosition = EpisodeNumber / (double)Show.Episodes;

            //First, check if the prediction was accurate

            bool
                PredictionCorrect = Show.Renewed && BlendedPerformance > BlendedThreshold || Show.Canceled && BlendedPerformance < BlendedThreshold,
                RatingCorrect = Show.Renewed && RatingPerformance > RatingThreshold || Show.Canceled && RatingPerformance < RatingThreshold,
                ViewerCorrect = Show.Canceled && ViewerPerformance > ViewerThreshold || Show.Canceled && ViewerPerformance < ViewerThreshold;

            var EpisodePair = new EpisodePair(EpisodeNumber, Show.Episodes);

            //Next, determine Error.
            if (Show.Renewed && Show.Canceled && MarginOfError.ContainsKey(EpisodePair))
            {
                //If the show is Renewed && Canceled(renewed for final season) and if there is a MarginOfError set
                //then a show in this position should ideally have a renewal probability of 55%
                //Error will be the distance between the show's performance and a target performance level that would achieve 55% odds

                double
                    Margin = MarginOfError[EpisodePair],
                    RMargin = RatingMargin.ContainsKey(EpisodePair) ? RatingMargin[EpisodePair] : Margin,
                    VMargin = ViewerMargin.ContainsKey(EpisodePair) ? ViewerMargin[EpisodePair] : Margin,
                    TargetRating = RatingThreshold + RMargin * 0.125661346855074,
                    TargetViewer = ViewerThreshold + VMargin * 0.125661346855074,
                    TargetBlended = BlendedPerformance + Margin * 0.125661346855074;

                Error += Math.Abs(RatingPerformance - TargetRating) * 0.5;
                Error += Math.Abs(ViewerPerformance - TargetViewer) * 0.5;
                Error += Math.Abs(BlendedPerformance - TargetBlended);
            }
            else
            {
                //Otherwise, Error should be 0 if the show was predicted correctly
                //If not, it should be the difference between the renewalthreshold and performance
                //Do this for ratings, viewers, and blended

                if (!RatingCorrect)
                    Error += RatingDistance * 0.5;

                if (!ViewerCorrect)
                    Error += ViewerDistance * 0.5;

                if (!PredictionCorrect)
                    Error += BlendedDistance;
            }

            return new ShowErrorContainer(this, PredictionCorrect, Error, Weight, CurrentPosition, RatingCorrect, ViewerCorrect, RatingDistance, ViewerDistance, BlendedDistance);
        }

        /// <summary>
        /// Manually test and set the accuracy of the Evolution Model
        /// </summary>
        public void TestAccuracy()
        {
            //First, calculate the error of the RatingsModel
            //The first error check tests the RatingsModel by feeding it the same rating number for every episode for every possible ratings count
            //If the RatingsModel is accurate, the output of the model should be identical to the numbers fed into the model
            //This basically ensures that the average prediction of RatingsModel is in the correct range

            //We need to first generate a weighted selection of the shows
            var WeightedShows = Network.GetWeightedShows();

            //Next, we need to check for errors in the RatingsModel
            var RatingsActions = TopRatingsActions(WeightedShows);

            //Finally, we need to check for whether the RenewalModel predicts renewal thresholds correctly for Renewed and Canceled shows
            var PredictionActions = TopPredictionActions(WeightedShows);

            //Now that we have the actions necessary, we need to calculate prediction accuracy:
            var EpisodePairs = Network.GetEpisodePairs();
            var RatingsResults = RatingsActions.AsParallel().Select(x => x());
            var PredictionResults = PredictionActions.AsParallel().Select(x => x());

            SetAccuracyAndError(RatingsResults, PredictionResults, EpisodePairs);
        }

        /// <summary>
        /// Get the predicted Odds for a given show in its current state
        /// </summary>
        /// <param name="Show">Show to be tested</param>
        /// <returns>Percentage odds of renewal</returns>
        public double GetOdds(Show Show)
        {
            var AllOutputs = TopModels.Select(x => x.GetPerformanceAndThreshold(Show));
            var outputs = new double[]
            {
                AllOutputs.Average(x => x[0]),
                AllOutputs.Average(x => x[1]),
                AllOutputs.Average(x => x[2]),
                AllOutputs.Average(x => x[3]),
                AllOutputs.Average(x => x[4])
            };

            var Episodes = new EpisodePair(Math.Min(Show.Ratings.Count, Show.Viewers.Count), Show.Episodes);

            return GetOdds(outputs, Episodes);
        }
    }
}
