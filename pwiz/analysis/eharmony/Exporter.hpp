//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

///
/// Exporter.hpp
///

#ifndef _EXPORTER_HPP_
#define _EXPORTER_HPP_

#include "PeptideMatcher.hpp"
#include "Feature2PeptideMatcher.hpp"
#include "pwiz/utility/proteome/Ion.hpp"

namespace pwiz{
namespace eharmony{

typedef boost::shared_ptr<DataFetcherContainer> DfcPtr;

struct Exporter
{    
    Exporter(const PeptideMatcher& pm, const Feature2PeptideMatcher& f2pm) : _pm(pm), _f2pm(f2pm) {}
    
    void writePM(ostream& os);
    void writeWigglePlot(ostream& os);
    void writeRTCalibrationPlot(ostream& os);
    void writeFunnyPeptides(ostream& os);
    void writeOKPeptides(ostream& os);
    void writeF2PM(ostream& os);
    void writeROCStats(ostream& os);
    void writePepXML(MSMSPipelineAnalysis& mspa, ostream& os);
    void writeCombinedPepXML(MSMSPipelineAnalysis& mspa, ostream& os);
    void writeRInputFile(ostream& os);
    void writeTruePositives(ostream& os);
    void writeFalsePositives(ostream& os);
    void writeTrueNegatives(ostream& os);
    void writeFalseNegatives(ostream& os);
    void writeUnknownPositives(ostream& os);
    void writeUnknownNegatives(ostream& os);
    void writeRTCalibrationData(ostream& ospep, ostream& osf0, ostream& osf1);
    void writeAnchors(ostream& os);

    PeptideMatcher _pm;
    Feature2PeptideMatcher _f2pm;
    DfcPtr _dfc;

};

} // namespace eharmony
} // namespace pwiz

#endif
