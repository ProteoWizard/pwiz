#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/IO.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/Version.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/analysis/Version.hpp"
#include "boost/program_options.hpp"
#include "boost/foreach.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"
#include <iostream>
#include <fstream>
#include <iterator>


using namespace std;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;
using boost::shared_ptr;

struct ChromatogramSpec {
    double mzMin;
    double mzMax;
};

struct ChromatogramSetSpec {
    string name;
    double minTime;
    double maxTime;
    vector<ChromatogramSpec> chromatogramSpecs;
    shared_ptr<ostream> postream;
};
size_t FindScanIndex(vector<TimeIntensityPair> &timeIntensityPairs, double time);
void GenerateChromatograms(MSDataFile &msDataFile, vector<ChromatogramSetSpec> &chromatogramSpecs);
void GetCentroidedSpectrum(SpectrumPtr spectrumPtr, vector<MZIntensityPair> &centroidedMzIntensityPairs);
vector<MZIntensityPair> GetChromatogramPoint(const vector<MZIntensityPair> mzIntensityPairs, double minMz, double maxMz);
boost::shared_ptr<FullReaderList> readerList;
void initializeReaderList()
{
    if (!readerList.get())
        readerList.reset(new FullReaderList);
}



int main(int argc, char **argv) {
	try {
		if (argc == 1 || argc > 3) {
			fprintf(stderr, "Usage: %s rawfile [chromatogramsfile]", argv[0]);
			return 1;
		}
	    ifstream ifstreamChromatograms;
		if (argc == 3) {
	        ifstreamChromatograms.open(argv[2]);
		}
	    vector<ChromatogramSetSpec> chromatogramSets;
		while(true) {
			string line;
			if (ifstreamChromatograms.is_open()) {
				while (!ifstreamChromatograms.eof() && line.length() == 0) {
					getline(ifstreamChromatograms, line);
				}
			} else {
				while (!cin.eof() && line.length() == 0) {
					getline(cin, line);
				}
			}
			if (line.length() == 0) {
				break;
			}
		    ChromatogramSetSpec chromatogramSetSpec;
		    string::size_type nextPos = line.find('\t');
		    chromatogramSetSpec.name = line.substr(0, nextPos);
		    string::size_type pos = nextPos + 1;
	        nextPos = line.find('\t', pos);
		    chromatogramSetSpec.minTime = atof(line.substr(pos, nextPos - pos).c_str());
		    pos = nextPos + 1;
		    nextPos = line.find('\t', pos);
		    chromatogramSetSpec.maxTime = atof(line.substr(pos, nextPos - pos).c_str());
		    while (true) {
		        pos = nextPos + 1;
		        nextPos = line.find('\t', pos);
		        ChromatogramSpec chromatogramSpec;
		        chromatogramSpec.mzMin = atof(line.substr(pos, nextPos - pos).c_str());
		        pos = nextPos + 1;
		        nextPos = line.find('\t', pos);
		        if (nextPos == string.npos) {
		            break;
		        }
		        chromatogramSpec.mzMax = atof(line.substr(pos, nextPos - pos).c_str());
		        chromatogramSetSpec.chromatogramSpecs.push_back(chromatogramSpec);
		    }
		    chromatogramSets.push_back(chromatogramSetSpec);
		}
		
		initializeReaderList();
		MSDataFile msDataFile(argv[1], (Reader *) readerList.get());
		GenerateChromatograms(msDataFile, chromatogramSets);
		return 0;
	} catch (exception& e)    {
        cerr << e.what() << endl;
    } catch (...) {
        cerr << "[" << argv[0] << "] Caught unknown exception.\n";
    }
    return 1;
}

void GenerateChromatograms(MSDataFile &msDataFile, vector<ChromatogramSetSpec> &chromatogramSpecs) {
    vector<ChromatogramSetSpec *> remainingChromatograms;
    SpectrumListPtr spectrumListPtr = msDataFile.run.spectrumListPtr;
    size_t totalScanCount = spectrumListPtr->size();
    ChromatogramPtr chromatogramPtr = msDataFile.run.chromatogramListPtr->chromatogram(0, true);
    vector<TimeIntensityPair> timeIntensityPairs;
    chromatogramPtr->getTimeIntensityPairs(timeIntensityPairs);
    double minTime = timeIntensityPairs.back().time;
    double maxTime = timeIntensityPairs.front().time;
    for (size_t i = 0; i < chromatogramSpecs.size(); i++) {
        minTime = min(minTime, chromatogramSpecs[i].minTime);
        maxTime = max(maxTime, chromatogramSpecs[i].maxTime);
        remainingChromatograms.push_back(&chromatogramSpecs[i]);
    }
    size_t firstScan = FindScanIndex(timeIntensityPairs, minTime);
    for (size_t iScan = firstScan; iScan < totalScanCount && remainingChromatograms.size() > 0; iScan++) {
        vector<ChromatogramSetSpec *> activeChromatograms;
        double time = timeIntensityPairs[iScan].time;
        double nextTime = FLT_MAX;
        if (spectrumListPtr->spectrum(iScan, false)->cvParam(MS_ms_level).valueAs<int>() != 1) {
            continue;
        }
        for (int i = remainingChromatograms.size() - 1; i >= 0; i--) {
            nextTime = min(nextTime, remainingChromatograms[i]->minTime);
			if (remainingChromatograms[i]->maxTime <= time) {
				remainingChromatograms.erase(remainingChromatograms.begin() + i);
			} else if (remainingChromatograms[i]->minTime <= time) {
                activeChromatograms.push_back(remainingChromatograms[i]);
            }
		}
        if (activeChromatograms.size() == 0) {
            size_t nextScan = FindScanIndex(timeIntensityPairs, nextTime);
            iScan = max(iScan, nextScan - 1);
            continue;
		}
        SpectrumPtr spectrum = spectrumListPtr->spectrum(iScan, true);
        vector<MZIntensityPair> mzIntensityPairs;
        GetCentroidedSpectrum(spectrum, mzIntensityPairs);
        for (size_t iChromatogramSet = 0; iChromatogramSet < activeChromatograms.size(); iChromatogramSet++) {
            ChromatogramSetSpec &chromatogramSetSpec = *activeChromatograms[iChromatogramSet];
            if (!chromatogramSetSpec.postream) {
                chromatogramSetSpec.postream = shared_ptr<ostream>(new ofstream(chromatogramSetSpec.name.c_str()));
            }
            *chromatogramSetSpec.postream << iScan << " " << time;
            for (size_t iChromatogram = 0; iChromatogram < chromatogramSetSpec.chromatogramSpecs.size(); iChromatogram++) {
                ChromatogramSpec chromatogramSpec = chromatogramSetSpec.chromatogramSpecs[iChromatogram];
                vector<MZIntensityPair> point = GetChromatogramPoint(mzIntensityPairs, chromatogramSpec.mzMin, chromatogramSpec.mzMax);
                *chromatogramSetSpec.postream << " " << point.size();
                for (size_t iPair = 0; iPair < point.size(); iPair++) {
                    MZIntensityPair mzIntensityPair = point[iPair];
                    *chromatogramSetSpec.postream << " " << mzIntensityPair.mz << " " << mzIntensityPair.intensity;
                }
            }
            *chromatogramSetSpec.postream << "\n";
        }
    }
}

bool CompareTime(const TimeIntensityPair &timeIntensityPair1, const TimeIntensityPair &timeIntensityPair2) {
    return timeIntensityPair1.time < timeIntensityPair2.time;
}
bool CompareMz(const MZIntensityPair &mzIntensityPair1, const MZIntensityPair& mzIntensityPair2) {
    return mzIntensityPair1.mz < mzIntensityPair2.mz;
}
size_t FindScanIndex(vector<TimeIntensityPair> &timeIntensityPairs, double time) {
    vector<TimeIntensityPair>::iterator low = lower_bound(timeIntensityPairs.begin(), timeIntensityPairs.end(), TimeIntensityPair(time,0), CompareTime);
    size_t result = low - timeIntensityPairs.begin();
    result = min(result, timeIntensityPairs.size() - 1);
    return result;
}

void GetCentroidedSpectrum(SpectrumPtr spectrumPtr, vector<MZIntensityPair> &centroidedMzIntensityPairs) {
    if (spectrumPtr->hasCVParam(MS_centroid_spectrum)) {
        spectrumPtr->getMZIntensityPairs(centroidedMzIntensityPairs);
        return;
    }
    vector<MZIntensityPair> mzIntensityPairs;
    spectrumPtr->getMZIntensityPairs(mzIntensityPairs);
    bool increasing = true;
    double currentMz = 0;
    double currentIntensity = 0;
    double lastIntensity = 0;
    for (size_t i = 0; i < mzIntensityPairs.size(); i++)
    {
        double intensity = mzIntensityPairs[i].intensity;
        if (intensity < lastIntensity)
        {
            increasing = false;
        }
        else
        {
            if (!increasing)
            {
                if (currentIntensity > 0)
                {
                    centroidedMzIntensityPairs.push_back(MZIntensityPair(currentMz, currentIntensity));
                }
                currentIntensity = 0;
            }
            increasing = true;
        }
        double nextIntensity = currentIntensity + intensity;
        if (nextIntensity > 0)
        {
            double mz = mzIntensityPairs[i].mz;
            currentMz = (currentMz * currentIntensity + mz * intensity) / nextIntensity;
            currentIntensity = nextIntensity;
        }
        lastIntensity = intensity;
    }
    if (currentIntensity > 0)
    {
        centroidedMzIntensityPairs.push_back(MZIntensityPair(currentMz, currentIntensity));
    }
}

size_t ClosestIndex(const vector<MZIntensityPair> mzIntensityPairs, double mz) {
    size_t index = lower_bound(mzIntensityPairs.begin(), mzIntensityPairs.end(), MZIntensityPair(mz, 0), CompareMz) - mzIntensityPairs.begin();
    if (index >= mzIntensityPairs.size()) {
        index--;
    }
    if (index > 0 && mz - mzIntensityPairs[index -1].mz < mzIntensityPairs[index].mz - mz) {
        index--;
    }
    return index;
}

vector<MZIntensityPair> GetChromatogramPoint(const vector<MZIntensityPair> mzIntensityPairs, double minMz, double maxMz) {
    vector<MZIntensityPair> result;
    if (mzIntensityPairs.size() == 0) {
        return result;
    }
    size_t iMin = ClosestIndex(mzIntensityPairs, minMz);
    size_t iMax = ClosestIndex(mzIntensityPairs, maxMz);
    for (size_t i = iMin; i <= iMax; i++) {
        result.push_back(mzIntensityPairs[i]);
    }
    return result;
}



