/*
Project Orleans Cloud Service SDK ver. 1.0
 
Copyright (c) Microsoft Corporation
 
All rights reserved.
 
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
associated documentation files (the ""Software""), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans;
using Orleans.Runtime;
using Orleans.Runtime.Configuration;
using Orleans.Runtime.MembershipService;
using Orleans.TestingHost;
using Orleans.AzureUtils;


namespace UnitTests.StorageTests
{
    /// <summary>
    /// Tests for operation of Orleans Membership Table using AzureStore - Requires access to external Azure storage
    /// </summary>
    [TestFixture]
    public class AzureMembershipTableTests
    {
        public TestContext TestContext { get; set; }

        private string deploymentId;
        private SiloAddress siloAddress;
        private AzureBasedMembershipTable membership;
        private static readonly TimeSpan timeout = TimeSpan.FromMinutes(1);
        private readonly TraceLogger logger = TraceLogger.GetLogger("AzureMembershipTableTests", TraceLogger.LoggerType.Application);

        [TestFixtureSetUp]
        public static void ClassInitialize(TestContext testContext)
        {
            TraceLogger.Initialize(new NodeConfiguration());
            TraceLogger.AddTraceLevelOverride("AzureTableDataManager", Logger.Severity.Verbose3);
            TraceLogger.AddTraceLevelOverride("OrleansSiloInstanceManager", Logger.Severity.Verbose3);
            TraceLogger.AddTraceLevelOverride("Storage", Logger.Severity.Verbose3);

            // Set shorter init timeout for these tests
            OrleansSiloInstanceManager.initTimeout = TimeSpan.FromSeconds(20);

            //Starts the storage emulator if not started already and it exists (i.e. is installed).
            if(!StorageEmulator.TryStart())
            {
                Console.WriteLine("Azure Storage Emulator could not be started.");
            }            
        }

        [SetUp]
        public void TestInitialize()
        {
            deploymentId = "test-" + Guid.NewGuid();
            int generation = SiloAddress.AllocateNewGeneration();
            siloAddress = SiloAddress.NewLocalAddress(generation);

            logger.Info("DeploymentId={0} Generation={1}", deploymentId, generation);

            GlobalConfiguration config = new GlobalConfiguration
            {
                DeploymentId = deploymentId,
                DataConnectionString = StorageTestConstants.DataConnectionString
            };

            var mbr = new AzureBasedMembershipTable();
            await mbr.InitializeMembershipTable(config, true, logger).WithTimeout(timeout);
            membership = mbr;
        }

        [TearDown]
        public void TestCleanup()
        {
            if (membership != null && SiloInstanceTableTestConstants.DeleteEntriesAfterTest)
            {
                membership.DeleteMembershipTableEntries(deploymentId).Wait();
                membership = null;
            }
            logger.Info("Test {0} completed - Outcome = {1}", TestContext.Test, TestContext.Result);
        }

        [TestFixtureTearDown]
        public static void ClassCleanup()
        {
            // Reset init timeout after tests
            OrleansSiloInstanceManager.initTimeout = AzureTableDefaultPolicies.TableCreationTimeout;
        }

        [Test, Category("Functional"), Category("Membership"), Category("Azure")]
        public async Task Membership_Azure_Init()
        {
            await Initialize();
            Assert.IsNotNull(membership, "Membership Table handler created");
        }

        [Test, Category("Functional"), Category("Membership"), Category("Azure")]
        public async Task Membership_Azure_ReadAll()
        {
            await Initialize();
            await MembershipTablePluginTests.MembershipTable_ReadAll(membership);
        }

        [Test, Category("Functional"), Category("Membership"), Category("Azure")]
        public async Task Membership_Azure_InsertRow()
        {
            await Initialize();
            await MembershipTablePluginTests.MembershipTable_InsertRow(membership);
        }

        [Test, Category("Functional"), Category("Membership"), Category("Azure")]
        public async Task Membership_Azure_ReadRow_EmptyTable()
        {
            await Initialize();
            await MembershipTablePluginTests.MembershipTable_ReadRow_EmptyTable(membership, siloAddress);
        }

        [Test, Category("Functional"), Category("Membership"), Category("Azure")]
        public async Task Membership_Azure_ReadRow_Insert_Read()
        {
            await Initialize();
            await MembershipTablePluginTests.MembershipTable_ReadRow_Insert_Read(membership, siloAddress);
        }

        [Test, Category("Functional"), Category("Membership"), Category("Azure")]
        public async Task Membership_Azure_ReadAll_Insert_ReadAll()
        {
            await Initialize();
            await MembershipTablePluginTests.MembershipTable_ReadAll_Insert_ReadAll(membership, siloAddress);
        }
    }
}
