/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
namespace pwiz.Common.SystemUtil
{
    public enum UpdateProgressResponse { normal, cancel, option1, option2 }

    /// <summary>
    /// Use this interface to provide a progress sink for a long operation,
    /// and let the operation know, if it has been cancelled.  Usually used
    /// with status bar progress for background operations.
    /// </summary>
    public interface IProgressMonitor
    {
        /// <summary>
        /// True if the load has been cancelled.
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// Reports updated <see cref="ProgressStatus"/> on a library load.
        /// </summary>
        /// <param name="status">The new status</param>
        UpdateProgressResponse UpdateProgress(ProgressStatus status);

        /// <summary>
        /// True if this progress monitor has a user interface.
        /// </summary>
        bool HasUI { get; }
    }
}