/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010-2011 University of Washington - Seattle, WA
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

namespace BuildAnalystMethod
{
    public class UsageException : Exception { }

    public sealed class MethodTransitions
    {
        public MethodTransitions(string outputMethod, string finalMethod, string transitionList)
        {
            OutputMethod = outputMethod;
            FinalMethod = finalMethod;
            TransitionList = transitionList;
            Transitions = CreateTransitionList().ToList();
        }

        public string OutputMethod { get; private set; }
        public string FinalMethod { get; private set; }
        public string TransitionList { get; private set; }

        public List<MethodTransition> Transitions { get; private set; }

        private IEnumerable<MethodTransition> CreateTransitionList()
        {
            var reader = new StringReader(TransitionList);
            string line;
            while ((line = reader.ReadLine()) != null)
                yield return new MethodTransition(line);
        }
    }

    public sealed class MethodTransition
    {
        public MethodTransition(string transitionListLine)
        {
            var values = transitionListLine.Split(',');
            if (values.Length < 6)
                throw new IOException("Invalid transition list format.  Each line must have 6 values.");
            try
            {
                int i = 0;
                PrecursorMz = double.Parse(values[i++], CultureInfo.InvariantCulture);
                ProductMz = double.Parse(values[i++], CultureInfo.InvariantCulture);
                Dwell = double.Parse(values[i++], CultureInfo.InvariantCulture);
                Label = values[i++];
                DP = double.Parse(values[i++], CultureInfo.InvariantCulture);
                CE = double.Parse(values[i++], CultureInfo.InvariantCulture);
                if (i < values.Length)
                    PrecursorWindow = string.IsNullOrEmpty(values[i]) ? (double?)null : double.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    ProductWindow = string.IsNullOrEmpty(values[i]) ? (double?)null : double.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    Group = values[i++];

                if (i < values.Length)
                    AveragePeakArea = string.IsNullOrEmpty(values[i]) ? (float?)null : float.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    VariableRtWindow = string.IsNullOrEmpty(values[i]) ? (double?)null : double.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    Threshold = string.IsNullOrEmpty(values[i]) ? (double?)null : double.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    Primary = string.IsNullOrEmpty(values[i]) ? 1 : int.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    CoV = string.IsNullOrEmpty(values[i]) ? 1 : double.Parse(values[i], CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new IOException("Invalid transition list format.  Failure parsing numeric value.");
            }
        }

        public double PrecursorMz { get; private set; }
        public double ProductMz { get; private set; }
        public double Dwell { get; private set; }
        public string Label { get; private set; }
        public double CE { get; private set; }
        public double DP { get; private set; }
        public double? PrecursorWindow { get; private set; }
        public double? ProductWindow { get; private set; }
        public double? Threshold { get; private set; }
        public int? Primary { get; private set; }
        public string Group { get; private set; }
        public float? AveragePeakArea { get; private set; }
        public double? VariableRtWindow { get; private set; }
        public double? CoV { get; private set; }

        public int ExperimentIndex { get; set; }
    }

    public abstract class BuildAnalystMethod
    {
        protected int? RTWindowInSeconds { get; set; }

        protected string TemplateMethod { get; set; }

        protected List<MethodTransitions> MethodTrans { get; set; }

        protected abstract string FileExtension { get; }

        protected BuildAnalystMethod()
        {
            MethodTrans = new List<MethodTransitions>();
        }

        public virtual void ParseCommandArgs(string[] args)
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

                ReadTransitions(Console.In, outputMethod);
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
                        ReadFile(inputFile, outputMethod, multiFile);
                    else
                    {
                        string dirName = Path.GetDirectoryName(filter) ?? ".";
                        foreach (var fileName in Directory.GetFiles(dirName, filter))
                        {
                            ReadFile(Path.Combine(dirName, fileName), null, multiFile);
                        }
                    }
                }
            }
        }

        protected static void Usage(string message)
        {
            Console.Error.WriteLine(message);
            Console.Error.WriteLine();
            Usage();
        }

        protected static void Usage()
        {
            throw new UsageException();
        }

        private void ReadFile(string inputFile, string outputMethod, bool multiFile)
        {
            if (!multiFile && string.IsNullOrEmpty(outputMethod))
            {
                string methodFileName = Path.GetFileNameWithoutExtension(inputFile) + FileExtension;
                string dirName = Path.GetDirectoryName(inputFile);
                outputMethod = (dirName != null ? Path.Combine(dirName, methodFileName) : inputFile);
            }

            using (var infile = new StreamReader(inputFile))
            {
                ReadTransitions(infile, outputMethod);
            }
        }

        private void ReadTransitions(TextReader instream, string outputMethod)
        {
            string outputMethodCurrent = outputMethod;
            string finalMethod = outputMethod;
            StringBuilder sb = new StringBuilder();

            string line;
            while ((line = instream.ReadLine()) != null)
            {
                line = line.Trim();

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
        }

        public abstract void build();
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
            File.Delete(Name);
        }
    }

}
