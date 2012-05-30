/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Linq;

namespace TestRunner
{
    class CommandLineArgs
    {
        public static bool HasArg(string argName)
        {
            return _originalArgs.Contains(argName);
        }

        public static bool ArgAsBool(string argName)
        {
            switch (_args[argName].ToLower())
            {
                case "on":
                case "true":
                case "1":
                    return true;

                case "off":
                case "false":
                case "0":
                    return false;

                default:
                    throw new ArgumentException(string.Format("\"{0}\" is not a legal boolean value for argument \"{1}\"", _args[argName], argName));
            }
        }

        public static string ArgAsString(string argName)
        {
            return _args[argName];
        }

        public static long ArgAsLong(string argName)
        {
            return Convert.ToInt64(_args[argName]);
        }

        public static double ArgAsDouble(string argName)
        {
            return Convert.ToDouble(_args[argName]);
        }

        public static string CommandLine
        {
            get
            {
                return "TestRunner " + _originalArgs.Aggregate("", (current, arg) => current + (arg + " "));
            }
        }

        public static void ParseArgs(string[] args, string defaultArgs)
        {
            _originalArgs = new List<string>();
            foreach (var arg in args)
            {
                _originalArgs.Add(arg.Split('=')[0]);
            }

            _args = new Dictionary<string, string>();
            ParseDefaults(defaultArgs);

            foreach (var words in args.Select(arg => arg.Split('=')))
            {
                if (!_args.ContainsKey(words[0]))
                {
                    throw new ArgumentException(string.Format(@"Unrecognized argument: {0}. Run ""TestRunner help"" to see a list of arguments.", words[0]));
                }

                // overwrite default value
                if (words.Length == 2)
                {
                    _args[words[0]] = words[1];
                }
            }
        }

        private static void ParseDefaults(string defaultArgs)
        {
            if (defaultArgs == "") return;
            var args = defaultArgs.Split(';');

            foreach (var words in args.Select(arg => arg.Split('=')))
            {
                _args[words[0]] = (words.Length == 1) ? "" : words[1];
            }
        }

        public static string SearchArgs(string argList)
        {
            return argList.Split(';').FirstOrDefault(HasArg);
        }

        private static Dictionary<string, string> _args;
        private static List<string> _originalArgs;
    }
}
