using System;
using System.IO;
using pwiz.ProteowizardWrapper;

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
namespace pwiz.PwizConvert
{
    /// <summary>
    /// Opens a mass spec data file using ProteoWizard, and writes
    /// its data to mzML.  For use with Skyline to keep problematic vendor
    /// file readers from loading in the Skyline process.  Files can be
    /// first converted to mzML running this process, imported into Skyline,
    /// and then the mzML deleted.
    /// <para>
    /// Because this application uses pwiz_bindings_cli.dll, it is much
    /// smaller than using msConvert, which uses the ProteoWizard static
    /// library.</para>
    /// </summary>
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (1 > args.Length || args.Length > 2)
            {
                Console.Error.WriteLine("Usage: PwizConvert <input file> [output file]");
                Console.Error.WriteLine("       Converts a recognized mass spec data file to mzML");
                return;
            }

            // CONSIDER: If Skyline ever uses this for multi-sample files, it will need
            //           to support converting a single sample by index.
            string inputFile = args[0];
            string outputFile = args.Length > 1 ? args[1] : Path.ChangeExtension(inputFile, ".mzML");

            try
            {
                var dataFiles = MsDataFileImpl.ReadAll(inputFile);
                if (dataFiles.Length == 1)
                    dataFiles[0].Write(outputFile);
                else
                {
                    foreach (var dataFile in dataFiles)
                    {
                        dataFile.Write(Path.ChangeExtension(outputFile, dataFile.RunId + Path.GetExtension(outputFile)));
                    }
                }
            }
            catch (Exception x)
            {
                Console.Error.WriteLine("ERROR: " + x.Message);
            }
        }
    }
}
