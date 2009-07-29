///
/// AMTDatabase.cpp
///

#include "AMTDatabase.hpp"
#include "Exporter.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "boost/filesystem.hpp"

using namespace std;
using namespace pwiz;
using namespace eharmony;
using namespace pwiz::proteome;

AMTDatabase::AMTDatabase(const AMTContainer& amtContainer)
{
    vector<boost::shared_ptr<SpectrumQuery> > ms2 = amtContainer._pidf->getAllContents();
    _peptides = PidfPtr(new PeptideID_dataFetcher(ms2));

}

vector<SpectrumQuery> AMTDatabase::query(DfcPtr dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in, string outputDir)
{
    outputDir += "/amtdb_query";
    outputDir += boost::lexical_cast<string>(boost::lexical_cast<int>(nds._threshold * 100));

    boost::filesystem::create_directory(outputDir);

    PeptideMatcher pm(dfc->_pidf_a, dfc->_pidf_b);
    Feature2PeptideMatcher f2pm(dfc->_fdf_a, dfc->_pidf_b, nds);

    cout << "[AMTDatabase] Number of matches accepted: " << f2pm.getMatches().size() << endl;

    Exporter exporter(pm, f2pm);
    exporter._dfc = dfc;

    ofstream ofs_anch((outputDir + "/anchors.txt").c_str());
    exporter.writeAnchors(ofs_anch);

    ofstream ofs_pep((outputDir + "/peptides.txt").c_str());
    ofstream ofs_feat0((outputDir + "/features.txt").c_str());
    ofstream ofs_feat1((outputDir + "/calibratedFeatures.txt").c_str());
    exporter.writeRTCalibrationData(ofs_pep, ofs_feat0, ofs_feat1);

    ofstream comb((outputDir + "/combined.xml").c_str());
    exporter.writeCombinedPepXML(mspa_in, comb);

    ofstream ofs_pm((outputDir + "/pm.xml").c_str());
    exporter.writePM(ofs_pm);

    ofstream ofs_wiggle((outputDir + "/wiggle.txt").c_str());
    exporter.writeWigglePlot(ofs_wiggle);

    ofstream ofs_rt((outputDir + "/calibration.txt").c_str());
    exporter.writeRTCalibrationPlot(ofs_rt);

    ofstream ofs_funny((outputDir + "/funnyPeptides.txt").c_str());
    exporter.writeFunnyPeptides(ofs_funny);

    ofstream ofs_ok((outputDir + "/okPeptides.txt").c_str());
    exporter.writeOKPeptides(ofs_ok);

    ofstream ofs_f2pm((outputDir + "/f2pm.xml").c_str());
    exporter.writeF2PM(ofs_f2pm);

    ofstream ofs_roc((outputDir + "/roc.txt").c_str());
    exporter.writeROCStats(ofs_roc);

    ofstream ofs_r((outputDir + "/r_input.txt").c_str());
    exporter.writeRInputFile(ofs_r);
    
    ofstream ofs_tp((outputDir + "/tp.txt").c_str());
    exporter.writeTruePositives(ofs_tp);

    ofstream ofs_fp((outputDir + "/fp.txt").c_str());
    exporter.writeFalsePositives(ofs_fp);

    ofstream ofs_tn((outputDir + "/tn.txt").c_str());
    exporter.writeTrueNegatives(ofs_tn);

    ofstream ofs_fn((outputDir + "/fn.txt").c_str());
    exporter.writeFalseNegatives(ofs_fn);
    
    ofstream ofs_up((outputDir + "/up.txt").c_str());
    exporter.writeUnknownPositives(ofs_up);

    ofstream ofs_un((outputDir + "/un.txt").c_str());
    exporter.writeUnknownNegatives(ofs_un);

    vector<SpectrumQuery> result;
    vector<MatchPtr> matches = f2pm.getMatches();
    vector<MatchPtr>::iterator it = matches.begin();
    for(; it != matches.end(); ++it) result.push_back((*it)->spectrumQuery);

    mspa_in.msmsRunSummary.spectrumQueries = result;

    ofstream ofs_pepxml((outputDir + "/ms1_5.pep.xml").c_str());
    exporter.writePepXML(mspa_in, ofs_pepxml);
    
    ofstream ofs_missed((outputDir + "/mismatches.xml").c_str());
    XMLWriter writer(ofs_missed);
    vector<MatchPtr> mismatches = f2pm.getMismatches();
    vector<MatchPtr>::iterator it2 = mismatches.begin();
    for(; it2 != mismatches.end(); ++it2) (*it2)->write(writer);


    return result;
}
