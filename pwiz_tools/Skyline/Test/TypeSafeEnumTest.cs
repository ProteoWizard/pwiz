/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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

using System;
using pwiz.SkylineTestUtil;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;

namespace pwiz.SkylineTest
{
    // ReSharper disable AccessToForEachVariableInClosure
    [TestClass]
    public class TypeSafeEnumTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestValidTypeSafeEnumValues()
        {
            var enumValues = new TestEnum[]
            {
                TestEnum.Minus1, 
                TestEnum.Two,
                0
            };
            TypeSafeEnum.ValidateList(enumValues);

            foreach (var enumValue in enumValues)
            {
                var typeSafeEnum = new TypeSafeEnum<TestEnum>(enumValue);
                Assert.AreEqual(enumValue, typeSafeEnum.Value);
                Assert.AreEqual(enumValue, TypeSafeEnum.Parse<TestEnum>(enumValue.ToString()));
                Assert.AreEqual(enumValue, TypeSafeEnum.Parse<TestEnum>(((int) enumValue).ToString()));
                Assert.IsTrue(TypeSafeEnum.IsValid(enumValue));
                Assert.AreEqual(enumValue.ToString(), typeSafeEnum.ToString());
                Assert.AreEqual(enumValue, TypeSafeEnum.ValidateOrDefault(enumValue, (TestEnum)(-100)));
            }
        }

        [TestMethod]
        public void TestInvalidTypeSafeEnumValues()
        {
            var invalidValues = new[]
            {
                (TestEnum) (-4),
                (TestEnum) 3,
            };
            AssertEx.ThrowsException<ArgumentException>(()=>TypeSafeEnum.ValidateList(invalidValues));
            foreach (var enumValue in invalidValues)
            {
                Assert.IsFalse(TypeSafeEnum.IsValid(enumValue));
                AssertEx.ThrowsException<ArgumentException>(
                    () => new TypeSafeEnum<TestEnum>(enumValue));
                AssertEx.ThrowsException<ArgumentException>(
                    ()=>TypeSafeEnum.Parse<TestEnum>(enumValue.ToString()));
                AssertEx.ThrowsException<ArgumentException>(
                    ()=>TypeSafeEnum.Parse<TestEnum>(((int) enumValue).ToString()));
                Assert.AreEqual(TestEnum.Minus1, 
                    TypeSafeEnum.ValidateOrDefault(enumValue, TestEnum.Minus1));
            }
        }

        enum TestEnum
        {
            Minus1 = -1,
            Two = 2,
        }
    }
}
