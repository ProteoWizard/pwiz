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

#include "TransientData.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "scoped_array.hpp"
#include "pwiz/utility/misc/endian.hpp"
#include "fftw3.h"
#include <iostream>
#include <iomanip>
#include <fstream>
#include <map>
#include <stdexcept>
#include <sstream>
#include <iterator>
#include <cmath>
#include <cstring>
#include <algorithm>


#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif 


using namespace pwiz::util;
using namespace std;


namespace pwiz {
namespace data {


struct TransientData::Impl
{
    double startTime;
    double observationDuration;
    double A;
    double B;

    vector<double> data;

    double bandwidth() const {return data.size()/observationDuration/2;}

    void writeHeader(ostream& os);
    void writeDataText(ostream& os);
    void writeDataBinary(ostream& os);

    Impl() : startTime(0), observationDuration(0), A(0), B(0) {}
};


void TransientData::Impl::writeHeader(ostream& os)
{
    os.precision(12);
    os << "MIDAS Data File v3.0\n";
    os << "\n";
    os << "Data Parameters:\n";
    os << "Data Detected at (seconds): " << fixed << setprecision(6) << startTime << endl;
    os << "Storage Type: float\n";
    os << "Voltage Scale: 1.0\n";
    os << "Data Points: " << data.size() << endl;  
    os << "Bandwidth: " << scientific << setprecision(15) << bandwidth() << endl; 
    os << "Reference Frequency: 0.0\n";
    os << "Calibration Type: 0\n";
    os << "Source Coeff0: " << A << endl;
    os << "Source Coeff1: " << B << endl;
    os << "Source Coeff2: 0.0\n";
    os << "Trap Voltage: 1.0\n";
    os << "Data:\n";
}


void TransientData::Impl::writeDataText(ostream& os)
{
    os.precision(6);
    os << scientific;
    copy(data.begin(), data.end(), ostream_iterator<double>(os, "\n"));
}


namespace {
inline float toFloat(double d) {return static_cast<float>(d);}
} // namespace


void TransientData::Impl::writeDataBinary(ostream& os)
{
    vector<float> stage;
    transform(data.begin(), data.end(), back_inserter(stage), toFloat);
    os.write((const char*)&stage[0], (streamsize)stage.size()*sizeof(float));
}


namespace {
unsigned int readParameters(const string& filename, map<string,string>& parameters)
{
    ifstream is(filename.c_str(), ios::binary);
    if (!is) throw runtime_error("[TransientData] Error reading file " + filename);
    
    const string& headerGood = "MIDAS Data File v3.";
    string headerTest;
    getline(is, headerTest);

    if (headerTest.substr(0, headerGood.size()) != headerGood)
        throw runtime_error("[TransientData] Not MIDAS format: " + filename);

    for (string buffer; is && buffer.substr(0,5)!="Data:"; )
    {
        getline(is, buffer);
        if (buffer.empty()) continue;
        istringstream iss(buffer);
        string name;
        getline(iss, name, ':');
        string value;
        iss >> value;
        if (value.empty()) continue;
        parameters[name] = value;
    }

    // return the number of bytes read
    int result = is.tellg();
    return result;
}


void readDataBinary(ifstream& is, unsigned int offset, unsigned int count, vector<double>& result)
{
    is.seekg(offset, ios::beg);

    // read in raw data as floats
    vector<float> raw(count);
    const unsigned int byteCount = count*sizeof(float);
    is.read((char*)&raw[0], byteCount);
    if (!is) throw runtime_error("[TransientData] Error reading data.\n");

    #ifdef PWIZ_BIG_ENDIAN
    unsigned int* buffer = reinterpret_cast<unsigned int*>(&raw[0]);
    transform(buffer, buffer+count, buffer, util::endianize32);
    #endif // PWIZ_BIG_ENDIAN 

    // return result as doubles
    copy(raw.begin(), raw.end(), result.begin());
}


void readDataText(ifstream& is, unsigned int offset, unsigned int count, vector<double>& result)
{
    is.seekg(offset, ios::beg);

    // read in raw data as floats
    vector<float> raw;
    copy(istream_iterator<float>(is), istream_iterator<float>(), back_inserter(raw));
    if (raw.size() != count)
        throw runtime_error("[TransientData] File size does not match read request.\n");

    // return result as doubles
    copy(raw.begin(), raw.end(), result.begin());
}


void readData(const string& filename, unsigned int offset, unsigned int count, vector<double>& result)
{
    ifstream is(filename.c_str(), ios::binary);
    if (!is) throw runtime_error("[TransientData] Error reading file " + filename);

    is.seekg(0, ios::end);
    if (offset+count*sizeof(float) == (unsigned int)is.tellg())
        readDataBinary(is, offset, count, result);
    else
        readDataText(is, offset, count, result);
}
}//namespace


PWIZ_API_DECL TransientData::TransientData()
:   impl_(new Impl)
{}


PWIZ_API_DECL TransientData::TransientData(const string& filename)
:   impl_(new Impl)
{
    map<string,string> parameters;
    unsigned int headerSize = readParameters(filename, parameters);

    impl_->startTime = atof(parameters["Data Detected at (seconds)"].c_str());

    int sampleCount = atoi(parameters["Data Points"].c_str());
    impl_->data.resize(sampleCount);

    double bandwidth = atof(parameters["Bandwidth"].c_str());
    impl_->observationDuration = impl_->data.size()/bandwidth/2;

    impl_->A = atof(parameters["Source Coeff0"].c_str());
    impl_->B = atof(parameters["Source Coeff1"].c_str());
   
    if (parameters["Storage Type"] != "float")
        throw runtime_error("Error: Storage type '" + parameters["Storage Type"] + "' not implemented.\n");

    readData(filename, headerSize, sampleCount, impl_->data);
}


PWIZ_API_DECL TransientData::~TransientData()
{} // automatic destruction of impl_


PWIZ_API_DECL double TransientData::startTime() const {return impl_->startTime;}
PWIZ_API_DECL void TransientData::startTime(double value) {impl_->startTime = value;}
PWIZ_API_DECL double TransientData::observationDuration() const {return impl_->observationDuration;}
PWIZ_API_DECL void TransientData::observationDuration(double value) {impl_->observationDuration = value;}
PWIZ_API_DECL double TransientData::A() const {return impl_->A;}
PWIZ_API_DECL void TransientData::A(double value) {impl_->A = value;}
PWIZ_API_DECL double TransientData::B() const {return impl_->B;}
PWIZ_API_DECL void TransientData::B(double value) {impl_->B = value;}
PWIZ_API_DECL const vector<double>& TransientData::data() const {return impl_->data;}
PWIZ_API_DECL vector<double>& TransientData::data() {return impl_->data;}
PWIZ_API_DECL double TransientData::bandwidth() const {return impl_->bandwidth();}


PWIZ_API_DECL double TransientData::magneticField() const 
{
    const double e = 1.60217733e-19;
    const double mu = 1.6605402e-27;
    return impl_->A*(2*M_PI*mu)/e;
}


PWIZ_API_DECL void TransientData::computeFFT(unsigned int zeroPadding, FrequencyData& result) const
{
    if (zeroPadding < 1)
        throw runtime_error("[TransientData] zeroPadding must be >= 1.");

    size_t sampleCount = impl_->data.size();
    if (sampleCount == 0)
        throw runtime_error("[TransientData] No data.");

    // create new array of zero-padded transient data
    vector<double> in(sampleCount * zeroPadding);
    copy(impl_->data.begin(), impl_->data.end(), in.begin());
    fill(in.begin()+sampleCount, in.end(), 0);

    // allocate output array
    int frequencyCount = (int)sampleCount*zeroPadding/2;
    scoped_array<fftw_complex> out(frequencyCount+1);

    // compute the FFT
   
    fftw_plan plan = fftw_plan_dft_r2c_1d((int)in.size(), 
                                          &in[0], 
                                          out.begin(), 
                                          FFTW_ESTIMATE);
    fftw_execute(plan);
    fftw_destroy_plan(plan);

    // return frequency data
    result.retentionTime(startTime());
    result.calibrationParameters(CalibrationParameters(impl_->A,impl_->B));
    result.observationDuration(observationDuration());
    for (int i=1; i<=frequencyCount; i++)
    {
        double frequency = impl_->bandwidth()*i/frequencyCount;
        double real = out[i][0];
        double imaginary = out[i][1];
        result.data().push_back(FrequencyDatum(frequency, complex<double>(real,imaginary)));
    }
    result.analyze();
}


PWIZ_API_DECL void TransientData::add(const Signal& signal)
{
    size_t sampleCount = impl_->data.size();
    double t0 = impl_->startTime;
    double T = impl_->observationDuration; 
    vector<double>::iterator it = impl_->data.begin();

    for (size_t i=0; i<sampleCount; ++i, ++it)
    {
        double t = t0 + T*i/sampleCount; 
        *it += signal(t);
    }
}


PWIZ_API_DECL void TransientData::write(std::ostream& os, Format format)
{
    impl_->writeHeader(os);

    switch (format)
    {
        case Binary:
            impl_->writeDataBinary(os);
            return;
        case Text: 
            impl_->writeDataText(os);
            return;
        default:
            throw runtime_error("[TransientData::write()]  This is not happening.");
    }
}


PWIZ_API_DECL void TransientData::write(const std::string& filename, Format format)
{
    ofstream os(filename.c_str());
    if (!os) throw runtime_error("Error creating file " + filename);
    write(os, format); 
}


} // namespace data 
} // namespace pwiz


