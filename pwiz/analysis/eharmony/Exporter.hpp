///
/// Exporter.hpp
///

#ifndef _EXPORTER_HPP_
#define _EXPORTER_HPP_

#include "PeptideMatcher.hpp"
#include "Peptide2FeatureMatcher.hpp"

namespace pwiz{
namespace eharmony{

struct Exporter
{
    Exporter(const PeptideMatcher& pm, const Peptide2FeatureMatcher& p2fm) : _pm(pm), _p2fm(p2fm) {}

    void writePM(ostream& os);
    void writeP2FM(ostream& os);
    void writeROCStats(ostream& os);
    void writePepXML(MSMSPipelineAnalysis& mspa, ostream& os);

    PeptideMatcher _pm;
    Peptide2FeatureMatcher _p2fm;

};

} // namespace eharmony
} // namespace pwiz

#endif

using namespace pwiz::eharmony;

void Exporter::writePM(ostream& os)
{
    XMLWriter pm_writer(os);
    const vector<pair<SpectrumQuery, SpectrumQuery> >& sqs = _pm.getMatches();

    vector<pair<SpectrumQuery, SpectrumQuery> >::const_iterator pm_it = sqs.begin();
    for(; pm_it != sqs.end(); ++pm_it)
        {
            pm_it->first.write(pm_writer);
            pm_it->second.write(pm_writer);

        }
    
}

void Exporter::writeP2FM(ostream& os)
{
    XMLWriter p2fm_writer(os);
    MatchData md(_p2fm.getMatches());
    md.write(p2fm_writer);

}

void Exporter::writeROCStats(ostream& os)
{
    MatchData fp(_p2fm.getFalsePositives());
    MatchData fn(_p2fm.getFalseNegatives());
    MatchData tp(_p2fm.getTruePositives());
    MatchData tn(_p2fm.getTrueNegatives());

    os << "All matches: " << _p2fm.getMatches().size() << endl;
    os << "truePositives: " << tp.matches.size() << endl;
    os << "falsePositives: " << fp.matches.size() << endl;
    os << "trueNegatives: " << tn.matches.size() << endl;
    os << "falseNegatives: " << fn.matches.size() << endl;

}

void Exporter::writePepXML(MSMSPipelineAnalysis& mspa, ostream& os) // mspa is the original pepXML. we are just changing the spectrumQueries attribute.
{
    vector<Match> matches = _p2fm.getMatches();
    vector<SpectrumQuery> hacked_sqs;
    
    vector<Match>::iterator it = matches.begin();
    for( ; it!= matches.end() ; ++it)
        {
            SpectrumQuery sq = it->spectrumQuery;
            sq.searchResult.searchHit.analysisResult.xResult.probability = it->score;
            // TODO need to change n term probs?
            hacked_sqs.push_back(sq);
            
        }

    mspa.msmsRunSummary.spectrumQueries = hacked_sqs;
    
    XMLWriter writer(os);
    mspa.write(writer);

}
