// Compat shim for the MSConvertGUI port. The cpp/CLI bindings (pwiz.CLI.msdata,
// pwiz.CLI.analysis, pwiz.CLI.util, pwiz.Common.Collections) exposed types under
// names + casing that don't match pwiz-sharp directly. Re-exposing them under the
// MSConvertGUI namespace lets MainForm / MainLogic / ProgressForm compile with
// minimal edits — only what's genuinely .NET-8-specific or pwiz-sharp-specific
// needs to change inline.
//
// The wrapping is intentionally thin: each shim forwards to a single pwiz-sharp
// type. No new behavior lives here.
#pragma warning disable CS1591  // shim types intentionally undocumented

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PwizMsd = Pwiz.Data.MsData;
using PwizReaders = Pwiz.Data.MsData.Readers;
using PwizAnalysis = Pwiz.Analysis;
using PwizMisc = Pwiz.Util.Misc;

namespace MSConvertGUI
{
    // ------------------------------------------------------------------------------------
    // pwiz.CLI.msdata
    // ------------------------------------------------------------------------------------

    /// <summary>Disposable list of MSData instances. cpp/CLI <c>MSDataList</c> exposed both
    /// the list-of-runs surface (multi-sample WIFFs) and IDisposable. Pwiz-sharp's reader
    /// only fills one run per call, but the list shape is the API the GUI port expects.</summary>
    public sealed class MSDataList : List<PwizMsd.MSData>, IDisposable
    {
        public void Dispose() { foreach (var m in this) m?.Dispose(); Clear(); }
    }

    // MSDataFile + WriteConfig + WriteFormat + BinaryPrecision/Compression/Numpress are
    // supplied by Pwiz.Data.MsData directly — no Compat shim. MainLogic.cs writes via
    // PwizMsd.MSDataFile.Write(...) and constructs WriteConfig instances natively.


    /// <summary>cpp/CLI <c>ReaderConfig</c> — lowercase fields that GUI MainLogic writes to.</summary>
    public sealed class ReaderConfig
    {
        public bool simAsSpectra;
        public bool srmAsSpectra;
        public bool combineIonMobilitySpectra;
        public bool ddaProcessing;
        public bool ignoreCalibrationScans;
        public bool acceptZeroLengthSpectra;
        public bool ignoreMissingZeroSamples;

        public PwizReaders.ReaderConfig ToPwizSharp() => new()
        {
            SimAsSpectra = simAsSpectra,
            SrmAsSpectra = srmAsSpectra,
            CombineIonMobilitySpectra = combineIonMobilitySpectra,
            DdaProcessing = ddaProcessing,
            IgnoreCalibrationScans = ignoreCalibrationScans,
            AcceptZeroLengthSpectra = acceptZeroLengthSpectra,
            // cpp names the option --ignoreMissingZeroSamples but stores it in
            // Reader::Config::ignoreZeroIntensityPoints (msconvert.cpp:453-454); pwiz-sharp
            // keeps the cpp field name, so the shim has to bridge the two spellings.
            IgnoreZeroIntensityPoints = ignoreMissingZeroSamples,
        };
    }

    /// <summary>cpp/CLI <c>ReaderList</c> — wraps pwiz-sharp's <c>Pwiz.Data.MsData.Readers.ReaderList</c>
    /// and provides the static <see cref="FullReaderList"/> singleton MSConvertGUI uses
    /// to identify and read vendor files.</summary>
    public sealed class ReaderList
    {
        private readonly PwizReaders.ReaderList _inner;

        private ReaderList(PwizReaders.ReaderList inner) { _inner = inner; }

        private static ReaderList _fullReaderList;
        public static ReaderList FullReaderList => _fullReaderList ??= BuildDefault();

        private static ReaderList BuildDefault()
        {
            var rl = Pwiz.Vendor.Thermo.ThermoReaderRegistration.CreateDefaultWithThermo();
            rl.Add(new Pwiz.Vendor.Bruker.Reader_Bruker());
            rl.Add(new Pwiz.Vendor.Waters.Reader_Waters());
            rl.Add(new Pwiz.Vendor.Agilent.Reader_Agilent());
            rl.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
            rl.Add(new Pwiz.Vendor.Shimadzu.Reader_Shimadzu());
            rl.Add(new Pwiz.Vendor.UIMF.Reader_UIMF());
            rl.Add(new Pwiz.Vendor.UNIFI.Reader_UNIFI());
            rl.Add(new Pwiz.Vendor.Mobilion.Reader_Mobilion());
            return new ReaderList(rl);
        }

        public string identify(string filename)
        {
            try
            {
                var cv = _inner.Identify(filename, head: null);
                if (cv == Pwiz.Data.Common.Cv.CVID.CVID_Unknown) return string.Empty;
                return Pwiz.Data.Common.Cv.CvLookup.CvTermInfo(cv).Name;
            }
            catch { return string.Empty; }
        }

        /// <summary>Every run of the file, one per sample of a multi-sample WIFF, as cpp/CLI read it.</summary>
        public void read(string filename, MSDataList result, ReaderConfig config)
        {
            _inner.Read(filename, result, config?.ToPwizSharp());
        }

        /// <summary>cpp/CLI exposes file-extension grouping for the open-data-source dialog.
        /// We synthesize a sensible default from each reader's static extension list.</summary>
        public Dictionary<string, IList<string>> getFileExtensionsByType()
        {
            var map = new Dictionary<string, IList<string>>();
            foreach (var r in _inner.Readers)
            {
                if (r.FileExtensions is null || r.FileExtensions.Count == 0) continue;
                map[r.TypeName] = r.FileExtensions.ToList();
            }
            return map;
        }

        public static string[] readIds(string path) => FullReaderList._inner.ReadIds(path);
    }

    // ------------------------------------------------------------------------------------
    // pwiz.CLI.util
    // ------------------------------------------------------------------------------------

    /// <summary>Base class mirroring cpp/CLI's abstract <c>IterationListener</c> with a virtual
    /// <c>update(UpdateMessage)</c>. MainLogic overrides this. Internally we adapt to
    /// <see cref="PwizMisc.IIterationListener"/> via a forwarding subclass.</summary>
    public abstract class IterationListener : PwizMisc.IIterationListener
    {
        public enum Status { Ok, Cancel }
        public sealed class UpdateMessage
        {
            public int iterationIndex;
            public int iterationCount;
            public string message;
        }
        public virtual Status update(UpdateMessage updateMessage) => Status.Ok;

        // Bridge: pwiz-sharp delivers `IterationUpdate` records; translate.
        PwizMisc.IterationStatus PwizMisc.IIterationListener.Update(PwizMisc.IterationUpdate message)
        {
            var msg = new UpdateMessage
            {
                iterationIndex = message.IterationIndex,
                iterationCount = message.IterationCount,
                message = message.Message,
            };
            return update(msg) == Status.Ok ? PwizMisc.IterationStatus.Ok : PwizMisc.IterationStatus.Cancel;
        }
    }

    /// <summary>cpp/CLI <c>IterationListenerRegistry</c> — thin wrapper around pwiz-sharp's
    /// type so the case-sensitive method names MainLogic uses (lowercase) keep working.</summary>
    public sealed class IterationListenerRegistry
    {
        private readonly PwizMisc.IterationListenerRegistry _inner = new();
        public PwizMisc.IterationListenerRegistry Inner => _inner;
        public void addListenerWithTimer(IterationListener listener, int periodSeconds)
            => _inner.AddListenerWithTimer(listener, TimeSpan.FromSeconds(periodSeconds));
        public void removeListener(IterationListener listener) => _inner.RemoveListener(listener);
    }

    // ------------------------------------------------------------------------------------
    // pwiz.CLI.analysis
    // ------------------------------------------------------------------------------------

    public static class SpectrumListFactory
    {
        public static void wrap(PwizMsd.MSData msd, IList<string> filters, IterationListenerRegistry ilr = null)
        {
            if (msd is null || filters is null || filters.Count == 0) return;
            // The MSData-shaped overload threads run context to filters that need it
            // (mzRefiner, turbocharger, etc.) and bookkeeps DataProcessing.
            PwizAnalysis.SpectrumListFactory.Wrap(msd, filters);
        }
    }

    // pwiz.Common.Collections.Map<K,V> — supplied by the StlContainers project as
    // System.Collections.Generic.Map<K,V> (same RBTree-backed shape as the cpp/CLI binding).
    // No shim here; MainLogic / MainForm pick it up via `using System.Collections.Generic`.
}

#pragma warning restore CS1591
