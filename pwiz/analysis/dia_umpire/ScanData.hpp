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

#ifndef _DIAUMPIRE_SCANDATA_
#define _DIAUMPIRE_SCANDATA_

#include <vector>
#include <set>
#include <string>
#include <limits>
#include <algorithm>
#include <boost/smart_ptr.hpp>
#include "InstrumentParameter.hpp"
#include "pwiz/data/common/cv.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/misc/sort_together.hpp"


namespace DiaUmpire {

using std::map;
using std::vector;
using std::set;
using std::string;
using std::runtime_error;
using namespace pwiz::cv;

struct XYZData
{
    XYZData() : x(0), y(0), z(0) {}
    XYZData(float x, float y, float z) : x(x), y(y), z(z) {}

    union { float rt; float x; };
    union { float mz; float y; };
    union { float intensity; float z; };

    float getX() const { return x; }
    float getY() const { return y; }
    float getZ() const { return z; }
};

struct XYData
{
    XYData() : x(0), y(0) {}
    XYData(float x, float y) : x(x), y(y) {}

    union { float mz; float x; };
    union { float intensity; float y; };

    float getX() const { return x; }
    float getY() const { return y; }

    bool operator< (const XYData& rhs) { return x == rhs.x ? y < rhs.y : x < rhs.x; }
};

class XYPointCollection
{
    public:

    float MaxY = 0;
    std::vector<XYData> Data;

    XYPointCollection() {
    }

    int PointCount() { return Data.size(); }

    float GetSumX() {
        float sum = 0;
        for (XYData point : Data) {
            sum += point.getX();
        }
        return sum;
    }

    float GetSumY() {
        float sum = 0;
        for (XYData point : Data) {
            sum += point.getY();
        }
        return sum;
    }


    void AddPoint(float x, float y) {
        Data.emplace_back(x, y);
        if (MaxY < y) {
            MaxY = y;
        }
    }

    void AddPointKeepMaxIfCloseValueExisted(float x, float y, float ppm) {
        bool insert = true;
        if (Data.size() > 0) {
            int idx = GetClosetIndexOfX(x);
            XYData pt = Data.at(idx);
            if (InstrumentParameter::CalcPPM(pt.getX(), x) < ppm) {
                insert = false;
                if (y < pt.getY()) {
                    pt.y = y;
                    pt.x = x;
                }
                if (MaxY < y) {
                    MaxY = y;
                }
            }
        }
        if (insert) {
            AddPoint(x, y);
        }
    }

    void AddPointKeepMaxIfValueExisted(float x, float y) {
        bool insert = true;
        if (Data.size() > 0) {
            int idx = GetClosetIndexOfX(x);
            XYData pt = Data.at(idx);
            if (pt.getX() == x) {
                insert = false;
                if (y < pt.getY()) {
                    pt.y = y;
                }
                if (MaxY < y) {
                    MaxY = y;
                }
            }
        }
        if (insert) {
            AddPoint(x, y);
        }
    }

    void AddPoint(XYData point) {
        Data.emplace_back(point);
        if (MaxY < point.getY()) {
            MaxY = point.getY();
        }
    }

    int PointCount() const
    {
        return Data.size();
    }

    void CentroidingbyLocalMaximum(int Resolution, float MinMZ)
    {
        if (Data.size() == 0) {
            return;
        }
        int oldcount = Data.size();
        XYPointCollection DataTemp = *this;
        int startindex = DataTemp.BinarySearchHigher(MinMZ);
        XYData& pt = DataTemp.Data.at(startindex);
        float maxintensity = pt.getY();
        float maxmz = pt.getX();
        float gap = pt.getX() / Resolution;
        Data.clear();
        for (int i = startindex + 1; i < oldcount; i++) {
            XYData& pti = DataTemp.Data.at(i);
            if (pti.getX() - maxmz < gap) {
                if (pti.getY() > maxintensity) {
                    maxintensity = pti.getY();
                    maxmz = pti.getX();
                    gap = pti.getX() / Resolution;
                }
            }
            else {
                AddPoint(maxmz, maxintensity);
                maxintensity = pti.getY();
                maxmz = pti.getX();
                gap = pti.getX() / Resolution;
            }
        }
    }

    int GetLowerIndexOfX(float x) const {
        return BinarySearchLower(x);
    }

    int GetHigherIndexOfX(float x) const {
        return BinarySearchHigher(x);
    }

    int GetClosetIndexOfX(float x) const {
        return BinarySearchClosest(x);
    }

    XYData GetPoinByXLower(float x) const {
        return Data.at(GetLowerIndexOfX(x));
    }

    XYData GetPoinByXCloset(float x) const {
        return Data.at(GetClosetIndexOfX(x));
    }

    XYData GetPoinByXHigher(float x) const {
        return Data.at(GetHigherIndexOfX(x));

    }

    XYPointCollection GetSubSetByXRange(float xlower, float xupper) const {
        if (PointCount() == 0) {
            return XYPointCollection();
        }

        XYPointCollection NewXYCollection;
        int start = GetLowerIndexOfX(xlower);

        if (start < 0) {
            start = 0;
        }

        for (int i = start; i < PointCount(); i++) {
            float x = Data.at(i).getX();
            if (x >= xlower && x <= xupper) {
                NewXYCollection.AddPoint(x, Data.at(i).getY());
            }
            else if (x > xupper) {
                break;
            }
        }
        return NewXYCollection;
    }

    XYData& get(int index)
    {
        return Data.at(index);
    }

    const XYData& get(int index) const
    {
        return Data.at(index);
    }

    int size() const
    {
        return Data.size();
    }

    int BinarySearchLower(const XYData& value) const
    {
        return BinarySearchLower(value.getX());
    }

    int BinarySearchHigher(float value) const
    {
        if (Data.empty()) {
            return 0;
        }
        int lower = 0;
        int upper = size() - 1;

        if (value - get(upper).getX() >= 0) {
            return upper;
        }
        if (value - get(0).getX() <= 0) {
            return 0;
        }

        while (lower <= upper) {
            int middle = (lower + upper) / 2;
            float comparisonResult = value - get(middle).getX();
            if (comparisonResult == 0) {
                while (middle - 1 >= 0 && get(middle - 1).getX() == value) {
                    middle--;
                }
                return middle;
            }
            else if (comparisonResult < 0) {
                upper = middle - 1;
            }
            else {
                lower = middle + 1;
            }
        }
        if (lower > size() - 1) {
            return size() - 1;
        }
        while (lower < size() - 1 && get(lower).getX() <= value) {
            lower++;
        }
        return lower;
    }

    int BinarySearchLower(float value) const
    {
        if (Data.empty()) {
            return 0;
        }
        int lower = 0;
        int upper = size() - 1;

        if (value - get(upper).getX() >= 0) {
            return upper;
        }
        if (value - get(0).getX() <= 0) {
            return 0;
        }

        while (lower <= upper) {
            int middle = (lower + upper) / 2;
            float comparisonResult = value - get(middle).getX();
            if (comparisonResult == 0) {
                while (middle - 1 >= 0 && get(middle - 1).getX() == value) {
                    middle--;
                }
                return middle;
            }
            else if (comparisonResult < 0) {
                upper = middle - 1;
            }
            else {
                lower = middle + 1;
            }
        }
        if (upper < 0) {
            return 0;
        }
        while (upper > 0 && get(upper).getX() >= value) {
            upper--;
        }
        return upper;
    }

    int BinarySearchClosest(float value) const
    {
        if (Data.empty()) {
            return 0;
        }
        int lower = 0;
        int upper = size() - 1;

        if (value - get(upper).getX() >= 0) {
            return upper;
        }
        if (value - get(0).getX() <= 0) {
            return 0;
        }

        while (lower <= upper) {
            int middle = (lower + upper) / 2;
            float comparisonResult = value - get(middle).getX();
            if (comparisonResult == 0) {
                return middle;
            }
            else if (comparisonResult < 0) {
                upper = middle - 1;
            }
            else {
                lower = middle + 1;
            }
        }

        if (std::abs(value - get(lower).getX()) > std::abs(value - get(upper).getX())) {
            return upper;
        }
        else {
            return lower;
        }
    }

};


class ScanData : public XYPointCollection
{
    mutable float _totIonCurrent = 0;

    public:

    int ScanNum;
    int MsLevel;
    float RetentionTime;
    float StartMz;
    float EndMz;
    float BasePeakMz;
    float BasePeakIntensity;
    float PrecursorMz;
    int PrecursorCharge;
    std::string ActivationMethod;
    float PrecursorIntensity;
    std::string Scantype;
    int precision;
    std::string compressionType;
    bool centroided;
    int precursorScanNum;
    int PeaksCountstring;
    float background = 0;
    std::string MGFTitle;
    boost::scoped_ptr<ScanData> TopPeakScan;
    float windowWideness;
    std::string scanType;
    float isolationWindowTargetMz;
    float isolationWindowLoffset;
    float isolationWindowRoffset;


    void Centroiding(int Resolution, float MinMZ) {
        CentroidingbyLocalMaximum(Resolution, MinMZ);
        centroided = true;
    }

    float PrecursorMass() {
        return PrecursorCharge * (PrecursorMz - pwiz::chemistry::Proton);
    }

    XYData* GetHighestPeakInMzWindow(float targetmz, float PPM)
    {
        float lowmz = InstrumentParameter::GetMzByPPM(targetmz, 1, PPM);
        int startidx = GetLowerIndexOfX(lowmz);
        XYData* closetPeak = nullptr;
        for (size_t idx = (size_t) startidx; idx < Data.size(); idx++) {
            XYData& peak = Data.at(idx);
            if (InstrumentParameter::CalcPPM(targetmz, peak.getX()) <= PPM) {
                if (closetPeak == nullptr || peak.getY() > closetPeak->getY()) {
                    closetPeak = &peak;
                }
            }
            else if (peak.getX() > targetmz) {
                break;
            }
        }
        return closetPeak;
    }

    void GenerateTopPeakScanData(int toppeaks)
    {
        std::sort(Data.begin(), Data.end(), [](const XYData& a, const XYData& b) -> bool { return a.y > b.y; });

        TopPeakScan.reset(new ScanData);
        for (size_t i = 0; TopPeakScan->PointCount() < toppeaks && i < Data.size(); ++i)
        {
            XYData peak = Data.at(i);
            TopPeakScan->AddPoint(peak.getY(), peak.getX());
        }

        std::sort(Data.begin(), Data.end(), [](const XYData& a, const XYData& b) -> bool { return a.x < b.x; });
    }

    void Normalization()
    {
        if (MaxY != 0) {
            for (int i = 0; i < PointCount(); i++) {
                XYData pt = Data.at(i);
                pt.y = (pt.getY() / MaxY);
            }
        }
    }

    void RemoveSignalBelowBG()
    {
        for (int i = Data.size() - 1; i >= 0; --i)
            if (Data.at(i).getY() <= background)
                Data.erase(Data.begin() + i);
        Data.shrink_to_fit();
    }

    float TotIonCurrent() const
    {
        if (_totIonCurrent == 0) {
            for (int i = 0; i < PointCount(); i++) {
                _totIonCurrent += Data.at(i).getY();
            }
        }
        return _totIonCurrent;
    }

    void SetTotIonCurrent(float totioncurrent)
    {
        _totIonCurrent = totioncurrent;
    }

    float GetTopNIntensity(int N)
    {
        std::sort(Data.begin(), Data.end(), [](const XYData& a, const XYData& b) -> bool { return a.y > b.y; });
        float topN = Data.size() > 10 ? Data.at(N).y : -1.0;
        std::sort(Data.begin(), Data.end(), [](const XYData& a, const XYData& b) -> bool { return a.x < b.x; });
        return topN;
    }

    void Preprocessing(const InstrumentParameter& parameter)
    {
        if (!centroided) {
            Centroiding(parameter.Resolution, parameter.MinMZ);
        }
        if (parameter.EstimateBG) {
            //detector.DetermineConstantBackground();
            AdjacentPeakHistogram();
        }
        else {
            if (MsLevel == 1) {
                background = parameter.MinMSIntensity;
            }
            if (MsLevel == 2) {
                background = parameter.MinMSMSIntensity;
            }
        }

        if (parameter.Denoise) {
            RemoveSignalBelowBG();
        }
        if (parameter.Deisotoping && MsLevel == 1) {
            //ScanPeakGroup scanpeak = new ScanPeakGroup(this, parameter);
            //scanpeak.Deisotoping();
        }
    }

    void AdjacentPeakHistogram()
    {
        if (PointCount() < 10)
            return;

        const float Ratio = 2;

        vector<float> IntList(Data.size());
        for (size_t i = 0; i < Data.size(); i++) {
            IntList[i] = (float) Data[i].getY();
        }        
        sort(IntList.begin(), IntList.end());

        float upper = IntList.at((int) (IntList.size() * 0.7f));
        float lower = IntList.at(0);

        if(upper<=lower+0.001){
            return;
        }
        int count1 = 0;
        int count2 = 0;
        int count3 = 0;
        int count4 = 0;
        int noise = 0;

        float bk = 0;
        float interval = (upper - lower) / 20;
        
        for (bk = lower; bk < upper; bk += interval)
        {
            count1 = 0;
            count2 = 0;
            count3 = 0;
            count4 = 0;
            noise = 0;
            int preidx = -1;
            for (size_t i = 1; i < Data.size(); i++) {
                if (Data.at(i).getY() > bk) {
                    if (preidx != -1) {
                        float dist = Data.at(i).getX() - Data.at(preidx).getX();
                        //writer.write(dist + "\t");
                        if (dist > 0.95 && dist < 1.05 && Data.at(preidx).getY() > Data.at(i).getY()) {
                            count1++;
                        } else if (dist > 0.45 && dist < 0.55 && Data.at(preidx).getY() > Data.at(i).getY()) {
                            count2++;
                        } else if (dist > 0.3 && dist < 0.36 && Data.at(preidx).getY() > Data.at(i).getY()) {
                            count3++;
                        } else if (dist > 0.24 && dist < 0.26 && Data.at(preidx).getY() > Data.at(i).getY()) {
                            count4++;
                        } else if (dist < 0.23f) {
                            noise++;
                        }                        
                    }
                    preidx=i;
                }                
            }
            if (noise < (count1 + count2 + count3 + count4) * Ratio)
                break;
        }
        if (bk > 0)
        {
            background = bk;
            RemoveSignalBelowBG();
        }
    }
};


class ScanCollection
{
    int NumScan;
    int NumScanLevel1;
    int NumScanLevel2;
    int StartScan = 1000000;
    int EndScan = 0;
    int Resolution;
    int NumPeaks; // across all scans
    float MinPrecursorInt = std::numeric_limits<float>::max();
    mutable XYPointCollection _tic;
    //mutable XYPointCollection _basepeak;
    std::vector<size_t> ms1ScanIndex;
    std::vector<size_t> ms2ScanIndex;

    public:

    map<int, ScanData> ScanHashMap;
    map<float, int> ElutionTimeToScanNoMap;

    ScanCollection(int Resolution = 0)
    {
        this->Resolution = Resolution;
        clear();
    }

    void clear()
    {
        ms1ScanIndex.clear();
        ms2ScanIndex.clear();
        ScanHashMap.clear();
        ElutionTimeToScanNoMap.clear();
        NumScan = 0;
        NumScanLevel1 = 0;
        NumScanLevel2 = 0;
        NumPeaks = 0;
    }

    void sortIndices()
    {
        sort(ms1ScanIndex.begin(), ms1ScanIndex.end());
        sort(ms2ScanIndex.begin(), ms2ScanIndex.end());
    }

    int size() const { return ScanHashMap.size(); }

    int GetNumPeaks() const { return NumPeaks; }

    const std::vector<size_t>& GetScanNoArray(int mslevel) const
    {
        if (mslevel == 1) {
            return ms1ScanIndex;
        }
        if (mslevel == 2) {
            return ms2ScanIndex;
        }
        throw runtime_error("unsupported ms level");
    }

    std::vector<size_t> GetMS2DescendingArray() {
        return ms2ScanIndex;
    }

    ScanData& AddScan(const pwiz::msdata::SpectrumPtr& spectrum)
    {
        if (ScanHashMap.count(spectrum->index))
            return ScanHashMap[spectrum->index];

        ScanData& scan = ScanHashMap[spectrum->index];

        scan.ScanNum = spectrum->index;
        scan.MGFTitle = spectrum->id;
        scan.MsLevel = spectrum->cvParamValueOrDefault<int>(MS_ms_level, 0);
        scan.RetentionTime = spectrum->scanList.scans.at(0).cvParam(MS_scan_start_time).timeInSeconds() / 60;
        scan.centroided = spectrum->hasCVParam(MS_centroid_spectrum);

        // local array copies
        vector<double> mzArray(spectrum->getMZArray()->data);
        vector<double> intensityArray(spectrum->getIntensityArray()->data);

        pwiz::util::sort_together(mzArray, intensityArray); // ensure data is m/z sorted

        for (size_t i = 0; i < mzArray.size(); ++i)
            scan.AddPoint(mzArray[i], intensityArray[i]);
        NumPeaks += mzArray.size();

        if (scan.MsLevel == 1) {
            NumScanLevel1++;
            ms1ScanIndex.push_back(scan.ScanNum);
        }

        if (scan.MsLevel == 2) {
            NumScanLevel2++;
            ms2ScanIndex.push_back(scan.ScanNum);

            const auto& p = spectrum->precursors.at(0);
            scan.isolationWindowTargetMz = p.isolationWindow.cvParamValueOrDefault(MS_isolation_window_target_m_z, 0.0);
            scan.isolationWindowLoffset = p.isolationWindow.cvParamValueOrDefault(MS_isolation_window_lower_offset, 0.0);
            scan.isolationWindowRoffset = p.isolationWindow.cvParamValueOrDefault(MS_isolation_window_upper_offset, 0.0);

            const auto& si = p.selectedIons.at(0);
            scan.PrecursorMz = si.cvParamValueOrDefault(MS_selected_ion_m_z, 0.0);
            scan.PrecursorCharge = si.cvParamValueOrDefault(MS_charge_state, 0);
            scan.PrecursorIntensity = si.cvParamValueOrDefault(MS_peak_intensity, 0.0);

            if (scan.PrecursorIntensity > 0)
                MinPrecursorInt = std::min(MinPrecursorInt, scan.PrecursorIntensity);

            scan.ActivationMethod = p.activation.cvParamChildValueOrDefault(MS_dissociation_method, string("CID"));
        }
        NumScan++;

        if (scan.ScanNum >= EndScan) {
            EndScan = scan.ScanNum;
        }
        if (scan.ScanNum <= StartScan) {
            StartScan = scan.ScanNum;
        }

        return scan;
    }

    const ScanData* GetParentMSScan(int ScanNo) const
    {
        auto scanItr = ScanHashMap.find(ScanNo);
        if (scanItr == ScanHashMap.end())
            return nullptr;

        while (scanItr != ScanHashMap.begin())
        {
            --scanItr;
            if (scanItr->second.MsLevel == 1)
                break;
        }
        return &scanItr->second;
    }

    const ScanData* GetScan(int ScanNO) const
    {
        if (!ScanAdded(ScanNO))
            return nullptr;
        return &ScanHashMap.find(ScanNO)->second;
    }

    bool ScanAdded(int ScanNo) const
    {
        return ScanHashMap.count(ScanNo) > 0;
    }

    void CentoridingAllScans(int Resolution, float MiniIntF)
    {
        for (auto& indexDataPair : ScanHashMap)
            indexDataPair.second.Centroiding(Resolution, MiniIntF);
    }

    int GetScanNoByRT(float RT) const
    {
        auto findItr = ElutionTimeToScanNoMap.lower_bound(RT);
        if (findItr == ElutionTimeToScanNoMap.end())
            return ElutionTimeToScanNoMap.rbegin()->second;
        return findItr->second;
    }

    /*ScanCollection GetSubCollectionByElutionTimeAndMZ(float startTime, float endTime, float startmz, float endmz, int msLevel, bool IsAddCalibrationScan)
    {
        ScanCollection scanCollection = new ScanCollection(Resolution);
        scanCollection.ElutionTimeToScanNoMap = ElutionTimeToScanNoMap;

        if (endTime == -1) {
            endTime = 9999999f;
        }

        //Find the start scan num and end scan num
        int StartScanNo = 0;
        int EndScanNo = 0;

        StartScanNo = GetScanNoByRT(startTime);
        EndScanNo = GetScanNoByRT(endTime);

        NavigableMap<Integer, ScanData> SubScaNavigableMap = ScanHashMap.subMap(StartScanNo, true, EndScanNo, true);
        for (ScanData scan : SubScaNavigableMap.values()) {
            if (endmz == -1) {
                if (((msLevel == 0 || scan.MsLevel == msLevel) && (IsAddCalibrationScan == true || scan.Scantype != "calibration")) && scan.PointCount() > 0 && scan.TotIonCurrent() > 0) {
                    scanCollection.AddScan(scan);
                }
            }
            else //filter mz
            {
                if (((msLevel == 0 || scan.MsLevel == msLevel) && (IsAddCalibrationScan == true || scan.Scantype != "calibration")) && scan.PointCount() > 0 && scan.TotIonCurrent() > 0) {
                    scanCollection.AddScan(scan.GetNewSubScanBymzRange(startmz, endmz));
                }
            }
        }
        return scanCollection;
    }*/

    const XYPointCollection& GetTIC() const
    {
        if (!_tic.Data.empty())
            return _tic;

        for (const auto& indexDataPair : ScanHashMap)
            _tic.AddPoint(indexDataPair.second.RetentionTime, indexDataPair.second.TotIonCurrent());
        return _tic;
    }

    /*const XYPointCollection& GetBasePeak()
    {
        if (!_basepeak.Data.empty())
            return _basepeak;

        for (auto& indexDataPair : ScanHashMap)
            for (ScanData scan : ScanHashMap.values()) {
                float TIC = 0;
                for (int i = 0; i < scan.PointCount(); i++) {
                    float intensity = scan.Data.get(i).getY();
                    if (intensity > TIC) {
                        TIC = intensity;
                    }
                }
                _tic.AddPoint(scan.RetentionTime, TIC);
            }
        }
        return _basepeak;
    }*/

    /*XYPointCollection GetXIC(float startMZ, float endMZ) {
        XYPointCollection xic = new XYPointCollection();
        for (ScanData scan : ScanHashMap.values()) {
            float intensity = 0f;
            XYPointCollection submz = scan.GetSubSetByXRange(startMZ, endMZ);
            for (int i = 0; i < submz.PointCount(); i++) {
                intensity += submz.Data.get(i).getY();
            }
            xic.AddPoint(scan.RetentionTime, intensity);
        }
        return xic;
    }*/

    //Remove peaks whose the intensity low than the threshold
    void RemoveBackground(int mslevel, float background)
    {
        for (auto& indexDataPair : ScanHashMap)
        {
            auto& scan = indexDataPair.second;
            if (scan.MsLevel == mslevel)
            {
                scan.background = background;
                scan.RemoveSignalBelowBG();
            }
        }
    }
};

typedef std::unique_ptr<ScanCollection> ScanCollectionPtr;


} // namespace DiaUmpire

#endif // _DIAUMPIRE_SCANDATA_
