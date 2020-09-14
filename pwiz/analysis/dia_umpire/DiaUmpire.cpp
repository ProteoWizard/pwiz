//
// Java author: Chih-Chiang Tsou <chihchiang.tsou@gmail.com>
//              Nesvizhskii Lab, Department of Computational Medicine and Bioinformatics
//
// Copyright 2014 University of Michigan, Ann Arbor, MI
//
//
// C++ port: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2020 Matt Chambers
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

#define PWIZ_SOURCE
//#define DIAUMPIRE_DEBUG 1

#include <boost/range/iterator_range_core.hpp>
#include <boost/asio/thread_pool.hpp>
#include <boost/asio/post.hpp>
#include <boost/thread.hpp>
#include <boost/container/flat_set.hpp>
#include <atomic>
#include <memory>
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "PeakCurve.hpp"
#include "PeakCluster.hpp"
#include "DiaUmpire.hpp"
#include "IsotopePatternMap.hpp"


using namespace pwiz::msdata;
using namespace pwiz::util;
namespace bg = boost::geometry;
namespace bgi = bg::index;

namespace DiaUmpire {

    MzRange MzRange::Empty = MzRange();

    struct DiaWindow : public TargetWindow
    {
        DiaWindow(TargetWindow const& target, TargetWindow const* nextTarget = nullptr)
            : TargetWindow(target.mzRange), nextWindowMzRange(nextTarget ? nextTarget->mzRange : MzRange::Empty)
        {
            spectraInRange = target.spectraInRange;
        }

        void PrecursorFragmentPairBuildingForMS1(DiaUmpire::Impl const& diaUmpire);
        void PrecursorFragmentPairBuildingForUnfragmentedIon(DiaUmpire::Impl const& diaUmpire);

        MzRange nextWindowMzRange;

        std::vector<PeakCurvePtr> peakCurves;
        std::vector<PeakClusterPtr> peakClusters;
        map<int, vector<float>> FragmentMS1Ranking, FragmentUnfragRanking;
        map<size_t, vector<PrecursorFragmentPairEdge>> FragmentsClu2Cur, UnFragIonClu2Cur;
    };

    namespace {
        enum class DiaUmpireStep
        {
            InlineStep = 0,
            AssignSpectraToWindows = 1,
            ReadAllSpectra,
            BuildPeakCurves,
            SmoothPeakCurves,
            ClusterPeakCurves,
            ReadMs2Spectra,
            ProcessDiaWindows,
            Count
        };
    }

    class DiaUmpire::Impl
    {
        public:

        Impl(const MSData& msd, const SpectrumListPtr& spectrumList, const Config& config, const IterationListenerRegistry* ilr);
        bool BuildDIAWindows();
        bool MS1PeakDetection();
        bool DIAMS2PeakDetection();

        std::vector<PseudoMsMsKey> outputScanKeys;
        map<string, shared_ptr<TemporaryFile>> spillFiles;

        private:

        IsotopePatternMap isotopePatternMap_;

        bool PeakCurveSmoothing(vector<PeakCurvePtr>& peakCurves, bool multithreaded = true);
        bool PeakCurveCorrClustering(MzRange mzRange, vector<PeakCurvePtr>& peakCurves, vector<PeakClusterPtr>& peakClusters, int msLevel, bool multithreaded = true);
        bool FindAllMzTracePeakCurves(const ScanCollection& scanCollection, vector<PeakCurvePtr>& peakCurves, float ppmTolerance, int msLevel,
                                      DiaUmpireStep step = DiaUmpireStep::InlineStep, const vector<size_t>& scanIndices = vector<size_t>());
        ScanCollectionPtr GetAllScanCollectionByMSLabel(bool MS1Included, bool MS2Included, bool MS1Peak, bool MS2Peak, float startTime, float endTime, DiaUmpireStep step);
        ScanCollectionPtr GetScanCollectionMS1Window(const TargetWindow& MS1Window, bool IncludePeak, float startTime, float endTime);

        bool FoundInInclusionList(float mz, float startTime, float endTime) const { return false; }
        bool FoundInInclusionRTList(float scanTime) const { throw std::logic_error("not implemented"); }
        bool FoundInInclusionMZList(float scanTime, float mz) const { throw std::logic_error("not implemented"); }

        bool iterateAndCheckCancellation(int index, int size, const string& msg, DiaUmpireStep step) const
        {
            if (!ilr_)
                return false;

            string msgWithStep = "[step " + lexical_cast<string>(int(step)) + " of " + lexical_cast<string>(int(DiaUmpireStep::Count)-1) + "] " + msg;

            boost::lock_guard<boost::mutex> g(ilrMutex_);
            return IterationListener::Status_Cancel == ilr_->broadcastUpdateMessage(IterationListener::UpdateMessage(index, size, msgWithStep));
        }

        const pwiz::msdata::MSData& msd_;
        pwiz::msdata::SpectrumListPtr slp_;
        const pwiz::msdata::SpectrumList& sl_;

        MSData msdQ1, msdQ2, msdQ3;
        SpectrumListSimple *slQ1, *slQ2, *slQ3;

        Config config_;

        std::vector<TargetWindowPtr> diaTargetWindows_, ms1WindowScheme_;
        std::vector<DiaWindow> diaWindows_;
        std::vector<PeakCurvePtr> ms1PeakCurves_;
        std::vector<PeakClusterPtr> ms1PeakClusters_;

        int ms1Count_, ms2Count_;
        float ms1CycleTime_;

        std::map<float, size_t> indexByScanTime_;
        std::vector<std::pair<int, float>> msLevelAndScanTimeByIndex_;
        const pwiz::util::IterationListenerRegistry* ilr_;
        mutable boost::mutex ilrMutex_;

        friend struct DiaWindow;
    };


    DiaUmpire::Impl::Impl(const MSData& msd, const SpectrumListPtr& spectrumList, const Config& config, const IterationListenerRegistry* ilr)
        : msd_(msd), slp_(spectrumList), sl_(*slp_), config_(config), ilr_(ilr)
    {
        isotopePatternMap_ = generateIsotopePatternMap(config_.instrumentParameters);

        if (config_.exportSeparateQualityMGFs)
        {
            slQ1 = new SpectrumListSimple;
            slQ2 = new SpectrumListSimple;
            slQ3 = new SpectrumListSimple;
            msdQ1.run.spectrumListPtr.reset(slQ1);
            msdQ2.run.spectrumListPtr.reset(slQ2);
            msdQ3.run.spectrumListPtr.reset(slQ3);
        }

#ifdef DIAUMPIRE_DEBUG
        vector<bfs::path> debugFilepaths;
        pwiz::util::expand_pathmask("c:/pwiz.git/pwiz/DiaUmpireCpp*", debugFilepaths);
        for (auto filepath : debugFilepaths)
            bfs::remove(filepath);
#endif

        if (!BuildDIAWindows()) return;
        if (!MS1PeakDetection()) return;
        if (!DIAMS2PeakDetection()) return;
        
        ms1PeakClusters_.clear();
        ms1PeakCurves_.clear();

        if (config_.exportSeparateQualityMGFs)
        {
            MSDataFile::WriteConfig writeConfig;
            writeConfig.format = MSDataFile::Format_MGF;
            MSDataFile::write(msdQ1, bfs::path(msd_.run.id).filename().string() + "-Q1.mgf", writeConfig);
            MSDataFile::write(msdQ2, bfs::path(msd_.run.id).filename().string() + "-Q2.mgf", writeConfig);
            MSDataFile::write(msdQ3, bfs::path(msd_.run.id).filename().string() + "-Q3.mgf", writeConfig);
        }
    }

    bool DiaUmpire::Impl::BuildDIAWindows()
    {
        DetailLevel msLevelDetailLevel = sl_.min_level_accepted([](const Spectrum& s) { return s.hasCVParam(MS_ms_level); });
        DetailLevel buildWindowsDetailLevel = msLevelDetailLevel;

        try
        {
            buildWindowsDetailLevel = sl_.min_level_accepted([](const Spectrum& s) -> boost::tribool
            {
                if (s.cvParam(MS_ms_level).valueAs<int>() == 1)
                    return boost::indeterminate;
                return !s.precursors.empty() && (s.precursors[0].isolationWindow.hasCVParam(MS_isolation_window_target_m_z) ||
                    !s.precursors[0].selectedIons.empty() &&
                    s.precursors[0].selectedIons[0].hasCVParam(MS_selected_ion_m_z));
            });
        }
        catch (runtime_error&)
        {
            throw runtime_error("[DiaUmpire::BuildDIAWindows] no MS2 spectra with isolation window target m/z");
        }

        map<MzRange, TargetWindow*> targetWindowByMzRange; // default ctor is nullptr

        // copy variable windows from config to DiaUmpire
        if (config_.diaTargetWindowScheme == TargetWindow::Scheme::SWATH_Variable)
            for (auto& window : config_.diaVariableWindows)
            {
                diaTargetWindows_.push_back(std::make_unique<TargetWindow>(window));
                targetWindowByMzRange[window.mzRange] = diaTargetWindows_.back().get();
            }

        ms1Count_ = 0;
        ms2Count_ = 0;

        string progressMessage = "assigning spectra to DIA windows";

        // iterate spectra to assign them to windows (SWATH fixed scheme creates the windows on the fly)
        for (size_t i = 0, end = sl_.size(); i < end; ++i)
        {
            SpectrumPtr s = sl_.spectrum(i, buildWindowsDetailLevel);
            if (s->hasCVParam(MS_profile_spectrum))
                throw runtime_error("[DiaUmpire::BuildDIAWindows] DIA Umpire requires centroided spectra; use the peakPicking filter");

            if (iterateAndCheckCancellation(i, end, progressMessage, DiaUmpireStep::AssignSpectraToWindows))
                return false;

            int msLevel = s->cvParamValueOrDefault<int>(MS_ms_level, 0);

            if (s->scanList.empty())
                continue;

            float scanTime = s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds() / 60;
            msLevelAndScanTimeByIndex_.emplace_back(make_pair(msLevel, scanTime));
            indexByScanTime_[scanTime] = i;

            if (msLevel == 1)
                ++ms1Count_;

            if (msLevel < 2)
                continue;
            if (s->precursors.empty())
                continue;

            float isolationWindowTargetMz = s->precursors[0].isolationWindow.cvParamValueOrDefault<float>(MS_isolation_window_target_m_z, 0);
            if (isolationWindowTargetMz == 0 && !s->precursors[0].selectedIons.empty())
            {
                isolationWindowTargetMz = s->precursors[0].selectedIons[0].cvParamValueOrDefault<float>(MS_selected_ion_m_z, 0);
                if (isolationWindowTargetMz == 0)
                    continue;
            }

            ++ms2Count_;

            float isolationWindowLowerOffset = s->precursors[0].isolationWindow.cvParamValueOrDefault<float>(MS_isolation_window_lower_offset, 0);
            float isolationWindowUpperOffset = s->precursors[0].isolationWindow.cvParamValueOrDefault<float>(MS_isolation_window_upper_offset, 0);

            if (config_.diaTargetWindowScheme == TargetWindow::Scheme::SWATH_Variable)
            {
                for (auto& window : diaTargetWindows_)
                {
                    if (window->mzRange.begin <= isolationWindowTargetMz && window->mzRange.end >= isolationWindowTargetMz)
                    {
                        window->spectraInRange.push_back(i);
                        break;
                    }
                }
            }
            else
            {
                if (isolationWindowLowerOffset == 0 || isolationWindowUpperOffset == 0)
                {
                    isolationWindowLowerOffset = (config_.diaFixedWindowSize + 1) * 0.2f;
                    isolationWindowUpperOffset = (config_.diaFixedWindowSize + 1) * 0.8f;
                }

                MzRange mzRange{ isolationWindowTargetMz - isolationWindowLowerOffset, isolationWindowTargetMz + isolationWindowUpperOffset };
                auto& windowPtr = targetWindowByMzRange[mzRange];
                if (windowPtr == nullptr)
                {
                    diaTargetWindows_.push_back(std::make_unique<TargetWindow>(mzRange));
                    windowPtr = diaTargetWindows_.back().get();
                }

                windowPtr->spectraInRange.push_back(i);
            }
        }

        if (ms1Count_ == 0)
            throw runtime_error("[DiaUmpire::BuildDIAWindows] no MS1 scans detected; they are required for DIA Umpire to work");

        ms1CycleTime_ = (indexByScanTime_.rbegin()->first - indexByScanTime_.begin()->first) / ms1Count_;

        if (diaTargetWindows_.empty())
            throw runtime_error("[DiaUmpire::BuildDIAWindows] no target windows");

        // DIA Umpire processes windows in descending order of m/z?
        sort(diaTargetWindows_.begin(), diaTargetWindows_.end(), [&](auto const& lhs, auto const& rhs) { return lhs->mzRange.begin > rhs->mzRange.begin; });
        for (int i = 0; i + 1 < diaTargetWindows_.size(); ++i)
        {
            auto& window = *diaTargetWindows_[i];
            if (window.spectraInRange.empty())
            {
                if (config_.diaTargetWindowScheme == TargetWindow::Scheme::SWATH_Variable)
                    cerr << "Warning: DIA window [" << window.mzRange.begin << "-" << window.mzRange.end << "] has no spectra assigned to it; are the variable windows set correctly?" << endl;
                continue;
            }
            diaWindows_.emplace_back(DiaWindow(window, diaTargetWindows_[i + 1].get()));
        }
        diaWindows_.emplace_back(DiaWindow(*diaTargetWindows_.back()));

        return true;
    }

    bool DiaUmpire::Impl::FindAllMzTracePeakCurves(const ScanCollection& scanCollection, vector<PeakCurvePtr>& peakCurves, float ppmTolerance, int msLevel, DiaUmpireStep step, const vector<size_t>& scanIndices)
    {
        boost::container::flat_set<pair<int, float>> IncludedHashMap;

        IncludedHashMap.reserve(scanCollection.GetNumPeaks() / 2);

        float preRT = 0;
        float SNR = msLevel == 1 ? config_.instrumentParameters.SNThreshold : config_.instrumentParameters.MS2SNThreshold;
        string progressMessage = "building peak curves";

        const auto& scansForMsLevel = scanIndices.empty() ? scanCollection.GetScanNoArray(msLevel) : scanIndices;
#ifdef DIAUMPIRE_DEBUG
        ofstream curvesLog(("DiaUmpireCpp-peaks-ms" + lexical_cast<string>(msLevel) + ".txt").c_str(), std::ios::app);
#endif
        //Loop for each scan in the ScanCollection
        for (int scanIdx = 0; scanIdx < scansForMsLevel.size(); ++scanIdx)
        {
            if (msLevel == 1 && iterateAndCheckCancellation(scanIdx, scansForMsLevel.size(), progressMessage, step))
                return false;

            const ScanData* scanPtr = scanCollection.GetScan(scansForMsLevel[scanIdx]);
            if (!scanPtr)
                continue; // scan not included in collection
            auto& scan = *scanPtr;

            float scanTime = scan.RetentionTime;

            //If we are doing targeted peak detection and the RT of current scan is not in the range of targeted list, jump to the next scan 
            if (config_.instrumentParameters.TargetIDOnly && !FoundInInclusionRTList(scanTime))
                continue;

            if (preRT == 0)
                preRT = scanTime - 0.01f;

            for (int peakIdx = 0; peakIdx < scan.Data.size(); ++peakIdx)
            {
                const auto& peak = scan.get(peakIdx);

                //If we are doing targeted peak detection and the RT and m/z of current peak is not in the range of targeted list, jump to the next peak 
                if (config_.instrumentParameters.TargetIDOnly && !FoundInInclusionMZList(scan.ScanNum, peak.mz))
                    continue;

                if (peak.mz < config_.instrumentParameters.MinMZ)
                    continue;

                auto insertResult = IncludedHashMap.insert(make_pair(scan.ScanNum, peak.mz));
                if (!insertResult.second)
                    continue; // scan/mz pair already processed

                //The current peak will be the starting peak of a new peak curve

                float startmz = peak.mz;
                float startint = peak.intensity;

                //Find the maximum peak within PPM window as the starting peak
                for (size_t k = peakIdx + 1; k < scan.Data.size(); ++k)
                {
                    const auto& nextPeak = scan.get(k);

                    if (InstrumentParameter::CalcPPM(nextPeak.mz, startmz) > ppmTolerance)
                        break;

                    auto insertResult = IncludedHashMap.insert(make_pair(scan.ScanNum, nextPeak.mz));
                    if (!insertResult.second)
                        continue; // scan/mz pair already processed

                    if (nextPeak.intensity >= startint)
                    {
                        startmz = nextPeak.mz;
                        startint = nextPeak.intensity;
                    }
                }

                //Initialize a new peak curve
                PeakCurvePtr PeakcurvePtr = std::make_unique<PeakCurve>(config_.instrumentParameters);
                PeakCurve& Peakcurve = *PeakcurvePtr;
                Peakcurve.MsLevel = msLevel;

                //Add a background peak
                Peakcurve.AddPeak(XYZData{ preRT, startmz, scan.background });
                //Add the starting peak
                Peakcurve.AddPeak(XYZData{ scan.RetentionTime, startmz, startint });
                Peakcurve.StartScan = scan.ScanNum;

                int missedScan = 0;
                float endrt = scan.RetentionTime;
                int endScan = scan.ScanNum;
                float bk = 0;

                //Starting from the next scan, find the following peaks given the starting peak
                for (int scan2Idx = scanIdx + 1; scan2Idx < scansForMsLevel.size() && (missedScan < config_.instrumentParameters.NoMissedScan /*|| (TargetedOnly && Peakcurve.RTWidth()<parameter.MaxCurveRTRange)*/); scan2Idx++)
                {
                    const ScanData* scan2Ptr = scanCollection.GetScan(scansForMsLevel[scan2Idx]);
                    if (!scan2Ptr)
                        break; // scan probably not included in ScanCollection
                    auto& scanData2 = *scan2Ptr;
                    int scanNO2 = scanData2.ScanNum;

                    endrt = scanData2.RetentionTime;
                    endScan = scanData2.ScanNum;
                    bk = scanData2.background;
                    float currentmz = 0;
                    float currentint = 0;

                    //If the scan is empty
                    if (scanData2.PointCount() == 0) {
                        if (config_.instrumentParameters.FillGapByBK) {
                            Peakcurve.AddPeak(XYZData{ scanData2.RetentionTime, Peakcurve.TargetMz, scanData2.background });
                        }
                        missedScan++;
                        continue;
                    }

                    //Find the m/z index 
                    int mzidx = scanData2.GetLowerIndexOfX(Peakcurve.TargetMz);
                    for (int pkidx = mzidx; pkidx < scanData2.Data.size(); pkidx++)
                    {
                        XYData currentpeak = scanData2.get(pkidx);
                        if (currentpeak.getX() < config_.instrumentParameters.MinMZ)
                            continue;

                        //Check if the peak has been included or not
                        if (IncludedHashMap.count(make_pair(scanNO2, currentpeak.mz)) > 0)
                            continue;

                        if (InstrumentParameter::CalcPPM(currentpeak.getX(), Peakcurve.TargetMz) > ppmTolerance) {
                            if (currentpeak.getX() > Peakcurve.TargetMz) {
                                break;
                            }
                        }
                        else {
                            //////////The peak is in the ppm window, select the highest peak
                            IncludedHashMap.insert(make_pair(scanNO2, currentpeak.mz));
                            if (currentint < currentpeak.getY()) {
                                currentmz = currentpeak.getX();
                                currentint = currentpeak.getY();
                            }
                        }
                    }

                    //No peak in the PPM window has been found
                    if (currentmz == 0) {
                        if (config_.instrumentParameters.FillGapByBK) {
                            Peakcurve.AddPeak(XYZData{ scanData2.RetentionTime, Peakcurve.TargetMz, scanData2.background });
                        }
                        missedScan++;
                    }
                    else {
                        missedScan = 0;
                        Peakcurve.AddPeak(XYZData{ scanData2.RetentionTime, currentmz, currentint });
                    }
                }
                Peakcurve.AddPeak(XYZData{ endrt, Peakcurve.TargetMz, bk });
                Peakcurve.EndScan = endScan;

                //First check if the peak curve is in targeted list
                if (FoundInInclusionList(Peakcurve.TargetMz, Peakcurve.StartRT(), Peakcurve.EndRT()))
                {
                    peakCurves.push_back(std::move(PeakcurvePtr));
                    //Then check if the peak curve passes the criteria
                }
                else if (Peakcurve.GetRawSNR() > SNR && Peakcurve.GetPeakList().size() >= config_.instrumentParameters.MinPeakPerPeakCurve + 2)
                {
#ifdef DIAUMPIRE_DEBUG
                    boost::format pkFormat(" (%.4f, %.2f)");
                    curvesLog << (boost::format("%d %d %d %d %.4f %.4f %.8f") % Peakcurve.StartScan % Peakcurve.EndScan % IncludedHashMap.size() % Peakcurve.GetPeakList().size() % Peakcurve.ApexInt % Peakcurve.ApexRT % Peakcurve.TargetMz).str();
                    for (auto pt : Peakcurve.GetPeakList())
                        curvesLog << (pkFormat % pt.x % pt.z).str();
                    curvesLog << "\n";
#endif
                    peakCurves.push_back(std::move(PeakcurvePtr));
                }
            }
            preRT = scan.RetentionTime;
        }

        int i = 1;
        //Assign peak curve index
        for (PeakCurvePtr& peakCurve : peakCurves)
            peakCurve->Index = i++;

        return true;
    }

    template <typename T, typename ValueT>
    std::decay_t<ValueT> map_lower_bound_or(const T& container, typename T::key_type const& key, ValueT&& defaultValue)
    {
        auto itr = container.lower_bound(key);
        if (itr == container.end())
            return std::move(defaultValue);
        return std::decay_t<ValueT>(itr->second);
    }

    ScanCollectionPtr DiaUmpire::Impl::GetAllScanCollectionByMSLabel(bool MS1Included, bool MS2Included, bool MS1Peak, bool MS2Peak, float startTime, float endTime, DiaUmpireStep step)
    {
        size_t startIndex = map_lower_bound_or(indexByScanTime_, startTime, 0);
        size_t endIndex = map_lower_bound_or(indexByScanTime_, endTime, indexByScanTime_.rbegin()->second);

        if (startIndex > endIndex) // sanity check
            swap(startIndex, endIndex);

        auto result = std::make_unique<ScanCollection>();

        boost::asio::thread_pool pool(4); // 4 threads
        boost::mutex m;
        vector<string> msLevels;
        if (MS1Included) msLevels.emplace_back("MS1");
        if (MS2Included) msLevels.emplace_back("MS2");
        string progressMessage = "reading " + bal::join(msLevels, "/") + " spectra into scan collection";

        for (size_t index = startIndex; index <= endIndex; ++index)
        {
            auto s = sl_.spectrum(index, true);

            boost::asio::post(pool, [&, index, s]
            {
                int msLevel = msLevelAndScanTimeByIndex_[s->index].first;
                if (MS1Included && msLevel == 1 ||
                    MS2Included && msLevel == 2)
                {
                    ScanData* scan;
                    {
                        boost::lock_guard<boost::mutex> g(m);
                        scan = &result->AddScan(s);
                    }
                    scan->Preprocessing(config_.instrumentParameters);

                    if (iterateAndCheckCancellation(index, endIndex + 1, progressMessage, step))
                        return;
                }
            });
        }
        pool.join();

        if (iterateAndCheckCancellation(endIndex, endIndex + 1, progressMessage, step))
            return nullptr;

        return result;
    }

    ScanCollectionPtr DiaUmpire::Impl::GetScanCollectionMS1Window(const TargetWindow& MS1Window, bool IncludePeak, float startTime, float endTime)
    {
        return nullptr;
    }

    bool DiaUmpire::Impl::PeakCurveSmoothing(vector<PeakCurvePtr>& peakCurves, bool multithreaded)
    {
#ifdef DIAUMPIRE_DEBUG
        boost::asio::thread_pool pool(1);// multithreaded ? config_.maxThreads : max(4, config_.maxThreads) / 4);
#else
        boost::asio::thread_pool pool(multithreaded ? config_.maxThreads : max(4, config_.maxThreads) / 4);
#endif
        boost::mutex m;
        vector<PeakCurvePtr> resultCurves;
        std::atomic<int> curvesSmoothed(0);
        string progressMessage = "smoothing peak curves";

        for (const PeakCurvePtr& curve : peakCurves)
        {
            boost::asio::post(pool, [&, curve]() mutable
            {
                if (multithreaded && iterateAndCheckCancellation(curvesSmoothed, peakCurves.size(), progressMessage, DiaUmpireStep::SmoothPeakCurves))
                    return;

                curve->DoBspline();

                if (config_.instrumentParameters.DetectByCWT)
                {
                    curve->DetectPeakRegion();
                    auto separateRegionPeakCurves = curve->SeparatePeakByRegion(config_.instrumentParameters.SNThreshold);

                    boost::lock_guard<boost::mutex> g(m);
                    for (PeakCurvePtr& result : separateRegionPeakCurves)
                    {
                        //result->ReleaseRawPeak();
                        resultCurves.push_back(result);
                    }
                }
                else
                {
                    boost::lock_guard<boost::mutex> g(m);
                    resultCurves.push_back(curve);
                    //curve->ReleaseRawPeak();
                }

                ++curvesSmoothed;
            });
        }
        pool.join();

        if (multithreaded && iterateAndCheckCancellation(peakCurves.size(), peakCurves.size(), progressMessage, DiaUmpireStep::SmoothPeakCurves))
            return false;

        swap(peakCurves, resultCurves);

        sort(peakCurves.begin(), peakCurves.end(), [](auto&& lhs, auto&& rhs) { return lhs->TargetMz == rhs->TargetMz ? lhs->ApexRT < rhs->ApexRT : lhs->TargetMz < rhs->TargetMz; });

        //map<int, int> oldIndexByIndex;
        int i = 1;
        for (PeakCurvePtr& peakCurve : peakCurves)
        {
            //oldIndexByIndex[peakCurve->Index] = peakCurve->Index;
            //oldIndexByIndex[i] = peakCurve->Index;
            peakCurve->Index = i++;
        }

#ifdef DIAUMPIRE_DEBUG
        if (multithreaded && peakCurves[0]->MsLevel == 2)
        {
            boost::lock_guard<boost::mutex> g(m);
            ofstream smoothLog("DiaUmpireCpp-peaks-smoothed.txt", std::ios::app);
            for (const auto& itr : peakCurves)
            {
                auto Peakcurve = *itr;
                smoothLog << (boost::format("%d %.4f %.4f %.8f %d %d\n") % oldIndexByIndex[Peakcurve.Index] % Peakcurve.ApexInt % Peakcurve.ApexRT % Peakcurve.TargetMz % Peakcurve.GetSmoothedList().size() % Peakcurve.GetPeakList().size()).str();
            }
        }
#endif
        return true;
    }

    bool DiaUmpire::Impl::PeakCurveCorrClustering(MzRange mzRange, vector<PeakCurvePtr>& peakCurves, vector<PeakClusterPtr>& peakClusters, int msLevel, bool multithreaded)
    {
        int MaxNoPeakCluster;
        int MinNoPeakCluster;
        int StartCharge;
        int EndCharge;
        float MiniIntensity;
        float SNR;

        if (msLevel == 1)
        {
            MaxNoPeakCluster = config_.instrumentParameters.MaxNoPeakCluster;
            MinNoPeakCluster = config_.instrumentParameters.MinNoPeakCluster;
            StartCharge = config_.instrumentParameters.StartCharge;
            EndCharge = config_.instrumentParameters.EndCharge;
            MiniIntensity = config_.instrumentParameters.MinMSMSIntensity;
            SNR = config_.instrumentParameters.MS2SNThreshold;
        }
        else
        {
            MaxNoPeakCluster = config_.instrumentParameters.MaxMS2NoPeakCluster;
            MinNoPeakCluster = config_.instrumentParameters.MinMS2NoPeakCluster;
            StartCharge = config_.instrumentParameters.MS2StartCharge;
            EndCharge = config_.instrumentParameters.MS2EndCharge;
            MiniIntensity = config_.instrumentParameters.MinMSMSIntensity;
            SNR = config_.instrumentParameters.MS2SNThreshold;
        }

        // generate peak curve search tree
        vector<PeakCurveTreeNode> peakCurveSearchNodes;
        peakCurveSearchNodes.reserve(peakCurves.size());
        for (PeakCurvePtr& Peakcurve : peakCurves)
            peakCurveSearchNodes.emplace_back(PeakCurveTreeNode(ApexRtTargetMzPoint(Peakcurve->ApexRT, Peakcurve->TargetMz), Peakcurve));
        PeakCurveSearchTree peakCurveSearchTree(peakCurveSearchNodes);

        ChiSquareGOF chiSquaredGof(MaxNoPeakCluster);

        boost::asio::thread_pool pool(multithreaded ? config_.maxThreads : max(4, config_.maxThreads) / 4);
        boost::mutex m;
        vector<PeakCurveClusteringCorrKDtree> clusterJobs;
        clusterJobs.reserve(peakCurves.size());
        std::atomic<int> curvesToCluster(0), curvesClustered(0);
        string progressMessage = "clustering peak curves";

        //For each peak curve
        for (size_t targetCurveIndex = 0; targetCurveIndex < peakCurves.size(); ++targetCurveIndex)
        {
            auto& Peakcurve = peakCurves[targetCurveIndex];
            if (Peakcurve->TargetMz < mzRange.begin || Peakcurve->TargetMz > mzRange.end)
                continue;

            ++curvesToCluster;

            //Create a thread unit for doing isotope clustering given a peak curve as the monoisotope peak
            clusterJobs.emplace_back(peakCurves, targetCurveIndex, peakCurveSearchTree, config_.instrumentParameters, isotopePatternMap_, chiSquaredGof,
                                     StartCharge, EndCharge, MaxNoPeakCluster, MinNoPeakCluster);

        }

        for (size_t i = 0; i < clusterJobs.size(); ++i)
        {
            boost::asio::post(pool, [&, i]
            {
                if (multithreaded && iterateAndCheckCancellation(curvesClustered, curvesToCluster, progressMessage, DiaUmpireStep::ClusterPeakCurves))
                    return;

                clusterJobs[i]();

                ++curvesClustered;
            });
        }
        pool.join();

        if (multithreaded && iterateAndCheckCancellation(curvesClustered, curvesToCluster, progressMessage, DiaUmpireStep::ClusterPeakCurves))
            return false;

        for (auto& unit : clusterJobs)
        {
            for (PeakClusterPtr peakCluster : unit.ResultClusters)
            {
                //Check if the monoistope peak of cluster has been grouped in other isotope cluster, if yes, remove the peak cluster
                if (!config_.instrumentParameters.RemoveGroupedPeaks || peakCluster->MonoIsotopePeak->ChargeGrouped.count(peakCluster->Charge) == 0)
                {
                    peakCluster->Index = peakClusters.size() + 1;
                    peakCluster->GetConflictCorr();
                    peakCluster->StartScan = map_lower_bound_or(indexByScanTime_, peakCluster->startRT, -1);
                    peakCluster->EndScan = map_lower_bound_or(indexByScanTime_, peakCluster->endRT, -1);
                    peakClusters.emplace_back(peakCluster);
                }
            }
        }

        return true;
    }


    void ExportPeakClusterResultCSV(string id, vector<PeakClusterPtr> const& peakClusters)
    {
        if (peakClusters.empty())
            return;
        ofstream peakClusterCsv("DiaUmpireCpp_PeakCluster.csv", std::ios::app);

        string mzstring = "";
        string Idxstring = "";
        string Corrstring = "";
        string SNRstring = "";
        string Peakheightstring = "";
        string PeakheightRTstring = "";
        string PeakAreastring = "";
        string Identifiedstring = "0";

        for (int i = 0; i < peakClusters.at(0)->mz.size(); i++) {
            mzstring += ",mz" + lexical_cast<string>(i + 1);
            Idxstring += ",PeakIdx" + lexical_cast<string>(i + 1);
            if (i > 0) {
                Corrstring += ",Corr" + lexical_cast<string>(i + 1);
            }
            SNRstring += ",SNR" + lexical_cast<string>(i + 1);
            Peakheightstring += ",PeakHeight" + lexical_cast<string>(i + 1);
            PeakheightRTstring += ",PeakHeightRT" + lexical_cast<string>(i + 1);
            PeakAreastring += ",PeakArea" + lexical_cast<string>(i + 1);
        }

        peakClusterCsv << "Cluster_Index,StartRT,EndRT,StartScan,EndScan,Identified,Charge" + mzstring + Idxstring + Corrstring + SNRstring + Peakheightstring + PeakheightRTstring + PeakAreastring + ",IsoMapProb,ConflictCorr,LeftInt,RightInt,NoRidges,MS1Score,MS1Prob,MS1LProb\n";
        //boost::format peakClusterFmt("%d,%.4f,%.4f,%d,%d,%s,%d,%s,%s,%s,%s,%s,%s,%s,%.4f,%.4f,%.4f,%.4f,%d,%.4f,%.4f,%.4f\n");
        boost::format peakClusterFmt("%s,%.4f,%.4f,%d,%d,%s,%d,%s,%s,%s,%s,%s,%s,%s,%.4f,%.4f,%.4f,%.4f,%d,%.4f,%.4f,%.4f\n");
        for (PeakClusterPtr const& clusterPtr : peakClusters)
        {
            PeakCluster const& cluster = *clusterPtr;

            Identifiedstring = "0";
            if (cluster.Identified) {
                Identifiedstring = "1";
            }

            //peakClusterFmt % cluster.Index % cluster.startRT % cluster.endRT % (cluster.StartScan-1) % cluster.EndScan % Identifiedstring % cluster.Charge;
            peakClusterFmt % id % cluster.startRT % cluster.endRT % (cluster.StartScan - 1) % cluster.EndScan % Identifiedstring % cluster.Charge;

            mzstring = "";
            Idxstring = "";
            Corrstring = "";
            SNRstring = "";
            Peakheightstring = "";
            PeakheightRTstring = "";
            PeakAreastring = "";

            for (int i = 0; i < cluster.mz.size(); i++)
            {
                mzstring += (boost::format("%.4f") % cluster.mz[i]).str() + ",";
                Idxstring += lexical_cast<string>(cluster.IsoPeakIndex[i]) + ",";
                if (i > 0) {
                    Corrstring += (boost::format("%.4f") % cluster.Corrs[i - 1]).str() + ",";
                }
                SNRstring += (boost::format("%.4f") % cluster.GetSNR(i)).str() + ",";
                Peakheightstring += (boost::format("%.4f") % cluster.PeakHeight[i]).str() + ",";
                PeakheightRTstring += (boost::format("%.4f") % cluster.PeakHeightRT[i]).str() + ",";
                PeakAreastring += (boost::format("%.4f") % cluster.PeakArea[i]).str() + ",";
            }
            peakClusterFmt % mzstring % Idxstring % Corrstring % SNRstring % Peakheightstring % PeakheightRTstring % PeakAreastring;
            peakClusterFmt % cluster.IsoMapProb % cluster.GetConflictCorr() % cluster.LeftInt % cluster.RightInt % cluster.NoRidges % cluster.MS1Score % cluster.MS1ScoreProbability % cluster.MS1ScoreLocalProb;
            peakClusterCsv << peakClusterFmt.str();
            peakClusterFmt.clear_binds();
        }
    }

    bool DiaUmpire::Impl::MS1PeakDetection()
    {
        //Calculate how many points per minute for B-spline peak smoothing
        config_.instrumentParameters.NoPeakPerMin = (int)(config_.instrumentParameters.SmoothFactor / ms1CycleTime_);

        vector<ScanCollectionPtr> scanCollections;
        if (ms1WindowScheme_.empty())
        {
            //The data has only one MS1 scan set
            scanCollections.emplace_back(GetAllScanCollectionByMSLabel(true, true, true, false, config_.instrumentParameters.startRT, config_.instrumentParameters.endRT, DiaUmpireStep::ReadAllSpectra));
        }
        else
        {
            //Get MS1 ScanCollection for each MS1 scan set
            for (const auto& window : ms1WindowScheme_)
                scanCollections.emplace_back(GetScanCollectionMS1Window(*window, true, msLevelAndScanTimeByIndex_.front().second, msLevelAndScanTimeByIndex_.back().second));
        }

        // last scan collection will be null if iteration was cancelled
        if (!scanCollections.back())
            return false;

#ifdef DIAUMPIRE_DEBUG
        {
            ofstream scansLog("DiaUmpireCpp-ms1-scans.txt");
            //boost::format pointFormat(" [%.2f,%.2f]");
            for (const ScanCollectionPtr& scanCollection : scanCollections)
                for (const auto& indexScanPair : scanCollection->ScanHashMap)
                {
                    if (indexScanPair.second.MsLevel == 1)
                        scansLog << indexScanPair.first << " " << indexScanPair.second.Data.size() << "\n";
                    //for (const auto& point : indexScanPair.second.Data)
                    //    scansLog << pointFormat % point.mz % point.intensity;
                    //scansLog << "\n";
                }
        }
#endif

        for (const ScanCollectionPtr& scanCollection : scanCollections)
        {
            //Detect mz trace peak curves for each ScanCollection
            FindAllMzTracePeakCurves(*scanCollection, ms1PeakCurves_, config_.instrumentParameters.MS1PPM, 1, DiaUmpireStep::BuildPeakCurves);
        }

        //Perform peak smoothing for each detected peak curve
        PeakCurveSmoothing(ms1PeakCurves_);

        // ClearRawPeaks(); clear unsortedPeakCurves.GetPeakLists()
        PeakCurveCorrClustering(MzRange{ -1e30f, 1e30f }, ms1PeakCurves_, ms1PeakClusters_, 1);

        if (config_.exportMs1ClusterTable)
            ExportPeakClusterResultCSV("MS1", ms1PeakClusters_);

        return true;
    }

    bool DiaUmpire::Impl::DIAMS2PeakDetection()
    {
        auto scanCollectionAllMs2 = GetAllScanCollectionByMSLabel(false, true, true, false, config_.instrumentParameters.startRT, config_.instrumentParameters.endRT, DiaUmpireStep::ReadMs2Spectra);

#ifdef DIAUMPIRE_DEBUG
        {
            ofstream scansLog("DiaUmpireCpp-ms2-scans.txt");
            //boost::format pointFormat(" [%.2f,%.2f]");
            for (const auto& indexScanPair : scanCollectionAllMs2->ScanHashMap)
            {
                scansLog << indexScanPair.first << " " << indexScanPair.second.Data.size();
                //for (const auto& point : indexScanPair.second.Data)
                //    scansLog << pointFormat % point.mz % point.intensity;
                scansLog << "\n";
            }
        }
#endif

        bool multithreadWindows = config_.multithreadOverWindows;

        boost::asio::thread_pool pool(!multithreadWindows ? 1 : config_.maxThreads);
        boost::mutex m;
        std::atomic<int> windowsProcessed(0);
        string progressMessage = "processing DIA window";
        string progressMessage2 = "generating pseudo-MS/MS spectra";

        //vector<shared_ptr<PseudoMSMSProcessing>> ScanList;
        std::atomic<int> spectraGenerated;

        for (auto& diaWindow : diaWindows_)
        {
            boost::asio::post(pool, [&]
            {
                string diaWindowId = (boost::format("MS2:[%.1f-%.1f]") % diaWindow.mzRange.begin % diaWindow.mzRange.end).str();

                if (iterateAndCheckCancellation(windowsProcessed, diaWindows_.size(), progressMessage, DiaUmpireStep::ProcessDiaWindows))
                    return;

                //cout << "Processing DIA MS2 (mz range):" << DIAwindow.DIA_MZ_Range.getX() << "_" << DIAwindow.DIA_MZ_Range.getY() << "( " << (count++) << "/" diaWindows_.size() << " )";

                DiaUmpireStep buildPeakCurvesStep = multithreadWindows ? DiaUmpireStep::InlineStep : DiaUmpireStep::BuildPeakCurves;
                FindAllMzTracePeakCurves(*scanCollectionAllMs2, diaWindow.peakCurves, config_.instrumentParameters.MS2PPM, 2, buildPeakCurvesStep, diaWindow.spectraInRange);
                if (iterateAndCheckCancellation(windowsProcessed, diaWindows_.size(), progressMessage, DiaUmpireStep::ProcessDiaWindows))
                    return;

                PeakCurveSmoothing(diaWindow.peakCurves, !multithreadWindows);
                if (iterateAndCheckCancellation(windowsProcessed, diaWindows_.size(), progressMessage, DiaUmpireStep::ProcessDiaWindows))
                    return;

                PeakCurveCorrClustering(diaWindow.mzRange, diaWindow.peakCurves, diaWindow.peakClusters, 2, !multithreadWindows);
                if (iterateAndCheckCancellation(windowsProcessed, diaWindows_.size(), progressMessage, DiaUmpireStep::ProcessDiaWindows))
                    return;

                if (diaWindow.peakCurves.empty())
                {
                    cerr << "No peak detected for window " << diaWindow.mzRange.begin << "-" << diaWindow.mzRange.end << endl;
                    return;
                }

                if (config_.exportMs2ClusterTable)
                    ExportPeakClusterResultCSV(diaWindowId, diaWindow.peakClusters);

                if (config_.instrumentParameters.MassDefectFilter)
                    //RemoveFragmentPeakByMassDefect();
                {
                    MassDefect MD;
                    //cout << endl << "No. of fragment peaks: " << diaWindow.peakCurves.size() << endl;
                    for (int i = diaWindow.peakCurves.size() - 1; i >= 0; --i)
                    {
                        auto& peakCurve = diaWindow.peakCurves[i];
                        bool remove = true;
                        for (int charge = 1; charge <= 2; charge++)
                        {
                            float mass = charge * (peakCurve->TargetMz - (float)pwiz::chemistry::Proton);
                            if (MD.InMassDefectRange(mass, config_.instrumentParameters.MassDefectOffset))
                            {
                                remove = false;
                                break;
                            }
                        }
                        if (remove)
                            diaWindow.peakCurves.erase(diaWindow.peakCurves.begin() + i);
                    }
                    //cout << "No. of remaining fragment peaks: " << diaWindow.peakCurves.size() << endl;
                }

                //FragmentGrouping();
                diaWindow.PrecursorFragmentPairBuildingForMS1(*this);
                diaWindow.PrecursorFragmentPairBuildingForUnfragmentedIon(*this);

                if (iterateAndCheckCancellation(windowsProcessed, diaWindows_.size(), progressMessage, DiaUmpireStep::ProcessDiaWindows))
                    return;

                vector<shared_ptr<PseudoMSMSProcessing>> LocalScanList;

                //PreparePseudoMSMS
                for (PeakClusterPtr& ms1clusterPtr : ms1PeakClusters_)
                {
                    PeakCluster& ms1cluster = *ms1clusterPtr;
                    if (diaWindow.mzRange.begin > ms1cluster.GetMaxMz() || diaWindow.mzRange.end < ms1cluster.TargetMz())
                        continue;

                    auto findItr = diaWindow.FragmentsClu2Cur.find(ms1cluster.Index);
                    if (findItr == diaWindow.FragmentsClu2Cur.end())
                        continue;

                    ms1cluster.GroupedFragmentPeaks = findItr->second;

                    if (diaWindow.nextWindowMzRange.empty() || diaWindow.nextWindowMzRange.end < ms1cluster.TargetMz())
                    {
                        auto pseudoScan = boost::make_shared<PseudoMSMSProcessing>(ms1clusterPtr, config_.instrumentParameters, ms1clusterPtr->IsotopeComplete(3) ? QualityLevel::Q1_IsotopeComplete : QualityLevel::Q2_Ms1Group);
                        (*pseudoScan)();

                        //boost::lock_guard<boost::mutex> g(m);
                        LocalScanList.emplace_back(pseudoScan);
                    }
                }

                if (iterateAndCheckCancellation(windowsProcessed, diaWindows_.size(), progressMessage, DiaUmpireStep::ProcessDiaWindows))
                    return;

                for (PeakClusterPtr& ms2clusterPtr : diaWindow.peakClusters)
                {
                    PeakCluster& ms2cluster = *ms2clusterPtr;
                    if (diaWindow.mzRange.begin > ms2cluster.TargetMz() || diaWindow.mzRange.end < ms2cluster.TargetMz())
                        continue;

                    auto findItr = diaWindow.UnFragIonClu2Cur.find(ms2cluster.Index);
                    if (findItr == diaWindow.UnFragIonClu2Cur.end())
                        continue;

                    ms2cluster.GroupedFragmentPeaks = findItr->second;

                    auto pseudoScan = boost::make_shared<PseudoMSMSProcessing>(ms2clusterPtr, config_.instrumentParameters, QualityLevel::Q3_UnfragmentedPrecursor);
                    (*pseudoScan)();

                    //boost::lock_guard<boost::mutex> g(m);
                    LocalScanList.emplace_back(pseudoScan);
                }

#ifdef DIAUMPIRE_DEBUG
                ofstream scanList("DiaUmpireCpp-scanList.txt");
                boost::format pointFormat("%.4f %.4f %d %d\n");
#endif

                MSData spillFile;
                spillFile.id = spillFile.run.id = msd_.id + " DIA window " + diaWindowId;
                auto outputScans = boost::make_shared<SpectrumListSimple>();
                spillFile.run.spectrumListPtr = outputScans;
                auto spillFilepathPtr = boost::make_shared<TemporaryFile>(".mz5");

                vector<PseudoMsMsKey> localPseudoMsMs;
                for (const auto& pseudoScan : LocalScanList)
                {
                    //if (!multithreadWindows && iterateAndCheckCancellation(spectraGenerated, LocalScanList.size(), progressMessage2, DiaUmpireStep::GeneratePseudoSpectra))
                    //    return;

                    const auto& precursorCluster = pseudoScan->Precursorcluster;

                    SpectrumPtr s(new Spectrum);
                    s->set(MS_ms_level, 2);
                    s->set(MS_MSn_spectrum);
                    s->set(MS_centroid_spectrum);
                    
                    switch (pseudoScan->qualityLevel)
                    {
                        case QualityLevel::Q1_IsotopeComplete:
                            s->userParams.emplace_back("DIA-Umpire quality level", "1", "xsd:positiveInteger");
                            break;

                        case QualityLevel::Q2_Ms1Group:
                            s->userParams.emplace_back("DIA-Umpire quality level", "2", "xsd:positiveInteger");
                            break;

                        case QualityLevel::Q3_UnfragmentedPrecursor:
                            s->userParams.emplace_back("DIA-Umpire quality level", "3", "xsd:positiveInteger");
                            break;
                    }

                    s->scanList.scans.emplace_back();
                    Scan& scan = s->scanList.scans.back();
                    scan.set(MS_scan_start_time, round(precursorCluster.PeakHeightRT.at(0) * 10000.0) / 10000.0, UO_minute);
                    if (!msd_.instrumentConfigurationPtrs.empty())
                        scan.instrumentConfigurationPtr = msd_.instrumentConfigurationPtrs[0];

                    s->precursors.emplace_back(precursorCluster.TargetMz(), precursorCluster.PeakHeight.at(0), precursorCluster.Charge, MS_number_of_detector_counts);

                    BinaryData<double> mzArray, intensityArray;
                    pseudoScan->GetScan(mzArray, intensityArray);
                    s->swapMZIntensityArrays(mzArray, intensityArray, MS_number_of_detector_counts);

                    if (config_.exportSeparateQualityMGFs)
                    {
                        boost::lock_guard<boost::mutex> g(m);
                        switch (pseudoScan->qualityLevel)
                        {
                            case QualityLevel::Q1_IsotopeComplete:
#ifdef DIAUMPIRE_DEBUG
                                scanList << (pointFormat % (precursorCluster.PeakHeightRT.at(0) * 60) % precursorCluster.TargetMz() % precursorCluster.Charge % mzArray.size()).str();
#endif
                                slQ1->spectra.emplace_back(s);
                                break;

                            case QualityLevel::Q2_Ms1Group:
                                slQ2->spectra.emplace_back(s);
                                break;

                            case QualityLevel::Q3_UnfragmentedPrecursor:
                                slQ3->spectra.emplace_back(s);
                                break;
                        }
                    }

                    s->index = outputScans->spectra.size();
                    s->id = "merged=" + lexical_cast<string>(s->index);
                    outputScans->spectra.emplace_back(s);
                    localPseudoMsMs.emplace_back(PseudoMsMsKey(precursorCluster.PeakHeightRT.at(0), precursorCluster.TargetMz(), precursorCluster.Charge, spillFilepathPtr.get(), s->index));
                }

                {
                    MSDataFile::WriteConfig writeConfig(MSDataFile::Format_MZ5);
                    writeConfig.useWorkerThreads = false;
                    writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;

                    {
                        boost::lock_guard<boost::mutex> g(m);
                        spillFiles[diaWindowId] = spillFilepathPtr;
                        outputScanKeys.insert(outputScanKeys.end(), localPseudoMsMs.begin(), localPseudoMsMs.end());
                    }

                    MSDataFile::write(spillFile, spillFilepathPtr->path().string(), writeConfig);
                }

                if (iterateAndCheckCancellation(windowsProcessed, diaWindows_.size(), progressMessage, DiaUmpireStep::ProcessDiaWindows))
                    return;

                diaWindow.peakClusters.clear();
                diaWindow.peakCurves.clear();

                diaWindow.FragmentsClu2Cur.clear();
                diaWindow.UnFragIonClu2Cur.clear();
                diaWindow.FragmentMS1Ranking.clear();
                diaWindow.FragmentUnfragRanking.clear();
                ++windowsProcessed;
            });
        }
        pool.join();

        if (iterateAndCheckCancellation(windowsProcessed, diaWindows_.size(), progressMessage, DiaUmpireStep::ProcessDiaWindows))
            return false;

        sort(outputScanKeys.begin(), outputScanKeys.end(), [](auto&& lhs, auto&& rhs)
        {
            return lhs.scanTime == rhs.scanTime ?
                        lhs.targetMz == rhs.targetMz ?
                            lhs.charge < rhs.charge :
                            lhs.targetMz < rhs.targetMz :
                        lhs.scanTime < rhs.scanTime;
        });

        int index = 0;
        for (auto& key : outputScanKeys)
        {
            key.index = index;
            key.id = "merged=" + lexical_cast<string>(index);
            ++index;
        }

        return true;
    }


    void DiaWindow::PrecursorFragmentPairBuildingForMS1(DiaUmpire::Impl const& diaUmpire)
    {
        auto const& ms1PeakClusters = diaUmpire.ms1PeakClusters_;
        auto const& instrumentParameters = diaUmpire.config_.instrumentParameters;

        vector<CorrCalcCluster2Curve> pairBuildingJobs;
        pairBuildingJobs.reserve(ms1PeakClusters.size());

        multimap<float, PeakCurvePtr> peakCurvesByRT;
        for (auto const& peakCurve : peakCurves)
            peakCurvesByRT.insert(make_pair(peakCurve->ApexRT, peakCurve));

        //For each peak curve
        for (PeakClusterPtr const& peakCluster : ms1PeakClusters)
        {
            if (peakCluster->GetMaxMz() < mzRange.begin || peakCluster->TargetMz() > mzRange.end)
                continue;

            //Create a thread unit for doing isotope clustering given a peak curve as the monoisotope peak
            pairBuildingJobs.emplace_back(peakCluster, peakCurvesByRT, instrumentParameters);

            pairBuildingJobs.back()();
        }

        for (auto& unit : pairBuildingJobs)
        {
            if (unit.GroupedFragmentList.empty())
                continue;
            auto& result = FragmentsClu2Cur[unit.MS1PeakCluster->Index];
            swap(result, unit.GroupedFragmentList);
        }

        //BuildFragmentMS1ranking();
        for (auto& clusterCurvePair : FragmentsClu2Cur)
            for (PrecursorFragmentPairEdge& fragmentClusterUnit : clusterCurvePair.second)
                FragmentMS1Ranking[fragmentClusterUnit.PeakCurveIndexB].push_back(fragmentClusterUnit.Correlation);

        // descending correlation sort
        for (auto& fragmentRankingPair : FragmentMS1Ranking)
            sort(fragmentRankingPair.second.rbegin(), fragmentRankingPair.second.rend());

        for (auto& clusterCurvePair : FragmentsClu2Cur)
        {
            for (PrecursorFragmentPairEdge& fragmentClusterUnit : clusterCurvePair.second)
            {
                auto& scorelist = FragmentMS1Ranking[fragmentClusterUnit.PeakCurveIndexB];
                for (int intidx = 0; intidx < scorelist.size(); intidx++)
                {
                    if (scorelist[intidx] <= fragmentClusterUnit.Correlation)
                    {
                        fragmentClusterUnit.FragmentMS1Rank = intidx + 1;
                        fragmentClusterUnit.FragmentMS1RankScore = (float)fragmentClusterUnit.FragmentMS1Rank / (float)scorelist.size();
                        break;
                    }
                }
            }
        }

        //FilterByCriteria();
        auto edgeCompare = [](PrecursorFragmentPairEdge const& lhs, PrecursorFragmentPairEdge const& rhs)
        {
            return lhs.PeakCurveIndexA == rhs.PeakCurveIndexA ? lhs.PeakCurveIndexB < rhs.PeakCurveIndexB : lhs.PeakCurveIndexA < rhs.PeakCurveIndexA;
        };

        map<size_t, vector<PrecursorFragmentPairEdge>> templist;
        for (auto& clusterCurvePair : FragmentsClu2Cur)
        {
            vector<float> CorrArrayList;
            map<PrecursorFragmentPairEdge, float, decltype(edgeCompare)> ScoreList(edgeCompare);
            for (PrecursorFragmentPairEdge& fragmentClusterUnit : clusterCurvePair.second)
            {
                float score = fragmentClusterUnit.Correlation * fragmentClusterUnit.Correlation * (float)log(fragmentClusterUnit.Intensity);
                ScoreList[fragmentClusterUnit] = score;
                CorrArrayList.push_back(score);
            }

            sort(CorrArrayList.rbegin(), CorrArrayList.rend());
            auto& newlist = templist[clusterCurvePair.first];

            for (PrecursorFragmentPairEdge& fragmentClusterUnit : clusterCurvePair.second)
            {
                int CorrRank = 0;
                for (int intidx = 0; intidx < CorrArrayList.size(); intidx++) {
                    if (CorrArrayList.at(intidx) <= ScoreList.at(fragmentClusterUnit))
                    {
                        CorrRank = intidx + 1;
                        break;
                    }
                }
                if (fragmentClusterUnit.Correlation >= instrumentParameters.CorrThreshold &&
                    CorrRank <= instrumentParameters.FragmentRank &&
                    fragmentClusterUnit.FragmentMS1Rank <= instrumentParameters.PrecursorRank &&
                    fragmentClusterUnit.ApexDelta <= instrumentParameters.ApexDelta) {
                    newlist.push_back(fragmentClusterUnit);
                }
            }
        }
        swap(FragmentsClu2Cur, templist);

        //ExportCluster2CurveCorr();
    }


    void DiaWindow::PrecursorFragmentPairBuildingForUnfragmentedIon(DiaUmpire::Impl const& diaUmpire)
    {
        if (peakClusters.empty())
            return;

        auto const& instrumentParameters = diaUmpire.config_.instrumentParameters;

        vector<CorrCalcCluster2Curve> pairBuildingJobs;
        pairBuildingJobs.reserve(peakClusters.size());

        multimap<float, PeakCurvePtr> peakCurvesByRT;
        for (auto const& peakCurve : peakCurves)
            peakCurvesByRT.insert(make_pair(peakCurve->ApexRT, peakCurve));

        //For each peak curve
        for (PeakClusterPtr const& peakCluster : peakClusters)
        {
            if (peakCluster->GetMaxMz() < mzRange.begin || peakCluster->TargetMz() > mzRange.end)
                continue;

            //Create a thread unit for doing isotope clustering given a peak curve as the monoisotope peak
            pairBuildingJobs.emplace_back(peakCluster, peakCurvesByRT, instrumentParameters);

            pairBuildingJobs.back()();
        }

        for (auto& unit : pairBuildingJobs)
        {
            if (unit.GroupedFragmentList.empty())
                continue;
            auto& result = UnFragIonClu2Cur[unit.MS1PeakCluster->Index];
            swap(result, unit.GroupedFragmentList);
        }

        //BuildFragmentMS1ranking();
        for (auto& clusterCurvePair : UnFragIonClu2Cur)
            for (PrecursorFragmentPairEdge& fragmentClusterUnit : clusterCurvePair.second)
                FragmentMS1Ranking[fragmentClusterUnit.PeakCurveIndexB].push_back(fragmentClusterUnit.Correlation);

        // descending correlation sort
        for (auto& fragmentRankingPair : FragmentUnfragRanking)
            sort(fragmentRankingPair.second.rbegin(), fragmentRankingPair.second.rend());

        for (auto& clusterCurvePair : UnFragIonClu2Cur)
        {
            for (PrecursorFragmentPairEdge& fragmentClusterUnit : clusterCurvePair.second)
            {
                auto& scorelist = FragmentUnfragRanking[fragmentClusterUnit.PeakCurveIndexB];
                for (int intidx = 0; intidx < scorelist.size(); intidx++)
                {
                    if (scorelist[intidx] <= fragmentClusterUnit.Correlation)
                    {
                        fragmentClusterUnit.FragmentMS1Rank = intidx + 1;
                        fragmentClusterUnit.FragmentMS1RankScore = (float)fragmentClusterUnit.FragmentMS1Rank / (float)scorelist.size();
                        break;
                    }
                }
            }
        }

        //FilterByCriteria();
        auto edgeCompare = [](PrecursorFragmentPairEdge const& lhs, PrecursorFragmentPairEdge const& rhs)
        {
            return lhs.PeakCurveIndexA == rhs.PeakCurveIndexA ? lhs.PeakCurveIndexB < rhs.PeakCurveIndexB : lhs.PeakCurveIndexA < rhs.PeakCurveIndexA;
        };

        map<size_t, vector<PrecursorFragmentPairEdge>> templist;
        for (auto& clusterCurvePair : UnFragIonClu2Cur)
        {
            vector<float> CorrArrayList;
            map<PrecursorFragmentPairEdge, float, decltype(edgeCompare)> ScoreList(edgeCompare);
            for (PrecursorFragmentPairEdge& fragmentClusterUnit : clusterCurvePair.second)
            {
                float score = fragmentClusterUnit.Correlation * fragmentClusterUnit.Correlation * (float)log(fragmentClusterUnit.Intensity);
                ScoreList[fragmentClusterUnit] = score;
                CorrArrayList.push_back(score);
            }

            sort(CorrArrayList.rbegin(), CorrArrayList.rend());
            auto& newlist = templist[clusterCurvePair.first];

            for (PrecursorFragmentPairEdge& fragmentClusterUnit : clusterCurvePair.second)
            {
                int CorrRank = 0;
                for (int intidx = 0; intidx < CorrArrayList.size(); intidx++) {
                    if (CorrArrayList.at(intidx) <= ScoreList.at(fragmentClusterUnit))
                    {
                        CorrRank = intidx + 1;
                        break;
                    }
                }
                if (fragmentClusterUnit.Correlation >= instrumentParameters.CorrThreshold &&
                    CorrRank <= instrumentParameters.FragmentRank &&
                    fragmentClusterUnit.FragmentMS1Rank <= instrumentParameters.PrecursorRank &&
                    fragmentClusterUnit.ApexDelta <= instrumentParameters.ApexDelta) {
                    newlist.push_back(fragmentClusterUnit);
                }
            }
        }
        swap(UnFragIonClu2Cur, templist);

        //ExportCluster2CurveCorr();
    }


    PWIZ_API_DECL
        DiaUmpire::DiaUmpire(const MSData& msd, const SpectrumListPtr& spectrumList, const Config& config, const IterationListenerRegistry* ilr)
        : impl_(std::make_unique<Impl>(msd, spectrumList, config, ilr))
    {
    }

    PWIZ_API_DECL DiaUmpire::~DiaUmpire() = default;

    PWIZ_API_DECL const std::vector<PseudoMsMsKey>& DiaUmpire::pseudoMsMsKeys() const
    {
        return impl_->outputScanKeys;
    }

    PWIZ_API_DECL const std::map<std::string, boost::shared_ptr<pwiz::util::TemporaryFile>>& DiaUmpire::spillFileByWindow() const
    {
        return impl_->spillFiles;
    }

    PWIZ_API_DECL Config::Config(const std::string& paramsFilepath)
    {
        if (!paramsFilepath.empty() && !bfs::exists(paramsFilepath))
            throw runtime_error("[DiaUmpire::Config] file \"" + paramsFilepath + "\" does not exist");

        auto& param = instrumentParameters;

        // default parameters from TTOF5600
        param.MS1PPM = 30;
        param.MS2PPM = 40;
        param.SNThreshold = 2.f;
        param.MS2SNThreshold = 2.f;
        param.MinMSIntensity = 5.f;
        param.MinMSMSIntensity = 1.f;
        param.MinRTRange = 0.1f;
        param.MaxNoPeakCluster = 4;
        param.MinNoPeakCluster = 2;
        param.MaxMS2NoPeakCluster = 4;
        param.MinMS2NoPeakCluster = 2;
        param.MaxCurveRTRange = 1.5f;
        param.Resolution = 17000;
        param.RTtol = 0.1f;
        param.Denoise = true;
        param.EstimateBG = true;
        param.RemoveGroupedPeaks = true;

        if (paramsFilepath.empty())
        {
            if (maxThreads == 0)
                maxThreads = boost::thread::hardware_concurrency();
            return;
        }

        ifstream reader(paramsFilepath.c_str());
        string line;
        vector<string> tokens;

        while (std::getline(reader, line))
        {
            if (line.empty() || line[0] == '#')
                continue;

            if (line == ("==window setting begin"))
            {
                while (std::getline(reader, line) && line != "==window setting end")
                {
                    if (line.empty())
                        continue;
                    bal::split(tokens, line, bal::is_any_of("\t"));
                    if (tokens.size() != 2)
                        throw runtime_error("Invalid variable window \"" + line + "\" - expected 2 values (start and end m/z)");
                    diaVariableWindows.emplace_back(MzRange(lexical_cast<float>(tokens[0]), lexical_cast<float>(tokens[1])));
                }
                continue;
            }
            vector<string> tokens;
            bal::split(tokens, line, bal::is_any_of("="));
            if (tokens.size() < 2)
                continue;

            string type = tokens[0];
            bal::trim(type);
            if (bal::starts_with(type, "para."))
                bal::replace_first(type, "para.", "SE.");

            string value = tokens[1];
            bal::trim(value);
            if (type == "ExportPrecursorPeak")
            {
                exportMs1ClusterTable = lexical_cast<bool>(value);
            }
            else if (type == "ExportFragmentPeak")
            {
                exportMs2ClusterTable = lexical_cast<bool>(value);
            }
            else if (type == "RPmax")
            {
                param.PrecursorRank = lexical_cast<int>(value);
            }
            else if (type == "RFmax")
            {
                param.FragmentRank = lexical_cast<int>(value);
            }
            else if (type == "CorrThreshold")
            {
                param.CorrThreshold = lexical_cast<float>(value);
            }
            else if (type == "DeltaApex")
            {
                param.ApexDelta = lexical_cast<float>(value);
            }
            else if (type == "RTOverlap")
            {
                param.RTOverlapThreshold = lexical_cast<float>(value);
            }
            else if (type == "BoostComplementaryIon")
            {
                param.BoostComplementaryIon = lexical_cast<bool>(value);
            }
            else if (type == "AdjustFragIntensity")
            {
                param.AdjustFragIntensity = lexical_cast<bool>(value);
            }
            else if (type == "SE.MS1PPM")
            {
                param.MS1PPM = lexical_cast<float>(value);
            }
            else if (type == "SE.MS2PPM")
            {
                param.MS2PPM = lexical_cast<float>(value);
            }
            else if (type == "SE.SN")
            {
                param.SNThreshold = lexical_cast<float>(value);
            }
            else if (type == "SE.MS2SN")
            {
                param.MS2SNThreshold = lexical_cast<float>(value);
            }
            else if (type == "SE.MinMSIntensity")
            {
                param.MinMSIntensity = lexical_cast<float>(value);
            }
            else if (type == "SE.MinMSMSIntensity")
            {
                param.MinMSMSIntensity = lexical_cast<float>(value);
            }
            else if (type == "SE.MinRTRange")
            {
                param.MinRTRange = lexical_cast<float>(value);
            }
            else if (type == "SE.MaxNoPeakCluster")
            {
                param.MaxNoPeakCluster = lexical_cast<int>(value);
                param.MaxMS2NoPeakCluster = lexical_cast<int>(value);
            }
            else if (type == "SE.MinNoPeakCluster")
            {
                param.MinNoPeakCluster = lexical_cast<int>(value);
                param.MinMS2NoPeakCluster = lexical_cast<int>(value);
            }
            else if (type == "SE.MinMS2NoPeakCluster")
            {
                param.MinMS2NoPeakCluster = lexical_cast<int>(value);
            }
            else if (type == "SE.MaxCurveRTRange")
            {
                param.MaxCurveRTRange = lexical_cast<float>(value);
            }
            else if (type == "SE.Resolution")
            {
                param.Resolution = lexical_cast<int>(value);
            }
            else if (type == "SE.RTtol")
            {
                param.RTtol = lexical_cast<float>(value);
            }
            else if (type == "SE.NoPeakPerMin")
            {
                param.NoPeakPerMin = lexical_cast<int>(value);
            }
            else if (type == "SE.StartCharge")
            {
                param.StartCharge = lexical_cast<int>(value);
            }
            else if (type == "SE.EndCharge")
            {
                param.EndCharge = lexical_cast<int>(value);
            }
            else if (type == "SE.MS2StartCharge")
            {
                param.MS2StartCharge = lexical_cast<int>(value);
            }
            else if (type == "SE.MS2EndCharge")
            {
                param.MS2EndCharge = lexical_cast<int>(value);
            }
            else if (type == "SE.NoMissedScan")
            {
                param.NoMissedScan = lexical_cast<int>(value);
            }
            else if (type == "SE.Denoise")
            {
                param.Denoise = lexical_cast<bool>(value);
            }
            else if (type == "SE.EstimateBG")
            {
                param.EstimateBG = lexical_cast<bool>(value);
            }
            else if (type == "SE.RemoveGroupedPeaks")
            {
                param.RemoveGroupedPeaks = lexical_cast<bool>(value);
            }
            else if (type == "SE.MinFrag")
            {
                param.MinFrag = lexical_cast<int>(value);
            }
            else if (type == "SE.IsoPattern")
            {
                param.IsoPattern = lexical_cast<float>(value);
            }
            else if (type == "SE.StartRT")
            {
                param.startRT = lexical_cast<float>(value);
            }
            else if (type == "SE.EndRT")
            {
                param.endRT = lexical_cast<float>(value);
            }
            else if (type == "SE.RemoveGroupedPeaksRTOverlap")
            {
                param.RemoveGroupedPeaksRTOverlap = lexical_cast<float>(value);
            }
            else if (type == "SE.RemoveGroupedPeaksCorr")
            {
                param.RemoveGroupedPeaksCorr = lexical_cast<float>(value);
            }
            else if (type == "SE.MinMZ")
            {
                param.MinMZ = lexical_cast<float>(value);
            }
            else if (type == "SE.MinPrecursorMass")
            {
                param.MinPrecursorMass = lexical_cast<float>(value);
            }
            else if (type == "SE.MaxPrecursorMass")
            {
                param.MaxPrecursorMass = lexical_cast<float>(value);
            }
            else if (type == "SE.IsoCorrThreshold")
            {
                param.IsoCorrThreshold = lexical_cast<float>(value);
            }
            else if (type == "SE.MassDefectFilter")
            {
                param.MassDefectFilter = lexical_cast<bool>(value);
            }
            else if (type == "SE.MassDefectOffset")
            {
                param.MassDefectOffset = lexical_cast<float>(value);
            }
            else if (type == "WindowType")
            {
                if (value == "SWATH")
                    diaTargetWindowScheme = TargetWindow::Scheme::SWATH_Fixed;
                else if (value == "V_SWATH")
                    diaTargetWindowScheme = TargetWindow::Scheme::SWATH_Variable;
                else
                    throw runtime_error("only SWATH and V_SWATH modes are supported for WindowType");
            }
            else if (type == "WindowSize")
            {
                diaFixedWindowSize = lexical_cast<int>(value);
            }
            else if (type == "Thread")
            {
                maxThreads = lexical_cast<int>(value);
            }
            else if (type == "MultithreadOverWindows")
            {
                multithreadOverWindows = lexical_cast<bool>(value);
            }
        }

        if (maxThreads == 0)
            maxThreads = boost::thread::hardware_concurrency();
    }

    PWIZ_API_DECL PseudoMsMsKey::PseudoMsMsKey(float scanTime, float targetMz, int charge, pwiz::util::TemporaryFile* spillFilePtr, size_t spillFileIndex)
        : scanTime(scanTime), targetMz(targetMz), charge(charge), spillFilePtr(spillFilePtr), spillFileIndex(spillFileIndex)
    {}

    std::map<std::string, std::string> InstrumentParameter::GetParameterMap() const
    {
        std::map<std::string, std::string> result;

        result["BoostComplementaryIon"] = lexical_cast<string>(BoostComplementaryIon);
        result["AdjustFragIntensity"] = lexical_cast<string>(AdjustFragIntensity);
        result["RPmax"] = lexical_cast<string>(PrecursorRank);
        result["RFmax"] = lexical_cast<string>(FragmentRank);
        result["RTOverlap"] = lexical_cast<string>(RTOverlapThreshold);
        result["CorrThreshold"] = lexical_cast<string>(CorrThreshold);
        result["DeltaApex"] = lexical_cast<string>(ApexDelta);

        result["SE.Resolution"] = lexical_cast<string>(Resolution);
        result["SE.MS1PPM"] = lexical_cast<string>(MS1PPM);
        result["SE.MS2PPM"] = lexical_cast<string>(MS2PPM);
        result["SE.SN"] = lexical_cast<string>(SNThreshold);
        result["SE.MinMSIntensity"] = lexical_cast<string>(MinMSIntensity);
        result["SE.MinMSMSIntensity"] = lexical_cast<string>(MinMSMSIntensity);
        result["SE.NoPeakPerMin"] = lexical_cast<string>(NoPeakPerMin);
        result["SE.MinRTRange"] = lexical_cast<string>(MinRTRange);
        result["SE.StartCharge"] = lexical_cast<string>(StartCharge);
        result["SE.EndCharge"] = lexical_cast<string>(EndCharge);
        result["SE.MS2StartCharge"] = lexical_cast<string>(MS2StartCharge);
        result["SE.MS2EndCharge"] = lexical_cast<string>(MS2EndCharge);
        result["SE.MaxCurveRTRange"] = lexical_cast<string>(MaxCurveRTRange);
        result["SE.RTtol"] = lexical_cast<string>(RTtol);
        result["SE.MS2SN"] = lexical_cast<string>(MS2SNThreshold);
        result["SE.MaxNoPeakCluster"] = lexical_cast<string>(MaxNoPeakCluster);
        result["SE.MinNoPeakCluster"] = lexical_cast<string>(MinNoPeakCluster);
        result["SE.MaxMS2NoPeakCluster"] = lexical_cast<string>(MaxMS2NoPeakCluster);
        result["SE.MinMS2NoPeakCluster"] = lexical_cast<string>(MinMS2NoPeakCluster);
        result["SE.Denoise"] = lexical_cast<string>(Denoise);
        result["SE.EstimateBG"] = lexical_cast<string>(EstimateBG);
        result["SE.DetermineBGByID"] = lexical_cast<string>(DetermineBGByID);
        result["SE.RemoveGroupedPeaks"] = lexical_cast<string>(RemoveGroupedPeaks);
        result["SE.Deisotoping"] = lexical_cast<string>(Deisotoping);
        result["SE.SymThreshold"] = lexical_cast<string>(SymThreshold);
        result["SE.NoMissedScan"] = lexical_cast<string>(NoMissedScan);
        result["SE.MinPeakPerPeakCurve"] = lexical_cast<string>(MinPeakPerPeakCurve);
        result["SE.MinMZ"] = lexical_cast<string>(MinMZ);
        result["SE.MinFrag"] = lexical_cast<string>(MinFrag);
        result["SE.MiniOverlapP"] = lexical_cast<string>(MiniOverlapP);
        result["SE.CheckMonoIsotopicApex"] = lexical_cast<string>(CheckMonoIsotopicApex);
        result["SE.DetectByCWT"] = lexical_cast<string>(DetectByCWT);
        result["SE.FillGapByBK"] = lexical_cast<string>(FillGapByBK);
        result["SE.IsoCorrThreshold"] = lexical_cast<string>(IsoCorrThreshold);
        result["SE.RemoveGroupedPeaksCorr"] = lexical_cast<string>(RemoveGroupedPeaksCorr);
        result["SE.RemoveGroupedPeaksRTOverlap"] = lexical_cast<string>(RemoveGroupedPeaksRTOverlap);
        result["SE.HighCorrThreshold"] = lexical_cast<string>(HighCorrThreshold);
        result["SE.MinHighCorrCnt"] = lexical_cast<string>(MinHighCorrCnt);
        result["SE.TopNLocal"] = lexical_cast<string>(TopNLocal);
        result["SE.TopNLocalRange"] = lexical_cast<string>(TopNLocalRange);
        result["SE.IsoPattern"] = lexical_cast<string>(IsoPattern);
        result["SE.StartRT"] = lexical_cast<string>(startRT);
        result["SE.EndRT"] = lexical_cast<string>(endRT);
        result["SE.TargetIDOnly"] = lexical_cast<string>(TargetIDOnly);
        result["SE.MassDefectFilter"] = lexical_cast<string>(MassDefectFilter);
        result["SE.MinPrecursorMass"] = lexical_cast<string>(MinPrecursorMass);
        result["SE.MaxPrecursorMass"] = lexical_cast<string>(MaxPrecursorMass);
        //result["UseOldVersion"]  = lexical_cast<string>(UseOldVersion);
        //result["SE.RT_window_Targeted"]  = lexical_cast<string>(RT_window_Targeted);
        result["SE.SmoothFactor"] = lexical_cast<string>(SmoothFactor);
        result["SE.DetectSameChargePairOnly"] = lexical_cast<string>(DetectSameChargePairOnly);
        result["SE.MassDefectOffset"] = lexical_cast<string>(MassDefectOffset);
        result["SE.MS2PairTopN"] = lexical_cast<string>(MS2PairTopN);
        result["SE.MS2Pairing"] = lexical_cast<string>(MS2Pairing);

        return result;
    }
} //namespace DiaUmpire