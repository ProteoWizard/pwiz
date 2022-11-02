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


#include "FrequencyData.hpp"
#include "FrequencyDataTestData.hpp"
#include "pwiz/data/misc/CalibrationParameters.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/filesystem/operations.hpp>


using namespace pwiz::util;
using namespace pwiz::data;


ostream* os_;


void diff(const string& filename1, const string& filename2)
{
	ifstream file1(filename1.c_str()), file2(filename2.c_str());
	string line1, line2;
	while (std::getline(file1, line1) && std::getline(file2, line2))
	    unit_assert(line1 == line2);
    if (os_) *os_ << "diff " << filename1 << " " << filename2 << ": success\n";
}


string filename1 = "FrequencyDataTest.output1.txt";


void test()
{
    // create some data, f(x) = abs(5-(x-2))
    FrequencyData fd;
    FrequencyData::container& data = fd.data();
    for (int i=-5; i<=5; i++)
        data.push_back(FrequencyDatum(i+2, 5-abs(i)));
    fd.analyze(); // recache after changing data

    // verify peak()
    FrequencyData::const_iterator max = fd.max();
    unit_assert(max->x == 2);
    unit_assert(max->y == 5.);

    // verify stats
    unit_assert(fd.mean() == 25./11);
    unit_assert(fd.meanSquare() == 85./11);
    unit_assert(fd.sumSquares() == 85.);
    unit_assert_equal(fd.variance(), 85./11 - 25.*25/11/11, 1e-12);

    // write out data
    if (os_) *os_ << "Writing " << filename1 << endl;
    fd.write(filename1, FrequencyData::Text);

    // read into const FrequencyData
    string filename2 = "FrequencyDataTest.output2.txt";
    FrequencyData fd2(filename1, FrequencyData::Text);

    // verify normalize()
    fd2.normalize();
    unit_assert(fd2.shift() == -2);
    unit_assert(fd2.scale() == 1./5);
    max = fd2.max();
    unit_assert(max->x == 0);
    unit_assert(max->y == 1.);

    // verify transform(shift, scale)
    fd2.transform(-fd2.shift(), 1./fd2.scale());

    // verify read/write
    if (os_) *os_ << "Writing " << filename2 << endl;
    fd2.write(filename2, FrequencyData::Text);
    diff(filename1, filename2);

    // test subrange
    string filename3 = "FrequencyDataTest.output3.txt";
    FrequencyData fd3(fd2, fd2.data().begin(), fd2.max()); // copy first half
    if (os_) *os_ << "Writing " << filename3 << endl;
    fd3.write(filename3, FrequencyData::Text);
    FrequencyData fd4(fd2, fd2.max(), fd2.data().end()); // copy second half
    ofstream os(filename3.c_str(), ios::app);
    fd4.write(os, FrequencyData::Text);
    os.close();
    diff(filename1, filename3);

    // read/write binary, and metadata
    fd.scanNumber(555);
    fd.retentionTime(444);
    fd.calibrationParameters(CalibrationParameters(1,1));
    fd.observationDuration(666);
    fd.noiseFloor(777);
    string filename4a = "FrequencyDataTest.output4a.txt";
    if (os_) *os_ << "Writing " << filename4a << endl;
    fd.write(filename4a, FrequencyData::Text);
    string filenameBinary1 = "FrequencyDataTest.output1.cfd";
    if (os_) *os_ << "Writing " << filenameBinary1 << endl;
    fd.write(filenameBinary1);

    FrequencyData fd5(filenameBinary1);
    unit_assert(fd5.observationDuration() == 666);
    fd5.observationDuration(fd.observationDurationEstimatedFromData());
    unit_assert(fd5.scanNumber() == 555);
    unit_assert(fd5.retentionTime() == 444);
    unit_assert(fd5.observationDuration() == 1);
    unit_assert(fd5.noiseFloor() == 777);
    if (os_) *os_ << "Calibration: " << fd5.calibrationParameters().A << " " << fd5.calibrationParameters().B << endl;

    string filename4b = "FrequencyDataTest.output4b.txt";
    if (os_) *os_ << "Writing " << filename4b << endl;
    fd5.write(filename4b, FrequencyData::Text);
    diff(filename4a, filename4b);
    fd.calibrationParameters(CalibrationParameters());

    // test window
    FrequencyData window1(fd, data.begin()+1, 2);
    FrequencyData window2(fd, fd.max(), 1);
    FrequencyData window3(fd, data.end()-2, 2);
    string filename5 = "FrequencyDataTest.output5.txt";
    if (os_) *os_ << "Writing " << filename5 << endl;
    ofstream os5(filename5.c_str());
    window1.write(os5, FrequencyData::Text);
    window2.write(os5, FrequencyData::Text);
    window3.write(os5, FrequencyData::Text);
    os5.close();
    diff(filename1, filename5);
}


void testFind()
{
    const FrequencyData fd(filename1);

    FrequencyData::const_iterator it = fd.findNearest(-.2);
    unit_assert(it!=fd.data().end() && it->x==0);

    it = fd.findNearest(.2);
    unit_assert(it!=fd.data().end() && it->x==0);

    it = fd.findNearest(6.1);
    unit_assert(it!=fd.data().end() && it->x==6);

    it = fd.findNearest(7.1);
    unit_assert(it!=fd.data().end() && it->x==7);
    
    it = fd.findNearest(666);
    unit_assert(it!=fd.data().end() && it->x==7);

    it = fd.findNearest(-666);
    unit_assert(it==fd.data().begin());
}


void testAddition()
{
    FrequencyData fd(filename1);
    FrequencyData fd2(filename1);

    fd += fd;

    for (FrequencyData::const_iterator it=fd.data().begin(), jt=fd2.data().begin(); 
         it!=fd.data().end(); 
         ++it, ++jt)
        unit_assert(it->y == 2.*jt->y);

    fd2.transform(0, -2.);
    fd += fd2;

    for (FrequencyData::const_iterator it=fd.data().begin(); it!=fd.data().end(); ++it)
        unit_assert(it->y == 0.);
}


void testNoiseFloor()
{
    FrequencyData fd(filename1);
    if (os_) *os_ << "variance: " << fd.variance() << endl;
    if (os_) *os_ << "noiseFloor: " << fd.noiseFloor() << endl;
}


void cleanTests()
{
    if (os_) *os_ << "Deleting FrequencyDataTest.output*.txt\n";
    vector<bfs::path> filepaths;
    expand_pathmask("FrequencyDataTest.output*.*", filepaths);
    for (size_t i=0; i < filepaths.size(); ++i)
        boost::filesystem::remove(filepaths[i]);
}


void testNoiseFloorVarianceCalculation()
{
    if (os_) *os_ << "testNoiseFloorVarianceCalculation()\n";
    if (os_) *os_ << setprecision(10);

    // test noise floor calculation on sample frequency data 

    string filename = "FrequencyDataTest.cfd.temp.txt";
    ofstream temp(filename.c_str());
    temp << sampleFrequencyData_;
    temp.close();

    FrequencyData fd(filename);
    boost::filesystem::remove(filename);

    double result = fd.cutoffNoiseFloor();
    if (os_) *os_ << "result: " << result << endl;
    unit_assert_equal(result, 29000, 1000);

    // test noise floor calculation on sample mass data 

    FrequencyData fdMasses;
    CalibrationParameters cp = CalibrationParameters::thermo_FT();

    for (RawMassDatum* p=sampleMassData_; p!=sampleMassData_+sampleMassDataSize_; ++p)
        fdMasses.data().push_back(FrequencyDatum(cp.frequency(p->mz), p->intensity));
    fdMasses.analyze();

    double result2 = fdMasses.cutoffNoiseFloor();
    if (os_) *os_ << "result2: " << result2 << endl;
    unit_assert_equal(result2, 6000, 1000);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) // verbose
            os_ = &cout;

        if (os_) *os_ << "FrequencyDataTest\n";

        test();
        testFind();
        testAddition();
        testNoiseFloor();
        cleanTests();
        testNoiseFloorVarianceCalculation();

        if (os_) *os_ << "success\n";
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

