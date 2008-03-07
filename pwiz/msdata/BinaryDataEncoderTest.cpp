//
// BinaryDataEncoderTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "BinaryDataEncoder.hpp"
#include "util/unit.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::msdata;


ostream* os_ = 0;


double sampleData_[] = 
{
    200.00018816645022000000, 0.00000000000000000000,
    200.00043034083151000000, 0.00000000000000000000,
    200.00067251579924000000, 0.00000000000000000000,
    200.00091469135347000000, 0.00000000000000000000,
    201.10647068550810000000, 0.00000000000000000000,
    201.10671554643099000000, 0.00000000000000000000,
    201.10696040795017000000, 0.00000000000000000000,
    201.10720527006566000000, 0.00000000000000000000,
    201.10745013277739000000, 908.68475341796875000000,
    201.10769499608537000000, 1266.26928710937500000000,
    201.10793985998967000000, 1258.11450195312500000000,
    201.10818472449023000000, 848.79339599609375000000,
    201.10842958958708000000, 0.00000000000000000000,
    201.10867445528024000000, 0.00000000000000000000,
    201.10891932156963000000, 0.0000000000000000000,
    200, 0,
    300, 1,
    400, 10,
    500, 100,
    600, 1000,
};


const int sampleDataSize_ = sizeof(sampleData_)/sizeof(double);


// regression test strings 
const char* sampleEncoded32Big_ = "Q0gADAAAAABDSAAcAAAAAENIACwAAAAAQ0gAPAAAAABDSRtCAAAAAENJG1IAAAAAQ0kbYgAAAABDSRtyAAAAAENJG4JEYyvTQ0kbkkSeSJ5DSRuiRJ1DqkNJG7JEVDLHQ0kbwgAAAABDSRvSAAAAAENJG+IAAAAAQ0gAAAAAAABDlgAAP4AAAEPIAABBIAAAQ/oAAELIAABEFgAARHoAAA==";
const char* sampleEncoded32Little_ = "DABIQwAAAAAcAEhDAAAAACwASEMAAAAAPABIQwAAAABCG0lDAAAAAFIbSUMAAAAAYhtJQwAAAAByG0lDAAAAAIIbSUPTK2NEkhtJQ55InkSiG0lDqkOdRLIbSUPHMlREwhtJQwAAAADSG0lDAAAAAOIbSUMAAAAAAABIQwAAAAAAAJZDAACAPwAAyEMAACBBAAD6QwAAyEIAABZEAAB6RA==";
const char* sampleEncoded64Little_ = "/xedigEAaUAAAAAAAAAAAIV5fYYDAGlAAAAAAAAAAACkK16CBQBpQAAAAAAAAAAAXy4/fgcAaUAAAAAAAAAAAK4HNjVoI2lAAAAAAAAAAACrvLg2aiNpQAAAAAAAAAAAnMM7OGwjaUAAAAAAAAAAAIIcvzluI2lAAAAAAAAAAABax0I7cCNpQAAAAGB6ZYxAJcTGPHIjaUAAAADAE8mTQOUSSz50I2lAAAAAQHWok0CYs88/diNpQAAAAOBYhopAP6ZUQXgjaUAAAAAAAAAAANvq2UJ6I2lAAAAAAAAAAABpgV9EfCNpQAAAAAAAAAAAAAAAAAAAaUAAAAAAAAAAAAAAAAAAwHJAAAAAAAAA8D8AAAAAAAB5QAAAAAAAACRAAAAAAABAf0AAAAAAAABZQAAAAAAAwIJAAAAAAABAj0A=";
const char* sampleEncoded64Big_ = "QGkAAYqdF/8AAAAAAAAAAEBpAAOGfXmFAAAAAAAAAABAaQAFgl4rpAAAAAAAAAAAQGkAB34/Ll8AAAAAAAAAAEBpI2g1NgeuAAAAAAAAAABAaSNqNri8qwAAAAAAAAAAQGkjbDg7w5wAAAAAAAAAAEBpI245vxyCAAAAAAAAAABAaSNwO0LHWkCMZXpgAAAAQGkjcjzGxCVAk8kTwAAAAEBpI3Q+SxLlQJOodUAAAABAaSN2P8+zmECKhljgAAAAQGkjeEFUpj8AAAAAAAAAAEBpI3pC2erbAAAAAAAAAABAaSN8RF+BaQAAAAAAAAAAQGkAAAAAAAAAAAAAAAAAAEBywAAAAAAAP/AAAAAAAABAeQAAAAAAAEAkAAAAAAAAQH9AAAAAAABAWQAAAAAAAECCwAAAAAAAQI9AAAAAAAA=";


const char* regressionTest(const BinaryDataEncoder::Config& config)
{
    if (config.precision == BinaryDataEncoder::Precision_32 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_LittleEndian)
        return sampleEncoded32Little_;

    if (config.precision == BinaryDataEncoder::Precision_32 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_BigEndian)
        return sampleEncoded32Big_;

    if (config.precision == BinaryDataEncoder::Precision_64 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_LittleEndian)
        return sampleEncoded64Little_;

    if (config.precision == BinaryDataEncoder::Precision_64 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_BigEndian)
        return sampleEncoded64Big_;
     
    throw runtime_error("[BinaryDataEncoderTest::regressionTest()] Untested configuration.");
}

 
void testConfiguration(const BinaryDataEncoder::Config& config)
{
    if (os_)
        *os_ << "testConfiguration: " << config << endl;

    // initialize scan data

    vector<double> binary(sampleDataSize_);
    copy(sampleData_, sampleData_+sampleDataSize_, binary.begin());

    if (os_)
    {
        *os_ << "original: " << binary.size() << endl;
        *os_ << setprecision(20) << fixed;
        copy(binary.begin(), binary.end(), ostream_iterator<double>(*os_, "\n")); 
    }

    // instantiate encoder

    BinaryDataEncoder encoder(config);

    // encode

    string encoded;
    encoder.encode(binary, encoded);

    if (os_)
        *os_ << "encoded: " << encoded.size() << endl << encoded << endl;

    // regression testing for encoding

    unit_assert(encoded == regressionTest(config));

    // decode

    vector<double> decoded;
    encoder.decode(encoded, decoded);

    if (os_)
    {
        *os_ << "decoded: " << decoded.size() << endl;
        copy(decoded.begin(), decoded.end(), ostream_iterator<double>(*os_, "\n")); 
    }

    // validate by comparing scan data before/after encode/decode

    unit_assert(binary.size() == decoded.size());
    const double epsilon = 1e-5; 
    for (vector<double>::const_iterator it=binary.begin(), jt=decoded.begin();  
         it!=binary.end(); ++it, ++jt)
    {
        unit_assert_equal(*it, *jt, epsilon);
    }

    if (os_) *os_ << "validated with epsilon: " << epsilon << "\n\n";
}


void test()
{
    BinaryDataEncoder::Config config;

    config.precision = BinaryDataEncoder::Precision_32;
    config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
    testConfiguration(config);
    
    config.precision = BinaryDataEncoder::Precision_32;
    config.byteOrder = BinaryDataEncoder::ByteOrder_BigEndian;
    testConfiguration(config);

    config.precision = BinaryDataEncoder::Precision_64;
    config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
    testConfiguration(config);

    config.precision = BinaryDataEncoder::Precision_64;
    config.byteOrder = BinaryDataEncoder::ByteOrder_BigEndian;
    testConfiguration(config);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "BinaryDataEncoderTest\n\n";
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}


