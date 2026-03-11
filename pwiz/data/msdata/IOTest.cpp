//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "IO.hpp"
#include "Diff.hpp"
#include "References.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::msdata;
using boost::iostreams::stream_offset;


ostream *os_ = 0;

template <typename object_type>
void testObject(const object_type& a)
{
    if (os_) *os_ << "testObject(): " << typeid(a).name() << endl;

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    object_type b;
    istringstream iss(oss.str());
    IO::read(iss, b);

    // compare 'a' and 'b'

    Diff<object_type, DiffConfig> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);
}


template <typename object_type>
void testObjectWithMSData(const object_type& a, const MSData& msd)
{
    if (os_) *os_ << "testObject(): " << typeid(a).name() << endl;

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a, msd);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    object_type b;
    istringstream iss(oss.str());
    IO::read(iss, b);

    // compare 'a' and 'b'

    Diff<object_type> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);
}


void testObject_SpectrumList(const SpectrumList& a)
{
  if (os_) *os_ << "testObject_SpectrumList(): " << endl;

  // write 'a' out to a stream

  ostringstream oss;
  XMLWriter writer(oss);
  MSData dummy;
  IO::write(writer, a, dummy);
  if (os_) *os_ << oss.str() << endl;

  // read 'b' in from stream

  SpectrumListSimple b;
  istringstream iss(oss.str());
  IO::read(iss, b);

  // compare 'a' and 'b'

  Diff<SpectrumList, DiffConfig, SpectrumListSimple> diff(a,b);
  if (diff && os_) *os_ << "diff:\n" << diff << endl;
  unit_assert(!diff);


}

void testObject_ChromatogramList(const ChromatogramList& a)

{
  if (os_) *os_ << "testObject_ChromatogramList(): " << endl;

  // write 'a' out to a stream

  ostringstream oss;
  XMLWriter writer(oss);
  IO::write(writer, a);
  if (os_) *os_ << oss.str() << endl;

  // read 'b' in from stream

  ChromatogramListSimple b;
  istringstream iss(oss.str());
  IO::read(iss, b);

  // compare 'a' and 'b'

  Diff<ChromatogramList, DiffConfig, ChromatogramListSimple> diff(a,b);
  if (diff && os_) *os_ << "diff:\n" << diff << endl;
  unit_assert(!diff);
}


void testCV()
{
    CV a;
    a.URI = "abcd";
    a.id = "efgh";
    a.fullName = "ijkl";
    a.version = "mnop";

    testObject(a);
}


void testUserParam()
{
    UserParam a;
    a.name = "abcd";
    a.value = "efgh";
    a.type = "ijkl";
    a.units = UO_minute;

    testObject(a);
}


void testCVParam()
{
    CVParam a(MS_selected_ion_m_z, "810.48", MS_m_z);
    testObject(a);

    CVParam b(UO_second, "123.45");
    testObject(b);
}


void testParamGroup()
{
    ParamGroup a("pg");
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_selected_ion_m_z, "666", MS_m_z));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


template <typename object_type>
void testNamedParamContainer()
{
    object_type a;
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_selected_ion_m_z, "666", MS_m_z));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


void testSourceFile()
{
    SourceFile a("id123", "name456", "location789");
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_selected_ion_m_z, "666", MS_m_z));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


void testFileDescription()
{
    FileDescription a;
    a.fileContent.cvParams.push_back(MS_MSn_spectrum);

    SourceFilePtr sf(new SourceFile("1", "tiny1.RAW", "file://F:/data/Exp01"));
    sf->cvParams.push_back(MS_Thermo_RAW_format);
    sf->cvParams.push_back(MS_SHA_1);
    a.sourceFilePtrs.push_back(sf);

    Contact contact;
    contact.cvParams.push_back(CVParam(MS_contact_name, "Darren"));
    a.contacts.push_back(contact);

    testObject(a);
}


void testSample()
{
    Sample a("id123", "name456");
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_selected_ion_m_z, "666", MS_m_z));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


void testComponent()
{
    Component a(ComponentType_Source, 1);
    a.userParams.push_back(UserParam("goober", "goo", "peanuts"));
    a.cvParams.push_back(CVParam(MS_ionization_type, "420"));
    a.cvParams.push_back(CVParam(MS_selected_ion_m_z, "666", MS_m_z));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("pgp")));
    testObject(a);
}


void testComponentList()
{
    ComponentList a;
    a.push_back(Component(MS_nanoelectrospray, 1));
    a.push_back(Component(MS_quadrupole_ion_trap, 2));
    a.push_back(Component(MS_electron_multiplier, 3));
    testObject(a);
}


void testSoftware()
{
    Software a;
    a.id = "goober";
    a.set(MS_ionization_type);
    a.version = "4.20";
    testObject(a);
}


void testInstrumentConfiguration()
{
    InstrumentConfiguration a;
    a.id = "LCQ Deca";
    a.cvParams.push_back(MS_LCQ_Deca);
    a.cvParams.push_back(CVParam(MS_instrument_serial_number, 23433));
    a.componentList.push_back(Component(MS_nanoelectrospray, 1));
    a.componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
    a.componentList.push_back(Component(MS_electron_multiplier, 3));
    a.softwarePtr = SoftwarePtr(new Software("XCalibur"));
    testObject(a);
}


void testProcessingMethod()
{
    ProcessingMethod a;
    a.order = 420;
    a.cvParams.push_back(CVParam(MS_deisotoping, false));
    a.cvParams.push_back(CVParam(MS_charge_deconvolution, false));
    a.cvParams.push_back(CVParam(MS_peak_picking, true));
    a.softwarePtr = SoftwarePtr(new Software("pwiz"));
    testObject(a);
}


void testDataProcessing()
{
    DataProcessing a;

    a.id = "msdata processing";

    ProcessingMethod pm1, pm2;

    pm1.order = 420;
    pm1.cvParams.push_back(CVParam(MS_deisotoping, false));
    pm1.cvParams.push_back(CVParam(MS_charge_deconvolution, false));
    pm1.cvParams.push_back(CVParam(MS_peak_picking, true));
    pm1.softwarePtr = SoftwarePtr(new Software("msdata"));

    pm2.order = 421;
    pm2.userParams.push_back(UserParam("testing"));

    a.processingMethods.push_back(pm1);
    a.processingMethods.push_back(pm2);

    testObject(a);
}


void testScanSettings()
{
    ScanSettings a;

    a.id = "as1";

    Target t1, t2;

    t1.set(MS_selected_ion_m_z, 200);
    t2.userParams.push_back(UserParam("testing"));

    a.targets.push_back(t1);
    a.targets.push_back(t2);

    a.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf1")));
    a.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("sf2")));

    testObject(a);
}


void testPrecursor()
{
    Precursor a;

    a.spectrumID = "scan=19";
    a.isolationWindow.set(MS_isolation_window_target_m_z, 123456, MS_m_z);
    a.isolationWindow.set(MS_isolation_window_lower_offset, 2, MS_m_z);
    a.isolationWindow.set(MS_isolation_window_upper_offset, 3, MS_m_z);
    a.selectedIons.resize(2);
    a.selectedIons[0].set(MS_selected_ion_m_z, 445.34, MS_m_z);
    a.selectedIons[1].set(MS_charge_state, 2);
    a.activation.set(MS_collision_induced_dissociation);
    a.activation.set(MS_collision_energy, 35.00);

    testObject(a);

    // TODO: fix this to test mzML 1.0 precursors;
    // (requires fixing the framework to support testing different schema versions)
}


void testProduct()
{
    Product a;

    a.isolationWindow.set(MS_isolation_window_target_m_z, 123456, MS_m_z);
    a.isolationWindow.set(MS_isolation_window_lower_offset, 2, MS_m_z);
    a.isolationWindow.set(MS_isolation_window_upper_offset, 3, MS_m_z);

    testObject(a);
}


void testScan()
{
    Scan a;

    a.instrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("LTQ FT"));
    a.paramGroupPtrs.push_back(ParamGroupPtr(new ParamGroup("CommonMS1SpectrumParams")));
    a.cvParams.push_back(CVParam(MS_scan_start_time, 5.890500, UO_minute));
    a.cvParams.push_back(CVParam(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]"));
    a.scanWindows.push_back(ScanWindow(400.0, 1800.0, MS_m_z));

    MSData dummy;
    testObjectWithMSData(a, dummy);
}


void testScanList()
{
    ScanList a;
    a.cvParams.push_back(MS_sum_of_spectra);

    Scan a1;
    a1.cvParams.push_back(MS_reflectron_on);

    Scan a2;
    a1.cvParams.push_back(MS_reflectron_off);

    a.scans.push_back(a1);
    a.scans.push_back(a2);

    MSData dummy;
    testObjectWithMSData(a, dummy);
}


void testBinaryDataArray(const BinaryDataEncoder::Config& config)
{
    if (os_) *os_ << "testBinaryDataArray():\n";

    BinaryDataArray a;
    IntegerDataArray aInt;
    for (int i=0; i<10; i++) a.data.push_back(i);
    for (int i=0; i<10; i++) aInt.data.push_back(i);
    a.dataProcessingPtr = aInt.dataProcessingPtr = DataProcessingPtr(new DataProcessing("msdata"));

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a, config);
    if (os_) *os_ << oss.str() << endl;

    ostringstream ossInt;
    XMLWriter writerInt(ossInt);
    IO::write(writerInt, aInt, config);
    if (os_) *os_ << ossInt.str() << endl;

    // read 'b' in from stream

    vector<BinaryDataArrayPtr> binaryDataArrayPtrs;
    vector<IntegerDataArrayPtr> integerDataArrayPtrs;

    istringstream iss(oss.str());
    IO::read(iss, binaryDataArrayPtrs, integerDataArrayPtrs);

    unit_assert_operator_equal(1, binaryDataArrayPtrs.size());
    unit_assert_operator_equal(0, integerDataArrayPtrs.size());

    istringstream issInt(ossInt.str());
    IO::read(issInt, binaryDataArrayPtrs, integerDataArrayPtrs);

    unit_assert_operator_equal(1, binaryDataArrayPtrs.size());
    unit_assert_operator_equal(1, integerDataArrayPtrs.size());

    // compare 'a' and 'b'

    Diff<BinaryDataArray> diff(a, *binaryDataArrayPtrs.back());
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    Diff<IntegerDataArray> diffInt(aInt, *integerDataArrayPtrs.back());
    if (diffInt && os_) *os_ << "diffInt:\n" << diffInt << endl;
    unit_assert(!diffInt);
}


void testBinaryDataArray()
{
    BinaryDataEncoder::Config config;

    config.precision = BinaryDataEncoder::Precision_32;
    config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
    testBinaryDataArray(config);

    config.precision = BinaryDataEncoder::Precision_64;
    config.byteOrder = BinaryDataEncoder::ByteOrder_LittleEndian;
    testBinaryDataArray(config);

    //config.precision = BinaryDataEncoder::Precision_64;
    //config.compression = BinaryDataEncoder::Compression_Zlib;
    //testBinaryDataArray(config);
}


const char* bdaWithExternalMetadata = "\
<binaryDataArray encodedLength=\"160\" arrayLength=\"15\"> \
    <referenceableParamGroupRef ref=\"mz_params\"/> \
    <binary>AAAAAAAAAAAAAAAAAADwPwAAAAAAAABAAAAAAAAACEAAAAAAAAAQQAAAAAAAABRAAAAAAAAAGEAAAAAAAAAcQAAAAAAAACBAAAAAAAAAIkAAAAAAAAAkQAAAAAAAACZAAAAAAAAAKEAAAAAAAAAqQAAAAAAAACxA</binary> \
</binaryDataArray>";


void testBinaryDataArrayExternalMetadata()
{
    // instantiate an MSData object with the binary array metadata held in a ParamGroup

    MSData msd;
    ParamGroupPtr pg(new ParamGroup);
    pg->id = "mz_params";
    pg->cvParams.push_back(MS_m_z_array);
    pg->cvParams.push_back(MS_64_bit_float);
    pg->cvParams.push_back(MS_no_compression);
    msd.paramGroupPtrs.push_back(pg);

    istringstream is(bdaWithExternalMetadata);
    vector<BinaryDataArrayPtr> binaryDataArrayPtrs;
    vector<IntegerDataArrayPtr> integerDataArrayPtrs;

    // test read with MSData reference

    IO::read(is, binaryDataArrayPtrs, integerDataArrayPtrs, &msd);
    auto& bda = *binaryDataArrayPtrs.back();

    unit_assert(bda.data.size() == 15);
    for (size_t i=0; i<15; i++)
        unit_assert(bda.data[i] == i);
}

const char *bdaMztWithShuffledZstdCompression = "<binaryDataArray encodedLength=\"3892\" arrayLength=\"1006\"> \
              <cvParam cvRef=\"MS\" accession=\"MS:1000523\" name=\"64-bit float\" value=\"\"/> \
              <cvParam cvRef=\"MS\" accession=\"MS:1003781\" name=\"byte-shuffled zstd compression\" value=\"\"/> \
              <cvParam cvRef=\"MS\" accession=\"MS:1000514\" name=\"m/z array\" value=\"\" unitCvRef=\"MS\" unitAccession=\"MS:1000040\" unitName=\"m/z\"/> \
              <binary>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</binary> \
            </binaryDataArray>";

const char *bdaIntensityZstdCompression = "<binaryDataArray encodedLength=\"4972\" arrayLength=\"1006\"> \
              <cvParam cvRef=\"MS\" accession=\"MS:1000521\" name=\"32-bit float\" value=\"\"/> \
              <cvParam cvRef=\"MS\" accession=\"MS:1003780\" name=\"zstd compression\" value=\"\"/> \
              <cvParam cvRef=\"MS\" accession=\"MS:1000515\" name=\"intensity array\" value=\"\" unitCvRef=\"MS\" unitAccession=\"MS:1000131\" unitName=\"number of detector counts\"/> \
              <binary>KLUv/WC4Di10AIr7ADo4EBjtKXTBtjtkBv7UV7KogZ0B3Iwueol8bllJBhaALtqGL7mkaxUZG9kySc1OxWZKEKyuUk9CDAaLA5gDlANkfD3kmdcIoPAcSu7MiwDpHPTI5zdJxP0Anuebi4HWYGrVllRa2I4OAU4pGthtCyjQVyhiX9d31h7GGGwV2XhuJbS+8VNbmj7DI+FCBfpp1T9oWoLpjc5ysl7QVMD7pE7DDzt4nasFlQMpJGukMjf6RtZVQ4otuVN0GN0gbtwuJqqr3VInP+eS8JJxyXFj9NhrHBqK7tEDWwOJGaexSeE77OB0EyItF8x1R5txXfYCuLt3Lvr5qFY/SBWgZ3GouOQUxoP+r5FI2rlln9lD0xB5FEnYBxqB/KcZKj7DgAEvw6NGb7H2Zp5CcJcsxjcwVMJrrGz9qnHGycgg6MIMihdHkcHrVTq+rI3cu6jY8UMLTH8QZ6uhKsxdrkkGLoZYOF3FddhGWOJuAQnsWjbI3BAx8m5nWjqQi++GnZp+Beodq1oovZCap55KROo2FvJeBhO1wcKEesiIEC8SWeI+PzicuRrYc5mgTaWx6lNE+v6Er5HnliTeg1uQQ0F+npIBJbc6wuU1rhzGoqfsaLYkeWk/MuNevwvlk176EIGpRHnkTwWZrxoVvJbLu46iaNqBHJxacMhpn20+uGyMiH/CgIi/Qcby1gQL7CNTxNVArf7CiKoBEFKns2lfLubM6kOQMDzSYp8HGlq12mZad3p5NWOQym6qVOsPgpv9JQhNn5Zm4Tx4Zu+DtOVFjkNHkvraCsqYvsJQlnm8YXLORDfvEil2K05iHZjjx5W7dPA9Ss2wfMjcyw2hV5Ai04ExWu8Ql+1RBWHfx8WT68Cb8kyuOq0BrkFbXi2ehIf7pjRGWghrhk7Qp/FYXIg5D7umPjAE+yU9xxt2kTuGsukm16amSrpjtBzbf6VC3Ybe49lw2UXhVqYj9FBxJRifP7JGxJUgurgxCQVGBILXKbRBPCMGlPNJQv5vks4bLI5+DQhEIw6h4gDOEPC2LzrtAmUFXEjp9UtbOH50Z8mrqLq4jQdHruE147AHXnmOiwRWg8riSjRatXTMdODUAurcDA/c5/hU6QxfyNwmKUawmh0pP7DdwWk9xoCtVKxl0sSRtChzE11VTqp97Q9v7hhJDAAMKEqnPc1T5H5CbkwoB4y3UDnBPyTrtKiZcaw44YDjgSGl04SYLiJXDV6EUoWJJFBqrjSJB+o7YyMrSjyL1Qoe8jPVqimT+iKenM+TRHPLryutAgtkcpbNjz33LlnDmCsBHQYMYBeF1fV1ZbpSaoQuU5DEe6GN7s6NuOpULlraqM1Uo9Ep0X1nPFwDNsj5auvXuRNPG3nxa64DqjxTioe7Rvq4FNCkXUYFZE1JidlneUZ31JJRBgfMWtlpAmQajRGsv2To+mVma8eAQsZIdEjuNES7PeKhRJnUpNOb5MHoDVg+XCclRq6ZElsfJmHTC4CN2X4ZwdwykDOjzeLMrtZJWhmRBrKyIjdX+rILQstpnHElByhtFg4YvxcawE1Dy26J5jJss1gO6S3NLH9TmKajVB/8Ds5Py2UhrxO5N1sqPn+uhQl1urNUsB1LFdvvBOeVAgbGb3O0WkpPiR49+vocqH7oYJgO8SFMa+h607EbBHjjjrz+8Vflsiao3DKpS2oMXtwKQCnbiZWxOwgNeQ4GYlqPGcObI+psW3e3uG0bDdFnccqaiIjow6Kc9mCsl6uJKbZtQABgD3Lu3TH2zy2D0K7QJdZ2SezvPuGt3ToJMxpD55F+stPZT20lOokZ31UixcR1xeLzQVHDJ84hbMkEXS0DNPVmpmC2oZDCVsUz2ZxOCly9JqWHIAnlZ+5kr1gSw7dNzrWTZV3beaHTk0Zb7rjk8GFgQGmltr87VJimQ8nC3iHUstGyY1jOXhi3GoK5Ec0uzYRS6t4hJdxSclbDdVHiJpuEHCTxwrMc/TyJULQPsaTdqdHQ25Sa1H5faLQArQB8LfvlaoMbToHC3hEVDNBUyvzu35PqFoLX6jWzxHvl6N1R9CBxSsiqZku10UhUaN0gmEEuhSFNDarY4ppoNHrSBGt/vXLey1NY53ECasEDTO6UB9iGAtvhGokiPoTolzOJsT0PLaeXgVvyO0Zbmofnk2u0R7WVkCb2mSIW7WmCs6Vw3PWUvjs9tRmjP5kM2/NHm/6yensKJzfO2qiDpdRhOqkmle5wsG6NUHpGMTd+GgrjdkOBBzXy/N4WLPdju3E3BnA5kTwGnacIxU3S+h0KjdcOwhvRQE9od8LErQ+lZN44C0a2bByh7t9luteXu4vaMLtg/DjLEKCU0JmDaLdUULCHCltZMbH0L3woeSYaXyKmamXZMcEbATDyVBLA7B17OtuJCO5Ka9QuJG7orkXpuVN4vi4VFKVdp8JbS7F5bKIjEN54RavH9BJcsHJEm12Y0ENWsw1kaUWjjYKdxRwR/2xz7SR85O5TGrp+AinXjk2RH1GU9BxCREdRSfcEJfj0AKZz7houZNzHVbYH4HvRnzQOL1AfrAai7HznhjNPzULAb3d8jxu94kuxP2djtP3dGOfPXEb4HpcmDGZuzJkQt00EQkc/Pui7jQ1L7gWF9VZCyHhMFtgVRNW4CCCiDzrmMZwqyPeoqsAnwhixGLcwbZ6lEq86I9NOYVt6zJTvnUt12jmkT7x4ecmBHYRYyRLjP13Q4dDXNEa9qE9sQuQeTJi8xoAefAikd4LY9yA5u1cdQTIbPFPWg3Jk1EY+DENLkVaVAp8LZfnDQFDU4as82SswkeKwmyC6a4gUbRoZYNwolT2hkNerEYJ1xC4UnrkNYsvAbAetFtBgTtlukgg+jXf5dQcRRF4oRHdaikLTqwbZ/qf25XhaCjwsTwVjEDVjL3ewORswvAog2Nu+lR01qZPnSbUg0K+nNO4XmiMtRjHtf5GtrKbzkqncyV6wALa5MrS9haWOo5HVuF1yYtzCyKHbp219eAXNh1AK8DdEkFrFo5qrVjIyAkMEtXNSxGDMOa4esqNNT2GT6zyuy66aStJ+nQO0FZier5JB6KM47U3cyNF2ikucBq+SpWRVv4BHW0d1OKBDbcx5lAdKLkMF3CnZaHzMmBnvWoU7EAmfnxxy86o+HJyqtfkFRR0YzicH7ZowXi8CnHI9ka49Aq/GCwEl3DEWz4sQbXmFEijtaxgZI5jR4hia7h7FgCyunHShJcW0/BTjGJsp4IAOEjX8EV+Ue2V9YMA9Y4dy63EgJktmfaPBV3Q0uI7SKF6gQBFWS5NrVjUQNwuUyW2I7J22AbrFSJY0b6bY5UYRILlJkiOHNl6mzFOGe+iNcizg63VoSl1phoxLJ2LFNshoVJmCJLcrxXd444BEuVs/vNzFKiFPhdGymS83vizbww1OwP7DHN6R8PmdiRW9nkAk2aoYqDQEyzZ95O13EdqH43xpXolPAy30heAWQCHITTPFRB/pie04N07vW2xWC31GaB+nel1Htsp+2rx/kGLjUUuwbngsnyCGzR9kPvm2VfBobU+PdGvRQOKsuFs8dQ+VklpND4R+UmJ5zRcNNJKVz5YjYA2vYA3BUw4EculRIntCol0B1Ql2E4TBcdY8sQA9mLgUEXTrJ7OtDDFBZDsZLfIGILXwjcKlb01SuAocTs62h2gryCW2deKgzZ1QKtVmSOu2SYGP26nRJ7cKlD8XDepBKzhLcAnXCrvJlYqeS4DL/bGmb+sYROa+OHEYrGRLAuc5FeIUWGQMV+SH0GSMkR6IYDI3itrVATLwWR8WQ43ZZEEipcyKdFpXOjIc+4IFX9Xo5OyGtlNFtWEwd3gw3AMmzqpc5B6Lf1+c4uS9iMakoQfwJooJr3CB7YqqLBjzTJdznrwPumI4YR85XEXMiRMI+fhblAecaPXKAngu4C0Pddo7QW/YkgYDbnHjAwtpYR2IsO0Orvx5Vqwmf7FzWS5PG4aBhYLBHu/4rg1y6hE1HDWIyalZCm/35YFD13QeQ+YchsWiQxvnFjEqAya+YKfK+zoRNAnIbPMQwoLjWM0YSRzTazJB/SkHMJ8qcdkLUJl6jwsk1yoRC5exEMpofuzdx1rZW1jR+pHRwx9GHsCbek/4zZMZbTTDzlMdxVhEIgLW4IfKVX7aHmESxNXMRlzSTeLl0gJgCFrysKYXJmsJCRnoSQorLblM5Ub1Mzc2BxJHd8ALpI/KRGIze3b8KVnESXaytHceQRmB0D7a0iNCZpIAJysJWfIWVo/NPCu1pn4sOcpW6A0CaPAdTUaPZL3WjgXOP5K4/KeO1Auv1JzR9MonBPl62WYqi7Gi4FsG+P7DCSwnpuRNiWW86mTVTHRGms+FnsdFRh1LzMFPgAK5mM9UHgTtcAgn+nhMKxXPIfoxB1OkL7GuaeeomXIHp0BsxESSB+JY7KBAtudJymw41xRfsKX3RBmr37G4hI+ggfNIGGu4xGMFntTzcyUZW94GcjgTP7E+0svkNU/KMBKtrhOAx88tS8u+wMjBCxGYMqnjK3NoBHyJIxn8V4jAwXjQ+ZTmIyPmUMGepG8fyyodi3KN7XSeXYeWHTuatfJZ0CMbgRGEr6bAPClV839+aexHj4Z/WEDHjBroMKiqHpcpvcOYSFtvIKjmDiSQ4TZZJYYzAUvrSjT6Ay5ata+TQzhSi4iJjjbZaUv7JFEzH1EhAjsicOIxw6MrNdmjrVMhiP0YmbGoEyotmWPyVq7BbZeIsDIMVRJa2BeorGVMKl/A8MZbvLp+5EGTDx/7oqojPnUi9Uubjk0J2XQ11WKvFRlSzaHr7wkGAeDYxNaH2SUxiObiGysM/4XDW++5CuDymTh6A4UWeqyEi74XsrrOn921a5LmX65mnreXPdTrWB9+kHDFVHFtrcZC+4QDyaZDwkgvIQN7An0mDgLGhVv549RBpGHN9Lb90urVVV9Bj+cty12E7pxBAA==</binary> \
            </binaryDataArray>";

void testBinaryArraysWithZstd()
{

    MSData msd;
    istringstream is(bdaMztWithShuffledZstdCompression);
    vector<BinaryDataArrayPtr> binaryDataArrayPtrs;
    vector<IntegerDataArrayPtr> integerDataArrayPtrs;

    // test read with byte-shuffled zstd

    IO::read(is, binaryDataArrayPtrs, integerDataArrayPtrs, &msd);
    auto &bda = *binaryDataArrayPtrs.back();
    unit_assert(bda.data.size() == 1006);

    unit_assert(std::abs(bda.data.front() - 236.047) < 1e-2);
    unit_assert(std::abs(bda.data.back() - 1636.43) < 1e-2);

    is = istringstream(bdaIntensityZstdCompression);

    // test read with zstd
    IO::read(is, binaryDataArrayPtrs, integerDataArrayPtrs, &msd);
    bda = *binaryDataArrayPtrs.back();

    unit_assert(bda.data.size() == 1006);

    unit_assert(std::abs(bda.data.front() - 11.6745) < 1e-2);
    unit_assert(std::abs(bda.data.back() - 19.6446) < 1e-2);
}

void testSpectrum()
{
    if (os_) *os_ << "testSpectrum():\n";

    Spectrum a;

    a.index = 123;
    a.id = "goo";
    a.defaultArrayLength = 666;
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("dp"));
    a.sourceFilePtr = SourceFilePtr(new SourceFile("sf"));
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    for (size_t i=0; i<a.defaultArrayLength; i++)
        a.binaryDataArrayPtrs.back()->data.push_back(i);
    a.binaryDataArrayPtrs.back()->set(MS_m_z_array);
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    for (size_t i=0; i<a.defaultArrayLength; i++)
        a.binaryDataArrayPtrs.back()->data.push_back(i*2);
    a.binaryDataArrayPtrs.back()->set(MS_intensity_array);
    a.cvParams.push_back(MS_reflectron_on);
    a.cvParams.push_back(MS_MSn_spectrum);

    a.precursors.push_back(Precursor());
    a.precursors.back().spectrumID = "19";
    a.precursors.back().selectedIons.resize(1);
    a.precursors.back().selectedIons[0].set(MS_selected_ion_m_z, 445.34, MS_m_z);
    a.precursors.back().selectedIons[0].set(MS_charge_state, 2);
    a.precursors.back().activation.set(MS_collision_induced_dissociation);
    a.precursors.back().activation.set(MS_collision_energy, 35.00, UO_electronvolt);

    a.products.push_back(Product());
    a.products.back().isolationWindow.set(MS_ionization_type, "420");

    a.scanList.scans.push_back(Scan());
    Scan& scan = a.scanList.scans.back();
    scan.set(MS_scan_start_time, 4.20);
    scan.set(MS_filter_string, "doobie");

    a.scanList.scans.push_back(Scan());
    Scan& scan2 = a.scanList.scans.back();
    scan2.set(MS_scan_start_time, 4.21);
    scan2.set(MS_filter_string, "doo");

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    MSData dummy;
    IO::write(writer, a, dummy);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    Spectrum b;
    istringstream iss(oss.str());
    IO::read(iss, b, IO::ReadBinaryData);
    unit_assert(b.sourceFilePosition == 0); // not -1

    // compare 'a' and 'b'

    Diff<Spectrum, DiffConfig> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    // test IgnoreBinaryData

    Spectrum c;
    iss.seekg(0);
    IO::read(iss, c); // default = IgnoreBinaryData
    unit_assert(c.binaryDataArrayPtrs[0]->data.empty());
    unit_assert(c.sourceFilePosition == 0); // not -1

    for (auto& bda : a.binaryDataArrayPtrs)
        bda->data.clear();
    diff(a, c);
    unit_assert(!diff);
}


void testChromatogram()
{
    if (os_) *os_ << "testChromatogram():\n";

    Chromatogram a;

    a.index = 123;
    a.id = "goo";
    a.defaultArrayLength = 666;
    a.dataProcessingPtr = DataProcessingPtr(new DataProcessing("dp"));
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    for (size_t i=0; i<a.defaultArrayLength; i++)
        a.binaryDataArrayPtrs.back()->data.push_back(i);
    a.binaryDataArrayPtrs.back()->set(MS_time_array);
    a.binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
    for (size_t i=0; i<a.defaultArrayLength; i++)
        a.binaryDataArrayPtrs.back()->data.push_back(i*2);
    a.binaryDataArrayPtrs.back()->set(MS_intensity_array);
    a.cvParams.push_back(MS_total_ion_current_chromatogram); // TODO: fix when CV has appropriate terms

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream

    Chromatogram b;
    istringstream iss(oss.str());
    IO::read(iss, b, IO::ReadBinaryData);
    unit_assert(b.sourceFilePosition == 0); // not -1

    // compare 'a' and 'b'

    Diff<Chromatogram, DiffConfig> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    // test IgnoreBinaryData

    Chromatogram c;
    iss.seekg(0);
    IO::read(iss, c); // default = IgnoreBinaryData
    unit_assert(c.binaryDataArrayPtrs[0]->data.empty());
    unit_assert(c.sourceFilePosition == 0); // not -1

    for (auto& bda : a.binaryDataArrayPtrs)
        bda->data.clear();
    diff(a, c);
    unit_assert(!diff);
}


void testSpectrumList()
{
    SpectrumListSimple a;

    SpectrumPtr spectrum1(new Spectrum);
    spectrum1->id = "goober";
    spectrum1->index = 0;
    spectrum1->defaultArrayLength = 666;
    spectrum1->userParams.push_back(UserParam("description1"));

    SpectrumPtr spectrum2(new Spectrum);
    spectrum2->id = "raisinet";
    spectrum2->index = 1;
    spectrum2->defaultArrayLength = 667;
    spectrum2->userParams.push_back(UserParam("description2"));

    a.spectra.push_back(spectrum1);
    a.spectra.push_back(spectrum2);
    a.dp = DataProcessingPtr(new DataProcessing("dp"));

    testObject_SpectrumList(a);
}


void testSpectrumListWithPositions()
{
    if (os_) *os_ << "testSpectrumListWithPositions()\n  ";

    SpectrumListSimple a;

    SpectrumPtr spectrum1(new Spectrum);
    spectrum1->id = "goober";
    spectrum1->index = 0;
    spectrum1->defaultArrayLength = 666;
    spectrum1->userParams.push_back(UserParam("description1"));

    SpectrumPtr spectrum2(new Spectrum);
    spectrum2->id = "raisinet";
    spectrum2->index = 1;
    spectrum2->defaultArrayLength = 667;
    spectrum2->userParams.push_back(UserParam("description2"));

    a.spectra.push_back(spectrum1);
    a.spectra.push_back(spectrum2);

    ostringstream oss;
    XMLWriter writer(oss);
    vector<stream_offset> positions;
    MSData dummy;
    IO::write(writer, a, dummy, BinaryDataEncoder::Config(), &positions);

    if (os_)
    {
        copy(positions.begin(), positions.end(), ostream_iterator<stream_offset>(*os_, " "));
        *os_ << endl << oss.str() << endl;
        *os_ << "\n\n";
    }

    unit_assert(positions.size() == 2);
    unit_assert(positions[0] == 27);
    unit_assert(positions[1] == 179);
}


class TestIterationListener : public IterationListener
{
    public:

    virtual Status update(const UpdateMessage& updateMessage)
    {
        indices_.push_back(updateMessage.iterationIndex);
        return Status_Ok;
    }

    const vector<size_t>& indices() const {return indices_;}

    private:
    vector<size_t> indices_;
};


class TestIterationListener_WithCancel : public IterationListener
{
    public:

    virtual Status update(const UpdateMessage& updateMessage)
    {
        if (updateMessage.iterationIndex == 5) return Status_Cancel;
        indices_.push_back(updateMessage.iterationIndex);
        return Status_Ok;
    }

    const vector<size_t>& indices() const {return indices_;}

    private:
    vector<size_t> indices_;
};


void testSpectrumListWriteProgress()
{
    if (os_) *os_ << "testSpectrumListWriteProgress()\n  ";

    SpectrumListSimple a;

    for (size_t i=0; i<11; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        spectrum->id = "goober_" + lexical_cast<string>(i);
        spectrum->index = i;
        spectrum->defaultArrayLength = 666;
        a.spectra.push_back(spectrum);
    }

    ostringstream oss;
    XMLWriter writer(oss);

    IterationListenerPtr listenerPtr(new TestIterationListener);
    TestIterationListener& listener = *boost::static_pointer_cast<TestIterationListener>(listenerPtr);
    IterationListenerRegistry registry;
    registry.addListener(listenerPtr, 3); // callbacks: 0,2,5,8,10

    MSData dummy;
    IO::write(writer, a, dummy, BinaryDataEncoder::Config(), 0, &registry);

    if (os_)
    {
        *os_ << "callback indices: ";
        copy(listener.indices().begin(), listener.indices().end(),
             ostream_iterator<size_t>(*os_, " "));
        *os_ << "\n\n";
    }

    unit_assert(listener.indices().size() == 5);
    unit_assert(listener.indices()[0] == 0);
    unit_assert(listener.indices()[1] == 2);
    unit_assert(listener.indices()[2] == 5);
    unit_assert(listener.indices()[3] == 8);
    unit_assert(listener.indices()[4] == 10);

    // test #2, this time with cancel at index 6

    IterationListenerPtr cancelListenerPtr(new TestIterationListener_WithCancel);
    TestIterationListener_WithCancel& cancelListener = *boost::static_pointer_cast<TestIterationListener_WithCancel>(cancelListenerPtr);
    IterationListenerRegistry registry2;
    registry2.addListener(cancelListenerPtr, 3); // callbacks: 0,2, cancel at 5

    ostringstream oss2;
    XMLWriter writer2(oss2);
    IO::write(writer2, a, dummy, BinaryDataEncoder::Config(), 0, &registry2);

    if (os_)
    {
        *os_ << "callback indices: ";
        copy(cancelListener.indices().begin(), cancelListener.indices().end(),
             ostream_iterator<size_t>(*os_, " "));
        *os_ << "\n\n";
    }

    unit_assert(cancelListener.indices().size() == 2);
    unit_assert(cancelListener.indices()[0] == 0);
    unit_assert(cancelListener.indices()[1] == 2);
}


void testChromatogramList()
{
    ChromatogramListSimple a;

    ChromatogramPtr chromatogram1(new Chromatogram);
    chromatogram1->id = "goober";
    chromatogram1->index = 0;
    chromatogram1->defaultArrayLength = 666;

    ChromatogramPtr chromatogram2(new Chromatogram);
    chromatogram2->id = "raisinet";
    chromatogram2->index = 1;
    chromatogram2->defaultArrayLength = 667;

    a.chromatograms.push_back(chromatogram1);
    a.chromatograms.push_back(chromatogram2);
    a.dp = DataProcessingPtr(new DataProcessing("dp"));

    testObject_ChromatogramList(a);
}


void testChromatogramListWithPositions()
{
    if (os_) *os_ << "testChromatogramListWithPositions()\n  ";

    ChromatogramListSimple a;

    ChromatogramPtr chromatogram1(new Chromatogram);
    chromatogram1->id = "goober";
    chromatogram1->index = 0;
    chromatogram1->defaultArrayLength = 666;

    ChromatogramPtr chromatogram2(new Chromatogram);
    chromatogram2->id = "raisinet";
    chromatogram2->index = 1;
    chromatogram2->defaultArrayLength = 667;

    a.chromatograms.push_back(chromatogram1);
    a.chromatograms.push_back(chromatogram2);

    ostringstream oss;
    XMLWriter writer(oss);
    vector<stream_offset> positions;
    IO::write(writer, a, BinaryDataEncoder::Config(), &positions);

    if (os_)
    {
        copy(positions.begin(), positions.end(), ostream_iterator<stream_offset>(*os_, " "));
        *os_ << endl << oss.str() << endl;
        *os_ << "\n\n";
    }

    unit_assert(positions.size() == 2);
    unit_assert(positions[0] == 31);
    unit_assert(positions[1] == 113);
}


void testRun()
{
    if (os_) *os_ << "testRun():\n";

    Run a;

    a.id = "goober";
    a.defaultInstrumentConfigurationPtr = InstrumentConfigurationPtr(new InstrumentConfiguration("instrumentConfiguration"));
    a.samplePtr = SamplePtr(new Sample("sample"));
    a.startTimeStamp = "20 April 2004 4:20pm";
    a.defaultSourceFilePtr = SourceFilePtr(new SourceFile("sf1"));

    // spectrumList

    shared_ptr<SpectrumListSimple> spectrumListSimple(new SpectrumListSimple);

    SpectrumPtr spectrum1(new Spectrum);
    spectrum1->id = "goober";
    spectrum1->index = 0;
    spectrum1->defaultArrayLength = 666;
    spectrum1->userParams.push_back(UserParam("description1"));

    SpectrumPtr spectrum2(new Spectrum);
    spectrum2->id = "raisinet";
    spectrum2->index = 1;
    spectrum2->defaultArrayLength = 667;
    spectrum2->userParams.push_back(UserParam("description2"));

    spectrumListSimple->spectra.push_back(spectrum1);
    spectrumListSimple->spectra.push_back(spectrum2);

    a.spectrumListPtr = spectrumListSimple;

    // chromatogramList

    shared_ptr<ChromatogramListSimple> chromatogramListSimple(new ChromatogramListSimple);

    ChromatogramPtr chromatogram1(new Chromatogram);
    chromatogram1->id = "goober";
    chromatogram1->index = 0;
    chromatogram1->defaultArrayLength = 666;

    ChromatogramPtr chromatogram2(new Chromatogram);
    chromatogram2->id = "raisinet";
    chromatogram2->index = 1;
    chromatogram2->defaultArrayLength = 667;

    chromatogramListSimple->chromatograms.push_back(chromatogram1);
    chromatogramListSimple->chromatograms.push_back(chromatogram2);

    a.chromatogramListPtr = chromatogramListSimple;

    // write 'a' out to a stream

    MSData dummy;

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a, dummy);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream, ignoring SpectrumList (default)

    Run b;
    istringstream iss(oss.str());
    IO::read(iss, b, IO::IgnoreSpectrumList); // IO::IgnoreSpectrumList

    // compare 'a' and 'b'

    Diff<Run, DiffConfig> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.spectrumListPtr.get());
    unit_assert(diff.a_b.spectrumListPtr->size() == 1);
    unit_assert(diff.a_b.spectrumListPtr->spectrum(0)->userParams.size() == 1);

    // read 'c' in from stream, reading SpectrumList

    Run c;
    iss.seekg(0);
    IO::read(iss, c, IO::ReadSpectrumList);

    // compare 'a' and 'c'

    diff(a,c);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    // remove SpectrumList and ChromatogramList from a, and compare to b

    a.spectrumListPtr.reset();
    a.chromatogramListPtr.reset();
    diff(a, b);
    unit_assert(!diff);
}


void initializeTestData(MSData& msd)
{
    msd.accession = "test accession";
    msd.id = "test id";

    // cvList

    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "http://psidev.sourceforge.net/ms/xml/mzdata/psi-ms.2.0.2.obo";
    cv.id = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "2.0.2";

    // fileDescription

    FileContent& fc = msd.fileDescription.fileContent;
    fc.cvParams.push_back(MS_MSn_spectrum);
    fc.userParams.push_back(UserParam("number of cats", "4"));

    SourceFilePtr sfp(new SourceFile);
    sfp->id = "1";
    sfp->name = "tiny1.RAW";
    sfp->location = "file://F:/data/Exp01";
    sfp->cvParams.push_back(MS_Thermo_RAW_format);
    sfp->cvParams.push_back(CVParam(MS_SHA_1,"71be39fb2700ab2f3c8b2234b91274968b6899b1"));
    msd.fileDescription.sourceFilePtrs.push_back(sfp);

    SourceFilePtr sfp_parameters(new SourceFile("sf_parameters", "parameters.par", "file:///C:/settings/"));
    msd.fileDescription.sourceFilePtrs.push_back(sfp_parameters);

    msd.fileDescription.contacts.resize(1);
    Contact& contact = msd.fileDescription.contacts.front();
    contact.cvParams.push_back(CVParam(MS_contact_name, "William Pennington"));
    contact.cvParams.push_back(CVParam(MS_contact_address,
                               "Higglesworth University, 12 Higglesworth Avenue, 12045, HI, USA"));
	contact.cvParams.push_back(CVParam(MS_contact_URL, "http://www.higglesworth.edu/"));
	contact.cvParams.push_back(CVParam(MS_contact_email, "wpennington@higglesworth.edu"));

    // paramGroupList

    ParamGroupPtr pg1(new ParamGroup);
    pg1->id = "CommonMS1SpectrumParams";
    pg1->cvParams.push_back(MS_positive_scan);
    msd.paramGroupPtrs.push_back(pg1);

    ParamGroupPtr pg2(new ParamGroup);
    pg2->id = "CommonMS2SpectrumParams";
    pg2->cvParams.push_back(MS_positive_scan);
    msd.paramGroupPtrs.push_back(pg2);

    // sampleList

    SamplePtr samplePtr(new Sample);
    samplePtr->id = "1";
    samplePtr->name = "Sample1";
    msd.samplePtrs.push_back(samplePtr);

    // instrumentConfigurationList

    InstrumentConfigurationPtr instrumentConfigurationPtr(new InstrumentConfiguration);
    instrumentConfigurationPtr->id = "LCQ Deca";
    instrumentConfigurationPtr->cvParams.push_back(MS_LCQ_Deca);
    instrumentConfigurationPtr->cvParams.push_back(CVParam(MS_instrument_serial_number,"23433"));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_nanoelectrospray, 1));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_quadrupole_ion_trap, 2));
    instrumentConfigurationPtr->componentList.push_back(Component(MS_electron_multiplier, 3));

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->set(MS_Xcalibur);
    softwareXcalibur->version = "2.0.5";
    instrumentConfigurationPtr->softwarePtr = softwareXcalibur;

    msd.instrumentConfigurationPtrs.push_back(instrumentConfigurationPtr);

    // softwareList

    SoftwarePtr softwareBioworks(new Software);
    softwareBioworks->id = "Bioworks";
    softwareBioworks->set(MS_Bioworks);
    softwareBioworks->version = "3.3.1 sp1";

    SoftwarePtr software_pwiz(new Software);
    software_pwiz->id = "pwiz";
    software_pwiz->set(MS_pwiz);
    software_pwiz->version = "1.0";

    msd.softwarePtrs.push_back(softwareBioworks);
    msd.softwarePtrs.push_back(software_pwiz);
    msd.softwarePtrs.push_back(softwareXcalibur);

    // dataProcessingList

    DataProcessingPtr dpXcalibur(new DataProcessing);
    dpXcalibur->id = "Xcalibur Processing";

    ProcessingMethod procXcal;
    procXcal.order = 1;
    procXcal.softwarePtr = softwareXcalibur;
    procXcal.cvParams.push_back(CVParam(MS_deisotoping, false));
    procXcal.cvParams.push_back(CVParam(MS_charge_deconvolution, false));
    procXcal.cvParams.push_back(CVParam(MS_peak_picking, true));

    dpXcalibur->processingMethods.push_back(procXcal);

    DataProcessingPtr dp_msconvert(new DataProcessing);
    dp_msconvert->id = "pwiz conversion";

    ProcessingMethod proc_msconvert;
    proc_msconvert.order = 2;
    proc_msconvert.softwarePtr = software_pwiz;
    proc_msconvert.cvParams.push_back(MS_Conversion_to_mzML);

    dp_msconvert->processingMethods.push_back(proc_msconvert);

    msd.dataProcessingPtrs.push_back(dpXcalibur);
    msd.dataProcessingPtrs.push_back(dp_msconvert);

    ScanSettingsPtr as1(new ScanSettings("as1"));
    as1->sourceFilePtrs.push_back(sfp_parameters);
    Target t1;
    t1.set(MS_m_z, 1000);
    Target t2;
    t2.set(MS_m_z, 1200);
    as1->targets.push_back(t1);
    as1->targets.push_back(t2);
    msd.scanSettingsPtrs.push_back(as1);

    // run

    msd.run.id = "Exp01";
    msd.run.defaultInstrumentConfigurationPtr = instrumentConfigurationPtr;
    msd.run.samplePtr = samplePtr;
    msd.run.startTimeStamp = "2007-06-27T15:23:45.00035";
    msd.run.defaultSourceFilePtr = sfp;

    shared_ptr<SpectrumListSimple> spectrumList(new SpectrumListSimple);
    msd.run.spectrumListPtr = spectrumList;

    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));
    spectrumList->spectra.push_back(SpectrumPtr(new Spectrum));

    Spectrum& s19 = *spectrumList->spectra[0];
    s19.id = "S19";
    s19.index = 0;
    s19.defaultArrayLength = 10;
    s19.cvParams.push_back(MS_MSn_spectrum);
    s19.set(MS_ms_level, 1);
    s19.cvParams.push_back(MS_centroid_spectrum);
    s19.cvParams.push_back(CVParam(MS_lowest_observed_m_z, 400.39));
    s19.cvParams.push_back(CVParam(MS_highest_observed_m_z, 1795.56));
    s19.cvParams.push_back(CVParam(MS_base_peak_m_z, 445.347));
    s19.cvParams.push_back(CVParam(MS_base_peak_intensity, 120053));
    s19.cvParams.push_back(CVParam(MS_total_ion_current, 1.66755e+007));
    s19.scanList.scans.push_back(Scan());
    Scan& s19scan = s19.scanList.scans.back();
    s19scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s19scan.paramGroupPtrs.push_back(pg1);
    s19scan.cvParams.push_back(CVParam(MS_scan_start_time, 5.890500, UO_minute));
    s19scan.cvParams.push_back(CVParam(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]"));
    s19scan.scanWindows.resize(1);
    ScanWindow& window = s19scan.scanWindows.front();
    window.cvParams.push_back(CVParam(MS_scan_window_lower_limit, 400.000000));
    window.cvParams.push_back(CVParam(MS_scan_window_upper_limit, 1800.000000));

    BinaryDataArrayPtr s19_mz(new BinaryDataArray);
    s19_mz->dataProcessingPtr = dpXcalibur;
    s19_mz->cvParams.push_back(MS_m_z_array);
    s19_mz->data.resize(10);
    for (int i=0; i<10; i++)
        s19_mz->data[i] = i;

    BinaryDataArrayPtr s19_intensity(new BinaryDataArray);
    s19_intensity->dataProcessingPtr = dpXcalibur;
    s19_intensity->cvParams.push_back(MS_intensity_array);
    s19_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        s19_intensity->data[i] = 10-i;

    s19.binaryDataArrayPtrs.push_back(s19_mz);
    s19.binaryDataArrayPtrs.push_back(s19_intensity);

    Spectrum& s20 = *spectrumList->spectra[1];
    s20.id = "S20";
    s20.index = 1;
    s20.defaultArrayLength = 10;

    s20.cvParams.push_back(MS_MSn_spectrum);
    s20.set(MS_ms_level, 2);

    s20.cvParams.push_back(MS_centroid_spectrum);
    s20.cvParams.push_back(CVParam(MS_lowest_observed_m_z, 320.39));
    s20.cvParams.push_back(CVParam(MS_highest_observed_m_z, 1003.56));
    s20.cvParams.push_back(CVParam(MS_base_peak_m_z, 456.347));
    s20.cvParams.push_back(CVParam(MS_base_peak_intensity, 23433));
    s20.cvParams.push_back(CVParam(MS_total_ion_current, 1.66755e+007));

    s20.precursors.resize(1);
    Precursor& precursor = s20.precursors.front();
    precursor.spectrumID= s19.id;
    precursor.selectedIons.resize(1);
    precursor.selectedIons[0].cvParams.push_back(CVParam(MS_selected_ion_m_z, 445.34));
    precursor.selectedIons[0].cvParams.push_back(CVParam(MS_charge_state, 2));
    precursor.activation.cvParams.push_back(MS_collision_induced_dissociation);
    precursor.activation.cvParams.push_back(CVParam(MS_collision_energy, 35.00, UO_electronvolt));

    s20.scanList.scans.push_back(Scan());
    Scan& s20scan = s20.scanList.scans.back();
    s20scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
    s20scan.paramGroupPtrs.push_back(pg2);
    s20scan.cvParams.push_back(CVParam(MS_scan_start_time, 5.990500, UO_minute));
    s20scan.cvParams.push_back(CVParam(MS_filter_string, "+ c d Full ms2  445.35@cid35.00 [ 110.00-905.00]"));
    s20scan.scanWindows.resize(1);
    ScanWindow& window2 = s20scan.scanWindows.front();
    window2.cvParams.push_back(CVParam(MS_scan_window_lower_limit, 110.000000));
    window2.cvParams.push_back(CVParam(MS_scan_window_upper_limit, 905.000000));

    BinaryDataArrayPtr s20_mz(new BinaryDataArray);
    s20_mz->dataProcessingPtr = dpXcalibur;
    s20_mz->cvParams.push_back(MS_m_z_array);
    s20_mz->data.resize(10);
    for (int i=0; i<10; i++)
        s20_mz->data[i] = i;

    BinaryDataArrayPtr s20_intensity(new BinaryDataArray);
    s20_intensity->dataProcessingPtr = dpXcalibur;
    s20_intensity->cvParams.push_back(MS_intensity_array);
    s20_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        s20_intensity->data[i] = 10-i;

    s20.binaryDataArrayPtrs.push_back(s20_mz);
    s20.binaryDataArrayPtrs.push_back(s20_intensity);

    // chromatograms

    shared_ptr<ChromatogramListSimple> chromatogramList(new ChromatogramListSimple);
    msd.run.chromatogramListPtr = chromatogramList;

    chromatogramList->chromatograms.push_back(ChromatogramPtr(new Chromatogram));

    Chromatogram& tic = *chromatogramList->chromatograms[0];
    tic.id = "tic";
    tic.index = 0;
    tic.defaultArrayLength = 10;
    tic.cvParams.push_back(MS_total_ion_current_chromatogram);

    BinaryDataArrayPtr tic_time(new BinaryDataArray);
    tic_time->dataProcessingPtr = dp_msconvert;
    tic_time->cvParams.push_back(MS_time_array);
    tic_time->data.resize(10);
    for (int i=0; i<10; i++)
        tic_time->data[i] = i;

    BinaryDataArrayPtr tic_intensity(new BinaryDataArray);
    tic_intensity->dataProcessingPtr = dp_msconvert;
    tic_intensity->cvParams.push_back(MS_intensity_array);
    tic_intensity->data.resize(10);
    for (int i=0; i<10; i++)
        tic_intensity->data[i] = 10-i;

    tic.binaryDataArrayPtrs.push_back(tic_time);
    tic.binaryDataArrayPtrs.push_back(tic_intensity);
}


void testMSData()
{
    if (os_) *os_ << "testMSData():\n";

    MSData a;
    initializeTestData(a);

    // write 'a' out to a stream

    ostringstream oss;
    XMLWriter writer(oss);
    IO::write(writer, a);
    if (os_) *os_ << oss.str() << endl;

    // read 'b' in from stream, ignoring SpectrumList (default)

    MSData b;
    istringstream iss(oss.str());
    IO::read(iss, b); // IO::IgnoreSpectrumList

    // compare 'a' and 'b'

    Diff<MSData, DiffConfig> diff(a,b);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(diff);
    unit_assert(diff.a_b.run.spectrumListPtr.get());
    unit_assert(diff.a_b.run.spectrumListPtr->size() == 1);
    unit_assert(diff.a_b.run.spectrumListPtr->spectrum(0)->userParams.size() == 1);

    // read 'c' in from stream, reading SpectrumList

    MSData c;
    iss.seekg(0);
    IO::read(iss, c, IO::ReadSpectrumList);

    // compare 'a' and 'c'

    diff(a,c);
    if (diff && os_) *os_ << "diff:\n" << diff << endl;
    unit_assert(!diff);

    // remove SpectrumList and ChromatogramList from a, and compare to b

    a.run.spectrumListPtr.reset();
    a.run.chromatogramListPtr.reset();
    diff(a, b);
    unit_assert(!diff);
}


void test()
{
    testCV();
    testUserParam();
    testCVParam();
    testParamGroup();
    testNamedParamContainer<FileContent>();
    testSourceFile();
    testNamedParamContainer<Contact>();
    testFileDescription();
    testSample();
    testComponent();
    testComponentList();
    testSoftware();
    testInstrumentConfiguration();
    testProcessingMethod();
    testDataProcessing();
    testNamedParamContainer<Target>();
    testScanSettings();
    testNamedParamContainer<IsolationWindow>();
    testNamedParamContainer<SelectedIon>();
    testNamedParamContainer<Activation>();
    testPrecursor();
    testProduct();
    testNamedParamContainer<ScanWindow>();
    testScan();
    testScanList();
    testBinaryDataArray();
    testBinaryDataArrayExternalMetadata();
    testSpectrum();
    testChromatogram();
    testSpectrumList();
    testSpectrumListWithPositions();
    testSpectrumListWriteProgress();
    testChromatogramList();
    testChromatogramListWithPositions();
    testRun();
    testMSData();
    testBinaryArraysWithZstd();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_MSData")

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        if (os_) *os_ << "ok\n";
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

