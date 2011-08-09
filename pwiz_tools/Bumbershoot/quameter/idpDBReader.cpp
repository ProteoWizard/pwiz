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
    *
    * @code
    * SELECT DISTINCT NativeID,Peptide,PrecursorMZ,MonoisotopicMassError,MolecularWeightError 
    * FROM PeptideSpectrumMatch JOIN Spectrum 
    * WHERE PeptideSpectrumMatch.QValue <= 0.05 
    * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
    * AND Rank = 1 
    * AND Charge=2 
    * ORDER BY Spectrum
    * @endcode
    */
    double IDPDBReader::MedianRealPrecursorError(const string& spectrumSourceId) {

        sqlite::database db(idpDBFile);
        string s = "SELECT DISTINCT NativeID,Peptide,PrecursorMZ,MonoisotopicMassError,MolecularWeightError from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and Charge=2 Order By Spectrum";
        sqlite::query qry(db, s.c_str() );
        vector<double> realPrecursorErrors;

        for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
            char const* nativeID;
            int peptideTemp;
            double precursorMZ, monoisotopicMassError, molecularWeightError;
            (*i).getter() >> nativeID >> peptideTemp >> precursorMZ >> monoisotopicMassError >> molecularWeightError;
            double massError = min(fabs(monoisotopicMassError),fabs(molecularWeightError));
            if (fabs(massError) <= 0.45)
                realPrecursorErrors.push_back(massError);
        }

        sort(realPrecursorErrors.begin(), realPrecursorErrors.end());
        return Q2(realPrecursorErrors);
    }
    
    /**
        * For metric MS1-5B: Find the mean of the absolute precursor errors
        *
        * @code
        * SELECT DISTINCT NativeID,Peptide,PrecursorMZ,MonoisotopicMassError,MolecularWeightError 
        * FROM PeptideSpectrumMatch JOIN Spectrum 
        * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        * AND Rank = 1 
        * AND Charge=2 
        * ORDER BY Spectrum
        * @endcode
        */
        double IDPDBReader::GetMeanAbsolutePrecursorErrors(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "SELECT DISTINCT NativeID,Peptide,PrecursorMZ,MonoisotopicMassError,MolecularWeightError from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and Charge=2 Order By Spectrum";
            sqlite::query qry(db, s.c_str() );
            vector<double> absolutePrecursorErrors;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                char const* nativeID;
                int peptideTemp;
                double precursorMZ, monoisotopicMassError, molecularWeightError;
                (*i).getter() >> nativeID >> peptideTemp >> precursorMZ >> monoisotopicMassError >> molecularWeightError;
                double massError = min(fabs(monoisotopicMassError),fabs(molecularWeightError));
                if (fabs(massError) <= 0.45)
                    absolutePrecursorErrors.push_back(fabs(massError));
            }

            sort(absolutePrecursorErrors.begin(), absolutePrecursorErrors.end());
            return ( (absolutePrecursorErrors[0] + absolutePrecursorErrors[absolutePrecursorErrors.size()-1]) / 2 );
        }

        /**
        * For metrics MS1-5C and MS1-5D: Find the median real value and interquartile distance of precursor errors (both in ppm)
        *
        * @code
        * SELECT DISTINCT NativeID, Peptide, PrecursorMZ, 
        * ((SUM(MonoMassDelta)+Peptide.MonoisotopicMass-psm.MonoisotopicMass)/(2*(SUM(MonoMassDelta)+Peptide.MonoisotopicMass)))*1000000 as PPMError 
        * FROM PeptideSpectrumMatch psm JOIN Spectrum JOIN Peptide JOIN PeptideModification pm JOIN Modification mod 
        * WHERE psm.Spectrum = Spectrum.Id 
        * AND psm.Peptide = Peptide.Id 
        * AND pm.PeptideSpectrumMatch=psm.Id 
        * AND mod.Id=pm.Modification 
        * AND Rank = 1 
        * AND Charge=2 
        * AND abs(MonoisotopicMassError) <= 0.45 
        * GROUP BY psm.Id 
        * ORDER BY PPMError
        * @endcode
        */
        PPMMassError IDPDBReader::GetRealPrecursorErrorPPM(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "SELECT DISTINCT NativeID, Peptide, PrecursorMZ, ((SUM(MonoMassDelta)+Peptide.MonoisotopicMass-psm.MonoisotopicMass)/(2*(SUM(MonoMassDelta)+Peptide.MonoisotopicMass)))*1000000 as PPMError from PeptideSpectrumMatch psm JOIN Spectrum JOIN Peptide JOIN PeptideModification pm JOIN Modification mod where psm.Spectrum = Spectrum.Id and psm.Peptide = Peptide.Id and pm.PeptideSpectrumMatch=psm.Id and mod.Id=pm.Modification and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and Charge=2 and abs(MonoisotopicMassError) <= 0.45 group by psm.Id order by PPMError";

            sqlite::query qry(db, s.c_str() );
            vector<double> realPrecursorErrorsPPM;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                char const* nativeID;
                int peptideTemp;
                double precursorMZ, ppmError;
                (*i).getter() >> nativeID >> peptideTemp >> precursorMZ >> ppmError;
                realPrecursorErrorsPPM.push_back(ppmError);
            }
            
            // Find the median for metric MS1-5C
            double median = Q2(realPrecursorErrorsPPM);
            // The interquartile range is the distance between Q1 and Q3
            double interquartileRange = Q3(realPrecursorErrorsPPM) - Q1(realPrecursorErrorsPPM);
            return PPMMassError(median,interquartileRange);
        }

        /**
        * For metric P-1: Find the median peptide identification score for all peptides
        *
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
            string s = "SELECT DISTINCT PeptideSpectrumMatch.Peptide, PeptideSpectrumMatchScore.Value from PeptideSpectrumMatch join PeptideSpectrumMatchScore join spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.spectrum = spectrum.id and PeptideSpectrumMatch.id = PeptideSpectrumMatchScore.PsmId and PeptideSpectrumMatchScore.ScoreNameId = 3 and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " order by PeptideSpectrumMatchScore.Value";
            sqlite::query qry(db, s.c_str() );
            vector<double> idScore;
            int peptideTemp;
            double scoreTemp;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                (*i).getter() >> peptideTemp >> scoreTemp;
                idScore.push_back(scoreTemp);
            }

            return Q2(idScore);
        }

        /**
        * For metric P-2A: Find the number of MS2 spectra that identify tryptic peptide ions
        *
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
        *
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
        *
        * @code
        * SELECT COUNT(DISTINCT PeptideInstance.Peptide) 
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
        int IDPDBReader::GetNumUniqueTrypticPeptides(const string& spectrumSourceId)
        {

            sqlite::database db(idpDBFile);
            string s = "select count(distinct PeptideInstance.Peptide) from PeptideInstance join Spectrum join PeptideSpectrumMatch join PeptideModification where PeptideSpectrumMatch.QValue <= 0.05 and PeptideInstance.peptide = PeptideSpectrumMatch.peptide and PeptideModification.PeptideSpectrumMatch = PeptideSpectrumMatch.id and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id and Spectrum.Source = " + spectrumSourceId + " and NTerminusIsSpecific = 1 and CTerminusIsSpecific = 1";
            sqlite::query qry(db, s.c_str() );
            int uniqueTrypticPeptides;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                (*i).getter() >> uniqueTrypticPeptides;
            } 

            return uniqueTrypticPeptides;
        }

        /**
        * For metric P-3: Find the ratio of semi- over fully-tryptic peptide IDs.
        *
        * @code
        * SELECT COUNT(DISTINCT PeptideInstance.Peptide) 
        * FROM PeptideInstance JOIN Spectrum JOIN PeptideSpectrumMatch
        * WHERE PeptideSpectrumMatch.QValue <= 0.05 
        * AND PeptideInstance.Peptide = PeptideSpectrumMatch.Peptide 
        * AND Rank = 1 
        * AND PeptideSpectrumMatch.Spectrum = Spectrum.Id 
        * AND ((NTerminusIsSpecific = 1 AND CTerminusIsSpecific = 0) 
        *	OR (NTerminusIsSpecific = 0 AND CTerminusIsSpecific = 1))
        * @endcode
        */
        int IDPDBReader::GetNumUniqueSemiTrypticPeptides(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "select count(distinct PeptideInstance.Peptide) from PeptideInstance join Spectrum join PeptideSpectrumMatch where PeptideSpectrumMatch.QValue <= 0.05 and PeptideInstance.peptide = PeptideSpectrumMatch.peptide and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id and Spectrum.Source = " + spectrumSourceId + " and ((NTerminusIsSpecific = 1 and CTerminusIsSpecific = 0) or (NTerminusIsSpecific = 0 and CTerminusIsSpecific = 1))";
            sqlite::query qry(db, s.c_str() );
            int uniqueSemiTrypticPeptides;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                (*i).getter() >> uniqueSemiTrypticPeptides;
            } 

            return uniqueSemiTrypticPeptides;
        }

/**
        * For metric DS-1A: Finds the number of peptides identified by one spectrum
        *
        * @code
        * SELECT COUNT(*) 
        * FROM PeptideSpectrumMatch 
        * WHERE Peptide IN 
        *	(SELECT Peptide 
        *	FROM PeptideSpectrumMatch JOIN Spectrum 
        *	WHERE PeptideSpectrumMatch.QValue <= 0.05 
        *	AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        *	AND Rank = 1 
        *	GROUP BY Peptide HAVING COUNT(Peptide)=1 
        *	ORDER BY COUNT(Peptide))
        * @endcode
        */
        int IDPDBReader::PeptidesIdentifiedOnce(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "select count(*) from PeptideSpectrumMatch where Peptide IN (select Peptide from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " GROUP BY Peptide HAVING COUNT(Peptide)=1 ORDER BY Count(peptide))";
            sqlite::query qry(db, s.c_str() );
            int identifiedOnce;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                (*i).getter() >> identifiedOnce;
            }

            return identifiedOnce;
        }

        /**
        * For metrics DS-1A and DS-1B: Finds the number of peptides identified by two spectra
        *
        * @code
        * SELECT COUNT(*)/2
        * FROM PeptideSpectrumMatch 
        * WHERE Peptide IN 
        *	(SELECT Peptide 
        *	FROM PeptideSpectrumMatch JOIN Spectrum 
        *	WHERE PeptideSpectrumMatch.QValue <= 0.05 
        *	AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        *	AND Rank = 1 
        *	GROUP BY Peptide HAVING COUNT(Peptide)=2 
        *	ORDER BY COUNT(Peptide))
        * @endcode
        */
        int IDPDBReader::PeptidesIdentifiedTwice(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "select COUNT(*)/2 from PeptideSpectrumMatch where Peptide IN (select Peptide from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " GROUP BY Peptide HAVING COUNT(Peptide)=2 ORDER BY Count(peptide))";
            sqlite::query qry(db, s.c_str() );
            int identifiedTwice;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                (*i).getter() >> identifiedTwice;
            }

            return identifiedTwice;
        }

        /**
        * For metric DS-1B: Finds the number of peptides identified by three spectra
        *
        * @code
        * SELECT COUNT(*)/3
        * FROM PeptideSpectrumMatch 
        * WHERE Peptide IN 
        *	(SELECT Peptide 
        *	FROM PeptideSpectrumMatch JOIN Spectrum 
        *	WHERE PeptideSpectrumMatch.QValue <= 0.05 
        *	AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
        *	AND Rank = 1 
        *	GROUP BY Peptide HAVING COUNT(Peptide)=3 
        *	ORDER BY COUNT(Peptide))
        * @endcode
        */
        int IDPDBReader::PeptidesIdentifiedThrice(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "select COUNT(*)/3 from PeptideSpectrumMatch where Peptide IN (select Peptide from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id and Spectrum.Source = " + spectrumSourceId + " GROUP BY Peptide HAVING COUNT(Peptide)=3 ORDER BY Count(peptide))";
            sqlite::query qry(db, s.c_str() );
            int identifiedThrice;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                (*i).getter() >> identifiedThrice;
            }

            return identifiedThrice;
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
        double IDPDBReader::MedianPrecursorMZ(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "SELECT DISTINCT NativeID, precursorMZ FROM PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " ORDER BY PrecursorMZ";
            sqlite::query qry(db, s.c_str() );
            vector<double> precursorMZ;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                char const* nativeID;
                double mz;
                (*i).getter() >> nativeID >> mz;
                precursorMZ.push_back(mz);		
            }

            // return the median of precursorMZ
            return Q2(precursorMZ);
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
        vector<string> IDPDBReader::GetNativeId(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "SELECT DISTINCT NativeID FROM PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " ORDER BY Spectrum.Id";
            sqlite::query qry(db, s.c_str() );
            vector<string> nativeIdV;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                char const* nativeID;
                (*i).getter() >> nativeID;
                string idScanNumStr(nativeID);
                nativeIdV.push_back( idScanNumStr );		
            }

            return nativeIdV;
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
        multimap<int, string> IDPDBReader::GetDuplicateID(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "select distinct NativeID,Peptide from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and Peptide IN (select Peptide from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 GROUP BY Peptide HAVING COUNT(Peptide) > 1 ORDER BY NativeID) ORDER BY Peptide";
            sqlite::query qry(db, s.c_str() );
            multimap<int, string> duplicatePeptides;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
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
        fourInts IDPDBReader::PeptideCharge(const string& spectrumSourceId) {

            sqlite::database db(idpDBFile);
            string s = "select distinct Peptide,Charge from PeptideSpectrumMatch JOIN Spectrum where PeptideSpectrumMatch.QValue <= 0.05 and PeptideSpectrumMatch.Spectrum=Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " ORDER BY Peptide";
            sqlite::query qry(db, s.c_str() );
            int charge1 = 0, charge2 = 0, charge3 = 0, charge4 = 0;

            for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
                int peptideID, chargeNum;
                (*i).getter() >> peptideID >> chargeNum;
                if (chargeNum == 1)
                    charge1++;
                else if (chargeNum == 2)
                    charge2++;
                else if (chargeNum == 3)
                    charge3++;
                else if (chargeNum == 4)
                    charge4++;
                //silent		else
                //silent			cout << "Peptide " << peptideID << "'s charge of " << chargeNum << " was not used in metrics IS-3A, IS-3B, or IS3-C.\n";
            }

            fourInts charges;
            charges.first = charge1;
            charges.second = charge2;
            charges.third = charge3;
            charges.fourth = charge4;

            return charges;
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
