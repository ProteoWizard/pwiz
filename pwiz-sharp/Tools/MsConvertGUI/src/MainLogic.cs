//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CustomProgressCell;
// pwiz.CLI.msdata / pwiz.CLI.analysis / pwiz.CLI.util types map to pwiz-sharp's equivalents:
// MSData / MSDataFile / WriteConfig / WriteFormat / BinaryPrecision / BinaryCompression /
// BinaryNumpress live in Pwiz.Data.MsData. ReaderConfig / ReaderList / IterationListener /
// SpectrumListFactory are MSConvertGUI shims (Compat.cs) that wrap their pwiz-sharp
// counterparts where the cpp-CLI surface needs a thin compatibility layer.
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Encoding;
using System.Text.RegularExpressions;

namespace MSConvertGUI
{
    public struct Config
    {
        public List<string> Filenames;
        public List<string> Filters;
        public string OutputPath;
        public string Extension;
        public WriteConfig WriteConfig;
        public ReaderConfig ReaderConfig;
        public string ContactFilename;

        public Config(string outputPath)
        {
            OutputPath = outputPath;
            Filenames = new List<string>();
            Filters = new List<string>();
            Extension = string.Empty;
            ContactFilename = string.Empty;
            WriteConfig = new WriteConfig();
            ReaderConfig = new ReaderConfig();
        }

        public string outputFilename(string inputFilename, Pwiz.Data.MsData.MSData inputMsData)
        {
            string runId = inputMsData.Run.Id;

            try
            {
                // if necessary, adjust runId so it makes a suitable filename
                if (String.IsNullOrEmpty(runId))
                    runId = Path.GetFileNameWithoutExtension(inputFilename) ?? string.Empty;
                else
                {
                    string extension = (Path.GetExtension(runId) ?? string.Empty).ToLower();
                    if (extension == ".mzml" ||
                        extension == ".mzxml" ||
                        extension == ".xml" ||
                        extension == ".mgf" ||
                        extension == ".ms1" ||
                        extension == ".cms1" ||
                        extension == ".ms2" ||
                        extension == ".cms2" ||
                        extension == ".mz5")
                        runId = Path.GetFileNameWithoutExtension(runId) ?? string.Empty;
                }

                // this list is for Windows; it's a superset of the POSIX list
                const string illegalFilename = "\\/*:?<>|\"";
                runId = new string(runId.Select(t => (t < 0x20 || t == 0x7f || illegalFilename.Contains(t)) ? '_' : t).ToArray());

                var newFilename = runId + Extension;
                var fullPath = Path.Combine(OutputPath, newFilename);
                return fullPath;
            }
            catch(ArgumentException e)
            {
                throw new ArgumentException(String.Format("error generating output filename for input file '{0}' and output run id '{1}'", inputFilename, runId), e);
            }
        }
    }

    public class MainLogic : IterationListener
    {
        public delegate void PercentageDelegate(int value, int maxValue, ProgressForm.JobInfo info);
        public delegate void LogDelegate(string status, ProgressForm.JobInfo info);
        public delegate void StatusDelegate(string status, ProgressBarStyle style, ProgressForm.JobInfo info);

        public PercentageDelegate PercentageUpdate;
        public LogDelegate LogUpdate;
        public StatusDelegate StatusUpdate;

        public bool Canceled => _canceled;

        public ProgressForm.JobInfo JobInfo { get; }
        private string _errorMessage;
        bool _canceled;
        private Map<string, int> _usedOutputFilenames;
        private object _calculateSHA1Mutex;

        static Queue<KeyValuePair<MainLogic, Config>> _workQueue = new Queue<KeyValuePair<MainLogic, Config>>();

        public MainLogic(ProgressForm.JobInfo jobInfo, Map<string, int> usedOutputFilenames, object calculateSHA1Mutex)
        {
            JobInfo = jobInfo;
            _canceled = false;
            _usedOutputFilenames = usedOutputFilenames;
            _calculateSHA1Mutex = calculateSHA1Mutex;
        }

        public Config ParseCommandLine(string outputFolder, string argv)
        {

            var config = new Config(outputFolder);
            string filelistFilename = null;
            string configFilename = null;

            var formatText = false;
            var formatMzMl = false;
            var formatMzMlB = false;
            var formatMzXml = false;
            var formatMz5 = false;
            var formatMgf = false;
            var formatMs1 = false;
            var formatCms1 = false;
            var formatMs2 = false;
            var formatCms2 = false;
            var precision32 = false;
            var precision64 = false;
            var noindex = false;
            var zlib = false;
            var gzip = false;

            var commandList = argv.Split('|');

            for (var x = 0; x < commandList.Length; x++)
            {
                switch (commandList[x])
                {
                    case "--filelist":
                    case "-f":
                        x++;
                        filelistFilename = commandList[x];
                        break;
                    case "--config":
                    case "-c":
                        x++;
                        configFilename = commandList[x];
                        break;
                    case "--ext":
                    case "-e":
                        x++;
                        config.Extension = commandList[x];
                        break;
                    case "--mzML":
                        formatMzMl = true;
                        break;
                    case "--mzMLb":
                        formatMzMlB = true;
                        break;
                    case "--mzXML":
                        formatMzXml = true;
                        break;
                    case "--mz5":
                        formatMz5 = true;
                        break;
                    case "--mgf":
                        formatMgf = true;
                        break;
                    case "--text":
                        formatText = true;
                        break;
                    case "--ms1":
                        formatMs1 = true;
                        break;
                    case "--cms1":
                        formatCms1 = true;
                        break;
                    case "--ms2":
                        formatMs2 = true;
                        break;
                    case "--cms2":
                        formatCms2 = true;
                        break;
                    case "--64":
                        precision64 = true;
                        break;
                    case "--32":
                        precision32 = true;
                        break;
                    case "--noindex":
                        noindex = true;
                        break;
                    case "--contactinfo":
                    case "-i":
                        x++;
                        config.ContactFilename = commandList[x];
                        break;
                    case "--zlib":
                    case "-z":
                        zlib = true;
                        break;
                    case "--gzip":
                    case "-g":
                        gzip = true;
                        break;
                    case "--filter":
                        x++;
                        config.Filters.Add(commandList[x]);
                        break;
                    case "--numpressPic":
                        config.WriteConfig.EncoderConfig.NumpressOverrides[CVID.MS_intensity_array] = BinaryNumpress.Pic;
                        break;
                    case "--numpressLinear":
                        config.WriteConfig.EncoderConfig.NumpressOverrides[CVID.MS_m_z_array] = BinaryNumpress.Linear;
                        break;
                    case "--numpressSlof":
                        // cpp/CLI had a typo here that set numpressLinear; corrected for the
                        // pwiz-sharp port (Slof maps to intensity, matching the cpp msconvert CLI).
                        config.WriteConfig.EncoderConfig.NumpressOverrides[CVID.MS_intensity_array] = BinaryNumpress.Slof;
                        break;
                    case "--combineIonMobilitySpectra":
                        config.ReaderConfig.combineIonMobilitySpectra = true;
                        break;
                    case "--simAsSpectra":
                        config.ReaderConfig.simAsSpectra = true;
                        break;
                    case "--srmAsSpectra":
                        config.ReaderConfig.srmAsSpectra = true;
                        break;
                    case "--ddaProcessing":
                        config.ReaderConfig.ddaProcessing = true;
                        break;
                    default:
                        config.Filenames.Add(commandList[x]);
                        break;
                }


            }

            #region Parse config file if required

            if (!string.IsNullOrEmpty(configFilename))
            {
                var fileIn = new StreamReader(configFilename);
                var entirefile = fileIn.ReadToEnd().Replace('=', ' ').Replace(Environment.NewLine, " ");
                fileIn.Close();

                commandList = entirefile.Split();

                for (var x = 0; x < commandList.Length; x++)
                {
                    switch (commandList[x])
                    {
                        case "--filelist":
                        case "-f":
                            x++;
                            filelistFilename = commandList[x];
                            break;
                        case "--outdir":
                        case "-o":
                            x++;
                            config.OutputPath = commandList[x];
                            break;
                        case "--ext":
                        case "-e":
                            x++;
                            config.Extension = commandList[x];
                            break;
                        case "--mzML":
                            formatMzMl = true;
                            break;
                        case "--mzXML":
                            formatMzXml = true;
                            break;
                        case "--mgf":
                            formatMgf = true;
                            break;
                        case "--text":
                            formatText = true;
                            break;
                        case "--ms1":
                            formatMs1 = true;
                            break;
                        case "--cms1":
                            formatCms1 = true;
                            break;
                        case "--ms2":
                            formatMs2 = true;
                            break;
                        case "--cms2":
                            formatCms2 = true;
                            break;
                        case "--64":
                            precision64 = true;
                            break;
                        case "--32":
                            precision32 = true;
                            break;
                        case "--noindex":
                            noindex = true;
                            break;
                        case "--contactinfo":
                        case "-i":
                            x++;
                            config.ContactFilename = commandList[x];
                            break;
                        case "--zlib":
                        case "-z":
                            zlib = true;
                            break;
                        case "--gzip":
                        case "-g":
                            gzip = true;
                            break;
                        case "--filter":
                            x++;
                            config.Filters.Add(commandList[x]);
                            break;
                    }


                }
            }
            #endregion


            // parse filelist if required

            if (!string.IsNullOrEmpty(filelistFilename))
            {
                var fileIn = new StreamReader(filelistFilename);
                while (!fileIn.EndOfStream)
                {
                    var filename = fileIn.ReadLine();
                    if (!string.IsNullOrEmpty(filename))
                        config.Filenames.Add(filename);
                }
                fileIn.Close();
            }

            // check stuff

            if (config.Filenames.Count == 0)
                throw new Exception("[msconvert] No files specified.");

            var count = (formatText ? 1 : 0) 
                + (formatMzMl ? 1 : 0)
                + (formatMzXml ? 1 : 0)
                + (formatMz5 ? 1 : 0)
                + (formatMzMlB ? 1 : 0)
                + (formatMgf ? 1 : 0)
                + (formatMs1 ? 1 : 0)
                + (formatCms1 ? 1 : 0)
                + (formatMs2 ? 1 : 0)
                + (formatCms2 ? 1 : 0);
            if (count > 1) throw new Exception("[msconvert] Multiple format flags specified.");
            if (formatText) config.WriteConfig.Format = WriteFormat.Text;
            if (formatMzMl) config.WriteConfig.Format = WriteFormat.Mzml;
            if (formatMzXml) config.WriteConfig.Format = WriteFormat.MzXml;
            if (formatMz5) config.WriteConfig.Format = WriteFormat.Mz5;
            if (formatMzMlB) config.WriteConfig.Format = WriteFormat.MzMLb;
            if (formatMgf) config.WriteConfig.Format = WriteFormat.Mgf;
            if (formatMs1) config.WriteConfig.Format = WriteFormat.Ms1;
            if (formatCms1) config.WriteConfig.Format = WriteFormat.Cms1;
            if (formatMs2) config.WriteConfig.Format = WriteFormat.Ms2;
            if (formatCms2) config.WriteConfig.Format = WriteFormat.Cms2;

            config.WriteConfig.Gzip = gzip; // if true, file is written as .gz

            if (String.IsNullOrEmpty(config.Extension))
            {
                switch (config.WriteConfig.Format)
                {
                    case WriteFormat.Text:
                        config.Extension = ".txt";
                        break;
                    case WriteFormat.Mzml:
                        config.Extension = ".mzML";
                        break;
                    case WriteFormat.MzXml:
                        config.Extension = ".mzXML";
                        break;
                    case WriteFormat.Mz5:
                        config.Extension = ".mz5";
                        break;
                    case WriteFormat.MzMLb:
                        config.Extension = ".mzMLb";
                        break;    
                    case WriteFormat.Mgf:
                        config.Extension = ".mgf";
                        break;
                    case WriteFormat.Ms1:
                        config.Extension = ".ms1";
                        break;
                    case WriteFormat.Cms1:
                        config.Extension = ".cms1";
                        break;
                    case WriteFormat.Ms2:
                        config.Extension = ".ms2";
                        break;
                    case WriteFormat.Cms2:
                        config.Extension = ".cms2";
                        break;
                    default:
                        throw new Exception("[msconvert] Unsupported format.");
                }
                if (config.WriteConfig.Gzip)
                {
                    config.Extension += ".gz";
                }
            }

            // precision defaults

            config.WriteConfig.EncoderConfig.Precision = BinaryPrecision.Bits64;

            // handle precision flags

            if (precision32 && precision64)
                throw new Exception("[msconvert] Incompatible precision flags.");

            if (precision32)
            {
                config.WriteConfig.EncoderConfig.Precision = BinaryPrecision.Bits32;
            }
            else if (precision64)
            {
                config.WriteConfig.EncoderConfig.Precision = BinaryPrecision.Bits64;
            }

            // other flags

            if (noindex)
                config.WriteConfig.Indexed = false;

            if (zlib)
            {
                config.WriteConfig.EncoderConfig.Compression = BinaryCompression.Zlib;
                config.WriteConfig.MzMLbCompressionLevel = 4;
            }
            else
            {
                config.WriteConfig.EncoderConfig.Compression = BinaryCompression.None;
                config.WriteConfig.MzMLbCompressionLevel = 0;
            }

            return config;
        }

        public override Status update (UpdateMessage updateMessage)
        {
            try
            {
                if (_canceled)
                    return Status.Cancel;

                if (updateMessage.iterationCount > 10) //prevent false low-number invocations
                {
                    /*if (LogUpdate != null)
                        LogUpdate(String.Format("Processing spectra {0} of {1}",
                                                updateMessage.iterationIndex + 1,
                                                updateMessage.iterationCount), _info);*/
                    if (updateMessage.message.Any())
                        StatusUpdate?.Invoke(String.Format("{0}: {1}/{2}", updateMessage.message, updateMessage.iterationIndex + 1, updateMessage.iterationCount), ProgressBarStyle.Continuous, JobInfo);
                    else
                        StatusUpdate?.Invoke(String.Format("{1}/{2}", updateMessage.message, updateMessage.iterationIndex + 1, updateMessage.iterationCount), ProgressBarStyle.Continuous, JobInfo);
                    PercentageUpdate?.Invoke(updateMessage.iterationIndex + 1, updateMessage.iterationCount, JobInfo);
                }
                return Status.Ok;
            }
            catch
            {
                return Status.Cancel;
            }
        }

        void processFile(string filename, Config config, ReaderList readers, Map<string, int> usedOutputFilenames)
        {
            // read in data file
            using (var msdList = new MSDataList())
            {
                string msg = String.Format("Opening file \"{0}\" for read...", filename);
                var stripCredentialsMatch = Regex.Match(filename, "https?://([^:]+:[^@]+@).*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                if (stripCredentialsMatch.Success)
                    msg = msg.Replace(stripCredentialsMatch.Groups[1].Value, "");

                LogUpdate?.Invoke(msg, JobInfo);
                StatusUpdate?.Invoke(msg, ProgressBarStyle.Marquee, JobInfo);
                readers.read(filename, msdList, config.ReaderConfig);

                foreach (var msd in msdList)
                {
                    try
                    {
                        var outputFilename = config.outputFilename(filename, msd);
                        string deduplicatedFilename = outputFilename;

                        StatusUpdate?.Invoke("Waiting...", ProgressBarStyle.Marquee, JobInfo);

                        // only one thread 
                        lock (_calculateSHA1Mutex)
                        {
                            // if output name is same as input name, add a suffix
                            if (filename == outputFilename)
                                ++usedOutputFilenames[outputFilename];

                            if (usedOutputFilenames.Contains(deduplicatedFilename))
                                deduplicatedFilename = deduplicatedFilename.Replace(Path.GetExtension(outputFilename), String.Format(" ({0}).{1}",  usedOutputFilenames[outputFilename] + 1, Path.GetExtension(outputFilename)));
                            ++usedOutputFilenames[outputFilename];

                            LogUpdate?.Invoke("Calculating SHA1 checksum...", JobInfo);
                            StatusUpdate?.Invoke("Calculating SHA1 checksum...", ProgressBarStyle.Marquee, JobInfo);
                            MSDataFile.CalculateSha1Checksums(msd);
                        }

                        var ilr = new IterationListenerRegistry();
                        ilr.addListenerWithTimer(this, 1);

                        LogUpdate?.Invoke("Processing...", JobInfo);
                        StatusUpdate?.Invoke("Processing...", ProgressBarStyle.Marquee, JobInfo);

                        SpectrumListFactory.wrap(msd, config.Filters, ilr);

                        // pwiz-sharp writers are single-threaded today; cpp's useWorkerThreads
                        // toggle has no equivalent. (The WriteConfig field doesn't exist on
                        // pwiz-sharp's WriteConfig either — match the surface.)

                        if ((msd.Run.SpectrumList == null) || msd.Run.SpectrumList.Count == 0)
                        {
                            if ((msd.Run.ChromatogramList != null) && msd.Run.ChromatogramList.Count > 0)
                            {
                                msg = "Note: input contains only chromatogram data.";
                                switch (config.WriteConfig.Format)
                                {
                                    case WriteFormat.Mz5:
                                    case WriteFormat.MzMLb:
                                    case WriteFormat.Mzml:
                                        break;
                                    default:
                                        msg += "  The selected output format can only represent spectra.  Consider using mzML instead.";
                                        break;
                                }
                            }
                            else
                                msg = "Note: input contains no spectra or chromatogram data.";
                            LogUpdate?.Invoke(msg, JobInfo);
                            StatusUpdate?.Invoke(msg, ProgressBarStyle.Continuous, JobInfo);
                        }

                        if (StatusUpdate != null && msd.Run.SpectrumList != null)
                            StatusUpdate(String.Format("Processing ({0} of {1})", 
                                                       DataGridViewProgressCell.MessageSpecialValue.CurrentValue,
                                                       DataGridViewProgressCell.MessageSpecialValue.Maximum),
                                         ProgressBarStyle.Continuous, JobInfo);

                        // write out the new data file
                        msg = String.Format("Writing \"{0}\"...", deduplicatedFilename);
                        LogUpdate?.Invoke(msg, JobInfo);
                        StatusUpdate?.Invoke(msg, ProgressBarStyle.Continuous, JobInfo);
                        MSDataFile.Write(msd, deduplicatedFilename, config.WriteConfig, ilr.Inner);
                        ilr.removeListener(this);
                    }
                    finally
                    {
                        msd.Dispose();
                    }
                }
            }
        }


        public static string[] ReadIds(string path)
        {
            return ReaderList.readIds(path);
        }

        /// <summary>
        /// Processes files, returns number of failed files
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        int Go(Config config, Map<string, int> usedOutputFilenames)
        {
            var readers = ReaderList.FullReaderList;

            var failedFileCount = 0;
            foreach (var it in config.Filenames)
            {
                try
                {
                    processFile(it, config, readers, usedOutputFilenames);
                }
                catch (Exception e)
                {
                    _errorMessage = e.ToString();
                    failedFileCount++;
                    if (LogUpdate == null)
                        throw;
                }
            }

            return failedFileCount;
        }

        public void ForceExit ()
        {
            _canceled = true;
        }

        public void QueueWork(Config config)
        {
            if (StatusUpdate != null) StatusUpdate("Waiting...", ProgressBarStyle.Continuous, JobInfo);

            _canceled = false;

            lock (_workQueue)
                _workQueue.Enqueue(new KeyValuePair<MainLogic, Config>(this, config));
        }

        public static void RunQueue()
        {
            var workThreads = new List<Thread>();
            for (int i = 0; i < Math.Min(Properties.Settings.Default.NumFilesToConvertInParallel, Environment.ProcessorCount); ++i)
            {
                var thread = new Thread(Work) {Priority = ThreadPriority.BelowNormal};
                thread.SetApartmentState(ApartmentState.STA);
                workThreads.Add(thread);
            }
            workThreads.ForEach(o => o.Start());
        }

        public static void ClearQueue()
        {
            lock (_workQueue)
                _workQueue.Clear();
        }

        public static void Work()
        {
            while (true)
            {
                KeyValuePair<MainLogic, Config> item;
                lock (_workQueue)
                {
                    if (!_workQueue.Any())
                        return;
                    item = _workQueue.Dequeue();
                    if (item.Key.Canceled)
                        return;
                }

                MainLogic logic = item.Key;
                Config config = item.Value;
                var LogUpdate = logic.LogUpdate;
                var StatusUpdate = logic.StatusUpdate;
                var PercentageUpdate = logic.PercentageUpdate;
                var _info = logic.JobInfo;
                _info.Started = true;

                try
                {
                    var result = logic.Go(config, logic._usedOutputFilenames) == 0 ? 100 : -99;

                    if (result == 100)
                    {
                        if (LogUpdate != null) LogUpdate("Finished.", _info);
                        if (StatusUpdate != null) StatusUpdate("Finished", ProgressBarStyle.Continuous, _info);
                        if (PercentageUpdate != null) PercentageUpdate(100, 100, _info);
                    }
                    else
                    {
                        if (LogUpdate != null) LogUpdate("Failed - " + logic._errorMessage, _info);
                        if (StatusUpdate != null) StatusUpdate("Failed", ProgressBarStyle.Continuous, _info);
                        if (PercentageUpdate != null) PercentageUpdate(-99, 100, _info);
                    }
                }
                catch (Exception e)
                {
                    try
                    {
                        if (LogUpdate != null) LogUpdate("Failed - " + e, _info);
                        if (StatusUpdate != null) StatusUpdate("Failed", ProgressBarStyle.Continuous, _info);
                        if (PercentageUpdate != null) PercentageUpdate(-99, 100, _info);
                    }
                    catch (Exception)
                    {
                        //probably nothing left to report to
                    }
                }

                _info.Finished = true;
            }
        }
    }
}
