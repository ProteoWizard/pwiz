/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.IO;
using System.Reflection;

namespace pwiz.Common.SystemUtil
{
    public static class ParallelEx
    {
        // This can be set to true to make debugging easier.
        public static readonly bool SINGLE_THREADED = true;

        private class IntHolder
        {
            public IntHolder(int theInt)
            {
                TheInt = theInt;
            }

            public int TheInt { get; private set; }
        }

        public static int GetThreadCount(int? maxThreads = null)
        {
            if (SINGLE_THREADED)
                return 1;
            int threadCount = Environment.ProcessorCount;
            int maxThreadCount = maxThreads ?? 8; // Trial with maximum of 8
            if (threadCount > maxThreadCount)
                threadCount = maxThreadCount;
            return threadCount;
        }

        public static void For(int fromInclusive, int toExclusive, Action<int> body, Action<AggregateException> catchClause = null, int? maxThreads = null)
        {
            Action<int> localBody = i =>
            {
                LocalizationHelper.InitThread(); // Ensure appropriate culture
                body(i);
            };
            LoopWithExceptionHandling(() =>
            {
                using (var worker = new QueueWorker<IntHolder>(null, (h, i) => localBody(h.TheInt)))
                {
                    worker.RunAsync(GetThreadCount(maxThreads), typeof(ParallelEx).Name);
                    for (int i = fromInclusive; i < toExclusive; i++)
                    {
                        if (worker.Exception != null)
                            break;
                        worker.Add(new IntHolder(i));
                    }
                    worker.DoneAdding(true);
                    if (worker.Exception != null)
                        throw new AggregateException(@"Exception in Parallel.For", worker.Exception);   
                }
            }, catchClause);
//            LoopWithExceptionHandling(() => Parallel.For(fromInclusive, toExclusive, PARALLEL_OPTIONS, localBody), catchClause);
        }

        public static void ForEach<TSource>(IEnumerable<TSource> source, Action<TSource> body, Action<AggregateException> catchClause = null, int? maxThreads = null) where TSource : class
        {
            Action<TSource> localBody = o =>
            {
                LocalizationHelper.InitThread(); // Ensure appropriate culture
                body(o);
            };
            LoopWithExceptionHandling(() =>
            {
                using (var worker = new QueueWorker<TSource>(null, (s, i) => localBody(s)))
                {
                    worker.RunAsync(GetThreadCount(maxThreads), typeof(ParallelEx).Name);
                    foreach (TSource s in source)
                    {
                        if (worker.Exception != null)
                            break;
                        worker.Add(s);
                    }
                    worker.DoneAdding(true);
                    if (worker.Exception != null)
                        throw new AggregateException(@"Exception in Parallel.ForEx", worker.Exception); 
                }
            }, catchClause);
//            LoopWithExceptionHandling(() => Parallel.ForEach(source, PARALLEL_OPTIONS, localBody), catchClause);
        }

        private static void LoopWithExceptionHandling(Action loop, Action<AggregateException> catchClause)
        {
            try
            {
                loop();
            }
            catch (AggregateException x)
            {
                Exception ex = null;
                x.Handle(inner =>
                {
                    if (inner is OperationCanceledException)
                    {
                        if (!(ex is OperationCanceledException))
                            ex = inner;
                        return true;
                    }
                    if (catchClause == null)
                    {
                        if (ex == null)
                            ex = inner;
                        return true;
                    }
                    return false;
                });

                if (ex != null)
                {
                    // The thrown exception needs to be preserved to preserve
                    // the original stack trace from which it was thrown.  In some cases,
                    // its type must also be preserved, because existing code handles certain
                    // exception types.  If this case threw only TargetInvocationException,
                    // then more frequently the code would just have to have a blanket catch
                    // of the base exception type, which could hide coding errors.
                    if (ex is InvalidDataException)
                        throw new InvalidDataException(ex.Message, ex);
                    if (ex is IOException)
                        throw new IOException(ex.Message, ex);
                    if (ex is OperationCanceledException)
                        throw new OperationCanceledException(ex.Message, ex);
                    throw new TargetInvocationException(ex.Message, ex);
                }
                if (catchClause != null)
                    catchClause(x);
            }
        }
    }
}