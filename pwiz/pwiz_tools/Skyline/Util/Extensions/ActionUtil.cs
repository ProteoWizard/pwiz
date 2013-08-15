/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Util.Extensions
{
    /// <summary>
    /// Utilities for running Actions.
    /// </summary>
    public static class ActionUtil
    {
        // Run async method, no arguments.

        /// <summary>
        /// Run an action asynchronously, with an optional callback.  This is preferred to
        /// calling BeginInvoke directly on the action, because then you have to worry about
        /// calling EndInvoke.  It's not optional, according to a number of articles on the
        /// web, like http://blog.aggregatedintelligence.com/2010/06/c-asynchronous-programming-using.html
        /// 
        /// It's easy to call a method with no arguments:  ActionUtil.RunAsync(MyMethod);
        /// For methods with arguments, use a lambda:  ActionUtil.RunAsync(() => MyMethodWithArgs(1, true));
        /// </summary>
        /// <param name="action">Action to be executed on a thread from the thread pool</param>
        /// <param name="callback">Optional Action to be executed when the first action is finished</param>
        public static void RunAsync(Action action, Action callback = null)
        {
            action.BeginInvoke(iar =>
            {
                action.EndInvoke(iar);  // required (see above)
                if (callback != null)
                    callback();
            }, null);
        }
    }
}
