//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#include "FrequencyData.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


namespace pwiz {
namespace data {


namespace {


#pragma pack(1)
struct BinaryRecord
{
    double frequency;
    double real;
    double imaginary;

    BinaryRecord(double f=0, double r=0, double i=0)
    :   frequency(f), real(r), imaginary(i)
    {}
};

struct BinaryHeader
{
    char magic[4]; // "PCC\0"
    char type[4];  // "CFD\0" (Complex Frequency Data) 
    int version;
    int headerSize;
    int scanNumber;
    double retentionTime;
    double calibrationA;
    double calibrationB;
    double observationDuration;
    double noiseFloor;
    int recordSize;
    int recordCount;

    BinaryHeader()
    :   version(6), // increment version number here if binary format changes
        headerSize(sizeof(BinaryHeader)),
        scanNumber(0),
        retentionTime(0),
        calibrationA(0),
        calibrationB(0),
        observationDuration(0),
        noiseFloor(0),
        recordSize(sizeof(BinaryRecord)),
        recordCount(0)
    {
        strcpy(magic, "PCC");
        strcpy(type, "CFD");
    }
};
#pragma pack()


FrequencyDatum recordToDatum(const BinaryRecord& record)
{
    return FrequencyDatum(record.frequency, complex<double>(record.real, record.imaginary));
}


BinaryRecord datumToRecord(const FrequencyDatum& datum)
{
    return BinaryRecord(datum.x, datum.y.real(), datum.y.imag());
}


} // namespace


struct FrequencyData::Impl
{
    container data_;

    int scanNumber_;
    double retentionTime_;
    CalibrationParameters calibrationParameters_;
    double observationDuration_;
    double noiseFloor_;

    double shift_;
    complex<double> scale_;

    const_iterator max_;
    double mean_;
    double meanSquare_;
    double sumSquares_;
    double variance_;

    Impl()
    :   scanNumber_(0), retentionTime_(0), 
        observationDuration_(0), noiseFloor_(0),
        shift_(0), scale_(1), 
        mean_(0), meanSquare_(0), sumSquares_(0), variance_(0)
    {}

    void analyze();
    void transform(double shift, complex<double> scale);
    void operator+=(const FrequencyData::Impl& that);
    void calculateNoiseFloor();

    double cutoffNoiseFloor();
    double observationDurationEstimatedFromData();
};


void FrequencyData::Impl::analyze()
{
    max_ = data_.end();

    double sum = 0;
    sumSquares_ = 0;

    for (const_iterator it=data_.begin(); it!=data_.end(); ++it)
    {
        if (max_==data_.end() || norm(it->y)>norm(max_->y))
            max_ = it;

        double value = abs(it->y);
        sum += value;
        sumSquares_ += value*value;
    }

    mean_ = sum/data_.size();
    meanSquare_ = sumSquares_/data_.size();
    variance_ = meanSquare_ - mean_*mean_;

    if (noiseFloor_ == 0)
        calculateNoiseFloor(); 
}


void FrequencyData::Impl::transform(double shift, complex<double> scale)
{
    shift_ += shift;
    scale_ *= scale;

    for (iterator it=data_.begin(); it!=data_.end(); ++it)
    {
        it->x += shift;
        it->y *= scale;
    }
}


void FrequencyData::Impl::operator+=(const FrequencyData::Impl& that)
{
    if (data_.size() != that.data_.size())
        throw runtime_error("[FrequencyData::Impl::operator+=] Size mismatch");

    const double epsilon = 1e-6;

    const_iterator jt = that.data_.begin();
    for (iterator it=data_.begin(); it!=data_.end(); ++it, ++jt)
    {
        if (abs(it->x - jt->x) > epsilon)
            throw runtime_error("[FrequencyData::Impl::operator+=] Domain mismatch");
            
        it->y += jt->y;
    }
}


namespace {
float floatNormIntensity(const FrequencyDatum& datum)
{
    return (float)norm(datum.y);
}
} // namespace


void FrequencyData::Impl::calculateNoiseFloor()
{
    if (data_.empty()) return;

    vector<float> squareIntensities;
    std::transform(data_.begin(), data_.end(), back_inserter(squareIntensities), floatNormIntensity);
    sort(squareIntensities.begin(), squareIntensities.end());
    
    size_t indexMidpoint = squareIntensities.size()/2;
    float medianNorm = squareIntensities[indexMidpoint];
    noiseFloor_ = sqrt(medianNorm * log(2.)); 
}


double FrequencyData::Impl::cutoffNoiseFloor()
{
    double cutoff = mean_ + sqrt(variance_);
    cutoff *= cutoff;

    // calculate stats below cutoff

    int count = 0;
    double sum = 0;
    double sumSquares = 0;

    for (const_iterator it=data_.begin(); it!=data_.end(); ++it)
    {
        if (norm(it->y) < cutoff)
        {
            double value = abs(it->y);
            sum += value;
            sumSquares += value*value;
            count++;
        }
    }

    double mean = sum/count;
    double meanSquare = sumSquares/count;
    double variance = meanSquare - mean*mean;

    return mean + sqrt(variance);
}


double FrequencyData::Impl::observationDurationEstimatedFromData()
{
    // frequency difference between samples is 1/T
    // compute average frequency difference to get a better estimate 

    double sum = 0;
    double count = 0;
    double sumAll = 0;
    double countAll = 0;

	if (data_.empty())
		return 0;

    for (iterator it=data_.begin()+1; it!=data_.end(); ++it)
    {
        double difference = it->x - (it-1)->x; 

        if (norm(it->y)>0 && norm((it-1)->y)>0) // avoid zero-signal gaps 
        {
            count++;
            sum += difference; 
        }

        countAll++;
        sumAll += difference;
    }

    double result = 0;

    if (sum > 0)
        result = count/sum; 
    else if (sumAll > 0)
        result = countAll/sumAll;

    return result;
}


PWIZ_API_DECL FrequencyData::FrequencyData()
:   impl_(new Impl)
{}


PWIZ_API_DECL FrequencyData::FrequencyData(const std::string& filename, IOMode mode)
:   impl_(new Impl)
{
    read(filename, mode);
}


PWIZ_API_DECL FrequencyData::FrequencyData(const FrequencyData& that, const_iterator begin, const_iterator end)
:   impl_(new Impl)
{
    copy(begin, end, back_inserter(impl_->data_));
    impl_->scanNumber_ = that.scanNumber();
    impl_->retentionTime_ = that.retentionTime();
    impl_->calibrationParameters_ = that.calibrationParameters();
    impl_->observationDuration_ = that.observationDuration();
    impl_->noiseFloor_ = that.noiseFloor();
    impl_->analyze();
}


PWIZ_API_DECL FrequencyData::FrequencyData(const FrequencyData& that, const_iterator center, int radius)
:   impl_(new Impl)
{
    const_iterator begin = center<that.data().begin()+radius ? that.data().begin() : center-radius;
	const_iterator end = that.data().end()-center <= radius ? that.data().end() : center+radius+1;
    copy(begin, end, back_inserter(impl_->data_));
    impl_->scanNumber_ = that.scanNumber();
    impl_->retentionTime_ = that.retentionTime();
    impl_->calibrationParameters_ = that.calibrationParameters();
    impl_->observationDuration_ = that.observationDuration();
    impl_->noiseFloor_ = that.noiseFloor();
    impl_->analyze();
}


PWIZ_API_DECL FrequencyData::~FrequencyData()
{} // automatic cleanup of impl_


PWIZ_API_DECL void FrequencyData::read(const string& filename, IOMode mode)
{
    ifstream is(filename.c_str(), ios::binary);
    if (!is)
        throw runtime_error("[Data.cpp::FrequencyData::read()] Unable to open file " + filename);

    if (mode == Automatic)
    {
        char magic[4];
        is.read(magic, 4);

        if (!strncmp(magic, "PCC", 4))
            mode = Binary;
        else
            mode = Text;

        is.seekg(0, ios::beg);
    }

    read(is, mode);

    if (mode == Text)
    {
        impl_->observationDuration_ = impl_->observationDurationEstimatedFromData();
    }
}


PWIZ_API_DECL void FrequencyData::read(std::istream& is, IOMode mode)
{
    if (mode == Text)
    {
        copy(istream_iterator<FrequencyDatum>(is),
             istream_iterator<FrequencyDatum>(),
             back_inserter(impl_->data_));
    }
    else if (mode == Binary)
    {
        BinaryHeader header;
        is.read((char*)&header, sizeof(header));
        if (!is) throw runtime_error("[Data.cpp::FrequencyData::read()] Error reading header.");

        BinaryHeader good;
        if (strncmp(header.magic, good.magic, sizeof(good.magic)) ||
            strncmp(header.type, good.type, sizeof(good.type)) ||
            header.version != good.version ||
            header.headerSize != good.headerSize ||
            header.recordSize != good.recordSize)
            throw runtime_error("[Data.cpp::FrequencyData::read()] Invalid header.");

        impl_->scanNumber_ = header.scanNumber;
        impl_->retentionTime_ = header.retentionTime;
        impl_->calibrationParameters_.A = header.calibrationA;
        impl_->calibrationParameters_.B = header.calibrationB;
        impl_->observationDuration_ = header.observationDuration;
        impl_->noiseFloor_ = header.noiseFloor;

        vector<BinaryRecord> records(header.recordCount);
        is.read((char*)&records[0], header.recordCount*sizeof(BinaryRecord));
        if (!is) throw runtime_error("[Data.cpp::FrequencyData::read()] Error reading records.");

        std::transform(records.begin(), records.end(), back_inserter(impl_->data_), recordToDatum);
    }
    else
    {
        throw logic_error("[Data.cpp::FrequencyData::read()] Cannot read from stream with mode==Automatic.\n");
    }

    impl_->analyze();
}


PWIZ_API_DECL void FrequencyData::write(const std::string& filename, IOMode mode) const
{
    ios::openmode flags = ios::out;
    if (mode == Binary)
        flags |= ios::binary;

    ofstream os(filename.c_str(), flags);
    if (!os)
        throw runtime_error("[Data.cpp::FrequencyData::write()] Unable to open file " + filename);

    write(os, mode);
}


PWIZ_API_DECL void FrequencyData::write(std::ostream& os, IOMode mode) const
{
    if (mode == Text)
    {
        os << setprecision(10);
        copy(impl_->data_.begin(), impl_->data_.end(), ostream_iterator<FrequencyDatum>(os, "\n"));
    }
    else
    {
        BinaryHeader header;
        header.scanNumber = impl_->scanNumber_;
        header.retentionTime = impl_->retentionTime_;
        header.calibrationA = impl_->calibrationParameters_.A;
        header.calibrationB = impl_->calibrationParameters_.B;
        header.observationDuration = impl_->observationDuration_;
        header.noiseFloor = impl_->noiseFloor_;
        header.recordCount = (int)impl_->data_.size();
        os.write((const char*)&header, sizeof(header));
        if (!os) throw runtime_error("[Data.cpp::FrequencyData::write()] Error writing header.");

        vector<BinaryRecord> records(header.recordCount);
        std::transform(impl_->data_.begin(), impl_->data_.end(), records.begin(), datumToRecord);
        os.write((const char*)&records[0], header.recordCount*sizeof(BinaryRecord));
        if (!os) throw runtime_error("[Data.cpp::FrequencyData::write()] Error writing records.");
    }
}


PWIZ_API_DECL FrequencyData::container& FrequencyData::data() {return impl_->data_;}
PWIZ_API_DECL const FrequencyData::container& FrequencyData::data() const {return impl_->data_;}

PWIZ_API_DECL int FrequencyData::scanNumber() const {return impl_->scanNumber_;}
PWIZ_API_DECL void FrequencyData::scanNumber(int value) {impl_->scanNumber_ = value;}

PWIZ_API_DECL double FrequencyData::retentionTime() const {return impl_->retentionTime_;}
PWIZ_API_DECL void FrequencyData::retentionTime(double value) {impl_->retentionTime_ = value;}

PWIZ_API_DECL const CalibrationParameters& FrequencyData::calibrationParameters() const {return impl_->calibrationParameters_;}
PWIZ_API_DECL void FrequencyData::calibrationParameters(const CalibrationParameters& c) {impl_->calibrationParameters_ = c;}

PWIZ_API_DECL double FrequencyData::observationDuration() const {return impl_->observationDuration_;}
PWIZ_API_DECL void FrequencyData::observationDuration(double value) {impl_->observationDuration_ = value;}


PWIZ_API_DECL double FrequencyData::noiseFloor() const {return impl_->noiseFloor_;}
PWIZ_API_DECL void FrequencyData::noiseFloor(double value) {impl_->noiseFloor_ = value;}

PWIZ_API_DECL void FrequencyData::transform(double shift, complex<double> scale) {impl_->transform(shift, scale);}
PWIZ_API_DECL double FrequencyData::shift() const {return impl_->shift_;}
PWIZ_API_DECL complex<double> FrequencyData::scale() const {return impl_->scale_;}
PWIZ_API_DECL void FrequencyData::normalize() {impl_->transform(-impl_->max_->x, 1/abs(impl_->max_->y));}
PWIZ_API_DECL void FrequencyData::operator+=(const FrequencyData& that) {*impl_+=*that.impl_;}
PWIZ_API_DECL void FrequencyData::analyze() {impl_->analyze();}
PWIZ_API_DECL FrequencyData::const_iterator FrequencyData::max() const {return impl_->max_;}
PWIZ_API_DECL double FrequencyData::mean() const {return impl_->mean_;}
PWIZ_API_DECL double FrequencyData::meanSquare() const {return impl_->meanSquare_;}
PWIZ_API_DECL double FrequencyData::sumSquares() const {return impl_->sumSquares_;}
PWIZ_API_DECL double FrequencyData::variance() const {return impl_->variance_;}
PWIZ_API_DECL double FrequencyData::cutoffNoiseFloor() const {return impl_->cutoffNoiseFloor();}
PWIZ_API_DECL double FrequencyData::observationDurationEstimatedFromData() const {return impl_->observationDurationEstimatedFromData();}


namespace {
bool hasFrequencyLessThan(const FrequencyDatum& a, const FrequencyDatum& b)
{
    return a.x < b.x;
}
} // namespace


PWIZ_API_DECL FrequencyData::const_iterator FrequencyData::findNearest(double frequency) const 
{
    FrequencyDatum dummy(frequency, 0);
    const_iterator above = lower_bound(impl_->data_.begin(), impl_->data_.end(), dummy, hasFrequencyLessThan);
    if (above == impl_->data_.begin()) return above;
    if (above == impl_->data_.end()) return above-1;

    const_iterator below = above - 1;
    return (abs(above->x-frequency) < abs(below->x-frequency)) ? above : below;
}


PWIZ_API_DECL pair<double,double> FrequencyData::magnitudeSample(const FrequencyDatum& datum)
{
    return make_pair(datum.x, abs(datum.y));
}



} // namespace data 
} // namespace pwiz

