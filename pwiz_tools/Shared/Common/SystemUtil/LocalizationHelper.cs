/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System;
using System.Globalization;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    public static class LocalizationHelper
    {
        static LocalizationHelper()
        {
            OriginalCulture = CurrentCulture = Thread.CurrentThread.CurrentCulture;
            OriginalUICulture = CurrentUICulture = Thread.CurrentThread.CurrentUICulture;
//            CurrentCulture = CurrentUICulture = new CultureInfo("ja");
        }

        public static void InitThread(string threadName = null)
        {
            InitThread(Thread.CurrentThread);
            if (threadName != null)
                Thread.CurrentThread.Name = threadName;
//            ConcurrencyVisualizer.AddThreadName();
        }

        public static void InitThread(Thread thread)
        {
            if (CurrentCulture != null)
                thread.CurrentCulture = CurrentCulture;
            thread.CurrentUICulture = CurrentUICulture ?? thread.CurrentCulture;
        }

        /// <summary>
        /// Stores the culture of the original thread of the application,
        /// which may be different from CurrentCulture, since it may be set in tests.
        /// </summary>
        public static CultureInfo OriginalCulture { get; private set; }

        public static CultureInfo OriginalUICulture { get; private set; }

        /// <summary>
        /// Culture used to initialize all threads.  Set to the culture of the
        /// original thread by default.  Can be set in tests.
        /// </summary>
        public static CultureInfo CurrentCulture { get; set; }

        public static CultureInfo CurrentUICulture { get; set; }

        public static T CallWithCulture<T>(CultureInfo cultureInfo, Func<T> func)
        {
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            var originalUiCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture = cultureInfo;
                return func();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalUiCulture;
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }
    }

    /// <summary>
    /// Sets CurrentThread.CurrentCulture for the duration of the object's lifetime (typically within a using() block),
    /// then restores it back to its original value
    /// </summary>
    public class CurrentCultureSetter : IDisposable
    {
        private CultureInfo PreviousCulture { get; }
        private CultureInfo PreviousUICulture { get; }

        public CurrentCultureSetter(CultureInfo newCultureInfo)
        {
            PreviousCulture = Thread.CurrentThread.CurrentCulture;
            PreviousUICulture = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentCulture = newCultureInfo;
            Thread.CurrentThread.CurrentUICulture = newCultureInfo;
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = PreviousCulture;
            Thread.CurrentThread.CurrentUICulture = PreviousUICulture;
        }
    }
}
