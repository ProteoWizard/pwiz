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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Implemented on an object which references state that may be used
    /// to undo an operation.  This interface and the pattern of its use
    /// inside the <see cref="UndoManager"/> were taken from the article:
    /// 
    /// Generic Memento Pattern for Undo-Redo in C#
    /// by Lu Yixiang
    /// 
    /// http://www.codeproject.com/KB/cs/generic_undo_redo.aspx
    /// </summary>
    public interface IUndoState
    {
        IUndoState Restore();
    }

    /// <summary>
    /// Interface implemented to support providing undo/redo state to the
    /// <see cref="UndoManager"/>.
    /// </summary>
    public interface IUndoable
    {
        IUndoState GetUndoState();
    }

    /// <summary>
    /// Interface for externally exposing an undo transaction as an
    /// object implementing <see cref="IDisposable"/> for use with a using
    /// clause.  The idea for such a transaction object comes from the
    /// article:
    /// 
    /// Make Your Application Reversible to Support Undo and Redo
    /// by Henrik Jonsson
    /// 
    /// http://www.codeproject.com/KB/dotnet/reversibleundoredo.aspx
    /// </summary>
    public interface IUndoTransaction : IDisposable
    {
        void Commit();

        void Rollback();
    }

    /// <summary>
    /// Used to the undo/redo state for a single <see cref="SrmDocument"/>
    /// instance.  Although this class shares some coding patterns with
    /// the undo architectures described in the articles listed above, in
    /// this case the <see cref="UndoManager"/> as it is used in this application
    /// is really a history of <see cref="SrmDocument"/> instances, made
    /// possible by its immutable architecture and complete separation
    /// from its views.
    /// 
    /// This makes it possible to restore any state in either stack, simply
    /// by calling its <see cref="IUndoState.Restore"/> method, where more
    /// traditional implementations based on recording actions must play back
    /// the undo sequences in order to reach a specific state.
    /// </summary>
    public class UndoManager
    {
        private readonly IUndoable _client;
        private readonly Stack<UndoRecord> _undoStack;
        private readonly Stack<UndoRecord> _redoStack;

        /// <summary>
        /// Pending record managed by a <see cref="UndoTransaction"/>
        /// which has not yet been pushed onto the undo stack.
        /// </summary>
        private UndoRecord _pendingRecord;

        /// <summary>
        /// Constructor for associating an <see cref="UndoManager"/>
        /// with a <see cref="IUndoable"/>.  The generalized interface
        /// makes it possible to unit test this class separate from
        /// <see cref="SrmDocument"/>, though in the running application,
        /// it is always managing references to document instances.
        /// </summary>
        /// <param name="client">Undo state provider</param>
        public UndoManager(IUndoable client)
        {
            _client = client;

            _undoStack = new Stack<UndoRecord>();
            _redoStack = new Stack<UndoRecord>();
        }

        /// <summary>
        /// Fires whenever the undo/redo stacks change, allowing a listener
        /// to update user interface based on the changing state.
        /// </summary>
        public event EventHandler<EventArgs> StacksChanged;

        /// <summary>
        /// True if an undo/redo action is currently executing.
        /// </summary>
        public bool InUndoRedo { get; private set; }

        /// <summary>
        /// True if a undo transaction has been opened, but not commmitted.
        /// </summary>
        public bool Recording { get { return _pendingRecord != null;  } }

        /// <summary>
        /// True if either in undo/redo or recording.
        /// </summary>
        private bool Active { get { return InUndoRedo || Recording; } }

        /// <summary>
        /// True if a call to <see cref="Undo"/> can be executed to restore state.
        /// Use to enable/disable undo user interface.
        /// </summary>
        public bool CanUndo { get { return !Active && _undoStack.Count > 0; } }

        /// <summary>
        /// True if a call to <see cref="Redo"/> can be executed to restore state.
        /// Use to enable/disable redo user interface.
        /// </summary>
        public bool CanRedo { get { return !Active && _redoStack.Count > 0; } }

        /// <summary>
        /// Count of the records on the undo stack.
        /// </summary>
        public int UndoCount { get { return _undoStack.Count; } }

        /// <summary>
        /// Count of the records on the redo stack.
        /// </summary>
        public int RedoCount { get { return _redoStack.Count; } }

        /// <summary>
        /// The description of the record at the top of the undo stack, or null if the stack is empty.
        /// </summary>
        public string UndoDescription { get { return GetDescription(_undoStack); } }

        /// <summary>
        /// A list of descriptions for all currently stored undo records.
        /// </summary>
        public IEnumerable<string> UndoDescriptions { get { return GetDescriptions(_undoStack); } }

        /// <summary>
        /// The description of the record at the top of the redo stack, or null if the stack is empty.
        /// </summary>
        public string RedoDescription { get { return GetDescription(_redoStack); } }

        /// <summary>
        /// A list of descriptions for all currently stored redo records.
        /// </summary>
        public IEnumerable<string> RedoDescriptions { get { return GetDescriptions(_redoStack); } }

        /// <summary>
        /// Clears both the undo and redo stacks.  Use after an even occurs that cannot
        /// be undone, such as opening a new document.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            
            if (StacksChanged != null)
                StacksChanged(this, new EventArgs());
        }

        /// <summary>
        /// Restore the <see cref="IUndoState"/> at the top of the undo stack.
        /// </summary>
        public void Undo()
        {
            if (CanUndo)
                Restore(0, _undoStack, _redoStack);
        }

        /// <summary>
        /// Restores the <see cref="IUndoState"/> at a specified depth in the undo stack.
        /// This method depends on the fact that this undo stack is really a document
        /// history and not a record of actions.  A record of actions would need to replay
        /// each in order.  Here we just pop the specified number of records, and restore
        /// the one left at the top.
        /// </summary>
        /// <param name="index">Index of record on the stack to restore</param>
        public void UndoRestore(int index)
        {
            if (CanUndo)
                Restore(index, _undoStack, _redoStack);
        }

        /// <summary>
        /// Restore the <see cref="IUndoState"/> at the top of the redo stack.
        /// </summary>
        public void Redo()
        {
            if (CanRedo)
                Restore(0, _redoStack, _undoStack);
        }

        /// <summary>
        /// Restores the <see cref="IUndoState"/> at a specified depth in the redo stack.
        /// This method depends on the fact that this undo stack is really a document
        /// history and not a record of actions.  A record of actions would need to replay
        /// each in order.  Here we just pop the specified number of records, and restore
        /// the one left at the top.
        /// </summary>
        /// <param name="index">Index of record on the stack to restore</param>
        public void RedoRestore(int index)
        {
            if (CanRedo)
                Restore(index, _redoStack, _undoStack);            
        }

        /// <summary>
        /// Creates a pending undo transaction, which may be committed after the
        /// undoable work is completed successfully, or rolledback in case of failure.
        /// The <see cref="IUndoTransaction"/> implements <see cref="IDisposable"/>
        /// and is intended for use inside a using clause, which rollback the transaction
        /// automatically, if it is not commited within the scope.
        /// </summary>
        /// <param name="description">Description of the action to be performed</param>
        /// <param name="undoState">An undo state snapshot from the UI thread, in case the transation is begun on another thread</param>
        /// <returns>Transaction instance</returns>
        public IUndoTransaction BeginTransaction(string description, IUndoState undoState = null)
        {
            if (InUndoRedo)
                throw new InvalidOperationException(Resources.UndoManager_BeginTransaction_Undo_transaction_may_not_be_started_in_undo_redo);

            // Inner transactions are ignored, since only the initial record is
            // desired in the undo stack.
            if (_pendingRecord != null)
                return new NoOpTransaction();

            _pendingRecord = new UndoRecord(description, undoState ?? _client.GetUndoState());
            return new UndoTransaction(this);
        }

        /// <summary>
        /// Commits the pending <see cref="IUndoState"/> record created by
        /// <see cref="BeginTransaction"/> to the undo stack, and clears the redo stack.
        /// </summary>
        private void Commit()
        {
            if (_pendingRecord == null)
                throw new InvalidOperationException(Resources.UndoManager_Commit_Commit_called_with_no_pending_undo_record);

            _redoStack.Clear();
            _undoStack.Push(_pendingRecord);
            _pendingRecord = null;

            if (StacksChanged != null)
                StacksChanged(this, new EventArgs());
        }

        /// <summary>
        /// Discards the pending <see cref="IUndoState"/> record created by
        /// <see cref="BeginTransaction"/>.
        /// </summary>
        private void Rollback()
        {
            _pendingRecord = null;
        }

        /// <summary>
        /// Restores the <see cref="IUndoState"/> at a specified depth in one of the stacks.
        /// This method depends on the fact that this undo stack is really a document
        /// history and not a record of actions.  A record of actions would need to replay
        /// each in order.  Here we just pop the specified number of records, and restore
        /// the one left at the top.
        /// 
        /// When the index is zero, however, it behaves much like any other undo manager.
        /// </summary>
        /// <param name="index">Index of record on the stack to restore</param>
        /// <param name="from">Stack to pop from</param>
        /// <param name="to">Stack to push resulting <see cref="IUndoState"/> to</param>
        private void Restore(int index, Stack<UndoRecord> from, Stack<UndoRecord> to)
        {
            if (_pendingRecord != null)
                throw new InvalidOperationException(Resources.UndoManager_Restore_Attempting_undo_redo_inside_undo_transaction);
            if (index >= from.Count)
                throw new IndexOutOfRangeException(string.Format(Resources.UndoManager_Restore_Attempt_to_index__0__beyond_length__1__, index, from.Count));

            try
            {
                InUndoRedo = true;

                // Because the undo manager stores a history of states, rather
                // than one of actions, all preceding states may simply be popped
                // off the stack, and only the desired state restored.

                // The undo state returned from the call to IUndoState.Restore
                // must be the first record pushed onto the redo stack, since it
                // contains the information needed to return the application to 
                // the state prior to this undo.

                // After that the rest of the popped undo history gets pushed
                // onto the redo stack so that an undo of 10 changes, and then
                // a redo will restore the state to that of 9 changes ago, and
                // 9 more redos will restore the state before the undo.  Or vice-
                // versa, if the from stack is the redo stack.

                // Save any records skipped by this operation.
                var list = new List<UndoRecord>();
                for (int i = 0; i < index; i++)
                    list.Add(from.Pop());

                // Descriptions need to be correctly assigned, with the record
                // returned from the actual restore operation containing the
                // description from the top of the undo stack.
                string description = (list.Count == 0 ? null : list[0].Description);

                // Restore the desired state, and push the original state.
                UndoRecord top = from.Pop();
                to.Push(top.Restore(description));

                // If undo/redo was multi-record, restore the rest of the history.
                // All descriptions need to shift, and the last record needs the
                // description of the just popped record.
                for (int i = 0; i < list.Count; i++)
                {
                    description = (i < list.Count - 1 ? list[i + 1].Description : top.Description);
                    to.Push(new UndoRecord(description, list[i].UndoState));
                }
            }
            finally
            {
                InUndoRedo = false;
            }

            if (StacksChanged != null)
                StacksChanged(this, new EventArgs());
        }

        /// <summary>
        /// Gets the description for the undo state at the top of a stack
        /// of <see cref="UndoRecord"/> objects.
        /// </summary>
        /// <param name="stack">The stack to inspect</param>
        /// <returns>The description of the top record</returns>
        private static string GetDescription(Stack<UndoRecord> stack)
        {
            return (stack.Count == 0 ? null : stack.Peek().Description);
        }

        /// <summary>
        /// Gets the list of descriptions for all undo state records on a stack
        /// of <see cref="UndoRecord"/> objects.
        /// </summary>
        /// <param name="stack">The stack to inspect</param>
        /// <returns>A list of all descriptions</returns>
        private static IEnumerable<string> GetDescriptions(IEnumerable<UndoRecord> stack)
        {
            foreach (UndoRecord record in stack)
                yield return record.Description;            
        }

        /// <summary>
        /// Internal class for storing undo state with its description on
        /// one of the stacks.
        /// </summary>
        private sealed class UndoRecord
        {
            public UndoRecord(string description, IUndoState undoState)
            {
                Description = description;
                UndoState = undoState;
            }

            public string Description { get; private set; }

            public IUndoState UndoState { get; private set; }

            public UndoRecord Restore(string description)
            {
                return new UndoRecord(description ?? Description, UndoState.Restore());
            }
        }

        /// <summary>
        /// A transaction object that implements <see cref="IDisposable"/>
        /// for use in recording undo actions with an <see cref="UndoManager"/>.
        /// </summary>
        private class UndoTransaction : IUndoTransaction
        {
            private readonly UndoManager _manager;

            public UndoTransaction(UndoManager manager)
            {
                _manager = manager;
            }

            public void Dispose()
            {
                Rollback();
            }

            public void Commit()
            {
                _manager.Commit();
            }

            public void Rollback()
            {
                _manager.Rollback();
            }
        }

        /// <summary>
        /// A transaction that does nothing, for use when <see cref="UndoManager.BeginTransaction"/>
        /// is called inside an existing transaction.
        /// </summary>
        private class NoOpTransaction : IUndoTransaction
        {
            public void Dispose() {}
            public void Commit() {}
            public void Rollback() {}
        }
    }
}
