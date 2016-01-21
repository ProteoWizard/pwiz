using System;
using System.Collections.Generic;
using System.IO;

namespace ImportPerf
{
    class CommandArgs
    {
        private const string ARG_THREADS = "threads";
        private const string ARG_PROCESSES = "processes";
        private const string ARG_UI = "ui";
        private const string ARG_ACG = "acg";
        private const string ARG_SKYLINE_PATH = "skyline-path";
        private const string ARG_FILE_PATH = "file-path";
        private const string ARG_DATA_DIR = "data-dir";
        private const string ARG_DATA_FILTER = "data-filter";

        public int Threads { get; private set; }
        public int Processes { get; private set; }
        public string SkylinePath { get; private set; }
        public string FilePath { get; private set; }
        public string DataDir { get; private set; }
        public string DataFilter { get; private set; }

        private enum AcgState { show, hide, none }

        private bool IsUi { get; set; }
        private AcgState Acg { get; set; }

        public string[] UiArgs
        {
            get
            {
                if (!IsUi)
                    return new string[0];
                var args = new List<string> {"--ui"};
                switch (Acg)
                {
                    case AcgState.hide:
                        args.Add("--hideacg");
                        break;
                    case AcgState.none:
                        args.Add("--noacg");
                        break;
                }
                return args.ToArray();
            }
        }

        public CommandArgs()
        {
            Threads = 1;
            Processes = 1;
        }

        public bool ParseArgs(string[] args)
        {
            try
            {
                return ParseArgsInternal(args);
            }
            catch (UsageException x)
            {
                Console.Error.WriteLine("Error: {0}", x.Message); // Not L10N
                return false;
            }
            catch (Exception x)
            {
                // Unexpected behavior, but better to output the error then appear to crash, and
                // have Windows write it to the application event log.
                Console.Error.WriteLine("Error: {0}", x.Message); // Not L10N
                Console.Error.WriteLine(x.StackTrace);
                return false;
            }
        }

        private bool ParseArgsInternal(IEnumerable<string> args)
        {
            foreach (string s in args)
            {
                var pair = new NameValuePair(s);
                if (string.IsNullOrEmpty(pair.Name))
                    continue;

                if (IsNameOnly(pair, ARG_UI))
                {
                    IsUi = true;
                }
                else if (IsNameValue(pair, ARG_ACG))
                {
                    if (pair.Value.ToLower() == AcgState.hide.ToString())
                        Acg = AcgState.hide;
                    else if (pair.Value.ToLower() == AcgState.none.ToString())
                        Acg = AcgState.none;
                    else if (pair.Value.ToLower() == AcgState.show.ToString())
                        Acg = AcgState.show;
                    else
                    {
                        Console.Error.WriteLine("Error: The value {0} for --{1} is not valid", pair.Value, pair.Name);
                    }
                }
                else if (IsNameValue(pair, ARG_THREADS))
                {
                    int threads;
                    if (!int.TryParse(pair.Value, out threads))
                    {
                        Console.Error.WriteLine("Error: The value {0} for --{1} must be an integer", pair.Value, pair.Name); // Not L10N
                        return false;
                    }
                    Threads = threads;
                }
                else if (IsNameValue(pair, ARG_PROCESSES)) // Not L10N
                {
                    int processes;
                    if (!int.TryParse(pair.Value, out processes))
                    {
                        Console.Error.WriteLine("Error: The value {0} for --{1} must be an integer", pair.Value, pair.Name); // Not L10N
                        return false;
                    }
                    Processes = processes;
                }
                else if (IsNameValue(pair, ARG_SKYLINE_PATH))
                {
                    SkylinePath = pair.Value;
                }
                else if (IsNameValue(pair, ARG_FILE_PATH))
                {
                    FilePath = pair.Value;
                }
                else if (IsNameValue(pair, ARG_DATA_DIR))
                {
                    DataDir = pair.Value;
                }
                else if (IsNameValue(pair, ARG_DATA_FILTER))
                {
                    DataFilter = pair.Value;
                }
                else
                {
                    Console.Error.WriteLine("Error: Unexpected argument --{0}", pair.Name); // Not L10N
                    return false;
                }
            }

            if (string.IsNullOrEmpty(SkylinePath))
            {
                SkylinePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SkylineDailyRunner.exe");
            }

            return true;
        }

        private bool IsNameOnly(NameValuePair pair, string name)
        {
            if (!pair.Name.Equals(name))
                return false;
            if (!string.IsNullOrEmpty(pair.Value))
                throw new ValueUnexpectedException(name);
            return true;
        }

        private static bool IsNameValue(NameValuePair pair, string name)
        {
            if (!pair.Name.Equals(name))
                return false;
            if (string.IsNullOrEmpty(pair.Value))
                throw new ValueMissingException(name);
            return true;
        }

        // ReSharper disable once UnusedMember.Local
        private static bool IsName(NameValuePair pair, string name)
        {
            return pair.Name.Equals(name);
        }

        private class ValueMissingException : UsageException
        {
            public ValueMissingException(string name)
                : base(string.Format("The argument --{0} requires a value and must be specified in the format --{0}=value", name)) // Not L10N
            {
            }
        }

        private class ValueUnexpectedException : UsageException
        {
            public ValueUnexpectedException(string name)
                : base(string.Format("The argument --{0} should not have a value specified", name)) // Not L10N
            {
            }
        }

        private class UsageException : ArgumentException
        {
            protected UsageException(string message)
                : base(message)
            {
            }
        }

        public struct NameValuePair
        {
            public NameValuePair(string arg)
                : this()
            {
                if (arg.StartsWith("--")) // Not L10N
                {
                    arg = arg.Substring(2);
                    int indexEqualsSign = arg.IndexOf('=');
                    if (indexEqualsSign >= 0)
                    {
                        Name = arg.Substring(0, indexEqualsSign);
                        Value = arg.Substring(indexEqualsSign + 1);
                    }
                    else
                    {
                        Name = arg;
                    }
                }
            }

            public string Name { get; private set; }
            public string Value { get; private set; }
            public int ValueInt { get { return int.Parse(Value); } }
            public double ValueDouble { get { return double.Parse(Value); } }
        }
    }
}
