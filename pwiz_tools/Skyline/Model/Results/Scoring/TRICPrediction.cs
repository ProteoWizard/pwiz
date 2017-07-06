/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Skyline.Model.Results.Scoring
{
    /// <summary>
    /// 
    /// </summary>
    public class TRICPrediction
    {
        private string key;

        public struct PredictionElement
        {
            public double? retentionTime;
            public double? retentionTimeRmsd;
            public double? qValue;
            public int? bestIndex;
            public int? secondBestIndex;
        }

        public TRICPrediction(string name, Dictionary<string, Dictionary<string, PredictionElement>> predictions )
            : base(name)
        {
            UseTric = true;
            Predictions = predictions;
        }

        public Dictionary<string, Dictionary<string, PredictionElement>> Predictions { get; private set;}
        public Boolean UseTric { get; private set; }


        public static TRICPrediction loadFromFile(string path)
        {
            var name = path.Split('\\').Last();
            var pred = new TRICPrediction(name);
            pred.key = path + DateTime.Now.Ticks;
            pred.UseTric = true;
            pred.Predictions = new Dictionary<string, Dictionary<string, PredictionElement>>();
            using (var reader = new StreamReader(path))
            {
                while (!reader.EndOfStream)
                {
                    string[] line = reader.ReadLine().Split('\t');
                    if (line.Length != 3)
                    {
                        throw new Exception("Tric Prediction File is incorrectly formatted");
                    }
                    var pep = line[0];
                    var replicate = line[1];
                    double rt;
                    if (Double.TryParse(line[2], out rt))
                    {
                        Dictionary<string, PredictionElement> subPredictions;
                        if (!pred.Predictions.TryGetValue(pep, out subPredictions))
                        {
                            subPredictions = new Dictionary<string, PredictionElement>();
                            pred.Predictions[pep] = subPredictions;
                        }
                        if (subPredictions.ContainsKey(replicate))
                        {
                            throw new Exception("Tric file contains multiple predictions for same transition group");
                        }
                        var element = new PredictionElement();
                        element.retentionTime = rt;
                        element.qValue = null;
                        subPredictions[replicate] = element;
                    }
                    else
                    {
                        throw new Exception("Retention time in Tric file could not be parsed");
                    }
                }
            }
            return pred;
        }

        private TRICPrediction(string name)
            : base(name)
        {
        }

        
        

        public TRICPrediction(TRICPrediction item)
            :base(item.Name+"(2)")
        {
            if (item.Predictions != null)
            {
                Predictions = new Dictionary<string, Dictionary<string, PredictionElement>>();
                foreach (var pred in item.Predictions.Keys)
                {
                    var subDict = new Dictionary<string, PredictionElement>();
                    Predictions.Add(pred, subDict);
                    var origSubDict = item.Predictions[pred];
                    foreach (var replicate in origSubDict.Keys)
                    {
                        subDict.Add(replicate, origSubDict[replicate]);
                    }
                }
            }
            UseTric = item.UseTric;
        }

        public void Serialize(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                foreach (var peptide in Predictions.Keys)
                {
                    var subPredictions = Predictions[peptide];
                    foreach (var replicate in subPredictions.Keys)
                    {
                        var prediction = subPredictions[replicate];
                        if (prediction.retentionTime.HasValue)
                        {
                            var rt = prediction.retentionTime.Value;
                            writer.Write(peptide + "\t" + replicate + "\t" + rt + "\n");
                        }
                    }
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(Object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var that = obj as TRICPrediction;
            if (that == null)
            {
                return false;
            }
            return key.Equals(that.key);
        }

        public override int GetHashCode()
        {
            return key.GetHashCode();
        }
    }
}
