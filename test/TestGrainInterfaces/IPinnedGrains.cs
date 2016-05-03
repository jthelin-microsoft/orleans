using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Orleans;

namespace UnitTests.GrainInterfaces
{
    /// <summary>
    /// Grain interface for PerSilo Example Grain
    /// </summary>
    public interface IPartitionGrain : IGrainWithGuidKey
    {
        /// <summary> Start this partiton grain. </summary>
        Task<PartitionInfo> Start();

        /// <summary> Stops this partiton grain. </summary>
        Task Stop();

        /// <summary> Return the <c>PartitionInfo</c> for this partition. </summary>
        Task<PartitionInfo> GetPartitionInfo();
    }

    /// <summary>
    /// Manager for Partition Grains.
    /// By convention, only Id=0 will be used, to ensure only single copy exists within the cluster.
    /// </summary>
    public interface IPartitionManager : IGrainWithIntegerKey
    {
        Task<IList<IPartitionGrain>> GetPartitions();
        Task<IList<PartitionInfo>> GetPartitionInfos();
        Task RegisterPartition(PartitionInfo partitonInfo, IPartitionGrain partitionGrain);
        Task RemovePartition(PartitionInfo partitonInfo, IPartitionGrain partitionGrain);
        Task Broadcast(Func<IPartitionGrain, Task> asyncAction);
    }

    [Serializable]
    [DebuggerDisplay("PartitionInfo:{PartitionId}")]
    public class PartitionInfo
    {
        public Guid PartitionId { get; set; }
        public string SiloId { get; set; }

        public override string ToString()
        {
            return string.Format("PartitionInfo:PartitionId={0},SiloId={1}", PartitionId, SiloId);
        }
    }
}
