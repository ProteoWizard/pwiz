//
// DiffTest.cpp
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

#include "Diff.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace std;
using namespace pwiz::util;
using namespace pwiz;
using namespace pwiz::tradata;
using boost::shared_ptr;


ostream* os_ = 0;


void testString()
{
    if (os_) *os_ << "testString()\n";

    Diff<string> diff("goober", "goober");
    unit_assert(diff.a_b.empty() && diff.b_a.empty());
    unit_assert(!diff);

    diff("goober", "goo");
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
}


void testCV()
{
    if (os_) *os_ << "testCV()\n";

    CV a, b;
    a.URI = "uri";
    a.id = "cvLabel";
    a.fullName = "fullName";
    a.version = "version";
    b = a;

    Diff<CV> diff;
    diff(a,b);

    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());
    unit_assert(!diff);

    a.version = "version_changed";

    diff(a,b); 
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.URI.empty() && diff.b_a.URI.empty());
    unit_assert(diff.a_b.id.empty() && diff.b_a.id.empty());
    unit_assert(diff.a_b.fullName.empty() && diff.b_a.fullName.empty());
    unit_assert(diff.a_b.version == "version_changed");
    unit_assert(diff.b_a.version == "version");
}


void testUserParam()
{
    if (os_) *os_ << "testUserParam()\n";

    UserParam a, b;
    a.name = "name";
    a.value = "value";
    a.type = "type";
    a.units = UO_minute;
    b = a;

    Diff<UserParam> diff(a, b);
    unit_assert(!diff);
    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());

    b.value = "value_changed";
    a.units = UO_second;
    unit_assert(diff(a,b));
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.name == "name");
    unit_assert(diff.b_a.name == "name");
    unit_assert(diff.a_b.value == "value");
    unit_assert(diff.b_a.value == "value_changed");
    unit_assert(diff.a_b.type.empty() && diff.b_a.type.empty());
    unit_assert(diff.a_b.units == UO_second);
    unit_assert(diff.b_a.units == UO_minute);
}


void testCVParam()
{
    if (os_) *os_ << "testCVParam()\n";

    CVParam a, b;
    a.cvid = MS_ionization_type; 
    a.value = "420";
    b = a;

    Diff<CVParam> diff(a, b);
    unit_assert(!diff);
    unit_assert(diff.a_b.empty());
    unit_assert(diff.b_a.empty());

    b.value = "value_changed";
    diff(a,b);
    unit_assert(diff);
    if (os_) *os_ << diff << endl;
    unit_assert(diff.a_b.cvid == MS_ionization_type);
    unit_assert(diff.b_a.cvid == MS_ionization_type);
    unit_assert(diff.a_b.value == "420");
    unit_assert(diff.b_a.value == "value_changed");
}


void testParamContainer()
{
    if (os_) *os_ << "testParamContainer()\n";

    ParamContainer a, b;
    a.userParams.push_back(UserParam("common"));
    b.userParams.push_back(UserParam("common"));
    a.cvParams.push_back(MS_m_z);
    b.cvParams.push_back(MS_m_z);
   
    Diff<ParamContainer> diff(a, b);
    unit_assert(!diff);

    a.userParams.push_back(UserParam("different", "1"));
    b.userParams.push_back(UserParam("different", "2"));
    a.cvParams.push_back(MS_charge_state);
    b.cvParams.push_back(MS_intensity);

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.userParams.size() == 1);
    unit_assert(diff.a_b.userParams[0] == UserParam("different","1"));
    unit_assert(diff.b_a.userParams.size() == 1);
    unit_assert(diff.b_a.userParams[0] == UserParam("different","2"));

    unit_assert(diff.a_b.cvParams.size() == 1);
    unit_assert(diff.a_b.cvParams[0] == MS_charge_state); 
    unit_assert(diff.b_a.cvParams.size() == 1);
    unit_assert(diff.b_a.cvParams[0] == MS_intensity);
}


void testSoftware()
{
    if (os_) *os_ << "testSoftware()\n";

    Software a, b;

    a.id = "msdata";
    a.version = "4.20";
    a.set(MS_ionization_type);
    b = a;

    Diff<Software> diff(a, b);
    unit_assert(!diff);

    b.version = "4.21";

    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
}


/*void testPrecursor()
{
    if (os_) *os_ << "testPrecursor()\n";

    Precursor a, b;

    a.mz = 420;
    a.charge = 2;
    b = a;

    Diff<Precursor> diff(a, b);
    unit_assert(!diff);

    b.charge = 3;
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.mz == 0);
    unit_assert(diff.a_b.charge == -1);
    unit_assert(diff.b_a.mz == 0);
    unit_assert(diff.b_a.charge == 1);
}


void testProduct()
{
    if (os_) *os_ << "testProduct()\n";

    Product a, b;

    a.mz = 420;
    a.charge = 2;
    b = a;

    Diff<Product> diff(a, b);
    unit_assert(!diff);

    b.charge = 3;
    
    diff(a, b);
        
    if (os_) *os_ << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.mz == 0);
    unit_assert(diff.a_b.charge == -1);
    unit_assert(diff.b_a.mz == 0);
    unit_assert(diff.b_a.charge == 1);
}*/


void testTraData()
{
    if (os_) *os_ << "testTraData()\n";

    TraData a, b;

    Diff<TraData> diff(a, b);
    unit_assert(!diff);

    b.version = "version";
    a.cvs.push_back(CV());
    b.softwarePtrs.push_back(SoftwarePtr(new Software("software")));
   
    diff(a, b);
    if (os_) *os_ << diff << endl;
    unit_assert(diff);

    unit_assert(diff.a_b.version.empty());
    unit_assert(diff.b_a.version == "version");

    unit_assert(diff.a_b.cvs.size() == 1);
    unit_assert(diff.b_a.cvs.empty());

    unit_assert(diff.a_b.softwarePtrs.empty());
    unit_assert(!diff.b_a.softwarePtrs.empty());
}

/*
void testMaxPrecisionDiff()

{ 
  if (os_)
    {
      *os_ <<"testMaxPrecisionDiff()\n";
    }

  double epsilon = numeric_limits<double>::epsilon(); 

  BinaryDataArrayPtr a(new BinaryDataArray);
  BinaryDataArrayPtr b(new BinaryDataArray);
  BinaryDataArrayPtr c(new BinaryDataArray);
  BinaryDataArrayPtr d(new BinaryDataArray);
  BinaryDataArrayPtr e(new BinaryDataArray);
  BinaryDataArrayPtr f(new BinaryDataArray);

  std::vector<double> data1;
  std::vector<double> data2;
 
  data1.push_back(3.0);
  data2.push_back(3.0000001);
  
  e->data = data1;
  f->data = data2;

  DiffConfig config;
  config.precision=1e-6;

  Diff<BinaryDataArray> diff_toosmall(*e,*f,config);
  
  //not diff for diff of 1e-7
  unit_assert(!diff_toosmall);

  data1.push_back(2.0);
  data2.push_back(2.0001);
  
  c->data = data1;
  d->data = data2;

  data1.push_back(1.0);
  data2.push_back(1.001);

  a->data = data1;
  b->data = data2;

  Diff<BinaryDataArray> diff(*a,*b,config);
  
  //diff 
  unit_assert(diff);

  if(os_) *os_<<diff<<endl;


  Diff<BinaryDataArray> diff2(*c,*d,config);

  //diff
  unit_assert(diff2);
  
  if(os_) *os_<<diff2<<endl;

  // BinaryDataArray UserParam is set
  unit_assert(!diff.a_b.userParams.empty());
  unit_assert(!diff.b_a.userParams.empty());

  // and correctly 
  double maxBin_a_b=boost::lexical_cast<double>(diff.a_b.userParam("Binary data array difference").value); 
  double maxBin_b_a=boost::lexical_cast<double>(diff.a_b.userParam("Binary data array difference").value); 

  unit_assert_equal(maxBin_a_b,.001,epsilon);
  unit_assert_equal(maxBin_b_a,.001,epsilon); 
  
  Run run_a, run_b;
 
  shared_ptr<SpectrumListSimple> sls_a(new SpectrumListSimple);
  shared_ptr<SpectrumListSimple> sls_b(new SpectrumListSimple);
 
  SpectrumPtr spa(new Spectrum);
  SpectrumPtr spb(new Spectrum);
  SpectrumPtr spc(new Spectrum);
  SpectrumPtr spd(new Spectrum);

  spa->binaryDataArrayPtrs.push_back(a);
  spb->binaryDataArrayPtrs.push_back(b);
  spc->binaryDataArrayPtrs.push_back(c);
  spd->binaryDataArrayPtrs.push_back(d);
  
  sls_a->spectra.push_back(spa);
  sls_a->spectra.push_back(spc);
  sls_b->spectra.push_back(spb);
  sls_b->spectra.push_back(spc);
  
  shared_ptr<ChromatogramListSimple> cls_a(new ChromatogramListSimple);
  shared_ptr<ChromatogramListSimple> cls_b(new ChromatogramListSimple);

  ChromatogramPtr cpa(new Chromatogram);
  ChromatogramPtr cpb(new Chromatogram);
  ChromatogramPtr cpc(new Chromatogram);
  ChromatogramPtr cpd(new Chromatogram);

  cpa->binaryDataArrayPtrs.push_back(a);
  cpb->binaryDataArrayPtrs.push_back(b);
  cpc->binaryDataArrayPtrs.push_back(c);
  cpd->binaryDataArrayPtrs.push_back(d);
  
  cls_a->chromatograms.push_back(cpa);
  cls_a->chromatograms.push_back(cpc);
  cls_b->chromatograms.push_back(cpb);
  cls_b->chromatograms.push_back(cpd);
  
  run_a.spectrumListPtr = sls_a;
  run_b.spectrumListPtr = sls_b;

  run_a.chromatogramListPtr = cls_a;
  run_b.chromatogramListPtr = cls_b;

  // Run user param is written for both Spectrum and Chromatogram binary data array difference user params, if present, with the correct value (max of the Spectrum and Chromatogram user params over the SpectrumList/ ChromatogramList respectively)

  Diff<Run> diff_run(run_a,run_b,config);
  
  // diff
  
  unit_assert(diff_run); 


  // Run user params are set

  unit_assert(!diff_run.a_b.userParams.empty());
  unit_assert(!diff_run.b_a.userParams.empty()); 


  // and correctly
  
  double maxSpecList_a_b=boost::lexical_cast<double>(diff_run.a_b.userParam("Spectrum binary data array difference").value);
  double maxSpecList_b_a=boost::lexical_cast<double>(diff_run.b_a.userParam("Spectrum binary data array difference").value);
  
  double maxChrList_a_b=boost::lexical_cast<double>(diff_run.a_b.userParam("Chromatogram binary data array difference").value);
  double maxChrList_b_a=boost::lexical_cast<double>(diff_run.b_a.userParam("Chromatogram binary data array difference").value);
  
  unit_assert_equal(maxSpecList_a_b,.001,epsilon);
  unit_assert_equal(maxSpecList_b_a,.001,epsilon);
  unit_assert_equal(maxChrList_a_b,.001,epsilon);
  unit_assert_equal(maxChrList_b_a,.001,epsilon);

  // test that Spectrum UserParam is written upon finding a binary data diff, with the correct value

  // user params are set  
  unit_assert(!diff_run.a_b.spectrumListPtr->spectrum(0)->userParams.empty());
  unit_assert(!diff_run.b_a.spectrumListPtr->spectrum(0)->userParams.empty()); //user params are set
  
  // and correctly
  
  double maxSpec_a_b=boost::lexical_cast<double>(diff_run.a_b.spectrumListPtr->spectrum(0)->userParam("Binary data array difference").value);
  double maxSpec_b_a=boost::lexical_cast<double>(diff_run.b_a.spectrumListPtr->spectrum(0)->userParam("Binary data array difference").value);

  unit_assert_equal(maxSpec_a_b,.001,epsilon);
  unit_assert_equal(maxSpec_b_a,.001,epsilon); 


  // test that Chromatogram UserParam is written upon finding a binary data diff, with the correct value

  // user params are set
  unit_assert(!diff_run.a_b.chromatogramListPtr->chromatogram(0)->userParams.empty());
  unit_assert(!diff_run.b_a.chromatogramListPtr->chromatogram(0)->userParams.empty());

  // and correctly

  double maxChr_a_b=boost::lexical_cast<double>(diff_run.a_b.chromatogramListPtr->chromatogram(0)->userParam("Binary data array difference").value);
  double maxChr_b_a=boost::lexical_cast<double>(diff_run.b_a.chromatogramListPtr->chromatogram(0)->userParam("Binary data array difference").value);
 
  unit_assert_equal(maxChr_a_b,.001,epsilon);
  unit_assert_equal(maxChr_b_a,.001,epsilon);

  if(os_) *os_<<diff_run<<endl;



  // test that maxPrecisionDiff is being returned correctly for a zero diff within diff_impl::diff(SpectrumList, SpectrumList, SpectrumList, SpectrumList, DiffConfig, double)

  shared_ptr<SpectrumListSimple> sls_a_a(new SpectrumListSimple);
  shared_ptr<SpectrumListSimple> sls_A_A(new SpectrumListSimple);

  double maxPrecisionNonDiffSpec=0;
  diff_impl::diff(*sls_a, *sls_a,*sls_a_a,*sls_A_A,config,maxPrecisionNonDiffSpec);
  unit_assert_equal(maxPrecisionNonDiffSpec,0,epsilon);


  // test that maxPrecisionDiff is being returned correctly for a non-zero diff within diff_impl::diff(SpectrumList, SpectrumList, SpectrumList, SpectrumList, DiffConfig, double)
  
  shared_ptr<SpectrumListSimple> sls_a_b(new SpectrumListSimple);
  shared_ptr<SpectrumListSimple> sls_b_a(new SpectrumListSimple);

  double maxPrecisionDiffSpec=0;
  diff_impl::diff(*sls_a, *sls_b,*sls_a_b,*sls_b_a,config,maxPrecisionDiffSpec);
  unit_assert_equal(maxPrecisionDiffSpec,.001,epsilon);


  // test that maxPrecisionDiff is being returned correctly for a zero diff within diff_impl::diff(ChromatogramList, ChromatogramList, ChromatogramList, ChromatogramList, DiffConfig, double)

  shared_ptr<ChromatogramListSimple> cls_a_a(new ChromatogramListSimple);
  shared_ptr<ChromatogramListSimple> cls_A_A(new ChromatogramListSimple);

  double maxPrecisionNonDiffChr=0;
  diff_impl::diff(*cls_a, *cls_a,*cls_a_a,*cls_A_A,config,maxPrecisionNonDiffChr);
  unit_assert_equal(maxPrecisionNonDiffChr,0,epsilon);

  // test that maxPrecisionDiff is being returned correctly for a non-zero diff within diff_impl::diff(ChromatogramList, ChromatogramList, ChromatogramList, ChromatogramList, DiffConfig, double)

  shared_ptr<ChromatogramListSimple> cls_a_b(new ChromatogramListSimple);
  shared_ptr<ChromatogramListSimple> cls_b_a(new ChromatogramListSimple);

  double maxPrecisionDiffChr=0;
  diff_impl::diff(*cls_a,*cls_b,*cls_a_b,*cls_b_a,config,maxPrecisionDiffChr);
  unit_assert_equal(maxPrecisionDiffSpec,.001,epsilon);

  
}

*/
void test()
{
    testString();
    testCV();
    testUserParam();
    testCVParam();
    testParamContainer();
    testSoftware();
    //testPrecursor();
    //testProduct();
    testTraData();
    //testMaxPrecisionDiff();
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

