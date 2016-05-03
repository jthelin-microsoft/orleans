using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orleans.Runtime.Placement
{
    /// <summary>
    /// PinnedGrainPlacementDirector will always place the target grain on the local silo.
    /// When activation is requested (OnSelectActivation), it always attempt to place on the local silo.
    /// It checks with the Distributed Directory, and if an existing activiation already exists on another silo,
    /// and in that case will refuse to activate the grain here.
    /// </summary>
    internal class PinnedGrainPlacementDirector : PlacementDirector
    {
        private static readonly Logger logger = LogManager.GetLogger("PinnedGrainPlacementDirector");

        internal override async Task<PlacementResult> OnSelectActivation(
            PlacementStrategy strategy,
            GrainId grain,
            IPlacementContext context)
        {
            bool ok = await CheckNotAttemptingMoveToDifferentSilo(grain, context);
            if (!ok)
            {
                return null;
            }
            return await OnAddActivation(strategy, grain, context);
        }

        internal override Task<PlacementResult> OnAddActivation(
            PlacementStrategy strategy,
            GrainId grain,
            IPlacementContext context)
        {
            SiloAddress localSilo = context.LocalSilo;
            string grainType = context.GetGrainTypeName(grain);
            return Task.FromResult(
                PlacementResult.SpecifyCreation(localSilo, strategy, grainType));
        }

        private async Task<bool> CheckNotAttemptingMoveToDifferentSilo(
            GrainId grain,
            IPlacementContext context)
        {
            SiloAddress localSilo = context.LocalSilo;
            List<ActivationAddress> places = (await context.Lookup(grain)).Addresses;
            bool ok;
            if (places.Count <= 0)
            {
                // No other activations for this grain
                ok = true;
            }
            else if (places.Count == 1 && places[0].Silo.Equals(localSilo))
            {
                // Activation already exists on this silo
                ok = true;
            }
            else
            {
                // Found existing activation of this grain on a different silo
                foreach (ActivationAddress place in places)
                {
                    logger.Warn(ErrorCode.Placement_PinndeGrain_MovedSilo,
                        "Found existing pinned grain {0} on silo {1} with address {2}",
                        grain, place.Silo, place.Activation);
                }
                // So fail placement on this silo.
                ok = false;
            }
            if (logger.IsVerbose2 && ok)
            {
                logger.Verbose2("Placing pinned grain {0} on silo {1}",
                    grain, localSilo);
            }
            return ok;
        }
    }
}
