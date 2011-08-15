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

#include "idpDBReader.h"

namespace freicore
{
namespace quameter
{

    /**
    * For metric MS1-5A: Find the median real value of precursor errors
    * For metric MS1-5B: Find the mean of the absolute precursor errors
    * For metrics MS1-5C and MS1-5D: Find the median real value and interquartile distance of precursor errors (both in ppm)
    * @code
    * SELECT DISTINCT NativeID, Peptide, PrecursorMZ, psm.MonoisotopicMass as PrecMonoMass, psm.MolecularWeight as PrecAvgMass, \
            (SUM(MonoMassDelta)+Peptide.MonoIsotopicMass) as PepMonoMass, \
            (SUM(MonoMassDelta)+Peptide.MolecularWeight) as PepAvgMass \
        from PeptideSpectrumMatch psm JOIN Spectrum JOIN Peptide JOIN PeptideModification pm JOIN Modification mod \
        where psm.Spectrum = Spectrum.Id and psm.Peptide = Peptide.Id \
        and pm.PeptideSpectrumMatch=psm.Id and mod.Id=pm.Modification \
        and Rank = 1 and Spectrum.Source = " + spectrumSourceId + "\
        and Charge=2 \
        group by psm.Id
    * @endcode
    */
        MassErrorStats IDPDBReader::getPrecursorMassErrorStats(const string& spectrumSourceId) 
        {
            sqlite::database db(idpDBFile);
            string query_sql = "SELECT DISTINCT NativeID, Peptide, PrecursorMZ, psm.MonoisotopicMass as PrecMonoMass, psm.MolecularWeight as PrecAvgMass, "
                                "(SUM(MonoMassDelta)+Peptide.MonoIsotopicMass) as PepMonoMass, "
                                "(SUM(MonoMassDelta)+Peptide.MolecularWeight) as PepAvgMass "
                                "from PeptideSpectrumMatch psm JOIN Spectrum JOIN Peptide JOIN PeptideModification pm JOIN Modification mod "
                                "where psm.Spectrum = Spectrum.Id and psm.Peptide = Peptide.Id "
                                "and pm.PeptideSpectrumMatch=psm.Id and mod.Id=pm.Modification "
                                "and Rank = 1 and Spectrum.Source = " + spectrumSourceId + 
                                "and Charge=2 "
                                "group by psm.Id";
            sqlite::query qry(db, query_sql.c_str() );
            accs::accumulator_set<double, accs::stats<accs::tag::median, accs::tag::mean > > massErrors;
            accs::accumulator_set<double, accs::stats<accs::tag::median, accs::tag::mean > > absMassErrors;
            accs::accumulator_set<double, accs::stats<accs::tag::median, accs::tag::mean > > ppmErrors;
            quartile<double> ppmErrorsForIQR;
            for (sqlite::query::iterator qIter = qry.begin(); qIter != qry.end(); ++qIter) {
                char const* nativeID;
                int peptideID;
                double precursorMZ, precMono, precAvg, pepMono, pepAvg;
                (*qIter).getter() >> nativeID >> peptideID >> precursorMZ >> precMono >> precAvg >> pepMono >> pepAvg;
                double massError = precMono-pepMono;
                double massErrorPPM = (massError/pepMono)*1e6;
                if(bal::equals(g_rtConfig->Instrument,"ltq"))
                {
                    massError = precAvg-pepAvg;
                    massErrorPPM = (massError/pepAvg)*1e6;
                }
                massErrors(massError);
                absMassErrors(fabs(massError));
                ppmErrors(massErrorPPM);
                ppmErrorsForIQR(massErrorPPM);
            }
            
            MassErrorStats massErrorStats;
            massErrorStats.medianError = accs::extract::median(massErrors);
            massErrorStats.meanAbsError = accs::extract::mean(absMassErrors);
            massErrorStats.medianPPMError = accs::extract::median(ppmErrors);
            massErrorStats.PPMErrorIQR = ppmErrorsForIQR.extract_IQR();
            return massErrorStats;
        }

        /**
        * For metric P-1: Find the median peptide identification score for all peptides
        * @code
        * SELECT DISTINCT PeptideSpectrumMatch.Peptide, PeptideSpectrumMatchScore.Value 
        * FROM PeptideSpectrumMatch JOIN PeptideSpectrumMatchScore JOIN spectrum 
        * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideSpectrumMatch.Spectrum = Spectrum.Id 
        * AND PeptideSpectrumMatch.Id = PeptideSpectrumMatchScore.PsmId 
        * AND PeptideSpectrumMatchScore.ScoreNameId = 3 
        * AND Rank = 1 
        * ORDER BY PeptideSpectrumMatchScore.Value
        * @endcode
        */
        double IDPDBReader::GetMedianIDScore(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string query_sql = "SELECT DISTINCT PeptideSpectrumMatch.Peptide, PeptideSpectrumMatchScore.Value from PeptideSpectrumMatch join PeptideSpectrumMatchScore join spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.spectrum = spectrum.id and PeptideSpectrumMatch.id = PeptideSpectrumMatchScore.PsmId and PeptideSpectrumMatchScore.ScoreNameId = 3 and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " order by PeptideSpectrumMatchScore.Value";
            sqlite::query qry(db, query_sql.c_str() );
            accs::accumulator_set<double, accs::stats<accs::tag::median> > scores;
            
            int peptideID;
            double score;
            for (sqlite::query::iterator qIter = qry.begin(); qIter != qry.end(); ++qIter) 
            {
                (*qIter).getter() >> peptideID >> score;
                scores(score);
            }
            return accs::extract::median(scores);
        }

        /**
        * For metric P-2A: Find the number of MS2 spectra that identify tryptic peptide ions
        * @code
        * SELECT COUNT(distinct nativeid) 
        * FROM PeptideInstance JOIN PeptideSpectrumMatch JOIN Spectrum 
        * WHERE PeptideInstance.Peptide = PeptideSpectrumMatch.Peptide 
        * AND PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptidespectrumMatch.Spectrum = Spectrum.Id 
        * AND Rank = 1 
        * AND NTerminusIsSpecific = 1 
        * AND CTerminusIsSpecific = 1 
        * ORDER BY Spectrum.Id
        * @endcode
        */
        int IDPDBReader::GetNumTrypticMS2Spectra(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "SELECT COUNT(distinct nativeid) FROM PeptideInstance JOIN PeptideSpectrumMatch JOIN Spectrum WHERE PeptideInstance.peptide = PeptideSpectrumMatch.peptide AND PeptideSpectrumMatch.QValue <= 0.05 and peptidespectrummatch.spectrum = spectrum.id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and NTerminusIsSpecific = 1 and CTerminusIsSpecific = 1 order by spectrum.id";
            sqlite::query qry(db, s.c_str() );
            int trypticMS2Spectra;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                (*i).getter() >> trypticMS2Spectra;
            }

            return (int)trypticMS2Spectra;
        }
/**
        * For metric P-2B: Find the number of tryptic peptide ions identified.
        * Ions with different charge states or modifications are counted separately.
        * @code
        * SELECT DISTINCT PeptideInstance. Peptide, Charge, Modification 
        * FROM PeptideInstance JOIN Spectrum JOIN PeptideSpectrumMatch JOIN PeptideModification 
        * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideInstance.Peptide = PeptideSpectrumMatch.Peptide 
        * AND PeptideModification.PeptideSpectrumMatch = PeptideSpectrumMatch.Id 
        * AND Rank = 1 
        * AND PeptideSpectrumMatch.Spectrum = Spectrum.Id 
        * AND NTerminusIsSpecific = 1 
        * AND CTerminusIsSpecific = 1
        * @endcode
        */
        int IDPDBReader::GetNumTrypticPeptides(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "select distinct PeptideInstance.Peptide, charge, modification from PeptideInstance join Spectrum join PeptideSpectrumMatch join PeptideModification where PeptideSpectrumMatch.QValue <= 0.05 and PeptideInstance.peptide = PeptideSpectrumMatch.peptide and PeptideModification.PeptideSpectrumMatch = PeptideSpectrumMatch.id and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id and Spectrum.Source = " + spectrumSourceId + " and NTerminusIsSpecific = 1 and CTerminusIsSpecific = 1";
            sqlite::query qry(db, s.c_str() );
            int trypticPeptides = 0;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                trypticPeptides++;
            } 

            return trypticPeptides;
        }

        /**
        * For metric P-2C: Find the number of unique tryptic peptide sequences identified
        * For metric P-3: Find the ratio of semi- over fully-tryptic peptide IDs.
        * @code
        * select distinct PeptideInstance.Peptide, NTerminusIsSpecific, CTerminusIsSpecific 
            from PeptideInstance join Spectrum join PeptideSpectrumMatch 
            were PeptideSpectrumMatch.QValue <= 0.05 
            and PeptideInstance.peptide = PeptideSpectrumMatch.peptide 
            and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id  
            and Spectrum.Source = 1 
        * @endcode
        */
        vector<size_t> IDPDBReader::getUniqueTrypticSemiTryptics(const string& spectrumSourceId)
        {

            sqlite::database db(idpDBFile);
            string query_sql = "select distinct PeptideInstance.Peptide, NTerminusIsSpecific, CTerminusIsSpecific "
                                    "from PeptideInstance join Spectrum join PeptideSpectrumMatch "
                               "where PeptideSpectrumMatch.QValue <= 0.05 "
                               "and PeptideInstance.peptide = PeptideSpectrumMatch.peptide "
                               "and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id  "
                               "and Spectrum.Source = " + spectrumSourceId ;
            sqlite::query qry(db, query_sql.c_str() );
            map<size_t, map<size_t, set<size_t> > > peptideCounts;
            int peptideID, nTerm, cTerm;
            for (sqlite::query::iterator qIter = qry.begin(); qIter != qry.end(); ++qIter) 
            {
                (*qIter).getter() >> peptideID >> nTerm >> cTerm;
                peptideCounts[nTerm][cTerm].insert(peptideID);
            }
            vector<size_t> counts(3);
            std::fill(counts.begin(),counts.end(),0);
            counts[NON_ENZYMATIC] = peptideCounts[0][0].size();
            counts[SEMI_ENZYMATIC] = peptideCounts[0][1].size() + peptideCounts[1][0].size();
            counts[FULLY_ENZYMATIC] = peptideCounts[1][1].size();
            return counts;
        }

/**
        * For metric DS-1A: Finds the number of peptides identified by one spectrum
        *
        * @code
        * SELECT Peptide, COUNT(*)
        * FROM PeptideSpectrumMatch JOIN Spectrum 
	    * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
	    * AND Rank = 1 
        * GROUP BY Peptide 
        * @endcode
        */
        map<size_t,size_t> IDPDBReader::getPeptideSamplingRates(const string& spectrumSourceId) 
        {
            sqlite::database db(idpDBFile);
            string query_sql = "SELECT Peptide, COUNT(*) "
                                "FROM PeptideSpectrumMatch JOIN Spectrum "
                                "WHERE PeptideSpectrumMatch.QValue <= 0.05 "
                                "AND PeptideSpectrumMatch.Spectrum=Spectrum.Id "
                                "AND Rank = 1 and Spectrum.Source = " + spectrumSourceId + 
                                "GROUP BY Peptide";
            sqlite::query qry(db, query_sql.c_str() );
            map<size_t,size_t> peptideIDRates;
            for (sqlite::query::iterator qIter = qry.begin(); qIter != qry.end(); ++qIter) 
            {
                int peptideID;
                int spectralCount;
                (*qIter).getter() >> peptideID >> spectralCount;
                ++peptideIDRates[spectralCount];
            }
            // return the counts
            return peptideIDRates;
        }

        /**
        * For metric IS-2: Find the median precursor m/z of unique ions of id'd peptides
        *
        * @code
        * SELECT DISTINCT NativeID, PrecursorMZ 
        * FROM PeptideSpectrumMatch JOIN Spectrum 
        * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        * AND Rank = 1 
        * ORDER BY PrecursorMZ
        * @endcode
        */
        double IDPDBReader::getMedianPrecursorMZ(const string& spectrumSourceId) 
        {
            sqlite::database db(idpDBFile);
            string query_sql = "SELECT DISTINCT NativeID, precursorMZ "
                                    "FROM PeptideSpectrumMatch JOIN Spectrum "
                                "where PeptideSpectrumMatch.QValue <= 0.05 "
                                "and PeptideSpectrumMatch.Spectrum=Spectrum.Id "
                                "and Rank = 1 and Spectrum.Source = " + spectrumSourceId + 
                                " ORDER BY PrecursorMZ";
            sqlite::query qry(db, query_sql.c_str() );
            accs::accumulator_set<double, accs::stats<accs::tag::median> > precursorMZs;

            for (sqlite::query::iterator qIter = qry.begin(); qIter != qry.end(); ++qIter) 
            {
                char const* nativeID;
                double mz;
                (*qIter).getter() >> nativeID >> mz;
                precursorMZs(mz);		
            }
            // return the median of precursorMZ
            return accs::extract::median(precursorMZs);
        }

        /**
        * Query the idpDB and return with a list of MS2 native IDs for all identified peptides
        *
        * @code
        * SELECT DISTINCT NativeID 
        * FROM PeptideSpectrumMatch JOIN Spectrum 
        * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        * AND Rank = 1 
        * AND Spectrum.Source
        * ORDER BY Spectrum.Id
        * @endcode
        */
        set<string> IDPDBReader::GetNativeId(const string& spectrumSourceId) 
        {
            sqlite::database db(idpDBFile);
            string s = "SELECT DISTINCT NativeID FROM PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " ORDER BY Spectrum.Id";
            sqlite::query qry(db, s.c_str() );
            set<string> nativeIds;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) 
            {
                char const* nativeID;
                (*i).getter() >> nativeID;
                //string idScanNumStr(nativeID);
                nativeIds.insert( boost::lexical_cast<string>(nativeID) );		
            }

            return nativeIds;
        }

        /**
        * Finds duplicate peptide IDs. Used in metrics C-1A and C-1B.
        *
        * @code
        * SELECT distinct NativeID,Peptide 
        * FROM PeptideSpectrumMatch JOIN Spectrum 
        * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        * AND Rank = 1 
        * AND Peptide IN
        * 	(SELECT Peptide 
        *	FROM PeptideSpectrumMatch JOIN Spectrum 
        *	WHERE PeptideSpectrumMatch.QValue <= 0.05 
        *	AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        *	AND Rank = 1 
        *	GROUP BY Peptide 
        *	HAVING COUNT(Peptide) > 1 
        *	ORDER BY NativeID) 
        * ORDER BY Peptide
        * @endcode
        */
        multimap<int, string> IDPDBReader::GetDuplicateID(const string& spectrumSourceId) 
        {

            sqlite::database db(idpDBFile);
            string s = "select distinct NativeID,Peptide from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and Peptide IN (select Peptide from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 GROUP BY Peptide HAVING COUNT(Peptide) > 1 ORDER BY NativeID) ORDER BY Peptide";
            sqlite::query qry(db, s.c_str() );
            multimap<int, string> duplicatePeptides;
            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) 
            {
                char const* nativeID;
                int peptideTemp;
                (*i).getter() >> nativeID >> peptideTemp;
                string idScanNumStr(nativeID);
                duplicatePeptides.insert(pair<int, string>(peptideTemp, idScanNumStr));	
            }

            return duplicatePeptides;
        }

        /**
        * For metrics IS-3A, IS-3B and IS-3C: Return the number of peptides with a charge of +1, +2, +3 and +4 
        *
        * @code
        * SELECT DISTINCT Peptide,Charge 
        * FROM PeptideSpectrumMatch JOIN Spectrum 
        * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        * AND Rank = 1 
        * ORDER BY Peptide
        * @endcode
        */
        map<size_t,size_t> IDPDBReader::getPeptideCharges(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string query_str = "select distinct Peptide,Charge"
                                "from PeptideSpectrumMatch JOIN Spectrum "
                                "where PeptideSpectrumMatch.QValue <= 0.05 "
                                "and PeptideSpectrumMatch.Spectrum=Spectrum.Id "
                                "and Rank = 1 and Spectrum.Source = " + spectrumSourceId + 
                                " ORDER BY Peptide";
            sqlite::query qry(db, query_str.c_str() );
            map<size_t, size_t> chargeStats;
            for (sqlite::query::iterator qIter = qry.begin(); qIter != qry.end(); ++qIter) 
            {
                int peptideID, charge;
                (*qIter).getter() >> peptideID >> charge;
                ++chargeStats[(size_t)charge];
            }
            return chargeStats;
        }

        /**
        * Used for peak finding of identified peptides
        *
        * @code
        * SELECT DISTINCT Peptide, NativeID, PrecursorMZ, IFNULL(GROUP_CONCAT(Modification || "@" || Offset), '') 
        * FROM Spectrum JOIN PeptideSpectrumMatch psm 
        * LEFT JOIN PeptideModification pm
        * ON pm.PeptideSpectrumMatch = psm.id 
        * WHERE psm.Spectrum = Spectrum.Id 
        * AND Rank = 1 
        * AND QValue <= 0.05 
        * GROUP BY psm.Id 
        * ORDER BY Peptide, Modification, Offset
        * @endcode
        */
        vector<XICWindows> IDPDBReader::MZRTWindows( const string& spectrumSourceId, map<string, int> nativeToArrayMap, vector<MS2ScanInfo> scanInfo) {

            sqlite::database db(idpDBFile);
            string s = "select distinct peptide, nativeID, precursorMZ, IFNULL(GROUP_CONCAT(modification || \"@\" || offset), '') from spectrum join peptidespectrummatch psm left join peptidemodification pm on pm.peptidespectrummatch = psm.id where psm.spectrum = Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and qvalue < 0.05 group by psm.id order by peptide, modification, offset";
            sqlite::query qry(db, s.c_str() );

            vector<XICWindows> pepWin;
            XICWindows tmpWindow;
            int lastPeptide = -876; // initial nonsense value
            std::string lastModif = "this is not a modification"; // initial nonsense value

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                char const* nativeID;
                int peptide;
                double precursorMZ;
                std::string modif;

                (*i).getter() >> peptide >> nativeID >> precursorMZ >> modif;

                int amalgLoc = nativeToArrayMap.find(nativeID)->second; // location in ms_amalgam array
                if (peptide != lastPeptide || modif.compare(lastModif) != 0) { // if either of these values change we make a new chromatogram
                    if (lastPeptide != -876) pepWin.push_back(tmpWindow);
                    lastPeptide = peptide;
                    tmpWindow.peptide = peptide;
                    tmpWindow.firstMS2RT = scanInfo[amalgLoc].MS2Retention;
                    tmpWindow.preMZ.clear();
                    tmpWindow.preRT.clear();			
                }

                if (scanInfo[amalgLoc].MS2Retention < tmpWindow.firstMS2RT) tmpWindow.firstMS2RT = scanInfo[amalgLoc].MS2Retention;
                double mzLower = precursorMZ - 0.5;
                double mzUpper = precursorMZ + 1.0;
                //double mzLower = precursorMZ - (precursorMZ / 100000); // lower bound for m/z interval; 10ppm = 10/1,000,000 = 1/100,000
                //double mzUpper = precursorMZ + (precursorMZ / 100000); // upper bound for m/z interval
                continuous_interval<double> mzWindow = construct<continuous_interval<double> >(mzLower, mzUpper, interval_bounds::closed());
                tmpWindow.preMZ.insert(mzWindow);
                double RTLower = scanInfo[amalgLoc].precursorRetention - 300; // lower bound for RT interval
                double RTUpper = scanInfo[amalgLoc].precursorRetention + 300; // lower bound for RT interval	
                continuous_interval<double> RTWindow = construct<continuous_interval<double> >(RTLower, RTUpper, interval_bounds::closed());
                tmpWindow.preRT.insert(RTWindow);
                lastModif = modif;
            }
            pepWin.push_back(tmpWindow); // add the last peptide to the vector			
            return pepWin;
        }

}
}
