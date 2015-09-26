/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Windows.Forms;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Manages the undo and redo buttons and menu items in the application.
    /// The undo and redo menu items undo/redo the last action.
    /// The undo and redo buttons display a dropdown with the last actions, and allow
    /// the user to undo/redo multiple actions at once.
    /// </summary>
    internal class UndoRedoButtons
    {
        private readonly UndoManager _undoManager;
        private readonly ToolStripMenuItem _undoMenuItem;
        private readonly ToolStripSplitButton _undoButton;
        private readonly ToolStripMenuItem _redoMenuItem;
        private readonly ToolStripSplitButton _redoButton;
        private readonly Action<Action> _runUIAction;

        public UndoRedoButtons(UndoManager undoManager,
            ToolStripMenuItem undoMenuItem, ToolStripSplitButton undoButton, 
            ToolStripMenuItem redoMenuItem, ToolStripSplitButton redoButton,
            Action<Action> runUIAction)
        {
            _undoManager = undoManager;
            _undoMenuItem = undoMenuItem;
            _undoButton = undoButton;
            _redoMenuItem = redoMenuItem;
            _redoButton = redoButton;
            _runUIAction = runUIAction;
        }

        public void AttachEventHandlers()
        {
            _undoManager.StacksChanged += undoManager_StacksChanged;
            _undoMenuItem.Click += (o, e) => _undoManager.Undo();
            _redoMenuItem.Click += (o, e) => _undoManager.Redo();
            _undoButton.ButtonClick += (o, e) => _undoManager.Undo();
            _redoButton.ButtonClick += (o, e) => _undoManager.Redo();
            _undoButton.DropDownOpening += (o, e) => PopulateDropDownButton(
                _undoButton, true);
            _redoButton.DropDownOpening += (o, e) => PopulateDropDownButton(
                _redoButton, false);
        }

        void undoManager_StacksChanged(object sender, EventArgs e)
        {
            _runUIAction(UpdateButtons);
        }

        void UpdateButtons()
        {
            _undoMenuItem.Enabled = _undoManager.CanUndo;
            _redoMenuItem.Enabled = _undoManager.CanRedo;
            _undoButton.Enabled = _undoManager.CanUndo;
            _redoButton.Enabled = _undoManager.CanRedo;
        }

        void PopulateDropDownButton(ToolStripDropDownItem button, bool undo)
        {
            UndoRedoList undoRedoList = new UndoRedoList();
            undoRedoList.ShowList(button, undo, _undoManager);
        }

    }
}
