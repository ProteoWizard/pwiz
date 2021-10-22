using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shimadzu.LabSolutions.MethodConverter;
using Shimadzu.LabSolutions.MethodWriter;

namespace BuildShimadzuMethod
{
    internal class UsageException : Exception { }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = -1;  // Failure until success

                var builder = new BuildShimadzuMethod();
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
                "Usage: BuildShimadzuMethod [options] <template method> [list file]*\n" +
                "   Takes template Shimadzu method file and a Skyline generated Shimadzu\n" +
                "   transition list as inputs, to generate a Shimadzu method\n" +
                "   file as output.\n" +
                "   -o <output file> New method is written to the specified output file\n" +
                "   -s               Transition list is read from stdin.\n" +
                "                    e.g. cat TranList.csv | BuildShimadzuMethod -s -o new.ext temp.ext\n" +
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

    internal sealed class BuildShimadzuMethod
    {
        private const string MethodExt = ".lcm";

        private string TemplateMethod { get; set; }
        private List<MethodTransitions> MethodTrans { get; set; }

        public BuildShimadzuMethod()
        {
            TemplateMethod = null;
            MethodTrans = new List<MethodTransitions>();
        }

        public void ParseCommandArgs(string[] args)
        {
            // Default to stdin for transition list input
            string outputMethod = null;
            bool readStdin = false;
            bool multiFile = false;

            int i = 0;
            while (i < args.Length && args[i].Length > 0 && args[i][0] == '-')
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

            var templateArg = args[i++];
            if (!string.IsNullOrWhiteSpace(templateArg))
                TemplateMethod = templateArg;

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
                string methodFileName = Path.GetFileNameWithoutExtension(inputFile) + MethodExt;
                string dirName = Path.GetDirectoryName(inputFile);
                outputMethod = dirName != null ? Path.Combine(dirName, methodFileName) : inputFile;
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
                        throw new IOException("Empty transition list found.");

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
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            foreach (var methodTranList in MethodTrans)
            {
                if (string.IsNullOrEmpty(TemplateMethod))
                {
                    var methodConverter = new MassMethodConverter();
                    var result = methodConverter.ConvertMethod(methodTranList.OutputMethod, methodTranList.TransitionList);
                    if (result != ConverterResult.OK)
                        throw new Exception(GetConverterErrorMessage(result));
                }
                else
                {
                    var outMeth = methodTranList.OutputMethod;
                    if (!outMeth.EndsWith(MethodExt))
                        outMeth = Path.ChangeExtension(outMeth, MethodExt);
                    try
                    {
                        // MethodWriter receives the template and overwrites it, so copy template to final output name
                        // The template is required to have .lcm extension
                        File.Copy(TemplateMethod, outMeth, true);
                    }
                    catch (Exception x)
                    {
                        throw new IOException($"Error copying template file {TemplateMethod} to destination {methodTranList.OutputMethod}: {x.Message}.");
                    }

                    var methodWriter = new MassMethodWriter();
                    var result = methodWriter.WriteMethod(outMeth, methodTranList.TransitionList);
                    if (result != WriterResult.OK)
                        throw new Exception(GetWriterErrorMessage(result));

                    if (!Equals(outMeth, methodTranList.OutputMethod))
                    {
                        File.Delete(methodTranList.OutputMethod);
                        File.Move(outMeth, methodTranList.OutputMethod);
                    }
                }
            }
        }

        private static string GetConverterErrorMessage(ConverterResult result)
        {
            switch (result)
            {
                case ConverterResult.OK:
                    return null;
                case ConverterResult.InputIsEmpty:
                    return "Input string is empty.";
                case ConverterResult.InputCannotBeParsed:
                    return "Input string cannot be parsed.";
                case ConverterResult.CannotOpenOutputFile:
                    return "Cannot open output file.";
                case ConverterResult.InvalidParameter:
                    return "Invalid parameter. Cannot create output method.";
                case ConverterResult.OutOfRangeEventNoError:
                    return "Number of events exceed maximum allowed by LabSolutions (1000).";
                case ConverterResult.EventNotContiguous:
                    return "Input events are not contiguous.";
                case ConverterResult.EventNotAscending:
                    return "Input events are not in ascending order.";
                case ConverterResult.MaxTransitionError:
                    return "The transition count exceeds the maximum allowed for this instrument type.";
                default:
                    return $"Unexpected response {result} from Shimadzu method converter.";
            }
        }

        private static string GetWriterErrorMessage(WriterResult result)
        {
            switch (result)
            {
                case WriterResult.OK:
                    return null;
                case WriterResult.InputIsEmpty:
                    return "Input string is empty.";
                case WriterResult.InputCannotBeParsed:
                    return "Input string cannot be parsed.";
                case WriterResult.OutputIsEmpty:
                    return "Output path is not specified.";
                case WriterResult.CannotOpenFile:
                    return "Cannot open output file.";
                case WriterResult.InvalidParameter:
                    return "Invalid parameter. Cannot create output method.";
                case WriterResult.UnsupportedFile:
                    return "Output file type is not supported.";
                case WriterResult.SerializeIOException:
                    return "Exception raised during output serialization.";
                case WriterResult.OutOfRangeEventNoError:
                    return "Number of events exceed the maximum allowed by LabSolutions (1000).";
                case WriterResult.OutputMethodEmpty:
                    return "Output method does not contain any events.";
                case WriterResult.EventNotContiguous:
                    return "Input events are not contiguous.";
                case WriterResult.EventNotAscending:
                    return "Input events are not in ascending order.";
                default:
                    return $"Unexpected response {result} from Shimadzu method writer.";
            }
        }
    }
}
