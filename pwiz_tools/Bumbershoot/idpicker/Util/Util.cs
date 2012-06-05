//
// $Id: ExtensionMethods.cs 393 2012-02-17 23:02:13Z chambm $
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s): Brendan MacLean <brendanx .at. u.washington.edu>,
//

using System;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;

namespace IDPicker
{
    public static partial class Util
    {
        /// <summary>
        /// Try an action that might throw an exception. If it does, sleep for a little while and
        /// try the action one more time. This oddity is necessary because certain file system
        /// operations (like moving a directory) can fail due to temporary file locks held by
        /// anti-virus software.
        /// </summary>
        /// <typeparam name="TEx">type of exception to catch</typeparam>
        /// <param name="action">action to try</param>
        /// <param name="repeatCount">how many times to retry the action</param>
        /// <param name="repeatDelay">how long (in milliseconds) to wait before the action is retried</param>
        public static void TryRepeatedly<TEx>(Action action, int repeatCount, int repeatDelay) where TEx : Exception
        {
            for (int i=0; i < repeatCount; ++i)
                try
                {
                    action();
                    return;
                }
                catch (TEx)
                {
                    Thread.Sleep(repeatDelay);
                    if (i + 1 == repeatCount)
                        throw;
                }
        }

        /// <summary>
        /// Try an action the might throw an Exception. If it fails, sleep for 500 milliseconds and try
        /// again. See the comments above for more detail about why this is necessary.
        /// </summary>
        /// <param name="action">action to try</param>
        public static void TryRepeatedly (Action action) { TryRepeatedly<Exception>(action, 2, 500); }
    }
}
