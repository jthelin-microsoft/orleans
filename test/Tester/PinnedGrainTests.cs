using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Orleans.Runtime.Configuration;
using Orleans.TestingHost;
using UnitTests.GrainInterfaces;
using UnitTests.Grains;
using UnitTests.Tester;
using Xunit;
using Xunit.Abstractions;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tester
{
    public class PinnedGrainTestsFixture : BaseTestClusterFixture
    {
        internal const int NumSilos = 2;

        protected override TestCluster CreateTestCluster()
        {
            var options = new TestClusterOptions(NumSilos);

            // Note: Using MemoryStore for testing only.
            options.ClusterConfiguration.AddMemoryStorageProvider("PartitionManagerStore");
            options.ClusterConfiguration.Globals.RegisterBootstrapProvider(
                providerTypeFullName: typeof(PartitionStartup).FullName,
                providerName: "PartitionGrainStartup");
            options.ClusterConfiguration.Defaults.PropagateActivityId = true;

            options.ClientConfiguration.PropagateActivityId = true;

            return new TestCluster(options);
        }
    }

    public class PinnedGrainTests : OrleansTestingBase, IClassFixture<PinnedGrainTestsFixture>
    {
        private int NumSilos { get; }
        private readonly IManagementGrain mgmtGrain;
        private readonly ITestOutputHelper output;

        public PinnedGrainTests(ITestOutputHelper output, PinnedGrainTestsFixture fixture)
        {
            this.output = output;
            TestCluster cluster = fixture.HostedCluster;
            NumSilos = cluster.GetActiveSilos().Count();
            mgmtGrain = GrainFactory.GetGrain<IManagementGrain>(RuntimeInterfaceConstants.SYSTEM_MANAGEMENT_ID);
            TestSilosStarted(PinnedGrainTestsFixture.NumSilos);
        }

        [Fact, TestCategory("BVT"), TestCategory("PinnedGrains"), TestCategory("Placement")]
        public async Task Init_CheckEnv_PinnedGrainTests()
        {
            // This tests just bootstraps the 2 default test silos, and checks that partition grains were created on each.
            Assert.AreEqual(PinnedGrainTestsFixture.NumSilos, NumSilos, "Should have expected number of silos");
            
            IPartitionManager partitionManager = GrainFactory.GetGrain<IPartitionManager>(0);
            IList<PartitionInfo> partitionInfos = await partitionManager.GetPartitionInfos();

            Assert.AreEqual(PinnedGrainTestsFixture.NumSilos, partitionInfos.Count, "Should have results for expected number of silos silos");
            Assert.AreEqual(NumSilos, partitionInfos.Count, " PartitionInfo list should return {0} values.", NumSilos);
            Assert.AreNotEqual(partitionInfos[0].PartitionId, partitionInfos[1].PartitionId, "PartitionIds should be different.");
            await CountActivations("Initial");
        }

        [Fact, TestCategory("BVT"), TestCategory("PinnedGrains"), TestCategory("Placement")]
        public async Task SendMsg_Client_PinnedGrains()
        {
            IPartitionManager partitionManager = GrainFactory.GetGrain<IPartitionManager>(0);
            IList<PartitionInfo> partitionInfosList1 = await partitionManager.GetPartitionInfos();

            Assert.AreEqual(NumSilos, partitionInfosList1.Count, "Initial: PartitionInfo list should return {0} values.", NumSilos);
            Assert.AreNotEqual(partitionInfosList1[0].PartitionId, partitionInfosList1[1].PartitionId, "Initial: PartitionIds should be different.");
            await CountActivations("Initial");

            foreach (var partition in partitionInfosList1)
            {
                Guid partitionId = partition.PartitionId;
                IPartitionGrain grain = GrainFactory.GetGrain<IPartitionGrain>(partitionId);
                PartitionInfo pi = await grain.GetPartitionInfo();
                output.WriteLine(pi);
            }

            await CountActivations("After Send");

            IList<PartitionInfo> partitionInfosList2 = await partitionManager.GetPartitionInfos();

            Assert.AreEqual(NumSilos, partitionInfosList2.Count, "After Send: PartitionInfo list should return {0} values.", NumSilos);
            foreach (int i in Enumerable.Range(0, PinnedGrainTestsFixture.NumSilos))
            {
                Assert.AreEqual(partitionInfosList1[i].PartitionId, partitionInfosList2[i].PartitionId, "After Send: Same PartitionIds [{0}]", i);
            }
            Assert.AreNotEqual(partitionInfosList2[0].PartitionId, partitionInfosList2[1].PartitionId, "After Send: PartitionIds should be different.");
            await CountActivations("After checks");
        }

        [Fact, TestCategory("BVT"), TestCategory("PinnedGrains"), TestCategory("Placement")]
        public async Task SendMsg_Broadcast_PinnedGrains()
        {
            IPartitionManager partitionManager = GrainFactory.GetGrain<IPartitionManager>(0);
            IList<PartitionInfo> partitionInfosList1 = await partitionManager.GetPartitionInfos();

            Assert.AreEqual(NumSilos, partitionInfosList1.Count, "Initial: PartitionInfo list should return {0} values.", NumSilos);
            Assert.AreNotEqual(partitionInfosList1[0].PartitionId, partitionInfosList1[1].PartitionId, "Initial: PartitionIds should be different.");
            await CountActivations("Initial");

            await partitionManager.Broadcast(p => p.GetPartitionInfo());

            await CountActivations("After Broadcast");

            IList<PartitionInfo> partitionInfosList2 = await partitionManager.GetPartitionInfos();

            Assert.AreEqual(NumSilos, partitionInfosList2.Count, "After Send: PartitionInfo list should return {0} values.", NumSilos);
            foreach (int i in Enumerable.Range(0, PinnedGrainTestsFixture.NumSilos))
            {
                Assert.AreEqual(partitionInfosList1[i].PartitionId, partitionInfosList2[i].PartitionId, "After Send: Same PartitionIds [{0}]", i);
            }
            Assert.AreNotEqual(partitionInfosList2[0].PartitionId, partitionInfosList2[1].PartitionId, "After Send: PartitionIds should be different.");

            await CountActivations("After checks");
        }

        private async Task CountActivations(string when)
        {
            string grainType = typeof(PartitionGrain).FullName;
            int siloCount = PinnedGrainTestsFixture.NumSilos;
            int expectedGrainsPerSilo = 1;
            var grainStats = (await mgmtGrain.GetSimpleGrainStatistics()).ToList();
            output.WriteLine("Got All Grain Stats: " + string.Join(" ", grainStats));
            var partitionGrains = grainStats.Where(gs => gs.GrainType == grainType).ToList();
            output.WriteLine("Got PartitionGrain Stats: " + string.Join(" ", partitionGrains));
            var wrongSilos = partitionGrains.Where(gs => gs.ActivationCount != expectedGrainsPerSilo).ToList();
            Assert.AreEqual(0, wrongSilos.Count, when + ": Silos with wrong number of {0} grains: {1}",
                grainType, string.Join(" ", wrongSilos));
            int count = partitionGrains.Select(gs => gs.ActivationCount).Sum();
            Assert.AreEqual(siloCount, count, when + ": Total count of {0} grains should be {1}. Got: {2}",
                grainType, siloCount, string.Join(" ", grainStats));
        }
    }

    public class PinnedGrainFailureTests : OrleansTestingBase, IDisposable
    {
        private int NumSilos { get; }
        private readonly TestCluster cluster;
        private readonly PinnedGrainTestsFixture fixture;

        private readonly ITestOutputHelper output;

        public PinnedGrainFailureTests(ITestOutputHelper output)
        {
            this.output = output;

            fixture = new PinnedGrainTestsFixture();
            cluster = fixture.HostedCluster;

            NumSilos = cluster.GetActiveSilos().Count();
            TestSilosStarted(PinnedGrainTestsFixture.NumSilos);

            NodeConfiguration cfg = cluster.Primary.NodeConfiguration;
            output.WriteLine(
                "Primary silo: Address = {0} Proxy gateway: {1}", 
                cfg.Endpoint, cfg.ProxyGatewayEndpoint);
            foreach (int i in Enumerable.Range(0, cluster.SecondarySilos.Count))
            {
                cfg = cluster.SecondarySilos[i].NodeConfiguration;
                output.WriteLine(
                    "Secondary silo {0} : Address = {1} Proxy gateway: {2}",
                    i + 1, cfg.Endpoint, cfg.ProxyGatewayEndpoint);
            }

            //output.WriteLine("Client configuration = \n{0}", cluster.ClientConfiguration);
            //output.WriteLine("Primary Silo configuration = \n{0}", cluster.Primary.NodeConfiguration);
            //foreach (int i in Enumerable.Range(0, cluster.SecondarySilos.Count))
            //{
            //    output.WriteLine("Secondary Silo {0} configuration = \n{1}", i+1, cluster.SecondarySilos[i].NodeConfiguration);
            //}
        }

        public virtual void Dispose()
        {
            fixture.Dispose();
        }

        [Fact, TestCategory("BVT"), TestCategory("PinnedGrains"), TestCategory("Placement")]
        public async Task PinnedGrains_SiloFails_Secondary()
        {
            IPartitionManager partitionManager = GrainFactory.GetGrain<IPartitionManager>(0);
            IList<PartitionInfo> partitionInfosList = await partitionManager.GetPartitionInfos();
            Assert.AreEqual(NumSilos, partitionInfosList.Count, "Initial: PartitionInfo list should return {0} values.", NumSilos);
            Assert.AreNotEqual(partitionInfosList[0].PartitionId, partitionInfosList[1].PartitionId, "Initial: PartitionIds should be different.");

            SiloHandle siloToKill = cluster.SecondarySilos.First();
            SiloAddress deadSilo = siloToKill.Silo.SiloAddress;
            cluster.StopSilo(siloToKill);

            foreach (int i in Enumerable.Range(0, PinnedGrainTestsFixture.NumSilos))
            {
                PartitionInfo pi = partitionInfosList[i];
                Guid partitionId = pi.PartitionId;

                if (pi.SiloId.Equals(deadSilo.ToString()))
                {
                    output.WriteLine("Partition {0} should be offline on dead silo {1}",
                        partitionId, pi.SiloId);
                    try
                    {
                        IPartitionGrain grain = GrainFactory.GetGrain<IPartitionGrain>(partitionId);
                        PartitionInfo dummy = await grain.GetPartitionInfo();
                    }
                    catch (SiloUnavailableException exc)
                    {
                        output.WriteLine(
                            "Got expected error talking to Partition {0} should be offline on dead silo {1} - Error = {2}",
                            pi.PartitionId, pi.SiloId, exc);
                    }
                }
                else
                {
                    output.WriteLine("Partition {0} should be online on alive silo {1}",
                        partitionId, pi.SiloId);
                    IPartitionGrain grain = GrainFactory.GetGrain<IPartitionGrain>(partitionId);
                    PartitionInfo partInfo = await grain.GetPartitionInfo();
                    output.WriteLine(partInfo);
                }
            }
        }

        [Fact, TestCategory("BVT"), TestCategory("PinnedGrains"), TestCategory("Placement")]
        public async Task PinnedGrains_SiloFails_Primary()
        {
            IPartitionManager partitionManager = GrainFactory.GetGrain<IPartitionManager>(0);
            IList<PartitionInfo> partitionInfosList = await partitionManager.GetPartitionInfos();
            Assert.AreEqual(NumSilos, partitionInfosList.Count, "Initial: PartitionInfo list should return {0} values.", NumSilos);
            Assert.AreNotEqual(partitionInfosList[0].PartitionId, partitionInfosList[1].PartitionId, "Initial: PartitionIds should be different.");

            foreach (int i in Enumerable.Range(0, PinnedGrainTestsFixture.NumSilos))
            {
                PartitionInfo pi = partitionInfosList[i];
                Guid partitionId = pi.PartitionId;
                output.WriteLine("Partition {0} is online on active silo {1}",
                    partitionId, pi.SiloId);
            }

            SiloHandle siloToKill = cluster.Primary;
            SiloAddress deadSilo = siloToKill.Silo.SiloAddress;
            output.WriteLine("Stopping silo {0}", deadSilo);
            cluster.StopSilo(siloToKill);

            foreach (int i in Enumerable.Range(0, PinnedGrainTestsFixture.NumSilos))
            {
                PartitionInfo pi = partitionInfosList[i];
                Guid partitionId = pi.PartitionId;

                if (pi.SiloId.Equals(deadSilo.ToString()))
                {
                    output.WriteLine("Partition {0} should be offline on dead silo {1}",
                        partitionId, pi.SiloId);
                    try
                    {
                        IPartitionGrain grain = GrainFactory.GetGrain<IPartitionGrain>(partitionId);
                        PartitionInfo dummy = await grain.GetPartitionInfo();
                    }
                    catch (SiloUnavailableException exc)
                    {
                        output.WriteLine(
                            "Got expected error talking to Partition {0} should be offline on dead silo {1} - Error = {2}",
                            pi.PartitionId, pi.SiloId, exc);
                    }
                }
                else
                {
                    output.WriteLine("Partition {0} should be online on alive silo {1}",
                        partitionId, pi.SiloId);
                    IPartitionGrain grain = GrainFactory.GetGrain<IPartitionGrain>(partitionId);
                    PartitionInfo partInfo = await grain.GetPartitionInfo();
                    output.WriteLine(partInfo);
                }
            }
        }

        [Fact, TestCategory("BVT"), TestCategory("Placement")]
        public async Task PreferLocalPlacementGrainShouldMigrateWhenKillSilo()
        {
            await cluster.WaitForLivenessToStabilizeAsync();
            output.WriteLine("******************** Starting test LocalGrainShouldMigrateWhenKillSilo ********************");
            TestSilosStarted(2);

            foreach (SiloHandle silo in cluster.GetActiveSilos())
            {
                NodeConfiguration cfg = silo.NodeConfiguration;
                output.WriteLine(
                    "Silo {0} : Address = {1} Proxy gateway: {2}",
                    cfg.SiloName, cfg.Endpoint, cfg.ProxyGatewayEndpoint);
            }

            Guid proxyKey = Guid.NewGuid();
            IRandomPlacementTestGrain proxy = GrainFactory.GetGrain<IRandomPlacementTestGrain>(proxyKey);
            IPEndPoint expected = await proxy.GetEndpoint();
            output.WriteLine("Proxy grain was placed on silo {0}", expected);

            Guid grainKey = Guid.NewGuid();
            await proxy.StartPreferLocalGrain(grainKey);
            IPreferLocalPlacementTestGrain grain = GrainFactory.GetGrain<IPreferLocalPlacementTestGrain>(grainKey);
            IPEndPoint actual = await grain.GetEndpoint();
            output.WriteLine("PreferLocalPlacement grain was placed on silo {0}", actual);
            Assert.AreEqual(expected, actual,
                "PreferLocalPlacement strategy should create activations on the local silo.");

            SiloHandle siloToKill = cluster.GetActiveSilos().First(s => s.Endpoint.Equals(expected));
            output.WriteLine("Killing silo {0} hosting locally placed grain", siloToKill);
            cluster.StopSilo(siloToKill);

            IPEndPoint newActual = await grain.GetEndpoint();
            output.WriteLine("PreferLocalPlacement grain was recreated on silo {0}", newActual);
            Assert.AreNotEqual(expected, newActual,
                "PreferLocalPlacement strategy should recreate activations on other silo if local fails.");
        }
    }
}
