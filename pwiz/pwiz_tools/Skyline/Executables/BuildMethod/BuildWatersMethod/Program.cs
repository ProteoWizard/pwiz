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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using VerifyESkylineLibrary;

namespace BuildWatersMethod
{
    internal class UsageException : Exception { }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = -1;  // Failure until success

                var builder = new BuildWatersMethod();
                builder.ParseCommandArgs(args);
                builder.build();

                Environment.ExitCode = 0;
            }
            catch (UsageException)
            {
                Usage();
            }
            catch (IOException x)
            {
                Console.Error.WriteLine("ERROR: {0}", x.Message);
            }
            catch (Exception x)
            {
                Console.Error.WriteLine("ERROR: {0}", x.Message);
            }

//            Console.WriteLine("Press any key to continue...");
//            Console.In.ReadLine();
        }

        static void Usage()
        {
            const string usage =
                    "Usage: BuildWatersMethod [options] <template method> [list file]*\n" +
                    "   Takes template Waters method file and a Skyline generated Waters\n" +
                    "   scheduled transition list as inputs, to generate a new Xevo or\n" +
                    "   Quattro Premier method file as output.\n" +
                    "   -x               Xevo method [default]\n" +
                    "   -q               Quattro Premier method\n" +
                    "   -d               Dwell time [default 0.05]\n" +
                    "   -w <RT window>   Retention time window\n" +
                    "   -o <output file> New method is written to the specified output file\n" +
                    "   -s               Transition list is read from stdin.\n" +
                    "                    e.g. cat TranList.csv | BuildWatersMethod -s -o new.ext temp.ext\n" +
                    "\n" +
                    "   -m               Multiple lists concatenated in the format:\n" +
                    "                    file1.ext\n" +
                    "                    <transition list>\n" +
                    "\n" +
                    "                    file2.ext\n" +
                    "                    <transition list>\n" +
                    "                    ...\n";
            Console.Error.Write(usage);
        }
    }

    internal sealed class MethodTransitions
    {
        public MethodTransitions(string outputMethod, string finalMethod, string transitionList)
        {
            OutputMethod = outputMethod;
            FinalMethod = finalMethod;
            TransitionList = transitionList;
        }

        public string OutputMethod { get; private set; }
        public string FinalMethod { get; private set; }
        public string TransitionList { get; private set; }
    }

    internal sealed class BuildWatersMethod
    {
        private const string WATERS_METHOD_EXT = ".exp";
        private const string INSTRUMENT_XEVO = "Xevo TQMS";
        private const string INSTRUMENT_QUATTRO_PREMIER = "Quattro Premier XE";

        private string InstrumentType { get; set; }

        private string TemplateMethod { get; set; }

        private int? RTWindow { get; set; }
        private double? DwellTime { get; set; }
        private int? DwellMode
        {
            get
            {
                if (DwellTime.HasValue)
                    return 1;
                return null;
            }
        }

        private List<MethodTransitions> MethodTrans { get; set; }

        public BuildWatersMethod()
        {
            InstrumentType = INSTRUMENT_XEVO;
            MethodTrans = new List<MethodTransitions>();
        }

        public void ParseCommandArgs(string[] args)
        {
            // Default to stdin for transition list input
            string outputMethod = null;
            bool readStdin = false;
            bool multiFile = false;

            int i = 0;
            while (i < args.Length && args[i][0] == '-')
            {
                switch (args[i++][1])
                {
                    case 'q':
                        InstrumentType = INSTRUMENT_QUATTRO_PREMIER;
                        break;
                    case 'x':
                        // Nothing to do, since Xevo is default
                        break;
                    case 'd':
                        try
                        {
                            DwellTime = double.Parse(args[i++], CultureInfo.InvariantCulture);
                        }
                        catch (Exception)
                        {
                            Usage(string.Format("The value {0} is not a valid dwell time.", args[i - 1]));
                        }
                        break;
                    case 'w':
                        try
                        {
                            RTWindow = (int)Math.Round(double.Parse(args[i++], CultureInfo.InvariantCulture));
                        }
                        catch (Exception)
                        {
                            Usage(string.Format("The value {0} is not a retention time window.", args[i - 1]));
                        }
                        break;

                    case 'o':
                        if (i >= args.Length)
                            throw new UsageException();
                        outputMethod = Path.GetFullPath(args[i++]);
                        break;
                    case 's':
                        readStdin = true;
                        break;
                    case 'm':
                        multiFile = true;
                        break;
                    default:
                        throw new UsageException();
                }
            }

            if (multiFile && !string.IsNullOrEmpty(outputMethod))
                Usage("Multi-file and specific output are not compatibile.");

            int argcLeft = args.Length - i;
            if (argcLeft < 1 || (!readStdin && argcLeft < 2))
                Usage();

            TemplateMethod = Path.GetFullPath(args[i++]);

            // Read input into a list of lists of fields
            if (readStdin)
            {
                if (!multiFile && string.IsNullOrEmpty(outputMethod))
                    Usage("Reading from standard in without multi-file format must specify an output file.");

                readTransitions(Console.In, outputMethod);
            }
            else
            {
                for (; i < args.Length; i++)
                {
                    string inputFile = Path.GetFullPath(args[i]);
                    string filter = null;
                    if (inputFile.Contains('*'))
                        filter = Path.GetFileName(inputFile);
                    else if (Directory.Exists(inputFile))
                        filter = "*.csv";

                    if (string.IsNullOrEmpty(filter))
                        readFile(inputFile, outputMethod, multiFile);
                    else
                    {
                        string dirName = Path.GetDirectoryName(filter) ?? ".";
                        foreach (var fileName in Directory.GetFiles(dirName, filter))
                        {
                            readFile(Path.Combine(dirName, fileName), null, multiFile);
                        }
                    }
                }
            }
        }

        private static void Usage(string message)
        {
            Console.Error.WriteLine(message);
            Console.Error.WriteLine();
            Usage();
        }

        private static void Usage()
        {
            throw new UsageException();
        }

        private void readFile(string inputFile, string outputMethod, bool multiFile)
        {
            if (!multiFile && string.IsNullOrEmpty(outputMethod))
            {
                string methodFileName = Path.GetFileNameWithoutExtension(inputFile) + WATERS_METHOD_EXT;
                string dirName = Path.GetDirectoryName(inputFile);
                outputMethod = (dirName != null ? Path.Combine(dirName, methodFileName) : inputFile);
            }

            using (var infile = new StreamReader(inputFile))
            {
                readTransitions(infile, outputMethod);
            }
        }

        private void readTransitions(TextReader instream, string outputMethod)
        {
            string outputMethodCurrent = outputMethod;
            string finalMethod = outputMethod;
            StringBuilder sb = new StringBuilder();

            string line;
            while ((line = instream.ReadLine()) != null)
            {
                line = line.Trim();
//                if (line.StartsWith("protein.name,"))
//                    continue;

                if (string.IsNullOrEmpty(outputMethodCurrent))
                {
                    if (!string.IsNullOrEmpty(outputMethod))
                    {
                        // Only one file, if outputMethod specified
                        throw new IOException(string.Format("Failure creating method file {0}. Transition lists may not contain blank lines.", outputMethod));
                    }

                    // Read output file path from a line in the file
                    outputMethodCurrent = line;
                    finalMethod = instream.ReadLine();
                    if (finalMethod == null)
                        throw new IOException(string.Format("Empty transition list found."));
                    
                    sb = new StringBuilder();
                }
                else if (string.IsNullOrEmpty(line))
                {
                    MethodTrans.Add(new MethodTransitions(outputMethodCurrent, finalMethod, sb.ToString()));
                    outputMethodCurrent = null;
                }
                else
                {
                    sb.AppendLine(line);
                }
            }

            // Add the last method, if there is one
            if (!string.IsNullOrEmpty(outputMethodCurrent))
            {
                MethodTrans.Add(new MethodTransitions(outputMethodCurrent, finalMethod, sb.ToString()));
            }

            // Read remaining contents of stream, in case it is stdin
            while (instream.ReadLine() != null)
            {
            }
        }

        public void build()
        {
            foreach (var methodTranList in MethodTrans)
            {
                Console.Error.WriteLine(string.Format("MESSAGE: Exporting method {0}", Path.GetFileName(methodTranList.FinalMethod)));
                if (string.IsNullOrEmpty(methodTranList.TransitionList))
                    throw new IOException(string.Format("Failure creating method file {0}.  The transition list is empty.", methodTranList.FinalMethod));

                OperationManager.GenerateMRMMethodFromString(methodTranList.TransitionList,
                    methodTranList.OutputMethod,
                    TemplateMethod,
                    InstrumentType,
                    RTWindow,
                    DwellMode,
                    DwellTime);

                if (!File.Exists(methodTranList.OutputMethod))
                    throw new IOException(string.Format("Failure creating method file {0}.", methodTranList.FinalMethod));
            }
        }
    }

    internal sealed class TempFile : IDisposable
    {
        public TempFile()
        {
            Name = Path.GetTempFileName();
        }

        public string Name { get; private set; }

        public void Dispose()
        {
            // VerifyESkylineLibrary.dll locks the CSV file.
//            File.Delete(Name);
        }
    }
}

