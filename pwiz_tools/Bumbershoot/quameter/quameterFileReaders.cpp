//
// $Id$
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
// The Original Code is the Quameter software.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#include "quameterFileReaders.h"

namespace freicore
{
namespace quameter
{

IDPDBReader::IDPDBReader(const string& file, const string& spectrumSourceId)
    : idpDBFile(file), spectrumSourceId(spectrumSourceId)
{
    sqlite::database db(idpDBFile, sqlite::no_mutex);
    db.execute("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA cache_size=50000");

    /**
    * For metric MS1-5A: Median real value of precursor m/z errors for +2 spectra
    * For metric MS1-5B: Mean of the absolute precursor m/z errors for +2 spectra
    * For metrics MS1-5C and MS1-5D: Median real value and IQR of precursor m/z errors for +2 spectra in ppm
    */
    {
        string deltaMassColumn = g_rtConfig->useAvgMass ? "AvgMassDelta" : "MonoMassDelta";
        string massColumn = g_rtConfig->useAvgMass ? "MolecularWeight" : "MonoisotopicMass";
        string sql = //"SELECT SUM(IFNULL("+deltaMassColumn+",0))+pep."+massColumn+" as CalculatedMass, "
                     //"       psm."+massColumn+"-(SUM(IFNULL("+deltaMassColumn+",0))+pep."+massColumn+") AS MassError "
                     "SELECT PrecursorMZ, "
                     "       ((SUM(IFNULL("+deltaMassColumn+",0))+pep."+massColumn+"+Charge*1.0076)/Charge)-PrecursorMZ AS MzError "
                     "FROM PeptideSpectrumMatch psm "
                     "JOIN Spectrum s ON psm.Spectrum=s.Id "
                     "JOIN Peptide pep ON psm.Peptide=pep.Id "
                     "LEFT JOIN PeptideModification pm ON psm.Id=pm.PeptideSpectrumMatch "
                     "LEFT JOIN Modification mod ON pm.Modification=mod.Id "
                     "WHERE QValue <= ? AND Source=? AND Rank=1 AND Charge=2 "
                     "GROUP BY s.Id "
                     "HAVING MzError <= 0.45";// AND GROUP_CONCAT(DISTINCT Charge)='2'";
        // GROUP_CONCAT(DISTINCT Charge)='2' is used instead of Charge=2 in order to
        // exclude +2 spectra that are also identified as +3s (to be consistent with NIST)
        sqlite::query q(db, sql.c_str());
        q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

        accs::accumulator_set<double, accs::stats<accs::tag::percentile> > massErrors;
        accs::accumulator_set<double, accs::stats<accs::tag::mean> > absMassErrors;
        accs::accumulator_set<double, accs::stats<accs::tag::percentile> > ppmErrors;
        BOOST_FOREACH(sqlite::query::rows row, q)
        {
            double calculatedMass, massError;
            row.getter() >> calculatedMass >> massError;
            double massErrorPPM = (massError/calculatedMass)*1e6;
            massErrors(massError);
            absMassErrors(fabs(massError));
            ppmErrors(massErrorPPM);
        }
        
        absMassErrors(0); // HACK: avoid NaN if there are no rows

        _precursorMassErrorStats.medianError = accs::percentile(massErrors, accs::percentile_number = 50);
        _precursorMassErrorStats.meanAbsError = accs::mean(absMassErrors);
        _precursorMassErrorStats.medianPPMError = accs::percentile(ppmErrors, accs::percentile_number = 50);
        _precursorMassErrorStats.PPMErrorIQR = accs::percentile(ppmErrors, accs::percentile_number = 75) -
                                               accs::percentile(ppmErrors, accs::percentile_number = 25);
    }

    string countBySpecificitySql = "SELECT Specificity, COUNT(*) "
                                   "FROM (SELECT MAX(NTerminusIsSpecific+CTerminusIsSpecific) AS Specificity "
                                   "      FROM PeptideInstance pi "
                                   "      JOIN PeptideSpectrumMatch psm ON pi.Peptide=psm.Peptide "
                                   "      JOIN Spectrum s ON psm.Spectrum=s.Id "
                                   "      WHERE QValue <= ? AND Rank=1 AND Source=? ";

    // For metric P-2A: Find the number of MS2 spectra that identify fully specific distinct matches
    {
        string sql = countBySpecificitySql + "GROUP BY psm.Id) GROUP BY Specificity";

        sqlite::query q(db, sql.c_str());
        q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

        _spectrumCountBySpecificity.resize(3, 0);
        BOOST_FOREACH(sqlite::query::rows row, q)
            _spectrumCountBySpecificity[row.get<int>(0)] = row.get<int>(1);
    }

    // For metric P-2B: Find the number of fully specific distinct matches identified
    {
        string sql = countBySpecificitySql +
                     "GROUP BY (SELECT pi.Peptide || ' ' || Charge || ' ' || "
                     "          IFNULL((SELECT GROUP_CONCAT(Modification || '@' || Offset) "
                     "                  FROM PeptideModification pm WHERE pm.PeptideSpectrumMatch=psm.Id), '') "
                     "         )) GROUP BY Specificity";

        sqlite::query q(db, sql.c_str());
        q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

        _distinctMatchCountBySpecificity.resize(3, 0);
        BOOST_FOREACH(sqlite::query::rows row, q)
            _distinctMatchCountBySpecificity[row.get<int>(0)] = row.get<int>(1);
    }

    // For metric P-2C: Find the number of fully specific distinct peptides identified
    {
        string sql = countBySpecificitySql + "GROUP BY pi.Peptide) GROUP BY Specificity";

        sqlite::database db(idpDBFile);
        sqlite::query q(db, sql.c_str());
        q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

        _distinctPeptideCountBySpecificity.resize(3, 0);
        BOOST_FOREACH(sqlite::query::rows row, q)
            _distinctPeptideCountBySpecificity[row.get<int>(0)] = row.get<int>(1);
    }

    // For metric DS-1[ABC]: Peptide oversampling
    {
        // Spectrum count by distinct match
        string sql = "SELECT COUNT(*) "
                     "FROM PeptideSpectrumMatch psm "
                     "JOIN Spectrum s ON psm.Spectrum=s.Id "
                     "WHERE QValue <= ? AND Rank=1 and Source=? "
                     "GROUP BY (Peptide || ' ' || Charge || ' ' ||"
                     "          IFNULL((SELECT GROUP_CONCAT(Modification || '@' || Offset) "
                     "                  FROM PeptideModification pm WHERE pm.PeptideSpectrumMatch=psm.Id), '')) ";
        sqlite::query q(db, sql.c_str());
        q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

        // only track up to 10 counts
        _peptideSamplingRates.resize(10, 0);
        BOOST_FOREACH(sqlite::query::rows row, q)
        {
            size_t count = (size_t) row.get<int>(0);
            if (count >= _peptideSamplingRates.size())
                continue;
            ++_peptideSamplingRates[count];
        }
    }

    // Returns a map of MS2 native IDs to distinct modified peptide
    {
        string sql = "SELECT NativeID, Peptide, "
                     "       IFNULL((SELECT GROUP_CONCAT(Modification || '@' || Offset) "
                     "               FROM PeptideModification pm WHERE pm.PeptideSpectrumMatch=psm.Id), '') AS Mods "
                     "FROM PeptideSpectrumMatch psm "
                     "JOIN Spectrum s ON psm.Spectrum=s.Id "
                     "WHERE QValue <= ? AND Rank=1 AND Source=? "
                     "GROUP BY s.Id "
                     "ORDER BY Peptide, Mods ";
        sqlite::query q(db, sql.c_str());
        q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

        char const* id;
        sqlite_int64 peptide, lastPeptide = 0;
        string mods, lastMods = "initial value";

        size_t distinctModifiedPeptide = 0;
        BOOST_FOREACH(sqlite::query::rows row, q)
        {
            row.getter() >> id >> peptide >> mods;
            if (peptide != lastPeptide || mods != lastMods)
                ++distinctModifiedPeptide;
            _distinctModifiedPeptideByNativeID[id] = distinctModifiedPeptide;
            lastPeptide = peptide;
            lastMods = mods;
        }
    }

    // Returns a map of MS2 native IDs to charge state(s)
    {
        string sql = "SELECT NativeID, Charge "
                     "FROM PeptideSpectrumMatch psm "
                     "JOIN Spectrum s ON psm.Spectrum=s.Id "
                     "WHERE QValue <= ? AND Rank=1 AND Source=? "
                     "GROUP BY s.Id "
                     "ORDER BY Charge";
        sqlite::query q(db, sql.c_str());
        q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

        BOOST_FOREACH(sqlite::query::rows row, q)
            _chargeStatesByNativeID[row.get<string>(0)].push_back(row.get<int>(1));
    }

    // For metrics IS-3A, IS-3B and IS-3C: Return the number of peptides with a charge of +1, +2, +3 and +4 
    {
        string sql = "SELECT Charge, COUNT(DISTINCT Peptide || ' ' || Charge || ' ' || "
                     "                     IFNULL((SELECT GROUP_CONCAT(Modification || '@' || Offset) "
                     "                             FROM PeptideModification pm WHERE pm.PeptideSpectrumMatch=psm.Id), '')) "
                     "FROM PeptideSpectrumMatch psm "
                     "JOIN Spectrum s ON psm.Spectrum=s.Id "
                     "WHERE QValue <= ? AND Rank=1 AND Source=? "
                     "GROUP BY Charge";
        sqlite::query q(db, sql.c_str());
        q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

        _distinctMatchCountByCharge.resize(5, 0);
        BOOST_FOREACH(sqlite::query::rows row, q)
        {
            size_t charge = (size_t) row.get<int>(0);
            if (_distinctMatchCountByCharge.size() <= charge)
                _distinctMatchCountByCharge.resize(charge+1, 0);
            _distinctMatchCountByCharge[charge] = row.get<int>(1);
        }
    }
}

/**
* For metric P-1: Find the median peptide identification score for all peptides
*/
double IDPDBReader::getMedianIDScore() const
{
    sqlite::database db(idpDBFile);
    static string sql = "SELECT psmScore.Value "
                        "FROM PeptideSpectrumMatch psm "
                        "JOIN PeptideSpectrumMatchScore psmScore ON psm.Id=psmScore.PsmId "
                        "JOIN Spectrum s ON psm.Spectrum=s.Id "
                        "WHERE QValue <= ? AND Rank=1 AND Source=? "
                        "AND psmScore.ScoreNameId = 3 ";
    sqlite::query q(db, sql.c_str());
    q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

    accs::accumulator_set<double, accs::stats<accs::tag::percentile> > scores;
    BOOST_FOREACH(sqlite::query::rows row, q)
        scores(row.get<double>(0));
    return accs::percentile(scores, accs::percentile_number = 50);
}

/**
* For metric IS-2: Find the median precursor m/z of distinct matches
*/
double IDPDBReader::getMedianPrecursorMZ() const
{
    sqlite::database db(idpDBFile);
    static string sql = "SELECT PrecursorMZ "
                        "FROM PeptideSpectrumMatch psm "
                        "JOIN Spectrum s ON psm.Spectrum=s.Id "
                        "WHERE QValue <= ? AND Rank=1 AND Source=? "
                        "GROUP BY (Peptide || ' ' || Charge || ' ' || "
                        "          IFNULL((SELECT GROUP_CONCAT(Modification || '@' || Offset) "
                        "                  FROM PeptideModification pm WHERE pm.PeptideSpectrumMatch=psm.Id), '')) "
                        "ORDER BY PrecursorMZ";

    sqlite::query q(db, sql.c_str());
    q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

    accs::accumulator_set<double, accs::stats<accs::tag::percentile> > precursorMZs;
    BOOST_FOREACH(sqlite::query::rows row, q)
        precursorMZs(row.get<double>(0));

    // return the median of precursorMZ
    return accs::percentile(precursorMZs, accs::percentile_number = 50);
}


struct SortByScanTime
{
    bool operator() (const PeptideSpectrumMatch& lhs, const PeptideSpectrumMatch& rhs) const
    {
        return lhs.spectrum->scanStartTime < rhs.spectrum->scanStartTime;
    }
};

struct ModifyPrecursorMZ
{
    double newMZ;
    ModifyPrecursorMZ(double newMZ) : newMZ(newMZ) {}
    void operator() (MS2ScanInfo& info) const {info.precursorMZ = newMZ;}
};


XICWindowList IDPDBReader::MZRTWindows(MS2ScanMap& ms2ScanMap)
{
    sqlite::database db(idpDBFile);
    string deltaMassColumn = g_rtConfig->useAvgMass ? "AvgMassDelta" : "MonoMassDelta";
    string massColumn = g_rtConfig->useAvgMass ? "MolecularWeight" : "MonoisotopicMass";
    string sql = "SELECT psm.Peptide, NativeID, PrecursorMZ, Charge, IFNULL(Mods, '') AS Mods, psmScore.Value, "
                 "       (IFNULL(TotalModMass,0)+pep."+massColumn+"+Charge*1.0076)/Charge AS ExactMZ, "
                 "       IFNULL(SUBSTR(Sequence, pi.Offset+1, pi.Length),DecoySequence) "
                 "FROM Spectrum s "
                 "JOIN PeptideSpectrumMatch psm ON s.Id=psm.Spectrum "
                 "JOIN Peptide pep ON psm.Peptide=pep.Id "
                 "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide "
                 "LEFT JOIN ProteinData pro ON pi.Protein=pro.Id "
                 "JOIN PeptideSpectrumMatchScore psmScore ON psm.Id=psmScore.PsmId "
                 "LEFT JOIN (SELECT pm.PeptideSpectrumMatch AS PsmId, "
                 "                  GROUP_CONCAT(Modification || '@' || pm.Offset) AS Mods, "
                 "                  SUM("+deltaMassColumn+") AS TotalModMass "
                 "           FROM PeptideModification pm "
                 "           JOIN Modification mod ON pm.Modification=mod.Id "
                 "           GROUP BY PsmId) mods2 ON psm.Id=mods2.PsmId "
                 "WHERE QValue <= ? AND Rank=1 and Source=? AND psmScore.ScoreNameId = 3 "
                 "GROUP BY psm.Id "
                 "ORDER BY psm.Peptide, Charge, Mods";

    sqlite::query q(db, sql.c_str());
    q.binder() << g_rtConfig->ScoreCutoff << spectrumSourceId;

    XICWindowList windows;
    XICWindow tmpWindow;
    PeptideSpectrumMatch tmpPSM;
    sqlite_int64 lastPeptide = 0;
    int lastCharge = 0;
    string lastModif = "initial value";
    SortByScanTime sortByScanTime;
    BOOST_FOREACH(sqlite::query::rows row, q)
    {
        sqlite_int64 peptide;
        char const* id;
        double precursorMZ;
        int charge;
        string modif;
        double score, exactMZ;
        string peptideSequence;

        row.getter() >> peptide >> id >> precursorMZ >> charge >> modif >> score >> exactMZ >> peptideSequence;

        MS2ScanMap::index<nativeID>::type::const_iterator itr = ms2ScanMap.get<nativeID>().find(id);
        if (itr == ms2ScanMap.get<nativeID>().end())
            throw runtime_error(string("PSM identified to spectrum \"") + id + "\" is not in the scan map");

        if (itr->msLevel > 2)
            continue;

        ms2ScanMap.get<nativeID>().modify(itr, ModifyPrecursorMZ(precursorMZ)); // prefer corrected monoisotopic m/z
        const MS2ScanInfo& scanInfo = *itr;

        if (peptide != lastPeptide || charge != lastCharge || modif != lastModif) 
        { // if any of these values change we make a new chromatogram
            if (lastPeptide > 0 && !tmpWindow.PSMs.empty()) 
            {
                sort(tmpWindow.PSMs.begin(), tmpWindow.PSMs.end(), sortByScanTime);
                tmpWindow.meanMS2RT /= tmpWindow.PSMs.size();
                windows.insert(tmpWindow);
            }
            lastPeptide = peptide;
            lastCharge = tmpPSM.charge = charge;
            lastModif = modif;
            tmpPSM.exactMZ = exactMZ;
            tmpWindow.peptide = peptideSequence;
            tmpWindow.firstMS2RT = tmpWindow.lastMS2RT = tmpWindow.meanMS2RT = scanInfo.scanStartTime;
            tmpWindow.maxScore = score;
            tmpWindow.maxScoreScanStartTime = scanInfo.scanStartTime;
            tmpWindow.PSMs.clear();
            tmpWindow.preRT.clear();
            tmpWindow.preMZ.clear();
            if (g_rtConfig->useAvgMass)
                tmpWindow.preMZ = g_rtConfig->chromatogramMzWindow(tmpPSM.exactMZ, tmpPSM.charge);
            else
            {
                IntegerSet::const_iterator itr = g_rtConfig->MonoisotopeAdjustmentSet.begin();
                for (; itr != g_rtConfig->MonoisotopeAdjustmentSet.end(); ++itr)
                    tmpWindow.preMZ += g_rtConfig->chromatogramMzWindow(tmpPSM.exactMZ + *itr * Neutron / tmpPSM.charge, tmpPSM.charge);
            }

            if (!scanInfo.identified)
                throw runtime_error("PSM for spectrum \"" + scanInfo.nativeID + "\" is not identified (should never happen)");

            if (!boost::icl::contains(tmpWindow.preMZ, scanInfo.precursorMZ))
            {
                cerr << "Warning: PSM for spectrum \"" << scanInfo.nativeID << "\" with observed m/z " << scanInfo.precursorMZ << " is disjoint with the exact m/z " << tmpPSM.exactMZ << endl;
                continue;
            }
        }

        tmpPSM.peptide = peptide;
        tmpPSM.spectrum = &scanInfo;
        tmpPSM.score = score;
        tmpWindow.PSMs.push_back(tmpPSM);

        tmpWindow.firstMS2RT = min(tmpWindow.firstMS2RT, scanInfo.scanStartTime);
        tmpWindow.lastMS2RT = max(tmpWindow.lastMS2RT, scanInfo.scanStartTime);
        tmpWindow.meanMS2RT += scanInfo.scanStartTime;
        if (tmpPSM.score > tmpWindow.maxScore)
        {
            tmpWindow.maxScore = tmpPSM.score;
            tmpWindow.maxScoreScanStartTime = scanInfo.scanStartTime;
        }
        tmpWindow.preRT += g_rtConfig->chromatogramScanTimeWindow(scanInfo.precursorScanStartTime);
        //tmpWindow.preMZ += g_rtConfig->chromatogramMzWindow(scanInfo.precursorMZ);
    }

    if (!tmpWindow.PSMs.empty())
    {
        sort(tmpWindow.PSMs.begin(), tmpWindow.PSMs.end(), sortByScanTime);
        tmpWindow.meanMS2RT /= tmpWindow.PSMs.size();
            windows.insert(tmpWindow); // add the last peptide to the vector
    }
    return windows;
}




void ScanRankerReader::extractData()
{
    ifstream reader(srTextFile.c_str());
    
    string input;
    getlinePortable(reader,input);
    while(boost::starts_with(input,"H"))
        getlinePortable(reader,input);
    do
    {
        if(!input.empty())
        {
            tokenizer parser(input, tabDelim);
            tokenizer::iterator itr = parser.begin();
            // Parse the columns
            ++itr;//int spectrumIndex = boost::lexical_cast<int>(*(++itr));
            string nativeID = *(++itr);
            //cout << nativeID << ",";
            double precMZ = boost::lexical_cast<double>(*(++itr));
            //cout << precMZ << ",";
            int charge = boost::lexical_cast<int>(*(++itr));
            //cout << charge << ",";
            double precMass = boost::lexical_cast<double>(*(++itr));
            //cout << precMass << ",";
            double bestTagScore = boost::lexical_cast<double>(*(++itr));
            //cout << bestTagScore << ",";
            double bestTagTIC = boost::lexical_cast<double>(*(++itr));
            //cout << bestTagTIC << ",";
            double tagMzRange = boost::lexical_cast<double>(*(++itr));
            //cout << tagMzRange << ",";
            double srScore = boost::lexical_cast<double>(*(++itr));
            //cout << srScore << endl;
            
            ScanRankerMS2PrecInfo scanInfo;
            scanInfo.nativeID = nativeID;
            scanInfo.precursorMZ = precMZ;
            scanInfo.precursorMass = precMass;
            scanInfo.charge = charge;
            precursorInfos.insert(make_pair(nativeID, scanInfo));
            bestTagScores.insert(make_pair(scanInfo, bestTagScore));
            tagMzRanges.insert(make_pair(scanInfo, tagMzRange));
            scanRankerScores.insert(make_pair(scanInfo, srScore));
            bestTagTics.insert(make_pair(scanInfo, bestTagTIC));
        }

    }while(getlinePortable(reader,input));
}

}
}
