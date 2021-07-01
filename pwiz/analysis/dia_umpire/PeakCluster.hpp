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

#ifndef _DIAUMPIRE_PEAKCLUSTER_
#define _DIAUMPIRE_PEAKCLUSTER_

#pragma warning( push )
#pragma warning( disable : 4457 ) // boost/geometry/index/rtree.hpp: declaration of 'median' hides function parameter

#include <vector>
#include <limits>
#include <iostream>
#include <fstream>
#include "InstrumentParameter.hpp"
#include "ScanData.hpp"
#include "PeakCurve.hpp"
#include "IsotopePatternMap.hpp"
#include <boost/container/flat_map.hpp>
#include <boost/geometry.hpp> // compile error without top-level header
#include <boost/geometry/geometries/point.hpp>
#include <boost/geometry/index/rtree.hpp>

namespace DiaUmpire {

using std::map;
using std::multimap;
using std::vector;
using std::set;
using std::string;
using std::runtime_error;

namespace bg = boost::geometry;
namespace bgi = bg::index;

typedef bg::model::point<float, 2, bg::cs::cartesian> ApexRtTargetMzPoint;
typedef pair<ApexRtTargetMzPoint, PeakCurvePtr> PeakCurveTreeNode;
typedef bgi::rtree<PeakCurveTreeNode, bgi::linear<3>> PeakCurveSearchTree;
typedef bg::model::box<ApexRtTargetMzPoint> PeakCurveSearchBox;


struct PeakOverlapRegion
{
    int PeakCurveIndexA;
    int PeakCurveIndexB;
    float Correlation;
};

struct PrecursorFragmentPairEdge : public PeakOverlapRegion
{
    float FragmentMz;
    float Intensity;
    float DeltaApex;
    float RTOverlapP;
    int FragmentMS1Rank = 0;
    float FragmentMS1RankScore = 1.f;
    float AdjustedFragInt;
    bool ComplementaryFragment = false;
    float MatchedFragMz;
};


/**
 * Peak isotope cluster data structure 
 * @author Chih-Chiang Tsou <chihchiang.tsou@gmail.com>
 */
class PeakCluster
{
    mutable vector<float> SNR;
    XYPointCollection FragmentScan;
    set<float> MatchScores;
    mutable float conflictCorr = -1;
    mutable float mass = 0;
    const ChiSquareGOF& chiSquaredGof;
    const pwiz::msdata::MSData& msd;

    public:

    int Index;
    vector<PeakCurvePtr> IsoPeaksCurves;
    PeakCurvePtr MonoIsotopePeak;
    vector<int> IsoPeakIndex;
    vector<float> Corrs;
    vector<float> PeakHeight;
    vector<float> PeakHeightRT;
    vector<float> PeakArea;
    mutable vector<float> mz;
    float startRT = std::numeric_limits<float>::max();
    float endRT = std::numeric_limits<float>::min();
    int StartScan;
    int EndScan;
    int Charge;
    float IsoMapProb = -1;
    vector<float> PeakDis;    
    int NoRidges;
    float OverlapP;
    vector<float> OverlapRT;
    float LeftInt;
    float RightInt;
    bool Identified;
    string AssignedPepIon = "";
    vector<PrecursorFragmentPairEdge> GroupedFragmentPeaks;
    float MS1Score;
    float MS1ScoreLocalProb;
    float MS1ScoreProbability;
    string SpectrumKey;

    PeakCluster(int IsotopicNum, int Charge, const ChiSquareGOF& chiSquaredGof, const pwiz::msdata::MSData& msd) : chiSquaredGof(chiSquaredGof), msd(msd)
    {
        IsoPeaksCurves.resize(IsotopicNum);
        Corrs.resize(IsotopicNum - 1);
        SNR.resize(IsotopicNum);
        OverlapRT.resize(IsotopicNum-1);
        PeakHeight.resize(IsotopicNum);
        PeakHeightRT.resize(IsotopicNum);
        PeakArea.resize(IsotopicNum);
        IsoPeakIndex.resize(IsotopicNum);
        //PeakDis=new float[IsotopicNum];
        mz.resize(IsotopicNum);
        for (int i = 0; i < IsotopicNum; i++) {
            SNR[i] = -1;
        }
        this->Charge = Charge;
        Identified = false;
        MS1Score = MS1ScoreLocalProb = MS1ScoreProbability = 0;
    }

    /*XYPointCollection GetNormalizedFragmentScan()
    {
        if (FragmentScan != nullptr)
            return FragmentScan;

        FragmentScan = new XYPointCollection();
        for (PrecursorFragmentPairEdge fragment : GroupedFragmentPeaks) {
            FragmentScan.AddPoint(fragment.FragmentMz, fragment.Intensity);
        }
        FragmentScan.Data.Finalize();
        Binning bining = new Binning();
        if (FragmentScan.PointCount() > 2) {
            FragmentScan=ScoreFunction.SpectralNormalizationForScan(bining.Binning(FragmentScan, 0f, nullptr));
        }

        return FragmentScan;
    }*/
    
    void AddScore(float score) {
        MatchScores.insert(score);
    }
    int GetScoreRank(float score) {
        if (MatchScores.empty())
            return -1;
        return MatchScores.size() - std::distance(MatchScores.begin(), MatchScores.upper_bound(score))+1;
    }
    
    int GetQualityCategory() {
        if ((IsoPeaksCurves.empty() ||IsoPeaksCurves[2] == nullptr) && mz[2] == 0.0f) {
            return 2;
        }
        return 1;
    }

    void SetMz(int pkidx, float value) {
        mz[pkidx] = value;
    }
    float RTVar = 0;

    float GetConflictCorr() const {
        if (conflictCorr == -1) {
            conflictCorr = IsoPeaksCurves[0]->ConflictCorr;
        }
        return conflictCorr;
    }

    void SetConflictCorr(float ConflictCorr) {
        conflictCorr = ConflictCorr;
    }

    float TargetMz() const
    {
        if (mz[0] == 0) {
            mz[0] = IsoPeaksCurves[0]->TargetMz;
        }
        return mz[0];
    }

    void SetSNR(int pkidx, float _snr) {
        SNR[pkidx] = _snr;
    }

    float GetSNR(int pkidx) const {
        if (SNR[pkidx] == -1) {
            if (!IsoPeaksCurves.empty() && IsoPeaksCurves[pkidx] != nullptr) {
                SNR[pkidx] = IsoPeaksCurves[pkidx]->GetRawSNR();
            }
            else if (pkidx==1){
                //Logger.getRootLogger().error("Failed to get SNR");
            }
        }
        return SNR[pkidx];
    }

    float NeutralMass() const {
        if (mass == 0) {
            if (MonoIsotopePeak != nullptr) {
                mass = Charge * (MonoIsotopePeak->TargetMz - (float) pwiz::chemistry::Proton);
            }
            else {
                mass = Charge * (mz[0] - (float) pwiz::chemistry::Proton);
            }
        }
        return mass;
    }

    void UpdateIsoMapProb(const IsotopePatternMap& isotopePatternMap) {
        if (IsoMapProb == -1) {
            IsoMapProb = GetChiSquareProbByIsoMap(isotopePatternMap);
        }
    }

    void AssignConfilictCorr() {
        for (size_t i = 1; i < IsoPeaksCurves.size(); i++) {
            if (IsoPeaksCurves[i] != nullptr) {
                if (Corrs[i - 1] > 0.6f) {
                    IsoPeaksCurves[i]->AddConflictScore(Corrs[i - 1]);
                }
            }
        }
    }

    void CalcPeakArea_V2() {
        int NoOfIsotopic = IsoPeaksCurves.size();

        PeakCurvePtr peakA = MonoIsotopePeak;
        startRT = MonoIsotopePeak->StartRT();
        endRT = MonoIsotopePeak->EndRT();
        
        if (IsoPeaksCurves.size()>1 && IsoPeaksCurves[1]!=nullptr) {
            startRT = min(MonoIsotopePeak->StartRT(), IsoPeaksCurves[1]->StartRT());
            endRT = max(MonoIsotopePeak->EndRT(), IsoPeaksCurves[1]->EndRT());
        }
        
        if(endRT==startRT){
            startRT=MonoIsotopePeak->GetSmoothedList().Data.at(0).getX();
            endRT=MonoIsotopePeak->GetSmoothedList().Data.back().getX();
        }

        NoRidges = 0;
        if (!peakA->RegionRidge.empty()) {
            for (float ridge : peakA->RegionRidge) {
                if (ridge >= startRT && ridge <= endRT) {
                    NoRidges++;
                }
            }
        }

        for (int i = 0; i < NoOfIsotopic; i++) {
            PeakCurvePtr peak = IsoPeaksCurves[i];
            if (peak == nullptr) {
                break;
            }
            for (int j = 0; j < peak->GetSmoothedList().PointCount(); j++) {
                const XYData& pt = peak->GetSmoothedList().Data.at(j);
                if (pt.getX() >= startRT && pt.getX() <= endRT) {
                    PeakArea[i] += pt.getY();
                    if (pt.getY() > PeakHeight[i]) {
                        PeakHeight[i] = pt.getY();
                        PeakHeightRT[i] = pt.getX();
                    }
                }
            }
            mz[i]=peak->TargetMz;
            IsoPeakIndex[i]=peak->Index;
        }        
    }

    //Generate isotope peak distribution
    void GeneratePeakDis() {
        if (!PeakDis.empty()) {
            return;
        }
        PeakDis.resize(PeakHeight.size());
        float firstPeak = PeakHeight[0];
        for (size_t i = 0; i < PeakDis.size(); i++) {
            if (PeakHeight[i] > 0) {
                PeakDis[i] = PeakHeight[i] / firstPeak;
            }
        }
    }

    //Get isotope pattern range according the mass of this peak cluster
    vector<XYData> GetPatternRange(const IsotopePatternMap& isotopePatternMap) const
    {
        vector<XYData> PatternRange(isotopePatternMap.size());
        for (size_t i = 0; i < isotopePatternMap.size(); i++)
        {
            auto findItr = isotopePatternMap[i].upper_bound(NeutralMass());
            if (findItr == isotopePatternMap[i].end()) {
                findItr = --isotopePatternMap[i].end();
            }
            PatternRange[i] = { findItr->second.begin, findItr->second.end };
        }
        return PatternRange;
    }


    float GetChiSquareProbByIsoMap(const IsotopePatternMap& isotopePatternMap)
    {
        GeneratePeakDis();
        vector<XYData> PatternRange(isotopePatternMap.size());
        for (size_t i = 0; i < isotopePatternMap.size(); i++)
        {
            auto findItr = isotopePatternMap[i].upper_bound(NeutralMass());
            if (findItr == isotopePatternMap[i].end()) {
                findItr = --isotopePatternMap[i].end();
            }
            PatternRange[i] = { findItr->second.begin, findItr->second.end };
        }
        vector<float> TheoIso(isotopePatternMap.size());

        TheoIso[0] = 1;

        for (size_t i = 1; i < isotopePatternMap.size(); i++) {
            if (PeakDis[i] >= PatternRange[i - 1].getY() && PeakDis[i] <= PatternRange[i - 1].getX()) {
                TheoIso[i] = PeakDis[i];
            } else {
                if (abs(PeakDis[1] - PatternRange[i - 1].getY()) > abs(PeakDis[i] - PatternRange[i - 1].getX())) {
                    TheoIso[i] = PatternRange[i - 1].getX();
                } else {
                    TheoIso[i] = PatternRange[i - 1].getY();
                }
            }
        }
        float prob = chiSquaredGof.GetGoodNessOfFitProb(TheoIso, PeakDis);

        return prob;
    }

    //Check is the number of detected isotope peaks passes the criterion
    bool IsotopeComplete(int minIsonum) const
    {
        for (int i = 0; i < minIsonum; i++) {
            if ((IsoPeaksCurves.empty() || IsoPeaksCurves[i] == nullptr) && mz[i] == 0.0f) {
                return false;
            }
        }
        return true;
    }

    float GetMaxMz() const
    {
        for (int i = (int) mz.size() - 1; i > 0; i--) {
            if (mz[i] > 0.0f) {
                return mz[i];
            }
        }
        return mz[0];
    }
};

typedef std::shared_ptr<PeakCluster> PeakClusterPtr;


class PeakCurveClusteringCorrKDtree
{
    vector<PeakCurvePtr> const& peakCurves;
    size_t targetCurveIndex;
    //PeakCurvePtr peakA;
    InstrumentParameter& parameter;
    MassDefect MD;
    const PeakCurveSearchTree& peakCurveSearchTree;
    const IsotopePatternMap& isotopePatternMap;
    const ChiSquareGOF& chiSquaredGof;
    const pwiz::msdata::MSData& msd;
    int MaxNoOfClusters;
    int MinNoOfClusters;
    int StartCharge;
    int EndCharge;
    boost::mutex& mx;

    public:

    vector<PeakClusterPtr> ResultClusters;


    PeakCurveClusteringCorrKDtree(vector<PeakCurvePtr> const& peakCurves, size_t targetCurveIndex, const PeakCurveSearchTree& peakCurveSearchTree, InstrumentParameter& parameter,
                                  const IsotopePatternMap& isotopePatternMap, const ChiSquareGOF& chiSquaredGof, const pwiz::msdata::MSData& msd,
                                  int StartCharge, int EndCharge, int MaxNoClusters, int MinNoClusters, boost::mutex& mx)
        : peakCurves(peakCurves), targetCurveIndex(targetCurveIndex), parameter(parameter), peakCurveSearchTree(peakCurveSearchTree), isotopePatternMap(isotopePatternMap), chiSquaredGof(chiSquaredGof), msd(msd), mx(mx)
    {
        this->MaxNoOfClusters = MaxNoClusters;
        this->MinNoOfClusters = MinNoClusters;
        this->StartCharge = StartCharge;
        this->EndCharge = EndCharge;
    }

    ~PeakCurveClusteringCorrKDtree()
    {

    }

    void operator()()
    {
        auto const& peakA = peakCurves[targetCurveIndex];
        float lowrt = peakA->ApexRT - parameter.DeltaApex - 1e-4;
        float highrt = peakA->ApexRT + parameter.DeltaApex + 1e-4;
        float lowmz = InstrumentParameter::GetMzByPPM(peakA->TargetMz - 1e-4, 1, parameter.MS1PPM);
        float highmz = InstrumentParameter::GetMzByPPM((peakA->TargetMz + 1e-4 + ((float)MaxNoOfClusters / StartCharge)), 1, -parameter.MS1PPM);

        boost::container::flat_multimap<float, PeakCurvePtr> PeakCurveListMZ;
        for (auto const& peakCurve : peakCurveSearchTree | bgi::adaptors::queried(bgi::within(PeakCurveSearchBox(ApexRtTargetMzPoint(lowrt, lowmz), ApexRtTargetMzPoint(highrt, highmz)))))
            PeakCurveListMZ.insert(make_pair(peakCurve.second->TargetMz, peakCurve.second));

#ifdef DIAUMPIRE_DEBUG
        if (peakA->MsLevel == 2)
        {
            std::ofstream peakSearchTreeLog(("DiaUmpireCpp-peakSearchTreeLog-" + msd.run.id + ".txt").c_str(), std::ios::app);
            peakSearchTreeLog << (boost::format("%d %.4f-%.4f %.4f-%.4f") % peakA->Index % lowrt % highrt % lowmz % highmz).str();
            boost::format peakFormat(" %.4f");
            for (auto& itr : PeakCurveListMZ)
            {
                peakSearchTreeLog << (peakFormat % itr.first);
            }
            peakSearchTreeLog << "\n";
        }
#endif

        float Arange = peakA->EndRT() - peakA->StartRT();
        for (int charge = EndCharge; charge >= StartCharge; charge--)
        {
            float mass = charge * (peakA->TargetMz - (float)pwiz::chemistry::Proton);
            if (mass<parameter.MinPrecursorMass || mass>parameter.MaxPrecursorMass || (parameter.MassDefectFilter && !MD.InMassDefectRange(mass, parameter.MassDefectOffset))) {
                continue;
            }
            PeakClusterPtr peakClusterPtr(new PeakCluster(MaxNoOfClusters, charge, chiSquaredGof, msd));
            PeakCluster& peakCluster = *peakClusterPtr;
            peakCluster.IsoPeaksCurves[0] = peakA;
            peakCluster.MonoIsotopePeak = peakA;
            vector<XYData> Ranges(MaxNoOfClusters - 1);
            for (int i = 0; i < MaxNoOfClusters - 1; i++)
            {
                if (isotopePatternMap[i].empty())
                    throw runtime_error("empty isotopePatternMap");

                auto findItr = isotopePatternMap[i].upper_bound(peakCluster.NeutralMass());
                if (findItr == isotopePatternMap[i].end()) {
                    findItr = --isotopePatternMap[i].end();
                }
                Ranges[i] = XYData(findItr->second.begin, findItr->second.end);
            }

            for (int pkidx = 1; pkidx < MaxNoOfClusters; pkidx++)
            {
                float ppmthreshold = parameter.MS1PPM + (parameter.MS1PPM * pkidx * 0.5f);
                float lowtheomz = InstrumentParameter::GetMzByPPM(peakA->TargetMz + (pkidx * ((float)pwiz::chemistry::Proton / charge)), charge, ppmthreshold);
                float uptheomz = InstrumentParameter::GetMzByPPM(peakA->TargetMz + (pkidx * ((float)pwiz::chemistry::Proton / charge)), charge, -ppmthreshold);
                auto startmzidx = PeakCurveListMZ.lower_bound(lowtheomz);

                float theomz = peakA->TargetMz + (pkidx * ((float)pwiz::chemistry::Proton / charge));
                float maxscore = 0;
                float maxcorr = 0;
                float maxoverlap = 0;
                const PeakCurvePtr* closestPeak = nullptr;

                for (auto mzidx = startmzidx; mzidx != PeakCurveListMZ.end(); ++mzidx)
                {
                    const PeakCurvePtr& peakB = mzidx->second;

                    if (peakB->TargetMz <= peakA->TargetMz) {
                        continue;
                    }
                    if (peakB->TargetMz > uptheomz) {
                        break;
                    }

                    float Brange = peakB->EndRT() - peakB->StartRT();
                    float OverlapP = 0;
                    if (peakA->StartRT() >= peakB->StartRT() && peakA->StartRT() <= peakB->EndRT() && peakA->EndRT() >= peakB->EndRT()) {
                        OverlapP = (peakB->EndRT() - peakA->StartRT()) / Brange;

                    }
                    else if (peakA->EndRT() >= peakB->StartRT() && peakA->EndRT() <= peakB->EndRT() && peakA->StartRT() <= peakB->StartRT()) {
                        OverlapP = (peakA->EndRT() - peakB->StartRT()) / Brange;

                    }
                    else if (peakA->StartRT() <= peakB->StartRT() && peakA->EndRT() >= peakB->EndRT()) {
                        OverlapP = 1;

                    }
                    else if (peakA->StartRT() >= peakB->StartRT() && peakA->EndRT() <= peakB->EndRT()) {
                        OverlapP = Arange / Brange;
                    }

                    if (parameter.TargetIDOnly || (OverlapP > parameter.MiniOverlapP && (!parameter.CheckMonoIsotopicApex || (peakA->ApexRT >= peakB->StartRT() && peakA->ApexRT <= peakB->EndRT() && peakB->ApexRT >= peakA->StartRT() && peakB->ApexRT <= peakA->EndRT()))))
                    {
                        float ppm = InstrumentParameter::CalcPPM(theomz, peakB->TargetMz);
                        if (ppm < ppmthreshold)
                        {
                            float corr = PeakCurveCorrCalc::CalPeakCorr(*peakA, *peakB, parameter.NoPeakPerMin);
                            if (std::isnan(corr)) {
                                corr = 0;
                                //System.out.print("Corr=NAN\n");
                            }

                            float PeakIntA = peakA->ApexInt;
                            float PeakIntB = peakB->GetMaxIntensityByRegionRange(max(peakA->StartRT(), peakB->StartRT()), min(peakB->EndRT(), peakA->EndRT()));

                            if ((parameter.TargetIDOnly && corr > 0.2f) || corr > parameter.IsoCorrThreshold)
                            {
                                //if (corr > parameter.IsoCorrThreshold || (PeakIntA > PeakIntB*1.5f && PeakIntB>0.1f * PeakIntA && (peakA->EndScan-peakA->StartScan)>4 && (peakB->EndScan-peakB->StartScan)>4)) {
                                float intscore = 0;
                                float IntRatio = PeakIntB / PeakIntA;

                                if (IntRatio > Ranges[pkidx - 1].getY() && IntRatio <= Ranges[pkidx - 1].getX()) {
                                    intscore = 1;
                                }
                                else {
                                    if (abs(IntRatio - Ranges[pkidx - 1].getY()) > abs(IntRatio - Ranges[pkidx - 1].getX())) {
                                        intscore = 1 - abs(IntRatio - Ranges[pkidx - 1].getX());
                                    }
                                    else {
                                        intscore = 1 - abs(IntRatio - Ranges[pkidx - 1].getY());
                                    }
                                }
                                if (intscore < 0) {
                                    intscore = 0;
                                }
                                float score = ((ppmthreshold - ppm) / ppmthreshold) + corr + intscore;

                                if (maxscore < score) {
                                    maxscore = score;
                                    closestPeak = &peakB;
                                    maxcorr = corr;
                                    maxoverlap = OverlapP;
                                }
                            }
                        }
                    }
                }

                if (closestPeak != nullptr)
                {
                    peakCluster.Corrs[pkidx - 1] = maxcorr;
                    peakCluster.IsoPeaksCurves[pkidx] = *closestPeak;
                    peakCluster.OverlapRT[pkidx - 1] = maxoverlap;
                    peakCluster.GetSNR(pkidx - 1);

                    if (pkidx == 1) {
                        peakCluster.OverlapP = maxoverlap;
                    }
                }
                else
                    break;
            }

            if (peakCluster.IsotopeComplete(MinNoOfClusters))
            {
                peakCluster.CalcPeakArea_V2();
                peakCluster.UpdateIsoMapProb(isotopePatternMap);
                {
                    boost::lock_guard<boost::mutex> g(mx);
                    peakCluster.AssignConfilictCorr();
                }
                peakCluster.LeftInt = peakA->GetSmoothedList().Data.at(0).getY();
                peakCluster.RightInt = peakA->GetSmoothedList().Data.at(peakA->GetSmoothedList().PointCount() - 1).getY();
                if (parameter.TargetIDOnly || peakCluster.IsoMapProb > parameter.IsoPattern)
                {
                    ResultClusters.push_back(peakClusterPtr);
                    if (!parameter.TargetIDOnly || (parameter.RemoveGroupedPeaks && peakCluster.Corrs[0] > parameter.RemoveGroupedPeaksCorr && peakCluster.OverlapP > parameter.RemoveGroupedPeaksRTOverlap))
                    {
                        for (size_t i = 1; i < peakCluster.IsoPeaksCurves.size(); i++)
                        {
                            PeakCurvePtr& peak = peakCluster.IsoPeaksCurves[i];
                            if (peak && peakCluster.Corrs[i - 1] > parameter.RemoveGroupedPeaksCorr && peakCluster.OverlapRT[i - 1] > parameter.RemoveGroupedPeaksRTOverlap) {
                                boost::lock_guard<boost::mutex> g(mx);
                                peak->ChargeGrouped.insert(charge);
                            }
                        }
                    }
                }
            }
        }

        //System.out.print("....done\n");
    }
};


/**
 * Thread unit for calculating peak profile correlation between a PeakCluster and all coeluting peak curves
 * @author Chih-Chiang Tsou <chihchiang.tsou@gmail.com>
 */
class CorrCalcCluster2Curve
{
    multimap<float, PeakCurvePtr> const& PeakCurveSortedListApexRT;
    const InstrumentParameter& parameter;

    public:

    PeakClusterPtr MS1PeakCluster;
    vector<PrecursorFragmentPairEdge> GroupedFragmentList;


    CorrCalcCluster2Curve(PeakClusterPtr MS1PeakCluster, multimap<float, PeakCurvePtr> const& PeakCurveSortedListApexRT, const InstrumentParameter& parameter)
        : PeakCurveSortedListApexRT(PeakCurveSortedListApexRT), parameter(parameter), MS1PeakCluster(MS1PeakCluster)
    {
    }


    void operator()()
    {
        //Get Start and End indices of peak curves which are in the RT range
        auto startRTitr = PeakCurveSortedListApexRT.lower_bound(MS1PeakCluster->PeakHeightRT[0] - parameter.DeltaApex);
        auto endRTitr = PeakCurveSortedListApexRT.lower_bound(MS1PeakCluster->PeakHeightRT[0] + parameter.DeltaApex);
        PeakCurve const& targetMS1Curve = *MS1PeakCluster->MonoIsotopePeak;

        //Calculate RT range of the peak cluster
        float ms1rtrange = targetMS1Curve.EndRT() - targetMS1Curve.StartRT();
        int highCorrCnt = 0;

        //For each peak curve
        for (auto const& itr : boost::make_iterator_range(startRTitr, endRTitr))
        {
            PeakCurve const& peakCurve = *itr.second;
            if (peakCurve.TargetMz > MS1PeakCluster->NeutralMass())
                continue;

            //RT range of the peak curve
            float peakcurvertrange = peakCurve.EndRT() - peakCurve.StartRT();

            //Overlap ratio
            float OverlapP = 0;
            if (targetMS1Curve.StartRT() >= peakCurve.StartRT() && targetMS1Curve.StartRT() <= peakCurve.EndRT() && targetMS1Curve.EndRT() >= peakCurve.EndRT()) {
                OverlapP = (peakCurve.EndRT() - targetMS1Curve.StartRT()) / ms1rtrange;
            }
            else if (targetMS1Curve.EndRT() >= peakCurve.StartRT() && targetMS1Curve.EndRT() <= peakCurve.EndRT() && targetMS1Curve.StartRT() <= peakCurve.StartRT()) {
                OverlapP = (targetMS1Curve.EndRT() - peakCurve.StartRT()) / ms1rtrange;
            }
            else if (targetMS1Curve.StartRT() <= peakCurve.StartRT() && targetMS1Curve.EndRT() >= peakCurve.EndRT()) {
                OverlapP = peakcurvertrange / ms1rtrange;
            }
            else if (targetMS1Curve.StartRT() >= peakCurve.StartRT() && targetMS1Curve.EndRT() <= peakCurve.EndRT()) {
                OverlapP = 1;
            }

            if (OverlapP > parameter.RTOverlap
                && targetMS1Curve.ApexRT >= peakCurve.StartRT()
                && targetMS1Curve.ApexRT <= peakCurve.EndRT()
                && peakCurve.ApexRT >= targetMS1Curve.StartRT()
                && peakCurve.ApexRT <= targetMS1Curve.EndRT())
            {
                float ApexDiff = std::abs(targetMS1Curve.ApexRT - peakCurve.ApexRT);
                //Calculate pearson correlation
                float corr = PeakCurveCorrCalc::CalPeakCorr(targetMS1Curve, peakCurve, parameter.NoPeakPerMin);

                //If the pearson correlation larger than the defined threshold 
                if (!std::isnan(corr) && corr > parameter.CorrThreshold)
                {
                    PrecursorFragmentPairEdge PrecursorFragmentPair;
                    PrecursorFragmentPair.Correlation = corr;
                    PrecursorFragmentPair.PeakCurveIndexA = MS1PeakCluster->Index;
                    //float intensity = peakCurve.GetMaxIntensityByRegionRange(targetMS1Curve.StartRT(), targetMS1Curve.EndRT());
                    PrecursorFragmentPair.PeakCurveIndexB = peakCurve.Index;
                    PrecursorFragmentPair.FragmentMz = peakCurve.TargetMz;
                    PrecursorFragmentPair.Intensity = peakCurve.ApexInt;
                    PrecursorFragmentPair.RTOverlapP = OverlapP;
                    PrecursorFragmentPair.DeltaApex = ApexDiff;
                    //FragmentPeaks.put(peakCurve.Index, peakCurve);
                    GroupedFragmentList.push_back(PrecursorFragmentPair);
                    if (PrecursorFragmentPair.Correlation > parameter.HighCorrThreshold)
                        highCorrCnt++;
                }
            }
        }
        if (highCorrCnt < parameter.MinHighCorrCnt)
            GroupedFragmentList.clear();
    }
};



enum QualityLevel
{
    Q1_IsotopeComplete,
    Q2_Ms1Group,
    Q3_UnfragmentedPrecursor
};


/**
 * Preprocessing to generate pseudo MS/MS spectra
 * @author Chih-Chiang Tsou <chihchiang.tsou@gmail.com>
 */
class PseudoMSMSProcessing
{
    InstrumentParameter const& parameter;
    vector<PrecursorFragmentPairEdge> fragments;
    float growth = 1;

    void SortFragmentByMZ()
    {
        sort(fragments.begin(), fragments.end(), [](auto const& lhs, auto const& rhs)
        {
            return lhs.FragmentMz < rhs.FragmentMz;
        });
    }

    public:

    PeakClusterPtr PrecursorclusterPtr;
    PeakCluster& Precursorcluster;
    QualityLevel qualityLevel;


    PseudoMSMSProcessing(PeakClusterPtr const& ms1cluster, const vector<PrecursorFragmentPairEdge>& groupedFragmentPeaks, InstrumentParameter const& parameter, QualityLevel qualityLevel)
        : parameter(parameter), fragments(groupedFragmentPeaks), PrecursorclusterPtr(ms1cluster), Precursorcluster(*PrecursorclusterPtr), qualityLevel(qualityLevel)
    {
    }

    void DeisotopingForPeakClusterFragment()
    {
        vector<PrecursorFragmentPairEdge> newfragments;
        vector<bool> fragmentmarked(fragments.size(), true);
        PrecursorFragmentPairEdge* currentmaxfragment = &fragments.at(0);
        int currentmaxindex = 0;
        for (size_t i = 1; i < fragments.size(); i++)
        {
            if (InstrumentParameter::CalcPPM(fragments.at(i).FragmentMz, currentmaxfragment->FragmentMz) > parameter.MS2PPM) {
                fragmentmarked[currentmaxindex] = false;
                currentmaxindex = i;
                currentmaxfragment = &fragments.at(i);
            }
            else if (fragments.at(i).Intensity > currentmaxfragment->Intensity) {
                currentmaxindex = i;
                currentmaxfragment = &fragments.at(i);
            }
        }

        fragmentmarked[currentmaxindex] = false;
        for (size_t i = 0; i < fragments.size(); i++)
        {
            if (fragmentmarked[i])
                continue;
            
            fragmentmarked[i] = true;
            PrecursorFragmentPairEdge& startfrag = fragments.at(i);

            bool groupped = false;
            for (int charge = 2; charge >= 1; charge--)
            {
                float lastint = startfrag.Intensity;
                bool found = false;
                for (int pkidx = 1; pkidx < 5; pkidx++)
                {
                    float targetmz = startfrag.FragmentMz + (float)pkidx / charge;
                    for (size_t j = i + 1; j < fragments.size(); j++)
                    {
                        if (fragmentmarked[j])
                            continue;

                        PrecursorFragmentPairEdge& targetfrag = fragments.at(j);
                        if (InstrumentParameter::CalcPPM(targetfrag.FragmentMz, targetmz) < parameter.MS2PPM * (pkidx * 0.5 + 1))
                        {
                            if (targetfrag.Intensity < lastint) {
                                fragmentmarked[j] = true;
                                lastint = targetfrag.Intensity;
                                found = true;
                                break;
                            }
                        }
                        else if (targetfrag.FragmentMz > targetmz) {
                            break;
                        }
                    }
                    if (!found) {
                        break;
                    }
                }
                if (found) {
                    groupped = true;
                    //convert to charge 1 m/z
                    startfrag.FragmentMz = startfrag.FragmentMz * charge - (charge - 1) * (float)pwiz::chemistry::Proton;
                    if (startfrag.FragmentMz <= Precursorcluster.NeutralMass()) {
                        newfragments.emplace_back(startfrag);
                    }
                }
            }
            if (!groupped) {
                newfragments.emplace_back(startfrag);
            }
        }

        swap(fragments, newfragments);
        SortFragmentByMZ();
    }

    void RemoveMatchedFrag(map<int, vector<PrecursorFragmentPairEdge>>& MatchedFragmentMap)
    {
        vector<PrecursorFragmentPairEdge> newlist;
        for (PrecursorFragmentPairEdge const& fragmentClusterUnit : fragments)
        {
            if (MatchedFragmentMap.count(fragmentClusterUnit.PeakCurveIndexB) == 0)
                newlist.emplace_back(fragmentClusterUnit);
        }
        fragments = newlist;
    }

    void BoostComplementaryIon()
    {
        float totalmass = (float)(Precursorcluster.TargetMz() * Precursorcluster.Charge - Precursorcluster.Charge * pwiz::chemistry::Proton);
        vector<bool> fragmentmarked(fragments.size(), false);
        for (size_t i = 0; i < fragments.size(); i++)
        {
            PrecursorFragmentPairEdge const& fragmentClusterUnit = fragments.at(i);
            if (fragmentmarked[i])
                continue;

            fragmentmarked[i] = true;
            vector<PrecursorFragmentPairEdge> GroupedFragments;
            GroupedFragments.emplace_back(fragmentClusterUnit);
            float complefrag1 = (float)(totalmass - fragmentClusterUnit.FragmentMz + 2 * pwiz::chemistry::Proton);

            if (complefrag1 >= fragmentClusterUnit.FragmentMz)
            {
                for (size_t j = i + 1; j < fragments.size(); j++)
                {
                    if (fragmentmarked[j])
                        continue;

                    PrecursorFragmentPairEdge const& fragmentClusterUnit2 = fragments.at(j);
                    if (InstrumentParameter::CalcPPM(complefrag1, fragmentClusterUnit2.FragmentMz) < parameter.MS2PPM) {
                        GroupedFragments.emplace_back(fragmentClusterUnit2);
                        fragmentmarked[j] = true;
                    }
                    else if (fragmentClusterUnit2.FragmentMz > complefrag1) {
                        break;
                    }
                }

            }

            if (GroupedFragments.size() < 2)
                continue;

            const PrecursorFragmentPairEdge* bestfragment = &GroupedFragments.at(0);
            for (PrecursorFragmentPairEdge const& fragment : GroupedFragments)
            {
                if (fragment.Intensity > bestfragment->Intensity)
                    bestfragment = &fragment;
            }

            for (PrecursorFragmentPairEdge& fragment : GroupedFragments)
            {
                fragment.ComplementaryFragment = true;
                fragment.Intensity = bestfragment->Intensity * growth;
                fragment.Correlation = bestfragment->Correlation;
                fragment.DeltaApex = bestfragment->DeltaApex;
                fragment.RTOverlapP = bestfragment->RTOverlapP;
                fragment.FragmentMS1Rank = bestfragment->FragmentMS1Rank;
                fragment.FragmentMS1RankScore = bestfragment->FragmentMS1RankScore;
            }
        }
    }

    void IdentifyComplementaryIon(float totalmass)
    {
        vector<bool> fragmentmarked(fragments.size(), false);
        for (size_t i = 0; i < fragments.size(); i++)
        {
            PrecursorFragmentPairEdge const& fragmentClusterUnit = fragments.at(i);
            if (fragmentmarked[i])
                continue;

            fragmentmarked[i] = true;
            vector<PrecursorFragmentPairEdge> GroupedFragments;
            GroupedFragments.emplace_back(fragmentClusterUnit);
            float complefrag1 = (float)(totalmass - fragmentClusterUnit.FragmentMz + 2 * pwiz::chemistry::Proton);

            if (complefrag1 >= fragmentClusterUnit.FragmentMz)
            {
                for (int j = i + 1; j < (int) fragments.size(); j++)
                {
                    if (fragmentmarked[j])
                        continue;

                    PrecursorFragmentPairEdge const& fragmentClusterUnit2 = fragments.at(j);
                    if (InstrumentParameter::CalcPPM(complefrag1, fragmentClusterUnit2.FragmentMz) < parameter.MS2PPM) {
                        GroupedFragments.emplace_back(fragmentClusterUnit2);
                        fragmentmarked[j] = true;
                    }
                    else if (fragmentClusterUnit2.FragmentMz > complefrag1) {
                        break;
                    }
                }
            }

            for (PrecursorFragmentPairEdge& fragment : GroupedFragments)
                fragment.ComplementaryFragment = true;
        }
    }

    void GetScan(pwiz::util::BinaryData<double>& mzArray, pwiz::util::BinaryData<double>& intensityArray) const
    {
        XYPointCollection Scan;
        for (PrecursorFragmentPairEdge const& fragmentClusterUnit : fragments)
        {
            if (parameter.AdjustFragIntensity) {
                Scan.AddPointKeepMaxIfCloseValueExisted(fragmentClusterUnit.FragmentMz, fragmentClusterUnit.Intensity * fragmentClusterUnit.Correlation * fragmentClusterUnit.Correlation, parameter.MS2PPM);
            }
            else {
                Scan.AddPointKeepMaxIfCloseValueExisted(fragmentClusterUnit.FragmentMz, fragmentClusterUnit.Intensity, parameter.MS2PPM);
            }
        }

        mzArray.resize(Scan.Data.size());
        intensityArray.resize(Scan.Data.size());
        for (size_t i = 0; i < Scan.Data.size(); ++i)
        {
            mzArray[i] = Scan.Data[i].mz;
            intensityArray[i] = Scan.Data[i].intensity;
        }
    }

    void operator()()
    {
        if (fragments.size() < 2)
            return;

        SortFragmentByMZ();
        if (parameter.BoostComplementaryIon) {
            DeisotopingForPeakClusterFragment();
            BoostComplementaryIon();
        }
    }
};


} // namespace DiaUmpire

#pragma warning( pop )

#endif // _DIAUMPIRE_PEAKCLUSTER_
