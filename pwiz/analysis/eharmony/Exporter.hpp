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
