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

#ifndef _DIAUMPIRE_PEAKCURVE_
#define _DIAUMPIRE_PEAKCURVE_

#include <vector>
#include <limits>
#include <queue>
#include <iostream>
#include <memory>
#include "InstrumentParameter.hpp"
#include "ScanData.hpp"
#include "DiaUmpireMath.hpp"

namespace DiaUmpire {

/**
 * Single m/z trace peak curve
 * @author Chih-Chiang Tsou <chihchiang.tsou@gmail.com>
 */
class PeakCurve
{
    vector<XYZData> PeakList;
    //X: retention time
    //Y: m/z
    //Z: intensity
    XYPointCollection SmoothData;
    //X: retention time
    //Y: intensity
    //XIC
    mutable float startint = 0;
    mutable float endrt = -1;
    mutable float startrt = -1;
    float TotalIntMzF = 0;
    float TotalIntF = 0;
    vector<XYZData> PeakRegionList;
    vector<vector<float>> NoRidgeRegion;

    public:

    struct PeakRidge
    {
        float RT;
        int lowScale;
        int ContinuousLevel = 0;
        float intensity;

        bool operator<(const PeakRidge& rhs) const
        {
            return RT < rhs.RT;
        }
    };
    typedef std::shared_ptr<PeakCurve> PeakCurvePtr;

    int Index;
    int StartScan = -1;
    int EndScan = -1;
    int MsLevel;
    std::pair<float, float> ApexRtTargetMzPair { -1, 0 };
    float& TargetMz = ApexRtTargetMzPair.second;
    float ApexInt = 0;
    float minIntF = std::numeric_limits<float>::infinity();
    float& ApexRT = ApexRtTargetMzPair.first;
    float MaxCorr = 0;
    bool CheckState = false;
    float ConflictCorr = 0;
    bool Grouped = false;
    set<int> ChargeGrouped;
    float MzVar = -1;
    vector<float> RegionRidge;
    const InstrumentParameter& parameter;

    public:

    //using B-spline to generate smoothed peak signals
    void DoBspline()
    {
        for (XYZData& point : PeakList) {
            XYData pt { point.getX(), point.getZ() };
            SmoothData.AddPoint(pt);
        }
        BSpline bspline;
        bool log = false;
#ifdef DIAUMPIRE_DEBUG
        log = MsLevel == 1;
#endif

        SmoothData = bspline.Run(SmoothData, (int) std::max((size_t) round(RTWidth() * parameter.NoPeakPerMin), PeakList.size()), 2, log ? Index : 0);
    }

    void DoInterpolation()
    {
        for (XYZData& point : PeakList) {
            XYData pt { point.getX(), point.getZ() };
            SmoothData.AddPoint(pt);
        }
        LinearInterpolation interpo;
        SmoothData = interpo.Run(SmoothData, (int) std::max((size_t) round(RTWidth() * parameter.NoPeakPerMin), PeakList.size()));
    }

    void AddConflictScore(float corr)
    {
        ConflictCorr += corr;
    }

    float GetRawSNR() {
        return ApexInt / minIntF;
    }

    void SetRTs(float StartRT, float EndRT) {
        startrt = StartRT;
        endrt = EndRT;
    }

    //Detect peak region using CWT based on smoothed peak signals
    void DetectPeakRegion()
    {
        std::vector<XYData> PeakArrayList;
        std::vector<PeakRidge> PeakRidgeList;
        PeakRegionList.clear();
        NoRidgeRegion.clear();
        if (RTWidth() * parameter.NoPeakPerMin < 1) {
            return;
        }
        for (int i = 0; i < SmoothData.PointCount(); i++) {
            PeakArrayList.emplace_back(SmoothData.Data.at(i).getX(), SmoothData.Data.at(i).getY());
        }
        //Start CWT process
        WaveletMassDetector waveletMassDetector(parameter, PeakArrayList, (int)(RTWidth() * parameter.NoPeakPerMin));
        waveletMassDetector.Run();

#ifdef DIAUMPIRE_DEBUG
        if (MsLevel == 1)
        {
            ofstream peakRidgeLog("c:/pwiz.git/pwiz/DiaUmpireCpp-peakRidges.txt", std::ios::app);
            int ridgeIdx = 0;
            for (auto& ridge : waveletMassDetector.PeakRidge)
            {
                boost::format pointFormat(" %.3f");
                peakRidgeLog << Index << " " << ridgeIdx++;
                for (auto& pt : *ridge)
                    peakRidgeLog << (pointFormat % pt.x).str();
                peakRidgeLog << "\n";
            }
        }
#endif

        int maxScale = waveletMassDetector.PeakRidge.size() - 1;
        const float noValuePlaceholder = std::numeric_limits<float>::infinity();

        //trace peak ridge from maximum wavelet scale to minimum scale
        for (int i = maxScale; i >= 0; i--) {
            //Get peak ridge list (maximum RT points given a CWT scale
            auto& PeakRidgeArrayPtr = waveletMassDetector.PeakRidge[i];

            if (PeakRidgeArrayPtr == nullptr) {
                maxScale = i;
                continue;
            }
            if (PeakRidgeArrayPtr->empty()) {
                continue;
            }

            auto& PeakRidgeArray = *PeakRidgeArrayPtr;

            //RT distance matrix between the groupped peak riges and peak ridges extracted from current CWT scale
            std::vector<std::vector<float>> DisMatrixF(PeakRidgeList.size(), std::vector<float>(PeakRidgeArray.size()));

            for (size_t k = 0; k < PeakRidgeList.size(); k++) {///For each existing peak ridge line
                for (size_t l = 0; l < PeakRidgeArray.size(); l++) {
                    DisMatrixF[k][l] = abs(PeakRidgeList.at(k).RT - PeakRidgeArray.at(l).getX());
                }
            }

            bool conti = true;
            std::vector<int> RemovedRidgeList;
            while (conti)
            {
                float closest = noValuePlaceholder;
                int ExistingRideIdx = -1;
                int PeakRidgeInx = -1;
                for (size_t k = 0; k < PeakRidgeList.size(); k++) {
                    for (size_t l = 0; l < PeakRidgeArray.size(); l++) {
                        {
                            if (DisMatrixF[k][l] < closest) {
                                closest = DisMatrixF[k][l];
                                ExistingRideIdx = k;
                                PeakRidgeInx = l;
                            }
                        }
                    }
                }

                if (closest != noValuePlaceholder && closest <= parameter.MinRTRange) {
                    PeakRidge ridge = PeakRidgeList.at(ExistingRideIdx);
                    PeakRidgeList.erase(PeakRidgeList.begin() + ExistingRideIdx);
                    ridge.lowScale = i;
                    ridge.ContinuousLevel++;
                    XYData nearestRidge = PeakRidgeArray.at(PeakRidgeInx);
                    ridge.RT = nearestRidge.getX();
                    PeakRidgeList.emplace_back(ridge);
                    sort(PeakRidgeList.begin(), PeakRidgeList.end());
                    RemovedRidgeList.emplace_back(PeakRidgeInx);
                    for (size_t k = 0; k < PeakRidgeList.size(); k++) {
                        DisMatrixF[k][PeakRidgeInx] = noValuePlaceholder;
                    }
                    for (size_t l = 0; l < PeakRidgeArray.size(); l++) {
                        DisMatrixF[ExistingRideIdx][l] = noValuePlaceholder;
                    }
                }
                else {
                    conti = false;
                }
            }

            sort(RemovedRidgeList.rbegin(), RemovedRidgeList.rend());
            for (int removeridge : RemovedRidgeList) {
                PeakRidgeArray.erase(PeakRidgeArray.begin() + removeridge);
            }

            std::vector<int> removelist;
            for (size_t k = 0; k < PeakRidgeList.size(); k++) {
                const PeakRidge& existridge = PeakRidgeList.at(k);
                if (existridge.lowScale - i > 2 && existridge.ContinuousLevel < maxScale / 2) {
                    removelist.emplace_back(k);
                }
            }
            sort(removelist.rbegin(), removelist.rend());
            for (size_t k = 0; k < removelist.size(); k++)
                PeakRidgeList.erase(PeakRidgeList.begin() + removelist.at(k));

            if (i > maxScale / 2) {
                for (const XYData& ridge : PeakRidgeArray)
                {
                    PeakRidge newRidge;
                    newRidge.RT = ridge.getX();
                    newRidge.lowScale = i;
                    newRidge.ContinuousLevel++;
                    newRidge.intensity = SmoothData.GetPoinByXCloset(newRidge.RT).getY();
                    PeakRidgeList.emplace_back(newRidge);
                    sort(PeakRidgeList.begin(), PeakRidgeList.end());
                }
            }
            PeakRidgeArray.clear();
        }

        if (PeakRidgeList.size() <= 1) {
            PeakRegionList.emplace_back(SmoothData.Data.at(0).getX(), ApexRT, SmoothData.Data.at(SmoothData.PointCount() - 1).getX());
            NoRidgeRegion.emplace_back(vector<float>(1, ApexRT));
        }
        if (PeakRidgeList.size() > 1)
        {
            vector<XYData> ValleyPoints(PeakRidgeList.size() + 1);
            ValleyPoints[0] = SmoothData.Data.at(0);
            const PeakRidge* currentridge = &PeakRidgeList.at(0);
            XYData localmin{ -1, noValuePlaceholder };
            int startidx = SmoothData.GetLowerIndexOfX(currentridge->RT);

            for (size_t j = 1; j < PeakRidgeList.size(); j++)
            {
                const PeakRidge* nextridge = &PeakRidgeList.at(j);
                for (size_t i = startidx; i < SmoothData.Data.size(); i++)
                {
                    const XYData& point = SmoothData.Data.at(i);
                    if (point.getX() > currentridge->RT && point.getX() < nextridge->RT) {
                        if (localmin.getY() > point.getY()) {
                            localmin = point;
                        }
                    }
                    if (point.getX() >= nextridge->RT) {
                        startidx = i;
                        break;
                    }
                }
                ValleyPoints[j] = localmin;
                localmin = XYData { -1, noValuePlaceholder };
                currentridge = nextridge;
            }
            ValleyPoints[PeakRidgeList.size()] = SmoothData.Data.at(SmoothData.PointCount() - 1);

            //Correct ridge rt and intensity
            startidx = 0;
            for (size_t i = 0; i < PeakRidgeList.size(); i++)
            {
                PeakRidge& ridge = PeakRidgeList.at(i);
                for (size_t j = startidx; j < SmoothData.Data.size(); j++)
                {
                    XYData& point = SmoothData.Data.at(j);
                    if (point.getX() < ValleyPoints[i + 1].getX()) {
                        if (ridge.intensity < point.getY()) {
                            ridge.intensity = point.getY();
                            ridge.RT = point.getX();
                        }
                    }
                    else {
                        startidx = j;
                        break;
                    }
                }
            }

            //Find split points to generate peak regions
            vector<bool> Splitpoints(PeakRidgeList.size() - 1);
            int left = 0;
            int right = PeakRidgeList.size() - 1;
            FindSplitPoint(PeakRidgeList, left, right, ValleyPoints, Splitpoints);

#ifdef DIAUMPIRE_DEBUG
            if (MsLevel == 1)
            {
                ofstream splitPointsLog("DiaUmpireCpp-splitPoints.txt", std::ios::app);
                boost::format pointFormat(" %.2f");
                splitPointsLog << Index << " ValleyPoints";
                for (const auto& pt : ValleyPoints)
                {
                    splitPointsLog << (pointFormat % pt.x).str();
                }
                splitPointsLog << " PeakRidgeList";
                for (const auto& pt : PeakRidgeList)
                {
                    splitPointsLog << (pointFormat % pt.RT).str();
                }
                splitPointsLog << " SplitPoints";
                for (const auto& pt : Splitpoints)
                {
                    splitPointsLog << " " << (pt ? 1 : 0);
                }
                splitPointsLog << "\n";
            }
#endif

            vector<float> RidgeRTs;
            startidx = 0;
            const PeakRidge* maxridge = &PeakRidgeList.at(0);

            for (size_t i = 0; i < PeakRidgeList.size() - 1; i++)
            {
                RidgeRTs.emplace_back(PeakRidgeList.at(i).RT);
                if (PeakRidgeList.at(i).intensity > maxridge->intensity) {
                    maxridge = &PeakRidgeList.at(i);
                }
                if (Splitpoints[i]) {
                    PeakRegionList.emplace_back(ValleyPoints[startidx].getX(), maxridge->RT, ValleyPoints[i + 1].getX());
                    NoRidgeRegion.emplace_back(RidgeRTs);

                    maxridge = &PeakRidgeList.at(i + 1);
                    RidgeRTs.clear();
                    startidx = i + 1;
                }
            }
            RidgeRTs.emplace_back(PeakRidgeList.at(PeakRidgeList.size() - 1).RT);
            if (PeakRidgeList.at(PeakRidgeList.size() - 1).intensity > maxridge->intensity) {
                maxridge = &PeakRidgeList.at(PeakRidgeList.size() - 1);
            }
            PeakRegionList.emplace_back(ValleyPoints[startidx].getX(), maxridge->RT, ValleyPoints[PeakRidgeList.size()].getX());

            NoRidgeRegion.emplace_back(RidgeRTs);
        }
    }

    //Split if multiple peak curves are detected
    vector<PeakCurvePtr> SeparatePeakByRegion(float SN)
    {
        vector<PeakCurvePtr> tempArrayList;
        vector<PeakCurvePtr> returnArrayList;

        //Generate a peak curve for each detected region
        for (size_t i = 0; i < GetPeakRegionList().size(); i++) {
            tempArrayList.emplace_back(new PeakCurve(parameter));
            PeakCurvePtr& peakCurve = tempArrayList.back();
            peakCurve->Index = this->Index;
            peakCurve->RegionRidge = NoRidgeRegion.at(i);
            peakCurve->MsLevel = MsLevel;
            XYZData& region = GetPeakRegionList().at(i);
            if (region.getZ() - region.getX() > parameter.MaxCurveRTRange) {
                int leftidx = GetSmoothedList().GetLowerIndexOfX(region.getX());
                int rightidx = GetSmoothedList().GetHigherIndexOfX(region.getZ());
                XYData* left = &GetSmoothedList().Data.at(leftidx);
                XYData* right = &GetSmoothedList().Data.at(rightidx);
                while ((right->getX() - left->getX()) > parameter.MaxCurveRTRange) {
                    if (right->getX() - region.getY() <= parameter.MaxCurveRTRange / 4) {
                        leftidx++;
                    }
                    else if (region.getY() - left->getX() <= parameter.MaxCurveRTRange / 4) {
                        rightidx--;
                    }
                    else if (left->getY() < right->getY()) {
                        leftidx++;
                    }
                    else {
                        rightidx--;
                    }
                    left = &GetSmoothedList().Data.at(leftidx);
                    right = &GetSmoothedList().Data.at(rightidx);
                }
                region.x = left->getX();
                region.z = right->getX();
            }
        }


#ifdef DIAUMPIRE_DEBUG
        if (MsLevel == 1)
        {
            ofstream peakRegionLog("DiaUmpireCpp-peakRegion.txt", std::ios::app);
            boost::format pointFormat(" (%.2f, %.2f, %.2f)");
            boost::format floatFormat(" %.2f");
            peakRegionLog << Index << " PeakRegionList";
            for (auto& pt : PeakRegionList)
                peakRegionLog << (pointFormat % pt.x % pt.y % pt.z).str();
            peakRegionLog << "\n";
        }

        ofstream peakComparisonLog("DiaUmpireCpp-peakComparisons.txt", std::ios::app);
        boost::format pointFormat(" (%.6f, %.6f, %.6f, %d)");

        vector<vector<string>> comparisonsByRegion(GetPeakRegionList().size());

        ofstream peakLog("DiaUmpireCpp-ms1-peaks-at-separate-by-region.txt", std::ios::app);
        boost::format float6Format(" %.6f");
        if (MsLevel == 1)
            peakLog << Index;
#endif

        //Add corresponding raw peaks
        for (size_t i = 0; i < GetPeakList().size(); i++)
        {
            XYZData& peak = GetPeakList().at(i);
#ifdef DIAUMPIRE_DEBUG
            if (MsLevel == 1)
                peakLog << (float6Format % peak.x).str();
#endif
            for (size_t j = 0; j < GetPeakRegionList().size(); j++)
            {

                XYZData& region = GetPeakRegionList().at(j);

#ifdef DIAUMPIRE_DEBUG
                bool addPeak = isDefinitelyGreaterThan(peak.getX(), region.getX(), 1e-8f, true) && isDefinitelyLessThan(peak.getX(), region.getZ(), 1e-8f, true);

                if (MsLevel == 1)
                    comparisonsByRegion[j].emplace_back((pointFormat % peak.getX() % region.getX() % region.getZ() % (addPeak ? 1 : 0)).str());
#endif
                if (isDefinitelyGreaterThan(peak.getX(), region.getX(), 1e-8f, true) && isDefinitelyLessThan(peak.getX(), region.getZ(), 1e-8f, true)) {
                    tempArrayList.at(j)->AddPeak(peak);
                    break;
                }
            }
        }

#ifdef DIAUMPIRE_DEBUG
        if (MsLevel == 1)
        {
            peakLog << "\n";
            for (int j = 0; j < GetPeakRegionList().size(); j++)
            {
                peakComparisonLog << Index << " " << GetPeakList().size() << " " << j << " RegionPeakList";
                for (auto& comparison : comparisonsByRegion[j])
                    peakComparisonLog << comparison;
                peakComparisonLog << " " << tempArrayList.at(j)->GetPeakList().size() << "\n";
            }
        }
#endif

        //Add corresponding smoothed peaks
        for (size_t i = 0; i < GetSmoothedList().Data.size(); i++)
        {
            XYData& peak = GetSmoothedList().Data.at(i);
            for (size_t j = 0; j < GetPeakRegionList().size(); j++)
            {
                XYZData& region = GetPeakRegionList().at(j);
                if (isDefinitelyGreaterThan(peak.getX(), region.getX(), 1e-8f, true) && isDefinitelyLessThan(peak.getX(), region.getZ(), 1e-8f, true)) {
                    tempArrayList.at(j)->GetSmoothedList().Data.emplace_back(peak);
                    break;
                }
            }
        }

        for (PeakCurvePtr& peak : tempArrayList)
        {
            if (peak->PeakList.size() > 2) {
                //peak->GetSmoothedList().Data.clear();
                returnArrayList.push_back(std::move(peak));
            }
        }
        return returnArrayList;
    }

    PeakCurve(const InstrumentParameter& parameter) : parameter(parameter) {}

    float StartInt() const {
        if (startint == 0) {
            startint = PeakList.at(0).getZ();
        }
        return startint;
    }

    float StartRT() const {
        if (startrt == -1) {
            if (SmoothData.Data.size() > 0) {
                startrt = SmoothData.Data.at(0).getX();
            }
            else {
                startrt = PeakList.at(1).getX();
            }
        }
        return startrt;
    }
    float _snr = -1;

    float GetSNR() {
        if (_snr == -1) {
            _snr = ApexInt;
        }
        return _snr;
    }

    float GetMaxIntensityByRegionRange(float StartRT, float EndRT)
    {
        float max = 0;
        for (int j = 0; j < GetSmoothedList().PointCount(); j++) {
            const XYData& pt = GetSmoothedList().Data.at(j);
            if (pt.getX() >= StartRT && pt.getX() <= EndRT && pt.getY() > max) {
                max = pt.getY();
            }
        }
        return max;
    }
    float _baseLine = -1;

    float GetBaseLine() {
        if (_baseLine == -1) {
            CalculateBaseLine();
            if (_baseLine == 0) {
                _baseLine = 1;
            }
        }
        return _baseLine;
    }
    float _noiseLevel = -1;

    float GetNoiseLevel() {
        if (_noiseLevel == -1) {
            CalculateBaseLine();
        }
        return _noiseLevel;
    }

    float EndRT() const {
        if (endrt == -1) {
            endrt = PeakList.at(PeakList.size() - 2).getX();
        }
        return endrt;
    }

    float LastScanRT() {
        return PeakList.at(PeakList.size() - 1).getX();
    }

    XYPointCollection GetPeakCollection() const
    {
        XYPointCollection PtCollection;

        for (size_t i = 0; i < SmoothData.Data.size(); i++) {
            PtCollection.AddPoint(SmoothData.Data.at(i).getX(), SmoothData.Data.at(i).getY());
        }
        return PtCollection;
    }

    XYPointCollection GetSmoothPeakCollection(float startRT, float endRT) const
    {
        XYPointCollection PtCollection;

        for (int i = 0; i < SmoothData.PointCount(); i++) {
            const XYData& pt = SmoothData.Data.at(i);
            if (pt.getX() > endRT) {
                break;
            }
            else if (pt.getX() >= startRT && pt.getX() <= endRT) {
                PtCollection.AddPoint(pt.getX(), pt.getY());
            }
        }
        return PtCollection;
    }

    float DetermineIntByRTRange(float StartRT, float EndRT) const
    {
        float Intensity = 0;
        for (int j = 0; j < GetSmoothedList().PointCount(); j++) {
            const XYData& pt = GetSmoothedList().Data.at(j);
            if (pt.getX() >= StartRT && pt.getX() <= EndRT) {
                if (pt.getY() > Intensity) {
                    Intensity = pt.getY();
                }
            }
        }
        return Intensity;
    }

    float RTWidth() const
    {
        float Width = 0;
        if (PeakList.size() > 0) {
            Width = PeakList.at(PeakList.size() - 1).getX() - PeakList.at(0).getX();
        }
        else if (SmoothData.PointCount() > 0) {
            Width = SmoothData.Data.at(SmoothData.PointCount() - 1).getX() - SmoothData.Data.at(0).getX();
        }
        if (Width < 0)
            throw runtime_error("[DiaUmpire::PeakCurve::RTWidth] peak times out of order");

        return Width;
    }

    vector<XYZData>& GetPeakList() {
        return PeakList;
    }

    const vector<XYZData>& GetPeakList() const {
        return PeakList;
    }

    XYPointCollection& GetSmoothedList() {
        return SmoothData;
    }

    const XYPointCollection& GetSmoothedList() const {
        return SmoothData;
    }

    vector<XYZData>& GetPeakRegionList() {
        return PeakRegionList;
    }

    const vector<XYZData>& GetPeakRegionList() const {
        return PeakRegionList;
    }

    void ReleasePeakData() {

        PeakList.clear();
        SmoothData.Data.clear();
        PeakRegionList.clear();
    }

    void ReleaseRawPeak() {
        pwiz::util::deallocate(PeakList);
        pwiz::util::deallocate(PeakRegionList);
    }

    void AddPeak(XYZData xYZPoint)
    {
        if (!PeakList.empty() && xYZPoint.getX() < PeakList.back().getX())
            throw runtime_error("[DiaUmpire::PeakCurve::AddPeak] scan time is not monotonically increasing");

        PeakList.emplace_back(xYZPoint);
        TotalIntMzF += xYZPoint.getY() * xYZPoint.getZ() * xYZPoint.getZ();
        TotalIntF += xYZPoint.getZ() * xYZPoint.getZ();
        if (xYZPoint.getZ() > ApexInt) {
            ApexInt = xYZPoint.getZ();
            ApexRT = xYZPoint.getX();
        }
        if (xYZPoint.getZ() < minIntF) {
            minIntF = xYZPoint.getZ();
        }
        TargetMz = TotalIntMzF / TotalIntF;
    }

    void CalculateMzVar() {
        MzVar = 0;
        for (size_t j = 0; j < PeakList.size(); j++) {
            MzVar += (PeakList.at(j).getX() - TargetMz) * (PeakList.at(j).getX() - TargetMz);
        }
        MzVar /= PeakList.size();
    }

    private:

    void CalculateBaseLine()
    {
        _baseLine = 0;
        std::queue<float> IntensityQueue;
        for (const XYData& point : SmoothData.Data) {
            IntensityQueue.emplace(point.getY());
        }

        if (IntensityQueue.size() > 10) {
            for (size_t i = 0; i < IntensityQueue.size() / 10; i++)
            {
                _baseLine += IntensityQueue.front();
                IntensityQueue.pop();
            }
            _baseLine /= (IntensityQueue.size() / 10);
        }
        else {
            _baseLine = IntensityQueue.front();
        }
    }

    void FindSplitPoint(vector<PeakRidge>& peakRidgeList, int left, int right, vector<XYData>& ValleyPoints, vector<bool>& splitpoints)
    {
        for (int i = left; i < right; i++)
        {
            if (ValidSplitPoint(peakRidgeList, left, right, i, ValleyPoints))
            {
                splitpoints[i] = true;
                FindSplitPoint(peakRidgeList, left, i, ValleyPoints, splitpoints);
                FindSplitPoint(peakRidgeList, i + 1, right, ValleyPoints, splitpoints);
                break;
            }
        }
    }

    bool ValidSplitPoint(vector<PeakRidge>& peakRidgeList, int left, int right, int cut, vector<XYData>& ValleyPoints)
    {
        PeakRidge* leftridge = &peakRidgeList.at(left);
        PeakRidge* rightridge = &peakRidgeList.at(cut + 1);

        for (int i = left; i <= cut; i++)
        {
            if (peakRidgeList.at(i).intensity > leftridge->intensity) {
                leftridge = &peakRidgeList.at(i);
            }
        }
        for (int i = cut + 1; i <= right; i++)
        {
            if (peakRidgeList.at(i).intensity > rightridge->intensity) {
                rightridge = &peakRidgeList.at(i);
            }
        }
        float leftValley = abs(ValleyPoints[left].getY() - ValleyPoints[cut + 1].getY()) / leftridge->intensity;
        float rightValley = abs(ValleyPoints[cut + 1].getY() - ValleyPoints[right + 1].getY()) / rightridge->intensity;

        return (leftValley < parameter.SymThreshold &&
                rightValley < parameter.SymThreshold);
    }
};

typedef PeakCurve::PeakCurvePtr PeakCurvePtr;


struct PeakCurveCorrCalc
{
    static float CalPeakCorr_Overlap(PeakCurve const& peakA, PeakCurve const& peakB, int Astart, int Aend, int Bstart, int Bend, int NoPeakPerMin)
    {
        return CalPeakCorr_Overlap(peakA, peakB, Astart, Aend, Bstart, Bend, false, NoPeakPerMin);
    }

    static float CalPeakCorr(PeakCurve const& peakA, PeakCurve const& peakB, int NoPointPerMin)
    {
        float startRT = std::max(peakA.StartRT(), peakB.StartRT());
        float endRT = std::min(peakA.EndRT(), peakB.EndRT());
        XYPointCollection const& PeakACollection = peakA.GetSmoothPeakCollection(startRT, endRT);
        XYPointCollection const& PeakBCollection = peakB.GetSmoothPeakCollection(startRT, endRT);
        float corre = 0;

        //double corre2 = 0f;
        if (PeakACollection.Data.size() > 0 && PeakBCollection.Data.size() > 0) {
            corre = PearsonCorr::CalcCorr(PeakACollection, PeakBCollection, NoPointPerMin);
        }
        return corre;
    }

    static float CalPeakCorr_Overlap(PeakCurve const& peakA, PeakCurve const& peakB, int Astart, int Aend, int Bstart, int Bend, bool output, int NoPeakPerMin)
    {
        float startRT = std::max(peakA.GetPeakRegionList().at(Astart).getX(), peakB.GetPeakRegionList().at(Bstart).getX());
        float endRT = std::min(peakA.GetPeakRegionList().at(Aend).getZ(), peakB.GetPeakRegionList().at(Bend).getZ());
        XYPointCollection const& PeakACollection = peakA.GetSmoothPeakCollection(startRT, endRT);
        XYPointCollection const& PeakBCollection = peakB.GetSmoothPeakCollection(startRT, endRT);
        float corre = 0;
        if (PeakACollection.Data.size() > 0 && PeakBCollection.Data.size() > 0) {
            corre = PearsonCorr::CalcCorr(PeakACollection, PeakBCollection, NoPeakPerMin);
            if (output) {
                std::ofstream writer("PeakA.csv");
                for (int i = 0; i < PeakACollection.PointCount(); i++) {
                    writer << PeakACollection.Data.at(i).getX() << "," << PeakACollection.Data.at(i).getY() << "\n";
                }

                std::ofstream writer2("PeakB.csv");
                for (int i = 0; i < PeakBCollection.PointCount(); i++) {
                    writer2 << PeakBCollection.Data.at(i).getX() << "," << PeakBCollection.Data.at(i).getY() << "\n";
                }
            }
        }
        return corre;
    }
};

} // namespace DiaUmpire

#endif // _DIAUMPIRE_PEAKCURVE_
