/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

//using System.Collections.Generic;
//using System.Drawing;
//using ZedGraph;
//using pwiz.MSGraph;
//using pwiz.Skyline.Model.Lib;

//namespace pwiz.Skyline.Controls.Graphs
//{
//    public class LibraryChromGraphItem : ChromGraphItem
//    {
//        private readonly string _title;
//        private readonly IPointList _points;
////        public LibraryChromGraphItem(string title, double peakRetentionTime, IPointList points)
////        {
////            _title = title;
////            PeakRetentionTime = peakRetentionTime;
////            _points = points;
////
////        }
//        public override string Title
//        {
//            get { return _title; }
//        }
//
//        public override PointAnnotation AnnotatePoint(PointPair point)
//        {
//            if (point.X == PeakRetentionTime)
//            {
//                return new PointAnnotation(string.Format("{0:F01}", PeakRetentionTime));
//            }
//            return null;
//        }
//
//        public override void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
//        {
//            annotations.Add(new TextObj());
//        }
//
//        public override void AddPreCurveAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
//        {
//        }
//
//        public double PeakRetentionTime { get; private set; }
//
//        public override IPointList Points
//        {
//            get { return _points; }
//        }
//
//    }
//}
