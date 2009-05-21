///
/// Exporter.hpp
///

#ifndef _EXPORTER_HPP_
#define _EXPORTER_HPP_

#include "PeptideMatcher.hpp"
#include "Peptide2FeatureMatcher.hpp"
#include "pwiz/utility/proteome/Ion.hpp"

namespace pwiz{
namespace eharmony{

struct Exporter
{    
    Exporter(const PeptideMatcher& pm, const Peptide2FeatureMatcher& p2fm) : _pm(pm), _p2fm(p2fm) {}
    
    void writePM(ostream& os);
    void writeP2FM(ostream& os);
    void writeROCStats(ostream& os);
    void writePepXML(MSMSPipelineAnalysis& mspa, ostream& os);
    void writeCombinedPepXML(MSMSPipelineAnalysis& mspa, ostream& os);
    void writeRInputFile(ostream& os);
    void writeTruePositives(ostream& os);
    void writeFalsePositives(ostream& os);
    void writeTrueNegatives(ostream& os);
    void writeFalseNegatives(ostream& os);

    PeptideMatcher _pm;
    Peptide2FeatureMatcher _p2fm;

};

} // namespace eharmony
} // namespace pwiz

#endif
