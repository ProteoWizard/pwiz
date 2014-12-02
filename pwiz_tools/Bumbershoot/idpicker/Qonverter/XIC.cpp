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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

#include "Embedder.hpp"
#include "Qonverter.hpp"
#include "SchemaUpdater.hpp"
#include "Interpolator.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/ThresholdFilter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.hpp"
#include "boost/foreach_field.hpp"
#include "boost/throw_exception.hpp"
#include "boost/xpressive/xpressive.hpp"

BEGIN_IDPICKER_NAMESPACE
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;
using namespace pwiz::chemistry;
//namespace sqlite = sqlite;
using namespace Embedder;
using pwiz::chemistry::MZTolerance;

namespace XIC {

#ifdef WIN32
const string defaultSourceExtensionPriorityList("mz5;mzML;mzXML;RAW;WIFF;d;t2d;ms2;cms2;mgf");
#else
const string defaultSourceExtensionPriorityList("mz5;mzML;mzXML;ms2;cms2;mgf");
#endif

XICConfiguration::XICConfiguration(bool AlignRetentionTime, string RTFolder, double MaxQValue,
                     const IntegerSet& MonoisotopicAdjustmentSet,
                     int RetentionTimeLowerTolerance, int RetentionTimeUpperTolerance,
                     MZTolerance ChromatogramMzLowerOffset, MZTolerance ChromatogramMzUpperOffset)
                     : AlignRetentionTime(AlignRetentionTime), RTFolder(RTFolder), MaxQValue(MaxQValue), MonoisotopicAdjustmentSet(MonoisotopicAdjustmentSet), RetentionTimeLowerTolerance(RetentionTimeLowerTolerance), RetentionTimeUpperTolerance(RetentionTimeUpperTolerance), ChromatogramMzLowerOffset(ChromatogramMzLowerOffset), ChromatogramMzUpperOffset(ChromatogramMzUpperOffset)
{}

XICWindowList GetMZRTWindows(sqlite::database& db, MS2ScanMap& ms2ScanMap, const string& sourceId, XICConfiguration config)
{
    // use avg mass if either offset is not in PPM units and the total tolerance in Daltons is greater than 1
    bool useAvgMass = config.ChromatogramMzUpperOffset.units != MZTolerance::PPM && config.ChromatogramMzLowerOffset.units != MZTolerance::PPM &&
                      config.ChromatogramMzUpperOffset.value + config.ChromatogramMzLowerOffset.value > 1;
    string deltaMassColumn = useAvgMass ? "AvgMassDelta" : "MonoMassDelta";
    string massColumn = useAvgMass ? "MolecularWeight" : "MonoisotopicMass";
    string sql = "SELECT psm.Id, psm.Peptide, Source, NativeID, PrecursorMZ, Charge, IFNULL(Mods, '') AS Mods, Qvalue,  "
                "(IFNULL(TotalModMass,0)+pep."+massColumn+"+Charge*1.0076)/Charge AS ExactMZ, dm.DistinctMatchkey, dm.DistinctMatchId   "
                //"IFNULL(SUBSTR(Sequence, pi.Offset+1, pi.Length),DecoySequence) || ' ' || Charge || ' ' ||  IFNULL(Mods, '') as Distinct_Match   "
                "FROM Spectrum s  "
                "JOIN PeptideSpectrumMatch psm ON s.Id=psm.Spectrum  "
                "JOIN DistinctMatch dm ON psm.id=dm.psmid  "
                "JOIN Peptide pep ON psm.Peptide=pep.Id  "
                "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide  "
                "LEFT JOIN ProteinData pro ON pi.Protein=pro.Id  "
                "LEFT JOIN (SELECT pm.PeptideSpectrumMatch AS PsmId,  "
                "GROUP_CONCAT(MonoMassDelta || '@' || pm.Offset) AS Mods,  "
                "SUM("+deltaMassColumn+") AS TotalModMass  "
                "FROM PeptideModification pm  "
                "JOIN Modification mod ON pm.Modification=mod.Id  "
                "GROUP BY PsmId) mods2 ON psm.Id=mods2.PsmId  "
                "WHERE QValue <= ? AND Source=? AND Rank=1  "
                //"WHERE Source=? AND Rank=1 AND psmScore.ScoreNameId = "+selectedScore+"  "
                "GROUP BY psm.Id  "
                "ORDER BY DistinctMatchkey, Charge, Mods ";



    sqlite::query q(db, sql.c_str());
    //q.binder() << config.MaxQValue << sourceId;
    q.binder() << config.MaxQValue << sourceId;

    XICWindowList windows;
    XICWindow tmpWindow;
    XICPeptideSpectrumMatch tmpPSM;
    map<double, int> psmMap;
    int lastCharge = 0;
    string lastModif = "initial value";
    SortByScanTime sortByScanTime;

    string currentMatch = "";
    BOOST_FOREACH(sqlite::query::rows row, q)
    {
        sqlite_int64 psmId;
        sqlite_int64 peptideId;
        char const* id;
        double precursorMZ;
        int charge;
        string modif;
        double score, exactMZ;
        string sourceId;
        string distinctMatch;
        string distinctMatchId;

        row.getter() >> psmId >> peptideId >> sourceId >> id >> precursorMZ >> charge >> modif >> score >> exactMZ >> distinctMatch >> distinctMatchId;

        MS2ScanMap::index<nativeID>::type::const_iterator itr = ms2ScanMap.get<nativeID>().find(id);
        if (itr == ms2ScanMap.get<nativeID>().end())
            throw runtime_error(string("PSM identified to spectrum \"") + id + "\" is not in the scan map");

        if (itr->msLevel > 2)
            continue;

        ms2ScanMap.get<nativeID>().modify(itr, ModifyPrecursorMZ(precursorMZ)); // prefer corrected monoisotopic m/z
        const MS2ScanInfo& scanInfo = *itr;

        if (distinctMatch != currentMatch)
        { // if any of these values change we make a new chromatogram
        
            if (!tmpWindow.PSMs.empty())
            {
                sort(tmpWindow.PSMs.begin(), tmpWindow.PSMs.end(), sortByScanTime);
                tmpWindow.meanMS2RT /= tmpWindow.PSMs.size();
                windows.insert(tmpWindow);
            }
                
            currentMatch = distinctMatch;//distinctMatchId;
            lastCharge = tmpPSM.charge = charge;
            lastModif = modif;
            tmpPSM.exactMZ = exactMZ;
            tmpWindow.source = sourceId;
            tmpWindow.distinctMatch=distinctMatch;
            tmpWindow.distinctMatchId=distinctMatchId;
            tmpWindow.peptideId=lexical_cast<string>(peptideId);
            tmpWindow.firstMS2RT = tmpWindow.lastMS2RT = tmpWindow.meanMS2RT = scanInfo.scanStartTime;
            tmpWindow.bestScore = score;
            tmpWindow.bestScoreScanStartTime = scanInfo.scanStartTime;
            tmpWindow.PSMs.clear();
			psmMap.clear();
            tmpWindow.preRT.clear();
            tmpWindow.preMZ.clear();

            if (useAvgMass || config.MonoisotopicAdjustmentSet.empty())
            {
                double centerMz = tmpPSM.exactMZ / tmpPSM.charge;
                double mzLower = centerMz - MZTolerance(config.ChromatogramMzLowerOffset.value * tmpPSM.charge, config.ChromatogramMzLowerOffset.units);
                double mzUpper = centerMz + MZTolerance(config.ChromatogramMzUpperOffset.value * tmpPSM.charge, config.ChromatogramMzUpperOffset.units);
                tmpWindow.preMZ += boost::icl::interval_set<double>(continuous_interval<double>::closed(mzLower, mzUpper));
            }
            else
            {
                IntegerSet::const_iterator itr = config.MonoisotopicAdjustmentSet.begin();
                for (; itr != config.MonoisotopicAdjustmentSet.end(); ++itr)
                {
                    if (tmpPSM.charge == 0) throw runtime_error("[chromatogramMzWindow] charge cannot be 0");
                    double centerMz = tmpPSM.exactMZ + *itr * Neutron / tmpPSM.charge;
                    double mzLower = centerMz - MZTolerance(config.ChromatogramMzLowerOffset.value * tmpPSM.charge, config.ChromatogramMzLowerOffset.units);
                    double mzUpper = centerMz + MZTolerance(config.ChromatogramMzUpperOffset.value * tmpPSM.charge, config.ChromatogramMzUpperOffset.units);
                    tmpWindow.preMZ += boost::icl::interval_set<double>(continuous_interval<double>::closed(mzLower, mzUpper));
                }
            }


            if (!scanInfo.identified)
                throw runtime_error("PSM " + scanInfo.nativeID + " is not identified (should never happen)");

            if (!boost::icl::contains(tmpWindow.preMZ, scanInfo.precursorMZ))
            {
                //fileOut << "Warning: PSM for spectrum \"" << scanInfo.nativeID << "\" with observed m/z " << scanInfo.precursorMZ << " is disjoint with the exact m/z " << tmpPSM.exactMZ << endl;
                continue;
            }
        }

        tmpPSM.id = psmId;
        tmpPSM.peptide = peptideId;
        tmpPSM.spectrum = &scanInfo;
        tmpPSM.score = score;
        
        //merge PSMs with identical scan times
        if (psmMap.find(scanInfo.scanStartTime) != psmMap.end())
		{
			if (tmpPSM.score < tmpWindow.PSMs[psmMap[scanInfo.scanStartTime]].score)
				tmpWindow.PSMs[psmMap[scanInfo.scanStartTime]] = tmpPSM;
		}
        else
        {
            tmpWindow.PSMs.push_back(tmpPSM);
            psmMap[scanInfo.scanStartTime] = tmpWindow.PSMs.size()-1;
        }

        tmpWindow.firstMS2RT = min(tmpWindow.firstMS2RT, scanInfo.scanStartTime);
        tmpWindow.lastMS2RT = max(tmpWindow.lastMS2RT, scanInfo.scanStartTime);
        tmpWindow.meanMS2RT += scanInfo.scanStartTime;
        if (tmpPSM.score < tmpWindow.bestScore)
        {
            tmpWindow.bestScore = tmpPSM.score;
            tmpWindow.bestScoreScanStartTime = scanInfo.scanStartTime;
        }
        tmpWindow.preRT += boost::icl::interval_set<double>(continuous_interval<double>::closed(scanInfo.precursorScanStartTime-config.RetentionTimeLowerTolerance, scanInfo.precursorScanStartTime+config.RetentionTimeUpperTolerance));
    }

    if (!tmpWindow.PSMs.empty())
    {
        sort(tmpWindow.PSMs.begin(), tmpWindow.PSMs.end(), sortByScanTime);
        tmpWindow.meanMS2RT /= tmpWindow.PSMs.size();
            windows.insert(tmpWindow); // add the last peptide to the vector
    }
    return windows;
}
//
void simulateGaussianPeak(double peakStart, double peakEnd,
                              double peakHeight, double peakBaseline,
                              double mean, double stddev,
                              size_t samples,
                              vector<double>& x, vector<double>& y)
{
    using namespace boost::math;
    normal_distribution<double> peakDistribution(mean, stddev);
    x.push_back(peakStart); y.push_back(peakBaseline);
    peakStart += numeric_limits<double>::epsilon();
    double sampleRate = (peakEnd - peakStart) / samples;
    double scale = peakHeight / pdf(peakDistribution, mean);
    for (size_t i=0; i <= samples; ++i)
    {
        x.push_back(peakStart + sampleRate*i);
        y.push_back(peakBaseline + scale * pdf(peakDistribution, x.back()));
    }
    x.push_back(peakEnd); y.push_back(peakBaseline);
}

int writeChromatograms(const string& idpDBFilename,
                            const XICWindowList& pepWindow,
                            const vector<RegDefinedPrecursorInfo>& RegDefinedPrecursors,
                            pwiz::util::IterationListenerRegistry* ilr,
                            const int& currentFile, const int& totalFiles)
{
    int totalAdded = 0;
    try
    {
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "Writing chromatograms to idpDB");
        shared_ptr<ChromatogramListSimple> chromatogramListSimple(new ChromatogramListSimple);

        sqlite::database idpDB(idpDBFilename);
        idpDB.execute("PRAGMA journal_mode=OFF;"
                      "PRAGMA synchronous=OFF;"
                      "PRAGMA cache_size=50000;"
                      IDPICKER_SQLITE_PRAGMA_MMAP);

        sqlite::transaction transaction(idpDB);
        //initialize the table
        idpDB.execute("CREATE TABLE IF NOT EXISTS XICMetrics (Id INTEGER PRIMARY KEY, DistinctMatch INTEGER, SpectrumSource INTEGER, Peptide INTEGER, PeakIntensity NUMERIC, PeakArea NUMERIC, PeakSNR NUMERIC, PeakTimeInSeconds NUMERIC);");
        string sql = "INSERT INTO XICMetrics (DistinctMatch, SpectrumSource, Peptide, PeakIntensity, PeakArea, PeakSNR, PeakTimeInSeconds) values (?,?,?,?,?,?,?)";
        sqlite::command insertPeptideIntensity(idpDB, sql.c_str());
        map<string,vector<double>> searchResults; 
        map<string,string> matchIdToPeptide; 
        string source = "";
        
        BOOST_FOREACH(const RegDefinedPrecursorInfo& info, RegDefinedPrecursors)
        {
            if (info.chromatogram.MS1RT.empty())
                continue;

            //cout<<"write reg defined precursors into chromatograms "<<endl;
            chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
            Chromatogram& c = *chromatogramListSimple->chromatograms.back();
            c.index = chromatogramListSimple->size()-1;


            c.id = info.chromatogram.id;
            c.setTimeIntensityArrays(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity, UO_second, MS_number_of_detector_counts);
            //for setting spline for regression rt
            double highestPeakIntensity=0;
            double medianIntensity=0;
            //output all crawdad peaks
            {
                chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& c2 = *chromatogramListSimple->chromatograms.back();
                c2.index = chromatogramListSimple->size()-1;
                c2.id = "Regression: Crawdad peaks for "+ info.chromatogram.id;
                c2.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);

                BOOST_FOREACH(const Peak& peak, info.chromatogram.peaks)
                {
                    simulateGaussianPeak(peak.startTime, peak.endTime,
                                         peak.intensity, info.baselineIntensity,
                                         peak.peakTime, peak.fwhm / 2.35482,
                                         50,
                                         c2.getTimeArray()->data, c2.getIntensityArray()->data);
                 }
            }

            //output only the best peak
            if(info.chromatogram.bestPeak)
            {
                 chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& c2 = *chromatogramListSimple->chromatograms.back();
                c2.index = chromatogramListSimple->size()-1;
                c2.id = "Regression:Best Crawdad peaks for "+ info.chromatogram.id;
                 c2.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);
                 const Peak& peak = *info.chromatogram.bestPeak;

                 //criteria for peak picking. the regression time should be within the peak area : startTime1, endTime1!
                 //4 standard deviation
                 double wideStartTime1 = peak.peakTime - peak.fwhm * 4 / 2.35482; //'4' is arbitrary, based on defaults. May add to options later
                 double wideEndTime1 = peak.peakTime + peak.fwhm  * 4  / 2.35482;
                 if(wideStartTime1<info.RegTime &&  info.RegTime<wideEndTime1)
                 {
                     simulateGaussianPeak(peak.startTime, peak.endTime,
                     peak.intensity, info.baselineIntensity,
                     peak.peakTime, peak.fwhm / 2.35482,
                     50,
                     c2.getTimeArray()->data, c2.getIntensityArray()->data);

                     const vector<double>& myIntensityVec=info.chromatogram.MS1Intensity;

                     const vector<double>& times=info.chromatogram.MS1RT; 
                     accs::accumulator_set<double, accs::stats<accs::tag::variance, accs::tag::mean> > PeakIntensities; 
                     accs::accumulator_set<double, accs::stats<accs::tag::variance, accs::tag::mean> > BackgroundIntensities; 
                     for (size_t  i = 0; i < myIntensityVec.size(); i++){
                         if(times[i]>=peak.startTime&&times[i]<=peak.endTime){
                             PeakIntensities(myIntensityVec[i]); 
                         }
                         else{
                             BackgroundIntensities(myIntensityVec[i]); 
                         }
                     }
                     double peak_mean=accs::mean(PeakIntensities);
                     double bcd_mean=accs::mean(BackgroundIntensities);
                     double peak_var=accs::variance(PeakIntensities); 
                     double bcd_var=accs::variance(BackgroundIntensities); 
                     double SNRatio=(peak_mean-bcd_mean)/ sqrt(peak_var+bcd_var);
                     double peakArea=peak.intensity*peak.fwhm;
                     
                    vector<double> newValue;
                    newValue.push_back(peak.intensity);
                    newValue.push_back(peakArea);
                    newValue.push_back(SNRatio);
                    newValue.push_back(peak.peakTime);
                    searchResults[lexical_cast<string>(info.matchId)] = newValue;
                    matchIdToPeptide[lexical_cast<string>(info.matchId)] = lexical_cast<string>(info.peptideId);
                }
            }
        }
        
        BOOST_FOREACH(const XICWindow& window, pepWindow)
        {
            if (window.MS1RT.empty())
                continue;
            chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
            Chromatogram& c = *chromatogramListSimple->chromatograms.back();
            c.index = chromatogramListSimple->size()-1;
            ostringstream oss;
            oss << " (id: " << window.PSMs[0].peptide
                << "; m/z: " << window.preMZ
                << "; time: " << window.preRT << ")";
            c.id = "Raw SIC for " + oss.str();
            c.set(MS_SIC_chromatogram);
            c.setTimeIntensityArrays(window.MS1RT, window.MS1Intensity, UO_second, MS_number_of_detector_counts);



            CrawdadPeakFinder crawdadPeakFinder;
            crawdadPeakFinder.SetChromatogram(window.MS1RT, window.MS1Intensity);

            chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
            Chromatogram& c3 = *chromatogramListSimple->chromatograms.back();
            c3.index = chromatogramListSimple->size()-1;
            c3.id = "Smoothed SIC for " + oss.str();
            c3.set(MS_SIC_chromatogram);
            double sampleRate = window.MS1RT[1] - window.MS1RT[0];
            size_t wingSize = crawdadPeakFinder.getWingData().size();
            vector<double> newRT(wingSize, 0);
            for(size_t i=0; i < wingSize; ++i) newRT[i] = window.MS1RT[0] - (wingSize-i)*sampleRate;
            newRT.insert(newRT.end(), window.MS1RT.begin(), window.MS1RT.end());
            for(size_t i=1; i <= wingSize; ++i) newRT.push_back(window.MS1RT.back() + i*sampleRate);
            const vector<float>& tmp = crawdadPeakFinder.getSmoothedIntensities();
            if (tmp.size() == newRT.size())
                c3.setTimeIntensityArrays(newRT, vector<double>(tmp.begin(), tmp.end()), UO_second, MS_number_of_detector_counts);

            float baselineIntensity = crawdadPeakFinder.getBaselineIntensity();

            // output all Crawdad peaks
            {
                chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& c2 = *chromatogramListSimple->chromatograms.back();
                c2.index = chromatogramListSimple->size()-1;
                c2.id = "Crawdad peaks for " + oss.str();
                c2.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);

                BOOST_FOREACH(const Peak& peak, window.peaks)
                {
                    simulateGaussianPeak(peak.startTime, peak.endTime,
                                         peak.intensity, baselineIntensity,
                                         peak.peakTime, peak.fwhm / 2.35482,
                                         50,
                                         c2.getTimeArray()->data, c2.getIntensityArray()->data);
                }
            }

            // output only the best peak
            if (window.bestPeak)
            {
                chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& c2 = *chromatogramListSimple->chromatograms.back();
                c2.index = chromatogramListSimple->size()-1;
                c2.id = "Best Crawdad peak for " + oss.str();
                c2.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);

                const Peak& peak = *window.bestPeak;
                simulateGaussianPeak(peak.startTime, peak.endTime,
                                     peak.intensity, baselineIntensity,
                                     peak.peakTime, peak.fwhm / 2.35482,
                                     50,
                                     c2.getTimeArray()->data, c2.getIntensityArray()->data);
                //cout<<oss.str()<<" intensity: "<< peak.intensity<<endl;


                const vector<double>& myIntensityVec=window.MS1Intensity;
                /*sort(myIntensityVec.begin(),myIntensityVec.end());
                vector<double>::iterator it;
                it=myIntensityVec.begin();
                double peakPrecursorIntensityMedian=*(it+=(myIntensityVec.size()/2));


                double SNRatio=(peak.intensity/peakPrecursorIntensityMedian) ;*/
                const vector<double>& times=window.MS1RT;
                accs::accumulator_set<double, accs::stats<accs::tag::variance, accs::tag::mean> > PeakIntensities;
                accs::accumulator_set<double, accs::stats<accs::tag::variance, accs::tag::mean> > BackgroundIntensities;
                  for (size_t  i = 0; i < myIntensityVec.size(); i++){
                      if(times[i]>=peak.startTime&&times[i]<=peak.endTime){
                         PeakIntensities(myIntensityVec[i]);
                      }
                      else{
                          BackgroundIntensities(myIntensityVec[i]);
                      }
                  }
                double peak_mean=accs::mean(PeakIntensities);
                double bcd_mean=accs::mean(BackgroundIntensities);
                double peak_var=accs::variance(PeakIntensities);
                double bcd_var=accs::variance(BackgroundIntensities);
                double SNRatio=(peak_mean-bcd_mean)/ sqrt(peak_var+bcd_var);

                double peakArea=peak.intensity*peak.fwhm;

                if (window.PSMs.size() > 0)
                {
                    // insertPeptideIntensity.binder() <<  lexical_cast<string>(window.distinctMatchId) << lexical_cast<string>(window.source)
                    // << lexical_cast<string>(peak.intensity) << lexical_cast<string>(peakArea) << lexical_cast<string>(SNRatio) << lexical_cast<string>(peak.peakTime);
                    // insertPeptideIntensity.execute();
                    // insertPeptideIntensity.reset();
                    totalAdded++;
                    
                    //insert binder edit starts here
                    source = lexical_cast<string>(window.source);
                    if (searchResults.count(lexical_cast<string>(window.distinctMatchId)) > 0)
                    {
                        vector<double> newValue;
                        newValue.push_back(searchResults[lexical_cast<string>(window.distinctMatchId)][0] + peak.intensity);
                        newValue.push_back(searchResults[lexical_cast<string>(window.distinctMatchId)][1] + peakArea);
                        newValue.push_back((SNRatio + searchResults[lexical_cast<string>(window.distinctMatchId)][2]) / 2);
                        newValue.push_back((peak.peakTime + searchResults[lexical_cast<string>(window.distinctMatchId)][3]) / 2);
                        searchResults[lexical_cast<string>(window.distinctMatchId)] = newValue;
                        matchIdToPeptide[lexical_cast<string>(window.distinctMatchId)] = lexical_cast<string>(window.peptideId);
                        // searchResults[lexical_cast<string>(window.distinctMatchId)][0] = //peak.intensity;
                        // searchResults[lexical_cast<string>(window.distinctMatchId)][1] += peakArea;
                        // searchResults[lexical_cast<string>(window.distinctMatchId)][2] = (SNRatio + searchResults[lexical_cast<string>(window.distinctMatchId)][2]) / 2;
                        // searchResults[lexical_cast<string>(window.distinctMatchId)][3] = (peak.peakTime + searchResults[lexical_cast<string>(window.distinctMatchId)][3]) / 2;
                    }
                    else
                    {
                        vector<double> newValue;
                        newValue.push_back(peak.intensity);
                        newValue.push_back(peakArea);
                        newValue.push_back(SNRatio);
                        newValue.push_back(peak.peakTime);
                        searchResults[lexical_cast<string>(window.distinctMatchId)] = newValue;
                        matchIdToPeptide[lexical_cast<string>(window.distinctMatchId)] = lexical_cast<string>(window.peptideId);
                    }
                }
                //cout<<c2.getTimeArray()->data<<endl;
            }
        }
        
        for(map<string,vector<double>>::iterator matchItr = searchResults.begin(); matchItr != searchResults.end(); matchItr++)
        {
            string match = matchItr->first;
            vector<double> matchResults = matchItr->second;
            
            insertPeptideIntensity.binder() <<  match << source << matchIdToPeptide[match] 
                    << lexical_cast<string>(matchResults[0]) << lexical_cast<string>(matchResults[1]) 
                    << lexical_cast<string>(matchResults[2]) << lexical_cast<string>(matchResults[3]) ;
                    insertPeptideIntensity.execute();
                    insertPeptideIntensity.reset();
        }
        
        transaction.commit();
    }
    catch (exception& e)
    {
        throw runtime_error(string("[writeChromatograms] ") + e.what());
    }
    return totalAdded;
}

int EmbedMS1ForFile(sqlite::database& idpDb, const string& idpDBFilePath, const string& sourceFilePath, const string& sourceId, XICConfiguration& config, pwiz::util::IterationListenerRegistry* ilr, const int& currentFile, const int& totalFiles)
{
    try
    {

        string sourceFilename = bfs::path(sourceFilePath).filename().string();
        // Initialize the idpicker reader. It supports idpDB's for now.
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "Reading identifications from " + sourceFilename);

        // Obtain the list of readers available
        ExtendedReaderList readers;
        MSDataFile msd(sourceFilePath, &readers);

        //TODO: add ability to apply spectrum list filters
        /*vector<string> wrappers;
        SpectrumListFactory::wrap(msd, wrappers);*/

        SpectrumList& spectrumList = *msd.run.spectrumListPtr;

        //ITERATION_UPDATE(ilr, currentFile, totalFiles, "Started processing file " + sourceFilename);

        // Spectral counts
        int MS1Count = 0, MS2Count = 0;

        MS1ScanMap ms1ScanMap;
        MS2ScanMap ms2ScanMap;

        map<string, string> tempMap;
        {
            string sql = "SELECT s.NativeID, dm.DistinctMatchkey   "//IFNULL(SUBSTR(Sequence, pi.Offset+1, pi.Length),DecoySequence) || ' ' || Charge || ' ' ||  IFNULL(Mods, '') as Distinct_Match   "
                             "FROM PeptideSpectrumMatch psm "
                             "JOIN DistinctMatch dm ON psm.id=dm.psmid  "
                             "JOIN Spectrum s ON psm.Spectrum=s.Id "
                             "JOIN Peptide pep ON psm.Peptide=pep.Id  "
                             "JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide  "
                             "LEFT JOIN ProteinData pro ON pi.Protein=pro.Id  "
                             "LEFT JOIN (SELECT pm.PeptideSpectrumMatch AS PsmId,  "
                             "GROUP_CONCAT(MonoMassDelta || '@' || pm.Offset) AS Mods,  "
                             "SUM(MonoMassDelta) AS TotalModMass  "
                             "FROM PeptideModification pm  "
                             "JOIN Modification mod ON pm.Modification=mod.Id  "
                             "GROUP BY PsmId) mods2 ON psm.Id=mods2.PsmId  "
                             "WHERE Qvalue<=0.05 AND Rank=1 AND Source=? "
                             "GROUP BY s.Id ";

            sqlite::query distinctModifiedPeptideByNativeIDQuery(idpDb, sql.c_str());

            distinctModifiedPeptideByNativeIDQuery.binder() << sourceId;

            char const* nativeId;
            string distinctMatch;

            int temp = 0;
            BOOST_FOREACH(sqlite::query::rows row, distinctModifiedPeptideByNativeIDQuery)
            {
                temp++;
                row.getter() >> nativeId >> distinctMatch;
                tempMap[nativeId] = distinctMatch;
            }
        }
        const map<string, string>& distinctModifiedPeptideByNativeID = tempMap;
        map<string, double> firstScanTimeOfDistinctModifiedPeptide;

        string lastMS1NativeId;
        size_t missingPrecursorIntensities = 0;
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "Found " + lexical_cast<string>(spectrumList.size())+ " spectra, reading metadata...");

        // For each spectrum
        for( size_t curIndex = 0; curIndex < spectrumList.size(); ++curIndex )
        {
            SpectrumPtr spectrum = spectrumList.spectrum(curIndex, false);

            if (spectrum->defaultArrayLength == 0)
                continue;

            if (spectrum->cvParam(MS_MSn_spectrum).empty() && spectrum->cvParam(MS_MS1_spectrum).empty())
                continue;

            CVParam spectrumMSLevel = spectrum->cvParam(MS_ms_level);
            if (spectrumMSLevel == CVID_Unknown)
                continue;

            // Check its MS level and increment the count
            int msLevel = spectrumMSLevel.valueAs<int>();
            if (msLevel == 1)
            {
                MS1ScanInfo scanInfo;
                lastMS1NativeId = scanInfo.nativeID = spectrum->id;
                scanInfo.totalIonCurrent = spectrum->cvParam(MS_total_ion_current).valueAs<double>();

                if (spectrum->scanList.scans.empty())
                    throw runtime_error("No scan start time for " + spectrum->id);

                Scan& scan = spectrum->scanList.scans[0];
                CVParam scanTime = scan.cvParam(MS_scan_start_time);
                if (scanTime.empty())
                    throw runtime_error("No scan start time for " + spectrum->id);

                scanInfo.scanStartTime = scanTime.timeInSeconds();

                ms1ScanMap.push_back(scanInfo);
                ++MS1Count;
            }
            else if (msLevel == 2)
            {
                MS2ScanInfo scanInfo;
                scanInfo.nativeID = spectrum->id;
                scanInfo.msLevel = 2;

                if (spectrum->precursors.empty() || spectrum->precursors[0].selectedIons.empty())
                    throw runtime_error("No selected ion found for MS2 " + spectrum->id);

                Precursor& precursor = spectrum->precursors[0];
                const SelectedIon& si = precursor.selectedIons[0];

                scanInfo.precursorIntensity = si.cvParam(MS_peak_intensity).valueAs<double>();
                if (scanInfo.precursorIntensity == 0)
                {
                    //throw runtime_error("No precursor intensity for MS2 " + spectrum->id);
                    //cerr << "\nNo precursor intensity for MS2 " + spectrum->id << endl;
                    ++missingPrecursorIntensities;

                    // fall back on MS2 TIC
                    scanInfo.precursorIntensity = spectrum->cvParam(MS_total_ion_current).valueAs<double>();
                }

                if (precursor.spectrumID.empty())
                {
                    if (lastMS1NativeId.empty())
                        throw runtime_error("No MS1 spectrum found before " + spectrum->id);
                    scanInfo.precursorNativeID = lastMS1NativeId;
                }
                else
                    scanInfo.precursorNativeID = precursor.spectrumID;

                if (spectrum->scanList.scans.empty())
                    throw runtime_error("No scan start time for " + spectrum->id);

                Scan& scan = spectrum->scanList.scans[0];
                CVParam scanTime = scan.cvParam(MS_scan_start_time);
                if (scanTime.empty())
                    throw runtime_error("No scan start time for " + spectrum->id);
                scanInfo.scanStartTime = scanTime.timeInSeconds();

                ++MS2Count;

                scanInfo.precursorMZ = si.cvParam(MS_selected_ion_m_z).valueAs<double>();
                if (si.cvParam(MS_selected_ion_m_z).empty() )
                    scanInfo.precursorMZ = si.cvParam(MS_m_z).valueAs<double>();
                if (scanInfo.precursorMZ == 0)
                    throw runtime_error("No precursor m/z for " + spectrum->id);

                scanInfo.precursorScanStartTime =
                    ms1ScanMap.get<nativeID>().find(scanInfo.precursorNativeID)->scanStartTime;

                // Only look at retention times of peptides identified in .idpDB
                // curIndex is the spectrum index, curIndex+1 is (usually) the scan number
                map<string, string>::const_iterator findItr = distinctModifiedPeptideByNativeID.find(spectrum->id);
                if (findItr != distinctModifiedPeptideByNativeID.end())
                {
                    scanInfo.identified = true;
                    scanInfo.distinctModifiedPeptide = findItr->second;
                    map<string, double>::iterator insertItr = firstScanTimeOfDistinctModifiedPeptide.insert(make_pair(findItr->second, scanInfo.scanStartTime)).first;
                    insertItr->second = min(insertItr->second, scanInfo.scanStartTime);
                }
                else
                { // this MS2 scan was not identified; we need this data for metrics MS2-4A/B/C/D
                    scanInfo.identified = false;
                    scanInfo.distinctModifiedPeptide = "";
                }
                ms2ScanMap.push_back(scanInfo);
            }

        } // finished cycling through all spectra

//        if (g_numWorkers == 1)
//            cout << endl;

        /*if (missingPrecursorIntensities)
            cerr << "Warning: " << missingPrecursorIntensities << " spectra are missing precursor trigger intensity; MS2 TIC will be used as a substitute." << endl;*/

        if (MS1Count == 0)
            throw runtime_error("no MS1 spectra found in \"" + sourceFilename + "\". Is the file incorrectly filtered?");

        if (MS2Count == 0)
            throw runtime_error("no MS2 spectra found in \"" + sourceFilename + "\". Is the file incorrectly filtered?");

        /*if (MS3OrGreaterCount)
            cerr << "Warning: " << MS3OrGreaterCount << " spectra have MS levels higher than 2 but will be ignored." << endl;*/

        XICWindowList pepWindow = GetMZRTWindows(idpDb,ms2ScanMap,sourceId, config);
        vector<RegDefinedPrecursorInfo> RegDefinedPrecursors;


//TODO: Give option for # if align the chromatogram peaks based on peptide retention time
        if(config.AlignRetentionTime)
        {
            string outputFilepath = config.RTFolder;
            string rawFileName =  bfs::path(sourceFilename).filename().string();
            string editedFileName= rawFileName.substr(0,rawFileName.find(".")) + "-peptideScantimeRegression.tsv";
            string regressionFilename = (bfs::path(outputFilepath) / bfs::path(editedFileName)).string();
            //string regressionFilename= rawFileName.substr(0,rawFileName.find(".")) + "-peptideScantimeRegression.tsv";
            //string regressionFilename=bfs::change_extension(bfs::path(outputFilepath) / bfs::path(sourceFilename).filename(), "-peptideScantimeRegression.tsv").string();
            ifstream file;

            try
            {
                file.open(regressionFilename.c_str()); // how to use a "string" variable
                if(!file)
                    throw(regressionFilename);//If the file is not found, this calls the "catch"
            }
            catch(string regressionFilename)//This catches the infile and aborts process
            {
                throw runtime_error("[EmbedMS1ForFile]: Alignment file not found. " + regressionFilename);
            }

            if (file.is_open())
            {
                RegDefinedPrecursorInfo info;

                std::string   line;
                getline(file, line);
                bool useAvgMass = config.ChromatogramMzUpperOffset.units != MZTolerance::PPM && config.ChromatogramMzLowerOffset.units != MZTolerance::PPM &&
                      config.ChromatogramMzUpperOffset.value + config.ChromatogramMzLowerOffset.value > 1;
                while(getline(file, line))
                {
                    istringstream   ss(line);
                    double     scantime1,precursorMZ;
                    int  matchId,charge,peptideId;
                    ss >> matchId>>scantime1>>charge>>precursorMZ>>peptideId;
                    info.matchId=lexical_cast<string>(matchId);
                    info.peptideId=lexical_cast<string>(peptideId);
                    info.exactMZ=precursorMZ;
                    info.charge=charge;
                    info.RegTime=scantime1;
        
                    // if (useAvgMass || config.MonoisotopicAdjustmentSet.empty())
                    // {
                        double centerMz = precursorMZ / charge;
                        double mzLower = centerMz - MZTolerance(config.ChromatogramMzLowerOffset.value * charge, config.ChromatogramMzLowerOffset.units);
                        double mzUpper = centerMz + MZTolerance(config.ChromatogramMzUpperOffset.value * charge, config.ChromatogramMzUpperOffset.units);
                        info.mzWindow = boost::icl::interval_set<double>(continuous_interval<double>::closed(mzLower, mzUpper));
                    // }
                    // else
                    // {
                        // IntegerSet::const_iterator itr = config.MonoisotopicAdjustmentSet.begin();
                        // for (; itr != config.MonoisotopicAdjustmentSet.end(); ++itr)
                        // {
                            // if (charge == 0) throw runtime_error("[RegDefinedMzWindow] charge cannot be 0");
                            // double centerMz = precursorMZ + *itr * Neutron / charge;
                            // double mzLower = centerMz - MZTolerance(config.ChromatogramMzLowerOffset.value * charge, config.ChromatogramMzLowerOffset.units);
                            // double mzUpper = centerMz + MZTolerance(config.ChromatogramMzUpperOffset.value * charge, config.ChromatogramMzUpperOffset.units);
                            // info.mzWindow += boost::icl::interval_set<double>(continuous_interval<double>::closed(mzLower, mzUpper));
                        // }
                    // }
                    
                    info.scanTimeWindow= boost::icl::interval_set<double>(continuous_interval<double>::closed(scantime1-config.RetentionTimeLowerTolerance, scantime1+config.RetentionTimeUpperTolerance));
                    info.chromatogram.id = "regression defined precursor " +info.matchId+" "+lexical_cast<string>(info.charge)+" (id: m/z: "+lexical_cast<string>(round(info.exactMZ))+"; time: "+lexical_cast<string>(info.RegTime)+") ";
                    RegDefinedPrecursors.push_back(info);
                }
                file.close();
            }
            cout<<"number of regdefined precursors are "<<RegDefinedPrecursors.size()<<endl;
        }
        // Going through all spectra once more to get intensities/retention times to build chromatograms
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "\rReading " + lexical_cast<string>(spectrumList.size()) + " peaks... ");
        map<double, double > peakMap;
        
        for( size_t curIndex = 0; curIndex < spectrumList.size(); ++curIndex )
        {

            SpectrumPtr spectrum = spectrumList.spectrum(curIndex, true);

            if (spectrum->cvParam(MS_MSn_spectrum).empty() && spectrum->cvParam(MS_MS1_spectrum).empty() )
                continue;

            CVParam spectrumMSLevel = spectrum->cvParam(MS_ms_level);
            if (spectrumMSLevel == CVID_Unknown)
                continue;
            // this time around we're only looking for MS1 spectra
            int msLevel = spectrumMSLevel.valueAs<int>();
            if (msLevel == 1)
            {
                Scan& scan = spectrum->scanList.scans[0];

                // all m/z and intensity data for a spectrum
                const vector<double>& mzV = spectrum->getMZArray()->data;
                const vector<double>& intensV = spectrum->getIntensityArray()->data;
                size_t arraySize = mzV.size();
                double curRT = scan.cvParam(MS_scan_start_time).timeInSeconds();

                accs::accumulator_set<double, accs::stats<accs::tag::min, accs::tag::max> > mzMinMax;
                mzMinMax = std::for_each(mzV.begin(), mzV.end(), mzMinMax);
                boost::icl::interval_set<double> spectrumMzRange(continuous_interval<double>::closed(accs::min(mzMinMax), accs::max(mzMinMax)));

                BOOST_FOREACH(const XICWindow& window, pepWindow)
                {
                    // if the MS1 retention time is not in the RT window constructed for this peptide, skip this window
                    if (!boost::icl::contains(hull(window.preRT), curRT))
                        continue;

                    // if the m/z window and the MS1 spectrum's m/z range do not overlap, skip this window
                    if (disjoint(window.preMZ, spectrumMzRange))
                        continue;

                    double sumIntensities = 0;
                    for (size_t iMZ = 0; iMZ < arraySize; ++iMZ)
                    {
                        // if this m/z is in the window, record its intensity and retention time
                        if (boost::icl::contains(window.preMZ, mzV[iMZ]))
                            sumIntensities += intensV[iMZ];
                    }
                    window.AddMS1(sumIntensities, curRT);

                } // done searching through all unique peptide windows for this MS1 scan

                //loop through all regression defined precursors

                      BOOST_FOREACH(RegDefinedPrecursorInfo& info, RegDefinedPrecursors)
                {

                    if (!boost::icl::contains(info.scanTimeWindow, curRT))
                        continue;

                    // if the PSM's m/z window and the MS1 spectrum's m/z range do not overlap, skip this window
                    if (disjoint(info.mzWindow, spectrumMzRange))
                        continue;

                    double sumIntensities = 0;
                    for (size_t iMZ = 0; iMZ < arraySize; ++iMZ)
                    {
                        // if this m/z is in the window, record its intensity and retention time
                        if (boost::icl::contains(info.mzWindow, mzV[iMZ]))
                            sumIntensities += intensV[iMZ];
                    }

                    info.chromatogram.AddMS1(sumIntensities,curRT);
                } // done with regression defined precursor scan

            }
            else if (msLevel == 2)
            {
                // calculate TIC manually if necessary
                MS2ScanInfo& scanInfo = const_cast<MS2ScanInfo&>(*ms2ScanMap.get<nativeID>().find(spectrum->id));
                if (scanInfo.precursorIntensity == 0)
                {
                    BOOST_FOREACH(const double& p, spectrum->getIntensityArray()->data)
                        scanInfo.precursorIntensity += p;
                }
            }
        } // end of spectra loop
        
        //finalize ms1 vectors
        BOOST_FOREACH(const XICWindow& window, pepWindow)
            window.FinalizeMS1();
        BOOST_FOREACH(RegDefinedPrecursorInfo& info, RegDefinedPrecursors)
            info.chromatogram.FinalizeMS1();

        // Find peaks with Crawdad
        // cycle through all distinct matches, passing each one to crawdad
        size_t i = 0;
        size_t windowBestPeaks = 0;
        size_t regBestPeaks = 0;
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "\rFinding distinct match peaks...");
        BOOST_FOREACH(const XICWindow& window, pepWindow)
        {
            ++i;
            if (window.MS1RT.empty())
            {
                /*cerr << "Warning: distinct match window for " << window.peptide
                     << " (id: " << window.PSMs[0].peptide
                     << "; m/z: " << window.preMZ
                     << "; time: " << window.preRT
                     << ") has no chromatogram data points!" << endl;*/
                continue;
            }

                if(window.MS1RT.size()<4){
            //cerr<<"Warning: the MS1 RT size is less than 4 :"<<window.peptide<<endl;
            continue;
            }

            // make chromatogram data points evenly spaced
            Interpolator interpolator(window.MS1RT, window.MS1Intensity);
            interpolator.resample(window.MS1RT, window.MS1Intensity);

            // eliminate negative signal
            BOOST_FOREACH(double& intensity, window.MS1Intensity)
                intensity = max(0.0, intensity);
            CrawdadPeakFinder crawdadPeakFinder;
            crawdadPeakFinder.SetChromatogram(window.MS1RT, window.MS1Intensity);

            vector<CrawdadPeakPtr> crawPeaks = crawdadPeakFinder.CalcPeaks();
            if (crawPeaks.size() == 0)
                continue;

            // if there are more than 2 PSMs, we interpolate a curve from their time/score pairs and
            // use the dot product between it and the interpolated SIC to pick the peak
            boost::optional<Interpolator> ms2Interpolator;
            vector<double> ms2Times, ms2Scores;
            if (window.PSMs.size() > 1)
            {
                // calculate the minimum time gap between PSMs
                double minDiff = window.PSMs[1].spectrum->scanStartTime - window.PSMs[0].spectrum->scanStartTime;
                for (size_t i=2; i < window.PSMs.size(); ++i)
                    minDiff = min(minDiff, window.PSMs[i].spectrum->scanStartTime - window.PSMs[i-1].spectrum->scanStartTime);

                // add zero scores before and after the "curve" using the minimum time gap
                ms2Times.push_back(window.PSMs.front().spectrum->scanStartTime - minDiff);
                ms2Scores.push_back(0);
                BOOST_FOREACH(const XICPeptideSpectrumMatch& psm, window.PSMs)
                {
                    ms2Times.push_back(psm.spectrum->scanStartTime);
                    ms2Scores.push_back(psm.score);
                }
                ms2Times.push_back(window.PSMs.back().spectrum->scanStartTime + minDiff);
                ms2Scores.push_back(0);

                ms2Interpolator = Interpolator(ms2Times, ms2Scores);
            }

            // find the peak with the highest sum of (PSM scores * interpolated SIC) within the peak;
            // if no IDs fall within peaks, find the peak closest to the best scoring id
            map<double, map<double, Peak> > peakByIntensityBySumOfProducts;
            BOOST_FOREACH(const CrawdadPeakPtr& crawPeak, crawPeaks)
            {
                double startTime = window.MS1RT[crawPeak->getStartIndex()];
                double endTime = window.MS1RT[crawPeak->getEndIndex()];
                double peakTime = window.MS1RT[crawPeak->getTimeIndex()];
                //double peakTime = startTime + (endTime-startTime)/2;

                // skip degenerate peaks
                if (crawPeak->getFwhm() == 0 || boost::math::isnan(crawPeak->getFwhm()) || startTime == peakTime || peakTime == endTime)
                    continue;

                // skip peaks which don't follow the raw data
                double rawPeakIntensity = window.MS1Intensity[crawPeak->getTimeIndex()];
                if (rawPeakIntensity < window.MS1Intensity[crawPeak->getStartIndex()] ||
                    rawPeakIntensity < window.MS1Intensity[crawPeak->getEndIndex()])
                    continue;

                // Crawdad Fwhm is in index units; we have to translate it back to time units
                double sampleRate = (endTime-startTime) / (crawPeak->getEndIndex()-crawPeak->getStartIndex());
                Peak peak(startTime, endTime, peakTime, crawPeak->getFwhm() * sampleRate, crawPeak->getHeight());
                window.peaks.insert(peak);

                if (!window.bestPeak || fabs((window.bestPeak->peakTime - window.bestScoreScanStartTime) / window.bestPeak->fwhm) >
                                        fabs((peakTime - window.bestScoreScanStartTime) / peak.fwhm))
                    window.bestPeak = peak;

                // calculate sum of products between PSM score and interpolated SIC over 4 standard deviations
                double wideStartTime = peakTime - peak.fwhm * 4 / 2.35482;
                double wideEndTime = peakTime + peak.fwhm  * 4 / 2.35482;
                double sumOfProducts = 0;
                if (ms2Interpolator)
                {
                    size_t wideStartIndex = boost::lower_bound(window.MS1RT, wideStartTime) - window.MS1RT.begin();
                    size_t wideEndIndex = boost::upper_bound(window.MS1RT, wideEndTime) - window.MS1RT.begin();
                    for (size_t i=wideStartIndex; i < wideEndIndex; ++i)
                        sumOfProducts += window.MS1Intensity[i] * ms2Interpolator->interpolate(ms2Times, ms2Scores, window.MS1RT[i]);
                }
                else
                {
                    BOOST_FOREACH(const XICPeptideSpectrumMatch& psm, window.PSMs)
                    {
                        if (wideStartTime <= psm.spectrum->scanStartTime && psm.spectrum->scanStartTime <= wideEndTime)
                            sumOfProducts += psm.score * interpolator.interpolate(window.MS1RT, window.MS1Intensity, psm.spectrum->scanStartTime);
                    }
                }

                if (sumOfProducts > 0)
                    peakByIntensityBySumOfProducts[sumOfProducts][peak.intensity] = peak;
            }
            if (!peakByIntensityBySumOfProducts.empty())
                window.bestPeak = peakByIntensityBySumOfProducts.rbegin()->second.rbegin()->second;
                
            if(window.bestPeak)
                windowBestPeaks++;

            /*if (!window.bestPeak)
            {
                cerr << "Warning: unable to select the best Crawdad peak for distinct match! " << window.peptide
                     << " (id: " << window.PSMs[0].peptide
                     << "; m/z: " << window.preMZ
                     << "; time: " << window.preRT
                     << ")" << endl;
            }
            else
                cout << "\n" << firstMS2.nativeID << "\t" << window.peptide << "\t" << window.bestPeak->intensity << "\t" << window.bestPeak->peakTime << "\t" << firstMS2.precursorIntensity << "\t" << (window.bestPeak->intensity / firstMS2.precursorIntensity);*/
        }
        i = 0;
        //cycle through all regression defined precursors, passing each one to crawdad
        ITERATION_UPDATE(ilr, currentFile, totalFiles, "\rFinding regression defined peptide peaks...");
            BOOST_FOREACH(RegDefinedPrecursorInfo& info, RegDefinedPrecursors)
        {

            ++i;
            LocalChromatogram& lc = info.chromatogram;
            if (lc.MS1RT.empty())
            {
                /*cerr << "Warning: regression defined precursor m/z " << info.exactMZ
                     << " (m/z: " << info.mzWindow
                     << "; time: " << info.scanTimeWindow
                     << ") has no chromatogram data points!" << endl;*/
                continue;
            }

                if(lc.MS1RT.size()<4){
            //cerr<<"Warning: the MS1 RT size is less than 4: "<<info.exactMZ<<endl;
            continue;
            }
            // make chromatogram data points evenly spaced
            Interpolator(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity).resample(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity);


            // eliminate negative signal
            BOOST_FOREACH(double& intensity, info.chromatogram.MS1Intensity)
                intensity = max(0.0, intensity);

            CrawdadPeakFinder crawdadPeakFinder;
            crawdadPeakFinder.SetChromatogram(lc.MS1RT, lc.MS1Intensity);
            info.baselineIntensity= crawdadPeakFinder.getBaselineIntensity();

            vector<CrawdadPeakPtr> crawPeaks = crawdadPeakFinder.CalcPeaks();
             if (crawPeaks.size() == 0)
                continue;


            BOOST_FOREACH(const CrawdadPeakPtr& crawPeak, crawPeaks)
            {

                double startTime = lc.MS1RT[crawPeak->getStartIndex()];
                double endTime = lc.MS1RT[crawPeak->getEndIndex()];
                double peakTime = lc.MS1RT[crawPeak->getTimeIndex()];
                //double peakTime = startTime + (endTime-startTime)/2;

                // skip degenerate peaks
                if (crawPeak->getFwhm() == 0 || boost::math::isnan(crawPeak->getFwhm()) || startTime == peakTime || peakTime == endTime)
                    continue;

                // skip peaks which don't follow the raw data
                double rawPeakIntensity = lc.MS1Intensity[crawPeak->getTimeIndex()];
                if (rawPeakIntensity < lc.MS1Intensity[crawPeak->getStartIndex()] ||
                    rawPeakIntensity < lc.MS1Intensity[crawPeak->getEndIndex()])
                    continue;

                // Crawdad Fwhm is in index units; we have to translate it back to time units
                double sampleRate = (endTime-startTime) / (crawPeak->getEndIndex()-crawPeak->getStartIndex());
                Peak peak(startTime, endTime, peakTime, crawPeak->getFwhm() * sampleRate, crawPeak->getHeight());
                lc.peaks.insert(peak);

                //if bestPeak is still null  (i.e. if statement is executing for the first time) or the current peak is closer to the regression time than the best peak, set the current peak as the best peak
                if (!lc.bestPeak || fabs(peakTime - info.RegTime) < fabs(lc.bestPeak->peakTime - info.RegTime))
                    lc.bestPeak = peak;
                    
                if (info.chromatogram.bestPeak)
                    regBestPeaks++;

            }
        }
            
        // populate the source statistics
        sqlite::command cmd2(idpDb, "UPDATE SpectrumSource SET "
                                    "TotalSpectraMS1 = ?, "
                                    "TotalSpectraMS2 = ?, "
                                    "QuantitationMethod = ? "
                                    "WHERE Id = ?");
        cmd2.binder() << MS1Count <<
                         MS2Count <<
                         int(QuantitationMethod::LabelFree) <<
                         sourceId;
        cmd2.execute();
        cmd2.reset();

        // Write chromatograms for visualization of data
        return writeChromatograms(idpDBFilePath, pepWindow,RegDefinedPrecursors, ilr, currentFile, totalFiles);
    }
    catch (exception& e)
    {
        throw runtime_error(string("[EmbedMS1ForFile] ") + e.what());
    }
    return 0;
}

} // namespace XIC
END_IDPICKER_NAMESPACE
