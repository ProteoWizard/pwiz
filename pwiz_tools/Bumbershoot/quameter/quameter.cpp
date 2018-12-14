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
// Contributor(s): Surendra Dasari
//

#include "quameter.h"
#include "quameterVersion.hpp"
#include "boost/lockfree/queue.hpp"
#include <boost/foreach_field.hpp>
#include <boost/math/distributions/normal.hpp>
#include <boost/range/algorithm/lower_bound.hpp>
#include <boost/range/algorithm/upper_bound.hpp>
#include "Interpolator.hpp"


namespace freicore
{
namespace quameter
{
    typedef boost::lockfree::queue<size_t, boost::lockfree::capacity<65534> > BoostLockFreeQueue;
    RunTimeConfig*                  g_rtConfig;
    boost::mutex                    msdMutex;
    BoostLockFreeQueue              metricsTasks;
    vector<QuameterInput>           allSources;

    void simulateGaussianPeak(double peakStart, double peakEnd,
                              double peakHeight, double peakBaseline,
                              double mean, double stddev,
                              size_t samples,
                              pwiz::util::BinaryData<double>& x, pwiz::util::BinaryData<double>& y)
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

    void writeChromatograms(const string& sourceFilename,
                            const MS2ScanMap& ms2ScanMap,
                            const XICWindowList& pepWindow,
                            const vector<UnidentifiedPrecursorInfo>& unidentifiedPrecursors)
    {
        MSData chromData;
        shared_ptr<ChromatogramListSimple> chromatogramListSimple(new ChromatogramListSimple);
        chromData.run.chromatogramListPtr = chromatogramListSimple;
        chromData.run.id = bfs::basename(sourceFilename);

        // if available, read NIST peaks;
        // input format is one line per peak: <exactMz> <peakTime> <peakHeight> <peakWidth> <peakFWHM>
        map<double, Peak> nistPeakByExactMz;
        if (bfs::exists("nist-peaks.txt"))
        {
            ifstream is("nist-peaks.txt");
            double exactMz, peakWidth;
            while (is >> exactMz)
            {
                Peak& peak = nistPeakByExactMz[exactMz];
                is >> peak.peakTime >> peak.intensity >> peakWidth >> peak.fwhm;
                peak.startTime = peak.peakTime - peakWidth/2;
                peak.endTime = peak.peakTime + peakWidth/2;
            }
        }

        // Put unique identified peptide chromatograms first in the file
        BOOST_FOREACH(const XICWindow& window, pepWindow)
        {
            chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
            Chromatogram& c = *chromatogramListSimple->chromatograms.back();
            c.index = chromatogramListSimple->size()-1;
            ostringstream oss;
            oss << "distinct match " << window.peptide
                << " (id: " << window.PSMs[0].peptide
                << "; m/z: " << window.preMZ
                << "; time: " << window.preRT << ")";
            c.id = "Raw SIC for " + oss.str();
            c.set(MS_SIC_chromatogram);
            c.setTimeIntensityArrays(window.MS1RT, window.MS1Intensity, UO_second, MS_number_of_detector_counts);

            // interpolated raw
            /*{
                interpolate(window.MS1RT, window.MS1Intensity);
                chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& c = *chromatogramListSimple->chromatograms.back();
                c.index = chromatogramListSimple->size()-1;
                ostringstream oss;
                oss << "distinct match " << window.peptide
                    << " (id: " << window.PSMs[0].peptide
                    << "; m/z: " << window.preMZ
                    << "; time: " << window.preRT << ")";
                c.id = "Raw interpolated SIC for " + oss.str();
                c.set(MS_SIC_chromatogram);
                c.setTimeIntensityArrays(window.MS1RT, window.MS1Intensity, UO_second, MS_number_of_detector_counts);
            }*/

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
            if (tmp.size() != newRT.size())
                cerr << "Warning: smoothed intensities vector has different size than MS1RT: " << tmp.size() << " vs. " << newRT.size() << endl;
            else
                c3.setTimeIntensityArrays(newRT, vector<double>(tmp.begin(), tmp.end()), UO_second, MS_number_of_detector_counts);

            float baselineIntensity = crawdadPeakFinder.getBaselineIntensity();
            double scaleForMS2Score = baselineIntensity;

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

                    if (peak.startTime < window.maxScoreScanStartTime && window.maxScoreScanStartTime < peak.endTime)
                        //scaleForMS2Score = (double) peak->getHeight();
                        scaleForMS2Score = ms2ScanMap.get<time>().find(window.maxScoreScanStartTime)->precursorIntensity;
                    else if (window.bestPeak && peak.peakTime == window.bestPeak->peakTime)
                        scaleForMS2Score = window.bestPeak->intensity;
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
            }

            // if available, make a chromatogram for NIST peaks
            {
                map<double, Peak>::const_iterator itr = nistPeakByExactMz.find(round(window.PSMs[0].exactMZ, 4));
                if (itr != nistPeakByExactMz.end())
                {
                    chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                    Chromatogram& c2 = *chromatogramListSimple->chromatograms.back();
                    c2.index = chromatogramListSimple->size()-1;
                    c2.id = "NIST peak for " + oss.str();
                    c2.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);

                    const Peak& peak = itr->second;
                    simulateGaussianPeak(peak.startTime, peak.endTime,
                                         peak.intensity, baselineIntensity,
                                         peak.peakTime, peak.fwhm / 2.35482,
                                         50,
                                         c2.getTimeArray()->data, c2.getIntensityArray()->data);
                }
            }

            // output MS2 times as chromatogram spikes
            {
                chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& ms2Chromatogram = *chromatogramListSimple->chromatograms.back();
                ms2Chromatogram.index = chromatogramListSimple->size()-1;
                ms2Chromatogram.id = "Identified MS2s for " + oss.str();
                ms2Chromatogram.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);
                pwiz::util::BinaryData<double>& ms2Times = ms2Chromatogram.getTimeArray()->data;
                pwiz::util::BinaryData<double>& ms2Intensities = ms2Chromatogram.getIntensityArray()->data;
                double epsilon = 1e-14;
                BOOST_FOREACH(const PeptideSpectrumMatch& psm, window.PSMs)
                {
                    double triggerTime = psm.spectrum->scanStartTime;
                    //double triggerIntensity = psm.spectrum->precursorIntensity;
                    ms2Times.push_back(triggerTime-epsilon); ms2Intensities.push_back(0);
                    //ms2Times.push_back(triggerTime); ms2Intensities.push_back(triggerIntensity);
                    ms2Times.push_back(triggerTime); ms2Intensities.push_back(baselineIntensity + scaleForMS2Score * psm.score / window.maxScore);
                    ms2Times.push_back(triggerTime+epsilon); ms2Intensities.push_back(0);
                }
            }

            // output MS2 splines
            if (window.PSMs.size() > 1)
            {
                vector<double> ms2Times, ms2Scores;

                // calculate the minimum time gap between PSMs
                double minDiff = window.PSMs[1].spectrum->scanStartTime - window.PSMs[0].spectrum->scanStartTime;
                for (size_t i=2; i < window.PSMs.size(); ++i)
                    minDiff = min(minDiff, window.PSMs[i].spectrum->scanStartTime - window.PSMs[i-1].spectrum->scanStartTime);

                // add zero scores before and after the "curve" using the minimum time gap
                ms2Times.push_back(window.PSMs.front().spectrum->scanStartTime - minDiff);
                ms2Scores.push_back(0);
                BOOST_FOREACH(const PeptideSpectrumMatch& psm, window.PSMs)
                {
                    ms2Times.push_back(psm.spectrum->scanStartTime);
                    ms2Scores.push_back(baselineIntensity + scaleForMS2Score * psm.score / window.maxScore);
                    //ms2Scores.push_back(psm.score);
                }
                ms2Times.push_back(window.PSMs.back().spectrum->scanStartTime + minDiff);
                ms2Scores.push_back(0);

                Interpolator ms2Interpolator(ms2Times, ms2Scores);
                
                chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
                Chromatogram& ms2Chromatogram = *chromatogramListSimple->chromatograms.back();
                ms2Chromatogram.index = chromatogramListSimple->size()-1;
                ms2Chromatogram.id = "Interpolated MS2s for " + oss.str();
                ms2Chromatogram.setTimeIntensityArrays(vector<double>(), vector<double>(), UO_second, MS_number_of_detector_counts);
                pwiz::util::BinaryData<double>& ms2InterpolatedTimes = ms2Chromatogram.getTimeArray()->data;
                pwiz::util::BinaryData<double>& ms2InterpolatedIntensities = ms2Chromatogram.getIntensityArray()->data;
                double sampleRate = minDiff / 10;
                for(double time=window.MS1RT.front(); time <= window.MS1RT.back(); time += sampleRate)
                {
                    ms2InterpolatedTimes.push_back(time);
                    ms2InterpolatedIntensities.push_back(ms2Interpolator.interpolate(ms2Times, ms2Scores, time));
                }
            }
        }

        BOOST_FOREACH(const UnidentifiedPrecursorInfo& info, unidentifiedPrecursors)
        {
            const LocalChromatogram& window = info.chromatogram;

            chromatogramListSimple->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
            Chromatogram& c = *chromatogramListSimple->chromatograms.back();
            c.index = chromatogramListSimple->size()-1;
            ostringstream oss;
            oss << "precursor m/z: " << info.mzWindow << "; time: " << info.scanTimeWindow;
            c.id = "Raw SIC for " + oss.str();
            c.setTimeIntensityArrays(window.MS1RT, window.MS1Intensity, UO_second, MS_number_of_detector_counts);


            // for idfree metrics, show peaks
            if (g_rtConfig->MetricsType == "idfree")
            {
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
                if (tmp.size() != newRT.size())
                    cerr << "Warning: smoothed intensities vector has different size than MS1RT: " << tmp.size() << " vs. " << newRT.size() << endl;
                else
                    c3.setTimeIntensityArrays(newRT, vector<double>(tmp.begin(), tmp.end()), UO_second, MS_number_of_detector_counts);

                float baselineIntensity = crawdadPeakFinder.getBaselineIntensity();
                double scaleForMS2Score = baselineIntensity;

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

                        /*if (peak.startTime < window.maxScoreScanStartTime && window.maxScoreScanStartTime < peak.endTime)
                            //scaleForMS2Score = (double) peak->getHeight();
                            scaleForMS2Score = ms2ScanMap.get<time>().find(window.maxScoreScanStartTime)->precursorIntensity;
                        else if (window.bestPeak && peak.peakTime == window.bestPeak->peakTime)
                            scaleForMS2Score = window.bestPeak->intensity;*/
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
                }
            }
            else // for idfree metrics the "unidentified" prefix is pointless
                c.id = "unidentified " + c.id;
        }
        string chromFilename = bfs::change_extension(sourceFilename, "-quameter_chromatograms.mz5").string();
        MSDataFile::write(chromData, chromFilename, MSDataFile::WriteConfig(MSDataFile::Format_MZ5));
    }

    int InitProcess( argList_t& args )
    {
        cout << "Quameter " << Version::str() << " (" << Version::LastModified() << ")\n" <<
                QUAMETER_LICENSE << endl;

        string usage = "Usage: " + lexical_cast<string>(bfs::path(args[0]).filename()) + " [optional arguments] <results filemask 1> [results filemask 2] ...\n"
                       "Optional arguments:\n"
                       "-cfg <config filepath>        : specify a configuration file other than the default\n"
                       "-workdir <working directory>  : change working directory (where output files are written)\n"
                       "-cpus <value>                 : force use of <value> worker threads\n"
                       "-ignoreConfigErrors           : ignore errors in configuration file or the command-line\n"
                       "-AnyParameterName <value>     : override the value of the given parameter to <value>\n"
                       "-dump                         : show runtime configuration settings before starting the run\n";

        bool ignoreConfigErrors = false;
        g_endianType = GetHostEndianType();
        g_numWorkers = GetNumProcessors();

        // First set the working directory, if provided
        for( size_t i=1; i < args.size(); ++i )
        {
            if( args[i] == "-workdir" && i+1 <= args.size() )
            {
                chdir( args[i+1].c_str() );
                args.erase( args.begin() + i );
            } else if( args[i] == "-cpus" && i+1 <= args.size() )
            {
                g_numWorkers = atoi( args[i+1].c_str() );
                args.erase( args.begin() + i );
            } else if( args[i] == "-ignoreConfigErrors" )
            {
                ignoreConfigErrors = true;
            } else
                continue;

            args.erase( args.begin() + i );
            --i;
        }

        g_rtConfig = new RunTimeConfig(!ignoreConfigErrors);
        g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;

        for( size_t i=1; i < args.size(); ++i )
        {
            if( args[i] == "-cfg" && i+1 <= args.size() )
            {
                if( g_rtConfig->initializeFromFile( args[i+1] ) )
                {
                    cerr << g_hostString << " could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
                    return 1;
                }
                args.erase( args.begin() + i );
            } else
                continue;

            args.erase( args.begin() + i );
            --i;
        }

        if( args.size() < 2 )
        {
            cerr << "Not enough arguments.\n\n" << usage << endl;
            return 1;
        }

        if( !g_rtConfig->initialized() )
        {
            if( g_rtConfig->initializeFromFile() )
                cerr << "Could not find the default configuration file (hard-coded defaults in use)." << endl;
        }

        // Command line overrides happen after config file has been distributed but before PTM parsing
        RunTimeVariableMap vars = g_rtConfig->getVariables();
        for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
        {
            string varName;
            varName += "-" + itr->first;

            for( size_t i=1; i < args.size(); ++i )
            {
                if( args[i].find( varName ) == 0 && i+1 <= args.size() )
                {
                    //cout << varName << " " << itr->second << " " << args[i+1] << endl;
                    itr->second = args[i+1];
                    args.erase( args.begin() + i );
                    args.erase( args.begin() + i );
                    --i;
                }
            }
        }

        g_rtConfig->setVariables( vars );

        for( size_t i=1; i < args.size(); ++i )
        {
            if (args[i] == "-dump" || args[i] == "-help" || args[i] == "--help")
            {
                g_rtConfig->dump();
                if (args[i] == "-help" || args[i] == "--help")
                    throw pwiz::util::usage_exception(usage);

                args.erase(args.begin() + i);
                --i;
            }
        }

        for( size_t i=1; i < args.size(); ++i )
        {
            if( args[i][0] == '-' )
            {
                if (!ignoreConfigErrors)
                {
                    cerr << "Error: unrecognized parameter \"" << args[i] << "\"" << endl;
                    return 1;
                }

                cerr << "Warning: ignoring unrecognized parameter \"" << args[i] << "\"" << endl;
                args.erase( args.begin() + i );
                --i;
            }
        }

        if (args.size() == 1)
        {
            cerr << "No data sources specified.\n\n" << usage << endl;
            return 1;
        }

        return 0;
    }

    int ProcessHandler( int argc, char* argv[] )
    {
        // Get the command line arguments and process them
        vector< string > args;
        for( int i=0; i < argc; ++i )
            args.push_back( argv[i] );

        if (InitProcess( args ) )
            return 1;

        allSources.clear();

        for( size_t i=1; i < args.size(); ++i )
            FindFilesByMask( args[i], g_inputFilenames );

        if (g_inputFilenames.empty() )
        {
            cout << "No data sources found with the given filemasks." << endl;
            return 1;
        }

        fileList_t::iterator fItr;
        for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
        {
            string inputFile = *fItr;
            bfs::path rawFile = bfs::change_extension(inputFile, "." + g_rtConfig->RawDataFormat);
            bfs::path sourceFilepath;
            if(!g_rtConfig->RawDataPath.empty())
                sourceFilepath = bfs::path(g_rtConfig->RawDataPath) / rawFile.filename();
            else
                sourceFilepath = rawFile;

            if(g_rtConfig->MetricsType != "idfree" && !bfs::exists(sourceFilepath))
            {
                cerr << "Unable to find raw file at: " << sourceFilepath << endl;
                continue;
            }

            if(bal::starts_with(g_rtConfig->MetricsType, "nistms") && bal::ends_with(inputFile,"idpDB") )
            {
                vector<QuameterInput> idpSrcs = GetIDPickerSpectraSources(inputFile);
                allSources.insert(allSources.end(),idpSrcs.begin(),idpSrcs.end());
            }
            else if(g_rtConfig->MetricsType == "idfree")
            {
                QuameterInput qip("",inputFile,"","","",IDFREE);
                allSources.push_back(qip);
            }
            else if(g_rtConfig->MetricsType == "scanranker" && bal::ends_with(inputFile,".txt") )
            {
                QuameterInput qip("",sourceFilepath.string(),"","",inputFile,SCANRANKER);
                allSources.push_back(qip);
            }
            else if(g_rtConfig->MetricsType == "pepitome" && bal::ends_with(inputFile,"pepXML") )
            {
                QuameterInput qip("",sourceFilepath.string(),"",inputFile,"",PEPITOME);
                allSources.push_back(qip); 
            }
            else
                cerr << "Warning: mismatched metrics type and input file type." << endl;
        }

        for(size_t taskID=0; taskID < allSources.size(); ++taskID)
            metricsTasks.push(taskID);

        g_numWorkers = min((int) allSources.size(), g_numWorkers);
        boost::thread_group workerThreadGroup;
        vector<boost::thread*> workerThreads;
        for (int i = 0; i < g_numWorkers; ++i)
            workerThreads.push_back(workerThreadGroup.create_thread(&ExecuteMetricsThread));

        workerThreadGroup.join_all();

        return 0;
    }

    void ExecuteMetricsThread()
    {
        try
        {
            size_t metricsTask;
            
            while(true)
            {
                if(!metricsTasks.pop(metricsTask))
                    break;
                const QuameterInput &metricsInput = allSources[metricsTask];
                switch(metricsInput.type)
                {
                    case NISTMS: NISTMSMetrics(metricsInput); break;
                    case SCANRANKER: ScanRankerMetrics(metricsInput); break;
                    case IDFREE: IDFreeMetrics(metricsInput); break;
                    default: break;
                }
            }
            
        } catch( std::exception& e )
        {
            cerr << " terminated with an error: " << e.what() << endl;
        } catch(...)
        {
            cerr << " terminated with an unknown error." << endl;
        }
    }

    void ScanRankerMetrics(const QuameterInput& currentFile)
    {
        try
        {
            boost::timer processingTimer;

            string sourceFilename = currentFile.sourceFile;
            const string& srFile = currentFile.scanRankerFile;

            ScanRankerReader reader(srFile);
            reader.extractData();

            accs::accumulator_set<double, accs::stats<accs::tag::mean, accs::tag::percentile,
                                                      accs::tag::kurtosis, accs::tag::skewness,
                                                      accs::tag::variance, accs::tag::error_of<accs::tag::mean > > > bestTagScoreStats;
            accs::accumulator_set<double, accs::stats<accs::tag::mean, accs::tag::percentile,
                                                      accs::tag::kurtosis, accs::tag::skewness,
                                                      accs::tag::variance, accs::tag::error_of<accs::tag::mean > > > bestTagTICStats;
            accs::accumulator_set<double, accs::stats<accs::tag::mean, accs::tag::percentile,
                                                      accs::tag::kurtosis, accs::tag::skewness,
                                                      accs::tag::variance, accs::tag::error_of<accs::tag::mean > > > tagMZRangeStats;

            typedef pair<string,ScanRankerMS2PrecInfo> TaggedSpectrum;
            BOOST_FOREACH(const TaggedSpectrum& ts, reader.precursorInfos)
            {
                bestTagScoreStats(reader.bestTagScores[ts.second]);
                bestTagTICStats(reader.bestTagTics[ts.second]);
                tagMZRangeStats(reader.tagMzRanges[ts.second]);
            }
            stringstream ss;
            ss << accs::mean(bestTagScoreStats) << ",";
            ss << accs::percentile(bestTagScoreStats, accs::percentile_number = 50) << ",";
            ss << accs::kurtosis(bestTagScoreStats) << ",";
            ss << accs::skewness(bestTagScoreStats) << ",";
            ss << accs::variance(bestTagScoreStats) << ",";
            ss << accs::error_of<accs::tag::mean>(bestTagScoreStats) << ",";
            ss << accs::mean(bestTagTICStats) << ",";
            ss << accs::percentile(bestTagTICStats, accs::percentile_number = 50) << ",";
            ss << accs::kurtosis(bestTagTICStats) << ",";
            ss << accs::skewness(bestTagTICStats) << ",";
            ss << accs::variance(bestTagTICStats) << ",";
            ss << accs::error_of<accs::tag::mean>(bestTagTICStats) << ",";
            ss << accs::mean(tagMZRangeStats) << ",";
            ss << accs::percentile(tagMZRangeStats, accs::percentile_number = 50) << endl;
            ss << accs::kurtosis(tagMZRangeStats) << ",";
            ss << accs::skewness(tagMZRangeStats) << ",";
            ss << accs::variance(tagMZRangeStats) << ",";
            ss << accs::error_of<accs::tag::mean>(tagMZRangeStats) << ",";

            //cout << sourceFilename << "," << ss.str();
        } catch(exception& e)
        {
            cout << "Error processing the ScanRanker metrics: " << e.what() << endl;
            exit(1);
        }
    }

    struct MZIntensityPairSortByMZ
    {
        bool operator() (const pwiz::msdata::MZIntensityPair& lhs, const pwiz::msdata::MZIntensityPair& rhs) const
        {
            return lhs.mz < rhs.mz;
        }
    };

    struct UnidentifiedPrecursorPeakWidthLessThan
    {
        bool operator() (const UnidentifiedPrecursorInfo& lhs, const UnidentifiedPrecursorInfo& rhs) const
        {
            if (!lhs.chromatogram.bestPeak && !rhs.chromatogram.bestPeak)
                return false;
            else if (!lhs.chromatogram.bestPeak)
                return true;
            else if (!rhs.chromatogram.bestPeak)
                return false;
            else
                return lhs.chromatogram.bestPeak->fwhm < rhs.chromatogram.bestPeak->fwhm;
        }
    };

    void IDFreeMetrics(const QuameterInput& currentFile)
    {
        try
        {
            boost::timer processingTimer;

            string sourceFilename = currentFile.sourceFile;

            // Obtain the list of readers available
            cout << "Opening source file " << sourceFilename << endl;
            boost::unique_lock<boost::mutex> guard(msdMutex);
            FullReaderList readers;
            MSDataFile msd(sourceFilename, &readers);
            cout << "Started processing file " << sourceFilename << endl;
            guard.unlock();

            // apply spectrum list filters
            vector<string> wrappers;
            if (!g_rtConfig->SpectrumListFilters.empty())
                bal::split(wrappers, g_rtConfig->SpectrumListFilters, bal::is_any_of(";"));

            SpectrumListFactory::wrap(msd, wrappers);

            SpectrumList& spectrumList = *msd.run.spectrumListPtr;
            string sourceName = GetFilenameWithoutExtension( GetFilenameFromFilepath( sourceFilename ) );

            // get startTimeStamp
            string startTimeStamp = msd.run.startTimeStamp;

            MS1ScanMap ms1ScanMap;
            MS2ScanMap ms2ScanMap;
            size_t missingPrecursorIntensities = 0;
            map<int, int> scanCountByChargeState;
            string lastMS1NativeId;

            accs::accumulator_set<double, accs::stats<accs::tag::min, accs::tag::max> > scanTimes;
            accs::accumulator_set<double, accs::stats<accs::tag::min, accs::tag::max, accs::tag::percentile> > ms1ScanTimes, ms2ScanTimes;

            // For each spectrum
            size_t curIndex;
            try
            {
                for (curIndex = 0; curIndex < spectrumList.size(); ++curIndex)
                {
                    if (g_numWorkers == 1 && (curIndex+1==spectrumList.size() || !((curIndex+1)%100))) cout << "\rReading metadata: " << (curIndex+1) << "/" << spectrumList.size() << flush;

                    SpectrumPtr spectrum = spectrumList.spectrum(curIndex, false);

                    if (spectrum->defaultArrayLength == 0)
                        continue;

                    if (spectrum->cvParam(MS_MSn_spectrum).empty() && spectrum->cvParam(MS_MS1_spectrum).empty())
                        continue;

                    CVParam spectrumMSLevel = spectrum->cvParam(MS_ms_level);
                    if (spectrumMSLevel == CVID_Unknown)
                        continue;

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
                        scanTimes(scanInfo.scanStartTime);
                        ms1ScanTimes(scanInfo.scanStartTime);

                        ms1ScanMap.push_back(scanInfo);
                    }
                    else if (msLevel == 2) 
                    {
                        MS2ScanInfo scanInfo;
                        scanInfo.nativeID = spectrum->id;
                        scanInfo.identified = false;
                        scanInfo.distinctModifiedPeptideID = 0;

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

                        CVParam chargeState = si.cvParam(MS_charge_state);
                        if (!chargeState.empty())
                            scanInfo.precursorCharge = chargeState.valueAs<int>();
                        else
                            scanInfo.precursorCharge = 0;
                        ++scanCountByChargeState[min(6, scanInfo.precursorCharge)]; // only track charges up to 5; anything higher is a '6'

                        if (precursor.spectrumID.empty())
                        {
                            if (lastMS1NativeId.empty())
                                continue; // skip MS2s before the first MS1

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
                        scanTimes(scanInfo.scanStartTime);
                        ms2ScanTimes(scanInfo.scanStartTime);

                        scanInfo.precursorMZ = si.cvParam(MS_selected_ion_m_z).valueAs<double>();
                        if (si.cvParam(MS_selected_ion_m_z).empty() )
                            scanInfo.precursorMZ = si.cvParam(MS_m_z).valueAs<double>();    
                        if (scanInfo.precursorMZ == 0)
                            throw runtime_error("No precursor m/z for " + spectrum->id);

                        auto findItr = ms1ScanMap.get<nativeID>().find(scanInfo.precursorNativeID);
                        scanInfo.precursorScanStartTime = findItr == ms1ScanMap.get<nativeID>().end() ? 0 : findItr->scanStartTime;

                        ms2ScanMap.push_back(scanInfo);
                    }
                } // finished cycling through all metadata
            }
            catch (exception& e)
            {
                throw runtime_error("error reading spectrum index " + lexical_cast<string>(curIndex) + " (" + e.what() + ")");
            }
            catch (...)
            {
                throw runtime_error("unknown error reading spectrum index " + lexical_cast<string>(curIndex));
            }

            if (g_numWorkers == 1) cout << endl;

            // This warning is unnecessary as long as none of the idfree metrics use the precursor trigger intensity.
            //if (missingPrecursorIntensities)
            //    cerr << "\nWarning: " << missingPrecursorIntensities << " spectra are missing precursor trigger intensity; MS2 TIC will be used as a substitute." << endl;

            vector<UnidentifiedPrecursorInfo> unidentifiedPrecursors;
            unidentifiedPrecursors.reserve(ms2ScanMap.size());
            BOOST_FOREACH(const MS2ScanInfo& scanInfo, ms2ScanMap)
            {
                unidentifiedPrecursors.push_back(UnidentifiedPrecursorInfo());
                UnidentifiedPrecursorInfo& info = unidentifiedPrecursors.back();
                info.spectrum = &scanInfo;
                info.scanTimeWindow = g_rtConfig->chromatogramScanTimeWindow(scanInfo.precursorScanStartTime);
                info.mzWindow = g_rtConfig->chromatogramMzWindow(scanInfo.precursorMZ, 1);
                info.chromatogram.id = "unidentified precursor m/z " + lexical_cast<string>(scanInfo.precursorMZ);
            }

            accs::accumulator_set<double, accs::stats<accs::tag::percentile> > ms1PeakCounts, ms2PeakCounts;
            accs::accumulator_set<double, accs::stats<accs::tag::percentile> > xicBestPeakTimes;
            vector<double> ms1TICs;
            ms1TICs.reserve(ms1ScanMap.size());
            double totalTIC = 0;

            int multiplyChargedMS2s = 0;

            // Going through all spectra once more to get intensities/retention times to build chromatograms
            try
            {
                for (curIndex = 0; curIndex < spectrumList.size(); ++curIndex)
                {
                    if (g_numWorkers == 1 && (curIndex+1==spectrumList.size() || !((curIndex+1)%100))) cout << "\rReading peaks: " << (curIndex+1) << "/" << spectrumList.size() << flush;

                    SpectrumPtr spectrum = spectrumList.spectrum(curIndex, true);

                    if (spectrum->defaultArrayLength == 0) // skip empty scans
                        continue;

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

                        ms1PeakCounts(arraySize);

                        accs::accumulator_set<double, accs::stats<accs::tag::min, accs::tag::max> > mzMinMax;
                        mzMinMax = std::for_each(mzV.begin(), mzV.end(), mzMinMax);
                        interval_set<double> spectrumMzRange(continuous_interval<double>::closed(accs::min(mzMinMax), accs::max(mzMinMax)));

                        // loop through all unidentified MS2 scans
                        BOOST_FOREACH(UnidentifiedPrecursorInfo& info, unidentifiedPrecursors)
                        {
                            if (!boost::icl::contains(info.scanTimeWindow, curRT))
                                continue;

                            // if the PSM's m/z window and the MS1 spectrum's m/z range do not overlap, skip this window
                            if (disjoint(info.mzWindow, spectrumMzRange))
                                continue;

                            double XIC = 0;
                            for (size_t i = 0; i < arraySize; ++i) 
                                // if this m/z is in the window, record its intensity and retention time
                                if (boost::icl::contains(info.mzWindow, mzV[i]))
                                    XIC += intensV[i];

                            info.chromatogram.MS1Intensity.push_back(XIC);
                            info.chromatogram.MS1RT.push_back(curRT);
                        } // done with unidentified MS2 scans
                        
                        double TIC = accumulate(intensV.begin(), intensV.end(), 0);
                        ms1TICs.push_back(TIC);
                        totalTIC += TIC;
                    }
                    else if (msLevel == 2)
                    {
                        vector<MZIntensityPair> peaks;
                        spectrum->getMZIntensityPairs(peaks);
                        sort(peaks.begin(), peaks.end(), MZIntensityPairSortByMZ());
                        size_t arraySize = peaks.size();

                        ms2PeakCounts(arraySize);

                        const MS2ScanInfo& scanInfo = *ms2ScanMap.get<nativeID>().find(spectrum->id);

                        // if charge state is unknown, determine whether it's likely to be singly or multiply charged
                        if (scanInfo.precursorCharge == 0)
                        {
                            double ticBelowPrecursorMz = 0;
                            double TIC = 0;
                            BOOST_FOREACH(MZIntensityPair& peak, peaks)
                            {
                                if (peak.mz < scanInfo.precursorMZ)
                                    ticBelowPrecursorMz += peak.intensity;
                                TIC += peak.intensity;
                            }

                            // if less than 90% of the intensity is below the precursor m/z,
                            // it's probably multiply charged
                            if (ticBelowPrecursorMz / TIC < 0.9)
                                ++multiplyChargedMS2s;

                            // calculate TIC manually if necessary
                            if (scanInfo.precursorIntensity == 0)
                                const_cast<MS2ScanInfo&>(scanInfo).precursorIntensity = TIC;
                        }

                        // calculate TIC manually if necessary
                        if (scanInfo.precursorIntensity == 0)
                        {
                            BOOST_FOREACH(const MZIntensityPair& p, peaks)
                                const_cast<MS2ScanInfo&>(scanInfo).precursorIntensity += p.intensity;
                        }
                    }
                } // end of spectra loop
            }
            catch (exception& e)
            {
                throw runtime_error("error reading spectrum index " + lexical_cast<string>(curIndex) + " (" + e.what() + ")");
            }
            catch (...)
            {
                throw runtime_error("unknown error reading spectrum index " + lexical_cast<string>(curIndex));
            }

            int scansWithDeterminedCharges = ms2ScanMap.size() - scanCountByChargeState[0];

            if (g_numWorkers == 1) cout << endl;
            
            // cycle through all chromatograms, passing each one to Crawdad
            size_t i = 0;
            double peakWidthTotal = 0; // keep track of total peak width when running Crawdad
            BOOST_FOREACH(UnidentifiedPrecursorInfo& info, unidentifiedPrecursors)
            {
                ++i;
                if (g_numWorkers == 1 && (i==unidentifiedPrecursors.size() || !(i%100))) cout << "\rFinding precursor peaks: " << i << "/" << unidentifiedPrecursors.size() << flush;

                LocalChromatogram& lc = info.chromatogram;
                if (lc.MS1RT.empty())
                {
                    cerr << "Warning: precursor m/z " << info.spectrum->precursorMZ
                         << " (m/z: " << info.mzWindow
                         << "; time: " << info.scanTimeWindow
                         << ") has no chromatogram data points!" << endl;
                    continue;
                }

                // make chromatogram data points evenly spaced
                Interpolator(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity).resample(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity);

                // eliminate negative signal
                BOOST_FOREACH(double& intensity, info.chromatogram.MS1Intensity)
                    intensity = max(0.0, intensity);

                CrawdadPeakFinder crawdadPeakFinder;
                crawdadPeakFinder.SetChromatogram(lc.MS1RT, lc.MS1Intensity);
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

                    if (!lc.bestPeak || fabs(peakTime - info.spectrum->scanStartTime) < fabs(lc.bestPeak->peakTime - info.spectrum->scanStartTime))
                        lc.bestPeak = peak;
                }

                if (lc.bestPeak)
                    peakWidthTotal += lc.bestPeak->fwhm;
            }

            XICWindowList dummyPepWindows;

            // Write chromatograms for visualization of data
            if (g_rtConfig->ChromatogramOutput)
                writeChromatograms(sourceFilename, ms2ScanMap, dummyPepWindows, unidentifiedPrecursors);

            // TIC metrics
            accs::accumulator_set<double, accs::stats<accs::tag::max, accs::tag::percentile> > ms1DeltaTICs, ms1TICs_acc;
            vector<double> cumulativeTIC(ms1TICs.size());
            cumulativeTIC[0] = ms1TICs[0] / totalTIC;
            for (size_t i=1; i < ms1TICs.size(); ++i)
            {
                ms1TICs_acc(ms1TICs[i]);
                ms1DeltaTICs(fabs(ms1TICs[i] - ms1TICs[i-1]));
                cumulativeTIC[i] = cumulativeTIC[i-1] + ms1TICs[i] / totalTIC;
            }

            double ms1DeltaTIC_Q1 = log(accs::percentile(ms1DeltaTICs, accs::percentile_number = 25));
            double ms1DeltaTIC_Q2 = log(accs::percentile(ms1DeltaTICs, accs::percentile_number = 50));
            double ms1DeltaTIC_Q3 = log(accs::percentile(ms1DeltaTICs, accs::percentile_number = 75));
            double ms1DeltaTIC_Q2_ratio = ms1DeltaTIC_Q2 - ms1DeltaTIC_Q1;
            double ms1DeltaTIC_Q3_ratio = ms1DeltaTIC_Q3 - ms1DeltaTIC_Q2;
            double ms1DeltaTIC_Q4_ratio = log(accs::max(ms1DeltaTICs)) - ms1DeltaTIC_Q3;

            double ms1TIC_Q1 = log(accs::percentile(ms1TICs_acc, accs::percentile_number = 25));
            double ms1TIC_Q2 = log(accs::percentile(ms1TICs_acc, accs::percentile_number = 50));
            double ms1TIC_Q3 = log(accs::percentile(ms1TICs_acc, accs::percentile_number = 75));
            double ms1TIC_Q2_ratio = ms1TIC_Q2 - ms1TIC_Q1;
            double ms1TIC_Q3_ratio = ms1TIC_Q3 - ms1TIC_Q2;
            double ms1TIC_Q4_ratio = log(accs::max(ms1TICs_acc)) - ms1TIC_Q3;

            vector<double>::const_iterator cumulativeTIC_Q1 = boost::lower_bound(cumulativeTIC, 0.25);
            vector<double>::const_iterator cumulativeTIC_Q2 = boost::lower_bound(cumulativeTIC, 0.50);
            vector<double>::const_iterator cumulativeTIC_Q3 = boost::lower_bound(cumulativeTIC, 0.75);
            size_t cumulativeTIC_Q1_Index = cumulativeTIC_Q1 - cumulativeTIC.begin();
            size_t cumulativeTIC_Q2_Index = cumulativeTIC_Q2 - cumulativeTIC.begin();
            size_t cumulativeTIC_Q3_Index = cumulativeTIC_Q3 - cumulativeTIC.begin();
            double ms1MinScanTime = accs::min(ms1ScanTimes);
            double ms1MaxScanTime = accs::max(ms1ScanTimes);
            double ms1ScanTimeDuration = ms1MaxScanTime - ms1MinScanTime;
            double durationOfCumulativeTIC_Q1 = ms1ScanMap[cumulativeTIC_Q1_Index].scanStartTime;
            double durationOfCumulativeTIC_Q2 = ms1ScanMap[cumulativeTIC_Q2_Index].scanStartTime;
            double durationOfCumulativeTIC_Q3 = ms1ScanMap[cumulativeTIC_Q3_Index].scanStartTime;
            double durationOfCumulativeTIC_Q1_interval = (durationOfCumulativeTIC_Q1 - ms1MinScanTime) / ms1ScanTimeDuration;
            double durationOfCumulativeTIC_Q2_interval = (durationOfCumulativeTIC_Q2 - durationOfCumulativeTIC_Q1) / ms1ScanTimeDuration;
            double durationOfCumulativeTIC_Q3_interval = (durationOfCumulativeTIC_Q3 - durationOfCumulativeTIC_Q2) / ms1ScanTimeDuration;
            double durationOfCumulativeTIC_Q4_interval = (ms1MaxScanTime - durationOfCumulativeTIC_Q3) / ms1ScanTimeDuration;

            double ms1ScanTimes_Q1 = accs::percentile(ms1ScanTimes, accs::percentile_number = 25);
            double ms1ScanTimes_Q2 = accs::percentile(ms1ScanTimes, accs::percentile_number = 50);
            double ms1ScanTimes_Q3 = accs::percentile(ms1ScanTimes, accs::percentile_number = 75);
            double ms1ScanTimes_Q1_interval = (ms1ScanTimes_Q1 - accs::min(ms1ScanTimes)) / ms1ScanTimeDuration;
            double ms1ScanTimes_Q2_interval = (ms1ScanTimes_Q2 - ms1ScanTimes_Q1) / ms1ScanTimeDuration;
            double ms1ScanTimes_Q3_interval = (ms1ScanTimes_Q3 - ms1ScanTimes_Q2) / ms1ScanTimeDuration;
            double ms1ScanTimes_Q4_interval = (accs::max(ms1ScanTimes) - ms1ScanTimes_Q3) / ms1ScanTimeDuration;

            double ms2ScanTimeDuration = accs::max(ms2ScanTimes) - accs::min(ms2ScanTimes);
            double ms2ScanTimes_Q1 = accs::percentile(ms2ScanTimes, accs::percentile_number = 25);
            double ms2ScanTimes_Q2 = accs::percentile(ms2ScanTimes, accs::percentile_number = 50);
            double ms2ScanTimes_Q3 = accs::percentile(ms2ScanTimes, accs::percentile_number = 75);
            double ms2ScanTimes_Q1_interval = (ms2ScanTimes_Q1 - accs::min(ms2ScanTimes)) / ms2ScanTimeDuration;
            double ms2ScanTimes_Q2_interval = (ms2ScanTimes_Q2 - ms2ScanTimes_Q1) / ms2ScanTimeDuration;
            double ms2ScanTimes_Q3_interval = (ms2ScanTimes_Q3 - ms2ScanTimes_Q2) / ms2ScanTimeDuration;
            double ms2ScanTimes_Q4_interval = (accs::max(ms2ScanTimes) - ms2ScanTimes_Q3) / ms2ScanTimeDuration;

            // calculate maximum scan frequency over any minute for MS1
            double ms1MaxFrequency = ms1ScanMap.size() / ms1ScanTimeDuration;
            {
                int scanCount = 0;
                MS1ScanMap::index<time>::type::const_iterator itr = ms1ScanMap.get<time>().begin(), end = ms1ScanMap.get<time>().end(), itr2;
                for (itr2 = itr; itr != end; ++itr, --scanCount)
                {
                    // advance itr2 to be at least 1 minute ahead of itr
                    while (itr2 != end && (itr2->scanStartTime - itr->scanStartTime) < 60) {++itr2; ++scanCount;}
                    if (itr2 == end) break;

                    double scanFrequency = scanCount / (itr2->scanStartTime - itr->scanStartTime);
                    //if (scanFrequency > ms1MaxFrequency)
                    //    cout << "New max ms1 frequency in range [" << itr->scanStartTime << "-" << itr2->scanStartTime << "]: " << scanFrequency << endl;
                    ms1MaxFrequency = max(ms1MaxFrequency, scanFrequency);
                }
            }

            // calculate maximum scan frequency over any minute for MS2
            double ms2MaxFrequency = ms2ScanMap.size() / ms2ScanTimeDuration;
            {
                int scanCount = 0;
                MS2ScanMap::index<time>::type::const_iterator itr = ms2ScanMap.get<time>().begin(), end = ms2ScanMap.get<time>().end(), itr2;
                for (itr2 = itr; itr != end; ++itr, --scanCount)
                {
                    // advance itr2 to be at least 1 minute ahead of itr
                    while (itr2 != end && (itr2->scanStartTime - itr->scanStartTime) < 60) {++itr2; ++scanCount;}
                    if (itr2 == end) break;

                    double scanFrequency = scanCount / (itr2->scanStartTime - itr->scanStartTime);
                    //if (scanFrequency > ms2MaxFrequency)
                    //    cout << "New max ms2 frequency in range [" << itr->scanStartTime << "-" << itr2->scanStartTime << "]: " << scanFrequency << endl;
                    ms2MaxFrequency = max(ms2MaxFrequency, scanFrequency);
                }
            }

            // XIC peak metrics
            double halfOfTotalPeakWidthFraction = 0;
            accs::accumulator_set<double, accs::stats<accs::tag::percentile> > peakWidths;
            accs::accumulator_set<double, accs::stats<accs::tag::percentile, accs::tag::max> > peakHeights;
            {
                sort(unidentifiedPrecursors.rbegin(), unidentifiedPrecursors.rend(), UnidentifiedPrecursorPeakWidthLessThan()); // sort descending by peak width

                // determine the set of peaks that account for 50% of the total peak width;
                // summarize the widths and heights of those peaks
                double peakWidthFraction = 0;
                //cout << endl << "Total peak width: " << peakWidthTotal << endl;
                for (size_t i=0; i < unidentifiedPrecursors.size(); ++i)
                {
                    const UnidentifiedPrecursorInfo& info = unidentifiedPrecursors[i];
                    if (info.chromatogram.bestPeak)
                    {
                        peakHeights(info.chromatogram.bestPeak->intensity);
                        peakWidths(info.chromatogram.bestPeak->fwhm);
                        peakWidthFraction += info.chromatogram.bestPeak->fwhm / peakWidthTotal;
                        peakWidths(info.chromatogram.bestPeak->fwhm);
                        //cout << "; " << info.chromatogram.bestPeak->fwhm << " (" << peakWidthFraction << ")";
                        if (peakWidthFraction > 0.5)
                        {
                            halfOfTotalPeakWidthFraction = (double) i / unidentifiedPrecursors.size();
                            break;
                        }
                    }
                }
            }
            
            // File for quameter output, default is to save to same directory as input file
            string outputFilepath = g_rtConfig->OutputFilepath;
            if (outputFilepath.empty())
                outputFilepath = bfs::change_extension(sourceFilename, ".qual.tsv").string();

            guard.lock();
            bool needsHeader = !bfs::exists(outputFilepath);

            ofstream qout(outputFilepath.c_str(), ios::out | ios::app);

            // Tab delimited output header
            if (needsHeader)
                qout << "Filename\t"
                        "StartTimeStamp\t"
                        "XIC-WideFrac\t"
                        "XIC-FWHM-Q1\t"
                        "XIC-FWHM-Q2\t"
                        "XIC-FWHM-Q3\t"
                        "XIC-Height-Q2\t"
                        "XIC-Height-Q3\t"
                        "XIC-Height-Q4\t"
                        "RT-Duration\t"
                        "RT-TIC-Q1\t"
                        "RT-TIC-Q2\t"
                        "RT-TIC-Q3\t"
                        "RT-TIC-Q4\t"
                        "RT-MS-Q1\t"
                        "RT-MS-Q2\t"
                        "RT-MS-Q3\t"
                        "RT-MS-Q4\t"
                        "RT-MSMS-Q1\t"
                        "RT-MSMS-Q2\t"
                        "RT-MSMS-Q3\t"
                        "RT-MSMS-Q4\t"
                        "MS1-TIC-Change-Q2\t"
                        "MS1-TIC-Change-Q3\t"
                        "MS1-TIC-Change-Q4\t"
                        "MS1-TIC-Q2\t"
                        "MS1-TIC-Q3\t"
                        "MS1-TIC-Q4\t"
                        "MS1-Count\t"
                        "MS1-Freq-Max\t"
                        "MS1-Density-Q1\t"
                        "MS1-Density-Q2\t"
                        "MS1-Density-Q3\t"
                        "MS2-Count\t"
                        "MS2-Freq-Max\t"
                        "MS2-Density-Q1\t"
                        "MS2-Density-Q2\t"
                        "MS2-Density-Q3\t"
                        "MS2-PrecZ-1\t"
                        "MS2-PrecZ-2\t"
                        "MS2-PrecZ-3\t"
                        "MS2-PrecZ-4\t"
                        "MS2-PrecZ-5\t"
                        "MS2-PrecZ-more\t"
                        "MS2-PrecZ-likely-1\t"
                        "MS2-PrecZ-likely-multi";
            qout << "\n";

            // Tab delimited metrics
            qout << bfs::path(sourceFilename).filename();
            qout << "\t" << startTimeStamp;
            qout << "\t" << halfOfTotalPeakWidthFraction;
            qout << "\t" << accs::percentile(peakWidths, accs::percentile_number = 25);
            qout << "\t" << accs::percentile(peakWidths, accs::percentile_number = 50);
            qout << "\t" << accs::percentile(peakWidths, accs::percentile_number = 75);
            qout << "\t" << log(accs::percentile(peakHeights, accs::percentile_number = 50) / accs::percentile(peakHeights, accs::percentile_number = 25));
            qout << "\t" << log(accs::percentile(peakHeights, accs::percentile_number = 75) / accs::percentile(peakHeights, accs::percentile_number = 50));
            qout << "\t" << log(accs::max(peakHeights) / accs::percentile(peakHeights, accs::percentile_number = 75));
            qout << "\t" << accs::max(scanTimes) - accs::min(scanTimes);
            qout << "\t" << durationOfCumulativeTIC_Q1_interval;
            qout << "\t" << durationOfCumulativeTIC_Q2_interval;
            qout << "\t" << durationOfCumulativeTIC_Q3_interval;
            qout << "\t" << durationOfCumulativeTIC_Q4_interval;
            qout << "\t" << ms1ScanTimes_Q1_interval;
            qout << "\t" << ms1ScanTimes_Q2_interval;
            qout << "\t" << ms1ScanTimes_Q3_interval;
            qout << "\t" << ms1ScanTimes_Q4_interval;
            qout << "\t" << ms2ScanTimes_Q1_interval;
            qout << "\t" << ms2ScanTimes_Q2_interval;
            qout << "\t" << ms2ScanTimes_Q3_interval;
            qout << "\t" << ms2ScanTimes_Q4_interval;
            qout << "\t" << ms1DeltaTIC_Q2_ratio;
            qout << "\t" << ms1DeltaTIC_Q3_ratio;
            qout << "\t" << ms1DeltaTIC_Q4_ratio;
            qout << "\t" << ms1TIC_Q2_ratio;
            qout << "\t" << ms1TIC_Q3_ratio;
            qout << "\t" << ms1TIC_Q4_ratio;
            qout << "\t" << ms1ScanMap.size();
            qout << "\t" << ms1MaxFrequency;
            qout << "\t" << accs::percentile(ms1PeakCounts, accs::percentile_number = 25);
            qout << "\t" << accs::percentile(ms1PeakCounts, accs::percentile_number = 50);
            qout << "\t" << accs::percentile(ms1PeakCounts, accs::percentile_number = 75);
            qout << "\t" << ms2ScanMap.size();
            qout << "\t" << ms2MaxFrequency;
            qout << "\t" << accs::percentile(ms2PeakCounts, accs::percentile_number = 25);
            qout << "\t" << accs::percentile(ms2PeakCounts, accs::percentile_number = 50);
            qout << "\t" << accs::percentile(ms2PeakCounts, accs::percentile_number = 75);
            qout << "\t" << (double) scanCountByChargeState[1] / ms2ScanMap.size();
            qout << "\t" << (double) scanCountByChargeState[2] / ms2ScanMap.size();
            qout << "\t" << (double) scanCountByChargeState[3] / ms2ScanMap.size();
            qout << "\t" << (double) scanCountByChargeState[4] / ms2ScanMap.size();
            qout << "\t" << (double) scanCountByChargeState[5] / ms2ScanMap.size();
            qout << "\t" << (double) scanCountByChargeState[6] / ms2ScanMap.size();
            qout << "\t" << (double) (scanCountByChargeState[0] - multiplyChargedMS2s) / ms2ScanMap.size();
            qout << "\t" << (double) multiplyChargedMS2s / ms2ScanMap.size();

            cout << endl << sourceFilename << " took " << processingTimer.elapsed() << " seconds to analyze.\n";
            guard.unlock();

            return;
        }
        catch (exception& e)
        {
            cerr << "\nError processing ID-free metrics: " << e.what() << endl;
        }
        catch (...)
        {
            cerr << "\nUnknown error processing ID-free metrics." << endl;
        }
        exit(1);
    }

    /**
     * The primary function where all metrics are calculated.
     */
    void NISTMSMetrics(const QuameterInput& currentFile) 
    {
        try 
        {
            boost::timer processingTimer;

            string sourceFilename = currentFile.sourceFile;
            const string& dbFilename = currentFile.idpDBFile;
            const string& sourceId = currentFile.sourceID;

            // Initialize the idpicker reader. It supports idpDB's for now.
            cout << "Reading identifications from " << sourceFilename << endl;
            IDPDBReader idpReader(dbFilename, sourceId);

            // Obtain the list of readers available
            cout << "Opening source file " << sourceFilename << endl;
            boost::unique_lock<boost::mutex> guard(msdMutex);
            FullReaderList readers;
            MSDataFile msd(sourceFilename, &readers);
            cout << "Started processing file " << sourceFilename << endl;
            guard.unlock();

            // apply spectrum list filters
            vector<string> wrappers;
            if (!g_rtConfig->SpectrumListFilters.empty())
                bal::split(wrappers, g_rtConfig->SpectrumListFilters, bal::is_any_of(";"));

            SpectrumListFactory::wrap(msd, wrappers);

            SpectrumList& spectrumList = *msd.run.spectrumListPtr;
            string sourceName = GetFilenameWithoutExtension( GetFilenameFromFilepath( sourceFilename ) );

            // get startTimeStamp
            string startTimeStamp = msd.run.startTimeStamp;

            // Spectral counts
            int MS1Count = 0, MS2Count = 0, MS3OrGreaterCount = 0;

            accs::accumulator_set<double, accs::stats<accs::tag::mean, accs::tag::percentile> > ms1IonInjectionTimes;
            accs::accumulator_set<double, accs::stats<accs::tag::mean, accs::tag::percentile> > ms2IonInjectionTimes;
            accs::accumulator_set<double, accs::stats<accs::tag::percentile> > MS2PeakCounts;
            MS1ScanMap ms1ScanMap;
            MS2ScanMap ms2ScanMap;

            const map<string, size_t>& distinctModifiedPeptideByNativeID = idpReader.distinctModifiedPeptideByNativeID();
            map<size_t, double> firstScanTimeOfDistinctModifiedPeptide;

            // Call MedianPrecursorMZ() for metric IS-2
            double medianPrecursorMZ = idpReader.getMedianPrecursorMZ();

            const vector<size_t>& spectrumCountBySpecificity = idpReader.spectrumCountBySpecificity();
            const vector<size_t>& distinctMatchCountBySpecificity = idpReader.distinctMatchCountBySpecificity();
            const vector<size_t>& distinctPeptideCountBySpecificity = idpReader.distinctPeptideCountBySpecificity();

            string lastMS1NativeId;
            double lastMS1IonInjectionTime = 0;
            size_t missingPrecursorIntensities = 0;

            // For each spectrum
            for( size_t curIndex = 0; curIndex < spectrumList.size(); ++curIndex ) 
            {
                if (g_numWorkers == 1 && (curIndex+1==spectrumList.size() || !((curIndex+1)%100))) cout << "\rReading metadata: " << (curIndex+1) << "/" << spectrumList.size() << flush;

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

                    if (scan.hasCVParam(MS_ion_injection_time))
                        lastMS1IonInjectionTime = scan.cvParam(MS_ion_injection_time).valueAs<double>();

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
                    map<string, size_t>::const_iterator findItr = distinctModifiedPeptideByNativeID.find(spectrum->id);
                    if (findItr != distinctModifiedPeptideByNativeID.end()) 
                    {
                        scanInfo.identified = true;
                        scanInfo.distinctModifiedPeptideID = findItr->second;
                        map<size_t, double>::iterator insertItr = firstScanTimeOfDistinctModifiedPeptide.insert(make_pair(findItr->second, scanInfo.scanStartTime)).first;
                        insertItr->second = min(insertItr->second, scanInfo.scanStartTime);

                        // assume the previous MS1 is the precursor scan
                        if (lastMS1IonInjectionTime > 0)
                            ms1IonInjectionTimes(lastMS1IonInjectionTime);

                        // For metric MS2-1
                        if (scan.hasCVParam(MS_ion_injection_time))
                            ms2IonInjectionTimes(scan.cvParam(MS_ion_injection_time).valueAs<double>());

                        // For metric MS2-3
                        MS2PeakCounts(spectrum->defaultArrayLength);
                    }
                    else 
                    { // this MS2 scan was not identified; we need this data for metrics MS2-4A/B/C/D
                        scanInfo.identified = false;
                        scanInfo.distinctModifiedPeptideID = 0;
                    }
                    ms2ScanMap.push_back(scanInfo);
                }
                else if (msLevel > 2) 
                {
                    MS2ScanInfo scanInfo;
                    scanInfo.nativeID = spectrum->id;
                    scanInfo.msLevel = msLevel;
                    scanInfo.distinctModifiedPeptideID = 0;
                    scanInfo.identified = true;

                    if (spectrum->scanList.scans.empty())
                        throw runtime_error("No scan start time for " + spectrum->id);

                    Scan& scan = spectrum->scanList.scans[0];
                    CVParam scanTime = scan.cvParam(MS_scan_start_time);
                    if (scanTime.empty())
                        throw runtime_error("No scan start time for " + spectrum->id);
                    scanInfo.scanStartTime = scanTime.timeInSeconds();

                    ms2ScanMap.push_back(scanInfo);

                    ++MS3OrGreaterCount;
                }

            } // finished cycling through all spectra

            if (g_numWorkers == 1) cout << endl;

            if (missingPrecursorIntensities)
                cerr << "Warning: " << missingPrecursorIntensities << " spectra are missing precursor trigger intensity; MS2 TIC will be used as a substitute." << endl;

            if (MS1Count == 0)
                throw runtime_error("no MS1 spectra found in \"" + sourceFilename + "\". Is the file incorrectly filtered?");

            if (MS2Count == 0)
                throw runtime_error("no MS2 spectra found in \"" + sourceFilename + "\". Is the file incorrectly filtered?");

            if (MS3OrGreaterCount)
                cerr << "Warning: " << MS3OrGreaterCount << " spectra have MS levels higher than 2 but will be ignored." << endl;

            accs::accumulator_set<double, accs::stats<accs::tag::percentile> > firstScanTimeOfDistinctModifiedPeptides;
            BOOST_FOREACH_FIELD((size_t id)(double firstScanTime), firstScanTimeOfDistinctModifiedPeptide)
                firstScanTimeOfDistinctModifiedPeptides(firstScanTime);
            double firstQuartileIDTime = accs::percentile(firstScanTimeOfDistinctModifiedPeptides, accs::percentile_number = 25);
            double thirdQuartileIDTime = accs::percentile(firstScanTimeOfDistinctModifiedPeptides, accs::percentile_number = 75);
            double ms1ScanTimeOfFirstQuartile = ms2ScanMap.get<time>().lower_bound(firstQuartileIDTime)->precursorScanStartTime;

            /*cout << endl << "Scan time deciles:";
            for (int i=0; i <= 100; i += 10)
                cout << " " << (accs::percentile(firstScanTimeOfDistinctPeptides, accs::percentile_number = i) / 60);
            cout << endl;*/

            // Metric C-2A: IQR of distinct peptide identifications
            double iqIDTime = (thirdQuartileIDTime - firstQuartileIDTime) / 60;

            // Metric DS-2A: number of MS1 scans taken over C-2A
            size_t iqMS1Scans = 0;
            {
                MS1ScanMap::index<time>::type::const_iterator itr = ms1ScanMap.get<time>().lower_bound(firstQuartileIDTime);
                while (itr != ms1ScanMap.get<time>().end() && itr->scanStartTime <= thirdQuartileIDTime)
                {
                    ++iqMS1Scans;
                    ++itr;
                }
            }

            // Metric DS-2B: number of MS2 scans taken over C-2A
            size_t iqMS2Scans = 0, iqMS2IDs = 0;
            set<size_t> iqDistinctModifiedPeptides;
            {
                MS2ScanMap::index<time>::type::const_iterator itr = ms2ScanMap.get<time>().lower_bound(ms1ScanTimeOfFirstQuartile);
                while (itr != ms2ScanMap.get<time>().end() && itr->scanStartTime <= thirdQuartileIDTime)
                {
                    ++iqMS2Scans;
                    if (itr->identified)
                    {
                        ++iqMS2IDs;
                        iqDistinctModifiedPeptides.insert(itr->distinctModifiedPeptideID);
                    }
                    ++itr;
                }
            }

            // Metric C-2B: number of distinct peptides identified over C-2A
            double iqIDRate = iqIDTime == 0 ? 0 : iqDistinctModifiedPeptides.size() / iqIDTime;

            // Metric IS-1A: number of times that MS1 TIC jumps by 10x in adjacent scans within C-2A
            // Metric IS-1B: number of times that MS1 TIC falls by 10x in adjacent scans within C-2A
            int ticDrop = 0;
            int ticJump = 0;

            MS1ScanMap::index<time>::type::const_iterator lastItr = ms1ScanMap.get<time>().lower_bound(firstQuartileIDTime), itr = lastItr;
            ++itr;
            while (itr != ms1ScanMap.get<time>().end() && itr->scanStartTime <= thirdQuartileIDTime)
            {
                // Is the current total ion current less than 1/10th of the last MS1 scan?
                if (itr->totalIonCurrent*10 < lastItr->totalIonCurrent) 
                    ++ticDrop;
                // Is the current total ion current more than 10 times the last MS1 scan?
                else if (itr->totalIonCurrent > lastItr->totalIonCurrent*10) 
                    ++ticJump;
                ++lastItr;
                ++itr;
            }

            // Metrics IS-3A, IS3-B, IS3-C
            const vector<size_t>& allIDedPrecCharges = idpReader.distinctMatchCountByCharge();
            float IS3A = allIDedPrecCharges[TWO] == 0 ? 0 : (float)allIDedPrecCharges[ONE] / allIDedPrecCharges[TWO];
            float IS3B = allIDedPrecCharges[TWO] == 0 ? 0 : (float)allIDedPrecCharges[THREE] / allIDedPrecCharges[TWO];
            float IS3C = allIDedPrecCharges[TWO] == 0 ? 0 : (float)allIDedPrecCharges[FOUR] / allIDedPrecCharges[TWO];

            // MS1-1: Median MS1 ion injection time
            double medianInjectionTimeMS1 = accs::percentile(ms1IonInjectionTimes, accs::percentile_number = 50);;

            // MS1-5A: Median real value of precursor errors    
            // MS1-5B: Mean of the absolute precursor errors
            // MS1-5C: Median real value of precursor errors in ppm
            // MS1-5D: Interquartile range in ppm of the precursor errors
            const MassErrorStats& precMassErrorStats = idpReader.precursorMassErrorStats();

            // MS2-1: Median MS2 ion injection time
            double medianInjectionTimeMS2 = accs::percentile(ms2IonInjectionTimes, accs::percentile_number = 50);

            // MS2-3: Median number of peaks in an MS2 scan
            double medianNumMS2Peaks = accs::percentile(MS2PeakCounts, accs::percentile_number = 50);

            // P-1: Median peptide ID score
            double medianIDScore = idpReader.getMedianIDScore();

            // P-2A: Number of MS2 spectra identifying tryptic peptide ions
            int numTrypticMS2Spectra = spectrumCountBySpecificity[2];

            // P-2B: Number of tryptic peptide ions identified
            int numTrypticPeptides = distinctMatchCountBySpecificity[2];

            // P-2C: Number of distinct tryptic peptides
            // P-3: Ratio of semi to fully tryptic peptides
            float ratioSemiToFullyTryptic = distinctPeptideCountBySpecificity[2] == 0 ? 0 :
                                            (float) distinctPeptideCountBySpecificity[1] /
                                                    distinctPeptideCountBySpecificity[2];

            // MS1-2B: Median MS1 TIC within C-2A
            double medianTIC = 0;
            {
                accs::accumulator_set<double, accs::stats<accs::tag::percentile> > ms1_IQR_TICs;
                MS1ScanMap::index<time>::type::const_iterator itr = ms1ScanMap.get<time>().lower_bound(ms1ScanTimeOfFirstQuartile);
                while (itr != ms1ScanMap.get<time>().end() && itr->scanStartTime <= thirdQuartileIDTime)
                {
                    ms1_IQR_TICs(itr->totalIonCurrent);
                    ++itr;
                }
                medianTIC = accs::percentile(ms1_IQR_TICs, accs::percentile_number = 50)/1000;
            }

            // Metric DS-1[AB]: Estimate oversampling
            const vector<size_t>& peptideSamplingRates = idpReader.peptideSamplingRates();
            int identifiedOnce = peptideSamplingRates[ONCE];
            int identifiedTwice = peptideSamplingRates[TWICE];
            int identifiedThrice = peptideSamplingRates[THRICE];
            float DS1A = identifiedTwice == 0 ? 1 : (float)identifiedOnce / identifiedTwice;
            float DS1B = identifiedThrice == 0 ? 1 : (float)identifiedTwice / identifiedThrice;

            // metrics that depend on binary data
            int numDuplicatePeptides = 0, peakTailing = 0, bleed = 0,
                idQ1 = 0, idQ2 = 0, idQ3 = 0, idQ4 = 0,
                totalQ1 = 0, totalQ2 = 0, totalQ3 = 0, totalQ4 = 0;
            double peakTailingRatio = 0, bleedRatio = 0,
                   identifiedPeakWidthMedian = 0, identifiedPeakWidthIQR = 0, fwhmQ1 = 0, fwhmQ3 = 0,
                   medianDistinctMatchPeakWidthOver9thScanTimeDecile = 0, medianDistinctMatchPeakWidthOver1stScanTimeDecile = 0, medianDistinctMatchPeakWidthOver5thScanTimeDecile = 0,
                   medianSamplingRatio = 0, bottomHalfSamplingRatio = 0,
                   medianSigNoisMS1 = 0, dynamicRangeOfPeptideSignals = 0, peakPrecursorIntensityMedian = 0,
                   medianSigNoisMS2 = 0, idRatioQ1 = 0, idRatioQ2 = 0, idRatioQ3 = 0, idRatioQ4 = 0,
                   peakPrecursorIntensity95thPercentile = 0, peakPrecursorIntensity5thPercentile = 0;

            if (g_rtConfig->MetricsType == "nistms")
            {
                accs::accumulator_set<double, accs::stats<accs::tag::percentile> > sigNoisMS1;
                accs::accumulator_set<double, accs::stats<accs::tag::percentile> > sigNoisMS2;

                XICWindowList pepWindow = idpReader.MZRTWindows(ms2ScanMap);
                vector<UnidentifiedPrecursorInfo> unidentifiedPrecursors;

                {
                    MS2ScanMap::index<identified>::type::const_iterator itr = ms2ScanMap.get<identified>().lower_bound(false);
                    for (; itr != ms2ScanMap.get<identified>().end() && !itr->identified; ++itr)
                    {
                        unidentifiedPrecursors.push_back(UnidentifiedPrecursorInfo());
                        UnidentifiedPrecursorInfo& info = unidentifiedPrecursors.back();
                        const MS2ScanInfo& scanInfo = *itr;
                        info.spectrum = &scanInfo;
                        info.scanTimeWindow = g_rtConfig->chromatogramScanTimeWindow(scanInfo.precursorScanStartTime);
                        info.mzWindow = g_rtConfig->chromatogramMzWindow(scanInfo.precursorMZ, 1);
                        info.chromatogram.id = "unidentified precursor m/z " + lexical_cast<string>(scanInfo.precursorMZ);
                    }
                }

                // Going through all spectra once more to get intensities/retention times to build chromatograms
                for( size_t curIndex = 0; curIndex < spectrumList.size(); ++curIndex ) 
                {
                    if (g_numWorkers == 1 && (curIndex+1==spectrumList.size() || !((curIndex+1)%100))) cout << "\rReading peaks: " << (curIndex+1) << "/" << spectrumList.size() << flush;

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
                        interval_set<double> spectrumMzRange(continuous_interval<double>::closed(accs::min(mzMinMax), accs::max(mzMinMax)));

                        // For Metric MS1-2A, signal to noise ratio of MS1, peaks/medians
                        if (curRT >= firstQuartileIDTime && curRT <= thirdQuartileIDTime) 
                        {
                            accs::accumulator_set<double, accs::stats<accs::tag::percentile, accs::tag::max, accs::tag::count> > ms1Peaks;
                            BOOST_FOREACH(const double& p, intensV)
                                    ms1Peaks(p);
                            if(accs::count(ms1Peaks) > 0)
                                sigNoisMS1(accs::max(ms1Peaks) / accs::percentile(ms1Peaks, accs::percentile_number = 50));
                        }

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

                            window.MS1Intensity.push_back(sumIntensities);
                            window.MS1RT.push_back(curRT);
                        } // done searching through all unique peptide windows for this MS1 scan

                        // loop through all unidentified MS2 scans
                        BOOST_FOREACH(UnidentifiedPrecursorInfo& info, unidentifiedPrecursors)
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

                            info.chromatogram.MS1Intensity.push_back(sumIntensities);
                            info.chromatogram.MS1RT.push_back(curRT);
                        } // done with unidentified MS2 scans
                    }
                    else if (msLevel == 2) 
                    {
                        // Metric MS2-2: SNR of identified MS2 spectra
                        if (distinctModifiedPeptideByNativeID.count(spectrum->id) > 0) 
                        {
                            accs::accumulator_set<double, accs::stats<accs::tag::percentile, accs::tag::max, accs::tag::count> > ms2Peaks;
                            BOOST_FOREACH(const double& p, spectrum->getIntensityArray()->data)
                                ms2Peaks(p);
                            if(accs::count(ms2Peaks) > 0)
                                sigNoisMS2(accs::max(ms2Peaks) / accs::percentile(ms2Peaks, accs::percentile_number = 50));
                        }
                        
                        // calculate TIC manually if necessary
                        MS2ScanInfo& scanInfo = const_cast<MS2ScanInfo&>(*ms2ScanMap.get<nativeID>().find(spectrum->id));
                        if (scanInfo.precursorIntensity == 0)
                            BOOST_FOREACH(const double& p, spectrum->getIntensityArray()->data)
                                scanInfo.precursorIntensity += p;
                    }
                } // end of spectra loop

                // MS1-2A: Median SNR of MS1 spectra within C-2A
                medianSigNoisMS1 = accs::percentile(sigNoisMS1, accs::percentile_number = 50);

                // MS2-2: Median SNR of identified MS2 spectra
                medianSigNoisMS2 = accs::percentile(sigNoisMS2, accs::percentile_number = 50);
                
                // Find peaks with Crawdad
                if (g_numWorkers == 1) cout << endl;

                // cycle through all distinct matches, passing each one to crawdad
                size_t i = 0;
                BOOST_FOREACH(const XICWindow& window, pepWindow)
                {
                    ++i;
                    if (g_numWorkers == 1 && (i==pepWindow.size() || !(i%100))) cout << "\rFinding distinct match peaks: " << i << "/" << pepWindow.size() << flush;

                    if (window.MS1RT.empty())
                    {
                        cerr << "Warning: distinct match window for " << window.peptide
                             << " (id: " << window.PSMs[0].peptide
                             << "; m/z: " << window.preMZ
                             << "; time: " << window.preRT
                             << ") has no chromatogram data points!" << endl;
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
                        BOOST_FOREACH(const PeptideSpectrumMatch& psm, window.PSMs)
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

                        if (!window.bestPeak || fabs(window.bestPeak->peakTime - window.maxScoreScanStartTime) >
                                                fabs(peakTime - window.maxScoreScanStartTime))
                            window.bestPeak = peak;

                        // calculate sum of products between PSM score and interpolated SIC over 4 standard deviations
                        double wideStartTime = peakTime - peak.fwhm * 4 / 2.35482;
                        double wideEndTime = peakTime + peak.fwhm * 4 / 2.35482;
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
                            BOOST_FOREACH(const PeptideSpectrumMatch& psm, window.PSMs)
                                if (wideStartTime <= psm.spectrum->scanStartTime && psm.spectrum->scanStartTime <= wideEndTime)
                                    sumOfProducts += psm.score * interpolator.interpolate(window.MS1RT, window.MS1Intensity, psm.spectrum->scanStartTime);
                        }

                        if (sumOfProducts > 0)
                            peakByIntensityBySumOfProducts[sumOfProducts][peak.intensity] = peak;
                    }

                    if (!peakByIntensityBySumOfProducts.empty())
                        window.bestPeak = peakByIntensityBySumOfProducts.rbegin()->second.rbegin()->second;

                    if (!window.bestPeak)
                    {
                        cerr << "Warning: unable to select the best Crawdad peak for distinct match! " << window.peptide
                             << " (id: " << window.PSMs[0].peptide
                             << "; m/z: " << window.preMZ
                             << "; time: " << window.preRT
                             << ")" << endl;
                    }
                    //else
                        //cout << "\n" << firstMS2.nativeID << "\t" << window.peptide << "\t" << window.bestPeak->intensity << "\t" << window.bestPeak->peakTime << "\t" << firstMS2.precursorIntensity << "\t" << (window.bestPeak->intensity / firstMS2.precursorIntensity);
                }

                if (g_numWorkers == 1) cout << endl;

                // cycle through all unidentifed MS2 scans, passing each one to crawdad
                i = 0;
                BOOST_FOREACH(UnidentifiedPrecursorInfo& info, unidentifiedPrecursors)
                {
                    ++i;
                    if (g_numWorkers == 1 && (i==unidentifiedPrecursors.size() || !(i%100))) cout << "\rFinding unidentified peptide peaks: " << i << "/" << unidentifiedPrecursors.size() << flush;

                    LocalChromatogram& lc = info.chromatogram;
                    if (lc.MS1RT.empty())
                    {
                        cerr << "Warning: unidentified precursor m/z " << info.spectrum->precursorMZ
                             << " (m/z: " << info.mzWindow
                             << "; time: " << info.scanTimeWindow
                             << ") has no chromatogram data points!" << endl;
                        continue;
                    }

                    // make chromatogram data points evenly spaced
                    Interpolator(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity).resample(info.chromatogram.MS1RT, info.chromatogram.MS1Intensity);

                    // eliminate negative signal
                    BOOST_FOREACH(double& intensity, info.chromatogram.MS1Intensity)
                        intensity = max(0.0, intensity);

                    CrawdadPeakFinder crawdadPeakFinder;
                    crawdadPeakFinder.SetChromatogram(lc.MS1RT, lc.MS1Intensity);
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

                        if (!lc.bestPeak || fabs(peakTime - info.spectrum->scanStartTime) < fabs(lc.bestPeak->peakTime - info.spectrum->scanStartTime))
                            lc.bestPeak = peak;
                    }
                }

                // Write chromatograms for visualization of data
                if (g_rtConfig->ChromatogramOutput)
                    writeChromatograms(sourceFilename, ms2ScanMap, pepWindow, unidentifiedPrecursors);

                // Metric C-3A: Median peak width of identified spectra
                // Metric C-3B: IQR of peak width of identified spectra
                {
                    accs::accumulator_set<double, accs::stats<accs::tag::percentile> > identifiedPeakWidths;
                    BOOST_FOREACH(const XICWindow& distinctMatch, pepWindow)
                        if (distinctMatch.bestPeak)
                            for (size_t i=0; i < distinctMatch.PSMs.size(); ++i) // the best peak is shared between all PSMs
                                identifiedPeakWidths(distinctMatch.bestPeak->fwhm);
                    identifiedPeakWidthMedian = accs::percentile(identifiedPeakWidths, accs::percentile_number = 50);
                    identifiedPeakWidthIQR = accs::percentile(identifiedPeakWidths, accs::percentile_number = 75) -
                                             accs::percentile(identifiedPeakWidths, accs::percentile_number = 25);
                }

                {
                    accs::accumulator_set<double, accs::stats<accs::tag::percentile> > distinctMatchTimes;
                    BOOST_FOREACH(const XICWindow& distinctMatch, pepWindow)
                        distinctMatchTimes(distinctMatch.firstMS2RT);

                    // Metric C-4A: Median peak width of distinct matches in the 9th scan time decile
                    {
                        accs::accumulator_set<double, accs::stats<accs::tag::percentile> > distinctMatchPeakWidths;
                        double startTime = accs::percentile(distinctMatchTimes, accs::percentile_number = 90);
                        XICWindowList::index<time>::type::const_iterator itr = pepWindow.get<time>().lower_bound(startTime),
                                                                         end = pepWindow.get<time>().end();
                        for (; itr != end; ++itr)
                            if (itr->bestPeak)
                                distinctMatchPeakWidths(itr->bestPeak->fwhm);
                        medianDistinctMatchPeakWidthOver9thScanTimeDecile = accs::percentile(distinctMatchPeakWidths, accs::percentile_number = 50);
                    }

                    // Metric C-4B: Median peak width of distinct matches in the 1st scan time decile
                    {
                        accs::accumulator_set<double, accs::stats<accs::tag::percentile> > distinctMatchPeakWidths;
                        double endTime = accs::percentile(distinctMatchTimes, accs::percentile_number = 10);
                        XICWindowList::index<time>::type::const_iterator itr = pepWindow.get<time>().begin(),
                                                                         end = pepWindow.get<time>().lower_bound(endTime);
                        for (; itr != end; ++itr)
                            if (itr->bestPeak)
                                distinctMatchPeakWidths(itr->bestPeak->fwhm);
                        medianDistinctMatchPeakWidthOver1stScanTimeDecile = accs::percentile(distinctMatchPeakWidths, accs::percentile_number = 50);
                    }

                    // Metric C-4C: Median peak width of distinct matches in the 5th scan time decile
                    {
                        accs::accumulator_set<double, accs::stats<accs::tag::percentile> > distinctMatchPeakWidths;
                        double startTime = accs::percentile(distinctMatchTimes, accs::percentile_number = 50);
                        double endTime = accs::percentile(distinctMatchTimes, accs::percentile_number = 60);
                        XICWindowList::index<time>::type::const_iterator itr = pepWindow.get<time>().lower_bound(startTime),
                                                                         end = pepWindow.get<time>().lower_bound(endTime);
                        for (; itr != end; ++itr)
                            if (itr->bestPeak)
                                distinctMatchPeakWidths(itr->bestPeak->fwhm);
                        medianDistinctMatchPeakWidthOver5thScanTimeDecile = accs::percentile(distinctMatchPeakWidths, accs::percentile_number = 50);
                    }
                }

                // Metrics C-1[AB]: # of PSMs that occur +/- 4 minutes away from the chosen precursor peak
                int duplicatePeptides = 1;
                BOOST_FOREACH(const XICWindow& distinctMatch, pepWindow)
                {
                    if (distinctMatch.PSMs.size() == 1 || !distinctMatch.bestPeak)
                        continue;
                    duplicatePeptides += distinctMatch.PSMs.size() - 1;
                    BOOST_FOREACH(const PeptideSpectrumMatch& psm, distinctMatch.PSMs)
                    {
                        double timeDelta = distinctMatch.bestPeak->peakTime - psm.spectrum->scanStartTime;
                        if (timeDelta > 240)
                            ++bleed;
                        else if (timeDelta < -240)
                            ++peakTailing;
                    }
                }

                // Metric C-1A: Chromatographic bleeding
                bleedRatio = (float)bleed / duplicatePeptides;

                // Metric C-1B: Chromatographic peak tailing
                peakTailingRatio = (float)peakTailing / duplicatePeptides;

                // For metric DS-3A: Median ratio of precursor peak intensity to trigger intensity for all distinct matches
                {
                    accs::accumulator_set<double, accs::stats<accs::tag::percentile> > precursorPeakToTriggerIntensityRatios;
                    BOOST_FOREACH(const XICWindow& distinctMatch, pepWindow)
                        if (distinctMatch.bestPeak && distinctMatch.PSMs.front().spectrum->precursorIntensity > 0)
                            precursorPeakToTriggerIntensityRatios(distinctMatch.bestPeak->intensity / distinctMatch.PSMs.front().spectrum->precursorIntensity);
                    medianSamplingRatio = accs::percentile(precursorPeakToTriggerIntensityRatios, accs::percentile_number = 50);
                }

                // For metric DS-3B: Median ratio of precursor peak intensity to trigger intensity for distinct matches with trigger intensity less than the median
                {
                    accs::accumulator_set<double, accs::stats<accs::tag::percentile> > precursorTriggerIntensities;
                    BOOST_FOREACH(const XICWindow& distinctMatch, pepWindow)
                        if (distinctMatch.bestPeak && distinctMatch.PSMs.front().spectrum->precursorIntensity > 0)
                            precursorTriggerIntensities(distinctMatch.PSMs.front().spectrum->precursorIntensity);
                    double medianTriggerIntensity = accs::percentile(precursorTriggerIntensities, accs::percentile_number = 50);
                    
                    accs::accumulator_set<double, accs::stats<accs::tag::percentile> > precursorPeakToTriggerIntensityRatios;
                    BOOST_FOREACH(const XICWindow& distinctMatch, pepWindow)
                        if (distinctMatch.bestPeak &&
                            distinctMatch.PSMs.front().spectrum->precursorIntensity > 0 &&
                            distinctMatch.PSMs.front().spectrum->precursorIntensity < medianTriggerIntensity)
                            precursorPeakToTriggerIntensityRatios(distinctMatch.bestPeak->intensity / distinctMatch.PSMs.front().spectrum->precursorIntensity);
                    bottomHalfSamplingRatio = accs::percentile(precursorPeakToTriggerIntensityRatios, accs::percentile_number = 50);
                }

                // Find the peak precursor intensity quartiles for all MS2 spectra
                accs::accumulator_set<double, accs::stats<accs::tag::percentile> > ms2_peakPrecursorIntensities;
                BOOST_FOREACH(const XICWindow& distinctMatch, pepWindow)
                    if (distinctMatch.bestPeak)
                        for (size_t i=0; i < distinctMatch.PSMs.size(); ++i)
                            ms2_peakPrecursorIntensities(distinctMatch.bestPeak->intensity);
                BOOST_FOREACH(const UnidentifiedPrecursorInfo& info, unidentifiedPrecursors)
                    if (info.chromatogram.bestPeak)
                        ms2_peakPrecursorIntensities(info.chromatogram.bestPeak->intensity);
                double intensQ1 = accs::percentile(ms2_peakPrecursorIntensities, accs::percentile_number = 25);
                double intensQ2 = accs::percentile(ms2_peakPrecursorIntensities, accs::percentile_number = 50);
                double intensQ3 = accs::percentile(ms2_peakPrecursorIntensities, accs::percentile_number = 75);

                /*cout << endl << "Peak precursor intensity quartiles: ";
                for (int i=0; i <= 100; i += 25)
                    cout << " " << accs::percentile(ms2_peakPrecursorIntensities, accs::percentile_number = i);
                cout << endl;*/

                // number of identified MS2s that have precursor max intensities in each quartile
                BOOST_FOREACH(const XICWindow& window, pepWindow)
                {
                    if (!window.bestPeak) continue;
                    double x = window.bestPeak->intensity;
                    if (x <= intensQ1) idQ1 += window.PSMs.size();
                    else if (x <= intensQ2) idQ2 += window.PSMs.size();
                    else if (x <= intensQ3) idQ3 += window.PSMs.size();
                    else idQ4 += window.PSMs.size();
                }

                // number of unidentified MS2s that have precursor max intensities in each quartile
                int unidQ1=0, unidQ2=0, unidQ3=0, unidQ4=0;
                BOOST_FOREACH(const UnidentifiedPrecursorInfo& info, unidentifiedPrecursors)
                {
                    if (!info.chromatogram.bestPeak) continue;
                    double x = info.chromatogram.bestPeak->intensity;
                    if (x <= intensQ1) ++unidQ1;
                    else if (x <= intensQ2) ++unidQ2;
                    else if (x <= intensQ3) ++unidQ3;
                    else ++unidQ4;
                }

                //cout << endl << "Identified/unidentified MS2s with 1st quartile peak precursor intensities: " << idQ1 << "/" << unidQ1;
                //cout << endl << "Identified/unidentified MS2s with 2nd quartile peak precursor intensities: " << idQ2 << "/" << unidQ2;
                //cout << endl << "Identified/unidentified MS2s with 3rd quartile peak precursor intensities: " << idQ3 << "/" << unidQ3;
                //cout << endl << "Identified/unidentified MS2s with 4th quartile peak precursor intensities: " << idQ4 << "/" << unidQ4;

                totalQ1 = idQ1+unidQ1;
                totalQ2 = idQ2+unidQ2;
                totalQ3 = idQ3+unidQ3;
                totalQ4 = idQ4+unidQ4;

                // MS1-3A: Ratio of peak precursor intensity at 95th vs. 5th percentile of identified spectra
                peakPrecursorIntensity95thPercentile = accs::percentile(ms2_peakPrecursorIntensities, accs::percentile_number = 95);
                peakPrecursorIntensity5thPercentile = accs::percentile(ms2_peakPrecursorIntensities, accs::percentile_number = 5);
                dynamicRangeOfPeptideSignals = peakPrecursorIntensity95thPercentile/peakPrecursorIntensity5thPercentile;

                // MS1-3B: Median peak precursor intensity for identified spectra
                peakPrecursorIntensityMedian = accs::percentile(ms2_peakPrecursorIntensities, accs::percentile_number = 50);

                // MS2-4A: Fraction of MS2 scans identified in the first quartile of peptides sorted by MS1 max intensity
                idRatioQ1 = (double)idQ1/totalQ1;
                // MS2-4B: Fraction of MS2 scans identified in the second quartile of peptides sorted by MS1 max intensity
                idRatioQ2 = (double)idQ2/totalQ2;
                // MS2-4C: Fraction of MS2 scans identified in the third quartile of peptides sorted by MS1 max intensity
                idRatioQ3 = (double)idQ3/totalQ3;
                // MS2-4D: Fraction of MS2 scans identified in the fourth quartile of peptides sorted by MS1 max intensity
                idRatioQ4 = (double)idQ4/totalQ4;
            } // MetricType == nistms

            if (g_numWorkers == 1) cout << endl;

            string emptyMetric = "NaN"; // NaN stands for Not a Number

            // File for quameter output, default is to save to same directory as input file
            string outputFilepath = g_rtConfig->OutputFilepath;
            if (outputFilepath.empty())
                outputFilepath = bfs::change_extension(sourceFilename, ".qual.tsv").string();
            
            guard.lock();

            bool needsHeader = !bfs::exists(outputFilepath);

            ofstream qout;
            qout.open(outputFilepath.c_str(), ios::out | ios::app);

            // Tab delimited output header
            if (needsHeader)
            {
                qout << "Filename\tStartTimeStamp\tC-1A\tC-1B\tC-2A\tC-2B\tC-3A\tC-3B\tC-4A\tC-4B\tC-4C";
                qout << "\tDS-1A\tDS-1B\tDS-2A\tDS-2B\tDS-3A\tDS-3B";
                qout << "\tIS-1A\tIS-1B\tIS-2\tIS-3A\tIS-3B\tIS-3C";
                qout << "\tMS1-1\tMS1-2A\tMS1-2B\tMS1-3A\tMS1-3B\tMS1-5A\tMS1-5B\tMS1-5C\tMS1-5D";
                qout << "\tMS2-1\tMS2-2\tMS2-3\tMS2-4A\tMS2-4B\tMS2-4C\tMS2-4D";
                qout << "\tP-1\tP-2A\tP-2B\tP-2C\tP-3" << endl;
            }

            // Tab delimited metrics
            qout << sourceFilename;
            qout << "\t" << startTimeStamp;
            qout << "\t" << bleedRatio << "\t" << peakTailingRatio << "\t" << iqIDTime << "\t" << iqIDRate << "\t" << identifiedPeakWidthMedian << "\t" << identifiedPeakWidthIQR;
            qout << "\t" << medianDistinctMatchPeakWidthOver9thScanTimeDecile  << "\t" << medianDistinctMatchPeakWidthOver1stScanTimeDecile  << "\t" << medianDistinctMatchPeakWidthOver5thScanTimeDecile;
            qout << "\t" << DS1A << "\t" << DS1B << "\t" << iqMS1Scans << "\t" << iqMS2Scans;
            qout << "\t" << medianSamplingRatio << "\t" << bottomHalfSamplingRatio;
            qout << "\t" << ticDrop << "\t" << ticJump << "\t" << medianPrecursorMZ << "\t" << IS3A << "\t" << IS3B << "\t" << IS3C;
            if (accs::count(ms1IonInjectionTimes)==0)
                qout << "\t" << emptyMetric;
            else
                qout << "\t" << medianInjectionTimeMS1;
            qout << "\t" << medianSigNoisMS1 << "\t" << medianTIC << "\t" << dynamicRangeOfPeptideSignals << "\t" << peakPrecursorIntensityMedian;
            qout << "\t" << precMassErrorStats.medianError << "\t" << precMassErrorStats.meanAbsError << "\t" << precMassErrorStats.medianPPMError << "\t" << precMassErrorStats.PPMErrorIQR;
            if (accs::count(ms2IonInjectionTimes)==0)
                qout << "\t" << emptyMetric;
            else
                qout << "\t" << medianInjectionTimeMS2;
            qout << "\t" << medianSigNoisMS2 << "\t" << medianNumMS2Peaks;
            qout << "\t" << idRatioQ1 << "\t" << idRatioQ2 << "\t" << idRatioQ3 << "\t" << idRatioQ4;
            qout << "\t" << medianIDScore << "\t" << numTrypticMS2Spectra << "\t" << numTrypticPeptides;
            qout << "\t" << distinctPeptideCountBySpecificity[2] << "\t" << ratioSemiToFullyTryptic << endl;

            cout << sourceFilename << " took " << processingTimer.elapsed() << " seconds to analyze.\n";
            guard.unlock();
        }
        catch (exception& e) 
        {
            cerr << "\nError processing metrics: " << e.what() << endl;
        }
        catch (...) 
        {
            cerr << "\nUnhandled error processing metrics." << endl;
            exit(1); // fear the unknown!
        }
        return;
    }
}
}

/**
 * Parses command line arguments and initialization files.
 * If multiple input files are given, creates a thread for each up to the computer's
 * thread limit and performs metrics on them.
 */
int main( int argc, char* argv[] ) 
{
    g_numProcesses = 1;
    g_pid = 0;

    int result = 0;
    try
    {
        result = quameter::ProcessHandler( argc, argv );
    } catch (pwiz::util::usage_exception& e)
    {
        cerr << e.what() << endl;
        result = 0;
    } catch (std::exception& e)
    {
        cerr << e.what() << endl;
        result = 1;
    } catch( ... )
    {
        cerr << "Caught unspecified fatal exception." << endl;
        result = 1;
    }
    
    return result;
    
}

