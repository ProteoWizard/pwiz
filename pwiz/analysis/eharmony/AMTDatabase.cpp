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
    //    vector<SpectrumQuery> ms2 = amtContainer._sqs;
    vector<boost::shared_ptr<SpectrumQuery> > ms2 = amtContainer._pidf->getAllContents();
    _peptides = PidfPtr(new PeptideID_dataFetcher(ms2));

}

vector<boost::shared_ptr<SpectrumQuery> > AMTDatabase::query(const Feature& f) 
{
    double mz = f.mz;           
    double rt = f.retentionTime;
    return _peptides->getSpectrumQueries(mz,rt);

}

vector<boost::shared_ptr<SpectrumQuery> > AMTDatabase::query(const double& mz, const double& rt) 
{
    return _peptides->getSpectrumQueries(mz,rt);

}

// pass in data fetcher container of AMTDatabase peptides, dummy features, query run peptides, query run features

vector<SpectrumQuery> AMTDatabase::query(DataFetcherContainer& dfc, const WarpFunctionEnum& wfe, const SearchNeighborhoodCalculator& snc)
{
    cout << "querying" << endl;
    //    dfc.adjustRT(true, false);
    dfc.warpRT(wfe);

    //    Peptide2FeatureMatcher p2fm(dfc._pidf_b, dfc._fdf_a, snc);
    Feature2PeptideMatcher f2pm(dfc._fdf_a, dfc._pidf_b, snc);
    vector<SpectrumQuery> result;
    vector<MatchPtr> matches = f2pm.getMatches();
    vector<MatchPtr>::iterator it = matches.begin();
    for(; it != matches.end(); ++it) result.push_back((*it)->spectrumQuery);

    return result;

}

vector<SpectrumQuery> AMTDatabase::query(DataFetcherContainer& dfc, const WarpFunctionEnum& wfe, SearchNeighborhoodCalculator& nds, MSMSPipelineAnalysis& mspa_in)
{
    cout << "querying ... " << endl;
    string outputDir = "./amtdb_query";
    //    outputDir += boost::lexical_cast<string>(boost::lexical_cast<int>(nds._Z * 100));
    boost::filesystem::create_directory(outputDir);

    if (!(dfc._pidf_a->getRtAdjustedFlag() && dfc._fdf_a->getMS2LabeledFlag())) dfc.adjustRT(true,false); // only do the second runs , not the whole database again  

    dfc.warpRT(wfe);
    nds.calculateTolerances(dfc);
    cout << "constructing pm ... " << endl;
    PeptideMatcher pm(dfc._pidf_a, dfc._pidf_b);

    cout << "constructing f2pm ... " << endl;
    
    Feature2PeptideMatcher f2pm(dfc._fdf_a, dfc._pidf_b, nds);

    Exporter exporter(pm, f2pm);
    exporter._dfc = DfcPtr(new DataFetcherContainer(dfc._pidf_a, dfc._pidf_b, dfc._fdf_a, dfc._fdf_b));
    
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

int main(int argc, char* argv[])
{
  if (argc < 4) 
    {
      cout << "Usage: ./amtdb pepxml featurefile stdev" << endl;
      return 1;
    }

    ifstream dbFile("amt/database.xml");
    AMTContainer amt;
    cout << "[amtdb] reading database file ... " << endl;
    amt.read(dbFile);
    cout << "[amtdb] constructing database for query ... " << endl;
    AMTDatabase db(amt);

    cout << "[amtdb] reading peptide file ... " << endl;
    ifstream queryPeptideFile(argv[1]);
    PidfPtr pidf_query(new PeptideID_dataFetcher(queryPeptideFile));
    MSMSPipelineAnalysis mspa_query;
    mspa_query.read(queryPeptideFile);

    cout << "[amtdb] reading feature file ... " << endl;
    ifstream queryFeatureFile(argv[2]);
    FdfPtr fdf_query(new Feature_dataFetcher(queryFeatureFile));
    //    DataFetcherContainer dfc(db._peptides, pidf_query, amt._fdf, fdf_query);
    DataFetcherContainer dfc(pidf_query, db._peptides, fdf_query, amt._fdf);
    dfc.adjustRT(true, false);    

    PeptideMatcher pm(pidf_query, db._peptides);
    Exporter exporter(pm, Feature2PeptideMatcher());
    
    string outputDir = "./amtdb_query";
    // outputDir += boost::lexical_cast<string>(boost::lexical_cast<int>(boost::lexical_cast<double>(argv[1]) * 100));
    boost::filesystem::create_directory(outputDir);
    /*
    ofstream ofs((outputDir + "/preWiggle.txt").c_str());
    exporter.writeWigglePlot(ofs);
    
    ofstream ofs2((outputDir + "/initialFeatures.txt").c_str());
    vector<FeatureSequenced> fsss = fdf_query.getAllContents();
    vector<FeatureSequenced>::iterator fritter = fsss.begin();
    for(; fritter != fsss.end(); ++fritter)
      {
	  ofs2 << fritter->feature->mz << "\t" << fritter->feature->retentionTime << "\n";

      }
    */
    //    ofstream ofs3((outputDir + "/anchors.txt").c_str());
    //exporter.writeAnchors(ofs3);

    WarpFunctionEnum wfe=PiecewiseLinear;
    SearchNeighborhoodCalculator snc(.001,72);
    //NormalDistributionSearch nds(boost::lexical_cast<double>(argv[3]));   

    cout << "[amtdb] querying amt database ... " << endl;
    db.query(dfc,wfe,snc,mspa_query);
    /*
    nds._Z = .01;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = .1;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = .2;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = .3;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = .4;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = .5;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = .75;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 1;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 2;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 3;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 5;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 7;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 10;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 15;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 20;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    nds._Z = 50;
    cout << "[amtdb] " << nds._Z;
    db.query(dfc,wfe,nds,mspa_query);
    */
    return 0;

}
