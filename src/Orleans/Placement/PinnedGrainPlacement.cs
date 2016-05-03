using System;

namespace Orleans.Placement
{
    [Serializable]
    internal class PinnedGrainPlacement : Runtime.PlacementStrategy
    {
        internal static PinnedGrainPlacement Singleton { get; private set; }

        internal static void InitializeClass()
        {
            Singleton = new PinnedGrainPlacement();
        }

        private PinnedGrainPlacement()
        { }

        public override bool Equals(object obj)
        {
            return obj is PinnedGrainPlacement;
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }
    }
}
