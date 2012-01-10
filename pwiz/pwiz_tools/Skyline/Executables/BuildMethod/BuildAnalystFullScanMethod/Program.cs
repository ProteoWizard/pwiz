/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using Interop.Analyst;
using BuildAnalystMethod;
using Interop.AcqMethodSvr;
using Interop.DDEMethodSvr;
using Interop.MSMethodSvr;
using Interop.ParameterSvr;

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
                "Usage: BuildAnalystFullScanMethod [options] <template method> [list file]*\n" +
                "   Takes template method file and a Skyline generated \n" +
                "   transition list as inputs, to generate a new method file\n" +
                "   as output.\n" +
                "   -1               Do an MS1 scan each cycle" +
                "   -i               Generate method for Information Dependent Acquisition (IDA)" +
                "   -r               Add retention time information to inclusion list (requires -i)\n" +
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
        //Documentation doesn't even have an '8', but debugging a real method
        //shows that this value should be 8 for a "TOF MS" scan
        public const short TOF_MS_SCAN = 8;
        public const short PROD_ION_SCAN = 9;
    
        private bool Ms1Scan { get; set; }

        private bool InclusionList { get; set; }

        private bool ScheduledMethod { get; set; }

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
                    case "i":
                        {
                            InclusionList = true;
                        }
                        break;
                    case "r":
                        {
                            ScheduledMethod = true;
                        }
                        break;
                    case "1":
                        Ms1Scan = true;
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

            GetAcqMethod(TemplateMethod, out templateMsMethod);

            ValidateMethod(templateMsMethod);

            
            foreach (var methodTranList in MethodTrans)
            {
                Console.Error.WriteLine(string.Format("MESSAGE: Exporting method {0}", Path.GetFileName(methodTranList.FinalMethod)));

                if (string.IsNullOrEmpty(methodTranList.TransitionList))
                {
                    throw new IOException(string.Format("Failure creating method file {0}.  The transition list is empty.",
                                          methodTranList.FinalMethod));
                }

                try
                {
                    WriteToTemplate(TemplateMethod, methodTranList);

                }
                catch (Exception x)
                {
                    throw new IOException(string.Format("Failure creating method file {0}.  {1}", methodTranList.FinalMethod, x.Message));
                }

                if (!File.Exists(methodTranList.OutputMethod))
                {
                    throw new IOException(string.Format("Failure creating method file {0}.", methodTranList.FinalMethod));
                }

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
            {
                throw new IOException("Failed to initialize.  Analyst may need to be started.");
            }

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
            if (method == null)
            {
                throw new IOException(string.Format("Failed to open template method {0}. " +
                                                    "The given template may be invalid for the available version of Analyst.",
                                                    TemplateMethod));
            }
            if (method.PeriodCount != 1)
            {
                throw new IOException(string.Format("Invalid template method {0}.  Expecting only one period.",
                                                    TemplateMethod));
            }


            var period = (Period) method.GetPeriod(0);
            var experimentCount = period.ExperimCount;

            

            if (InclusionList)
            {
                if(!IsDataDependentMethod(method))
                {
                    throw new IOException(string.Format("Invalid template method for an IDA experiment {0}. "+
                                                        "Template does not support inclusion lists.",
                                                        TemplateMethod));
                }

                // A valid IDA method will have at least one TOF MS scan and one Product Ion scan
                // On the 5600 (Analyst TF1.5)we can have 1 or 2 TOF MS scans followed by 1 Product Ion scan
                // On the QSTAR (Analyst 2.0) we should have 1 TOF MS scan followed by 1 or more Product Ion scans
                if (experimentCount < 2)
                    throw new IOException(string.Format("Invalid template method for an IDA experiment {0}. "+
                                                        "Template must have one or two TOF MS experiments and at least one Product Ion experiment.",
                                                        TemplateMethod));

                // get the first experiment, it should be a TOF MS experiment
                var msExperiment = (Experiment) period.GetExperiment(0);

                if (!IsTofMsScan(msExperiment))
                    throw new IOException(string.Format("Invalid template method for an IDA experiment {0}. "+
                                                        "First experiment type must be TOF MS.",
                                                        TemplateMethod));

                // get the last experiment, it should be a Product ion experiment
                msExperiment = ((Experiment)period.GetExperiment(experimentCount -1));
               
                if (!IsProductIonScan(msExperiment))
                    throw new IOException(string.Format("Invalid template method for and IDA experiment {0}. "+
                                                        "Last experiment type must be Product Ion Scan.",
                                                        TemplateMethod));
            }
            else
            {
                if (IsDataDependentMethod(method))
                {
                    throw new IOException(string.Format("Invalid template method for a targeted MS/MS experiment {0}. "+
                                                        "The given template is for a data dependent experiment.",
                                                        TemplateMethod));
                }

                if (experimentCount != 1 && experimentCount != 2)
                {
                    throw new IOException(string.Format("Invalid template method for a targeted MS/MS experiment {0}. " +
                                                        "Template must have 1 or 2 experiments.",
                                                        TemplateMethod));
                }

                // If the template has 1 experiment it should be a Product Ion experiment
                if(experimentCount == 1)
                {
                    var msExperiment = (Experiment)period.GetExperiment(0);
                    if (!IsProductIonScan(msExperiment))
                        throw new IOException(string.Format("Invalid template method for a targeted MS/MS experiment {0}. "+
                                                            "Template does not have a Product Ion Scan.",
                                                            TemplateMethod));
                }

                // If the template has 2 experiments require the first one to be a TOF MS and the second one to be a Product Ion
                if(experimentCount == 2)
                {
                    var msExperiment = (Experiment)period.GetExperiment(0);
                    if (!IsTofMsScan(msExperiment))
                        throw new IOException(string.Format("Invalid template method for a targeted MS/MS experiment {0}. "+
                                                            "Template has 2 experiments. First experiment type must be TOF MS.",
                                                            TemplateMethod));

                    msExperiment = (Experiment)period.GetExperiment(1);
                    if (!IsProductIonScan(msExperiment))
                        throw new IOException(string.Format("Invalid template method for a targeted MS/MS experiment {0}. "+
                                                            "Template has 2 experiments. Second experiment type must be Product Ion Scan.",
                                                            TemplateMethod));
                }
            }
        }

        private void WriteToIDATemplate(MassSpecMethod method, MethodTransitions transitions)
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

                // If the ScheduledMethod flag was set assume that the Dwell time column in the 
                // transition list file has retention time.
                if (ScheduledMethod)
                    retentionTime = transition.Dwell;

                string entryKey = transition.PrecursorMz + retentionTime.ToString();
                if (!addedEntries.Contains(entryKey)
                    && transition.PrecursorMz > minTOFMass
                    && transition.PrecursorMz < maxTOFMass)
                {
                    ((IDDEMethodObj)idaServer).AddIonEntry(1, transition.PrecursorMz, retentionTime);
                    addedEntries.Add(entryKey);
                }
            }
        }

        private void WriteToTargetedMSMSTemplate(MassSpecMethod method, MethodTransitions transitions)
        {
            var period = (Period)method.GetPeriod(0);


            Experiment tofExperiment = null;
            Experiment prodIonExperiment; 
           
            switch (period.ExperimCount)
            {
                case 2:
                    tofExperiment = (Experiment) period.GetExperiment(0);
                    prodIonExperiment = (Experiment) period.GetExperiment(1);

                    // delete the product ion experiment. We will be adding one for each precursor ion in the transition list.
                    period.DeleteExperiment(1);

                    break;

                case 1:
                    prodIonExperiment = (Experiment)period.GetExperiment(0);

                    // delete the product ion experiment. We will be adding one for each precursor ion in the transition list.
                    period.DeleteExperiment(0);

                    break;

                default:
                    throw new IOException(string.Format("Expected 1 or 2 experiments in the template. Found {0} experiments.", period.ExperimCount));
            }
            

            int j;

            if (Ms1Scan)
            {
                // If the template does not already have a TOF MS scan add one now.
                if(tofExperiment == null)
                {
                    var experiment = (Experiment)period.CreateExperiment(out j);
                    experiment.InitExperiment();
                    experiment.ScanType = TOF_MS_SCAN; 
                }
            }


            if(prodIonExperiment == null)
            {
                throw new IOException("Product Ion scan was not found in the method.");
            }

            // Get the TOF mass range from the template
            var tofPropertiesTemplate = (ITOFProperties)prodIonExperiment;
           
            
            short s;

            //Get initial source parameters from the template
            var srcParamsTbl = (ParamDataColl)prodIonExperiment.SourceParamsTbl;
            float sourceGas1 = ((ParameterData)srcParamsTbl.FindParameter("GS1", out s)).startVal;
            float sourceGas2 = ((ParameterData)srcParamsTbl.FindParameter("GS2", out s)).startVal;
            float curtainGas = ((ParameterData)srcParamsTbl.FindParameter("CUR", out s)).startVal;
            float temperature = ((ParameterData)srcParamsTbl.FindParameter("TEM", out s)).startVal;

            string ionSprayVoltageParamName = "ISVF"; // ISVF on 5600, IS on QSTAR
            float ionSprayVoltage;

            var paramData = ((ParameterData)srcParamsTbl.FindParameter(ionSprayVoltageParamName, out s));
            if (s != -1)
            {
                ionSprayVoltage = paramData.startVal;
            }
            else
            {
                ionSprayVoltageParamName = "IS";
                ionSprayVoltage = ((ParameterData)srcParamsTbl.FindParameter(ionSprayVoltageParamName, out s)).startVal;
            }

            // We will use parameters from the first mass range in the template product ion experiment.
            var massRange_template = (IMassRange)prodIonExperiment.GetMassRange(0);
            var paramTbl_template = (ParamDataColl)massRange_template.MassDepParamTbl;


            double minPrecursorMass = double.MaxValue;
            double maxPrecursorMass = 0;

            var precursors = new List<double>();

            foreach (var transition in transitions.Transitions)
            {
                if (precursors.Contains(transition.PrecursorMz))
                    continue;

                precursors.Add(transition.PrecursorMz);

                var experiment = (Experiment) period.CreateExperiment(out j);
                experiment.InitExperiment();

                // Setting ScanType to 6 for QSTAR causes method export to fail. Setting it to 9 works for both AB 5600 and QSTAR
                experiment.ScanType = PROD_ION_SCAN;

                experiment.FixedMass = transition.PrecursorMz;

                minPrecursorMass = Math.Min(minPrecursorMass, transition.PrecursorMz);
                maxPrecursorMass = Math.Max(maxPrecursorMass, transition.PrecursorMz);

                var tofProperties = (ITOFProperties)experiment;

              
                tofProperties.AccumTime = tofPropertiesTemplate.AccumTime;
                tofProperties.TOFMassMin = tofPropertiesTemplate.TOFMassMin;
                tofProperties.TOFMassMax = tofPropertiesTemplate.TOFMassMax;
                

                // The following should trigger the "Suggest" button functionality
                // of updating the Q2 transmission window.
                tofProperties.UseQ1TranDefault = 1;
                //tofProperties.UseTOFExtrDefault = 1; // Works without this one.


                // High Sensitivity vs. High Resolution
                var tofProperties2 = experiment as ITOFProperties2;
                var templateTofProperties2 = prodIonExperiment as ITOFProperties2;
                if (tofProperties2 != null && templateTofProperties2 != null)
                {
                    tofProperties2.HighSensitivity = templateTofProperties2.HighSensitivity;
                }

                var srcParams = (ParamDataColl)experiment.SourceParamsTbl;
                
                srcParams.AddSetParameter("GS1", sourceGas1, sourceGas1, 0, out s);
                srcParams.AddSetParameter("GS2", sourceGas2, sourceGas2, 0, out s);
                srcParams.AddSetParameter("CUR", curtainGas, curtainGas, 0, out s);
                srcParams.AddSetParameter("TEM", temperature, temperature, 0, out s);
                srcParams.AddSetParameter(ionSprayVoltageParamName, ionSprayVoltage, ionSprayVoltage, 0, out s);

                // Copy the compound dependent parameters from the template
                for (int i = 0; i < experiment.MassRangesCount; i++)
                {
                    var mr_i = (IMassRange)experiment.GetMassRange(i);
                    var paramTbl_i = (ParamDataColl)mr_i.MassDepParamTbl;

                    // Declustering potential
                    float dp = ((ParameterData)paramTbl_template.FindParameter("DP", out s)).startVal;
                    if(s != -1)
                    {
                        paramTbl_i.AddSetParameter("DP", dp, dp, 0, out s); 
                    }
                    

                    // Collision engergy
                    float ce = ((ParameterData)paramTbl_template.FindParameter("CE", out s)).startVal;
                    if (s != -1)
                    {
                        paramTbl_i.AddSetParameter("CE", ce, ce, 0, out s);
                    }

                    // Ion release delay
                    float ird = ((ParameterData)paramTbl_template.FindParameter("IRD", out s)).startVal;
                    if (s != -1)
                    {
                        paramTbl_i.AddSetParameter("IRD", ird, ird, 0, out s);
                    }

                    // Ion release width
                    float irw = ((ParameterData)paramTbl_template.FindParameter("IRW", out s)).startVal;
                    if (s != -1)
                    {
                        paramTbl_i.AddSetParameter("IRW", irw, irw, 0, out s);
                    }

                    // Collision energy spread; Only on the Analyst TF 1.5.1 and TF1.5.2
                    paramData = ((ParameterData)paramTbl_template.FindParameter("CES", out s));
                    if (s != -1)
                    {
                        paramTbl_i.AddSetParameter("CES", paramData.startVal, paramData.startVal, 0, out s);
                    }

                    // Focusing potential; Only on Analyst QS 2.0
                    paramData = ((ParameterData)paramTbl_template.FindParameter("FP", out s));
                    if (s != -1)
                    {
                        paramTbl_i.AddSetParameter("FP", paramData.startVal, paramData.startVal, 0, out s);
                    }

                    // Declustering potential 2; Only on Analyst QS 2.0
                    paramData = ((ParameterData)paramTbl_template.FindParameter("DP2", out s));
                    if (s != -1)
                    {
                        paramTbl_i.AddSetParameter("DP2", paramData.startVal, paramData.startVal, 0, out s);
                    }

                    // Collision gas; Only on Analyst QS 2.0
                    paramData = ((ParameterData)paramTbl_template.FindParameter("CAD", out s));
                    if (s != -1)
                    {
                        paramTbl_i.AddSetParameter("CAD", paramData.startVal, paramData.startVal, 0, out s);
                    }
                }
            }

            // Expand the mass range for the TOF MS scan if the precursor mass of any of the MS/MS experiments
            // was out of the range
            if(Ms1Scan)
            {
                var ms1TofProperties = (ITOFProperties)(period.GetExperiment(0));
                ms1TofProperties.TOFMassMin = Math.Min(ms1TofProperties.TOFMassMin, minPrecursorMass);
                ms1TofProperties.TOFMassMax = Math.Max(ms1TofProperties.TOFMassMax, maxPrecursorMass);
            }
        }
        
        public void WriteToTemplate(String templateMethodFile, MethodTransitions transitions)
        {
            MassSpecMethod templateMsMethod;

            IAcqMethod templateAcqMethod = GetAcqMethod(TemplateMethod, out templateMsMethod);

            var method = ExtractMsMethod(templateAcqMethod);

            if(InclusionList)
            {
                WriteToIDATemplate(method, transitions);
            }

            else
            {
               WriteToTargetedMSMSTemplate(method, transitions); 
            }

            templateAcqMethod.SaveAcqMethodToFile(transitions.OutputMethod, 1);
        }


        private static bool IsDataDependentMethod(MassSpecMethod method)
        {
            return ((IMassSpecMethod2) method).DataDependent == 1;
        }


        private static bool IsTofMsScan(Experiment experiment)
        {
            return experiment.ScanType == TOF_MS_SCAN;
        }

        private static bool IsProductIonScan(Experiment experiment)
        {
            return experiment.ScanType == PROD_ION_SCAN;
            // Previous comment about ScanType was: Product ion scan is 9 in TF 1.5, 6 in QS 2.0
            // However, I found the ScanType for a ProductIon to always be 9;
            // return (experiment.ScanType == 6 || experiment.ScanType == 9);
        }
    }
}