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

using System;
using System.Collections.Generic;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;

namespace Pwiz.SeeMS;

/// <summary>
/// Every run of one data file, one per sample of a multi-sample container such as a WIFF: what
/// cpp/CLI's ReaderList.read filled an MSDataList with. Disposing the list disposes the runs, which
/// releases the vendor file handles behind them.
/// </summary>
public sealed class MSDataList : List<MSData>, IDisposable
{
    /// <summary>Reads every run of <paramref name="path"/>.</summary>
    public static MSDataList Read(ReaderList readers, string path, ReaderConfig config)
    {
        var result = new MSDataList();
        try
        {
            int runCount = readers.IdentifyReader(path, null) is IMultiSampleReader multiSample
                ? Math.Max(1, multiSample.EnumerateSampleNames(path).Length)
                : 1;
            for (int run = 0; run < runCount; ++run)
            {
                var msd = new MSData();
                result.Add(msd);
                readers.Read(path, msd, run, config);
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>Disposes every run in the list.</summary>
    public void Dispose()
    {
        foreach (var msd in this)
            msd.Dispose();
        Clear();
    }
}
