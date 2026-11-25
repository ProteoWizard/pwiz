/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;

namespace pwiz.Skyline.Util
{
    // How a grid edit changed many cells at once (DataGridViewPasteHandler, in the Skyline UI),
    // as the audit log records it
    public enum BatchModifyAction { Paste, Clear, FillDown }

    public class BatchModifyInfo : AuditLogOperationSettings<BatchModifyInfo> // TODO: this is a little lazy, consider rewriting
    {
        public BatchModifyInfo(BatchModifyAction batchModifyAction, string viewName, RowFilter rowFilter, string extraInfo = null)
        {
            BatchModifyAction = batchModifyAction;
            ViewName = viewName;
            Filter = rowFilter;
            ExtraInfo = extraInfo;
        }

        public BatchModifyAction BatchModifyAction { get; private set; }
        [Track(defaultValues: typeof(DefaultValuesNull))]
        public string ViewName { get; private set; }
        [TrackChildren]
        public RowFilter Filter { get; private set; }
        public string ExtraInfo { get; private set; }
    }
}
