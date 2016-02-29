using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using Xunit;
using Orleans.Serialization;

namespace UnitTests.Serialization
{
    /// <summary>
    /// Summary description for SerializationTests
    /// </summary>
    public class SerializationTestsImmutableTypes
    {
        private const bool UseJsonFallbackSerializer = true; // FWIW, this flag makes no difference to the failure mode.

        public SerializationTestsImmutableTypes()
        {
            SerializationManager.InitializeForTesting(useJsonFallbackSerializer: UseJsonFallbackSerializer);
        }

        [Fact, TestCategory("BVT"), TestCategory("Functional"), TestCategory("Serialization"), TestCategory("Immutable")]
        public void SerializationTests_AsReadOnly()
        {
            var data = new long[] {1, 2, 3};
            var dataList = new List<long>(data);

            IList<long> input, output;
            
            input = dataList.AsReadOnly(); // Returned ReadOnlyCollection<T>
            output = SerializationManager.RoundTripSerializationForTesting(input);
            Assert.AreEqual(input.ToString(), output.ToString(), "List.AsReadOnly");
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], output[i], "List.AsReadOnly[{0}]", i);
            }

            input = Array.AsReadOnly(data); // Returned ReadOnlyCollection<T>
            output = SerializationManager.RoundTripSerializationForTesting(input);
            Assert.AreEqual(input.ToString(), output.ToString(), "Array.AsReadOnly");
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], output[i], "Array.AsReadOnly[{0}]", i);
            }
        }

        [Fact, TestCategory("BVT"), TestCategory("Functional"), TestCategory("Serialization"), TestCategory("Immutable")]
        public void SerializationTests_ImmutableList()
        {
            var data = new long[] { 1, 2, 3 };

            IList<long> input, output;

            input = ImmutableList<long>.Empty;
            output = SerializationManager.RoundTripSerializationForTesting(input);
            Assert.AreEqual(input.ToString(), output.ToString(), "ImmutableList.Empty");
            Assert.AreEqual(0, output.Count, "ImmutableList.Empty.Length");

            input = data.ToImmutableList(); // 
            output = SerializationManager.RoundTripSerializationForTesting(input);
            Assert.AreEqual(input.ToString(), output.ToString(), "data.ToImmutableList");
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], output[i], "data.ToImmutableList[{0}]", i);
            }
        }
    }
}
