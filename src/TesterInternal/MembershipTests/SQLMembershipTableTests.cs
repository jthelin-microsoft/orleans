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
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans;
using Orleans.AzureUtils;
using Orleans.Runtime;
using Orleans.Runtime.Configuration;
using Orleans.Runtime.MembershipService;
using Orleans.Runtime.Storage.Relational;
using Orleans.Runtime.Storage.Management;
using Orleans.Runtime.Storage.RelationalExtensions;
using Orleans.TestingHost;
using UnitTests.StorageTests;

namespace UnitTests.MembershipTests
{
    /// <summary>
    /// Tests for operation of Orleans Membership Table using SqlServer - Requires access to SqlServer instance on this machine.
    /// </summary>
    [TestFixture]
    //[DeploymentItem("CreateOrleansTables_SqlServer.sql")]
    public class SQLMembershipTableTests
    {
        private string deploymentId;
        private SiloAddress siloAddress;
        private IMembershipTable membership;
        private static IRelationalStorage relationalStorage;
        private const string testDatabaseName = "OrleansTest";
        private static readonly TimeSpan timeout = TimeSpan.FromMinutes(1);
        private readonly TraceLogger logger = TraceLogger.GetLogger("SQLMembershipTableTests", TraceLogger.LoggerType.Application);

        [TestFixtureSetUp]
        public void ClassInitialize()
        {
            TraceLogger.Initialize(new NodeConfiguration());
            TraceLogger.AddTraceLevelOverride("SQLMembershipTableTests", Logger.Severity.Verbose3);

            // Set shorter init timeout for these tests
            OrleansSiloInstanceManager.initTimeout = TimeSpan.FromSeconds(20);

            Console.WriteLine("Initializing relational databases...");
            relationalStorage = RelationalStorageUtilities.CreateDefaultSqlServerStorageInstance();

            Console.WriteLine("Dropping and recreating database '{0}' with connectionstring '{1}'", testDatabaseName, relationalStorage.ConnectionString);
            if(relationalStorage.ExistsDatabaseAsync(testDatabaseName).Result)
            {
                relationalStorage.DropDatabaseAsync(testDatabaseName).Wait();
            }
            relationalStorage.CreateDatabaseAsync(testDatabaseName).Wait();

            //The old storage instance has the previous connection string, time have a new handle with a new connection string...
            relationalStorage = relationalStorage.CreateNewStorageInstance(testDatabaseName);

            Console.WriteLine("Creating database tables...");
            var creationScripts = RelationalStorageUtilities.RemoveBatchSeparators(File.ReadAllText("CreateOrleansTables_SqlServer.sql"));
            foreach(var creationScript in creationScripts)
            {
                var res = relationalStorage.ExecuteAsync(creationScript).Result;
            }

            //Currently there's only one database under test, SQL Server. So this as the other
            //setup is hardcoded here: putting the database in simple recovery mode.
            //This removes the use of recovery log in case of database crashes, which
            //improves performance to some degree, depending on usage. For non-performance testing only.
            var simpleModeRes = relationalStorage.ExecuteAsync(string.Format("ALTER DATABASE [{0}] SET RECOVERY SIMPLE;", testDatabaseName)).Result;

            Console.WriteLine("Initializing relational databases done.");
        }

        [TestFixtureTearDown]
        public void ClassCleanup()
        {
            // Reset init timeout after tests
            OrleansSiloInstanceManager.initTimeout = AzureTableDefaultPolicies.TableCreationTimeout;
        }

        [SetUp]
        public void TestInitialize()
        {
            Initialize().Wait();
        }

        private async Task Initialize()
        {
            deploymentId = "test-" + Guid.NewGuid();
            int generation = SiloAddress.AllocateNewGeneration();
            siloAddress = SiloAddress.NewLocalAddress(generation);

            logger.Info("DeploymentId={0} Generation={1}", deploymentId, generation);

            GlobalConfiguration config = new GlobalConfiguration
            {
                DeploymentId = deploymentId,
                DataConnectionString = relationalStorage.ConnectionString
            };

            var mbr = new SqlMembershipTable();
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
            TestContext testContext = TestContext.CurrentContext;
            logger.Info("Test {0} completed - Outcome = {1}", testContext.Test.Name, testContext.Result.Status);
        }

        [Test, Category("Membership"), Category("SqlServer")]
        public void MembershipTable_SqlServer_Init()
        {
            Assert.IsNotNull(membership, "Membership Table handler created");
        }

        [Test, Category("Membership"), Category("SqlServer")]
        public async Task MembershipTable_SqlServer_ReadAll()
        {
            await MembershipTablePluginTests.MembershipTable_ReadAll(membership);
        }

        [Test, Category("Membership"), Category("SqlServer")]
        public async Task MembershipTable_SqlServer_InsertRow()
        {
            await MembershipTablePluginTests.MembershipTable_InsertRow(membership);
        }

        [Test, Category("Membership"), Category("SqlServer")]
        public async Task MembershipTable_SqlServer_ReadRow_EmptyTable()
        {
            await MembershipTablePluginTests.MembershipTable_ReadRow_EmptyTable(membership, siloAddress);
        }

        [Test, Category("Membership"), Category("SqlServer")]
        public async Task MembershipTable_SqlServer_ReadRow_Insert_Read()
        {
            await MembershipTablePluginTests.MembershipTable_ReadRow_Insert_Read(membership, siloAddress);
        }

        [Test, Category("Membership"), Category("SqlServer")]
        public async Task MembershipTable_SqlServer_ReadAll_Insert_ReadAll()
        {
            await MembershipTablePluginTests.MembershipTable_ReadAll_Insert_ReadAll(membership, siloAddress);
        }

        [Test, Category("Membership"), Category("SqlServer")]
        public async Task MembershipTable_SqlServer_UpdateRow()
        {
            await MembershipTablePluginTests.MembershipTable_UpdateRow(membership);
        }
    }
}
