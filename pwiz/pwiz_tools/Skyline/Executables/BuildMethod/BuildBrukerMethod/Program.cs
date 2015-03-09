using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BDAL.RetentionTimeMassList;

namespace BuildBrukerMethod
{
    internal class UsageException : Exception { }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = -1;  // Failure until success

                var builder = new BuildBrukerMethod();
                builder.ParseCommandArgs(args);
                builder.Build();

                Environment.ExitCode = 0;
            }
            catch (UsageException)
            {
                Usage();
            }
            catch (Exception x)
            {
                WriteError(x.Message);
            }
        }

        static void WriteError(string message)
        {
            var reader = new StringReader(message);
            string line;
            while ((line = reader.ReadLine()) != null)
                Console.Error.WriteLine("ERROR: {0}", line);
        }

        static void Usage()
        {
            const string usage =
                    "Usage: BuildBrukerMethod [options] <template method> [list file]*\n" +
                    "   Takes template Bruker method file and a Skyline generated Bruker\n" +
                    "   transition list as inputs, to generate a Bruker method\n" +
                    "   file as output.\n" +
                    "   -o <output file> New method is written to the specified output file\n" +
                    "   -s               Transition list is read from stdin.\n" +
                    "                    e.g. cat TranList.csv | BuildBrukerMethod -s -o new.ext temp.ext\n" +
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

    internal sealed class BuildBrukerMethod
    {
        private const string BrukerMethodExt = ".m";

        private string TemplateMethod { get; set; }

        private List<MethodTransitions> MethodTrans { get; set; }
        private CRetTimeMassListData CRetTimeMassListData { get; set; }

        public BuildBrukerMethod()
        {
            MethodTrans = new List<MethodTransitions>();
            CRetTimeMassListData = new CRetTimeMassListData
                {
                    MassRangeMode = CRetTimeMassListData.EMassRangeMode.INCLUDED,
                    RetTimeUnit = CRetTimeMassListData.ERetTimeUnit.MINUTES,
                    SingleMassWidth = 0.5,
                    Version = 3
                };
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
                Usage("Multi-file and specific output are not compatible.");

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

        private void ReadFile(string inputFile, string outputMethod, bool multiFile)
        {
            if (!multiFile && string.IsNullOrEmpty(outputMethod))
            {
                string methodFileName = Path.GetFileNameWithoutExtension(inputFile) + BrukerMethodExt;
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
            var sb = new StringBuilder();

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
                        throw new IOException(string.Format("Empty isolation list found."));

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

        public void Build()
        {
            foreach (var methodTranList in MethodTrans)
            {
                Console.Error.WriteLine("MESSAGE: Exporting method {0}", Path.GetFileName(methodTranList.FinalMethod));
                if (string.IsNullOrEmpty(methodTranList.TransitionList))
                    throw new IOException(string.Format("Failure creating method file {0}.  The transition list is empty.", methodTranList.FinalMethod));

                // Make sure the target output does not already exist as a file, because
                // Bruker writes its methods as directories, and normal Skyline behavior
                // is to write a temporary file.
                if (File.Exists(methodTranList.OutputMethod))
                {
                    try
                    {
                        File.Delete(methodTranList.OutputMethod);
                    }
                    catch (FileNotFoundException)
                    {
                    }
                }

                // Copy template method to new method
                CopyFolderContents(TemplateMethod, methodTranList.OutputMethod);

                IEnumerable<CRetTimeMass> cRetTimeMassList = GetCRetTimeMassList(methodTranList.TransitionList);
                foreach (var cRetTimeMass in cRetTimeMassList)
                    CRetTimeMassListData.RetTimeMassList.Add(cRetTimeMass);

                string errorText = string.Empty;
                string methodFile = CSubmethodProperties.GetFileName();
                bool success = CPersist.WriteDataToFile(methodTranList.OutputMethod, methodFile, CRetTimeMassListData, ref errorText);
                if (!success || !File.Exists(Path.Combine(methodTranList.OutputMethod, methodFile)))
                    throw new IOException(string.Format("Failure creating method file {0}. {1}", methodFile, errorText));

                // Skyline uses a segmented progress status, which expects 100% for each
                // segment, with one segment per file.
                Console.Error.WriteLine("100%");
            }
        }

        private void CopyFolderContents(string source, string destination)
        {
            string[] files = Directory.GetFiles(source);

            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            // Copy the files and overwrite destination files if they already exist. 
            foreach (string s in files)
            {
                // Use static Path methods to extract only the file name from the path.
                string fileName = Path.GetFileName(s);
                if (fileName != null)
                {
                    string destFile = Path.Combine(destination, fileName);
                    File.Copy(s, destFile, true);
                }
            }
        }

        private IEnumerable<CRetTimeMass> GetCRetTimeMassList(string csvData)
        {
            var cRetTimeMassList = new List<CRetTimeMass>();
            
            string[] lines = csvData.Split('\r', '\n');
            if (!lines.Any())
                throw new InvalidDataException("CSV data does not contain any lines");

            var columnMap = new Dictionary<int, string>();

            for (int i = 0; i < lines.Count(); ++i)
            {
                string[] fields = lines[i].Split(',');
                int numFields = fields.Count();
                if (i == 0)
                {
                    // Get headers
                    for (int j = 0; j < numFields; ++j)
                        columnMap.Add(j, fields[j]);
                }
                else
                {
                    // Skip empty lines
                    if (string.IsNullOrEmpty(lines[i]))
                        continue;

                    cRetTimeMassList.Add(GetCRetTimeMass(i + 1, fields, columnMap));
                }
            }

            return cRetTimeMassList;
        }

        private CRetTimeMass GetCRetTimeMass(int lineNum, string[] csvFields, Dictionary<int, string> columnMap)
        {
            int numFields = csvFields.Count();

            if (numFields != columnMap.Count)
                throw new InvalidDataException("CSV data contains different number of values than headers");

            // Get data
            uint? time = null, tolerance = null;
            double? massBegin = null, massEnd = null;
            for (int j = 0; j < numFields; ++j)
            {
                bool parseFail = false;
                if (string.Equals(columnMap[j], "ret time (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    double timeMinutes;
                    parseFail = !double.TryParse(csvFields[j], NumberStyles.Float, CultureInfo.InvariantCulture, out timeMinutes);
                    time = (uint) Math.Round(timeMinutes * 60 * 1000);
                }
                else if (string.Equals(columnMap[j], "tolerance", StringComparison.InvariantCultureIgnoreCase))
                {
                    double toleranceMinutes;
                    parseFail = !double.TryParse(csvFields[j], NumberStyles.Float, CultureInfo.InvariantCulture, out toleranceMinutes);
                    tolerance = (uint) Math.Round(toleranceMinutes * 60 * 1000);
                }
                else if (string.Equals(columnMap[j], "precursor ion min", StringComparison.InvariantCultureIgnoreCase))
                {
                    double mass;
                    parseFail = !double.TryParse(csvFields[j], NumberStyles.Float, CultureInfo.InvariantCulture, out mass);
                    massBegin = mass;
                }
                else if (string.Equals(columnMap[j], "precursor ion max", StringComparison.InvariantCultureIgnoreCase))
                {
                    double mass;
                    parseFail = !double.TryParse(csvFields[j], NumberStyles.Float, CultureInfo.InvariantCulture, out mass);
                    massEnd = mass;
                }

                if (parseFail)
                {
                    throw new SyntaxErrorException(
                        string.Format("Error parsing value '{0}' for '{1}' on line {2}",
                                      csvFields[j], columnMap[j], lineNum));
                }
            }

            if (!time.HasValue || !tolerance.HasValue || !massBegin.HasValue || !massEnd.HasValue)
                throw new InvalidDataException(string.Format("CSV data is missing a required field on line {0}", lineNum));

            return new CRetTimeMass(time.Value, tolerance.Value, massBegin.Value, massEnd.Value);
        }
    }
}
