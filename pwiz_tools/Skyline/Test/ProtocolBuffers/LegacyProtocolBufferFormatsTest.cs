/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Serialization;
using pwiz.SkylineTest.ProtocolBuffers.GeneratedCode;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.ProtocolBuffers
{
    /// <summary>
    /// Verifies that using the types in the Google.Protobuf.WellKnownTypes package
    /// are binary compatible with the OptionalDouble protocol buffer messages that
    /// we used to use.
    /// </summary>
    [TestClass]
    public class LegacyProtocolBufferFormatsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestAlternateProtocolBufferFormats()
        {

            foreach (var doubleValue in new double?[] { null, 1, -1, 1.5, -1.5, double.MinValue, double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var legacyTransitionPeak = new LegacySkylineDocumentProto.Types.LegacyTransitionPeak()
                {
                    IonMobility = ToOptional(doubleValue)
                };
                var peak = new SkylineDocumentProto.Types.TransitionPeak()
                {
                    IonMobility = doubleValue
                };
                VerifyBinaryCompatible(legacyTransitionPeak, peak);
            }

            foreach (var floatValue in new float?[] { null, 1, -1, 1.5f, -1.5f, float.MinValue, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var legacyTransition = new LegacySkylineDocumentProto.Types.LegacyTransition
                {
                    IsotopeDistProportion= ToOptional(floatValue)
                };
                var transition = new SkylineDocumentProto.Types.Transition
                {
                    IsotopeDistProportion = floatValue
                };
                VerifyBinaryCompatible(legacyTransition, transition);
            }

            foreach (var stringValue in new[] { null, "", "hello" })
            {
                var legacyTransition = new LegacySkylineDocumentProto.Types.LegacyTransition
                {
                    Adduct = ToOptional(stringValue)
                };
                var transition = new SkylineDocumentProto.Types.Transition
                {
                    Adduct = stringValue
                };
                VerifyBinaryCompatible(legacyTransition, transition);
            }
        }

        private void VerifyBinaryCompatible(IMessage message1, IMessage message2)
        {
            var bytes1 = message1.ToByteArray();
            var bytes2 = message2.ToByteArray();
            CollectionAssert.AreEqual(bytes1, bytes2);
        }

        public static double? FromOptional(LegacySkylineDocumentProto.Types.OptionalDouble optionalDouble)
        {
            return optionalDouble == null ? (double?)null : optionalDouble.Value;
        }

        public static LegacySkylineDocumentProto.Types.OptionalDouble ToOptional(double? doubleValue)
        {
            return doubleValue.HasValue
                ? new LegacySkylineDocumentProto.Types.OptionalDouble { Value = doubleValue.Value } : null;
        }
        public static double? FromOptional(LegacySkylineDocumentProto.Types.OptionalFloat optionalFloat)
        {
            return optionalFloat == null ? (double?)null : optionalFloat.Value;
        }

        public static LegacySkylineDocumentProto.Types.OptionalFloat ToOptional(float? floatValue)
        {
            return floatValue.HasValue
                ? new LegacySkylineDocumentProto.Types.OptionalFloat{ Value = floatValue.Value } : null;
        }
        public static string FromOptional(LegacySkylineDocumentProto.Types.OptionalString optionalString)
        {
            return optionalString == null ? null : optionalString.Value;
        }

        public static LegacySkylineDocumentProto.Types.OptionalString ToOptional(string value)
        {
            return value == null ? null : new LegacySkylineDocumentProto.Types.OptionalString { Value = value };
        }

    }
}
