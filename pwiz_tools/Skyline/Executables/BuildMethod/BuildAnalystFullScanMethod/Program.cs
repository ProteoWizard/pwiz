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
using System.IO;
using AcqMethodSvrLib;
using BuildAnalystMethod;
using MSMethodSvrLib;
using ParameterSvrLib;
using Interop.DDEMethodSvr;

namespace BuildAnalystFullScanMethod
{
    class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = -1;  // Failure until success

                var builder = new BuildAnalystFullScanMethod();
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
                "Usage: BuildAnalystFullScanMethod [options] <template method> [list file]*\n" +
                "   Takes template method file and a Skyline generated \n" +
                "   transition list as inputs, to generate a new method file\n" +
                "   as output.\n" +
                "   -1               Do an MS1 scan each cycle" +
                "   -i N             Do data-dependent acquisition for the top N precursors every scan" +
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
                "                    ...";
            Console.Error.Write(usage);
        }
    }

    class BuildAnalystFullScanMethod : BuildAnalystMethod.BuildAnalystMethod
    {
        protected bool Ms1Scan { get; set; }

        protected bool InclusionList { get; set; }

        protected int TopNPrecursors { get; set; }

        public void ParseCommandArgs(List<string> args)
        {
            TopNPrecursors = 5;

            var args2 = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                if (arg.Length < 2)
                    continue;
                switch (arg.Substring(1, arg.Length - 1))
                {
                    case "i":
                        {
                            InclusionList = true;
                            if (i + 1 >= args.Count)
                                throw new UsageException();
                            int topN;
                            if (!int.TryParse(args[++i], out topN))
                                throw new UsageException();
                            if (topN < 1)
                                throw new UsageException();
                            TopNPrecursors = topN;
                        }
                        break;
                    case "1":
                        Ms1Scan = true;
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
            var msExperiment = (Experiment)((Period)method.GetPeriod(0)).GetExperiment(0);
            var experimentCount = ((Period)method.GetPeriod(0)).ExperimCount;
            var experimentType = msExperiment.ScanType;
            //Product ion scan is 9 in the new version of the software, 6 in the old. TOF MS is 8
            if (InclusionList)
            {
                if (experimentType != 8)
                    throw new IOException(string.Format("Invalid template method {0}. Experiment type must be TOF MS for an IDA experiment.",
                                                    TemplateMethod));

                if (experimentCount != 2)
                    throw new IOException(string.Format("Invalid template method {0}. Template must have one TOF MS experiment and one Product Ion experiment.",
                                                    TemplateMethod));

                var experiment2Type = ((Experiment)((Period)method.GetPeriod(0)).GetExperiment(1)).ScanType;
                if (experiment2Type != 9 && experiment2Type != 6)
                    throw new IOException(string.Format("Invalid template method {0}. Second experiment type must be Product Ion Scan.",
                                                     TemplateMethod));
            }
            else
            {
                if (experimentType != 9 && experimentType != 6)
                    throw new IOException(string.Format("Invalid template method {0}. Experiment type must be Product Ion Scan.",
                                                     TemplateMethod));

                if (experimentCount != 1)
                    throw new IOException(string.Format("Invalid template method {0}. Template must have only one experiment.",
                                                    TemplateMethod));
            }
        }

        public override void WriteToTemplate(IAcqMethod acqMethod, MethodTransitions transitions)
        {

            var method = ExtractMsMethod(acqMethod);
            var period = (Period)method.GetPeriod(0);
            var mExperiment = (Experiment)period.GetExperiment(0);

            var tofPropertiesX = (ITOFProperties)mExperiment;
            var accumTime = tofPropertiesX.AccumTime;

            var precursors = new List<double>();

            short s;

            var srcParamsTbl = (ParamDataColl)((Experiment)period.GetExperiment(0)).SourceParamsTbl;
            //Get initial source parameters from the template
            float sourceGas1 = ((ParameterData)srcParamsTbl.FindParameter("GS1", out s)).startVal;
            float sourceGas2 = ((ParameterData)srcParamsTbl.FindParameter("GS2", out s)).startVal;
            float curtainGas = ((ParameterData)srcParamsTbl.FindParameter("CUR", out s)).startVal;
            float temperature = ((ParameterData)srcParamsTbl.FindParameter("TEM", out s)).startVal;
            string ionSprayVoltageParamName = "ISVF"; // ISVF on 5600, IS on QSTAR
            float ionSprayVoltage;
            try
            {
                ionSprayVoltage = ((ParameterData)srcParamsTbl.FindParameter(ionSprayVoltageParamName, out s)).startVal;
            }
            catch (Exception)
            {
                ionSprayVoltageParamName = "IS";
                ionSprayVoltage = ((ParameterData)srcParamsTbl.FindParameter(ionSprayVoltageParamName, out s)).startVal;
            }

            if (InclusionList)
            {
                object idaServer;
                ((IMassSpecMethod2)method).GetDataDependSvr(out idaServer);
                ((IDDEMethodObj)idaServer).putUseIncludeList(1);

                double minTOFMass = 0;
                double maxTOFMass = 0;
                ((IDDEMethodObj)idaServer).getUsersSurvTOFMasses(ref minTOFMass, ref maxTOFMass);

                var addedEntries = new List<string>();
                foreach (var transition in transitions.Transitions)
                {
                    double retentionTime = 0;
                    string entryKey = transition.PrecursorMz + retentionTime.ToString();
                    if (!addedEntries.Contains(entryKey)
                        && transition.PrecursorMz > minTOFMass
                        && transition.PrecursorMz < maxTOFMass)
                    {
                        ((IDDEMethodObj)idaServer).AddIonEntry(1, transition.PrecursorMz, retentionTime);
                        addedEntries.Add(entryKey);
                    }
                }

                int experimentIndex;
                //TopN - 1 because the template comes with one product ion scan.
                for (int i = 0; i < TopNPrecursors-1; i++)
                {
                    var experiment = (Experiment)period.CreateExperiment(out experimentIndex);
                    experiment.InitExperiment();
                    experiment.ScanType = Equals(ionSprayVoltageParamName, "IS") ? (short)6 : (short)9;
                }
            }
            else
            {
                period.DeleteExperiment(0);

                int j;

                if (Ms1Scan)
                {
                    var experiment = (Experiment)period.CreateExperiment(out j);
                    experiment.InitExperiment();
                    experiment.ScanType = 8; //Documentation doesn't even have an '8', but debugging a real method
                    //shows that this value should be 8 for a "TOF MS" scan
                }

                foreach (var transition in transitions.Transitions)
                {
                    if (precursors.Contains(transition.PrecursorMz))
                        continue;

                    precursors.Add(transition.PrecursorMz);

                    var experiment = (Experiment)period.CreateExperiment(out j);
                    experiment.InitExperiment();

                    experiment.ScanType = Equals(ionSprayVoltageParamName, "IS") ? (short)6 : (short)9;

                    experiment.FixedMass = transition.PrecursorMz;

                    var tofProperties = (ITOFProperties)experiment;

                    try
                    {
                        tofProperties.AccumTime = transition.Dwell;
                    }
                    catch (Exception)
                    {
                        tofProperties.AccumTime = accumTime;
                    }
                    tofProperties.TOFMassMin = 400;
                    tofProperties.TOFMassMax = 1400;

                    var srcParams = (ParamDataColl)experiment.SourceParamsTbl;
                    srcParams.AddSetParameter("GS1", sourceGas1, sourceGas1, 0, out s);
                    srcParams.AddSetParameter("GS2", sourceGas2, sourceGas2, 0, out s);
                    srcParams.AddSetParameter("CUR", curtainGas, curtainGas, 0, out s);
                    srcParams.AddSetParameter("TEM", temperature, temperature, 0, out s);
                    srcParams.AddSetParameter(ionSprayVoltageParamName, ionSprayVoltage, ionSprayVoltage, 0, out s);
                }
            }

            acqMethod.SaveAcqMethodToFile(transitions.OutputMethod, 1);
        }
    }
}