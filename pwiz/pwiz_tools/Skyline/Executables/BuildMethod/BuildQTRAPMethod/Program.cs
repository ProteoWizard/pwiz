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
using Interop.AcqMethodSvr;
using Interop.Analyst;
using BuildAnalystMethod;
using Interop.MSMethodSvr;
using Interop.ParameterSvr;

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

        protected override string FileExtension
        {
            get
            {
                return ".dam";
            }
        }

        public override void ParseCommandArgs(string[] args)
        {
            var listArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.Length < 2)
                    continue;
                switch (arg.Substring(1, arg.Length - 1))
                {
                    case "w":
                        i++;
                        if (i < args.Length)
                            arg = args[i];
                        else
                            Usage("Retention time expected after -w.");

                        try
                        {
                            RTWindow = (int)Math.Round(double.Parse(arg, CultureInfo.InvariantCulture));
                        }
                        catch (Exception)
                        {
                            Usage(string.Format("The value {0} is not a retention time window.", arg));
                        }
                        break;
                    default:
                        listArgs.Add(arg);
                        break;
                }
            }

            base.ParseCommandArgs(listArgs.ToArray());
            
        }

        public override void build()
        {
            MassSpecMethod templateMsMethod;

            IAcqMethod templateAcqMethod = GetAcqMethod(TemplateMethod, out templateMsMethod);


            ValidateMethod(templateMsMethod);


            foreach (var methodTranList in MethodTrans)
            {
                Console.Error.WriteLine(string.Format("MESSAGE: Exporting method {0}", Path.GetFileName(methodTranList.FinalMethod)));

                if (string.IsNullOrEmpty(methodTranList.TransitionList))
                    throw new IOException(string.Format("Failure creating method file {0}.  The transition list is empty.", methodTranList.FinalMethod));

                try
                {
                    WriteToTemplate(templateAcqMethod, methodTranList);

                }
                catch (Exception x)
                {
                    throw new IOException(string.Format("Failure creating method file {0}.  {1}", methodTranList.FinalMethod, x.Message));
                }

                if (!File.Exists(methodTranList.OutputMethod))
                    throw new IOException(string.Format("Failure creating method file {0}.", methodTranList.FinalMethod));

                // Skyline uses a segmented progress status, which expects 100% for each
                // segment, with one segment per file.
                Console.Error.WriteLine("100%");
            }
        }

        internal static IAcqMethod GetAcqMethod(string methodFilePath, out MassSpecMethod templateMsMethod)
        {
            ApplicationClass analyst = new ApplicationClass();

            // Make sure that Analyst is fully started
            IAcqMethodDirConfig acqMethodDir = (IAcqMethodDirConfig)analyst.Acquire();
            if (acqMethodDir == null)
                throw new IOException("Failed to initialize.  Analyst may need to be started.");

            object acqMethodObj;
            acqMethodDir.LoadNonUIMethod(methodFilePath, out acqMethodObj);
            IAcqMethod templateAcqMethod = (IAcqMethod)acqMethodObj;

            templateMsMethod = ExtractMsMethod(templateAcqMethod);
            return templateAcqMethod;
        }

        internal static MassSpecMethod ExtractMsMethod(IAcqMethod dataAcqMethod)
        {
            if (dataAcqMethod == null)
                return null;

            const int kMsMethodDeviceType = 0; // device type for MassSpecMethod
            int devType; // device type
            EnumDeviceMethods enumMethod = (EnumDeviceMethods)dataAcqMethod;
            enumMethod.Reset();
            do
            {
                object devMethod; // one of the sub-methods
                int devModel; // device model
                int devInst; // device instrument type
                enumMethod.Next(out devMethod, out devType, out devModel, out devInst);
                if (devType == kMsMethodDeviceType)
                    return (MassSpecMethod)devMethod;
                if (devType == -1)
                {
                    //Call Err.Raise(1, "ExtractMSMethod", "No MS method found")
                    return null;
                }
            }
            while (devType == kMsMethodDeviceType);

            return null;
        }
        private void ValidateMethod(MassSpecMethod method)
        {

            // Do some validation that happens regardless of instrument
            if (method == null)
            {
                throw new IOException(string.Format("Failed to open template method {0}. " +
                                                    "The given template may be invalid for the available version of Analyst.",
                                                    TemplateMethod));
            }

            if (method.PeriodCount == 0)
            {
                throw new IOException(string.Format("Invalid template method {0}.  Expecting at least one period.",
                                                    TemplateMethod));

            }

            // Get the last period in the given template method. 
            // We will add transitions to the last period only. 
            var msPeriod = (Period)method.GetPeriod(method.PeriodCount - 1);

            var msExperiment = (Experiment)msPeriod.GetExperiment(0);
            var experimentType = msExperiment.ScanType;
            if (experimentType != 4)
            {
                throw new IOException(string.Format("Invalid template method {0}.  Experiment type must be MRM.",
                                                    TemplateMethod));
            }

            var msExperiment7 = (IExperiment7)msExperiment;
            if (RTWindow.HasValue)
            {
                if (msExperiment7.KnownRetentionTimes == 0)
                {
                    throw new IOException(string.Format("Invalid template method {0}.  Template does not support scheduled MRM.",
                                                        TemplateMethod));
                }
            }
            else
            {
                if (msExperiment7.KnownRetentionTimes != 0)
                {
                    throw new IOException(string.Format("Invalid template method {0}.  Template is for scheduled MRM.",
                                                        TemplateMethod));
                }
            }
        }


        private void WriteToTemplate(IAcqMethod acqMethod, MethodTransitions transitions)
        {
         
            var method = ExtractMsMethod(acqMethod);
            // Get the last period in the given template method. 
            // We will add transitions to the last period only. 
            var period = (Period)method.GetPeriod(method.PeriodCount - 1);
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

