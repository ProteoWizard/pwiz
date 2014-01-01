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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// This is a test class for UndoManagerTest and is intended
    /// to contain all UndoManagerTest Unit Tests
    /// </summary>
    [TestClass]
    public class UndoManagerTest : AbstractUnitTest
    {
        /// <summary>
        /// A test for undo transactions and stack changed notifications.
        /// </summary>
        [TestMethod]
        public void UndoTransactionTest()
        {
            var undoable = new TestUndoable();
            var manager = new UndoManager(undoable);

            manager.StacksChanged += undoable.UndoStacksChanged;

            Assert.IsNull(manager.UndoDescription);
            Assert.AreEqual(0, manager.UndoDescriptions.ToArray().Length);
            Assert.IsNull(manager.RedoDescription);
            Assert.AreEqual(0, manager.RedoDescriptions.ToArray().Length);

            // Test functions which should be no-ops with nothing to do.
            manager.Undo();
            manager.UndoRestore(10);
            manager.Redo();
            manager.RedoRestore(10);

            const string description = "Success";
            using (var undo = manager.BeginTransaction(description))
            {
                Assert.IsTrue(manager.Recording);
                undoable.Revise();
                undoable.AllowStackChanges = true;
                undo.Commit();
                undoable.AllowStackChanges = false;
            }
            Assert.IsFalse(manager.Recording);
            // Undo stack should contain new record
            Assert.AreEqual(description, manager.UndoDescription);

            using (manager.BeginTransaction("Implicit rollback"))
            {
                Assert.IsTrue(manager.Recording);
                // No commit call
            }
            Assert.IsFalse(manager.Recording);
            // Undo stack shouldn't change
            Assert.AreEqual(description, manager.UndoDescription);

            using (var undo = manager.BeginTransaction("Explicit rollback"))
            {
                Assert.IsTrue(manager.Recording);
                undo.Rollback();
                Assert.IsFalse(manager.Recording);
            }
            // Undo stack shouldn't change
            Assert.AreEqual(description, manager.UndoDescription);

            var undoFree = manager.BeginTransaction("Rollback without Dispose");
            Assert.IsTrue(manager.Recording);
            undoFree.Rollback();
            Assert.IsFalse(manager.Recording);
            // Undo stack shouldn't change
            Assert.AreEqual(description, manager.UndoDescription);

            undoFree = manager.BeginTransaction("Commit without Dispose");
            Assert.IsTrue(manager.Recording);
            undoable.Revise();
            undoable.AllowStackChanges = true;
            undoFree.Commit();
            undoable.AllowStackChanges = false;
            Assert.IsFalse(manager.Recording);
            // Undo stack should have extra record
            Assert.AreEqual(2, manager.UndoCount);

            undoable.AllowStackChanges = true;
            manager.Undo();
            Assert.AreEqual(description, manager.UndoDescription);
            Assert.AreEqual(1, undoable.RevisionIndex);
            manager.Redo();
            Assert.AreEqual(2, undoable.RevisionIndex);
            Assert.AreEqual(2, manager.UndoCount);
            manager.Undo();
            Assert.AreEqual(1, manager.RedoCount);
            undoable.AllowStackChanges = false;

            var descriptionNested = "Nested transactions";
            using (var undo = manager.BeginTransaction(descriptionNested))
            {
                Assert.IsTrue(manager.Recording);
                using (var undoInner = manager.BeginTransaction("Inner transaction"))
                {
                    undoable.Revise();
                    undoInner.Commit(); // Should be ignored
                    Assert.IsTrue(manager.Recording);
                }
                Assert.IsTrue(manager.Recording);
                undoable.AllowStackChanges = true;
                undo.Commit();
                undoable.AllowStackChanges = false;
                Assert.IsFalse(manager.Recording);
            }
            Assert.AreEqual(descriptionNested, manager.UndoDescription);
            // Make sure undo stack was cleared
            Assert.AreEqual(0, manager.RedoCount);
            Assert.AreEqual(0, manager.RedoDescriptions.ToArray().Length);
            Assert.IsNull(manager.RedoDescription);

            try
            {
                using (var undo = manager.BeginTransaction("Commit after rollback"))
                {
                    Assert.IsTrue(manager.Recording);
                    undo.Rollback();
                    Assert.IsFalse(manager.Recording);
                    undo.Commit();
                    Assert.Fail("Exception exptected");
                }
            }
            catch (Exception)
            {
                Assert.AreEqual(2, manager.UndoCount);
            }

            try
            {
                descriptionNested = "Double commit";
                using (var undo = manager.BeginTransaction(descriptionNested))
                {
                    Assert.IsTrue(manager.Recording);
                    using (var undoInner = manager.BeginTransaction("Inner transaction"))
                    {
                        undoable.Revise();
                        undoInner.Commit();
                        Assert.IsTrue(manager.Recording);
                    }
                    undoable.AllowStackChanges = true;
                    undo.Commit();
                    undoable.AllowStackChanges = false;
                    Assert.IsFalse(manager.Recording);

                    undo.Commit();
                    Assert.Fail("Exception exptected");
                }
            }
            catch (Exception)
            {
                Assert.AreEqual(3, manager.UndoCount);
                Assert.AreEqual(3, manager.UndoDescriptions.ToArray().Length);
                Assert.AreEqual(descriptionNested, manager.UndoDescription);
            }

            Assert.AreEqual(7, undoable.CountStackChanges);
        }

        /// <summary>
        /// Test undo/redo of multiple records at a time
        /// </summary>
        [TestMethod]
        public void UndoRedoMultiTest()
        {
            var undoable = new TestUndoable();
            var manager = new UndoManager(undoable);
            
            const int count = 6;

            for (int i = 0; i < count; i++)
            {
                using (var undo = manager.BeginTransaction(string.Format("Revision {0}", i + 1)))
                {
                    undoable.Revise();
                    undo.Commit();
                }
            }

            for (int i = 0; i < count; i++)
            {
                manager.UndoRestore(i);
                string[] undoDescriptions = manager.UndoDescriptions.ToArray();
                for (int j = 0; j < undoDescriptions.Length; j++)
                    Assert.AreEqual(string.Format("Revision {0}", count - j - i - 1), undoDescriptions[j]);

                string[] redoDescriptions = manager.RedoDescriptions.ToArray();
                for (int j = 0; j < redoDescriptions.Length; j++)
                    Assert.AreEqual(string.Format("Revision {0}", count + j - i), redoDescriptions[j]);

                Assert.AreEqual(count - i - 1, manager.UndoCount);
                Assert.AreEqual(i + 1, manager.RedoCount);
                Assert.AreEqual(count - i - 1, undoable.RevisionIndex);
                manager.RedoRestore(i);
                Assert.AreEqual(count, manager.UndoCount);
                Assert.AreEqual(0, manager.RedoCount);
                Assert.AreEqual(count, undoable.RevisionIndex);
            }

            // Try undoing beyond end of stack
            try
            {
                manager.UndoRestore(count);
                Assert.Fail("Expected exception");
            }
            catch (IndexOutOfRangeException)
            {
                Assert.AreEqual(count, manager.UndoCount);
            }

            manager.UndoRestore(count / 2);

            // Try redoing beyond end of stack
            try
            {
                manager.RedoRestore(count);
                Assert.Fail("Expected exception");
            }
            catch (IndexOutOfRangeException)
            {
                Assert.AreEqual(count / 2 + 1, manager.RedoCount);
            }

            manager.Clear();
            Assert.AreEqual(0, manager.UndoCount);
            Assert.AreEqual(0, manager.RedoCount);
        }
    }

    /// <summary>
    /// Shim class for testing the <see cref="UndoManager"/>
    /// </summary>
    internal class TestUndoable : IUndoable
    {
        public int RevisionIndex { get; private set; }

        public void Revise()
        {
            RevisionIndex++;
        }

        public bool AllowStackChanges { get; set; }
        public int CountStackChanges { get; private set; }

        public void UndoStacksChanged(object sender, EventArgs e)
        {
            // Make sure stack changes are allowed
            Assert.IsTrue(AllowStackChanges);

            CountStackChanges++;
        }

        #region Implementation of IUndoable

        IUndoState IUndoable.GetUndoState()
        {
            return new RevisionUndoState(this, RevisionIndex);
        }

        private class RevisionUndoState : IUndoState
        {
            private readonly TestUndoable _undoable;
            private readonly int _revision;

            public RevisionUndoState(TestUndoable manager, int revision)
            {
                _undoable = manager;
                _revision = revision;
            }

            public IUndoState Restore()
            {
                var restore = new RevisionUndoState(_undoable, _undoable.RevisionIndex);
                _undoable.RevisionIndex = _revision;
                return restore;
            }
        }

        #endregion        
    }
}