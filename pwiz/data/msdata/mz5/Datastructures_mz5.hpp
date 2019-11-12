//
// $Id$
//
//
// Original authors: Mathias Wilhelm <mw@wilhelmonline.com>
//                   Marc Kirchner <mail@marc-kirchner.de>
//
// Copyright 2011 Proteomics Center
//                Children's Hospital Boston, Boston, MA 02135
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

#ifndef DATASTRUCTURES_MZ5_HPP_
#define DATASTRUCTURES_MZ5_HPP_

#include "../../common/cv.hpp"
#include "../../common/ParamTypes.hpp"
#include "../MSData.hpp"
#include "H5Cpp.h"
#include <string>
#include <vector>

namespace pwiz {
namespace msdata {
namespace mz5 {

/**
 * All these classes are wrappers with build in converter methods from pwiz::msdata::MSData to the corresponding MZ5 representation.
 * Each class has a copy constructor, an assign operator and a destructor. Some have a corresponding data struct, to allow the usage of HOFFSET, which only works with a plain old datatype(POD).
 */

/**
 * Forward declaration of ReferenceWrite_mz5 and ReferenceRead_mz5 to resolve cyclic dependency.
 */
class ReferenceWrite_mz5;
class ReferenceRead_mz5;
class Configuration_mz5;
class Connection_mz5;

/**
 * CVParam value string size
 */
#define CVL 128
/**
 * UserParam value string size
 */
#define USRVL 128
/**
 * UserParam name string size
 */
#define USRNL 256
/**
 * UserParam type string size.
 */
#define USRTL 64

/**
 * General mz5 file information.
 * This struct contains information about the mz5 version, and how to handle specific data sets.
 *
 * The functionality of didFiltering is removed, but it will stay in the FileInformationMZ5Data struct.
 */
struct FileInformationMZ5Data
{
    unsigned short majorVersion;
    unsigned short minorVersion;
    unsigned short didFiltering;
    unsigned short deltaMZ;
    unsigned short translateInten;
};

struct FileInformationMZ5: public FileInformationMZ5Data
{
    FileInformationMZ5();
    FileInformationMZ5(const FileInformationMZ5&);
    FileInformationMZ5(const Configuration_mz5&);
    ~FileInformationMZ5();
    FileInformationMZ5& operator=(const FileInformationMZ5&);
    void init(const unsigned short majorVersion,
            const unsigned short minorVersion, const unsigned didFiltering,
            const unsigned deltaMZ, const unsigned translateInten);
    static H5::CompType getType();
};

struct ContVocabMZ5Data
{
    char* uri;
    char* fullname;
    char* id;
    char* version;
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#cv
 */
struct ContVocabMZ5: public ContVocabMZ5Data
{
    ContVocabMZ5();
    ContVocabMZ5(const pwiz::cv::CV);
    ContVocabMZ5(const std::string& uri, const std::string& fullname,
            const std::string& id, const std::string& version);
    ContVocabMZ5(const char* uri, const char* fullname, const char* id,
            const char* version);
    ContVocabMZ5(const ContVocabMZ5&);
    ContVocabMZ5& operator=(const ContVocabMZ5&);
    ~ContVocabMZ5();
    void init(const std::string&, const std::string&, const std::string&,
            const std::string&);
    pwiz::cv::CV getCV();
    static H5::CompType getType();
    static void convert(std::vector<ContVocabMZ5>&, const std::vector<
            pwiz::cv::CV>);
};

struct CVRefMZ5Data
{
    char* name;
    char* prefix;
    unsigned long accession;
};

/**
 * This class will be referenced from CVParamMZ5 and UserParamMZ5 to reduce storage size.
 */
struct CVRefMZ5: public CVRefMZ5Data
{
    CVRefMZ5();
    CVRefMZ5(const pwiz::cv::CVID);
    CVRefMZ5(const CVRefMZ5&);
    CVRefMZ5& operator=(const CVRefMZ5&);
    ~CVRefMZ5();
    void init(const char* name, const char* prefix,
            const unsigned long accession);
    static H5::CompType getType();
};

struct UserParamMZ5Data
{
    char name[USRNL];
    char value[USRVL];
    char type[USRTL];
    unsigned long unitCVRefID;
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#userParam
 */
struct UserParamMZ5: public UserParamMZ5Data
{
    UserParamMZ5();
    UserParamMZ5(const UserParamMZ5&);
    UserParamMZ5(const pwiz::data::UserParam&, const ReferenceWrite_mz5& wref);
    UserParamMZ5& operator=(const UserParamMZ5&);
    ~UserParamMZ5();
    void init(const char* name, const char* value, const char* type,
            const unsigned long urefid);
    pwiz::data::UserParam getUserParam(const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

struct CVParamMZ5Data
{
    char value[CVL];
    unsigned long typeCVRefID;
    unsigned long unitCVRefID;
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#cvParam
 */
struct CVParamMZ5: public CVParamMZ5Data
{
    CVParamMZ5();
    CVParamMZ5(const CVParamMZ5&);
    CVParamMZ5(const pwiz::data::CVParam&, const ReferenceWrite_mz5& wref);
    CVParamMZ5& operator=(const CVParamMZ5&);
    ~CVParamMZ5();
    void init(const char* value, const unsigned long& cvrefid,
            const unsigned long& urefid);
    void fill(pwiz::data::CVParam&, const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

struct RefMZ5Data
{
    unsigned long refID;
};

/**
 * Is used as a general reference for different tags.
 */
struct RefMZ5: public RefMZ5Data
{
    RefMZ5();
    RefMZ5(const RefMZ5&);
    RefMZ5(const pwiz::data::ParamGroup&, const ReferenceWrite_mz5& ref);
    RefMZ5(const pwiz::msdata::Sample&, const ReferenceWrite_mz5& ref);
    RefMZ5(const pwiz::msdata::SourceFile&, const ReferenceWrite_mz5& ref);
    RefMZ5(const pwiz::msdata::Software&, const ReferenceWrite_mz5& ref);
    RefMZ5(const pwiz::msdata::ScanSettings&, const ReferenceWrite_mz5& ref);
    RefMZ5(const pwiz::msdata::DataProcessing&, const ReferenceWrite_mz5& ref);
    RefMZ5(const pwiz::msdata::InstrumentConfiguration&,
            const ReferenceWrite_mz5& ref);
    RefMZ5(const std::string& spectrumID, const ReferenceWrite_mz5& ref);
    RefMZ5& operator=(const RefMZ5&);
    ~RefMZ5();
    pwiz::msdata::ParamGroupPtr getParamGroupPtr(const ReferenceRead_mz5& rref);
    pwiz::msdata::SamplePtr getSamplePtr(const ReferenceRead_mz5& rref);
    pwiz::msdata::SourceFilePtr getSourceFilePtr(const ReferenceRead_mz5& rref);
    pwiz::msdata::SoftwarePtr getSoftwarePtr(const ReferenceRead_mz5& rref);
    pwiz::msdata::ScanSettingsPtr getScanSettingPtr(
            const ReferenceRead_mz5& rref);
    pwiz::msdata::DataProcessingPtr getDataProcessingPtr(
            const ReferenceRead_mz5& rref);
    pwiz::msdata::InstrumentConfigurationPtr getInstrumentPtr(
            const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

struct RefListMZ5Data
{
    size_t len;
    RefMZ5* list;
};

/**
 * Variable length list of references.
 */
struct RefListMZ5: RefListMZ5Data
{
    RefListMZ5();
    RefListMZ5(const RefListMZ5&);
    RefListMZ5(const std::vector<pwiz::data::ParamGroupPtr>&,
            const ReferenceWrite_mz5& ref);
    RefListMZ5(const std::vector<pwiz::msdata::SourceFilePtr>&,
            const ReferenceWrite_mz5& ref);
    RefListMZ5& operator=(const RefListMZ5&);
    ~RefListMZ5();
    void init(const RefMZ5* list, const size_t len);
    void fill(std::vector<pwiz::msdata::ParamGroupPtr>&,
            const ReferenceRead_mz5& rref);
    void fill(std::vector<pwiz::msdata::SourceFilePtr>&,
            const ReferenceRead_mz5& rref);
    static H5::VarLenType getType();
};

struct ParamListMZ5Data
{
    unsigned long cvParamStartID;
    unsigned long cvParamEndID;
    unsigned long userParamStartID;
    unsigned long userParamEndID;
    unsigned long refParamGroupStartID;
    unsigned long refParamGroupEndID;
};

/**
 * This class represents a pwiz::msdata::ParamContainer but only stores start and end indices for CVParams, UserParams and ParamGroups.
 */
struct ParamListMZ5: ParamListMZ5Data
{
    ParamListMZ5();
    ParamListMZ5(const ParamListMZ5&);
    ParamListMZ5(const std::vector<pwiz::msdata::CVParam>& cv,
            const std::vector<pwiz::msdata::UserParam>& user,
            const std::vector<pwiz::msdata::ParamGroupPtr>& param,
            const ReferenceWrite_mz5& wref);
    ParamListMZ5& operator=(const ParamListMZ5&);
    ~ParamListMZ5();
    bool empty();
    void init(const unsigned long cvstart, const unsigned long cvend,
            const unsigned long usrstart, const unsigned long usrend,
            const unsigned long refstart, const unsigned long refend);
    void fillParamContainer(pwiz::msdata::ParamContainer&,
            const ReferenceRead_mz5& rref);
    void fillParamContainer(std::vector<pwiz::msdata::CVParam>& cv,
            std::vector<pwiz::msdata::UserParam>& user, std::vector<
                    pwiz::msdata::ParamGroupPtr>& param,
            const ReferenceRead_mz5& rref);
    static H5::CompType getType();
    static void convert(std::vector<ParamListMZ5>&, const std::vector<
            pwiz::data::ParamContainer>&, const ReferenceWrite_mz5& wref);
    static void convert(std::vector<ParamListMZ5>&, const std::vector<
            pwiz::msdata::Contact>&, const ReferenceWrite_mz5& wref);
    static void convert(std::vector<ParamListMZ5>&, const std::vector<
            pwiz::msdata::FileContent>&, const ReferenceWrite_mz5& wref);
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#referenceableParamGroup
 */
struct ParamGroupMZ5
{
    char* id;
    ParamListMZ5 paramList;
    ParamGroupMZ5();
    ParamGroupMZ5(const ParamGroupMZ5&);
            ParamGroupMZ5(const pwiz::data::ParamGroup&,
                    const ReferenceWrite_mz5& wref);
    ParamGroupMZ5& operator=(const ParamGroupMZ5&);
    ~ParamGroupMZ5();
    void init(const ParamListMZ5& params, const char* id);
    pwiz::data::ParamGroup* getParamGroup(const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#sourceFile
 */
struct SourceFileMZ5
{
    char* id;
    char* location;
    char* name;
    ParamListMZ5 paramList;
    SourceFileMZ5();
    SourceFileMZ5(const SourceFileMZ5&);
    SourceFileMZ5(const pwiz::msdata::SourceFile&,
            const ReferenceWrite_mz5& wref);
    SourceFileMZ5& operator=(const SourceFileMZ5&);
    ~SourceFileMZ5();
    void init(const ParamListMZ5& params, const char* id, const char* location,
            const char* name);
    pwiz::msdata::SourceFile* getSourceFile(const ReferenceRead_mz5& rref);
    static H5::CompType getType();
    static void convert(std::vector<SourceFileMZ5>&, const std::vector<
            pwiz::msdata::SourceFilePtr>&, const ReferenceWrite_mz5& wref);
    static void read(const std::vector<pwiz::msdata::SourceFilePtr>&,
            const ReferenceWrite_mz5& wref);
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#sample
 */
struct SampleMZ5
{
    char* id;
    char* name;
    ParamListMZ5 paramList;
    SampleMZ5();
    SampleMZ5(const SampleMZ5&);
    SampleMZ5(const pwiz::msdata::Sample&, const ReferenceWrite_mz5& wref);
    SampleMZ5& operator=(const SampleMZ5&);
    ~SampleMZ5();
    void init(const ParamListMZ5& params, const char* id, const char* name);
    pwiz::msdata::Sample* getSample(const ReferenceRead_mz5& rref);
    static H5::CompType getType();
    static void convert(std::vector<SampleMZ5>&, const std::vector<
            pwiz::msdata::SamplePtr>&, const ReferenceWrite_mz5& wref);
    static void read(const std::vector<pwiz::msdata::SamplePtr>&,
            const ReferenceWrite_mz5& wref);
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#software
 */
struct SoftwareMZ5
{
    char* id;
    char* version;
    ParamListMZ5 paramList;
    SoftwareMZ5();
    SoftwareMZ5(const SoftwareMZ5&);
    SoftwareMZ5(const pwiz::msdata::Software&, const ReferenceWrite_mz5& wref);
    SoftwareMZ5& operator=(const SoftwareMZ5&);
    ~SoftwareMZ5();
    void init(const ParamListMZ5& params, const char* id, const char* version);
    pwiz::msdata::Software* getSoftware(const ReferenceRead_mz5& rref);
    static H5::CompType getType();
    static void convert(std::vector<SoftwareMZ5>&, const std::vector<
            pwiz::msdata::SoftwarePtr>&, const ReferenceWrite_mz5& wref);
    static void read(const std::vector<pwiz::msdata::SoftwarePtr>&,
            const ReferenceWrite_mz5& wref);
};

/**
 * This class is used as a generalized container for Targets, SelectedIons, Products and ScanWindows.
 * See for example:
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#isolationWindow
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#product
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#scanWindowList
 */
struct ParamListsMZ5
{
    size_t len;
    ParamListMZ5* lists;
    ParamListsMZ5();
    ParamListsMZ5(const ParamListsMZ5&);
    ParamListsMZ5(const std::vector<pwiz::msdata::Target>&,
            const ReferenceWrite_mz5& wref);
    ParamListsMZ5(const std::vector<pwiz::msdata::SelectedIon>&,
            const ReferenceWrite_mz5& wref);
    ParamListsMZ5(const std::vector<pwiz::msdata::Product>&,
            const ReferenceWrite_mz5& wref);
    ParamListsMZ5(const std::vector<pwiz::msdata::ScanWindow>&,
            const ReferenceWrite_mz5& wref);
    ParamListsMZ5& operator=(const ParamListsMZ5&);
    ~ParamListsMZ5();
    void init(const ParamListMZ5* list, const size_t len);
    void
    fill(std::vector<pwiz::msdata::Target>&, const ReferenceRead_mz5& rref);
    void fill(std::vector<pwiz::msdata::Product>&,
            const ReferenceRead_mz5& rref);
    void fill(std::vector<pwiz::msdata::ScanWindow>&,
            const ReferenceRead_mz5& rref);
    void fill(std::vector<pwiz::msdata::SelectedIon>&,
            const ReferenceRead_mz5& rref);
    static H5::VarLenType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#scanSettings
 */
struct ScanSettingMZ5
{
    char* id;
    ParamListMZ5 paramList;
    RefListMZ5 sourceFileIDs;
    ParamListsMZ5 targetList;
    ScanSettingMZ5();
    ScanSettingMZ5(const ScanSettingMZ5&);
    ScanSettingMZ5(const pwiz::msdata::ScanSettings&,
            const ReferenceWrite_mz5& wref);
    ScanSettingMZ5& operator=(const ScanSettingMZ5&);
    ~ScanSettingMZ5();
    void
    init(const ParamListMZ5& params, const RefListMZ5& refSourceFiles,
            const ParamListsMZ5 targets, const char* id);
    pwiz::msdata::ScanSettings* getScanSetting(const ReferenceRead_mz5& rref);
    static H5::CompType getType();
    static void convert(std::vector<ScanSettingMZ5>&, const std::vector<
            pwiz::msdata::ScanSettingsPtr>&, const ReferenceWrite_mz5& wref);
    static void read(const std::vector<pwiz::msdata::ScanSettingsPtr>&,
            const ReferenceWrite_mz5& wref);
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#source
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#analyzer
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#detector
 */
struct ComponentMZ5
{
    ParamListMZ5 paramList;
    unsigned long order;
    ComponentMZ5();
    ComponentMZ5(const ComponentMZ5&);
            ComponentMZ5(const pwiz::msdata::Component&,
                    const ReferenceWrite_mz5& wref);
    ComponentMZ5& operator=(const ComponentMZ5&);
    ~ComponentMZ5();
    void init(const ParamListMZ5&, const unsigned long order);
    void fillComponent(pwiz::msdata::Component& c,
            const pwiz::msdata::ComponentType t, const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

/**
 * List of components.
 */
struct ComponentListMZ5
{
    size_t len;
    ComponentMZ5* list;
    ComponentListMZ5();
    ComponentListMZ5(const ComponentListMZ5&);
    ComponentListMZ5(const std::vector<ComponentMZ5>&);
    ComponentListMZ5& operator=(const ComponentListMZ5&);
    ~ComponentListMZ5();
    void init(const ComponentMZ5*, const size_t&);
    void
    fill(pwiz::msdata::ComponentList&, const pwiz::msdata::ComponentType t,
            const ReferenceRead_mz5& rref);
    static H5::VarLenType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#componentList
 */
struct ComponentsMZ5
{
    ComponentListMZ5 sources;
    ComponentListMZ5 analyzers;
    ComponentListMZ5 detectors;
    ComponentsMZ5();
    ComponentsMZ5(const ComponentsMZ5&);
    ComponentsMZ5(const pwiz::msdata::ComponentList&,
            const ReferenceWrite_mz5& wref);
    ComponentsMZ5& operator=(const ComponentsMZ5&);
    ~ComponentsMZ5();
    void init(const ComponentListMZ5& sources,
            const ComponentListMZ5& analyzers,
            const ComponentListMZ5& detectors);
    void fill(pwiz::msdata::ComponentList&, const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#instrumentConfiguration
 */
struct InstrumentConfigurationMZ5
{
    char* id;
    ParamListMZ5 paramList;
    ComponentsMZ5 components;
    RefMZ5 scanSettingRefID;
    RefMZ5 softwareRefID;
    InstrumentConfigurationMZ5();
    InstrumentConfigurationMZ5(const InstrumentConfigurationMZ5&);
    InstrumentConfigurationMZ5(const pwiz::msdata::InstrumentConfiguration&,
            const ReferenceWrite_mz5& ref);
    InstrumentConfigurationMZ5& operator=(const InstrumentConfigurationMZ5&);
    ~InstrumentConfigurationMZ5();
    void init(const ParamListMZ5& params, const ComponentsMZ5& components,
            const RefMZ5& refScanSetting, const RefMZ5& refSoftware,
            const char* id);
    pwiz::msdata::InstrumentConfiguration* getInstrumentConfiguration(
            const ReferenceRead_mz5& rref);
    static H5::CompType getType();
    static void read(
            const std::vector<pwiz::msdata::InstrumentConfigurationPtr>&,
            const ReferenceWrite_mz5& wref);
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#processingMethod
 */
struct ProcessingMethodMZ5
{
    ParamListMZ5 paramList;
    RefMZ5 softwareRefID;
    unsigned long order;
    ProcessingMethodMZ5();
    ProcessingMethodMZ5(const ProcessingMethodMZ5&);
    ProcessingMethodMZ5(const pwiz::msdata::ProcessingMethod&,
            const ReferenceWrite_mz5& ref);
    ProcessingMethodMZ5& operator=(const ProcessingMethodMZ5&);
    ~ProcessingMethodMZ5();
    void init(const ParamListMZ5& params, const RefMZ5& refSoftware,
            const unsigned long order);
    void fillProcessingMethod(pwiz::msdata::ProcessingMethod& p,
            const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

/**
 * Variable length processing method list.
 */
struct ProcessingMethodListMZ5
{
    size_t len;
    ProcessingMethodMZ5* list;
    ProcessingMethodListMZ5();
    ProcessingMethodListMZ5(const ProcessingMethodListMZ5&);
    ProcessingMethodListMZ5(const std::vector<pwiz::msdata::ProcessingMethod>&,
            const ReferenceWrite_mz5& wref);
    ProcessingMethodListMZ5& operator=(const ProcessingMethodListMZ5&);
    ~ProcessingMethodListMZ5();
    void init(const ProcessingMethodMZ5* list, const size_t len);
    void fill(std::vector<pwiz::msdata::ProcessingMethod>&,
            const ReferenceRead_mz5& rref);
    static H5::VarLenType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#dataProcessing
 */
struct DataProcessingMZ5
{
    char* id;
    ProcessingMethodListMZ5 processingMethodList;
    DataProcessingMZ5();
    DataProcessingMZ5(const DataProcessingMZ5&);
    DataProcessingMZ5(const pwiz::msdata::DataProcessing&,
            const ReferenceWrite_mz5& wref);
    DataProcessingMZ5& operator=(const DataProcessingMZ5&);
    ~DataProcessingMZ5();
    void init(const ProcessingMethodListMZ5&, const char* id);
    pwiz::msdata::DataProcessing* getDataProcessing(
            const ReferenceRead_mz5& rref);
    static H5::CompType getType();
    static void convert(std::vector<DataProcessingMZ5>&, const std::vector<
            pwiz::msdata::DataProcessingPtr>&, const ReferenceWrite_mz5& wref);
    static void read(const std::vector<pwiz::msdata::DataProcessingPtr>&,
            const ReferenceWrite_mz5& wref);
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#precursor
 */
struct PrecursorMZ5
{
    char* externalSpectrumId;
    ParamListMZ5 paramList;
    ParamListMZ5 activation;
    ParamListMZ5 isolationWindow;
    ParamListsMZ5 selectedIonList;
    RefMZ5 spectrumRefID;
    RefMZ5 sourceFileRefID;
    PrecursorMZ5();
    PrecursorMZ5(const PrecursorMZ5&);
            PrecursorMZ5(const pwiz::msdata::Precursor&,
                    const ReferenceWrite_mz5& wref);
    PrecursorMZ5& operator=(const PrecursorMZ5&);
    ~PrecursorMZ5();
    void init(const ParamListMZ5& params,
            const ParamListMZ5& activation,
            const ParamListMZ5& isolationWindow,
            const ParamListsMZ5 selectedIonList, const RefMZ5& refSpectrum,
            const RefMZ5& refSourceFile, const char* externalSpectrumId);
    void fillPrecursor(pwiz::msdata::Precursor&, const ReferenceRead_mz5& rref, const Connection_mz5& conn);
    static H5::CompType getType();
};

/**
 * Variable length precursor list.
 */
struct PrecursorListMZ5
{
    size_t len;
    PrecursorMZ5* list;
    PrecursorListMZ5();
    PrecursorListMZ5(const PrecursorListMZ5&);
    PrecursorListMZ5(const std::vector<PrecursorMZ5>&);
    PrecursorListMZ5(const std::vector<pwiz::msdata::Precursor>&,
            const ReferenceWrite_mz5& wref);
    PrecursorListMZ5& operator=(const PrecursorListMZ5&);
    ~PrecursorListMZ5();
    void init(const PrecursorMZ5*, const size_t len);
    void fill(std::vector<pwiz::msdata::Precursor>&,
            const ReferenceRead_mz5& rref, const Connection_mz5& conn);
    static H5::VarLenType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#chromatogram but without binary data element.
 */
struct ChromatogramMZ5
{
    char* id;
    ParamListMZ5 paramList;
    PrecursorMZ5 precursor;
    ParamListMZ5 productIsolationWindow;
    RefMZ5 dataProcessingRefID;
    unsigned long index;
    ChromatogramMZ5();
    ChromatogramMZ5(const ChromatogramMZ5&);
    ChromatogramMZ5(const pwiz::msdata::Chromatogram&,
            const ReferenceWrite_mz5& wref);
    ChromatogramMZ5& operator=(const ChromatogramMZ5&);
    ~ChromatogramMZ5();
    void init(const ParamListMZ5& params, const PrecursorMZ5& precursor,
            const ParamListMZ5& productIsolationWindow,
            const RefMZ5& refDataProcessing, const unsigned long index,
            const char* id);
    pwiz::msdata::Chromatogram* getChromatogram(const ReferenceRead_mz5& rref, const Connection_mz5& conn);
    pwiz::msdata::ChromatogramIdentity getChromatogramIdentity();
    static H5::CompType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#scan
 */
struct ScanMZ5
{
    char* externalSpectrumID;
    ParamListMZ5 paramList;
    ParamListsMZ5 scanWindowList;
    RefMZ5 instrumentConfigurationRefID;
    RefMZ5 sourceFileRefID;
    RefMZ5 spectrumRefID;
    ScanMZ5();
    ScanMZ5(const ScanMZ5&);
    ScanMZ5(const pwiz::msdata::Scan&, const ReferenceWrite_mz5& wref);
    ScanMZ5& operator=(const ScanMZ5&);
    ~ScanMZ5();
    void init(const ParamListMZ5& params, const ParamListsMZ5& scanWindowList,
            const RefMZ5& refInstrument, const RefMZ5& refSourceFile,
            const RefMZ5& refSpectrum, const char* externalSpectrumID);
    void fill(pwiz::msdata::Scan&, const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

/**
 * Variable length scan list.
 */
struct ScanListMZ5
{
    size_t len;
    ScanMZ5* list;
    ScanListMZ5();
    ScanListMZ5(const ScanListMZ5&);
    ScanListMZ5(const std::vector<ScanMZ5>&);
    ScanListMZ5(const std::vector<pwiz::msdata::Scan>&,
            const ReferenceWrite_mz5& wref);
    ScanListMZ5& operator=(const ScanListMZ5&);
    ~ScanListMZ5();
    void init(const ScanMZ5* list, const size_t len);
    void fill(std::vector<pwiz::msdata::Scan>&, const ReferenceRead_mz5& rref);
    static H5::VarLenType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#scanList
 */
struct ScansMZ5
{
    ParamListMZ5 paramList;
    ScanListMZ5 scanList;
    ScansMZ5();
    ScansMZ5(const ScansMZ5&);
    ScansMZ5(const pwiz::msdata::ScanList&, const ReferenceWrite_mz5& wref);
    ScansMZ5& operator=(const ScansMZ5&);
    ~ScansMZ5();
    void init(const ParamListMZ5& params, const ScanListMZ5& scanList);
    void fill(pwiz::msdata::ScanList& sl, const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

/**
 *http://www.peptideatlas.org/tmp/mzML1.1.0.html#spectrum but without binary data elements.
 */
struct SpectrumMZ5
{
    char* id;
    char* spotID;
    ParamListMZ5 paramList;
    ScansMZ5 scanList;
    PrecursorListMZ5 precursorList;
    ParamListsMZ5 productList;
    RefMZ5 dataProcessingRefID;
    RefMZ5 sourceFileRefID;
    unsigned int index;
    SpectrumMZ5();
    SpectrumMZ5(const SpectrumMZ5&);
    SpectrumMZ5(const pwiz::msdata::Spectrum&, const ReferenceWrite_mz5& wref);
    SpectrumMZ5& operator=(const SpectrumMZ5&);
    ~SpectrumMZ5();
    void init(const ParamListMZ5& params, const ScansMZ5& scanList,
            const PrecursorListMZ5& precursors,
            const ParamListsMZ5& productIonIsolationWindows,
            const RefMZ5& refDataProcessing, const RefMZ5& refSourceFile,
            const unsigned long index, const char* id, const char* spotID);
    pwiz::msdata::Spectrum* getSpectrum(const ReferenceRead_mz5& rref, const Connection_mz5& conn);
    pwiz::msdata::SpectrumIdentity getSpectrumIdentity();
    void fillSpectrumIdentity(pwiz::msdata::SpectrumIdentity& si);
    static H5::CompType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#run but without spectrum and chromatogram elements.
 */
struct RunMZ5
{
    char* id;
    char* startTimeStamp;
    char* fid;
    char* facc;
    ParamListMZ5 paramList;
    RefMZ5 defaultSpectrumDataProcessingRefID;
    RefMZ5 defaultChromatogramDataProcessingRefID;
    RefMZ5 defaultInstrumentConfigurationRefID;
    RefMZ5 sourceFileRefID;
    RefMZ5 sampleRefID;
    RunMZ5();
    RunMZ5(const RunMZ5&);
    RunMZ5(const pwiz::msdata::Run&, const std::string fid,
            const std::string facc, const ReferenceWrite_mz5& wref);
    RunMZ5& operator=(const RunMZ5&);
    ~RunMZ5();
    void
    init(const ParamListMZ5& params, const RefMZ5& refSpectrumDP,
            const RefMZ5& refChromatogramDP,
            const RefMZ5& refDefaultInstrument, const RefMZ5& refSourceFile,
            const RefMZ5& refSample, const char* id,
            const char* startTimeStamp, const char* fid, const char* facc);
    void addInformation(pwiz::msdata::Run&, const ReferenceRead_mz5& rref);
    static H5::CompType getType();
};

/**
 * http://www.peptideatlas.org/tmp/mzML1.1.0.html#binaryDataArray but without raw data
 */
struct BinaryDataMZ5
{
    ParamListMZ5 xParamList;
    ParamListMZ5 yParamList;
    RefMZ5 xDataProcessingRefID;
    RefMZ5 yDataProcessingRefID;
    BinaryDataMZ5();
    BinaryDataMZ5(const BinaryDataMZ5&);
    BinaryDataMZ5(const pwiz::msdata::BinaryDataArray& bdax,
            const pwiz::msdata::BinaryDataArray& bday,
            const ReferenceWrite_mz5& wref);
    BinaryDataMZ5& operator=(const BinaryDataMZ5&);
    ~BinaryDataMZ5();
    bool empty();
    void init(const ParamListMZ5& xParams, const ParamListMZ5& yParams,
            const RefMZ5& refDPx, const RefMZ5& refDPy);
    void fill(pwiz::msdata::BinaryDataArray& bdax,
            pwiz::msdata::BinaryDataArray& bday, const ReferenceRead_mz5& rref);
    static H5::CompType getType();
    static void convert(std::vector<BinaryDataMZ5>& l,
            const pwiz::msdata::SpectrumListPtr& sptr,
            const ReferenceWrite_mz5& wref);
    static void convert(std::vector<BinaryDataMZ5>&,
            const pwiz::msdata::ChromatogramListPtr& cptr,
            const ReferenceWrite_mz5& wref);
};

}
}
}

#endif /* DATASTRUCTURES_MZ5_HPP_ */
