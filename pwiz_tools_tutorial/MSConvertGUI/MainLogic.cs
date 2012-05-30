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
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using pwiz.CLI.util;

namespace MSConvertGUI
{
    public struct Config
    {
        public List<string> Filenames;
        public List<string> Filters;
        public string OutputPath;
        public string Extension;
        public MSDataFile.WriteConfig WriteConfig;
        public string ContactFilename;

        public Config(string outputPath)
        {
            OutputPath = outputPath;
            Filenames = new List<string>();
            Filters = new List<string>();
            Extension = string.Empty;
            ContactFilename = string.Empty;
            WriteConfig = new MSDataFile.WriteConfig();
        }

        public string outputFilename(string inputFilename, MSData inputMsData)
        {
            string runId = inputMsData.run.id;


            // if necessary, adjust runId so it makes a suitable filename
            if (String.IsNullOrEmpty(runId))
                runId = Path.GetFileNameWithoutExtension(inputFilename) ?? string.Empty;
            else
            {
                string tempExtension = (Path.GetExtension(runId) ?? string.Empty).ToLower();
                if (tempExtension == ".mzml" ||
                    tempExtension == ".mzxml" ||
                    tempExtension == ".xml" ||
                    tempExtension == ".mgf" ||
                    tempExtension == ".ms2" ||
                    tempExtension == ".cms2")
                    runId = Path.GetFileNameWithoutExtension(runId) ?? string.Empty;
            }

            // this list is for Windows; it's a superset of the POSIX list
            const string illegalFilename = "\\/*:?<>|\"";
            foreach (var t in illegalFilename)
                if (runId.Contains(t))
                    runId = runId.Replace(t, '_');

            var newFilename = runId + Extension;
            var fullPath = Path.Combine(OutputPath, newFilename);
            return fullPath;
        }
    } ;

    public class MainLogic : IterationListener
    {
        public delegate void PercentageDelegate(int value, int maxValue, ProgressForm.JobInfo info);
        public delegate void LogDelegate(string status, ProgressForm.JobInfo info);
        public delegate void StatusDelegate(string status, ProgressBarStyle style, ProgressForm.JobInfo info);

        public PercentageDelegate PercentageUpdate;
        public LogDelegate LogUpdate;
        public StatusDelegate StatusUpdate;

        private readonly ProgressForm.JobInfo _info;
        private string _errorMessage;
        bool _canceled;

        public MainLogic(ProgressForm.JobInfo info)
        {
            _info = info;
            _canceled = false;

            int numThreads = Environment.ProcessorCount;
            ThreadPool.SetMinThreads(numThreads, numThreads);
            ThreadPool.SetMaxThreads(numThreads, numThreads);
        }

        public Config ParseCommandLine(string outputFolder, string argv)
        {

            var config = new Config(outputFolder);
            string filelistFilename = null;
            string configFilename = null;

            var formatText = false;
            var formatMzMl = false;
            var formatMzXml = false;
            var formatMz5 = false;
            var formatMgf = false;
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
                + (formatMgf ? 1 : 0)
                + (formatMs2 ? 1 : 0)
                + (formatCms2 ? 1 : 0);
            if (count > 1) throw new Exception("[msconvert] Multiple format flags specified.");
            if (formatText) config.WriteConfig.format = MSDataFile.Format.Format_Text;
            if (formatMzMl) config.WriteConfig.format = MSDataFile.Format.Format_mzML;
            if (formatMzXml) config.WriteConfig.format = MSDataFile.Format.Format_mzXML;
            if (formatMz5) config.WriteConfig.format = MSDataFile.Format.Format_MZ5;
            if (formatMgf) config.WriteConfig.format = MSDataFile.Format.Format_MGF;
            if (formatMs2) config.WriteConfig.format = MSDataFile.Format.Format_MS2;
            if (formatCms2) config.WriteConfig.format = MSDataFile.Format.Format_CMS2;

            config.WriteConfig.gzipped = gzip; // if true, file is written as .gz

            if (String.IsNullOrEmpty(config.Extension))
            {
                switch (config.WriteConfig.format)
                {
                    case MSDataFile.Format.Format_Text:
                        config.Extension = ".txt";
                        break;
                    case MSDataFile.Format.Format_mzML:
                        config.Extension = ".mzML";
                        break;
                    case MSDataFile.Format.Format_mzXML:
                        config.Extension = ".mzXML";
                        break;
                    case MSDataFile.Format.Format_MZ5:
                        config.Extension = ".mz5";
                        break;
                    case MSDataFile.Format.Format_MGF:
                        config.Extension = ".mgf";
                        break;
                    case MSDataFile.Format.Format_MS2:
                        config.Extension = ".ms2";
                        break;
                    case MSDataFile.Format.Format_CMS2:
                        config.Extension = ".cms2";
                        break;
                    default:
                        throw new Exception("[msconvert] Unsupported format.");
                }
                if (config.WriteConfig.gzipped)
                {
                    config.Extension += ".gz";
                }
            }

            // precision defaults

            config.WriteConfig.precision = MSDataFile.Precision.Precision_64;

            // handle precision flags

            if (precision32 && precision64)
                throw new Exception("[msconvert] Incompatible precision flags.");

            if (precision32)
            {
                config.WriteConfig.precision = MSDataFile.Precision.Precision_32;
            }
            else if (precision64)
            {
                config.WriteConfig.precision = MSDataFile.Precision.Precision_64;
            }

            // other flags

            if (noindex)
                config.WriteConfig.indexed = false;

            if (zlib)
                config.WriteConfig.compression = MSDataFile.Compression.Compression_Zlib;

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
                    if (PercentageUpdate != null)
                        PercentageUpdate(updateMessage.iterationIndex + 1,
                                         updateMessage.iterationCount, _info);
                }
                return Status.Ok;
            }
            catch
            {
                return Status.Cancel;
            }
        }

        object calculateSHA1Mutex = new object();
        void processFile(string filename, Config config, ReaderList readers)
        {
            if (LogUpdate != null) LogUpdate("Opening file...", _info);
            if (StatusUpdate != null) StatusUpdate("Opening file...", ProgressBarStyle.Marquee, _info);

            // read in data file
            using (var msdList = new MSDataList())
            {
                readers.read(filename, msdList);

                foreach (var msd in msdList)
                {
                    var outputFilename = config.outputFilename(filename, msd);

                    if (filename == outputFilename)
                        throw new ArgumentException("Output filepath is the same as input filepath");

                    if (LogUpdate != null) LogUpdate("Calculating SHA1 checksum...", _info);
                    if (StatusUpdate != null) StatusUpdate("Calculating SHA1 checksum...", ProgressBarStyle.Marquee, _info);

                    // only one thread 
                    lock (calculateSHA1Mutex)
                        MSDataFile.calculateSHA1Checksums(msd);

                    if (LogUpdate != null) LogUpdate("Processing...", _info);
                    if (StatusUpdate != null) StatusUpdate("Processing...", ProgressBarStyle.Marquee, _info);

                    SpectrumListFactory.wrap(msd, config.Filters);
                    if (StatusUpdate != null && msd.run.spectrumList != null)
                        StatusUpdate(String.Format("Processing ({0} of {1})", 
                                                   DataGridViewProgressCell.MessageSpecialValue.CurrentValue,
                                                   DataGridViewProgressCell.MessageSpecialValue.Maximum),
                                     ProgressBarStyle.Continuous, _info);

                    // write out the new data file
                    IterationListenerRegistry ilr = null;
                    ilr = new IterationListenerRegistry();
                    ilr.addListener(this, 100);
                    MSDataFile.write(msd, outputFilename, config.WriteConfig, ilr);
                }
            }
        }

        /// <summary>
        /// Processes files, returns number of failed files
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        int Go(Config config)
        {
            var readers = ReaderList.FullReaderList;

            var failedFileCount = 0;
            foreach (var it in config.Filenames)
            {
                try
                {
                    processFile(it, config, readers);
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

        public void WorkAsync(Config config)
        {
            if (StatusUpdate != null) StatusUpdate("Waiting...", ProgressBarStyle.Continuous, _info);

            _canceled = false;
            ThreadPool.QueueUserWorkItem(Work, config);
        }

        public void Work(object state)
        {
            var worker = new Thread((x) =>
            {
                Config config = (Config) state;
                try
                {
                    var result = Go(config) == 0 ? 100 : -99;

                    if (result == 100)
                    {
                        if (LogUpdate != null) LogUpdate("Finished.", _info);
                        if (StatusUpdate != null) StatusUpdate("Finished", ProgressBarStyle.Continuous, _info);
                        if (PercentageUpdate != null) PercentageUpdate(100, 100, _info);
                    }
                    else
                    {
                        if (LogUpdate != null) LogUpdate("Failed - " + _errorMessage, _info);
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
            });
            worker.SetApartmentState(ApartmentState.STA);
            worker.Priority = ThreadPriority.BelowNormal;
            worker.Start();
            worker.Join();
        }
    }
}
