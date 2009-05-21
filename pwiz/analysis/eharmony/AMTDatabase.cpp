///
/// AMTDatabase.cpp
///

#include "AMTDatabase.hpp"
#include "Exporter.hpp"
#include "pwiz/utility/proteome/Ion.hpp"

using namespace std;
using namespace pwiz;
using namespace eharmony;
using namespace pwiz::proteome;

AMTDatabase::AMTDatabase(const AMTContainer& amtContainer)
{
    vector<SpectrumQuery> ms2 = amtContainer._sqs;
    _peptides = PeptideID_dataFetcher(ms2);
    cout << _peptides.getAllContents().size() << endl;

}

vector<boost::shared_ptr<SpectrumQuery> > AMTDatabase::query(const Feature& f) 
{
    double mz = f.mzMonoisotopic;           
    double rt = f.retentionTime;
    return _peptides.getSpectrumQueries(mz,rt);

}

vector<boost::shared_ptr<SpectrumQuery> > AMTDatabase::query(const double& mz, const double& rt) 
{
    cout << "querying ... " << endl;
    cout << "result size: " << _peptides.getSpectrumQueries(mz,rt).size() << endl;
    cout << "_peptides.size(): " << _peptides.getAllContents().size() << endl;
    cout << "some contents: " << Ion::mz(_peptides.getAllContents().begin()->precursorNeutralMass, _peptides.getAllContents().begin()->assumedCharge) << "   " << _peptides.getAllContents().begin()->retentionTimeSec << endl;
    return _peptides.getSpectrumQueries(mz,rt);

}

// pass in data fetcher container of AMTDatabase peptides, dummy features, query run peptides, query run features

vector<SpectrumQuery> AMTDatabase::query(DataFetcherContainer& dfc, const WarpFunctionEnum& wfe, const SearchNeighborhoodCalculator& snc)
{
    cout << "querying" << endl;
    dfc.adjustRT(false);
    dfc.warpRT(wfe);

    Peptide2FeatureMatcher p2fm(dfc._pidf_a, dfc._fdf_b, snc);

    vector<SpectrumQuery> result;
    vector<Match> matches = p2fm.getMatches();
    vector<Match>::iterator it = matches.begin();
    for(; it != matches.end(); ++it) result.push_back(it->spectrumQuery);

    return result;

}

vector<SpectrumQuery> AMTDatabase::query(DataFetcherContainer& dfc, const WarpFunctionEnum& wfe, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa_in)
{
    cout << "querying ... " << endl;
    string outputDir = "./amtdb_query";
    outputDir += boost::lexical_cast<string>(nds._Z);
    boost::filesystem::create_directory(outputDir);

    dfc.adjustRT(false); // only do the second runs , not the whole database again
    dfc.warpRT(wfe);
    nds.calculateTolerances(dfc);

    cout << "constructing pm ... " << endl;
    PeptideMatcher pm(dfc);
    cout << "constructing p2fm ... " << endl;
    Peptide2FeatureMatcher p2fm(dfc._pidf_a, dfc._fdf_b, nds);

    Exporter exporter(pm, p2fm);

    ofstream ofs_pm((outputDir + "/pm.xml").c_str());
    exporter.writePM(ofs_pm);

    ofstream ofs_p2fm((outputDir + "/p2fm.xml").c_str());
    exporter.writeP2FM(ofs_p2fm);

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
    vector<Match> matches = p2fm.getMatches();
    vector<Match>::iterator it = matches.begin();
    for(; it != matches.end(); ++it) result.push_back(it->spectrumQuery);

    mspa_in.msmsRunSummary.spectrumQueries = result;

    ofstream ofs_pepxml((outputDir + "/ms1_5.pep.xml").c_str());
    exporter.writePepXML(mspa_in, ofs_pepxml);

    ofstream ofs_missed((outputDir + "/mismatches.xml").c_str());
    XMLWriter writer(ofs_missed);
    vector<Match> mismatches = p2fm.getMismatches();
    vector<Match>::iterator it2 = mismatches.begin();
    for(; it2 != mismatches.end(); ++it2) it2->write(writer);


    return result;
}

int main(int argc, char* argv[])
{
    ifstream dbFile("./amt/database.xml");
    AMTContainer amt;
    cout << "[amtdb] reading database file ... " << endl;
    amt.read(dbFile);
    cout << "[amtdb] constructing database for query ... " << endl;
    AMTDatabase db(amt);

    cout << "[amtdb] reading peptide file ... " << endl;
    //ifstream queryPeptideFile("./2007/20080410-A-18Mix_Data10_msprefix.pep.xml");
    ifstream queryPeptideFile("2007/20080618-A-6mixtestRG_Data08_msprefix.pep.xml");
    PeptideID_dataFetcher pidf_query(queryPeptideFile);
    MSMSPipelineAnalysis mspa_query;
    mspa_query.read(queryPeptideFile);

    cout << "[amtdb] reading feature file ... " << endl;
    //    ifstream queryFeatureFile("./2007/20080410-A-18Mix_Data10_msprefix.features");
    ifstream queryFeatureFile("./2007/20080618-A-6mixtestRG_Data08_msprefix.features");
    Feature_dataFetcher fdf_query(queryFeatureFile);

    DataFetcherContainer dfc(db._peptides, pidf_query, amt._fdf, fdf_query);
    WarpFunctionEnum wfe = Default;
    //SearchNeighborhoodCalculator snc(.001,60);
    NormalDistributionSearch nds(boost::lexical_cast<double>(argv[1]));   

    cout << "[amtdb] querying amt database ... " << endl;
    db.query(dfc,wfe,nds,mspa_query);
    
    /*
    double mz;
    double rt;
    bool done;

        while(!done)
      {
	  cout << "query mz:" << endl;
	  cin >> mz;
	  cout << "query rt: " << endl;
	  cin >> rt;

	  vector<SpectrumQuery> result = db.query(mz,rt);
    
	  ostringstream oss;
	  XMLWriter writer(oss);
	  vector<SpectrumQuery>::iterator it = result.begin();
	  for(; it != result.end(); ++it) it->write(writer);

	  cout << oss.str();
	   
      }
    */
    return 0;

}
