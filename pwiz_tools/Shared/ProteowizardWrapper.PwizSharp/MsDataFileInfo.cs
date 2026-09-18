// MsDataFileInfo — pwiz-sharp sandbox port. Same shape as the legacy
// pwiz.CLI-backed version: opens a file, runs a predicate on its MSData,
// closes it. Called by DiaUmpire.Config.GetConfigFromDiaUmpireOutput and
// other quick metadata probes.
using System;
using Pwiz.Data.MsData;

namespace pwiz.ProteowizardWrapper
{
    public class MsDataFileInfo
    {
        /// <summary>
        /// Open a file with pwiz-sharp, run a predicate on it, and close the file.
        /// </summary>
        public static T RunPredicate<T>(string filepath, Func<MSData, T> predicate, int sampleIndex = 0)
        {
            using var msd = new MSData();
            Pwiz.Data.MsData.Readers.ReaderList.Default.Read(filepath, msd);
            return predicate(msd);
        }
    }
}
