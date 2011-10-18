/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using AcqMethodSvrLib;
using BuildAnalystMethod;
using MSMethodSvrLib;
using ParameterSvrLib;

namespace BuildQTRAPMethod
{
    internal class UsageException : Exception { }

    class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = -1;  // Failure until success

                var builder = new BuildQtrapMethod();
                var listArgs = new List<string>(args);
                builder.ParseCommandArgs(listArgs);
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
                    "Usage: BuildQTRAPMethod [options] <template method> [list file]*\n" +
                    "   Takes template QTRAP method file and a Skyline generated QTRAP\n" +
                    "   transition list as inputs, to generate a new QTRAP method file\n" +
                    "   as output.\n" +
                    "   -w <RT window>   Retention time window for schedule [unscheduled otherwise]\n" +
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


    class BuildQtrapMethod : BuildAnalystMethod.BuildAnalystMethod
    {

        private int? RTWindow { get; set; }

        public void ParseCommandArgs(List<string> args)
        {
            var args2 = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                if (arg.Length < 2)
                    continue;
                switch (arg.Substring(1, arg.Length - 1))
                {
                    case "w":
                        try
                        {
                            RTWindow = (int)Math.Round(double.Parse(args[i++], CultureInfo.InvariantCulture));
                        }
                        catch (Exception)
                        {
                            Usage(string.Format("The value {0} is not a retention time window.", args[i - 1]));
                        }
                        break;
                    default:
                        args2.Add(arg);
                        break;
                }
            }

            ParseCommandArgs(args2.ToArray());
            
        }

        public override void ValidateMethod(MassSpecMethod method)
        {
            var msPeriod = (Period)method.GetPeriod(0);
            if (msPeriod.ExperimCount != 1)
                throw new IOException(string.Format("Invalid template method {0}.  Expecting only one experiment.", TemplateMethod));
            var msExperiment = (Experiment)msPeriod.GetExperiment(0);
            var experimentType = msExperiment.ScanType;
            if (experimentType != 4)
                throw new IOException(string.Format("Invalid template method {0}.  Experiment type must be MRM.", TemplateMethod));

            var msExperiment7 = (IExperiment7)msExperiment;
            if (RTWindow.HasValue)
            {
                if (msExperiment7.KnownRetentionTimes == 0)
                    throw new IOException(string.Format("Invalid template method {0}.  Template does not support scheduled MRM.", TemplateMethod));
            }
            else
            {
                if (msExperiment7.KnownRetentionTimes != 0)
                    throw new IOException(string.Format("Invalid template method {0}.  Template is for scheduled MRM.", TemplateMethod));
            }
        }


        public override void WriteToTemplate(IAcqMethod acqMethod, MethodTransitions transitions)
        {
         
            var method = ExtractMsMethod(acqMethod);
            var period = (Period)method.GetPeriod(0);
            var msExperiment = (Experiment)period.GetExperiment(0);

            msExperiment.DeleteAllMasses();

            foreach (var transition in transitions.Transitions)
            {
                int i;
                var msMassRange = (MassRange) msExperiment.CreateMassRange(out i);
                var msMassRange3 = (IMassRange3) msMassRange;

                msMassRange.SetMassRange(transition.PrecursorMz, 0, transition.ProductMz);
                msMassRange.DwellTime = transition.Dwell;
                msMassRange3.CompoundID = transition.Label;
                var massRangeParams = (ParamDataColl) msMassRange.MassDepParamTbl;
                short s;
                massRangeParams.Description = transition.Label;
                massRangeParams.AddSetParameter("DP", (float) transition.DP, (float) transition.DP, 0, out s);
                massRangeParams.AddSetParameter("EP", 10, 10, 0, out s);
                massRangeParams.AddSetParameter("CE", (float) transition.CE, (float) transition.CE, 0, out s);
                massRangeParams.AddSetParameter("CXP", 15, 15, 0, out s);
            }

            acqMethod.SaveAcqMethodToFile(transitions.OutputMethod, 1);
            
        }

    }
}

