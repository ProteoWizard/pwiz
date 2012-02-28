//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Initial Developer of the DirecTag peptide sequence tagger is Matt Chambers.
// Contributor(s): Surendra Dasaris
//
// The Initial Developer of the ScanRanker GUI is Zeqiang Ma.
// Contributor(s): 
//
// Copyright 2009 Vanderbilt University
//

using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.IO;

namespace ScanRanker
{
    public class DirecTagInfo
    {
        /*
         DirecTag config from MainForm
            NumChargeStates = 3
            FragmentMzTolerance = 0.5
            IsotopeMzTolerance = 0.25
            PrecursorMzTolerance = 1.25
            UseAvgMassOfSequences = 1
            UseChargeStateFromMS = 0
            UseMultipleProcessors = 0
            StaticMods = C 57.0215
            
            WriteOutTags = 0
            WriteScanRankerMetrics = 1
            ScanRankerMetricsFileName = "";
            WriteHighQualSpectra = 1
            HighQualSpecFileName = "";
            OutputFormat = "mzXML"
            HighQualSpecCutoff = 0.6

         DirecTag hard code default config
            ComplementScoreWeight	=	1
            IntensityScoreWeight	=	1
            MzFidelityScoreWeight	=	1
            TicCutoffPercentage = 1
            MaxPeakCount = 100
            MaxTagCount = 20
            MaxTagScore = 20
            ContextScoreWeight = 0
            TagLength = 3
            DeisotopingMode = 1
            OnLongestPathScoreWeight = 0
            OutputSuffix = ""
         */
        private string hardCodeCfg =
                 "ComplementScoreWeight = 1" + "\r\n"
                 + "IntensityScoreWeight = 1" + "\r\n"
                 + "MzFidelityScoreWeight = 1" + "\r\n"
                 + "MaxPeakCount = 100" + "\r\n"
                 + "MaxTagCount = 20" + "\r\n"
                 + "MaxTagScore = 20" + "\r\n"
                 + "TicCutoffPercentage = 1" + "\r\n"
                 + "PreferIntenseComplements = 1" + "\r\n"
                 + "ContextScoreWeight = 0" + "\r\n"
                 + "DeisotopingMode = 0" + "\r\n"
                 + "OnLongestPathScoreWeight = 0" + "\r\n"
                 + "OutputSuffix = \"\"";
        public string HardCodeCfg
        {
            get { return hardCodeCfg; }
            set { hardCodeCfg = value; }
        }

        private float precursorMzTolerance;
        public float PrecursorMzTolerance
        {
            get {return precursorMzTolerance;}
            set { precursorMzTolerance = value;}
        }

        private float fragmentMzTolerance;
        public float FragmentMzTolerance
        {
            get {return fragmentMzTolerance;}
            set { fragmentMzTolerance = value;}
        }

        private float isotopeMzTolerance;
        public float IsotopeMzTolerance
        {
            get {return isotopeMzTolerance;}
            set { isotopeMzTolerance = value;}
        }

        private int tagLength;
        public int TagLength
        {
            get { return tagLength; }
            set { tagLength = value; }
        }

        private int useAvgMassOfSequences;
        public int UseAvgMassOfSequences
        {
            get {return useAvgMassOfSequences;}
            set { useAvgMassOfSequences = value;}
        }

        private int numChargeStates;
        public int NumChargeStates
        {
            get{return numChargeStates;}
            set{numChargeStates = value;}
        }

        private float writeOutTags;
        public float WriteOutTags
        {
            get { return writeOutTags; }
            set { writeOutTags = value; }
        }

        private int useChargeStateFromMS;
        public int UseChargeStateFromMS
        {
            get{return useChargeStateFromMS;}
            set{useChargeStateFromMS = value;}
        }
        
        private int useMultipleProcessors;
        public int UseMultipleProcessors
        {
            get{return useMultipleProcessors;}
            set{useMultipleProcessors = value;}
        }

        private string staticMods;
        public string StaticMods
        {
            get{return staticMods;}
            set{staticMods = value;}
        }

        private int writeScanRankerMetrics;
        public int WriteScanRankerMetrics
        {
            get{return writeScanRankerMetrics;}
            set{writeScanRankerMetrics = value;}
        }

        private string scanRankerMetricsFileName;
        public string ScanRankerMetricsFileName
        {
            get { return scanRankerMetricsFileName; }
            set { scanRankerMetricsFileName = value; }
        }
        
        private int writeHighQualSpectra;
        public int WriteHighQualSpectra
        {
            get{return writeHighQualSpectra;}
            set{writeHighQualSpectra = value;}
        }

        private string highQualSpecFileName;
        public string HighQualSpecFileName
        {
            get { return highQualSpecFileName; }
            set { highQualSpecFileName = value; }
        }
        
        private string outputFormat;
        public string OutputFormat
        {
            get{return outputFormat;}
            set{outputFormat = value;}
        }

        private float highQualSpecCutoff;
        public float HighQualSpecCutoff
        {
            get{return highQualSpecCutoff;}
            set{highQualSpecCutoff = value;}
        }

        public void WriteDirectagCfg()
        {
            try
            {
                //Directory.SetCurrentDirectory(outDir);
                using (TextWriter tw = File.CreateText("directag.cfg"))
                {
                    //TextWriter tw = new StreamWriter("directag.cfg");


                    tw.WriteLine("PrecursorMzTolerance = {0}", precursorMzTolerance);
                    tw.WriteLine("FragmentMzTolerance = {0}", fragmentMzTolerance);
                    tw.WriteLine("IsotopeMzTolerance = {0}", isotopeMzTolerance);
                    tw.WriteLine("TagLength = {0}", tagLength);
                    tw.WriteLine("NumChargeStates = {0}", numChargeStates);
                    tw.WriteLine("UseAvgMassOfSequences = {0}", useAvgMassOfSequences);
                    tw.WriteLine("UseChargeStateFromMS = {0}", useChargeStateFromMS);
                    tw.WriteLine("UseMultipleProcessors = {0}", useMultipleProcessors);
                    tw.WriteLine("StaticMods = \"{0}\"", staticMods);
                    tw.WriteLine();
                    tw.WriteLine("WriteOutTags = {0}", writeOutTags);
                    tw.WriteLine("WriteScanRankerMetrics = {0}", writeScanRankerMetrics);
                    //tw.WriteLine("ScanRankerMetricsFileName = \"{0}\"", scanRankerMetricsFileName);
                    tw.WriteLine("WriteHighQualSpectra = {0}", writeHighQualSpectra);
                    //tw.WriteLine("HighQualSpecFileName = \"{0}\"", highQualSpecFileName);
                    tw.WriteLine("OutputFormat = \"{0}\"", outputFormat);
                    tw.WriteLine("HighQualSpecCutoff = {0}", highQualSpecCutoff);
                    tw.WriteLine();
                    //hard code default
                    tw.WriteLine(hardCodeCfg);
                    tw.Close();
                }
            }
            catch (Exception exc)
            {
                //throw new Exception("Error creating DirecTag configuration file\r\n", exc);
                Workspace.SetText("\r\nError in creating DirecTag configuration file\r\n");
                throw new Exception(exc.Message);
            }

        }



    }
}
