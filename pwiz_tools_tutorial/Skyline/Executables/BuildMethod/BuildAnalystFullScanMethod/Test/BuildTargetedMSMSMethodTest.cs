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
using Interop.MSMethodSvr;
using Interop.ParameterSvr;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace BuildAnalystFullScanMethod.Test
{
    [TestClass]
    public class BuildTargetedMSMSMethodTest : BuildMethodTest
    {

        [TestMethod]
        public void TestTargetedMsmsNoTOFMs()
        {
            TestTargetedMsmsNoTOFMs(IsQSTAR() ? METHOD_FILE_QSTAR : METHOD_FILE_5600);
        }

        [TestMethod]
        public void TestTargetedMsmsTOFMs()
        {
            TestTargetedMsmsTOFMs(IsQSTAR() ? METHOD_FILE_QSTAR : METHOD_FILE_5600);
        }

        [TestMethod]
        public void TestTargetedMsmsTOFMs_TemplateHasTOFMs()
        {
            TestTargetedMsmsTOFMs(IsQSTAR() ? METHOD_FILE_QSTAR_MS1_MS2 : METHOD_FILE_5600_MS1_MS2);
        }

        private void TestTargetedMsmsNoTOFMs(string templateMethodFile)
        {
            var args = new[] { 
                                GetTemplateFilePath(templateMethodFile), 
                                GetTransListUnschedPath() 
                              };

            var builder = new BuildAnalystFullScanMethod();
            builder.ParseCommandArgs(args);
            builder.build();

            string methodFilePath = GetMethodUnschedPath();
            string templateFilePath = GetTemplateFilePath(templateMethodFile);

            TestTargetedMsmsCommon(templateFilePath, methodFilePath, false);

            DeleteOutput(methodFilePath);
        }

        private void TestTargetedMsmsTOFMs(string templateMethodFile)
        {
            var args = new[]
                           {
                               "-1", 
                               GetTemplateFilePath(templateMethodFile),
                               GetTransListUnschedPath()
                           };

            var builder = new BuildAnalystFullScanMethod();
            builder.ParseCommandArgs(args);
            builder.build();

            string methodFilePath = GetMethodUnschedPath();
            string templateFilePath = GetTemplateFilePath(templateMethodFile);

            TestTargetedMsmsCommon(templateFilePath, methodFilePath, true);

            DeleteOutput(methodFilePath);
        }

        private void TestTargetedMsmsCommon(string templateFilePath, string methodFilePath, bool doTOFMs)
        {
            var method = GetMethod(methodFilePath);
            Assert.AreEqual(1, method.PeriodCount);

            var period = (Period)method.GetPeriod(0);

            // Read in the template method for comparison
            MassSpecMethod templateMethod = GetMethod(templateFilePath);

            var prodIonIndex = ((Period)templateMethod.GetPeriod(0)).ExperimCount == 1 ? 0 : 1;
            
            var templateProdIonExpt = (Experiment)((Period)(templateMethod.GetPeriod(0))).GetExperiment(prodIonIndex);

            var templateTofMsExpt = ((Period)templateMethod.GetPeriod(0)).ExperimCount == 2
                                        ? (Experiment) ((Period) (templateMethod.GetPeriod(0))).GetExperiment(0)
                                        : null;

            if (doTOFMs)
            {
                // We should have 1 "TOF MS" experiments and 20 "Product Ion" experiments
                Assert.AreEqual(21, period.ExperimCount);

                // first experiment should be a TOF experiment
                var experiment = (Experiment)period.GetExperiment(0);
                Assert.AreEqual(BuildAnalystFullScanMethod.TOF_MS_SCAN, experiment.ScanType);

                if(templateTofMsExpt != null)
                {
                    CompareExperiments(templateTofMsExpt, experiment, IsQstarTemplate(templateFilePath));
                }
                
                experiment = (Experiment)period.GetExperiment(1);
                Assert.AreEqual(BuildAnalystFullScanMethod.PROD_ION_SCAN, experiment.ScanType);

                CompareExperiments(templateProdIonExpt, experiment, IsQstarTemplate(templateFilePath));
            }
            else
            {
                // We should have 20 "Product Ion" experiments
                Assert.AreEqual(20, period.ExperimCount);

                var experiment = (Experiment)period.GetExperiment(0);
                Assert.AreEqual(BuildAnalystFullScanMethod.PROD_ION_SCAN, experiment.ScanType);

                CompareExperiments(templateProdIonExpt, experiment, IsQstarTemplate(templateFilePath));
            }
        }

        private static void CompareExperiments(Experiment templateExpt, Experiment myExpt, bool isQstar)
        {
            var tofProperties_template = (ITOFProperties)templateExpt;
            var tofProperties_mine = (ITOFProperties)myExpt;
         

            Assert.AreEqual(tofProperties_template.TOFMassMin, tofProperties_mine.TOFMassMin);
            Assert.AreEqual(tofProperties_template.TOFMassMax, tofProperties_mine.TOFMassMax);
            // It looks like the accumulation time for an experiment is not updated if the value provided 
            // to the setter method is within a certain delta. So the accumulation time in the template
            // experiment may not be identical to the one that we generate.
            Assert.AreEqual(tofProperties_template.AccumTime, tofProperties_mine.AccumTime, 0.1);


            var srcParamsTbl_template = (ParamDataColl)templateExpt.SourceParamsTbl;
            var srcParamsTbl_mine = (ParamDataColl)myExpt.SourceParamsTbl;
           

            CompareParam("GS1", srcParamsTbl_template, srcParamsTbl_mine);
            CompareParam("GS2", srcParamsTbl_template, srcParamsTbl_mine);
            CompareParam("CUR", srcParamsTbl_template, srcParamsTbl_mine);
            CompareParam("TEM", srcParamsTbl_template, srcParamsTbl_mine);

            Assert.AreEqual(templateExpt.MassRangesCount, myExpt.MassRangesCount);

            for (int i = 0; i < templateExpt.MassRangesCount; i++)
            {
                var compoundDepParams_template =
                    (ParamDataColl) ((MassRange) (templateExpt.GetMassRange(i))).MassDepParamTbl;
                var compoundDepParams_mine = (ParamDataColl) ((MassRange) (myExpt.GetMassRange(i))).MassDepParamTbl;

                AssertParamNotZero("DP", compoundDepParams_mine);
                AssertParamNotZero("CE", compoundDepParams_mine);
                CompareParam("IRD", compoundDepParams_template, compoundDepParams_mine);
                CompareParam("IRW", compoundDepParams_template, compoundDepParams_mine);

                if (!isQstar)
                {
                    CompareParam("CES", compoundDepParams_template, compoundDepParams_mine);
                }
                else
                {
                    CompareParam("FP", compoundDepParams_template, compoundDepParams_mine);
                    CompareParam("DP2", compoundDepParams_template, compoundDepParams_mine);
                    CompareParam("CAD", compoundDepParams_template, compoundDepParams_mine);
                }
            }

            if (!isQstar)
            {
                Assert.AreEqual(((ITOFProperties2)templateExpt).HighSensitivity, ((ITOFProperties2)myExpt).HighSensitivity);
            }
        }

        private static void CompareParam(string paramKey, ParamDataColl templateParamColl, ParamDataColl myParamColl)
        {
            short s1;
            short s2;

            var templateParam = (ParameterData)templateParamColl.FindParameter(paramKey, out s1);
            var myParam = (ParameterData)myParamColl.FindParameter(paramKey, out s2);
            if (templateParam == null)
            {
                Assert.IsNull(myParam);
                return;
            }
            
            Assert.IsNotNull(myParam);
            Assert.AreEqual(templateParam.startVal, myParam.startVal);
        }

        private static void AssertParamNotZero(string paramKey, ParamDataColl myParamColl)
        {
            short s;

            var myParam = (ParameterData)myParamColl.FindParameter(paramKey, out s);

            Assert.IsNotNull(myParam);
            Assert.AreNotEqual(0, myParam.startVal);
        }
    }
}
