using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Orleans.Placement;
using Orleans.Providers;
using Orleans.Runtime;
using UnitTests.GrainInterfaces;

namespace UnitTests.Grains
{
    public class PartitionManagerConfig
    {
        public IDictionary<Guid, IPartitionGrain> Partitions { get; set; }
        public IDictionary<Guid, PartitionInfo> PartitionInfos { get; set; }
    }

    /// <summary>
    /// Grain implemention for Partition Grains.
    /// One partition grain is created per silo.
    /// </summary>
    [PinnedGrain]
    public class PartitionGrain : Grain, IPartitionGrain
    {
        PartitionInfo PartitionConfig { get; set; }

        private Logger logger;
        private Guid partitionId;
        private string siloId;
        private IPartitionGrain me;
        private IPartitionManager partitionManager;
        private bool registered;

        public override async Task OnActivateAsync()
        {
            logger = GetLogger(GetType().Name + "-" + this.GetPrimaryKey());
            logger.Info("Activate");

            partitionId = this.GetPrimaryKey();
            siloId = RuntimeIdentity;
            me = this.AsReference<IPartitionGrain>();
            partitionManager = GrainFactory.GetGrain<IPartitionManager>(0);

            if (PartitionConfig == null)
            {
                PartitionConfig = new PartitionInfo
                {
                    PartitionId = partitionId,
                    SiloId = siloId
                };
            }
            else
            {
                await RegisterWithPartitionManager();
            }
        }

        public override async Task OnDeactivateAsync()
        {
            await Stop();
        }

        public async Task<PartitionInfo> Start()
        {
            await RegisterWithPartitionManager();

            DelayDeactivation(TimeSpan.MaxValue); // Ensure we do not get paged out

            logger.Info("Partition grain {0} is now started on silo {1}", partitionId, siloId);
            return PartitionConfig;
        }

        public async Task Stop()
        {
            await UnregisterWithPartitionManager();

            DeactivateOnIdle(); // Mark ourselves for deactivation

            logger.Info("Partition grain {0} is now stopped on silo {1}", partitionId, siloId);
        }

        public Task<PartitionInfo> GetPartitionInfo()
        {
            if (logger.IsVerbose)
            {
                logger.Verbose("GetPartitionInfo");
            }
            return Task.FromResult(PartitionConfig);
        }

        private async Task RegisterWithPartitionManager()
        {
            if (!registered)
            {
                logger.Info("Registering partition grain {0} on silo {1} with partition manager", partitionId, siloId);
                await partitionManager.RegisterPartition(PartitionConfig, me);
                registered = true;
                logger.Info("Partition grain {0} on silo {1} has been registered with partition manager.", partitionId, siloId);
            }
            else if (logger.IsVerbose)
            {
                logger.Verbose("Partition grain {0} is already registered", partitionId);
            }
        }

        private async Task UnregisterWithPartitionManager()
        {
            if (registered)
            {
                logger.Info("Unregistering partition grain {0} from partition manager.", partitionId);
                await partitionManager.RemovePartition(PartitionConfig, me);
                registered = false;
                logger.Info("Partition grain {0} on silo {1} has been unregistered from partition manager.", partitionId, siloId);
            }
            else if (logger.IsVerbose)
            {
                logger.Verbose("Partition grain {0} is already unregistered", partitionId);
            }
        }
    }

    /// <summary>
    /// Grain implemention for Partition Manager Grain.
    /// One partition manager grain is created per cluster.
    /// By convention, only Id=0 will be used.
    /// </summary>
    [StorageProvider(ProviderName = "PartitionManagerStore")]
    [Reentrant]
    public class PartitionManagerGrain : Grain<PartitionManagerConfig>, IPartitionManager
    {
        private Logger logger;

        public override Task OnActivateAsync()
        {
            logger = GetLogger(GetType().Name + this.GetPrimaryKey());
            logger.Info("Activate");

            if (State.PartitionInfos == null)
            {
                State.PartitionInfos = new Dictionary<Guid, PartitionInfo>();
            }
            if (State.Partitions == null)
            {
                State.Partitions = new Dictionary<Guid, IPartitionGrain>();
            }
            // Don't need to write default init data back to store
            return TaskDone.Done;
        }

        public Task<IList<IPartitionGrain>> GetPartitions()
        {
            IList<IPartitionGrain> partitions = State.Partitions.Values.ToList();
            return Task.FromResult(partitions);
        }

        public Task<IList<PartitionInfo>> GetPartitionInfos()
        {
            IList<PartitionInfo> partitionInfos = State.PartitionInfos.Values.ToList();
            return Task.FromResult(partitionInfos);
        }

        public async Task RegisterPartition(PartitionInfo partitionInfo, IPartitionGrain partitionGrain)
        {
            logger.Info("RegisterPartition {0} on silo {1}", partitionInfo.PartitionId, partitionInfo.SiloId);
            Guid partitionId = partitionInfo.PartitionId;
            State.Partitions[partitionId] = partitionGrain;
            State.PartitionInfos[partitionId] = partitionInfo;
            await WriteStateAsync();
        }

        public async Task RemovePartition(PartitionInfo partitionInfo, IPartitionGrain partitionGrain)
        {
            logger.Info("RemovePartition {0} on silo {1}", partitionInfo.PartitionId, partitionInfo.SiloId);
            Guid partitionId = partitionInfo.PartitionId;
            State.Partitions.Remove(partitionId);
            State.PartitionInfos.Remove(partitionId);
            await WriteStateAsync();
        }

        public async Task Broadcast(Func<IPartitionGrain, Task> asyncAction)
        {
            IDictionary<Guid, IPartitionGrain> partitions = State.Partitions;
            logger.Info("Broadcast: Send to {0} partitions", partitions.Count);
            IList<Task> tasks = new List<Task>();
            foreach (KeyValuePair<Guid, IPartitionGrain> p in partitions)
            {
                Guid id = p.Key;
                IPartitionGrain grain = p.Value;
                logger.Info("Broadcast: Sending message to partition {0} on silo {1}", id, State.PartitionInfos[id].SiloId);
                tasks.Add(asyncAction(grain));
            }
            // Await here so that tail async is contained and any errors show this method in stack trace.
            logger.Info("Broadcast: Awaiting ack from {0} partitions", tasks.Count);
            await Task.WhenAll(tasks);
        }

    }

    /// <summary>
    /// App startup shim to bootstrap the partition grain in each silo.
    /// </summary>
    public class PartitionStartup : IBootstrapProvider
    {
        public string Name { get; private set; }
        public Guid PartitionId { get; private set; }
        public string HostId { get; private set; }

        private IGrainFactory GrainFactory { get; set; }
        private Logger logger;
        /// <summary> The provider runtime for this silo. </summary>
        private IProviderRuntime providerRuntime;
        /// <summary> Our partition grain for this silo. </summary>
        private IPartitionGrain partitionGrain;

        /// <summary>
        /// Bootstrap the partition grain in this silo.
        /// </summary>
        public async Task Init(string name, IProviderRuntime runtime, IProviderConfiguration config)
        {
            logger = runtime.GetLogger("Startup:" + name);
            GrainFactory = runtime.GrainFactory;
            providerRuntime = runtime;

            HostId = runtime.SiloIdentity;

            // Use different partition id value for each silo instance.
            PartitionId = Guid.NewGuid();

            Name = "Partition-" + PartitionId;

            await CreatePartition(PartitionId, HostId);
        }

        public async Task Close()
        {
            await RemovePartition(PartitionId, HostId);
        }

        /// <summary>
        /// Create and start the partition instance for this silo.
        /// </summary>
        /// <param name="partitionId">Id value for this partition.</param>
        /// <param name="hostId">Id value for this host.</param>
        /// <returns>Returns <c>true</c> if the partition grain instance on this silo was created successfully.</returns>
        private async Task<bool> CreatePartition(Guid partitionId, string hostId)
        {
            Grain pinnedGrain = providerRuntime.ServiceProvider.GetService(typeof(PartitionGrain)) as Grain;

            logger.Info("Creating partition grain id {0} on silo id {1}", partitionId, hostId);
            partitionGrain = GrainFactory.GetGrain<IPartitionGrain>(partitionId);
            logger.Info("Partition grain {0} has been created on silo id {1}", partitionId, hostId);

            logger.Info("Starting partition grain id {0} on silo id {1}", partitionId, hostId);
            PartitionInfo partitionInfo = await partitionGrain.Start();
            logger.Info("Partition grain {0} has been started on silo id {1} using partition info {2}",
                partitionId, hostId, partitionInfo);
            return true;
        }

        /// <summary>
        /// Remove the partition instance for this silo.
        /// </summary>
        /// <param name="partitionId">Id value for this partition.</param>
        /// <param name="hostId">Id value for this host.</param>
        /// <returns>Grain reference to the partion grain instance that was created.</returns>
        private async Task RemovePartition(Guid partitionId, string hostId)
        {
            logger.Info("Stopping partition grain id {0} on silo id {1}", PartitionId, HostId);
            await partitionGrain.Stop();
            logger.Info("Partition grain {0} has been stopped on silo id {1}", partitionId, hostId);
        }
    }
}
