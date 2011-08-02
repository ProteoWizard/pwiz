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
// The Initial Developer of the Original Code is Ken Polzin.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#include "quameter.h"

/**
 * The primary function where all metrics are calculated.
 */
void MetricMaster(const string& dbFilename, string sourceFilename, const string& sourceId) {
try {
	boost::timer t;

	ofstream qout; // short for quameter output, save to same directory as input file
	qout.open (	boost::filesystem::change_extension(sourceFilename, "-quameter_results.txt").string().c_str() );

	// Set up the reading	
	FullReaderList readers;
	MSDataFile msd(sourceFilename, & readers);
	
    int startSpectraIndex = 0;	
    SpectrumList& spectrumList = *msd.run.spectrumListPtr;
    string sourceName = GetFilenameWithoutExtension( GetFilenameFromFilepath( sourceFilename ) );

    // Spectral indices
	size_t firstIndex = max((size_t)startSpectraIndex, (size_t)0);
    size_t maxIndex = spectrumList.size()-1;
    size_t lastIndex = maxIndex;

	int MS1Count = 0, MS2Count = 0;
	
	vector<CVParam> scanTime;
	vector<CVParam> scan2Time;
	vector<double> retentionTime;
	int iter = 0;
	vector<string> nativeID = GetNativeId(dbFilename, sourceId);
	map<string, double> ticMap;
	vector<double> ionInjectionTimeMS1;
	vector<double> ionInjectionTimeMS2;
	vector<int> MS2Peaks;
	multimap<string, double> precursorRetentionMap;
	map<string, double> retentionMap;

	// scanInfo holds MS1 and MS2 data
	vector<ms_amalgam> scanInfo;
	vector<preMZandRT> unidentMS2;
	
	// If we have an MS2 nativeID find the scanInfo array position
	map<string, int> nativeToArrayMap;
	map<int, string> arrayToNativeMap;

    // For each spectrum
    for( size_t curIndex = firstIndex; curIndex <= lastIndex; ++curIndex ) {
		SpectrumPtr spectrum = spectrumList.spectrum(curIndex, true);
	
        if( spectrum->cvParam(MS_MSn_spectrum).empty() && spectrum->cvParam(MS_MS1_spectrum).empty() )
            continue;

        CVParam spectrumMsLevel = spectrum->cvParam(MS_ms_level);
        if( spectrumMsLevel == CVID_Unknown )
            continue;

        // Check its MS level and increment the count
		int msLevel = spectrumMsLevel.valueAs<int>();
        if( msLevel == 1 ) {
			Scan& scan = spectrum->scanList.scans[0];
			// The scan= number is 1+Spectrum, or 1+curIndex
			scanTime.push_back(scan.cvParam(MS_scan_start_time));
			if (!scan.hasCVParam(MS_scan_start_time)) cout << "No MS1 retention time found for " << spectrum->id << "!\n";
			precursorRetentionMap.insert(pair<string, double>(spectrum->id, scan.cvParam(MS_scan_start_time).timeInSeconds()));
			ticMap[spectrum->id] = spectrum->cvParam(MS_total_ion_current).valueAs<double>();
			arrayToNativeMap[MS1Count] = spectrum->id;

//all m/z data for a spectrum!
//			vector<double> mzArray = spectrum->getMZArray()->data;
			// For metric MS1-1 -- note, do not use for Waters raw files!
			if (scan.hasCVParam(MS_ion_injection_time))
				ionInjectionTimeMS1.push_back(scan.cvParam(MS_ion_injection_time).valueAs<double>());
            MS1Count++;
		}
		else if( msLevel == 2) {
			Precursor& precursor = spectrum->precursors[0];
			const SelectedIon& si = precursor.selectedIons[0];
			double precursorIntensity = si.cvParam(MS_peak_intensity).valueAs<double>();
			string precursorID = precursor.spectrumID;

			Scan& scan = spectrum->scanList.scans[0];
			scan2Time.push_back(scan.cvParam(MS_scan_start_time)); // in seconds
			// For metric MS2-1 -- do not use for Waters files
			if (scan.hasCVParam(MS_ion_injection_time))
				ionInjectionTimeMS2.push_back(scan.cvParam(MS_ion_injection_time).valueAs<double>());
			MS2Peaks.push_back(spectrum->defaultArrayLength);		

			// If the scan is empty don't count it -- not used currently
			// if (spectrum->defaultArrayLength == 0)
			MS2Count++;
	
			double precursorMZ = si.cvParam(MS_selected_ion_m_z).valueAs<double>();
			if( si.cvParam(MS_selected_ion_m_z).empty() ) precursorMZ = si.cvParam(MS_m_z).valueAs<double>();	
			
			// Only look at retention times of peptides identified in .idpDB
			// curIndex is the spectrum index, curIndex+1 is (usually) the scan number
			if( iter < ((int)nativeID.size()) && (spectrum->id == nativeID[iter]) ) {
				retentionTime.push_back(scan.cvParam(MS_scan_start_time).timeInSeconds());
				retentionMap[spectrum->id] = scan.cvParam(MS_scan_start_time).timeInSeconds();

				ms_amalgam amalgTemp;
				amalgTemp.MS2 = spectrum->id;
				amalgTemp.MS2Retention = scan.cvParam(MS_scan_start_time).timeInSeconds();
				amalgTemp.precursor = precursorID;
				amalgTemp.precursorMZ = precursorMZ;
				amalgTemp.precursorIntensity = precursorIntensity;
				amalgTemp.precursorRetention = precursorRetentionMap.find(precursorID)->second;		
				scanInfo.push_back(amalgTemp);

				int iii = scanInfo.size() - 1;
				nativeToArrayMap[scanInfo[iii].MS2] = iii;
				iter++;
			}
			else { // this MS2 scan was not identified; we need this data for metrics MS2-4A/B/C/D
				preMZandRT tmp;
				tmp.MS2Retention = scan.cvParam(MS_scan_start_time).timeInSeconds();
				tmp.precursorMZ = precursorMZ;
				tmp.precursorRetention = precursorRetentionMap.find(precursorID)->second;
				unidentMS2.push_back(tmp);			
			}
		}

    } // finished cycling through all spectra

	// Find the first RT quartile
	double firstQuartileIDTime = 0;
	int firstQuartileIndex = 0;
	int retentSize = (int)retentionTime.size();
	if ( retentSize % 4 == 0 ) {
		int index1 = (retentSize/4)-1;
		int index2 = (retentSize/4);
		firstQuartileIDTime = (retentionTime[index1] + retentionTime[index2]) / 2;
		firstQuartileIndex = (index1 + index2) / 2;
	}
	else {
		int index1 = (retentSize)/4;
		firstQuartileIDTime = retentionTime[index1];
		firstQuartileIndex = index1;
	}
	
	// Find the third quartile
	double thirdQuartileIDTime = 0;
	int thirdQuartileIndex = 0;
	if ( (retentSize * 3) % 4 == 0 ) {
		int index1 = (3*retentSize/4)-1;
		int index2 = (3*retentSize/4);
		thirdQuartileIDTime = (retentionTime[index1] + retentionTime[index2]) / 2;
		thirdQuartileIndex = (index1 + index2) / 2;
	}
	else {
		int index1 = ( (3*retentSize)/4 );
		thirdQuartileIDTime = retentionTime[index1];
		thirdQuartileIndex = index1;
	}
	
// ------------------- start peak-finding code ------------------- //	

	vector<windowData> pepWindow = MZRTWindows(dbFilename, sourceId, nativeToArrayMap, scanInfo);
	vector<double> sigNoisMS1;
	vector<double> sigNoisMS2;
	vector<chromatogram> identMS2Chrom(scanInfo.size());
	vector<chromatogram> unidentMS2Chrom(unidentMS2.size());
	iter = 0;

	// Going through all spectra once more to get intensities/retention times to build chromatograms
    for( size_t curIndex = firstIndex; curIndex <= lastIndex; ++curIndex ) {
		SpectrumPtr spectrum = spectrumList.spectrum(curIndex, true);

        if( spectrum->cvParam(MS_MSn_spectrum).empty() && spectrum->cvParam(MS_MS1_spectrum).empty() )
            continue;

        CVParam spectrumMsLevel = spectrum->cvParam(MS_ms_level);
        if( spectrumMsLevel == CVID_Unknown )
            continue;

        // this time around we're only looking for MS1 spectra
		int msLevel = spectrumMsLevel.valueAs<int>();
        if( msLevel == 1 ) {
			Scan& scan = spectrum->scanList.scans[0];	
			
			// all m/z and intensity data for a spectrum
			vector<double> mzV 	   = spectrum->getMZArray()->data;
			vector<double> intensV = spectrum->getIntensityArray()->data;
			unsigned int arraySize = mzV.size();
			double curRT = scan.cvParam(MS_scan_start_time).timeInSeconds();
			
			double mzMin = *min_element(mzV.begin(), mzV.end());
			double mzMax = *max_element(mzV.begin(), mzV.end());

			// For Metric MS1-2A, signal to noise ratio of MS1, peaks/medians
			if (curRT <= thirdQuartileIDTime) {
				vector<double> sortedIntens = intensV;
				sort(sortedIntens.begin(), sortedIntens.end());

				double medianIntens = Q2(sortedIntens);
		
				sigNoisMS1.push_back( (double)sortedIntens[arraySize-1] / medianIntens );
			}
			
			for (unsigned int iWin = 0; iWin < pepWindow.size(); iWin++) {
				// if the MS1 retention time is not in the RT window constructed for this peptide, go to the next peptide
				if (!contains(pepWindow[iWin].preRT, curRT)) continue;
				
				double mzWindowMin = lower(*pepWindow[iWin].preMZ.begin());
				double mzWindowMax = upper(*pepWindow[iWin].preMZ.rbegin());

				// if the m/z window is outside this MS1 spectrum's m/z range, go to the next peptide
				if (mzWindowMax < mzMin || mzWindowMin > mzMax) continue;

				double sumIntensities = 0;
				// now cycle through the mzV vector
				for (unsigned int iMZ = 0; iMZ < arraySize; iMZ++) {
					// if this m/z is in the window, record its intensity and retention time
					if (contains(pepWindow[iWin].preMZ, mzV[iMZ]))
						sumIntensities += intensV[iMZ];
				}
				if (sumIntensities != 0) {
					pepWindow[iWin].MS1Intensity.push_back(sumIntensities);
					pepWindow[iWin].MS1RT.push_back(curRT);
				}

			} // done searching through all unique peptide windows for this MS1 scan
			
			// loop through all identified MS2 scans, not just uniques
			for (int idIter = 0, idSize = scanInfo.size(); idIter < idSize; idIter++) {
				
				double RTLower = scanInfo[idIter].precursorRetention - 300; // lower bound for RT interval
				double RTUpper = scanInfo[idIter].precursorRetention + 300; // lower bound for RT interval	
				continuous_interval<double> RTWindow = construct<continuous_interval<double> >(RTLower, RTUpper, interval_bounds::closed());
			
				if (!contains(RTWindow, curRT)) continue;

				double mzLower = scanInfo[idIter].precursorMZ - 0.5;
				double mzUpper = scanInfo[idIter].precursorMZ + 1.0;
				interval_set<double> mzWindow;
				mzWindow.insert( construct<continuous_interval<double> >(mzLower, mzUpper, interval_bounds::closed()) );
				double mzWindowMin = lower(*mzWindow.begin());
				double mzWindowMax = upper(*mzWindow.rbegin());
		
				// if the m/z window is outside this MS1 spectrum's m/z range, go to the next peptide
				if (mzWindowMax < mzMin || mzWindowMin > mzMax) continue;

				// if the m/z window is outside this MS1 spectrum's m/z range, go to the next peptide
				if (mzWindowMax < mzMin || mzWindowMin > mzMax) continue;		

				double sumIntensities = 0;
				// now cycle through the mzV vector
				for (unsigned int iMZ = 0; iMZ < arraySize; iMZ++) {
					// if this m/z is in the window, record its intensity and retention time
					if (contains(mzWindow, mzV[iMZ]))
						sumIntensities += intensV[iMZ];
				}
				if (sumIntensities != 0) {
					identMS2Chrom[idIter].MS1Intensity.push_back(sumIntensities);
					identMS2Chrom[idIter].MS1RT.push_back(curRT);
				}			
			} // done with identified MS2 scans
			
			// loop through all unidentified MS2 scans
			for (int unidIter = 0, unidSize = unidentMS2.size(); unidIter < unidSize; unidIter++) {
				
				double RTLower = unidentMS2[unidIter].precursorRetention - 300; // lower bound for RT interval
				double RTUpper = unidentMS2[unidIter].precursorRetention + 300; // lower bound for RT interval	
				continuous_interval<double> RTWindow = construct<continuous_interval<double> >(RTLower, RTUpper, interval_bounds::closed());
			
				if (!contains(RTWindow, curRT)) continue;

				double mzLower = unidentMS2[unidIter].precursorMZ - 0.5;
				double mzUpper = unidentMS2[unidIter].precursorMZ + 1.0;
				interval_set<double> mzWindow;
				mzWindow.insert( construct<continuous_interval<double> >(mzLower, mzUpper, interval_bounds::closed()) );
			
				double mzWindowMin = lower(*mzWindow.begin());
				double mzWindowMax = upper(*mzWindow.rbegin());
				
				// if the m/z window is outside this MS1 spectrum's m/z range, go to the next peptide
				if (mzWindowMax < mzMin || mzWindowMin > mzMax) continue;

				// if the m/z window is outside this MS1 spectrum's m/z range, go to the next peptide
				if (mzWindowMax < mzMin || mzWindowMin > mzMax) continue;		

				double sumIntensities = 0;
				// now cycle through the mzV vector
				for (unsigned int iMZ = 0; iMZ < arraySize; iMZ++) {
					// if this m/z is in the window, record its intensity and retention time
					if (contains(mzWindow, mzV[iMZ]))
						sumIntensities += intensV[iMZ];
				}
				if (sumIntensities != 0) {
					unidentMS2Chrom[unidIter].MS1Intensity.push_back(sumIntensities);
					unidentMS2Chrom[unidIter].MS1RT.push_back(curRT);
				}
			} // done with unidentified MS2 scans		
			
		}
		else if ( msLevel == 2) {
			// For Metric MS2-2, signal to noise ratio of MS1, peaks/medians
			// We're only looking at identified peptides here
			if( iter < ((int)nativeID.size()) && (spectrum->id == nativeID[iter]) ) {
				vector<double> intensV = spectrum->getIntensityArray()->data;
				int arraySize = intensV.size();
				sort(intensV.begin(), intensV.end());

				double medianIntens = Q2(intensV);
				
				sigNoisMS2.push_back( (double)intensV[arraySize-1] / medianIntens );
				iter++;
					
			}
		}
	} // end of spectra searching. we now have Intensity/RT pairs to build chromatograms

	// Make an MSData object and output some chromatograms for SeeMS to read
	MSData chromData;
	shared_ptr<ChromatogramListSimple> chromatogramListSimple(new ChromatogramListSimple);
	chromData.run.chromatogramListPtr = chromatogramListSimple;
	chromData.run.id = "this must be set";
	
	// Put unique identified peptide chromatograms first in the file
	for (unsigned int iWin = 0; iWin < pepWindow.size(); iWin++) {
		chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
		Chromatogram& c = *chromatogramListSimple->chromatograms.back();
		c.index = iWin;
		c.id = "unique identified peptide";
		c.setTimeIntensityArrays(pepWindow[iWin].MS1RT, pepWindow[iWin].MS1Intensity, UO_second, MS_number_of_counts);
	}
	// next are chromatograms from identified MS2 scans
	unsigned int iOffset = pepWindow.size();
	for (unsigned int i = 0; i < identMS2Chrom.size(); i++) {
		chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
		Chromatogram& c = *chromatogramListSimple->chromatograms.back();
		c.index = i + iOffset;
		c.id = "identified MS2 scan";
		c.setTimeIntensityArrays(identMS2Chrom[i].MS1RT, identMS2Chrom[i].MS1Intensity, UO_second, MS_number_of_counts);
	}
	// last are chromatograms from
	iOffset += identMS2Chrom.size();
	for (unsigned int i = 0; i < unidentMS2Chrom.size(); i++) {
		chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
		Chromatogram& c = *chromatogramListSimple->chromatograms.back();
		c.index = i + iOffset;
		c.id = "unidentified MS2 scan";
		c.setTimeIntensityArrays(unidentMS2Chrom[i].MS1RT, unidentMS2Chrom[i].MS1Intensity, UO_second, MS_number_of_counts);
	}
	string chromFilename = boost::filesystem::change_extension(sourceFilename, "-quameter_chromatograms.mzML").string();
	MSDataFile::write(chromData, chromFilename);

	// Find peaks with Crawdad
	using namespace SimpleCrawdad;
	using namespace crawpeaks;
	typedef boost::shared_ptr<CrawdadPeak> CrawdadPeakPtr;
	vector<intensityPair> idMS2Intensities;
	vector<double> idMS2PeakV;
	vector<double> unidMS2PeakV;
	vector<double> allMS2Peaks;
	vector<double> identifiedPeptideFwhm;
	vector<double> identifiedPeptidePeaks;
	
	// cycle through all unique peptides, passing each one to crawdad
	for (unsigned int iWin = 0; iWin < pepWindow.size(); iWin++) {
		CrawdadPeakFinder crawdadPeakFinder;
		crawdadPeakFinder.SetChromatogram(pepWindow[iWin].MS1RT, pepWindow[iWin].MS1Intensity);

		vector<CrawdadPeakPtr> crawPeaks = crawdadPeakFinder.CalcPeaks();
		if (crawPeaks.size() == 0) continue;
		
		double closestRT = -1;
		double closestIntensity;
		double closestFwhm;

		BOOST_FOREACH(CrawdadPeakPtr peakPtr, crawPeaks) {
			int rtIndex = (*peakPtr).getTimeIndex();
			if (closestRT == -1) {
				closestRT = pepWindow[iWin].MS1RT[rtIndex];
				closestIntensity = (*peakPtr).getHeight();
				closestFwhm = (*peakPtr).getFwhm();
			}
			else if (fabs(pepWindow[iWin].MS1RT[rtIndex] - pepWindow[iWin].firstMS2RT) < fabs(closestRT - pepWindow[iWin].firstMS2RT)) {
				closestRT = pepWindow[iWin].MS1RT[rtIndex];
				closestIntensity = (*peakPtr).getHeight();
				closestFwhm = (*peakPtr).getFwhm();
			}
		}
		
		identifiedPeptideFwhm.push_back(closestFwhm);
		identifiedPeptidePeaks.push_back(closestIntensity);
	}
	// cycle through all identifed MS2 scans, passing each one to crawdad
	for (unsigned int i = 0; i < identMS2Chrom.size(); i++) {
		CrawdadPeakFinder crawdadPeakFinder;
		crawdadPeakFinder.SetChromatogram(identMS2Chrom[i].MS1RT, identMS2Chrom[i].MS1Intensity);

		vector<CrawdadPeakPtr> crawPeaks = crawdadPeakFinder.CalcPeaks();
		if (crawPeaks.size() == 0) continue;
		
		double closestRT = -1;
		double closestIntensity;

		BOOST_FOREACH(CrawdadPeakPtr peakPtr, crawPeaks) {
			int rtIndex = (*peakPtr).getTimeIndex();
			if (closestRT == -1) {
				closestRT = identMS2Chrom[i].MS1RT[rtIndex];
				closestIntensity = (*peakPtr).getHeight();
			}
			else if (fabs(identMS2Chrom[i].MS1RT[rtIndex] - scanInfo[i].MS2Retention) < fabs(closestRT - scanInfo[i].MS2Retention)) {
				closestRT = identMS2Chrom[i].MS1RT[rtIndex];
				closestIntensity = (*peakPtr).getHeight();
			}
		}

		intensityPair tmpIntensityPair;
		tmpIntensityPair.precursorIntensity = scanInfo[i].precursorIntensity;
		tmpIntensityPair.peakIntensity = closestIntensity;
		idMS2Intensities.push_back(tmpIntensityPair);
		idMS2PeakV.push_back(closestIntensity);
		allMS2Peaks.push_back(closestIntensity);
	}
	// cycle through all unidentifed MS2 scans, passing each one to crawdad
	for (unsigned int i = 0; i < unidentMS2Chrom.size(); i++) {
		CrawdadPeakFinder crawdadPeakFinder;
		crawdadPeakFinder.SetChromatogram(unidentMS2Chrom[i].MS1RT, unidentMS2Chrom[i].MS1Intensity);
		vector<CrawdadPeakPtr> crawPeaks = crawdadPeakFinder.CalcPeaks();
		
		if (crawPeaks.size() == 0) continue;
		
		double closestRT = -1;
		double closestIntensity;

		BOOST_FOREACH(CrawdadPeakPtr peakPtr, crawPeaks) {
			int rtIndex = (*peakPtr).getTimeIndex();
			if (closestRT == -1) {
				closestRT = unidentMS2Chrom[i].MS1RT[rtIndex];
				closestIntensity = (*peakPtr).getHeight();
			}
			else if (fabs(unidentMS2Chrom[i].MS1RT[rtIndex] - unidentMS2[i].MS2Retention) < fabs(closestRT - unidentMS2[i].MS2Retention)) {
				closestRT = unidentMS2Chrom[i].MS1RT[rtIndex];
				closestIntensity = (*peakPtr).getHeight();
			}
		}
		
		unidMS2PeakV.push_back(closestIntensity);
		allMS2Peaks.push_back(closestIntensity);
	}
// -------------------- end peak-finding code -------------------- //

	// More quartiles to find, this time for precursor intensities
	sort(idMS2PeakV.begin(), idMS2PeakV.end());
	sort(unidMS2PeakV.begin(), unidMS2PeakV.end());
	sort(allMS2Peaks.begin(), allMS2Peaks.end());

	// Find the first intensity quartile
	double intensQ1 = Q1(allMS2Peaks);
	
	// Find the median intensity (Q2)
	double intensQ2 = Q2(allMS2Peaks);
	
	// Find the third intensity quartile
	double intensQ3 = Q3(allMS2Peaks);

	int idQ1=0, idQ2=0, idQ3=0, idQ4=0;	// number of identified MS2s that have precursor max intensities in each quartile
	for(unsigned int i=0, idSize=idMS2PeakV.size(); i < idSize; i++) {
		if (idMS2PeakV[i] <= intensQ1) idQ1++;
		else if (idMS2PeakV[i] <= intensQ2) idQ2++;
		else if (idMS2PeakV[i] <= intensQ3) idQ3++;
		else idQ4++;
	}

	int unidQ1=0, unidQ2=0, unidQ3=0, unidQ4=0;	// number of unidentified MS2s that have precursor max intensities in each quartile
	for(unsigned int i=0, unidSize=unidMS2PeakV.size(); i < unidSize; i++) {
		if (unidMS2PeakV[i] <= intensQ1) unidQ1++;
		else if (unidMS2PeakV[i] <= intensQ2) unidQ2++;
		else if (unidMS2PeakV[i] <= intensQ3) unidQ3++;
		else unidQ4++;
	}

	int totalQ1 = idQ1+unidQ1;
	int totalQ2 = idQ2+unidQ2;
	int totalQ3 = idQ3+unidQ3;
	int totalQ4 = idQ4+unidQ4;
	
	// Metric C-2A
	double iqIDTime = (thirdQuartileIDTime - firstQuartileIDTime);	// iqIDTime stands for interquartile identification time (in seconds)

	// Metric C-2B
	double iqIDRate = (thirdQuartileIndex - firstQuartileIndex) *60 / iqIDTime;

	// Metric C-4A: Median peak width for peptides in last decile sorted by RT
	// Going slightly out of order (C-4A/B before C-3A/B) because the former use 'unsorted' identifiedPeptideFwhm (default is sorted by RT) while the C-3A/B then sort identifiedPeptideFwhm by width size
	double medianFwhmLastRTDecile;
	int sizeFwhm = identifiedPeptideFwhm.size();
	int lastDecileStart = ( (sizeFwhm+1) * 9 / 10 );
	int lastDecile = sizeFwhm - lastDecileStart;
	if ( sizeFwhm < 10)	// If there aren't more than 10 items in this vector the last decile is meaningless
		medianFwhmLastRTDecile = identifiedPeptideFwhm[sizeFwhm-1];
	else if ( lastDecile % 2 == 0 ) {
		int index1 = (lastDecile/2)-1 + lastDecileStart;
		int index2 = (lastDecile/2) + lastDecileStart;
		medianFwhmLastRTDecile = (identifiedPeptideFwhm[index1] + identifiedPeptideFwhm[index2]) / 2;
	}
	else {
		int index1 = (lastDecile)/2 + lastDecileStart;
		medianFwhmLastRTDecile = identifiedPeptideFwhm[index1];
	}	

	// Metric C-4B: Median peak width for peptides in first decile sorted by RT
	double medianFwhmFirstRTDecile;
	int firstDecile = (sizeFwhm+1) / 10;
	if ( sizeFwhm < 10)
		medianFwhmFirstRTDecile = identifiedPeptideFwhm[0];
	else if ( firstDecile % 2 == 0 ) {
		int index1 = (firstDecile/2)-1;
		int index2 = (firstDecile/2);
		medianFwhmFirstRTDecile = (identifiedPeptideFwhm[index1] + identifiedPeptideFwhm[index2]) / 2;
	}
	else {
		int index1 = (firstDecile)/2;
		medianFwhmFirstRTDecile = identifiedPeptideFwhm[index1];
	}
	
	// Metric C-4C: Median peak width for peptides "in the median decile" sorted by RT -- really just the median of all the values
	double medianFwhmByRT = Q2(identifiedPeptideFwhm);
	
	// Metric C-3A - uses sizeFwhm from Metric C-4A; the difference is the sorting here
	sort(identifiedPeptideFwhm.begin(), identifiedPeptideFwhm.end());
	double medianFwhm = Q2(identifiedPeptideFwhm);

	// Metric C-3B
	// First quartile
	double fwhmQ1 = Q1(identifiedPeptideFwhm);
	
	// Third quartile
	double fwhmQ3 = Q3(identifiedPeptideFwhm);
	
	// Interquartile fwhm
	double iqFwhm = fwhmQ3 - fwhmQ1;

	// Metric DS-2A - number of MS1 scans taken over C-2A
	int iqMS1Scans = 0;
	for(int i=0; i<(int)scanTime.size(); i++) {
		if (scanTime[i].timeInSeconds() >= firstQuartileIDTime && scanTime[i].timeInSeconds() <= thirdQuartileIDTime)
			iqMS1Scans++;
	}
//	double iqMS1Rate = iqMS1Scans / iqIDTime;

	// Metric DS-2B - number of MS2 scans taken over C-2A
	int iqMS2Scans = 0;
	for(int i=0; i<(int)scan2Time.size(); i++) {
		if (scan2Time[i].timeInSeconds() >= firstQuartileIDTime && scan2Time[i].timeInSeconds() <= thirdQuartileIDTime)
			iqMS2Scans++;
	}
//	double iqMS2Rate = iqMS2Scans / iqIDTime;

	// Look for repeat peptide IDs in .idpDB. Used for metric C-1A and C-1B
	multimap<int, string> duplicatePeptides = GetDuplicateID(dbFilename, sourceId);
	
	typedef multimap<int, string>::iterator mapIter;
    mapIter m_it, s_it;
	int bleed = 0;			// For Metric C-1A
	int peakTailing = 0;	// For Metric C-1B
	int numDuplicatePeptides = duplicatePeptides.size();

	// Cycle through the duplicate peptides, listing every MS2 scan per peptide
    for (m_it = duplicatePeptides.begin();  m_it != duplicatePeptides.end();  m_it = s_it) {
        int theKey = (*m_it).first;
		double peakIntensity = -1, maxMS1Time = -1;
        pair<mapIter, mapIter> keyRange = duplicatePeptides.equal_range(theKey);
		
		if ( (int)duplicatePeptides.count(theKey) < 2 ) {
			numDuplicatePeptides--;
//			cout << "Skipping peptide " << theKey << " because it's not a repeat ID\n";
			s_it = keyRange.second;
			continue;
		}		

        for (s_it = keyRange.first;  s_it != keyRange.second;  ++s_it) {
			// get precursor intensity and time
			int temp = nativeToArrayMap.find((*s_it).second)->second;
			if (peakIntensity == -1 || (scanInfo[temp].precursorIntensity > peakIntensity))
			{
				peakIntensity = scanInfo[temp].precursorIntensity;
				maxMS1Time = scanInfo[temp].precursorRetention;
			}
        }

        for (s_it = keyRange.first;  s_it != keyRange.second;  ++s_it) {
			// compare MS2 time to max MS1 time
			int temp = nativeToArrayMap.find( (*s_it).second )->second;
			if ( (scanInfo[temp].MS2Retention - maxMS1Time) > 4) {
				peakTailing++;
			}
			else if ( (maxMS1Time - scanInfo[temp].MS2Retention) > 4)
				bleed++;			
        }	
    }

	// Metric C-1A: Chromatographic bleeding
	float peakTailingRatio = (float)peakTailing/duplicatePeptides.size();
	
	// Metric C-1B: Chromatographic peak tailing
	float bleedRatio = (float)bleed/duplicatePeptides.size();
	
	// Metric DS-1A: Estimates oversampling
	int identifiedOnce = PeptidesIdentifiedOnce(dbFilename, sourceId);
	int identifiedTwice = PeptidesIdentifiedTwice(dbFilename, sourceId);
	float DS1A = (float)identifiedOnce/identifiedTwice;
	
	// Metric DS-1B: Estimates oversampling
	int identifiedThrice = PeptidesIdentifiedThrice(dbFilename, sourceId);
	float DS1B = (float)identifiedTwice/identifiedThrice;

	// For metric DS-3A: Ratio of MS1 max intensity over sampled intensity for median identified peptides sorted by max intensity
	sort( idMS2Intensities.begin(), idMS2Intensities.end(), compareByPeak );
	int idMS2Size = (int)idMS2Intensities.size();
	double medianSamplingRatio = 0;
	if ( idMS2Size % 2 == 0 ) {
		int index1 = (idMS2Size/2)-1;
		int index2 = (idMS2Size/2);
		medianSamplingRatio = ((idMS2Intensities[index1].peakIntensity / idMS2Intensities[index1].precursorIntensity) + (idMS2Intensities[index2].peakIntensity / idMS2Intensities[index2].precursorIntensity)) / 2;
	}
	else {
		int index1 = (idMS2Size)/2;
		medianSamplingRatio = (idMS2Intensities[index1].peakIntensity / idMS2Intensities[index1].precursorIntensity);
	}	

	// For metric DS-3B: Same as DS-3A except only look at the bottom 50% of identified peptides sorted by max intensity
	double bottomHalfSamplingRatio = 0;
	if ( idMS2Size % 4 == 0 ) {
		int index1 = (idMS2Size/4)-1;
		int index2 = (idMS2Size/4);
		bottomHalfSamplingRatio = ((idMS2Intensities[index1].peakIntensity / idMS2Intensities[index1].precursorIntensity) + (idMS2Intensities[index2].peakIntensity / idMS2Intensities[index2].precursorIntensity)) / 2;
	}
	else {
		int index1 = (idMS2Size)/4;
		bottomHalfSamplingRatio = (idMS2Intensities[index1].peakIntensity / idMS2Intensities[index1].precursorIntensity);
	}
	
	// Metrics IS-1A and IS-1B
	double lastTIC = -1;
	int ticDrop = 0;
	int ticJump = 0;
	for (iter = 0; iter < (int)ticMap.size(); iter++) {
		string nativeIter = arrayToNativeMap.find(iter)->second;
		if (precursorRetentionMap.find(nativeIter)->second <= thirdQuartileIDTime) {
			// Is the current total ion current less than 1/0th of the last MS1 scan?
			if ( (lastTIC != -1) && (10*(ticMap.find(nativeIter)->second) < lastTIC)) {
				ticDrop++;
//silent				cout << "NativeID: " << nativeIter << "\tPrevious TIC: " << lastTIC << "\tCurrent TIC: " << ticMap.find(nativeIter)->second << endl;
			}
			// Is the current total ion current more than 10 times the last MS1 scan?
			else if ( (lastTIC != -1) && ((ticMap.find(nativeIter)->second) > (10*lastTIC)) ) {
				ticJump++;
//silent				cout << "NativeID: " << nativeIter << "\tPrevious TIC: " << lastTIC << "\tCurrent TIC: " << ticMap.find(nativeIter)->second << endl;
			}	
			
			lastTIC = ticMap.find(nativeIter)->second;			
		}
	}

	// Call MedianPrecursorMZ() for metric IS-2
	double medianPrecursorMZ = MedianPrecursorMZ(dbFilename, sourceId);

	// Call PeptideCharge() for metrics IS-3A, IS3-B, IS3-C
	fourInts PeptideCharge(const string&, const string&);
	fourInts allCharges = PeptideCharge(dbFilename, sourceId);
	int charge1 = allCharges.first;
	int charge2 = allCharges.second;
	int charge3 = allCharges.third;
	int charge4 = allCharges.fourth;
	
	float IS3A = (float)charge1/charge2;
	float IS3B = (float)charge3/charge2;
	float IS3C = (float)charge4/charge2;
	
	// MS1-1: Median MS1 ion injection time
	double medianInjectionTimeMS1 = 0;
	if (!ionInjectionTimeMS1.empty()) {
		sort(ionInjectionTimeMS1.begin(), ionInjectionTimeMS1.end());
		medianInjectionTimeMS1 = Q2(ionInjectionTimeMS1);
	}

	// MS1-2A: Median signal-to-noise ratio (which is max peak/median peak) in MS1 spectra before C-2A
	
	sort(sigNoisMS1.begin(), sigNoisMS1.end());
	double medianSigNoisMS1 = Q2(sigNoisMS1);
		
	// MS1-2B: Median TIC value for identified peptides through the third quartile
	
	vector<double> ticVector;
	for (int iTic = 0; iTic < thirdQuartileIndex; iTic++)
		ticVector.push_back(ticMap.find(scanInfo[iTic].precursor)->second);
		
	sort(ticVector.begin(), ticVector.end());
	double medianTIC = Q2(ticVector)/1000;
	
	// MS1-3A: Ratio of 95th over 5th percentile of MS1 max intensities of identified peptides
	sort(identifiedPeptidePeaks.begin(), identifiedPeptidePeaks.end());
	int numPeaks = identifiedPeptidePeaks.size();
	double ninetyfifthPercPeak = identifiedPeptidePeaks[(int)(.95*numPeaks + .5)-1]; // 95th percentile peak
	double fifthPercPeak = identifiedPeptidePeaks[(int)(.05*numPeaks + .5)-1]; // 5th percentile peak
	double dynamicRangeOfPeptideSignals = ninetyfifthPercPeak/fifthPercPeak;

	// MS1-3B: Median MS1 peak height for identified peptides
	// the peaks have already been sorted by intensity in metric MS1-3A above
	double medianMS1Peak = Q2(identifiedPeptidePeaks);

	// MS1-5A: Median real value of precursor errors	
	double medianRealPrecursorError = MedianRealPrecursorError(dbFilename, sourceId);

	// MS1-5B: Mean of the absolute precursor errors
	double meanAbsolutePrecursorError = GetMeanAbsolutePrecursorErrors(dbFilename, sourceId);
	
	// MS1-5C: Median real value of precursor errors in ppm
	ppmStruct realPrecursorErrorPPM = GetRealPrecursorErrorPPM(dbFilename, sourceId);
	double medianRealPrecursorErrorPPM = realPrecursorErrorPPM.median;
	
	// MS1-5D: Interquartile range in ppm of the precursor errors
	// Uses variables from MS1-5C above
	double interquartileRealPrecursorErrorPPM = realPrecursorErrorPPM.interquartileRange;
	
	// MS2-1: Median MS2 ion injection time
	double medianInjectionTimeMS2 = 0;
	if (!ionInjectionTimeMS2.empty())
	{
		sort(ionInjectionTimeMS2.begin(), ionInjectionTimeMS2.end());
		medianInjectionTimeMS2 = Q2(ionInjectionTimeMS2);
	}

	// MS2-2: Median S/N ratio (max/median peak heights) for identified MS2 spectra
	sort(sigNoisMS2.begin(), sigNoisMS2.end());
	double medianSigNoisMS2 = Q2(sigNoisMS2);

	// MS2-3: Median number of peaks in an MS2 scan
	sort(MS2Peaks.begin(), MS2Peaks.end());
	int Q2(vector<int>);
	int medianNumMS2Peaks = Q2(MS2Peaks);

	// MS2-4A: Fraction of MS2 scans identified in the first quartile of peptides sorted by MS1 max intensity
	double idRatioQ1 = (double)idQ1/totalQ1;

	// MS2-4B: Fraction of MS2 scans identified in the second quartile of peptides sorted by MS1 max intensity
	double idRatioQ2 = (double)idQ2/totalQ2;
	
	// MS2-4C: Fraction of MS2 scans identified in the third quartile of peptides sorted by MS1 max intensity
	double idRatioQ3 = (double)idQ3/totalQ3;

	// MS2-4D: Fraction of MS2 scans identified in the fourth quartile of peptides sorted by MS1 max intensity
	double idRatioQ4 = (double)idQ4/totalQ4;
	
	// P-1: Median peptide ID score
	double medianIDScore = GetMedianIDScore(dbFilename, sourceId);
	
	// P-2A: Number of MS2 spectra identifying tryptic peptide ions
	int numTrypticMS2Spectra = GetNumTrypticMS2Spectra(dbFilename, sourceId);
	
	// P-2B: Number of tryptic peptide ions identified
	int numTrypticPeptides = GetNumTrypticPeptides(dbFilename, sourceId);
	
	// P-2C: Number of unique tryptic peptides
	int numUniqueTrypticPeptides = GetNumUniqueTrypticPeptides(dbFilename, sourceId);
	
	// P-3: Ratio of semi to fully tryptic peptides
	// uses variable from P-2C above
	int numUniqueSemiTrypticPeptides = GetNumUniqueSemiTrypticPeptides(dbFilename, sourceId);
	float ratioSemiToFullyTryptic = (float)numUniqueSemiTrypticPeptides/numUniqueTrypticPeptides;

	string emptyMetric = "NaN"; // NaN stands for Not a Number
	qout << sourceFilename << endl;
	qout << "\nMetrics:\n";
	qout << "--------\n";
	qout << "C-1A: Chromatographic peak tailing: " << peakTailing << "/" << numDuplicatePeptides << " = " << peakTailingRatio << endl;
	qout << "C-1B: Chromatographic bleeding: " << bleed << "/" << numDuplicatePeptides << " = " << bleedRatio << endl;
	qout << "C-2A: Time period over which middle 50% of peptides were identified: " << thirdQuartileIDTime << " sec - " << firstQuartileIDTime << " sec = " << iqIDTime/60 << " minutes.\n";
	qout << "C-2B: Peptide identification rate during the interquartile range: " << iqIDRate << " peptides/min.\n";
	qout << "C-3A: Median peak width for identified peptides: " << medianFwhm << " seconds.\n";
	qout << "C-3B: Interquartile peak width for identified peptides: " << fwhmQ3 << " - " << fwhmQ1 << " = "<< iqFwhm << " seconds.\n";
	qout << "C-4A: Median peak width for identified peptides in the last RT decile: " << medianFwhmLastRTDecile << " seconds.\n";
	qout << "C-4B: Median peak width for identified peptides in the first RT decile: " << medianFwhmFirstRTDecile << " seconds.\n";
	qout << "C-4C: Median peak width for identified peptides in median RT decile: " << medianFwhmByRT << " seconds.\n";
	qout << "DS-1A: Ratio of peptides identified once over those identified twice: " << identifiedOnce << "/" << identifiedTwice << " = " << DS1A << endl;
	qout << "DS-1B: Ratio of peptides identified twice over those identified thrice: " << identifiedTwice << "/" << identifiedThrice << " = " << DS1B << endl;
	qout << "DS-2A: Number of MS1 scans taken over the interquartile range: " << iqMS1Scans << " scans\n";
	qout << "DS-2B: Number of MS2 scans taken over the interquartile range: " << iqMS2Scans << " scans\n";
	qout << "DS-3A: MS1 peak intensity over MS1 sampled intensity at median sorted by max intensity: " << medianSamplingRatio << endl;
	qout << "DS-3B: MS1 peak intensity over MS1 sampled intensity at median sorted by max intensity of bottom 50% of MS1: " << bottomHalfSamplingRatio << endl;
	qout << "IS-1A: Number of big drops in total ion current value: " << ticDrop << endl; 
	qout << "IS-1B: Number of big jumps in total ion current value: " << ticJump << endl;
	qout << "IS-2: Median m/z value for all unique ions of identified peptides: " << medianPrecursorMZ << endl;
	qout << "IS-3A: +1 charge / +2 charge: " << charge1 << "/" << charge2 << " = " << IS3A << endl;
	qout << "IS-3B: +3 charge / +2 charge: " << charge3 << "/" << charge2 << " = " << IS3B << endl;
	qout << "IS-3C: +4 charge / +2 charge: " << charge4 << "/" << charge2 << " = " << IS3C << endl;
	if (ionInjectionTimeMS1.empty())
		qout << "MS1-1: Median MS1 ion injection time: " << emptyMetric << endl;
	else
		qout << "MS1-1: Median MS1 ion injection time: " << medianInjectionTimeMS1 << " ms\n";
	qout << "MS1-2A: Median signal-to-noise ratio (max/median peak height) for MS1 up to and including C-2A: " << medianSigNoisMS1 << endl;
	qout << "MS1-2B: Median TIC value of identified peptides before the third quartile: " << medianTIC << endl;
	qout << "MS1-3A: Ratio of 95th over 5th percentile MS1 max intensities of identified peptides: " << ninetyfifthPercPeak << "/" << fifthPercPeak << " = " << dynamicRangeOfPeptideSignals << endl;
	qout << "MS1-3B: Median maximum MS1 value for identified peptides: " << medianMS1Peak << endl;
	qout << "MS1-5A: Median real value of precursor errors: " << medianRealPrecursorError << endl;
	qout << "MS1-5B: Mean of the absolute precursor errors: " << meanAbsolutePrecursorError << endl;
	qout << "MS1-5C: Median real value of precursor errors in ppm: " << medianRealPrecursorErrorPPM << endl;
	qout << "MS1-5D: Interquartile range in ppm of the precursor errors: " << interquartileRealPrecursorErrorPPM << endl;
	if (ionInjectionTimeMS2.empty())
		qout << "MS2-1: Median MS2 ion injection time: " << emptyMetric << endl;
	else
		qout << "MS2-1: Median MS2 ion injection time: " << medianInjectionTimeMS2 << " ms\n";
	qout << "MS2-2: Median S/N ratio (max/median peak height) for identified MS2 spectra: " << medianSigNoisMS2 << endl;
	qout << "MS2-3: Median number of peaks in an MS2 scan: " << medianNumMS2Peaks << endl;
	qout << "MS2-4A: Fraction of MS2 scans identified in the first quartile of peptides sorted by MS1 max intensity: " << idQ1 << "/" << totalQ1 << " = " << idRatioQ1 << endl;
	qout << "MS2-4B: Fraction of MS2 scans identified in the second quartile of peptides sorted by MS1 max intensity: " << idQ2 << "/" << totalQ2 << " = " << idRatioQ2 << endl;
	qout << "MS2-4C: Fraction of MS2 scans identified in the third quartile of peptides sorted by MS1 max intensity: " << idQ3 << "/" << totalQ3 << " = " << idRatioQ3 << endl;
	qout << "MS2-4D: Fraction of MS2 scans identified in the fourth quartile of peptides sorted by MS1 max intensity: " << idQ4 << "/" << totalQ4 << " = " << idRatioQ4 << endl;
	qout << "P-1: Median peptide identification score: " << medianIDScore << endl;
	qout << "P-2A: Number of MS2 spectra identifying tryptic peptide ions: " << numTrypticMS2Spectra << endl;
	qout << "P-2B: Number of tryptic peptide ions identified: " << numTrypticPeptides << endl;
	qout << "P-2C: Number of unique tryptic peptide sequences identified: " << numUniqueTrypticPeptides << endl;
	qout << "P-3: Ratio of semi/fully tryptic peptides: " << numUniqueSemiTrypticPeptides << "/" << numUniqueTrypticPeptides << " = " << ratioSemiToFullyTryptic << endl;
	
	qout << "\nNot metrics:\n";
	qout << "------------\n";
	if (!ionInjectionTimeMS1.empty() && !ionInjectionTimeMS2.empty()) {
		qout << "MS1 mean ion injection time: " << (ionInjectionTimeMS1[0] + ionInjectionTimeMS1[ionInjectionTimeMS1.size()-1])/2 << endl;
		qout << "MS2 mean ion injection time: " << (ionInjectionTimeMS2[0] + ionInjectionTimeMS2[ionInjectionTimeMS1.size()-1])/2 << endl;
	}
	qout << "Total number of MS1 scans: " << MS1Count << endl;
	qout << "Total number of MS2 scans: " << MS2Count << endl << endl;


/*notab	// Tab delimited output: metrics in one row
	cout << sourceFilename;
	cout << "\t" << peakTailingRatio << "\t" << bleedRatio << "\t" << iqIDTime << "\t" << iqIDRate << "\t" << DS1A << "\t" << DS1B << "\t" << iqMS1Scans << "\t" << iqMS2Scans;
	cout << "\t" << ticDrop << "\t" << ticJump << "\t" << medianPrecursorMZ << "\t" << IS3A << "\t" << IS3B << "\t" << IS3C;
	if (ionInjectionTimeMS1.empty())
		cout << "\t" << emptyMetric;
	else
		cout << "\t" << medianInjectionTimeMS1;
	cout << "\t" << medianTIC << "\t" << medianRealPrecursorError << "\t" << meanAbsolutePrecursorError << "\t" << medianRealPrecursorErrorPPM << "\t" << interquartileRealPrecursorErrorPPM;
	if (ionInjectionTimeMS2.empty())
		cout << "\t" << emptyMetric;
	else
		cout << "\t" << medianInjectionTimeMS2;
	cout << "\t" << medianNumMS2Peaks << "\t" << medianIDScore << "\t" << numTrypticMS2Spectra << "\t" << numTrypticPeptides;
	cout << "\t" << numUniqueTrypticPeptides << "\t" << ratioSemiToFullyTryptic << endl;
*/
	qout.close();
	cout << sourceFilename << " took " << t.elapsed() << " seconds to analyze.\n";
	}
	catch (exception& e) {
        cerr << "Exception in MetricMaster thread: " << e.what() << endl;
    }
	catch (...) {
        cerr << "Unhandled exception in MetricMaster thread." << endl;
        exit(1); // fear the unknown!
    }
	return;
}

/**
  * Return the first quartile of a vector<double>
  */
double Q1(vector<double> dataSet) {
	double quartile1;
	int dataSize = (int)dataSet.size();
	if ( dataSize % 4 == 0 ) {
		int index1 = (dataSize/4)-1;
		int index2 = (dataSize/4);
		quartile1 = (dataSet[index1] + dataSet[index2]) / 2;
	}
	else {
		int index1 = (dataSize)/4;
		quartile1 = dataSet[index1];
	}
	return quartile1;
}

/**
  * Return the second quartile (aka median) of a vector<double>
  */
double Q2(vector<double> dataSet) {
	double quartile2;
	int dataSize = (int)dataSet.size();
	if ( dataSize % 2 == 0 ) {
		int index1 = (dataSize/2)-1;
		int index2 = (dataSize/2);
		quartile2 = (dataSet[index1] + dataSet[index2]) / 2;
	}
	else {
		int index1 = (dataSize)/2;
		quartile2 = dataSet[index1];
	}
	return quartile2;
}

/**
  * Overload second quartile function to int
  */
int Q2(vector<int> dataSet) {
	int quartile2;
	int dataSize = (int)dataSet.size();
	if ( dataSize % 2 == 0 ) {
		int index1 = (dataSize/2)-1;
		int index2 = (dataSize/2);
		quartile2 = (dataSet[index1] + dataSet[index2]) / 2;
	}
	else {
		int index1 = (dataSize)/2;
		quartile2 = dataSet[index1];
	}
	return quartile2;
}

/**
  * Return the third quartile of a vector<double>
  */
double Q3(vector<double> dataSet) {
	double quartile3;
	int dataSize = (int)dataSet.size();
	if ( (dataSize * 3) % 4 == 0 ) {
		int index1 = (3*dataSize/4)-1;
		int index2 = (3*dataSize/4);
		quartile3 = (dataSet[index1] + dataSet[index2]) / 2;
	}
	else {
		int index1 = (3*dataSize)/4;
		quartile3 = dataSet[index1];
	}
	return quartile3;
}

/**
  * Given an idpDB file, return its RAW/mzML/etc source files.
  * Add: also accept filenames of interest (e.g. ignore all source files except -these-)
  * Add: also accept an extension type (e.g. use the .RAW source file instead of .mzML)
  */
vector<sourceFile> GetSpectraSources(const string& dbFilename) {
	sqlite::database db(dbFilename);
	string s = "select Id, Name from SpectrumSource";
	sqlite::query qry(db, s.c_str() );
	vector<sourceFile> sources;

	for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
		sourceFile tmpFile;
		int tmpId;
		string tmpFilename;
		
		(*i).getter() >> tmpId >> tmpFilename;
		
		stringstream sourceId;
		sourceId << tmpId; // convert int to string

		path p = dbFilename;
		tmpFilename = p.parent_path().string() + "/" + tmpFilename + ".mzML";
		ifstream checkFile(tmpFilename.c_str());
		ifstream checkFile2(boost::filesystem::change_extension(tmpFilename, ".mzXML").string().c_str());
		ifstream checkFile3(boost::filesystem::change_extension(tmpFilename, ".RAW").string().c_str());
		
		if (checkFile)
			tmpFilename = boost::filesystem::change_extension(tmpFilename, ".mzML").string();
		else if (checkFile2)
			tmpFilename = boost::filesystem::change_extension(tmpFilename, ".mzXML").string();
		else if (checkFile3) 
			tmpFilename = boost::filesystem::change_extension(tmpFilename, ".RAW").string();
		else {
			cout << "Cannot find file " << boost::filesystem::change_extension(tmpFilename, "").string() << " with any known extensions.\n";
			continue; // if this source file doesn't exist we can't do anything, so let's try for the next source file
		}

		tmpFile.filename = tmpFilename;
		tmpFile.id = sourceId.str();
		tmpFile.dbFilename = dbFilename;
		sources.push_back(tmpFile);
		cout << "db: " << tmpFile.dbFilename << "\tid: " << tmpFile.id << "\tsource: " << tmpFile.filename << endl;
	}
	cout << endl;
	return sources;	
}

/**
  * Query the idpDB and return with a list of MS2 native IDs for all identified peptides
  *
  * SELECT DISTINCT NativeID 
  * FROM PeptideSpectrumMatch JOIN Spectrum 
  * WHERE PeptideSpectrumMatch.QValue <= 0.05 
  * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  * AND Rank = 1 
  * AND Spectrum.Source
  * ORDER BY Spectrum.Id
  */
vector<string> GetNativeId(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
  * For metric DS-1A: Finds the number of peptides identified by one spectrum
  *
  * SELECT COUNT(*) 
  * FROM PeptideSpectrumMatch 
  * WHERE Peptide 
  * IN (SELECT Peptide 
  *		FROM PeptideSpectrumMatch JOIN Spectrum 
  *		WHERE PeptideSpectrumMatch.QValue <= 0.05 
  *		AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  *		AND Rank = 1 
  *		GROUP BY Peptide HAVING COUNT(Peptide)=1 
  *		ORDER BY COUNT(Peptide))
  */
int PeptidesIdentifiedOnce(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
  * SELECT COUNT(*)/2
  * FROM PeptideSpectrumMatch 
  * WHERE Peptide 
  * IN (SELECT Peptide 
  *		FROM PeptideSpectrumMatch JOIN Spectrum 
  *		WHERE PeptideSpectrumMatch.QValue <= 0.05 
  *		AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  *		AND Rank = 1 
  *		GROUP BY Peptide HAVING COUNT(Peptide)=2 
  *		ORDER BY COUNT(Peptide))
  */
int PeptidesIdentifiedTwice(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
  * SELECT COUNT(*)/3
  * FROM PeptideSpectrumMatch 
  * WHERE Peptide IN 
  *		(SELECT Peptide 
  *		 FROM PeptideSpectrumMatch JOIN Spectrum 
  *		 WHERE PeptideSpectrumMatch.QValue <= 0.05 
  *		 AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  *		 AND Rank = 1 
  *		 GROUP BY Peptide HAVING COUNT(Peptide)=3 
  *		 ORDER BY COUNT(Peptide))
  */
int PeptidesIdentifiedThrice(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
  * SELECT DISTINCT NativeID, PrecursorMZ 
  * FROM PeptideSpectrumMatch JOIN Spectrum 
  * WHERE PeptideSpectrumMatch.QValue <= 0.05 
  * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  * AND Rank = 1 
  * ORDER BY PrecursorMZ
  */
double MedianPrecursorMZ(const string& dbFilename, const string& spectrumSourceId) {
	
	sqlite::database db(dbFilename);
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
  * Finds duplicate peptide IDs. Used in metrics C-1A and C-1B.
  *
  * SELECT distinct NativeID,Peptide 
  * FROM PeptideSpectrumMatch JOIN Spectrum 
  * WHERE PeptideSpectrumMatch.QValue <= 0.05 
  * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  * AND Rank = 1 
  * AND Peptide IN
  * 		(SELECT Peptide 
  *		 FROM PeptideSpectrumMatch JOIN Spectrum 
  *		 WHERE PeptideSpectrumMatch.QValue <= 0.05 
  *		 AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  *		 AND Rank = 1 
  *		 GROUP BY Peptide 
  *		 HAVING COUNT(Peptide) > 1 
  *		 ORDER BY NativeID) 
  * ORDER BY Peptide
  */
multimap<int, string> GetDuplicateID(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
  * SELECT DISTINCT Peptide,Charge 
  * FROM PeptideSpectrumMatch JOIN Spectrum 
  * WHERE PeptideSpectrumMatch.QValue <= 0.05 
  * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  * AND Rank = 1 
  * ORDER BY Peptide
  */
fourInts PeptideCharge(const string& dbFilename, const string& spectrumSourceId) {
	
	sqlite::database db(dbFilename);
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
  * For metric MS1-5A: Find the median real value of precursor errors
  *
  * SELECT DISTINCT NativeID,Peptide,PrecursorMZ,MonoisotopicMassError,MolecularWeightError 
  * FROM PeptideSpectrumMatch JOIN Spectrum 
  * WHERE PeptideSpectrumMatch.QValue <= 0.05 
  * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
  * AND Rank = 1 
  * AND Charge=2 
  * ORDER BY Spectrum
  */
double MedianRealPrecursorError(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
 * SELECT DISTINCT NativeID,Peptide,PrecursorMZ,MonoisotopicMassError,MolecularWeightError 
 * FROM PeptideSpectrumMatch JOIN Spectrum 
 * WHERE PeptideSpectrumMatch.QValue <= 0.05 
 * AND PeptideSpectrumMatch.Spectrum=Spectrum.Id 
 * AND Rank = 1 
 * AND Charge=2 
 * ORDER BY Spectrum
 */
double GetMeanAbsolutePrecursorErrors(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
 */
ppmStruct GetRealPrecursorErrorPPM(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
	
	ppmStruct returnMe;
	
	// Find the median for metric MS1-5C
	returnMe.median = Q2(realPrecursorErrorsPPM);

	// The interquartile range is the distance between Q1 and Q3
	returnMe.interquartileRange = Q3(realPrecursorErrorsPPM) - Q1(realPrecursorErrorsPPM);
	
	return returnMe;
}

/**
 * For metric P-1: Find the median peptide identification score for all peptides
 *
 * SELECT DISTINCT PeptideSpectrumMatch.Peptide, PeptideSpectrumMatchScore.Value 
 * FROM PeptideSpectrumMatch JOIN PeptideSpectrumMatchScore JOIN spectrum 
 * WHERE PeptideSpectrumMatch.QValue <= 0.05 
 * AND PeptideSpectrumMatch.Spectrum = Spectrum.Id 
 * AND PeptideSpectrumMatch.Id = PeptideSpectrumMatchScore.PsmId 
 * AND PeptideSpectrumMatchScore.ScoreNameId = 3 
 * AND Rank = 1 
 * ORDER BY PeptideSpectrumMatchScore.Value
 */
double GetMedianIDScore(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
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
 * SELECT COUNT(distinct nativeid) 
 * FROM PeptideInstance JOIN PeptideSpectrumMatch JOIN Spectrum 
 * WHERE PeptideInstance.Peptide = PeptideSpectrumMatch.Peptide 
 * AND PeptideSpectrumMatch.QValue <= 0.05 
 * AND PeptidespectrumMatch.Spectrum = Spectrum.Id 
 * AND Rank = 1 
 * AND Spectrum.Source = " + SpectrumSourceId + " 
 * AND NTerminusIsSpecific = 1 
 * AND CTerminusIsSpecific = 1 
 * ORDER BY Spectrum.Id
 */
int GetNumTrypticMS2Spectra(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
	string s = "SELECT COUNT(distinct nativeid) FROM PeptideInstance JOIN PeptideSpectrumMatch JOIN Spectrum WHERE PeptideInstance.peptide = PeptideSpectrumMatch.peptide AND PeptideSpectrumMatch.QValue <= 0.05 and peptidespectrummatch.spectrum = spectrum.id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and NTerminusIsSpecific = 1 and CTerminusIsSpecific = 1 order by spectrum.id";
	sqlite::query qry(db, s.c_str() );
	int trypticMS2Spectra;

	for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
		(*i).getter() >> trypticMS2Spectra;
	}

	return (int)trypticMS2Spectra;
}

// For metric P-2B
int GetNumTrypticPeptides(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
	string s = "select distinct PeptideInstance.Peptide, charge, modification from PeptideInstance join Spectrum join PeptideSpectrumMatch join PeptideModification where PeptideSpectrumMatch.QValue <= 0.05 and PeptideInstance.peptide = PeptideSpectrumMatch.peptide and PeptideModification.PeptideSpectrumMatch = PeptideSpectrumMatch.id and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id and Spectrum.Source = " + spectrumSourceId + " and NTerminusIsSpecific = 1 and CTerminusIsSpecific = 1";
	sqlite::query qry(db, s.c_str() );
	int trypticPeptides = 0;
	
	for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
		trypticPeptides++;
	} 

	return trypticPeptides;
}

// For metric P-2C
int GetNumUniqueTrypticPeptides(const string& dbFilename, const string& spectrumSourceId)
{

	sqlite::database db(dbFilename);
	string s = "select count(distinct PeptideInstance.Peptide) from PeptideInstance join Spectrum join PeptideSpectrumMatch join PeptideModification where PeptideSpectrumMatch.QValue <= 0.05 and PeptideInstance.peptide = PeptideSpectrumMatch.peptide and PeptideModification.PeptideSpectrumMatch = PeptideSpectrumMatch.id and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id and Spectrum.Source = " + spectrumSourceId + " and NTerminusIsSpecific = 1 and CTerminusIsSpecific = 1";
	sqlite::query qry(db, s.c_str() );
	int uniqueTrypticPeptides;
	
	for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
		(*i).getter() >> uniqueTrypticPeptides;
	} 

	return uniqueTrypticPeptides;
}

// For metric P-3
int GetNumUniqueSemiTrypticPeptides(const string& dbFilename, const string& spectrumSourceId) {

	sqlite::database db(dbFilename);
	string s = "select count(distinct PeptideInstance.Peptide) from PeptideInstance join Spectrum join PeptideSpectrumMatch join PeptideModification where PeptideSpectrumMatch.QValue <= 0.05 and PeptideInstance.peptide = PeptideSpectrumMatch.peptide and PeptideModification.PeptideSpectrumMatch = PeptideSpectrumMatch.id and Rank = 1 and PeptideSpectrumMatch.Spectrum = Spectrum.Id and Spectrum.Source = " + spectrumSourceId + " and ((NTerminusIsSpecific = 1 and CTerminusIsSpecific = 0) or (NTerminusIsSpecific = 0 and CTerminusIsSpecific = 1))";
	sqlite::query qry(db, s.c_str() );
	int uniqueSemiTrypticPeptides;
	
	for (sqlite::query::iterator i = qry.begin(); i != qry.end(); ++i) {
		(*i).getter() >> uniqueSemiTrypticPeptides;
	} 

	return uniqueSemiTrypticPeptides;
}

// Used for peak finding of identified peptides
vector<windowData> MZRTWindows(const string& dbFilename, const string& spectrumSourceId, map<string, int> nativeToArrayMap, vector<ms_amalgam> scanInfo) {

	sqlite::database db(dbFilename);
	string s = "select distinct peptide, nativeID, precursorMZ, IFNULL(GROUP_CONCAT(modification || \"@\" || offset), '') from spectrum join peptidespectrummatch psm left join peptidemodification pm on pm.peptidespectrummatch = psm.id where psm.spectrum = Spectrum.Id and Rank = 1 and Spectrum.Source = " + spectrumSourceId + " and qvalue < 0.05 group by psm.id order by peptide, modification, offset";
	sqlite::query qry(db, s.c_str() );

	vector<windowData> pepWin;
	windowData tmpWindow;
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

int main( int argc, char* argv[] ) {
	if (argc == 1) {
        cerr << "Usage: quameter <idpDB file>" << endl;
        return 1;
    }

/*notab	// Tab delimited output: header
	cout << "Filename\tC-1A\tC-1B\tC-2A\tC-2B\tDS-1A\tDS-1B\tDS-2A\tDS-2B";
	cout << "\tIS-1A\tIS1-B\tIS-2\tIS-3A\tIS-3B\tIS-3C";
	cout << "\tMS1-1\tMS1-2B\tMS1-5A\tMS1-5B\tMS1-5C\tMS1-5D\tMS2-1";
	cout << "\tMS2-3\tP-1\tP-2A\tP-2B";
	cout << "\tP-2C\tP-3" << endl;
*/	

	vector<sourceFile> allSources;
	for (int j=1; j < argc; j++) {
		// Queries the idpDB file for source filenames and their database ID number
		try {
			vector<sourceFile> spectraSources = GetSpectraSources(argv[j]);
			allSources.insert(allSources.end(), spectraSources.begin(), spectraSources.end());
		}
		catch (exception& e) {
			cerr << "Exception in GetSpectraSources: " << e.what() << endl;
		}
		catch (...) {
			cerr << "Unhandled exception in GetSpectraSources." << endl;
			exit(1);
		}
	}
	
	int numFiles = allSources.size();
	int current = 0;		
	int maxThreads = boost::thread::hardware_concurrency(); 
	if (maxThreads < 1) maxThreads = 1; // there must be at least one thread

	for (int k = 0; k < max(1,( ((numFiles-1)/maxThreads)+1 )); k++) { // at least go through this loop once.
		boost::thread_group threadGroup;
		for (int l = 0; (l < maxThreads) && (current < numFiles); l++) {
			cout << "current: " << current << "\tdb: " << allSources[current].dbFilename << "\tid: " << allSources[current].id << "\tfile: " << allSources[current].filename << endl;
			threadGroup.add_thread(new boost::thread(MetricMaster, allSources[current].dbFilename, allSources[current].filename, allSources[current].id));
			current++;
		}
		threadGroup.join_all();
	}
	
	return 0;
}
