//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#include "Pseudo2DGel.hpp"
#include "pwiz/utility/misc/Image.hpp"
#include "pwiz/analysis/peptideid/PeptideID_pepXML.hpp"
#include "pwiz/analysis/peptideid/PeptideID_flat.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>
#include <algorithm>


namespace pwiz {
namespace analysis {


namespace bfs = boost::filesystem;
using namespace pwiz::util;
using namespace pwiz::peptideid;
using std::max;
using std::min;

//
// Pseudo2DGel::Config
//


PWIZ_API_DECL Pseudo2DGel::Config::Config()
:   mzLow(200), mzHigh(2000), timeScale(1.0), binCount(640),
    zRadius(2), bry(false), grey(false), binSum(false), ms2(false)
    
{
}

PWIZ_API_DECL Pseudo2DGel::Config::Config(const string& args)
:   mzLow(200), mzHigh(2000), timeScale(1.0), binCount(640),
    zRadius(2), bry(false), grey(false), binSum(false), ms2(false)
{
    process(args);
}

void Pseudo2DGel::Config::process(const std::string& args)
{
    vector<string> tokens;
    istringstream iss(args);
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

    static int count = 0;
    label = lexical_cast<string>(count++);

    for (vector<string>::iterator it=tokens.begin(); it!=tokens.end(); ++it)
    {
        if (it->find("label=") == 0)
            label = it->substr(6);
        else if (it->find("mzLow=") == 0)
            mzLow = (float)atof(it->c_str()+6);
        else if (it->find("mzHigh=") == 0)
            mzHigh = (float)atof(it->c_str()+7);
        else if (it->find("timeScale=") == 0)
            timeScale = (float)atof(it->c_str()+10);
        else if (it->find("binCount=") == 0)
            binCount = atoi(it->c_str()+9);
        else if (it->find("zRadius=") == 0)
            zRadius = (float)atof(it->c_str()+8);
        else if (*it == "scan")
            binScan = true;
        else if (*it == "time")
            binScan = false;
        else if (*it == "bry")
            bry = true; 
        else if (*it == "grey")
            grey = true; 
        else if (*it == "gray")
            grey = true; 
        else if (*it == "binSum")
            binSum = true; 
        else if (*it == "ms2locs")
        {
            ms2 = true;
        }
        else if (*it == "shape")
            markupShape = (it->substr(6) == "square" ? square : circle);
        else if (it->find("pepxml=") == 0)
            peptide_id = shared_ptr<PeptideID>(new PeptideID_pepXml(it->c_str()+7));
        else if (it->find("msi=") == 0)
            peptide_id = shared_ptr<PeptideID>(
                new PeptideID_flat(it->c_str()+4,
                                   shared_ptr<FlatRecordBuilder>(new MSInspectRecordBuilder())));
        else if (it->find("flat=") == 0)
            peptide_id = shared_ptr<PeptideID>(
                new PeptideID_flat(it->c_str()+5,
                                   shared_ptr<FlatRecordBuilder>(new FlatRecordBuilder())));
        else 
            cout << "[Pseudo2DGel::Config] Ignoring argument: " << *it << endl;
    }
}

//
// Pseudo2DGel::Impl
//


namespace {

class IntensityFunction
{
    public:
    // map value -> [0,1]
    virtual float operator()(float value) const = 0;
    virtual float low() const = 0;
    virtual float high() const = 0;
    virtual ~IntensityFunction(){}
};

class ColorMap
{
    public:
    // map intensity in [0,1] -> (r,g,b) in [0,1]x[0,1]x[0,1] 
    virtual void operator()(float intensity, float& red, float& green, float& blue) const = 0;
    virtual ~ColorMap(){}
};


} // namespace 

namespace {


} // namespace

class Pseudo2DGel::Impl
{
    public:

    Impl(const MSDataCache& cache, const Config& config);

    void open(const DataInfo& dataInfo);

    void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum);

    void close(const DataInfo& dataInfo);

    private:

    const MSDataCache& cache_;
    Config config_;

    // main buffer for holding intensity data in bins
    vector< vector<float> > scanBuffer_;

    struct Child
    {
        size_t bin;
        std::string nativeID;

        Child(size_t bin, const string& nativeID)
            : bin(bin), nativeID(nativeID) {}
    };
    
    // map index -> vector of children (as list of bins)
    map<size_t, vector<shared_ptr<Child> > > children_;  

    typedef vector<size_t> ScanList; // by index
    struct ScanInfo {
        ScanList scans;
        vector<double> rts;
        float minTime, maxTime;

        vector<int> bin; // optional
        
        void clear(){
            scans.clear();
            rts.clear();
            bin.clear();
            minTime = maxTime = -1;
        }

        bool empty(){
            return scans.empty();
        }
    };

    ScanInfo itScans_;
    ScanInfo ftScans_;

    ScanInfo ms2Scans_;

    int idedPeptides_;
    
    MarkupShape markupShape_;

    // Returns the score for this ms2 scan, it's precursor. Throws
    // an exception if no such scores exists or if both have a value.
    double getScore(size_t cachedIndex);
    
    // This is used for linear time rendering
    vector< vector<float> > pixelBins_;

    int bin(double mz); // converts mz -> bin index
    void clear();

    // color map
    void instantiateColorMap();
    auto_ptr<ColorMap> colorMap_;
    shared_ptr<ColorMap> circleColorMap_;
    Image::Color color(float intensity) const;
    Image::Color circleColor(float intensity) const;

    // data processing and image creation
    size_t countUniquePeptides(const ScanList& scans);
    Image::Color chooseMarkupColor(size_t ms2Index);
    void writeImages(const DataInfo& dataInfo);
    auto_ptr<IntensityFunction> createIntensityFunction(const ScanList& scans);
    void writeImage(const DataInfo& dataInfo, const string& label, ScanInfo& scans);

    void drawScans(Image& image, const ScanInfo& scansInfo,
                   const IntensityFunction& intensityFunction,
                   const Image::Point& begin, const Image::Point& end); 

    void drawTimes(Image& image, const ScanInfo& scansInfo,
                   const IntensityFunction& intensityFunction,
                   const Image::Point& begin, const Image::Point& end); 

    void drawMS2(Image& image, const ScanInfo& scans, 
                   const IntensityFunction& intensityFunction,
                   const Image::Point& begin, const Image::Point& end); 

    void drawLegend(Image& image, const IntensityFunction& intensityFunction,
                    const Image::Point& begin, const Image::Point& end); 

    void drawMS2Legend(Image& image, const ScanInfo& scansInfo,
                       const IntensityFunction& intensityFunction,
                       const Image::Point& begin, const Image::Point& end); 

    void drawTIC(Image& image, const ScanList& scans, 
                 const Image::Point& begin, const Image::Point& end); 

    void drawTimeTIC(Image& image, const ScanList& scans, 
                 const Image::Point& begin, const Image::Point& end); 

    void drawTMZ(Image& image, const ScanList& scans, 
                 const Image::Point& begin, const Image::Point& end);

    friend struct prob_comp;
};

struct prob_comp
{
public:
    Pseudo2DGel::Impl* impl;
    
    prob_comp(Pseudo2DGel::Impl* impl)
        : impl(impl)
    {}
    
    // Compares 
    bool operator()(const size_t& x, const size_t& y) const
    {
        bool result = x < y;

        try
        {
            string nativeID_x = id::value(impl->cache_[x].id, "scan");
            string nativeID_y = id::value(impl->cache_[y].id, "scan");
            
            if (impl->config_.peptide_id != NULL)
            {
                double x_score = impl->getScore(x);
                double y_score = impl->getScore(y);
                result = x_score < y_score;
            }
        }
        catch(range_error re)
        {
            // cerr << "caught range_error: "<<re.what()<<endl;;
        }
        catch(logic_error le)
        {
            cerr << "caught logic_error: "<<le.what()<<endl;
        }
        catch(...) {}

        return result;
    }
};    

Pseudo2DGel::Impl::Impl(const MSDataCache& cache, const Config& config)
:   cache_(cache), config_(config)
{
    instantiateColorMap();
}


void Pseudo2DGel::Impl::open(const DataInfo& dataInfo)
{
    idedPeptides_ = 0;
    clear();
    scanBuffer_.resize(cache_.size());
}


void Pseudo2DGel::Impl::update(const DataInfo& dataInfo, 
                               const Spectrum& spectrum)
{
    const SpectrumInfo& info = cache_[spectrum.index];

    // save peaks as a row in the scanBuffer

    scanBuffer_[info.index].resize(config_.binCount);

    for (vector<MZIntensityPair>::const_iterator it=info.data.begin(); it!=info.data.end(); ++it)
    {
        if (it->mz<config_.mzLow || it->mz>config_.mzHigh) continue;

        int x = bin(it->mz);
        float intensity = (float)it->intensity;
       
        if (config_.binSum)
        {
            // store sum of intensities in bin
            scanBuffer_[info.index][x] += intensity;
        }
        else
        {
            // default -- store max intensity in bin
            if (scanBuffer_[info.index][x] < intensity)
                scanBuffer_[info.index][x] = intensity;
        }
    }

    // special handling based on msLevel and instrument type
    
    static size_t lastParent = 0;

    if (info.msLevel == 1)
    {
        lastParent = info.index; // remember this scan in case there are children

        if (cvIsA(info.massAnalyzerType, MS_ion_trap)){
            itScans_.scans.push_back(info.index);
            itScans_.rts.push_back(info.retentionTime);
            itScans_.bin.push_back(bin(info.basePeakMZ));
            if (itScans_.minTime < 0)
                itScans_.minTime = info.retentionTime;
            
            if (itScans_.maxTime < info.retentionTime)
                itScans_.maxTime = info.retentionTime;
        }
        else if (info.massAnalyzerType == MS_FT_ICR ||
                 cvIsA(info.massAnalyzerType, MS_orbitrap)){
            ftScans_.scans.push_back(info.index);
            ftScans_.rts.push_back(info.retentionTime);
            ftScans_.bin.push_back(bin(info.basePeakMZ));

            if (ftScans_.minTime < 0)
                ftScans_.minTime = info.retentionTime;
            
            if (ftScans_.maxTime < info.retentionTime)
                ftScans_.maxTime = info.retentionTime;
        }
    }
    else if (info.msLevel == 2 && info.precursors.size() == 1)
    {
        // Save the ms2 scan index and retention times for possible
        // ms2 display.
        ms2Scans_.scans.push_back(info.index);
        ms2Scans_.rts.push_back(info.retentionTime);
        ms2Scans_.bin.push_back(bin(info.precursors[0].mz));
    }
}


void Pseudo2DGel::Impl::close(const DataInfo& dataInfo)
{
    writeImages(dataInfo);
}

double Pseudo2DGel::Impl::getScore(size_t cachedIndex)
{
    if (cachedIndex >= cache_.size())
        throw out_of_range("ms2 index out of bounds");

    // Set score to 
    double score = -1;

    if (cache_[cachedIndex].precursors.size() == 0)
    {
        ostringstream oss;
        oss << "No precursors found for ms2 w/ scan number "
            << id::value(cache_[cachedIndex].id, "scan");
        throw logic_error(oss.str());
    }

    // TODO: commented out until nativeID usage can be fixed -- dk
    const string nativeID = id::value(cache_[cachedIndex].id, "scan");
    double mz1, mz;
    mz1 = mz = cache_[cachedIndex].basePeakMZ;
    double retentionTime1, retentionTime;
    retentionTime1 = retentionTime = cache_[cachedIndex].retentionTime;

    PeptideID::Location locMs2(nativeID, mz,retentionTime);

    bool noRecord = true;
    try
    {
        if (config_.peptide_id.get())
        {
            PeptideID::Record rMs2 = config_.peptide_id->record(locMs2);
            score = rMs2.normalizedScore;
            noRecord = false;
        }
    }
    catch(...)
    {
        size_t idx = cache_[cachedIndex].precursors[0].index;

        string id_ms1 = id::value(cache_[idx].id, "scan");
        mz = cache_[idx].basePeakMZ;
        retentionTime = cache_[idx].retentionTime;
        
        PeptideID::Location locMs1(id_ms1, mz, retentionTime);

        if (config_.peptide_id.get())
        {
            PeptideID::Record rMs1 = config_.peptide_id->record(locMs1);
            score = rMs1.normalizedScore;
            noRecord = false;
        }
    }

    // DEBUG -- setting score to max for ms2locs
    if (noRecord)
        score = 1.0;

    return score;
}

int Pseudo2DGel::Impl::bin(double mz)
{
    const float& low = config_.mzLow;
    const float& high = config_.mzHigh;
    const int& binCount = config_.binCount;

    int result = (int)((mz-low)/(high-low) * binCount);
    if (result < 0) result = 0;
    if (result > binCount-1) result = binCount-1;
    return result;
}


void Pseudo2DGel::Impl::clear()
{
    scanBuffer_.clear();
    children_.clear();
    itScans_.clear();
    ftScans_.clear();
}

namespace {
template <typename T>
inline T positiveLogarithm(T value)
{
    return value>1 ? log(value) : 0;
}
} // namespace


namespace {
class IntensityFunctionLogStats : public IntensityFunction
{
    public:

    // this function is positiveLogarithm() followed by a map
    // [center-radius, center+radius] -> [0,1], 
    // clamping values that fall out of the domain.

    IntensityFunctionLogStats(float center, float radius)
    :   low_(center-radius),
        high_(center+radius)
    {}

    virtual float operator()(float value) const
    {
        float result = (positiveLogarithm(value)-low_)/(high_-low_);
        if (result > 1) result = 1;
        if (result < 0) result = 0;
        return result;
    }

    virtual float low() const {return low_;}
    virtual float high() const {return high_;}

    private:
    float low_;
    float high_;
};
} // namespace


namespace {
class ColorMapBRY : public ColorMap
{
    public:
    virtual void operator()(float intensity, float& red, float& green, float& blue) const
    {
        // 000000 -> 0000FF (increase B linearly)
        // 0000FF -> FF0000 (increase R and decrease B linearly)
        // FF0000 -> FFFF00 (increase G linearly)

        if (intensity < .25)
        {
            blue = intensity * 4;
        }
        else if (intensity < .75)
        {
            blue = 1 - (intensity-.25f)*2;
            red = (intensity-.25f)*2;
        }
        else
        {
            red = 1;
            green = (intensity-.75f)*4;
        }
    }
};
} // namespace


namespace {
float colorTable_[256][3] =
{
    {0.0000000e+00,   0.0000000e+00,   5.1562500e-01},
    {0.0000000e+00,   0.0000000e+00,   5.3125000e-01},
    {0.0000000e+00,   0.0000000e+00,   5.4687500e-01},
    {0.0000000e+00,   0.0000000e+00,   5.6250000e-01},
    {0.0000000e+00,   0.0000000e+00,   5.7812500e-01},
    {0.0000000e+00,   0.0000000e+00,   5.9375000e-01},
    {0.0000000e+00,   0.0000000e+00,   6.0937500e-01},
    {0.0000000e+00,   0.0000000e+00,   6.2500000e-01},
    {0.0000000e+00,   0.0000000e+00,   6.4062500e-01},
    {0.0000000e+00,   0.0000000e+00,   6.5625000e-01},
    {0.0000000e+00,   0.0000000e+00,   6.7187500e-01},
    {0.0000000e+00,   0.0000000e+00,   6.8750000e-01},
    {0.0000000e+00,   0.0000000e+00,   7.0312500e-01},
    {0.0000000e+00,   0.0000000e+00,   7.1875000e-01},
    {0.0000000e+00,   0.0000000e+00,   7.3437500e-01},
    {0.0000000e+00,   0.0000000e+00,   7.5000000e-01},
    {0.0000000e+00,   0.0000000e+00,   7.6562500e-01},
    {0.0000000e+00,   0.0000000e+00,   7.8125000e-01},
    {0.0000000e+00,   0.0000000e+00,   7.9687500e-01},
    {0.0000000e+00,   0.0000000e+00,   8.1250000e-01},
    {0.0000000e+00,   0.0000000e+00,   8.2812500e-01},
    {0.0000000e+00,   0.0000000e+00,   8.4375000e-01},
    {0.0000000e+00,   0.0000000e+00,   8.5937500e-01},
    {0.0000000e+00,   0.0000000e+00,   8.7500000e-01},
    {0.0000000e+00,   0.0000000e+00,   8.9062500e-01},
    {0.0000000e+00,   0.0000000e+00,   9.0625000e-01},
    {0.0000000e+00,   0.0000000e+00,   9.2187500e-01},
    {0.0000000e+00,   0.0000000e+00,   9.3750000e-01},
    {0.0000000e+00,   0.0000000e+00,   9.5312500e-01},
    {0.0000000e+00,   0.0000000e+00,   9.6875000e-01},
    {0.0000000e+00,   0.0000000e+00,   9.8437500e-01},
    {0.0000000e+00,   0.0000000e+00,   1.0000000e+00},
    {0.0000000e+00,   1.5625000e-02,   1.0000000e+00},
    {0.0000000e+00,   3.1250000e-02,   1.0000000e+00},
    {0.0000000e+00,   4.6875000e-02,   1.0000000e+00},
    {0.0000000e+00,   6.2500000e-02,   1.0000000e+00},
    {0.0000000e+00,   7.8125000e-02,   1.0000000e+00},
    {0.0000000e+00,   9.3750000e-02,   1.0000000e+00},
    {0.0000000e+00,   1.0937500e-01,   1.0000000e+00},
    {0.0000000e+00,   1.2500000e-01,   1.0000000e+00},
    {0.0000000e+00,   1.4062500e-01,   1.0000000e+00},
    {0.0000000e+00,   1.5625000e-01,   1.0000000e+00},
    {0.0000000e+00,   1.7187500e-01,   1.0000000e+00},
    {0.0000000e+00,   1.8750000e-01,   1.0000000e+00},
    {0.0000000e+00,   2.0312500e-01,   1.0000000e+00},
    {0.0000000e+00,   2.1875000e-01,   1.0000000e+00},
    {0.0000000e+00,   2.3437500e-01,   1.0000000e+00},
    {0.0000000e+00,   2.5000000e-01,   1.0000000e+00},
    {0.0000000e+00,   2.6562500e-01,   1.0000000e+00},
    {0.0000000e+00,   2.8125000e-01,   1.0000000e+00},
    {0.0000000e+00,   2.9687500e-01,   1.0000000e+00},
    {0.0000000e+00,   3.1250000e-01,   1.0000000e+00},
    {0.0000000e+00,   3.2812500e-01,   1.0000000e+00},
    {0.0000000e+00,   3.4375000e-01,   1.0000000e+00},
    {0.0000000e+00,   3.5937500e-01,   1.0000000e+00},
    {0.0000000e+00,   3.7500000e-01,   1.0000000e+00},
    {0.0000000e+00,   3.9062500e-01,   1.0000000e+00},
    {0.0000000e+00,   4.0625000e-01,   1.0000000e+00},
    {0.0000000e+00,   4.2187500e-01,   1.0000000e+00},
    {0.0000000e+00,   4.3750000e-01,   1.0000000e+00},
    {0.0000000e+00,   4.5312500e-01,   1.0000000e+00},
    {0.0000000e+00,   4.6875000e-01,   1.0000000e+00},
    {0.0000000e+00,   4.8437500e-01,   1.0000000e+00},
    {0.0000000e+00,   5.0000000e-01,   1.0000000e+00},
    {0.0000000e+00,   5.1562500e-01,   1.0000000e+00},
    {0.0000000e+00,   5.3125000e-01,   1.0000000e+00},
    {0.0000000e+00,   5.4687500e-01,   1.0000000e+00},
    {0.0000000e+00,   5.6250000e-01,   1.0000000e+00},
    {0.0000000e+00,   5.7812500e-01,   1.0000000e+00},
    {0.0000000e+00,   5.9375000e-01,   1.0000000e+00},
    {0.0000000e+00,   6.0937500e-01,   1.0000000e+00},
    {0.0000000e+00,   6.2500000e-01,   1.0000000e+00},
    {0.0000000e+00,   6.4062500e-01,   1.0000000e+00},
    {0.0000000e+00,   6.5625000e-01,   1.0000000e+00},
    {0.0000000e+00,   6.7187500e-01,   1.0000000e+00},
    {0.0000000e+00,   6.8750000e-01,   1.0000000e+00},
    {0.0000000e+00,   7.0312500e-01,   1.0000000e+00},
    {0.0000000e+00,   7.1875000e-01,   1.0000000e+00},
    {0.0000000e+00,   7.3437500e-01,   1.0000000e+00},
    {0.0000000e+00,   7.5000000e-01,   1.0000000e+00},
    {0.0000000e+00,   7.6562500e-01,   1.0000000e+00},
    {0.0000000e+00,   7.8125000e-01,   1.0000000e+00},
    {0.0000000e+00,   7.9687500e-01,   1.0000000e+00},
    {0.0000000e+00,   8.1250000e-01,   1.0000000e+00},
    {0.0000000e+00,   8.2812500e-01,   1.0000000e+00},
    {0.0000000e+00,   8.4375000e-01,   1.0000000e+00},
    {0.0000000e+00,   8.5937500e-01,   1.0000000e+00},
    {0.0000000e+00,   8.7500000e-01,   1.0000000e+00},
    {0.0000000e+00,   8.9062500e-01,   1.0000000e+00},
    {0.0000000e+00,   9.0625000e-01,   1.0000000e+00},
    {0.0000000e+00,   9.2187500e-01,   1.0000000e+00},
    {0.0000000e+00,   9.3750000e-01,   1.0000000e+00},
    {0.0000000e+00,   9.5312500e-01,   1.0000000e+00},
    {0.0000000e+00,   9.6875000e-01,   1.0000000e+00},
    {0.0000000e+00,   9.8437500e-01,   1.0000000e+00},
    {0.0000000e+00,   1.0000000e+00,   1.0000000e+00},
    {1.5625000e-02,   1.0000000e+00,   9.8437500e-01},
    {3.1250000e-02,   1.0000000e+00,   9.6875000e-01},
    {4.6875000e-02,   1.0000000e+00,   9.5312500e-01},
    {6.2500000e-02,   1.0000000e+00,   9.3750000e-01},
    {7.8125000e-02,   1.0000000e+00,   9.2187500e-01},
    {9.3750000e-02,   1.0000000e+00,   9.0625000e-01},
    {1.0937500e-01,   1.0000000e+00,   8.9062500e-01},
    {1.2500000e-01,   1.0000000e+00,   8.7500000e-01},
    {1.4062500e-01,   1.0000000e+00,   8.5937500e-01},
    {1.5625000e-01,   1.0000000e+00,   8.4375000e-01},
    {1.7187500e-01,   1.0000000e+00,   8.2812500e-01},
    {1.8750000e-01,   1.0000000e+00,   8.1250000e-01},
    {2.0312500e-01,   1.0000000e+00,   7.9687500e-01},
    {2.1875000e-01,   1.0000000e+00,   7.8125000e-01},
    {2.3437500e-01,   1.0000000e+00,   7.6562500e-01},
    {2.5000000e-01,   1.0000000e+00,   7.5000000e-01},
    {2.6562500e-01,   1.0000000e+00,   7.3437500e-01},
    {2.8125000e-01,   1.0000000e+00,   7.1875000e-01},
    {2.9687500e-01,   1.0000000e+00,   7.0312500e-01},
    {3.1250000e-01,   1.0000000e+00,   6.8750000e-01},
    {3.2812500e-01,   1.0000000e+00,   6.7187500e-01},
    {3.4375000e-01,   1.0000000e+00,   6.5625000e-01},
    {3.5937500e-01,   1.0000000e+00,   6.4062500e-01},
    {3.7500000e-01,   1.0000000e+00,   6.2500000e-01},
    {3.9062500e-01,   1.0000000e+00,   6.0937500e-01},
    {4.0625000e-01,   1.0000000e+00,   5.9375000e-01},
    {4.2187500e-01,   1.0000000e+00,   5.7812500e-01},
    {4.3750000e-01,   1.0000000e+00,   5.6250000e-01},
    {4.5312500e-01,   1.0000000e+00,   5.4687500e-01},
    {4.6875000e-01,   1.0000000e+00,   5.3125000e-01},
    {4.8437500e-01,   1.0000000e+00,   5.1562500e-01},
    {5.0000000e-01,   1.0000000e+00,   5.0000000e-01},
    {5.1562500e-01,   1.0000000e+00,   4.8437500e-01},
    {5.3125000e-01,   1.0000000e+00,   4.6875000e-01},
    {5.4687500e-01,   1.0000000e+00,   4.5312500e-01},
    {5.6250000e-01,   1.0000000e+00,   4.3750000e-01},
    {5.7812500e-01,   1.0000000e+00,   4.2187500e-01},
    {5.9375000e-01,   1.0000000e+00,   4.0625000e-01},
    {6.0937500e-01,   1.0000000e+00,   3.9062500e-01},
    {6.2500000e-01,   1.0000000e+00,   3.7500000e-01},
    {6.4062500e-01,   1.0000000e+00,   3.5937500e-01},
    {6.5625000e-01,   1.0000000e+00,   3.4375000e-01},
    {6.7187500e-01,   1.0000000e+00,   3.2812500e-01},
    {6.8750000e-01,   1.0000000e+00,   3.1250000e-01},
    {7.0312500e-01,   1.0000000e+00,   2.9687500e-01},
    {7.1875000e-01,   1.0000000e+00,   2.8125000e-01},
    {7.3437500e-01,   1.0000000e+00,   2.6562500e-01},
    {7.5000000e-01,   1.0000000e+00,   2.5000000e-01},
    {7.6562500e-01,   1.0000000e+00,   2.3437500e-01},
    {7.8125000e-01,   1.0000000e+00,   2.1875000e-01},
    {7.9687500e-01,   1.0000000e+00,   2.0312500e-01},
    {8.1250000e-01,   1.0000000e+00,   1.8750000e-01},
    {8.2812500e-01,   1.0000000e+00,   1.7187500e-01},
    {8.4375000e-01,   1.0000000e+00,   1.5625000e-01},
    {8.5937500e-01,   1.0000000e+00,   1.4062500e-01},
    {8.7500000e-01,   1.0000000e+00,   1.2500000e-01},
    {8.9062500e-01,   1.0000000e+00,   1.0937500e-01},
    {9.0625000e-01,   1.0000000e+00,   9.3750000e-02},
    {9.2187500e-01,   1.0000000e+00,   7.8125000e-02},
    {9.3750000e-01,   1.0000000e+00,   6.2500000e-02},
    {9.5312500e-01,   1.0000000e+00,   4.6875000e-02},
    {9.6875000e-01,   1.0000000e+00,   3.1250000e-02},
    {9.8437500e-01,   1.0000000e+00,   1.5625000e-02},
    {1.0000000e+00,   1.0000000e+00,   0.0000000e+00},
    {1.0000000e+00,   9.8437500e-01,   0.0000000e+00},
    {1.0000000e+00,   9.6875000e-01,   0.0000000e+00},
    {1.0000000e+00,   9.5312500e-01,   0.0000000e+00},
    {1.0000000e+00,   9.3750000e-01,   0.0000000e+00},
    {1.0000000e+00,   9.2187500e-01,   0.0000000e+00},
    {1.0000000e+00,   9.0625000e-01,   0.0000000e+00},
    {1.0000000e+00,   8.9062500e-01,   0.0000000e+00},
    {1.0000000e+00,   8.7500000e-01,   0.0000000e+00},
    {1.0000000e+00,   8.5937500e-01,   0.0000000e+00},
    {1.0000000e+00,   8.4375000e-01,   0.0000000e+00},
    {1.0000000e+00,   8.2812500e-01,   0.0000000e+00},
    {1.0000000e+00,   8.1250000e-01,   0.0000000e+00},
    {1.0000000e+00,   7.9687500e-01,   0.0000000e+00},
    {1.0000000e+00,   7.8125000e-01,   0.0000000e+00},
    {1.0000000e+00,   7.6562500e-01,   0.0000000e+00},
    {1.0000000e+00,   7.5000000e-01,   0.0000000e+00},
    {1.0000000e+00,   7.3437500e-01,   0.0000000e+00},
    {1.0000000e+00,   7.1875000e-01,   0.0000000e+00},
    {1.0000000e+00,   7.0312500e-01,   0.0000000e+00},
    {1.0000000e+00,   6.8750000e-01,   0.0000000e+00},
    {1.0000000e+00,   6.7187500e-01,   0.0000000e+00},
    {1.0000000e+00,   6.5625000e-01,   0.0000000e+00},
    {1.0000000e+00,   6.4062500e-01,   0.0000000e+00},
    {1.0000000e+00,   6.2500000e-01,   0.0000000e+00},
    {1.0000000e+00,   6.0937500e-01,   0.0000000e+00},
    {1.0000000e+00,   5.9375000e-01,   0.0000000e+00},
    {1.0000000e+00,   5.7812500e-01,   0.0000000e+00},
    {1.0000000e+00,   5.6250000e-01,   0.0000000e+00},
    {1.0000000e+00,   5.4687500e-01,   0.0000000e+00},
    {1.0000000e+00,   5.3125000e-01,   0.0000000e+00},
    {1.0000000e+00,   5.1562500e-01,   0.0000000e+00},
    {1.0000000e+00,   5.0000000e-01,   0.0000000e+00},
    {1.0000000e+00,   4.8437500e-01,   0.0000000e+00},
    {1.0000000e+00,   4.6875000e-01,   0.0000000e+00},
    {1.0000000e+00,   4.5312500e-01,   0.0000000e+00},
    {1.0000000e+00,   4.3750000e-01,   0.0000000e+00},
    {1.0000000e+00,   4.2187500e-01,   0.0000000e+00},
    {1.0000000e+00,   4.0625000e-01,   0.0000000e+00},
    {1.0000000e+00,   3.9062500e-01,   0.0000000e+00},
    {1.0000000e+00,   3.7500000e-01,   0.0000000e+00},
    {1.0000000e+00,   3.5937500e-01,   0.0000000e+00},
    {1.0000000e+00,   3.4375000e-01,   0.0000000e+00},
    {1.0000000e+00,   3.2812500e-01,   0.0000000e+00},
    {1.0000000e+00,   3.1250000e-01,   0.0000000e+00},
    {1.0000000e+00,   2.9687500e-01,   0.0000000e+00},
    {1.0000000e+00,   2.8125000e-01,   0.0000000e+00},
    {1.0000000e+00,   2.6562500e-01,   0.0000000e+00},
    {1.0000000e+00,   2.5000000e-01,   0.0000000e+00},
    {1.0000000e+00,   2.3437500e-01,   0.0000000e+00},
    {1.0000000e+00,   2.1875000e-01,   0.0000000e+00},
    {1.0000000e+00,   2.0312500e-01,   0.0000000e+00},
    {1.0000000e+00,   1.8750000e-01,   0.0000000e+00},
    {1.0000000e+00,   1.7187500e-01,   0.0000000e+00},
    {1.0000000e+00,   1.5625000e-01,   0.0000000e+00},
    {1.0000000e+00,   1.4062500e-01,   0.0000000e+00},
    {1.0000000e+00,   1.2500000e-01,   0.0000000e+00},
    {1.0000000e+00,   1.0937500e-01,   0.0000000e+00},
    {1.0000000e+00,   9.3750000e-02,   0.0000000e+00},
    {1.0000000e+00,   7.8125000e-02,   0.0000000e+00},
    {1.0000000e+00,   6.2500000e-02,   0.0000000e+00},
    {1.0000000e+00,   4.6875000e-02,   0.0000000e+00},
    {1.0000000e+00,   3.1250000e-02,   0.0000000e+00},
    {1.0000000e+00,   1.5625000e-02,   0.0000000e+00},
    {1.0000000e+00,   0.0000000e+00,   0.0000000e+00},
    {9.8437500e-01,   0.0000000e+00,   0.0000000e+00},
    {9.6875000e-01,   0.0000000e+00,   0.0000000e+00},
    {9.5312500e-01,   0.0000000e+00,   0.0000000e+00},
    {9.3750000e-01,   0.0000000e+00,   0.0000000e+00},
    {9.2187500e-01,   0.0000000e+00,   0.0000000e+00},
    {9.0625000e-01,   0.0000000e+00,   0.0000000e+00},
    {8.9062500e-01,   0.0000000e+00,   0.0000000e+00},
    {8.7500000e-01,   0.0000000e+00,   0.0000000e+00},
    {8.5937500e-01,   0.0000000e+00,   0.0000000e+00},
    {8.4375000e-01,   0.0000000e+00,   0.0000000e+00},
    {8.2812500e-01,   0.0000000e+00,   0.0000000e+00},
    {8.1250000e-01,   0.0000000e+00,   0.0000000e+00},
    {7.9687500e-01,   0.0000000e+00,   0.0000000e+00},
    {7.8125000e-01,   0.0000000e+00,   0.0000000e+00},
    {7.6562500e-01,   0.0000000e+00,   0.0000000e+00},
    {7.5000000e-01,   0.0000000e+00,   0.0000000e+00},
    {7.3437500e-01,   0.0000000e+00,   0.0000000e+00},
    {7.1875000e-01,   0.0000000e+00,   0.0000000e+00},
    {7.0312500e-01,   0.0000000e+00,   0.0000000e+00},
    {6.8750000e-01,   0.0000000e+00,   0.0000000e+00},
    {6.7187500e-01,   0.0000000e+00,   0.0000000e+00},
    {6.5625000e-01,   0.0000000e+00,   0.0000000e+00},
    {6.4062500e-01,   0.0000000e+00,   0.0000000e+00},
    {6.2500000e-01,   0.0000000e+00,   0.0000000e+00},
    {6.0937500e-01,   0.0000000e+00,   0.0000000e+00},
    {5.9375000e-01,   0.0000000e+00,   0.0000000e+00},
    {5.7812500e-01,   0.0000000e+00,   0.0000000e+00},
    {5.6250000e-01,   0.0000000e+00,   0.0000000e+00},
    {5.4687500e-01,   0.0000000e+00,   0.0000000e+00},
    {5.3125000e-01,   0.0000000e+00,   0.0000000e+00},
    {5.1562500e-01,   0.0000000e+00,   0.0000000e+00},
    {5.0000000e-01,   0.0000000e+00,   0.0000000e+00}
}; // colorTable_
} // namespace 


namespace {
class ColorMapTouchTable : public ColorMap
{
    public:
    virtual void operator()(float intensity, float& red, float& green, float& blue) const
    {
        if (intensity < 0)
            throw runtime_error("[ColorMapTouchTable::operator()] negative "
                                "intensity passed in");
        
        int index = (int)(intensity*255);

        if (index == 0)
        {
            red = green = blue = 0;
        }
        else
        {
            float* rgb = colorTable_[index];
            red = rgb[0];
            green = rgb[1];
            blue = rgb[2];
        }
    }
};
} // namespace

namespace {
class ColorMapRB : public ColorMap
{
public:
    virtual void operator()(float intensity, float& red, float& green, float& blue) const
    {
        red = .801f * (1 - intensity) + .199f;
            green = .199f + .801f * intensity;
            blue = .6f * intensity;
    }
};
} // namespace

namespace {
class ColorMapGrey : public ColorMap
{
public:
    virtual void operator()(float intensity, float& red, float& green, float& blue) const
    {
        red = intensity;
        green = intensity;
        blue = intensity;
    }
};
} // namespace

void Pseudo2DGel::Impl::instantiateColorMap()
{
    if (config_.bry)
        colorMap_ = auto_ptr<ColorMap>(new ColorMapBRY);
    else if (config_.grey)
        colorMap_ = auto_ptr<ColorMap>(new ColorMapGrey);
    else
        colorMap_ = auto_ptr<ColorMap>(new ColorMapTouchTable);

    circleColorMap_ = shared_ptr<ColorMap>(new ColorMapRB);
}


Image::Color Pseudo2DGel::Impl::color(float intensity) const
{
    if (!colorMap_.get())
        throw runtime_error("[Pseudo2DGel::Impl::color()] Color map not instantiated.");

    float r=0, g=0, b=0;
    (*colorMap_)(intensity, r, g, b);
    return Image::Color(int(r*255), int(g*255), int(b*255));
}

Image::Color Pseudo2DGel::Impl::circleColor(float intensity) const
{
    if (!circleColorMap_.get())
        throw runtime_error("[Pseudo2DGel::Impl::color()] Circle Color map not instantiated.");

    intensity = min(1.f, max(0.f, intensity));
    float r=0, g=0, b=0;
    (*circleColorMap_)(intensity, r, g, b);
    return Image::Color(int(r*255), int(g*255), int(b*255));
}

size_t Pseudo2DGel::Impl::countUniquePeptides(const ScanList& scans)
{
    if (config_.peptide_id == NULL)
        return 0;
    
    map<string, size_t> counts;

    if (config_.peptide_id != NULL)
    {
        for (size_t i=0; i<scans.size(); i++)
        {
            size_t index = scans.at(i);
            try
            {
                PeptideID::Record record = config_.peptide_id->record(
                    PeptideID::Location(lexical_cast<string>(cache_[index].scanNumber), // TODO check nativeID usage -- dk
                                        cache_[index].retentionTime,
                                        cache_[index].basePeakMZ));
                string sequence = record.sequence;

                // Find the actual sequence present.
                size_t begin = sequence.find(".");
                begin = (begin == string::npos ? 0 : begin);
                size_t end = sequence.find(".", begin);
                end = (end == string::npos ? sequence.size() : end);
                
                string s = sequence.substr(begin, end);
                if (s.size()>0)
                    counts[s] += 1;
            }
            catch(...) {}
        }
    }

    return counts.size();
}

Image::Color Pseudo2DGel::Impl::chooseMarkupColor(size_t cachedIndex)
{
    Image::Color color;

    if (config_.peptide_id != NULL)
    {
        color = Image::Color(0x64, 0x95, 0xED);
        color = Image::white();
        try
        {
            float score = getScore(cachedIndex);
            //float score = (float)config_.peptide_id->record(
            //                        PeptideID::Location(cache_[ms2Index].nativeID,
            //                            cache_[ms2Index].basePeakMZ,
            //                            cache_[ms2Index].retentionTime)).normalizedScore;
            color = circleColor(score);
            idedPeptides_++;
        }
        catch(...) {}
    }
    else
        color = Image::white();

    return color;
}

void Pseudo2DGel::Impl::writeImages(const DataInfo& dataInfo)
{
    string label = ".image." + config_.label;

    writeImage(dataInfo, label + ".itms", itScans_);
    writeImage(dataInfo, label + ".ftms", ftScans_);
}


auto_ptr<IntensityFunction> 
Pseudo2DGel::Impl::createIntensityFunction(const ScanList& scans)
{
    double count = 0;
    double discarded = 0;
    double max = 0;
    double sum = 0;
    double sum2 = 0;
    double sum_log = 0;
    double sum2_log = 0;
    vector<double> logHistogram(20);

    for (ScanList::const_iterator it=scans.begin(); it!=scans.end(); ++it)
    {
        size_t index = *it;

        for (unsigned int j=0; j<scanBuffer_[index].size(); j++) 
        {
            double value = scanBuffer_[index][j];

            // ignore small values for statistics calculations
            if (value<=1)
            {
                discarded++;
                continue;
            }

            double logValue = positiveLogarithm(value); 

            count++;
            if (max < value) max = value;
            sum += value;
            sum2 += value*value;
            sum_log += logValue;
            sum2_log += logValue*logValue;

            int bin = int(logValue);
            if (bin < 0) bin = 0;
            if (bin > (int)logHistogram.size() - 1)
                bin = (int)logHistogram.size() - 1;
            logHistogram[bin]++;
        }
    }

    //double mean = sum/count;
    //double variance = sum2/count - mean*mean;
    double mean_log = sum_log/count;
    double variance_log = sum2_log/count - mean_log*mean_log;
/*
    cout << "count: " << count << endl;
    cout << "discarded: " << discarded << endl;
    cout << "max: " << max << endl;
    cout << "sum: " << sum << endl;
    cout << "sum2: " << sum2 << endl;
    cout << "mean: " << mean << endl;
    cout << "variance: " << variance << endl;
    cout << "sd: " << sqrt(variance) << endl;
    cout << "mean_log: " << mean_log << endl;
    cout << "variance_log: " << variance_log << endl;
    cout << "sd_log: " << sqrt(variance_log) << endl;

    cout << "histogram:\n";
    for (unsigned int i=0; i<logHistogram.size(); i++)
        cout << "  " << setw(2) << i << " " << logHistogram[i] << endl; 
*/

    // instantiate intensity function centered at mean_log, 
    // with radius == zRadius * (standard deviation)
    float radius = (float)(config_.zRadius * sqrt(variance_log)); 
    auto_ptr<IntensityFunction> result(new IntensityFunctionLogStats((float)mean_log, radius)); 
    if (!result.get()) throw runtime_error("[Pseudo2DGel::Impl::createIntensityFunction()] Memory error.");
    return result;
}


namespace {


const int textHeight_ = 20;
const int graphMargin_ = 30;


Image::Color separatorColor_(100, 100, 100);
Image::Color boxColor_(128, 0, 128);


void drawHorizontalSeparator(Image& image, int y, int x1, int x2) 
{
    image.line(Image::Point(x1,y), Image::Point(x2,y), separatorColor_); 
    image.line(Image::Point(x1,y-1), Image::Point(x2,y-1), separatorColor_); 
}


void drawVerticalSeparator(Image& image, int x, int y1, int y2) 
{
    image.line(Image::Point(x,y1), Image::Point(x,y2), separatorColor_);  
    image.line(Image::Point(x-1,y1), Image::Point(x-1,y2), separatorColor_); 
}


} // namespace


void Pseudo2DGel::Impl::writeImage(const DataInfo& dataInfo, const string& label, ScanInfo& scanInfo)
{
    ScanList& scans = scanInfo.scans;

    if (scans.empty())
        return;

    auto_ptr<IntensityFunction> intensityFunction = createIntensityFunction(scans);

    const int titleBarHeight = 3*textHeight_;
    const int x1 = 150;
    const int y1 = titleBarHeight + 300;
    const int x2 = x1 + 4*graphMargin_ + config_.binCount;
    const int y2 = (config_.binScan ? y1 + 4*textHeight_ + (int)scans.size() :
                    y1 + 4*textHeight_ + (int)(config_.timeScale * scanInfo.maxTime - scanInfo.minTime));

    auto_ptr<Image> image = Image::create(x2, y2);

    drawLegend(*image, *intensityFunction, Image::Point(0, titleBarHeight), Image::Point(x1, y1));
    if (config_.binScan)
    {
        drawScans(*image, scanInfo, *intensityFunction, Image::Point(x1, y1), Image::Point(x2, y2));
        drawTIC(*image, scans, Image::Point(0, y1), Image::Point(x1, y2));
    }
    else
    {
        drawTimes(*image, scanInfo, *intensityFunction, Image::Point(x1, y1), Image::Point(x2, y2));
        drawTimeTIC(*image, scans, Image::Point(0, y1), Image::Point(x1, y2));
    }
    // The MS2 legend was removed when the MS2 became uniformly white.
    //if (config_.ms2)
    //    drawMS2Legend(*image, scanInfo, *intensityFunction, Image::Point(x1, titleBarHeight), Image::Point(x2, y1));
    drawTMZ(*image, scans, Image::Point(x1, titleBarHeight+150), Image::Point(x2, y1));

    // separator lines
    image->clip(Image::Point(0,0), Image::Point(x2,y2));
/*
    drawHorizontalSeparator(*image, titleBarHeight, 0, x2);
    drawHorizontalSeparator(*image, y1, 0, x2);
    drawVerticalSeparator(*image, x1, titleBarHeight, y2);
*/

    // title
    image->string(dataInfo.sourceFilename, Image::Point(x2/2, textHeight_), 
                  Image::white(), Image::Giant, Image::CenterX); 

    bfs::path filename = dataInfo.outputDirectory;
    filename /= dataInfo.sourceFilename + label + ".png";

    if (dataInfo.log) *dataInfo.log << "[Pseudo2DGel] Writing file " << filename.string() << endl;
    image->writePng(filename.string().c_str());
}

void Pseudo2DGel::Impl::drawScans(Image& image, 
                              const ScanInfo& scansInfo, 
                              const IntensityFunction& intensityFunction, 
                              const Image::Point& begin, const Image::Point& end) 
{
    const ScanList& scans = scansInfo.scans;
    
    image.clip(begin, end - Image::Point(1,1));

    Image::Point graphBegin = begin + Image::Point(3*graphMargin_, 3*textHeight_);
    
    // draw the scans
    for (size_t j=0; j<scans.size(); j++)
    {
        size_t index = scans[j];
        Image::Point lineBegin = graphBegin + Image::Point(0, (int)j);

        // draw the scan as a row
        for (int i=0; i<config_.binCount; i++)
        {
            float value = (float)(scanBuffer_.at(index).at(i));
            float intensity = intensityFunction(value);
            image.pixel(lineBegin + Image::Point(i, 0), color(intensity));
        }

    }

    // draw ms2 indicators if desired
    if (config_.ms2)
    {
        drawMS2(image, scansInfo, intensityFunction, begin, end);
    }

    // box

    image.line(graphBegin, 
               graphBegin + Image::Point(0,(int)scans.size()), 
               boxColor_); 

    image.line(graphBegin + Image::Point(0,(int)scans.size()), 
               graphBegin + Image::Point(config_.binCount, (int)scans.size()), 
               boxColor_); 

    image.line(graphBegin + Image::Point(config_.binCount, (int)scans.size()),
               graphBegin + Image::Point(config_.binCount, 0),
               boxColor_); 

    image.line(graphBegin + Image::Point(config_.binCount, 0),
               graphBegin,
               boxColor_); 


    // captions
    for (int mz=200; mz<4000; mz+=200)
    {
        if (mz<config_.mzLow) continue;
        if (mz>config_.mzHigh) break;
        
        ostringstream caption;
        caption << mz;

        Image::Point position = graphBegin + Image::Point(bin(mz), -textHeight_); 
        image.string(caption.str(), position, Image::white(), Image::MediumBold, Image::CenterX); 
    }

    image.string("m/z", graphBegin + Image::Point(config_.binCount/2, -textHeight_*2), 
                 Image::white(), Image::Large, Image::CenterX);

    for (size_t j=0; j<scans.size(); j+=50)
    {
        size_t index = scans[j];

        ostringstream caption;
        caption << fixed << setprecision(2) << cache_[index].retentionTime;
        Image::Point position = graphBegin + Image::Point(-graphMargin_, (int)j);
        image.string(caption.str(), position, Image::white(), Image::MediumBold, Image::CenterX | Image::CenterY); 
    }

    image.string("rt", graphBegin + Image::Point(-graphMargin_*2, 0), 
                 Image::white(), Image::Large, Image::Right | Image::CenterY);
}


void Pseudo2DGel::Impl::drawTimes(Image& image, 
                                  const ScanInfo& scansInfo, 
                                  const IntensityFunction& intensityFunction, 
                                  const Image::Point& begin, const Image::Point& end) 
{
    const double rt_range = scansInfo.maxTime - scansInfo.minTime;
    const ScanList& scans = scansInfo.scans;
    size_t lines = (size_t)(config_.timeScale * rt_range);

    image.clip(begin, end - Image::Point(1,1));

    Image::Point graphBegin = begin + Image::Point(3*graphMargin_, 3*textHeight_);
    
    // draw the times
    size_t startIndex = 0;
    size_t endIndex = 0;
    
    vector<size_t> weights;
    weights.resize(lines);
    fill(weights.begin(), weights.end(), 0);
         
    pixelBins_.resize(lines);

    size_t tick = 0;
    // bin the data by time windows
    for (size_t j=0; j<scans.size(); j++)
    {
        size_t index = scans[j];
        //size_t rt_bin = (int)(config_.timeScale * (scansInfo.rts[j]
        //- scansInfo.minTime));
        size_t rt_bin = (size_t) lines * (scansInfo.rts[j] - scansInfo.minTime) / rt_range;

        startIndex = endIndex;
        endIndex = rt_bin;

        for (size_t k=startIndex; k<endIndex; k++)
        {
            if ((int)pixelBins_.at(k).size()<config_.binCount)
            {
                pixelBins_.at(k).resize(config_.binCount);
                fill(pixelBins_.at(k).begin(), pixelBins_.at(k).end(), 0);
            }
            
            for (int i=0; i<config_.binCount; i++)
            {
                float value = (float)scanBuffer_.at(index).at(i);
                pixelBins_[k][i] = (weights.at(k) * pixelBins_.at(k).at(i) + value) / (weights.at(k) + 1);
            }

            if (tick%50 == 0 && weights.at(k) == 0)
            {
                ostringstream caption;
                caption << fixed << setprecision(2) << k;
                Image::Point position = graphBegin + Image::Point(-graphMargin_, (int)k);
                image.string(caption.str(), position, Image::white(), Image::MediumBold, Image::CenterX | Image::CenterY); 
            }

            if (weights.at(k) == 0)
                tick++;
            
            weights[k] += 1;
        }
    }

    for (size_t j=0; j<pixelBins_.size(); j++)
    {
        if (pixelBins_.at(j).empty())
            break;
        
        Image::Point lineBegin = graphBegin + Image::Point(0, (int)j);

        // draw the bin as a row
        for (int i=0; i<config_.binCount; i++)
        {
            float value = (float)(pixelBins_.at(j).at(i));
            float intensity = intensityFunction(value);
            image.pixel(lineBegin + Image::Point(i, 0), color(intensity));
        }
    }

    // draw ms2 indicators if desired
    if (config_.ms2)
    {
        drawMS2(image, scansInfo, intensityFunction, begin, end);
    }

    // box

    image.line(graphBegin, 
               graphBegin + Image::Point(0,(int)lines), 
               boxColor_); 

    image.line(graphBegin + Image::Point(0,(int)lines), 
               graphBegin + Image::Point(config_.binCount, (int)lines), 
               boxColor_); 

    image.line(graphBegin + Image::Point(config_.binCount, (int)lines),
               graphBegin + Image::Point(config_.binCount, 0),
               boxColor_); 

    image.line(graphBegin + Image::Point(config_.binCount, 0),
               graphBegin,
               boxColor_); 

    // captions
    for (int mz=200; mz<4000; mz+=200)
    {
        if (mz<config_.mzLow) continue;
        if (mz>config_.mzHigh) break;
        
        ostringstream caption;
        caption << mz;

        Image::Point position = graphBegin + Image::Point(bin(mz), -textHeight_); 
        image.string(caption.str(), position, Image::white(), Image::MediumBold, Image::CenterX); 
    }

    image.string("m/z", graphBegin + Image::Point(config_.binCount/2, -textHeight_*2), 
                 Image::white(), Image::Large, Image::CenterX);

    //for (size_t j=0; j<scans.size(); j+=50)
    //{
    //    size_t index = scans[j];

    //    ostringstream caption;
    //    caption << fixed << setprecision(2) << cache_[index].retentionTime;
    //    Image::Point position = graphBegin + Image::Point(-graphMargin_, (int)j);
    //    image.string(caption.str(), position, Image::white(), Image::MediumBold, Image::CenterX | Image::CenterY); 
    //}

    image.string("rt", graphBegin + Image::Point(-graphMargin_*2, 0), 
                 Image::white(), Image::Large, Image::Right | Image::CenterY);
}


// TODO move to PeptideID file
ostream& operator<<(ostream& out, const PeptideID::Record record)
{
    out << "[PeptideID::Record nativeID=" << record.nativeID
        << ", retentionTimeSec=" << record.retentionTimeSec
        << ", m/z=" << record.mz << "]";

    return out;
}

void Pseudo2DGel::Impl::drawMS2(Image& image, const ScanInfo& scansInfo, 
             const IntensityFunction& intensityFunction,
             const Image::Point& begin, const Image::Point& end)
{
    Image::Point graphBegin = begin + Image::Point(3*graphMargin_, 3*textHeight_);

    // If a peptide_id object has been configured, then sort the scans
    // by probability.

    ScanList orderedScans(scansInfo.scans.size());
    copy(scansInfo.scans.begin(), scansInfo.scans.end(), orderedScans.begin());

    if (config_.peptide_id != NULL)
    {
        prob_comp comp(this);
        sort(orderedScans.begin(), orderedScans.end(), comp);
    }

    for (size_t j=0; j<ms2Scans_.scans.size(); j++)
    {
        size_t yPixel = (config_.binScan ?
                         lower_bound(scansInfo.rts.begin(), scansInfo.rts.end(), ms2Scans_.rts[j])
                         - scansInfo.rts.begin() - 1
                         : (int)(*lower_bound(scansInfo.rts.begin(), scansInfo.rts.end(), ms2Scans_.rts[j])));
        
        Image::Point lineBegin = graphBegin + Image::Point(0, (int)yPixel);
        
        double sc = 1;
        // The score is removed b/c it just doesn't make sense here.
        //try {
        //    sc = getScore(ms2Scans_.scans[j]);
        //}
        //catch(...)
        //{
        //}

        if (config_.positiveMs2Only && sc <= 0)
            continue;
        
        Image::Point center = lineBegin + Image::Point(ms2Scans_.bin[j],0);
        
        //Image::Color color = chooseMarkupColor(ms2Scans_.scans[j]);
        Image::Color color;
        if (config_.grey)
            color = Image::Color(255, 0, 0);
        else
            color = Image::white();
            

        if (config_.markupShape == circle)
            image.circle(center, 3, color, false);
        else if (config_.markupShape == square)
        {
            Image::Point p1(center.x-1, center.y-1), p2(center.x+1, center.y+1);
            image.rectangle(p1, p2, color, false);
        }
    }
}

void Pseudo2DGel::Impl::drawMS2Legend(Image& image, const ScanInfo& scansInfo, 
                               const IntensityFunction& intensityFunction,
                               const Image::Point& begin, const Image::Point& end)
{
    const int xMargin = 20;
    const int yMargin = 10 + textHeight_;
    const int size = 20;

    image.clip(begin, end - Image::Point(1,1));

    for (int i=0; i<=10; i++)
    {
        float intensity = i / 10.;

        ostringstream oss;
        oss << i*10;

        int x = xMargin + (i+1)*size;
        int y = yMargin + textHeight_;

        image.rectangle(begin + Image::Point(x, y), 
                        begin + Image::Point(x+size, y+size), 
                        circleColor(intensity));

        image.stringUp(oss.str(), begin + Image::Point(x+3, y+size+22), Image::white(), Image::MediumBold); 
    }

    image.string("Probability", begin + Image::Point((begin.x+size*9)/2, yMargin), Image::white(), 
                 Image::Large, Image::CenterX);

    // draw idedPeptides_ / ms2Scans_.size();
    ostringstream oss;
    oss << "#id/#ms2: " << idedPeptides_ << "/" << ms2Scans_.scans.size();
    
    image.string(oss.str(), begin + Image::Point(begin.x+size*11, yMargin + textHeight_), Image::white(),
                                                 Image::Large);
    
    
    size_t unique = countUniquePeptides(ms2Scans_.scans);

    ostringstream oss2;
    oss2 << "#unique: " << unique;

    image.string(oss2.str(), begin + Image::Point(begin.x+size*11, yMargin + 2*textHeight_), Image::white(),
                                                 Image::Large);
    
}

void Pseudo2DGel::Impl::drawLegend(Image& image, 
                               const IntensityFunction& intensityFunction,
                               const Image::Point& begin, const Image::Point& end) 
{
    const int xMargin = 20;
    const int yMargin = 10 + textHeight_;
    const int size = 20;

    image.clip(begin, end - Image::Point(1,1));

    float low = intensityFunction.low();
    float high = intensityFunction.high();

    if (high <= low)
    {
        ostringstream oss;
        oss << "[Pseudo2DGel::Impl::drawLegend()] Illegal low/high bounds: "
            << low << "/" << high;
        throw runtime_error(oss.str().c_str());
    }
    
    for (int i=0; i<=10; i++)
    {
        float t = low + i*(high-low)/10;
        float value = exp(t);
        float intensity = intensityFunction(value);

        ostringstream oss;
        oss << fixed << setprecision(2) << value;

        int x = xMargin;
        int y = yMargin + (i+1)*size;

        image.rectangle(begin + Image::Point(x, y), 
                        begin + Image::Point(x+size, y+size), 
                        color(intensity));

        image.string(oss.str(), begin + Image::Point(x+size+10, y+3), Image::white(), Image::MediumBold); 
    }

    image.string("Intensity", begin + Image::Point((begin.x+end.x)/2, yMargin), Image::white(), 
                 Image::Large, Image::CenterX);
}


namespace {
string shortScientific(double x)
{
    ostringstream oss;
    oss << scientific << setprecision(1) << x;
    string result = oss.str();
    string::size_type indexPlusMinus = result.find_first_of("+-");
    string::size_type indexNonzero = result.find_first_not_of('0', indexPlusMinus+1);
    result.erase(indexPlusMinus, indexNonzero-indexPlusMinus);
    if (indexNonzero == string::npos) result += '0';
    return result;
}
} // namespace


namespace {
Image::Color positiveColor_(255, 255, 255);
Image::Color negativeColor_(255, 255, 0);
} // namespace


void Pseudo2DGel::Impl::drawTIC(Image& image, const ScanList& scans, 
                            const Image::Point& begin, const Image::Point& end)
{
    image.clip(begin, end - Image::Point(1,1));

    // compute min and max

    double min = numeric_limits<double>::max();
    double max = 0;

    for (ScanList::const_iterator it=scans.begin(); it!=scans.end(); ++it)
    {
        size_t index = *it;
        double tic = cache_[index].totalIonCurrent;
        if (min > tic) min = tic;
        if (max < tic) max = tic;
    }

    // draw graph

    Image::Point graphBegin = begin + Image::Point(graphMargin_, 3*textHeight_); 
    Image::Point graphEnd = end + Image::Point(-graphMargin_, -textHeight_); 
    int graphWidth = graphEnd.x - graphBegin.x;

    Image::Point last = graphBegin; 
    bool lastPositive = false;

    double logmin = log(min);
    double logmax = log(max);

    for (size_t j=0; j<scans.size(); j++)
    {
        size_t index = scans[j];
        double logtic = log(cache_[index].totalIonCurrent);
        double t = (logtic-logmin)/(logmax-logmin); // t in [0,1]
        int i = (int)(t * (graphEnd.x-graphBegin.x)); // pixel x-offset
   
        Image::Point current = graphBegin + Image::Point(i,(int)config_.timeScale*j);
        bool currentPositive = (t >= .5); 

        Image::Point midpoint((graphEnd.x+graphBegin.x)/2, (current.y+last.y)/2); 

        if (lastPositive && currentPositive)
        {
            image.line(last, current, positiveColor_);
        }
        else if (!lastPositive && !currentPositive)
        {
            image.line(last, current, negativeColor_);
        }
        else if (lastPositive && !currentPositive)
        {
             image.line(last, midpoint, positiveColor_);
             image.line(current, midpoint, negativeColor_);
        }
        else
        {
             image.line(last, midpoint, negativeColor_);
             image.line(current, midpoint, positiveColor_);
        }

        last = current; 
        lastPositive = currentPositive;
    }

    // box

    image.line(graphBegin, 
               graphBegin + Image::Point(0,(int)config_.timeScale*scans.size()), 
               boxColor_); 

    image.line(graphBegin + Image::Point(graphWidth/2, 0), 
               graphBegin + Image::Point(graphWidth/2, (int)config_.timeScale*scans.size()), 
               boxColor_); 

    image.line(graphBegin + Image::Point(graphWidth, 0), 
               graphBegin + Image::Point(graphWidth, (int)config_.timeScale*scans.size()), 
               boxColor_); 

    image.line(graphBegin,
               graphBegin + Image::Point(graphWidth, 0), 
               boxColor_); 

    image.line(graphEnd - Image::Point(graphWidth, 1),
               graphEnd - Image::Point(0, 1),
               boxColor_); 

    // captions

    double mean = exp((logmin+logmax)/2);
    string captionMin = shortScientific(min);
    string captionMean = shortScientific(mean);
    string captionMax = shortScientific(max);

    image.string("TIC", graphBegin + Image::Point(graphWidth/2, -2*textHeight_), Image::white(), Image::Large, Image::CenterX);
    image.string(captionMin, graphBegin + Image::Point(0, -textHeight_), Image::white(), Image::Small, Image::CenterX);
    image.string(captionMean, graphBegin + Image::Point(graphWidth/2, -textHeight_), Image::white(), Image::Small, Image::CenterX);
    image.string(captionMax, graphBegin + Image::Point(graphWidth, -textHeight_), Image::white(), Image::Small, Image::CenterX);
}


void Pseudo2DGel::Impl::drawTimeTIC(Image& image, const ScanList& scans, 
                                    const Image::Point& begin, const Image::Point& end)
{
    image.clip(begin, end - Image::Point(1,1));

    // compute min and max

    double min = numeric_limits<double>::max();
    double max = 0;

    for (ScanList::const_iterator it=scans.begin(); it!=scans.end(); ++it)
    {
        size_t index = *it;
        double tic = cache_[index].totalIonCurrent;
        if (min > tic) min = tic;
        if (max < tic) max = tic;
    }

    // draw graph

    Image::Point graphBegin = begin + Image::Point(graphMargin_, 3*textHeight_); 
    Image::Point graphEnd = end + Image::Point(-graphMargin_, -textHeight_); 
    int graphWidth = graphEnd.x - graphBegin.x;

    Image::Point last = graphBegin; 
    bool lastPositive = false;

    double logmin = log(min);
    double logmax = log(max);

    double rt = 0.;
    for (size_t j=0; j<scans.size(); j++)
    {
        size_t index = scans[j];
        double logtic = log(cache_[index].totalIonCurrent);
        rt = cache_[index].retentionTime;
        double t = (logtic-logmin)/(logmax-logmin); // t in [0,1]
        int i = (int)(t * (graphEnd.x-graphBegin.x)); // pixel x-offset
   
        Image::Point current = graphBegin + Image::Point(i,(int)rt);
        bool currentPositive = (t >= .5); 

        Image::Point midpoint((graphEnd.x+graphBegin.x)/2, (current.y+last.y)/2); 

        if (lastPositive && currentPositive)
        {
            image.line(last, current, positiveColor_);
        }
        else if (!lastPositive && !currentPositive)
        {
            image.line(last, current, negativeColor_);
        }
        else if (lastPositive && !currentPositive)
        {
             image.line(last, midpoint, positiveColor_);
             image.line(current, midpoint, negativeColor_);
        }
        else
        {
             image.line(last, midpoint, negativeColor_);
             image.line(current, midpoint, positiveColor_);
        }

        last = current; 
        lastPositive = currentPositive;
    }

    // box

    image.line(graphBegin, 
               graphBegin + Image::Point(0, (int)rt), 
               boxColor_); 

    image.line(graphBegin + Image::Point(graphWidth/2, 0), 
               graphBegin + Image::Point(graphWidth/2, (int)rt), 
               boxColor_); 

    image.line(graphBegin + Image::Point(graphWidth, 0), 
               graphBegin + Image::Point(graphWidth, (int)rt), 
               boxColor_); 

    image.line(graphBegin,
               graphBegin + Image::Point(graphWidth, 0), 
               boxColor_); 

    image.line(graphEnd - Image::Point(graphWidth, 1),
               graphEnd - Image::Point(0, 1),
               boxColor_); 

    // captions

    double mean = exp((logmin+logmax)/2);
    string captionMin = shortScientific(min);
    string captionMean = shortScientific(mean);
    string captionMax = shortScientific(max);

    image.string("TIC", graphBegin + Image::Point(graphWidth/2, -2*textHeight_), Image::white(), Image::Large, Image::CenterX);
    image.string(captionMin, graphBegin + Image::Point(0, -textHeight_), Image::white(), Image::Small, Image::CenterX);
    image.string(captionMean, graphBegin + Image::Point(graphWidth/2, -textHeight_), Image::white(), Image::Small, Image::CenterX);
    image.string(captionMax, graphBegin + Image::Point(graphWidth, -textHeight_), Image::white(), Image::Small, Image::CenterX);
}


void Pseudo2DGel::Impl::drawTMZ(Image& image, const ScanList& scans, 
                            const Image::Point& begin, const Image::Point& end)
{
    image.clip(begin, end - Image::Point(1,1));

    // compute sums

    vector<float> tmz(config_.binCount);

    for (ScanList::const_iterator it=scans.begin(); it!=scans.end(); ++it)
    {
        size_t index = *it;
        for (int i=0; i<config_.binCount; i++)
            tmz[i] += (float)(scanBuffer_.at(index).at(i));
    }

    // compute min/max

    double min = numeric_limits<double>::max();
    double max = 0;

    for (int i=0; i<config_.binCount; i++)
    {
        double value = tmz[i];
        if (min > value && value > 0) min = value;
        if (max < value) max = value;
    }

    // draw graph

    Image::Point graphBegin = begin + Image::Point(3*graphMargin_, 3*textHeight_); 
    Image::Point graphEnd = end + Image::Point(-graphMargin_, -textHeight_); 
    int graphWidth = graphEnd.x - graphBegin.x;
    int graphHeight = graphEnd.y - graphBegin.y;

    Image::Point last = graphEnd - Image::Point(graphWidth, 0);
    bool lastPositive = false;

    double logmin = log(min);
    double logmax = log(max);
    double logmean = (logmin + logmax)/2;

    for (int i=0; i<config_.binCount; i++) 
    {
        double logtmz = tmz[i]>=min ? log(tmz[i]) : logmin;
        double t = (logtmz-logmin)/(logmax-logmin); // t in [0,1]
        int j = (int)((1-t) * (graphEnd.y-graphBegin.y)); // pixel y-offset
   
        Image::Point current = graphBegin + Image::Point(i,j);
        bool currentPositive = (t >= .5); 

        Image::Point midpoint((current.x+last.x)/2, (graphEnd.y+graphBegin.y)/2); 

        if (lastPositive && currentPositive)
        {
            image.line(last, current, positiveColor_);
        }
        else if (!lastPositive && !currentPositive)
        {
            image.line(last, current, negativeColor_);
        }
        else if (lastPositive && !currentPositive)
        {
             image.line(last, midpoint, positiveColor_);
             image.line(current, midpoint, negativeColor_);
        }
        else
        {
             image.line(last, midpoint, negativeColor_);
             image.line(current, midpoint, positiveColor_);
        }

        last = current; 
        lastPositive = currentPositive;
    }

    // box

    image.line(graphBegin, 
               graphBegin + Image::Point(graphWidth, 0), 
               boxColor_); 

    image.line(graphBegin + Image::Point(0, graphHeight/2), 
               graphBegin + Image::Point(graphWidth, graphHeight/2),
               boxColor_); 

    image.line(graphBegin + Image::Point(0, graphHeight),
               graphBegin + Image::Point(graphWidth, graphHeight), 
               boxColor_); 

    image.line(graphBegin,
               graphBegin + Image::Point(0, graphHeight), 
               boxColor_); 

    image.line(graphEnd - Image::Point(1, graphHeight),
               graphEnd - Image::Point(1, 0),
               boxColor_); 

    // captions

    double mean = exp(logmean);
    string captionMin = shortScientific(min);
    string captionMean = shortScientific(mean);
    string captionMax = shortScientific(max);

    image.string("Total m/z", graphBegin + Image::Point(graphWidth/2, -textHeight_), Image::white(), Image::Large, Image::CenterX);
    image.string(captionMax, graphBegin + Image::Point(-graphMargin_, 0 ), Image::white(), Image::Small, Image::CenterX | Image::CenterY);
    image.string(captionMean, graphBegin + Image::Point(-graphMargin_, graphHeight/2), Image::white(), Image::Small, Image::CenterX | Image::CenterY);
    image.string(captionMin, graphBegin + Image::Point(-graphMargin_, graphHeight), Image::white(), Image::Small, Image::CenterX | Image::CenterY);
}


//
// Pseudo2DGel
//


PWIZ_API_DECL Pseudo2DGel::Pseudo2DGel(const MSDataCache& cache, const Config& config)
:   impl_(new Impl(cache, config))
{}

PWIZ_API_DECL void Pseudo2DGel::open(const DataInfo& dataInfo)
{
    impl_->open(dataInfo);
}


PWIZ_API_DECL
MSDataAnalyzer::UpdateRequest 
Pseudo2DGel::updateRequested(const DataInfo& dataInfo, 
                             const SpectrumIdentity& spectrumIdentity) const 
{
    return UpdateRequest_Full;
}


PWIZ_API_DECL
void Pseudo2DGel::update(const DataInfo& dataInfo, 
                         const Spectrum& spectrum)
{
    return impl_->update(dataInfo, spectrum); 
}


PWIZ_API_DECL void Pseudo2DGel::close(const DataInfo& dataInfo)
{
    impl_->close(dataInfo);
}


} // namespace analysis 
} // namespace pwiz

