//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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
#include "pwiz/utility/misc/unit.hpp"
#include "boost/filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
namespace bfs = boost::filesystem;


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
const char* sampleEncoded32LittleZlib_ = "eJzjYfBwZgACGSitA6VtoLSTtCeYDoLSSVC6CEo3AenL2skuk4D0PI95LouA9CrnuS6bgPRxoxCXQ1B1l6D0IyjNADWfgWEakG6wZ2A4AaQVHBkYfgHpE04MDGIuDAxVLgB7LB3q";
const char* sampleEncoded32BigZlib_ = "eJxz9mDgYQACZw8GGSitA6VtwLSntBOUDoLSSVC6CEo3uSRrXwbSk1zmecwD0otc5jqvAtKbXEKMjgPpQ1B1l6D0I6j5YOA8jYHBvgFIn2BgcFQA0r8YGJyAbBcxIK5iYAAAfLkd6g==";
const char* sampleEncoded64LittleZlib_ = "eJz7Lz63i5Eh04EBClora9uYkfhLtOOaWJH48Xr2dexI/HXsZqYZygj+6j07zLKQ+HMOW1vkIPGbZPZb5iHxo447WRdA+AlVqT0OqkeO2RRB+AeET052eCrkbVcC4TuUrpjsMGPzefsyCP9BRFuXg/2yEMcKJPNuv7rpVIXEz2yMd6lB4kNFUfkHiqD8D/YQuhLKV4HQDvVQfiSEPtAEFe93AACmsz1+";
const char* sampleEncoded64BigZlib_ = "eJxzyGRg7Jor/p8BChwyGZjbaitbkfisTXHaS5D47HX2evEIvnKGqRn7OiR+ltmOPauR+DkW1ofnIPHzLPfLNCHxC6ydjkc59KRWJUD4RTbHjqg6TD4pfADCL7HzFnrqMHlFqQOEX2Z/fvMMh662iAcQfoVjyDJ7JPOqnG6+uo3Er3GJb8xEcj8KcCg6AKbtP0D5lVBaBUrXO0DoSCi/CaLeoR8iDgC0Qj1+";
const char* sampleEncodedNumpressLinear_ =     "QS69PAAAAAAu7AEMAAAAAA9J0wgQ61LPfgY70wgQbTLPfg4d0wgQ7hLPfgMM1BgQwGKtfgvq1SgQ4UKtfgjc1SgQIyKtfgXO1SgQRAKtfgKw5SgQ78OG4QNVqQugf3Tmpg+6yRCARe2G9wiYdBGAecaFZgs+qjKwizv8oQVa5SgQS0GtfgJM5SgQjCGtfgwC5BgQApLPfgicxA4Q5MmQzQzK9+kgoDYaDQAvNdQwS+AZrAhzqAY5hKD/kA==";
const char* sampleEncodedNumpressLinearZlib_ = "eJxz1NtrwwAEem8YeUA0v+dlDoHXQefr2KyBjFyj83V8skDGO6Hzdcw8VyQEDiStreN+dVVD4KHT2jqOO0CGstLaOtZzQIYL09o6pg1PNQTeH257yBy6kntBfcmzZfy7Tgo0uL5t+84xo0SwofJYaxq33SqjDd3WfxayRgEVezsCdfkAGT2Ka+t4mJ5ICDBNOl/HMecIn8CTkxPO8pz6/lJhgZkUL4O+6RUD7weSaziKV7BZtiz4PwEAkp1KXg==";
const char* sampleEncodedNumpressSlof_ =     "QMHqAAAAAAACvgAAAr4AAAK+AAACvgAANL4AADS+AAA0vgAANL4AADS+GvQ0vvr/NL6//zS+qfE0vgAANL4AADS+AAACvgAAeszWGMHW6VW73lqlQOWH9w==";
const char* sampleEncodedNumpressSlofZlib_ = "eJxzOPiKAQSY9qFiEwws9cVk36//Jvv2A/HKj8hyIPVVZ65JHLz2MnT3vailDk/bvwMAn1ogtQ==";
const char* sampleEncodedNumpressPic_ =     "aMhoyGjIaMhpyGnIachpyGnF2DacUvRpxa5GnFFTachpyGnIaMhcIXFQkXpU8WRlhSWOMA==";
const char* sampleEncodedNumpressPicZlib_ = "eJzLOJEBhpkwePSG2ZygL5lH17nNCQyGiGWciFEsDJhYFfIxJbVVtc8AAAjsG4c=";
const char* sampleEncodedModified64BigZlib_ = "eJxzyGRg7Jor/r/+/X8wcMhkYG6rrWz9j+CzNsVpL6m/D+ez19nrxf+H85UzTM3Y1zFAAZCfZbZjz2okfo6F9eE5SPw8y/0yTUj8Amun41EOPalVCRB+kc2xI6oOk08KH4DwS+y8hZ46TF5R6gDhl9mf3zzDoast4gGEX+EYssweybwqp5uvbiPxa1ziGzMRfAYU4FB0AEzbf4DyK6G0CpSud4DQkVB+E0S9Qz9EHACREFv+";

const char* regressionTest(const BinaryDataEncoder::Config& config,bool expectNumpressIgnored)
{
    if (expectNumpressIgnored)  // when set, expecting numpress not to be used even though it was requested
    {
        return sampleEncodedModified64BigZlib_;
    }
    else
    {
        if (config.numpress == BinaryDataEncoder::Numpress_Linear)
            return (BinaryDataEncoder::Compression_Zlib==config.compression)?sampleEncodedNumpressLinearZlib_:sampleEncodedNumpressLinear_;

        if (config.numpress == BinaryDataEncoder::Numpress_Pic)
            return (BinaryDataEncoder::Compression_Zlib==config.compression)?sampleEncodedNumpressPicZlib_:sampleEncodedNumpressPic_;

        if (config.numpress == BinaryDataEncoder::Numpress_Slof)
            return (BinaryDataEncoder::Compression_Zlib==config.compression)?sampleEncodedNumpressSlofZlib_:sampleEncodedNumpressSlof_;
    }
    if (config.precision == BinaryDataEncoder::Precision_32 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_LittleEndian && 
        config.compression == BinaryDataEncoder::Compression_None)
        return sampleEncoded32Little_;

    if (config.precision == BinaryDataEncoder::Precision_32 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_BigEndian &&
        config.compression == BinaryDataEncoder::Compression_None)
        return sampleEncoded32Big_;

    if (config.precision == BinaryDataEncoder::Precision_64 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_LittleEndian &&
        config.compression == BinaryDataEncoder::Compression_None)
        return sampleEncoded64Little_;

    if (config.precision == BinaryDataEncoder::Precision_64 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_BigEndian &&
        config.compression == BinaryDataEncoder::Compression_None)
        return sampleEncoded64Big_;

    if (config.precision == BinaryDataEncoder::Precision_32 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_LittleEndian && 
        config.compression == BinaryDataEncoder::Compression_Zlib)
        return sampleEncoded32LittleZlib_;
     
    if (config.precision == BinaryDataEncoder::Precision_32 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_BigEndian && 
        config.compression == BinaryDataEncoder::Compression_Zlib)
        return sampleEncoded32BigZlib_;

    if (config.precision == BinaryDataEncoder::Precision_64 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_LittleEndian && 
        config.compression == BinaryDataEncoder::Compression_Zlib)
        return sampleEncoded64LittleZlib_;
     
    if (config.precision == BinaryDataEncoder::Precision_64 &&
        config.byteOrder == BinaryDataEncoder::ByteOrder_BigEndian && 
        config.compression == BinaryDataEncoder::Compression_Zlib)
        return sampleEncoded64BigZlib_;

     throw runtime_error("[BinaryDataEncoderTest::regressionTest()] Untested configuration.");
}

 
void testConfiguration(const BinaryDataEncoder::Config& config_in)
{
    BinaryDataEncoder::Config config(config_in);
    if (os_)
        *os_ << "testConfiguration: " << config << endl;

    // initialize scan data

    vector<double> binary(sampleDataSize_);
    copy(sampleData_, sampleData_+sampleDataSize_, binary.begin());

    bool checkNumpressMaxErrorSupression = (BinaryDataEncoder::Numpress_None != config.numpress)&&(config.numpressLinearErrorTolerance>0);
    if (checkNumpressMaxErrorSupression) 
    {
        binary[1] = numeric_limits<double>::max( )-.1; // attempt to blow out the numpress lossiness limiter
        binary[3] = -binary[1]; // attempt to blow out the numpress lossiness limiter
        binary[5] = .5*binary[1]; // attempt to blow out the numpress lossiness limiter
        binary[7] = .5*binary[3]; // attempt to blow out the numpress lossiness limiter
    }

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

    unit_assert(encoded == regressionTest(config,checkNumpressMaxErrorSupression));

    // decode

    BinaryData<double> decoded;
    encoder.decode(encoded, decoded);

    if (os_)
    {
        *os_ << "decoded: " << decoded.size() << endl;
        copy(decoded.begin(), decoded.end(), ostream_iterator<double>(*os_, "\n")); 
    }

    // validate by comparing scan data before/after encode/decode

    unit_assert(binary.size() == decoded.size());

    const double epsilon = config.precision == BinaryDataEncoder::Precision_64 ? 1e-14 : 1e-5 ;

    auto jt = decoded.begin();
    switch (config.numpress) 
    { 
    case BinaryDataEncoder::Numpress_Linear: 
    case BinaryDataEncoder::Numpress_Slof: 
    case BinaryDataEncoder::Numpress_Pic:
        // lossy compression
        for (auto it = binary.begin(); it!=binary.end(); ++it, ++jt)
        {
            if (0==*it || 0==*jt)
                unit_assert_equal(*it, *jt, 0.1);
            else if (*it > *jt)
                unit_assert((*jt)/(*it) > .999 );
            else 
                unit_assert((*it)/(*jt) > .999 );
        }
        break;
    default:
        for (auto it = binary.begin(); it!=binary.end(); ++it, ++jt)
        {
            unit_assert_equal(*it, *jt, epsilon);
        }
        break;
    }
    if (os_) *os_ << "validated with epsilon: " << fixed << setprecision(1) << scientific << epsilon << "\n\n";
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

    config.precision = BinaryDataEncoder::Precision_32;
    config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
    config.compression = BinaryDataEncoder::Compression_Zlib;
    testConfiguration(config);

    config.precision = BinaryDataEncoder::Precision_32;
    config.byteOrder = BinaryDataEncoder::ByteOrder_BigEndian;
    config.compression = BinaryDataEncoder::Compression_Zlib;
    testConfiguration(config);

    config.precision = BinaryDataEncoder::Precision_64;
    config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
    config.compression = BinaryDataEncoder::Compression_Zlib;
    testConfiguration(config);

    config.precision = BinaryDataEncoder::Precision_64;
    config.byteOrder = BinaryDataEncoder::ByteOrder_BigEndian;
    config.compression = BinaryDataEncoder::Compression_Zlib;
    testConfiguration(config);
    
    // test the numpress stuff with and without zlib, and to see if it honors error limits
    config.compression = BinaryDataEncoder::Compression_None;
    config.numpressLinearErrorTolerance = 0; // means don't do tolerance checks
    config.numpressSlofErrorTolerance = 0; // means don't do tolerance checks
    for (int zloop=3;zloop--;)
    {
        config.numpress = BinaryDataEncoder::Numpress_Linear;
        testConfiguration(config);

        config.numpress = BinaryDataEncoder::Numpress_Slof;
        testConfiguration(config);

        config.numpress = BinaryDataEncoder::Numpress_Pic;
        testConfiguration(config);

        config.compression = BinaryDataEncoder::Compression_Zlib; // and again with zlib
        if (1==zloop) // and finally test numpress excessive error avoidance
        {
            config.numpressLinearErrorTolerance = .01;
            config.numpressSlofErrorTolerance = .01;
        }
    }

}


void testBadFile(const string& filename)
{
    if (os_) *os_ << "testBadFile: " << filename << flush;

    size_t filesize = 0;

    try
    {
        filesize = (size_t) bfs::file_size(filename);
    }
    catch (exception&)
    {
        cerr << "\nUnable to find file " << filename << endl;
        return;
    }

    if (os_) *os_ << " (" << filesize << " bytes)\n"; 

    unit_assert(filesize%sizeof(double) == 0);
    
    // read data from file into memory

    vector<double> data(filesize/sizeof(double));
    ifstream is(filename.c_str(), ios::binary);
    is.read((char*)&data[0], filesize);

    // set configuration to produce the error

    BinaryDataEncoder::Config config;

    if (filename.find("BinaryDataEncoderTest.bad.bin")!=string::npos)
    {
        // zlib compression encoding error with this configuration
        config.precision = BinaryDataEncoder::Precision_32;
        config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
        config.compression = BinaryDataEncoder::Compression_Zlib;
    }

    // encode and decode

    BinaryDataEncoder encoder(config);
    string encoded;
    encoder.encode(data, encoded); 

    BinaryData<double> decoded;
    encoder.decode(encoded, decoded);
    
    // verify

    unit_assert(decoded.size() == data.size());
    for (size_t i=0; i<decoded.size(); i++)
        unit_assert(decoded[i] == data[i]);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        vector<string> filenames;  

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) os_ = &cout;
            else if (bal::starts_with(argv[i], "--")) continue;
            else filenames.push_back(argv[i]);
        }

        if (os_) *os_ << "BinaryDataEncoderTest\n\n";
        test();
        for_each(filenames.begin(), filenames.end(), testBadFile);

    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}


