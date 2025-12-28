/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.5) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.Drawing;
using System.Windows.Forms;

namespace pwiz.Skyline.Controls.FilesTree
{
    /// <summary>
    /// Allows test code to intercept DoDragDrop and simulate drag events without mouse interaction.
    /// </summary>
    public interface IDragDropHandler
    {
        DragDropEffects DoDragDrop(Control control, object data, DragDropEffects allowedEffects);
    }

    public class DefaultDragDropHandler : IDragDropHandler
    {
        public static readonly DefaultDragDropHandler Instance = new DefaultDragDropHandler();

        public DragDropEffects DoDragDrop(Control control, object data, DragDropEffects allowedEffects)
        {
            return control.DoDragDrop(data, allowedEffects);
        }
    }

    /// <summary>
    /// Test implementation that captures drag data and allows simulation of drag events.
    /// </summary>
    public class DragDropSimulator : IDragDropHandler
    {
        private FilesTreeForm _form;
        private DataObject _dragData;
        private DragDropEffects _allowedEffects;

        public DragDropSimulator(FilesTreeForm form)
        {
            _form = form;
        }

        public bool IsDragging { get; private set; }
        public DataObject DragData => _dragData;
        public DragDropEffects AllowedEffects => _allowedEffects;
        public DragDropEffects LastEffect { get; private set; }

        // Captures drag data instead of starting a real drag operation
        public DragDropEffects DoDragDrop(Control control, object data, DragDropEffects allowedEffects)
        {
            _dragData = data as DataObject;
            _allowedEffects = allowedEffects;
            IsDragging = true;
            return DragDropEffects.None;
        }

        public void SimulateDragEnter(Point screenPoint)
        {
            if (!IsDragging || _dragData == null)
                return;

            var args = CreateDragEventArgs(screenPoint, _allowedEffects);
            _form.RaiseDragEnter(args);
            LastEffect = args.Effect;
        }

        public void SimulateDragOver(Point screenPoint)
        {
            if (!IsDragging || _dragData == null)
                return;

            var args = CreateDragEventArgs(screenPoint, _allowedEffects);
            _form.RaiseDragOver(args);
            LastEffect = args.Effect;
        }

        public void SimulateDragLeave()
        {
            if (!IsDragging)
                return;

            _form.RaiseDragLeave();
        }

        public void SimulateDrop(Point screenPoint)
        {
            if (!IsDragging || _dragData == null)
                return;

            var args = CreateDragEventArgs(screenPoint, LastEffect);
            _form.RaiseDragDrop(args);
            EndDrag();
        }

        public void SimulateRemoveTargetDragEnter(Point screenPoint)
        {
            if (!IsDragging || _dragData == null)
                return;

            var args = CreateDragEventArgs(screenPoint, _allowedEffects);
            _form.RaiseRemoveTargetDragEnter(args);
            LastEffect = args.Effect;
        }

        public void SimulateRemoveTargetDrop(Point screenPoint)
        {
            if (!IsDragging || _dragData == null)
                return;

            var args = CreateDragEventArgs(screenPoint, LastEffect);
            _form.RaiseRemoveTargetDragDrop(args);
            EndDrag();
        }

        public void SimulateEscapeCancel()
        {
            if (!IsDragging)
                return;

            _form.RaiseQueryContinueDrag(true);
            EndDrag();
        }

        public void EndDrag()
        {
            IsDragging = false;
            _dragData = null;
        }

        private DragEventArgs CreateDragEventArgs(Point screenPoint, DragDropEffects allowedEffects)
        {
            // Last parameter is the initial effect - set to Move so DropNodes will perform the operation
            return new DragEventArgs(_dragData, 0, screenPoint.X, screenPoint.Y, allowedEffects, DragDropEffects.Move);
        }
    }
}
