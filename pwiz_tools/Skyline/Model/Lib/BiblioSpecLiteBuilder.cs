/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using pwiz.BiblioSpec;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public sealed class BiblioSpecLiteBuilder : ILibraryBuilder
    {
        public const string EXT_PEP_XML = ".pep.xml";
        public const string EXT_PEP_XML_ONE_DOT = ".pepXML";
        public const string EXI_MZID = ".mzid";
        public const string EXT_IDP_XML = ".idpXML";
        public const string EXT_SQT = ".sqt";
        public const string EXT_DAT = ".dat";
        public const string EXT_SSL = ".ssl";
        public const string EXT_XTAN_XML = ".xtan.xml";
        public const string EXT_PILOT_XML = ".group.xml";
        public const string EXT_PERCOLATOR = ".perc.xml";
        public const string EXT_PERCOLATOR_XML = "results.xml";
        public const string EXT_WATERS_MSE = "final_fragment.csv";

        private ReadOnlyCollection<string> _inputFiles;

        public BiblioSpecLiteBuilder(string name, string outputPath, IList<string> inputFiles)
        {
            LibrarySpec = new BiblioSpecLiteSpec(name, outputPath);

            InputFiles = inputFiles;
        }

        public LibrarySpec LibrarySpec { get; private set; }
        public string OutputPath { get { return LibrarySpec.FilePath; } }

        public LibraryBuildAction Action { get; set; }
        public bool KeepRedundant { get; set; }
        public double? CutOffScore { get; set; }
        public string Authority { get; set; }
        public string Id { get; set; }

        public IList<string> InputFiles
        {
            get { return _inputFiles; }
            private set { _inputFiles = value as ReadOnlyCollection<string> ?? new ReadOnlyCollection<string>(value); }
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            string message = string.Format("Building {0} library", Path.GetFileName(OutputPath));
            ProgressStatus status = new ProgressStatus(message);

            progress.UpdateProgress(status);
            string redundantLibrary = BiblioSpecLiteSpec.GetRedundantName(OutputPath);
            var blibBuilder = new BlibBuild(redundantLibrary, InputFiles)
            {
                Authority = Authority,
                CutOffScore = CutOffScore,
                Id = Id,

            };
            try
            {
                if (!blibBuilder.BuildLibrary(Action, progress, ref status))
                {
                    return false;
                }
            }
            catch (IOException x)
            {
                progress.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch (Exception x)
            {
                Console.WriteLine(x.Message);
                progress.UpdateProgress(status.ChangeErrorException(new Exception(string.Format("Failed trying to build the redundant library {0}.", redundantLibrary))));
                return false;
            }
            var blibFilter = new BlibFilter();
            status = new ProgressStatus(message);
            progress.UpdateProgress(status);
            // Write the non-redundant library to a temporary file first
            try
            {
                using (var saver = new FileSaver(OutputPath))
                {
                    if (!blibFilter.Filter(redundantLibrary, saver.SafeName, progress, ref status))
                    {
                        return false;
                    }
                    saver.Commit();
                }
            }
            catch (IOException x)
            {
                progress.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch
            {
                progress.UpdateProgress(status.ChangeErrorException(new Exception(string.Format("Failed trying to build the library {0}.", OutputPath))));
                return false;
            }
            finally
            {
                if (!KeepRedundant)
                    File.Delete(redundantLibrary);
            }

            return true;
        }
    }
}
