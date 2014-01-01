/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests for <see cref="Identity"/> and <see cref="IdentityPath"/>.
    /// </summary>
    [TestClass]
    public class IdentityTest : AbstractUnitTest
    {
        [TestMethod]
        public void IdentityBaseTest()
        {
            SimpleIdentity id = new SimpleIdentity();
            const int count = 3;
            for (int i = 0; i < count; i++)
            {
                // Default contentless implementation instances are all equal
                SimpleIdentity idNew = new SimpleIdentity();
                Assert.AreEqual(id, idNew);
                Assert.AreEqual(0, idNew.GetHashCode());
                // Global index always incrementing
                Assert.AreEqual(id.GlobalIndex + i + 1, idNew.GlobalIndex);
            }

            for (int i = 0; i < count; i++)
            {
                // Default contentless implementation instances are all equal
                NumericIdentity idNew = new NumericIdentity(i);
                Assert.AreNotEqual(id, idNew);
                if (i > 0)
                    Assert.AreNotEqual(0, idNew.GetHashCode());
                // Global index always incrementing
                Assert.AreEqual(id.GlobalIndex + count + i + 1, idNew.GlobalIndex);
            }
        }

        [TestMethod]
        public void IdentityPathTest()
        {
            SimpleIdentity id = new SimpleIdentity();
            IdentityPath pathSimple = new IdentityPath(id);
            Assert.AreEqual(IdentityPath.ROOT, pathSimple.Parent);
            Assert.AreEqual(IdentityPath.ROOT, pathSimple.GetPathTo(-1));
            Assert.AreEqual(id, pathSimple.Child);
            Assert.AreEqual(1, pathSimple.Length);
            Assert.AreEqual(0, pathSimple.Depth);
            AssertEx.ThrowsException<IndexOutOfRangeException>(() => pathSimple.GetPathTo(-2));
            AssertEx.ThrowsException<IndexOutOfRangeException>(() => pathSimple.GetPathTo(1));

            IdentityPath pathRoot = new IdentityPath(new Identity[0]);
            Assert.AreEqual(IdentityPath.ROOT, pathRoot);
            Assert.AreEqual(0, pathRoot.Length);
            Assert.AreEqual(-1, pathRoot.Depth);
            AssertEx.ThrowsException<IndexOutOfRangeException>(() => pathRoot.Child);
            AssertEx.ThrowsException<IndexOutOfRangeException>(() => pathRoot.Parent);

            const int count = 5;

            List<Identity> listId1 = new List<Identity>();
            List<Identity> listId2 = new List<Identity>();
            HashSet<IdentityPath> setPaths = new HashSet<IdentityPath>();

            IdentityPath last = IdentityPath.ROOT;
            for (int i = 0; i < count; i++)
            {
                listId1.Add(new NumericIdentity(i));
                listId2.Add(new NumericIdentity(i));

                IdentityPath path = new IdentityPath(listId1);
                Assert.AreEqual(path, new IdentityPath(listId1));
                Assert.AreNotEqual(path, new IdentityPath(listId2));
                Assert.AreEqual(last, path.Parent);
                Assert.AreSame(listId1[i], path.Child);
                Assert.AreEqual(path, new IdentityPath(path.Parent, path.Child));
                Assert.AreEqual(i, path.Depth);
                Assert.AreEqual(listId1.Count, path.Length);
                Assert.AreSame(path.Child, path.GetIdentity(path.Depth));
                Assert.AreEqual(path.Parent, path.GetPathTo(path.Depth - 1));
                Assert.AreEqual("/" + listId1.ToString("/"), path.ToString());
                for (int j = 0; j < i; j++)
                    Assert.IsTrue(setPaths.Contains(path.GetPathTo(j)));
                setPaths.Add(path);
                last = path;
            }
        }

        private class SimpleIdentity : Identity
        {            
        }

        private class NumericIdentity : Identity
        {
            public NumericIdentity(int value)
            {
                Value = value;
            }

            private int Value { get; set; }

            private bool Equals(NumericIdentity obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return base.Equals(obj) && obj.Value == Value;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return Equals(obj as NumericIdentity);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    {
                        return (base.GetHashCode()*397) ^ Value;
                    }
                }
            }

            public override string ToString()
            {
                return Value.ToString(LocalizationHelper.CurrentCulture);
            }
        }
    }
}