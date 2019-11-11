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

#include "Datastructures_mz5.hpp"
#include "ReferenceWrite_mz5.hpp"
#include "ReferenceRead_mz5.hpp"
#include "Configuration_mz5.hpp"
#include "../../common/cv.hpp"
#include <climits>
#include <stdexcept>
#include <cstring>

// TODO get rid of all the unnecessary copy constructor calls of CVParams
// TODO store often used data structures in hdf5
// TODO get rid of all vlen data types

namespace pwiz {
namespace msdata {
namespace mz5 {

using namespace H5;

namespace {

char* emptyString()
{
    char* dest = new char[1];
    dest[0] = '\0';
    return dest;
}

char* c_string(const std::string& s)
{
    char* ret = new char[s.length() + 1];
    s.copy(ret, std::string::npos);
    ret[s.length()] = '\0';
    return ret;
}

// get rid of this function
char* strcpyi(const char* src)
{
    if (src)
    {
        size_t srclen = strlen(src);
        char* dest = new char[srclen + 1];
        strcpy(dest, src);
        dest[srclen] = '\0';
        return dest;
    }
    return emptyString();
}

std::string getName(const pwiz::cv::CVID cvid)
{
    return pwiz::cv::cvTermInfo(cvid).name;
}

std::string getPrefix(const pwiz::cv::CVID cvid)
{
    std::string id = pwiz::cv::cvTermInfo(cvid).id;
    size_t f = id.find_first_of(':', 0);
    return id.substr(0, f);
}

unsigned long getAccession(const pwiz::cv::CVID cvid)
{
    std::string id = pwiz::cv::cvTermInfo(cvid).id;
    size_t f = id.find_first_of(':', 0);
    if (f != std::string::npos)
    {
        std::string acc = id.substr(f + 1, id.size());
        unsigned long acci;
        //TODO change to ul?
        if (sscanf(acc.c_str(), "%lu", &acci) == EOF)
        {
            return ULONG_MAX - 1;
        }
        return acci;
    }
    return ULONG_MAX - 1;
}

StrType getStringType()
{
    StrType stringtype(PredType::C_S1, H5T_VARIABLE);
    return stringtype;
}

StrType getFStringType(const size_t len)
{
    StrType stringtype(PredType::C_S1, len);
    return stringtype;
}

} // namespace


FileInformationMZ5::FileInformationMZ5()
{
    this->majorVersion = Configuration_mz5::MZ5_FILE_MAJOR_VERSION;
    this->minorVersion = Configuration_mz5::MZ5_FILE_MINOR_VERSION;
    this->didFiltering = 0;
    this->deltaMZ = 1;
    this->translateInten = 1;
}

FileInformationMZ5::FileInformationMZ5(const FileInformationMZ5& rhs)
{
    init(rhs.majorVersion, rhs.minorVersion, rhs.didFiltering, rhs.deltaMZ,
            rhs.translateInten);
}

FileInformationMZ5::FileInformationMZ5(const Configuration_mz5& c)
{
    unsigned short didfiltering = 0;
    unsigned short deltamz = c.doTranslating() ? 1 : 0;
    unsigned short translateinten = c.doTranslating() ? 1 : 0;
    init(Configuration_mz5::MZ5_FILE_MAJOR_VERSION,
            Configuration_mz5::MZ5_FILE_MINOR_VERSION, didfiltering, deltamz,
            translateinten);
}

FileInformationMZ5::~FileInformationMZ5()
{
}

FileInformationMZ5& FileInformationMZ5::operator=(const FileInformationMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.majorVersion, rhs.minorVersion, rhs.didFiltering, rhs.deltaMZ,
                rhs.translateInten);
    }
    return *this;
}

void FileInformationMZ5::init(const unsigned short majorVersion,
        const unsigned short minorVersion, const unsigned didFiltering,
        const unsigned deltaMZ, const unsigned translateInten)
{
    this->majorVersion = majorVersion;
    this->minorVersion = minorVersion;
    this->didFiltering = didFiltering;
    this->deltaMZ = deltaMZ;
    this->translateInten = translateInten;
}

H5::CompType FileInformationMZ5::getType()
{
    CompType ret(sizeof(FileInformationMZ5Data));
    ret.insertMember("majorVersion", HOFFSET(FileInformationMZ5Data,
            majorVersion), PredType::NATIVE_USHORT);
    ret.insertMember("minorVersion", HOFFSET(FileInformationMZ5Data,
            minorVersion), PredType::NATIVE_USHORT);
    ret.insertMember("didFiltering", HOFFSET(FileInformationMZ5Data,
            didFiltering), PredType::NATIVE_USHORT);
    ret.insertMember("deltaMZ", HOFFSET(FileInformationMZ5Data, deltaMZ),
            PredType::NATIVE_USHORT);
    ret.insertMember("translateInten", HOFFSET(FileInformationMZ5Data,
            translateInten), PredType::NATIVE_USHORT);
    return ret;
}

ContVocabMZ5::ContVocabMZ5()
{
    init(emptyString(), emptyString(), emptyString(), emptyString());
}

ContVocabMZ5::ContVocabMZ5(const pwiz::cv::CV cv)
{
    init(cv.URI, cv.fullName, cv.id, cv.version);
}

ContVocabMZ5::ContVocabMZ5(const std::string& uri, const std::string& fullname,
        const std::string& id, const std::string& version)
{
    init(uri, fullname, id, version);
}

ContVocabMZ5::ContVocabMZ5(const char* uri, const char* fullname,
        const char* id, const char* version)
{
    this->uri = strcpyi(uri);
    this->fullname = strcpyi(fullname);
    this->id = strcpyi(id);
    this->version = strcpyi(version);
}

ContVocabMZ5::ContVocabMZ5(const ContVocabMZ5& cv)
{
    this->uri = strcpyi(cv.uri);
    this->fullname = strcpyi(cv.fullname);
    this->id = strcpyi(cv.id);
    this->version = strcpyi(cv.version);
}

ContVocabMZ5& ContVocabMZ5::operator=(const ContVocabMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] uri;
        delete[] fullname;
        delete[] id;
        delete[] version;
        this->uri = strcpyi(rhs.uri);
        this->fullname = strcpyi(rhs.fullname);
        this->id = strcpyi(rhs.id);
        this->version = strcpyi(rhs.version);
    }
    return *this;
}

ContVocabMZ5::~ContVocabMZ5()
{
    delete[] uri;
    delete[] fullname;
    delete[] id;
    delete[] version;
}

void ContVocabMZ5::init(const std::string& uri, const std::string& fullname,
        const std::string& id, const std::string& version)
{
    this->uri = c_string(uri);
    this->fullname = c_string(fullname);
    this->id = c_string(id);
    this->version = c_string(version);
}

pwiz::cv::CV ContVocabMZ5::getCV()
{
    pwiz::cv::CV c;
    std::string sid(this->id);
    std::string suri(this->uri);
    std::string sfullname(fullname);
    std::string sversion(this->version);
    c.URI = suri;
    c.fullName = sfullname;
    c.id = sid;
    c.version = sversion;
    return c;
}

CompType ContVocabMZ5::getType()
{
    CompType cvtype(sizeof(ContVocabMZ5Data));
    StrType stringtype = getStringType();
    cvtype.insertMember("uri", HOFFSET(ContVocabMZ5Data, uri), stringtype);
    cvtype.insertMember("fullname", HOFFSET(ContVocabMZ5Data, fullname),
            stringtype);
    cvtype.insertMember("id", HOFFSET(ContVocabMZ5Data, id), stringtype);
    cvtype.insertMember("version", HOFFSET(ContVocabMZ5Data, version),
            stringtype);
    return cvtype;
}

void ContVocabMZ5::convert(std::vector<ContVocabMZ5>& l, const std::vector<
        pwiz::cv::CV> cvs)
{
    for (size_t i = 0; i < cvs.size(); ++i)
    {
        l.push_back(ContVocabMZ5(cvs[i]));
    }
}

CVRefMZ5::CVRefMZ5()
{
    init(emptyString(), emptyString(), ULONG_MAX);
}

CVRefMZ5::CVRefMZ5(const pwiz::cv::CVID cvid)
{
    init(getName(cvid).c_str(), getPrefix(cvid).c_str(), getAccession(cvid));
}

CVRefMZ5::CVRefMZ5(const CVRefMZ5& cvref)
{
    init(cvref.name, cvref.prefix, cvref.accession);
}

CVRefMZ5& CVRefMZ5::operator=(const CVRefMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] name;
        delete[] prefix;
        init(rhs.name, rhs.prefix, rhs.accession);
    }
    return *this;
}

CVRefMZ5::~CVRefMZ5()
{
    delete[] name;
    delete[] prefix;
}

void CVRefMZ5::init(const char* name, const char* prefix,
        const unsigned long accession)
{
    this->accession = accession;
    this->name = strcpyi(name);
    this->prefix = strcpyi(prefix);
}

CompType CVRefMZ5::getType()
{
    CompType ret(sizeof(CVRefMZ5Data));
    StrType stringtype = getStringType();
    ret.insertMember("name", HOFFSET(CVRefMZ5Data, name), stringtype);
    ret.insertMember("prefix", HOFFSET(CVRefMZ5Data, prefix), stringtype);
    ret.insertMember("accession", HOFFSET(CVRefMZ5Data, accession),
            PredType::NATIVE_ULONG);
    return ret;
}

UserParamMZ5::UserParamMZ5()
{
    init(0, 0, 0, ULONG_MAX);
}

UserParamMZ5::UserParamMZ5(const UserParamMZ5& userparam)
{
    init(userparam.name, userparam.value, userparam.type, userparam.unitCVRefID);
}

UserParamMZ5::UserParamMZ5(const pwiz::data::UserParam& userparam,
        const ReferenceWrite_mz5& wref)
{
    unsigned long uRefID = wref.getCVRefId(userparam.units);
    init(userparam.name.c_str(), userparam.value.c_str(),
            userparam.type.c_str(), uRefID);
}

UserParamMZ5& UserParamMZ5::operator=(const UserParamMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.name, rhs.value, rhs.type, rhs.unitCVRefID);
    }
    return *this;
}

UserParamMZ5::~UserParamMZ5()
{
}

void UserParamMZ5::init(const char* name, const char* value, const char* type,
        const unsigned long urefid)
{
    if (name) {
        strncpy(this->name, name, USRNL);
    }
    this->name[USRNL - 1] = '\0';
    if (value) {
        strncpy(this->value, value, USRVL);
    }
    this->value[USRVL - 1] = '\0';
    if (type) {
        strncpy(this->type, type, USRTL);
    }
    this->type[USRTL - 1] = '\0';
    this->unitCVRefID = urefid;
}

pwiz::data::UserParam UserParamMZ5::getUserParam(const ReferenceRead_mz5& rref)
{
    pwiz::msdata::UserParam u;
    std::string sname(this->name);
    std::string svalue(this->value);
    std::string stype(this->type);
    u.name = sname;
    u.value = svalue;
    u.type = stype;
    u.units = rref.getCVID(this->unitCVRefID);
    return u;
}

CompType UserParamMZ5::getType()
{
    CompType ret(sizeof(UserParamMZ5Data));
    StrType namestringtype = getFStringType(USRNL);
    StrType valuestringtype = getFStringType(USRVL);
    StrType typestringtype = getFStringType(USRTL);
    ret.insertMember("name", HOFFSET(UserParamMZ5Data, name), namestringtype);
    ret.insertMember("value", HOFFSET(UserParamMZ5Data, value), valuestringtype);
    ret.insertMember("type", HOFFSET(UserParamMZ5Data, type), typestringtype);
    ret.insertMember("uRefID", HOFFSET(UserParamMZ5Data, unitCVRefID),
            PredType::NATIVE_ULONG);
    return ret;
}

CVParamMZ5::CVParamMZ5()
{
    init(0, ULONG_MAX, ULONG_MAX);
}

CVParamMZ5::CVParamMZ5(const CVParamMZ5& cvparam)
{
    init(cvparam.value, cvparam.typeCVRefID, cvparam.unitCVRefID);
}

CVParamMZ5::CVParamMZ5(const pwiz::data::CVParam& cvparam,
        const ReferenceWrite_mz5& wref)
{
    init(cvparam.value.c_str(), wref.getCVRefId(cvparam.cvid), wref.getCVRefId(
            cvparam.units));
}

CVParamMZ5& CVParamMZ5::operator=(const CVParamMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.value, rhs.typeCVRefID, rhs.unitCVRefID);
    }
    return *this;
}

CVParamMZ5::~CVParamMZ5()
{
}

void CVParamMZ5::init(const char* value, const unsigned long& cvrefid,
        const unsigned long& urefid)
{
    if (value)
    {
        strcpy(this->value, value);
    } else {
        this->value[0] = '\0';
    }
    this->value[CVL - 1] = '\0';
    this->typeCVRefID = cvrefid;
    this->unitCVRefID = urefid;
}

void CVParamMZ5::fill(pwiz::data::CVParam& c, const ReferenceRead_mz5& rref)
{
    c.value = this->value;
    c.cvid = rref.getCVID(this->typeCVRefID);
    c.units = rref.getCVID(this->unitCVRefID);
}

CompType CVParamMZ5::getType()
{
    CompType ret(sizeof(CVParamMZ5Data));
    StrType stringtype = getFStringType(CVL);
    ret.insertMember("value", HOFFSET(CVParamMZ5Data, value), stringtype);
    ret.insertMember("cvRefID", HOFFSET(CVParamMZ5Data, typeCVRefID),
            PredType::NATIVE_ULONG);
    ret.insertMember("uRefID", HOFFSET(CVParamMZ5Data, unitCVRefID),
            PredType::NATIVE_ULONG);
    return ret;
}

RefMZ5::RefMZ5()
{
    this->refID = ULONG_MAX;
}

RefMZ5::RefMZ5(const RefMZ5& refparam)
{
    this->refID = refparam.refID;
}

RefMZ5::RefMZ5(const pwiz::data::ParamGroup& pg, const ReferenceWrite_mz5& ref)
{
    this->refID = ref.getParamGroupId(pg);
}

RefMZ5::RefMZ5(const pwiz::msdata::Sample& sample,
        const ReferenceWrite_mz5& ref)
{
    this->refID = ref.getSampleId(sample);
}

RefMZ5::RefMZ5(const pwiz::msdata::SourceFile& sourcefile,
        const ReferenceWrite_mz5& ref)
{
    this->refID = ref.getSourceFileId(sourcefile);
}

RefMZ5::RefMZ5(const pwiz::msdata::Software& software,
        const ReferenceWrite_mz5& ref)
{
    this->refID = ref.getSoftwareId(software);
}

RefMZ5::RefMZ5(const pwiz::msdata::ScanSettings& scansettings,
        const ReferenceWrite_mz5& ref)
{
    this->refID = ref.getScanSettingId(scansettings);
}

RefMZ5::RefMZ5(const pwiz::msdata::DataProcessing& dp,
        const ReferenceWrite_mz5& ref)
{
    this->refID = ref.getDataProcessingId(dp);
}

RefMZ5::RefMZ5(const std::string& spectrumID, const ReferenceWrite_mz5& ref)
{
    this->refID = ref.getSpectrumIndex(spectrumID);
}

RefMZ5::RefMZ5(const pwiz::msdata::InstrumentConfiguration& ic,
        const ReferenceWrite_mz5& ref)
{
    this->refID = ref.getInstrumentId(ic);
}

RefMZ5& RefMZ5::operator=(const RefMZ5& rhs)
{
    if (this != &rhs)
    {
        this->refID = rhs.refID;
    }
    return *this;
}

RefMZ5::~RefMZ5()
{
}

pwiz::msdata::ParamGroupPtr RefMZ5::getParamGroupPtr(
        const ReferenceRead_mz5& rref)
{
    return rref.getParamGroupPtr(refID);
}

pwiz::msdata::SourceFilePtr RefMZ5::getSourceFilePtr(
        const ReferenceRead_mz5& rref)
{
    return rref.getSourcefilePtr(refID);
}

pwiz::msdata::SamplePtr RefMZ5::getSamplePtr(const ReferenceRead_mz5& rref)
{
    return rref.getSamplePtr(refID);
}

pwiz::msdata::SoftwarePtr RefMZ5::getSoftwarePtr(const ReferenceRead_mz5& rref)
{
    return rref.getSoftwarePtr(refID);
}

pwiz::msdata::ScanSettingsPtr RefMZ5::getScanSettingPtr(
        const ReferenceRead_mz5& rref)
{
    return rref.getScanSettingPtr(refID);
}

pwiz::msdata::DataProcessingPtr RefMZ5::getDataProcessingPtr(
        const ReferenceRead_mz5& rref)
{
    return rref.getDataProcessingPtr(refID);
}

pwiz::msdata::InstrumentConfigurationPtr RefMZ5::getInstrumentPtr(
        const ReferenceRead_mz5& rref)
{
    return rref.getInstrumentPtr(refID);
}

CompType RefMZ5::getType()
{
    CompType ret(sizeof(RefMZ5Data));
    ret.insertMember("refID", HOFFSET(RefMZ5Data, refID),
            PredType::NATIVE_ULONG);
    return ret;
}

RefListMZ5::RefListMZ5()
{
    this->len = 0;
    this->list = 0;
}

RefListMZ5::RefListMZ5(const RefListMZ5& refparamlist)
{
    init(refparamlist.list, refparamlist.len);
}

RefListMZ5::RefListMZ5(
        const std::vector<pwiz::data::ParamGroupPtr>& paramgroupptr,
        const ReferenceWrite_mz5& ref)
{
    this->len = paramgroupptr.size();
    this->list = new RefMZ5[this->len];
    for (size_t i = 0; i < paramgroupptr.size(); ++i)
    {
        this->list[i] = RefMZ5(*paramgroupptr[i].get(), ref);
    }
}

RefListMZ5::RefListMZ5(
        const std::vector<pwiz::msdata::SourceFilePtr>& sourcefileptr,
        const ReferenceWrite_mz5& ref)
{
    std::vector<RefMZ5> l;
    for (size_t i = 0; i < sourcefileptr.size(); ++i)
    {
        if (sourcefileptr[i].get())
        {
            l.push_back(RefMZ5(*sourcefileptr[i].get(), ref));
        }
    }
    if (l.size() > 0)
    {
        init(&l[0], l.size());
    }
    else
    {
        init(0, 0);
    }
}

RefListMZ5& RefListMZ5::operator=(const RefListMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] list;
        init(rhs.list, rhs.len);
    }
    return *this;
}

RefListMZ5::~RefListMZ5()
{
    delete[] list;
}

void RefListMZ5::init(const RefMZ5* list, const size_t len)
{
    this->len = len;
    this->list = new RefMZ5[this->len];
    for (unsigned long i = 0; i < this->len; ++i)
    {
        this->list[i] = list[i];
    }
}

void RefListMZ5::fill(std::vector<pwiz::msdata::ParamGroupPtr>& l,
        const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        l.push_back(list[i].getParamGroupPtr(rref));
    }
}

void RefListMZ5::fill(std::vector<pwiz::msdata::SourceFilePtr>& l,
        const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        l.push_back(list[i].getSourceFilePtr(rref));
    }
}

VarLenType RefListMZ5::getType()
{
    CompType c = RefMZ5::getType();
    VarLenType ret(&c);
    return ret;
}

ParamGroupMZ5::ParamGroupMZ5() :
    id(emptyString()), paramList()
{
}

ParamGroupMZ5::ParamGroupMZ5(const ParamGroupMZ5& paramgroup)
{
    init(paramgroup.paramList, paramgroup.id);
}

ParamGroupMZ5::ParamGroupMZ5(const pwiz::data::ParamGroup& pg,
        const ReferenceWrite_mz5& wref)
{
    init(ParamListMZ5(pg.cvParams, pg.userParams, pg.paramGroupPtrs, wref),
            pg.id.c_str());
    wref.getParamGroupId(pg, this);
}

ParamGroupMZ5& ParamGroupMZ5::operator=(const ParamGroupMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        init(rhs.paramList, rhs.id);
    }
    return *this;
}

ParamGroupMZ5::~ParamGroupMZ5()
{
    delete[] id;
}

void ParamGroupMZ5::init(const ParamListMZ5& params, const char* id)
{
    this->paramList = params;
    this->id = strcpyi(id);
}

pwiz::data::ParamGroup* ParamGroupMZ5::getParamGroup(
        const ReferenceRead_mz5& rref)
{
    pwiz::data::ParamGroup* pg = new pwiz::data::ParamGroup();
    std::string sid(this->id);
    if (!sid.empty())
    {
        pg->id = sid;
    }
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (*pg), rref);
    return pg;
}

CompType ParamGroupMZ5::getType()
{
    CompType ret(sizeof(ParamGroupMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    return ret;
}

ParamListMZ5::ParamListMZ5()
{
    init(0, 0, 0, 0, 0, 0);
}

ParamListMZ5::ParamListMZ5(const ParamListMZ5& paramlist)
{
    init(paramlist.cvParamStartID, paramlist.cvParamEndID,
            paramlist.userParamStartID, paramlist.userParamEndID,
            paramlist.refParamGroupStartID, paramlist.refParamGroupEndID);
}

ParamListMZ5::ParamListMZ5(const std::vector<pwiz::msdata::CVParam>& cv,
        const std::vector<pwiz::msdata::UserParam>& user, const std::vector<
                pwiz::msdata::ParamGroupPtr>& param,
        const ReferenceWrite_mz5& wref)
{
    wref.getIndizes(cvParamStartID, cvParamEndID, userParamStartID,
            userParamEndID, refParamGroupStartID, refParamGroupEndID, cv, user,
            param);
}

ParamListMZ5& ParamListMZ5::operator=(const ParamListMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.cvParamStartID, rhs.cvParamEndID, rhs.userParamStartID,
                rhs.userParamEndID, rhs.refParamGroupStartID,
                rhs.refParamGroupEndID);
    }
    return *this;
}

ParamListMZ5::~ParamListMZ5()
{
}

bool ParamListMZ5::empty() {
    return this->cvParamEndID == 0
            && this->userParamEndID == 0
            && this->refParamGroupEndID == 0;
}

void ParamListMZ5::init(const unsigned long cvstart, const unsigned long cvend,
        const unsigned long usrstart, const unsigned long usrend,
        const unsigned long refstart, const unsigned long refend)
{
    this->cvParamStartID = cvstart;
    this->cvParamEndID = cvend;
    this->userParamStartID = usrstart;
    this->userParamEndID = usrend;
    this->refParamGroupStartID = refstart;
    this->refParamGroupEndID = refend;
}

void ParamListMZ5::fillParamContainer(pwiz::msdata::ParamContainer& pc,
        const ReferenceRead_mz5& rref)
{
    rref.fill(pc.cvParams, pc.userParams, pc.paramGroupPtrs, cvParamStartID,
            cvParamEndID, userParamStartID, userParamEndID,
            refParamGroupStartID, refParamGroupEndID);
}

void ParamListMZ5::fillParamContainer(std::vector<pwiz::msdata::CVParam>& cv,
        std::vector<pwiz::msdata::UserParam>& user, std::vector<
                pwiz::msdata::ParamGroupPtr>& param,
        const ReferenceRead_mz5& rref)
{
    rref.fill(cv, user, param, cvParamStartID, cvParamEndID, userParamStartID,
            userParamEndID, refParamGroupStartID, refParamGroupEndID);
}

CompType ParamListMZ5::getType()
{
    CompType ret(sizeof(ParamListMZ5Data));
    ret.insertMember("cvstart", HOFFSET(ParamListMZ5Data, cvParamStartID),
            PredType::NATIVE_ULONG);
    ret.insertMember("cvend", HOFFSET(ParamListMZ5Data, cvParamEndID),
            PredType::NATIVE_ULONG);
    ret.insertMember("usrstart", HOFFSET(ParamListMZ5Data, userParamStartID),
            PredType::NATIVE_ULONG);
    ret.insertMember("usrend", HOFFSET(ParamListMZ5Data, userParamEndID),
            PredType::NATIVE_ULONG);
    ret.insertMember("refstart",
            HOFFSET(ParamListMZ5Data, refParamGroupStartID),
            PredType::NATIVE_ULONG);
    ret.insertMember("refend", HOFFSET(ParamListMZ5Data, refParamGroupEndID),
            PredType::NATIVE_ULONG);
    return ret;
}

void ParamListMZ5::convert(std::vector<ParamListMZ5>& l, const std::vector<
        pwiz::data::ParamContainer>& pcl, const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < pcl.size(); ++i)
    {
        l.push_back(ParamListMZ5(pcl[i].cvParams, pcl[i].userParams,
                pcl[i].paramGroupPtrs, wref));
    }
}

void ParamListMZ5::convert(std::vector<ParamListMZ5>& l, const std::vector<
        pwiz::msdata::Contact>& pcl, const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < pcl.size(); ++i)
    {
        l.push_back(ParamListMZ5(pcl[i].cvParams, pcl[i].userParams,
                pcl[i].paramGroupPtrs, wref));
    }
}

void ParamListMZ5::convert(std::vector<ParamListMZ5>& l, const std::vector<
        pwiz::msdata::FileContent>& pcl, const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < pcl.size(); ++i)
    {
        l.push_back(ParamListMZ5(pcl[i].cvParams, pcl[i].userParams,
                pcl[i].paramGroupPtrs, wref));
    }
}

SourceFileMZ5::SourceFileMZ5() :
    id(emptyString()), location(emptyString()), name(emptyString()),
            paramList()
{
}

SourceFileMZ5::SourceFileMZ5(const SourceFileMZ5& sourcefile)
{
    init(sourcefile.paramList, sourcefile.id, sourcefile.location,
            sourcefile.name);
}

SourceFileMZ5::SourceFileMZ5(const pwiz::msdata::SourceFile& sourcefile,
        const ReferenceWrite_mz5& wref)
{
    init(ParamListMZ5(sourcefile.cvParams, sourcefile.userParams,
            sourcefile.paramGroupPtrs, wref), sourcefile.id.c_str(),
            sourcefile.location.c_str(), sourcefile.name.c_str());
    wref.getSourceFileId(sourcefile, this);
}

SourceFileMZ5& SourceFileMZ5::operator=(const SourceFileMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        delete[] location;
        delete[] name;
        init(rhs.paramList, rhs.id, rhs.location, rhs.name);
    }
    return *this;
}

SourceFileMZ5::~SourceFileMZ5()
{
    delete[] id;
    delete[] location;
    delete[] name;
}

void SourceFileMZ5::init(const ParamListMZ5& params, const char* id,
        const char* location, const char* name)
{
    this->paramList = params;
    this->id = strcpyi(id);
    this->location = strcpyi(location);
    this->name = strcpyi(name);
}

pwiz::msdata::SourceFile* SourceFileMZ5::getSourceFile(
        const ReferenceRead_mz5& rref)
{
    pwiz::msdata::SourceFile* sf = new pwiz::msdata::SourceFile();
    std::string sname(this->name);
    std::string sid(this->id);
    std::string slocation(this->location);
    if (!sid.empty())
    {
        sf->id = sid;
    }
    sf->location = slocation;
    sf->name = sname;
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (*sf), rref);
    return sf;
}

CompType SourceFileMZ5::getType()
{
    CompType ret(sizeof(SourceFileMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("location", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("name", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    return ret;
}

void SourceFileMZ5::convert(std::vector<SourceFileMZ5>& l, const std::vector<
        pwiz::msdata::SourceFilePtr>& sfl, const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < sfl.size(); ++i)
    {
        if (sfl[i].get())
        {
            l.push_back(SourceFileMZ5(*sfl[i].get(), wref));
        }
    }
}

void SourceFileMZ5::read(const std::vector<pwiz::msdata::SourceFilePtr>& sfl,
        const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < sfl.size(); ++i)
    {
        if (sfl[i].get())
        {
            SourceFileMZ5(*sfl[i].get(), wref);
        }
    }
}

SampleMZ5::SampleMZ5() :
    id(emptyString()), name(emptyString()), paramList()
{
}

SampleMZ5::SampleMZ5(const SampleMZ5& sample)
{
    init(sample.paramList, sample.id, sample.name);
}

SampleMZ5::SampleMZ5(const pwiz::msdata::Sample& sample,
        const ReferenceWrite_mz5& wref)
{
    init(ParamListMZ5(sample.cvParams, sample.userParams,
            sample.paramGroupPtrs, wref), sample.id.c_str(),
            sample.name.c_str());
    wref.getSampleId(sample, this);
}

SampleMZ5& SampleMZ5::operator=(const SampleMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        delete[] name;
        init(rhs.paramList, rhs.id, rhs.name);
    }
    return *this;
}

SampleMZ5::~SampleMZ5()
{
    delete[] id;
    delete[] name;
}

void SampleMZ5::init(const ParamListMZ5& params, const char* id,
        const char* name)
{
    this->paramList = params;
    this->id = strcpyi(id);
    this->name = strcpyi(name);
}

pwiz::msdata::Sample* SampleMZ5::getSample(const ReferenceRead_mz5& rref)
{
    pwiz::msdata::Sample* s = new pwiz::msdata::Sample();
    std::string sid(this->id);
    std::string sname(this->name);
    if (!sid.empty())
    {
        s->id = sid;
    }
    s->name = sname;
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::data::ParamContainer&> (*s), rref);
    return s;
}

CompType SampleMZ5::getType()
{
    CompType ret(sizeof(SampleMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("name", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    return ret;
}

void SampleMZ5::convert(std::vector<SampleMZ5>& l, const std::vector<
        pwiz::msdata::SamplePtr>& sl, const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < sl.size(); ++i)
    {
        if (sl[i].get())
        {
            l.push_back(SampleMZ5(*sl[i].get(), wref));
        }
    }
}

void SampleMZ5::read(const std::vector<pwiz::msdata::SamplePtr>& sl,
        const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < sl.size(); ++i)
    {
        if (sl[i].get())
        {
            SampleMZ5(*sl[i].get(), wref);
        }
    }
}

SoftwareMZ5::SoftwareMZ5() :
    id(emptyString()), version(emptyString()), paramList()
{
}

SoftwareMZ5::SoftwareMZ5(const SoftwareMZ5& software)
{
    init(software.paramList, software.id, software.version);
}

SoftwareMZ5::SoftwareMZ5(const pwiz::msdata::Software& software,
        const ReferenceWrite_mz5& wref)
{
    init(ParamListMZ5(software.cvParams, software.userParams,
            software.paramGroupPtrs, wref), software.id.c_str(),
            software.version.c_str());
    wref.getSoftwareId(software, this);
}

SoftwareMZ5& SoftwareMZ5::operator=(const SoftwareMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        delete[] version;
        init(rhs.paramList, rhs.id, rhs.version);
    }
    return *this;
}

SoftwareMZ5::~SoftwareMZ5()
{
    delete[] id;
    delete[] version;
}

void SoftwareMZ5::init(const ParamListMZ5& params, const char* id,
        const char* version)
{
    this->paramList = params;
    this->id = strcpyi(id);
    this->version = strcpyi(version);
}

pwiz::msdata::Software* SoftwareMZ5::getSoftware(const ReferenceRead_mz5& rref)
{
    pwiz::msdata::Software* s = new pwiz::msdata::Software();
    std::string sid(this->id);
    std::string sversion(this->version);
    if (!sid.empty())
    {
        s->id = sid;
    }
    s->version = sversion;
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (*s), rref);
    return s;
}

CompType SoftwareMZ5::getType()
{
    CompType ret(sizeof(SoftwareMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("version", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    return ret;
}

void SoftwareMZ5::convert(std::vector<SoftwareMZ5>& l, const std::vector<
        pwiz::msdata::SoftwarePtr>& sl, const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < sl.size(); ++i)
    {
        if (sl[i].get())
        {
            l.push_back(SoftwareMZ5(*sl[i].get(), wref));
        }
    }
}

void SoftwareMZ5::read(const std::vector<pwiz::msdata::SoftwarePtr>& sl,
        const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < sl.size(); ++i)
    {
        if (sl[i].get())
        {
            SoftwareMZ5(*sl[i].get(), wref);
        }
    }
}

ParamListsMZ5::ParamListsMZ5() :
    len(0), lists(0)
{

}

ParamListsMZ5::ParamListsMZ5(const ParamListsMZ5& target)
{
    init(target.lists, target.len);
}

ParamListsMZ5::ParamListsMZ5(const std::vector<pwiz::msdata::Target>& targets,
        const ReferenceWrite_mz5& wref)
{
    this->len = targets.size();
    this->lists = new ParamListMZ5[this->len];
    for (size_t i = 0; i < targets.size(); ++i)
    {
        this->lists[i] = ParamListMZ5(targets[i].cvParams,
                targets[i].userParams, targets[i].paramGroupPtrs, wref);
    }
}

ParamListsMZ5::ParamListsMZ5(
        const std::vector<pwiz::msdata::SelectedIon>& selected,
        const ReferenceWrite_mz5& wref)
{
    this->len = selected.size();
    this->lists = new ParamListMZ5[this->len];
    for (size_t i = 0; i < selected.size(); ++i)
    {
        this->lists[i] = ParamListMZ5(selected[i].cvParams,
                selected[i].userParams, selected[i].paramGroupPtrs, wref);
    }
}

ParamListsMZ5::ParamListsMZ5(
        const std::vector<pwiz::msdata::Product>& products,
        const ReferenceWrite_mz5& wref)
{
    this->len = products.size();
    this->lists = new ParamListMZ5[this->len];
    for (size_t i = 0; i < products.size(); ++i)
    {
        this->lists[i] = ParamListMZ5(products[i].isolationWindow.cvParams,
                products[i].isolationWindow.userParams,
                products[i].isolationWindow.paramGroupPtrs, wref);
    }
}

ParamListsMZ5::ParamListsMZ5(
        const std::vector<pwiz::msdata::ScanWindow>& scanWindows,
        const ReferenceWrite_mz5& wref)
{
    this->len = scanWindows.size();
    this->lists = new ParamListMZ5[this->len];
    for (size_t i = 0; i < scanWindows.size(); ++i)
    {
        this->lists[i] = ParamListMZ5(scanWindows[i].cvParams,
                scanWindows[i].userParams, scanWindows[i].paramGroupPtrs, wref);
    }
}

ParamListsMZ5& ParamListsMZ5::operator=(const ParamListsMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] lists;
        init(rhs.lists, rhs.len);
    }
    return *this;
}

ParamListsMZ5::~ParamListsMZ5()
{
    delete[] lists;
}

void ParamListsMZ5::init(const ParamListMZ5* list, const size_t len)
{
    this->len = len;
    this->lists = new ParamListMZ5[this->len];
    for (unsigned long i = 0; i < this->len; ++i)
    {
        this->lists[i] = list[i];
    }
}

void ParamListsMZ5::fill(std::vector<pwiz::msdata::Target>& l,
        const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        pwiz::msdata::Target t;
        this->lists[i].fillParamContainer(
                dynamic_cast<pwiz::msdata::ParamContainer&> (t), rref);
        l.push_back(t);
    }
}

void ParamListsMZ5::fill(std::vector<pwiz::msdata::Product>& l,
        const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        pwiz::msdata::Product p;
        lists[i].fillParamContainer(
                dynamic_cast<pwiz::msdata::ParamContainer&> (p.isolationWindow),
                rref);
        l.push_back(p);
    }
}

void ParamListsMZ5::fill(std::vector<pwiz::msdata::ScanWindow>& l,
        const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        pwiz::msdata::ScanWindow sw;
        lists[i].fillParamContainer(
                dynamic_cast<pwiz::msdata::ParamContainer&> (sw), rref);
        l.push_back(sw);
    }
}

void ParamListsMZ5::fill(std::vector<pwiz::msdata::SelectedIon>& l,
        const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        pwiz::msdata::SelectedIon si;
        lists[i].fillParamContainer(
                dynamic_cast<pwiz::msdata::ParamContainer&> (si), rref);
        l.push_back(si);
    }
}

VarLenType ParamListsMZ5::getType()
{
    CompType c(ParamListMZ5::getType());
    VarLenType ret(&c);
    return ret;
}

ScanSettingMZ5::ScanSettingMZ5() :
    id(emptyString()), paramList(), sourceFileIDs(), targetList()
{
}

ScanSettingMZ5::ScanSettingMZ5(const ScanSettingMZ5& scanSetting)
{
    init(scanSetting.paramList, scanSetting.sourceFileIDs,
            scanSetting.targetList, scanSetting.id);
}

ScanSettingMZ5::ScanSettingMZ5(const pwiz::msdata::ScanSettings& scansetting,
        const ReferenceWrite_mz5& wref)
{
    init(ParamListMZ5(), RefListMZ5(scansetting.sourceFilePtrs, wref),
            ParamListsMZ5(scansetting.targets, wref), scansetting.id.c_str());
    wref.getScanSettingId(scansetting, this);
}

ScanSettingMZ5& ScanSettingMZ5::operator=(const ScanSettingMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        init(rhs.paramList, rhs.sourceFileIDs, rhs.targetList, rhs.id);
    }
    return *this;
}

ScanSettingMZ5::~ScanSettingMZ5()
{
    delete[] id;
}

void ScanSettingMZ5::init(const ParamListMZ5& params,
        const RefListMZ5& refSourceFiles, const ParamListsMZ5 targets,
        const char* id)
{
    this->paramList = params;
    this->sourceFileIDs = refSourceFiles;
    this->targetList = targets;
    this->id = strcpyi(id);
}

pwiz::msdata::ScanSettings* ScanSettingMZ5::getScanSetting(
        const ReferenceRead_mz5& rref)
{
    pwiz::msdata::ScanSettings* s = new pwiz::msdata::ScanSettings();
    std::string sid(this->id);
    if (!sid.empty())
    {
        s->id = sid;
    }
    //TODO scan settings has no cv, usr or ref
    this->sourceFileIDs.fill(s->sourceFilePtrs, rref);
    this->targetList.fill(s->targets, rref);
    return s;
}

CompType ScanSettingMZ5::getType()
{
    CompType ret(sizeof(ScanSettingMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("refSourceFiles", offset, RefListMZ5::getType());
    offset += sizeof(RefListMZ5Data);
    ret.insertMember("targets", offset, ParamListsMZ5::getType());
    return ret;
}

void ScanSettingMZ5::convert(std::vector<ScanSettingMZ5>& l, const std::vector<
        pwiz::msdata::ScanSettingsPtr>& s, const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < s.size(); ++i)
    {
        if (s[i].get())
        {
            l.push_back(ScanSettingMZ5(*s[i].get(), wref));
        }
    }
}

void ScanSettingMZ5::read(const std::vector<pwiz::msdata::ScanSettingsPtr>& s,
        const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < s.size(); ++i)
    {
        if (s[i].get())
        {
            ScanSettingMZ5(*s[i].get(), wref);
        }
    }
}

ComponentMZ5::ComponentMZ5() :
    paramList(), order(ULONG_MAX)
{
}

ComponentMZ5::ComponentMZ5(const ComponentMZ5& component)
{
    init(component.paramList, component.order);
}

ComponentMZ5::ComponentMZ5(const pwiz::msdata::Component& component,
        const ReferenceWrite_mz5& wref)
{
    init(ParamListMZ5(component.cvParams, component.userParams,
            component.paramGroupPtrs, wref), component.order);
}

ComponentMZ5& ComponentMZ5::operator=(const ComponentMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.paramList, rhs.order);
    }
    return *this;
}

ComponentMZ5::~ComponentMZ5()
{
}

void ComponentMZ5::init(const ParamListMZ5& param, const unsigned long order)
{
    this->paramList = param;
    this->order = order;
}

void ComponentMZ5::fillComponent(pwiz::msdata::Component& c,
        const pwiz::msdata::ComponentType t, const ReferenceRead_mz5& rref)
{
    c.order = order;
    c.type = t;
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (c), rref);
}

CompType ComponentMZ5::getType()
{
    CompType ret(sizeof(ComponentMZ5));
    size_t offset = 0;
    ret.insertMember("paramList", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("order", offset, PredType::NATIVE_ULONG);
    return ret;
}

ComponentListMZ5::ComponentListMZ5() :
    len(0), list(0)
{
}

ComponentListMZ5::ComponentListMZ5(const ComponentListMZ5& componentlist)
{
    init(componentlist.list, componentlist.len);
}

ComponentListMZ5::ComponentListMZ5(const std::vector<ComponentMZ5>& clist)
{
    if (clist.size() > 0)
    {
        init(&clist[0], clist.size());
    }
    else
    {
        init(0, 0);
    }
}

ComponentListMZ5& ComponentListMZ5::operator =(const ComponentListMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] list;
        init(rhs.list, rhs.len);
    }
    return *this;
}

ComponentListMZ5::~ComponentListMZ5()
{
    delete[] list;
}

void ComponentListMZ5::init(const ComponentMZ5* list, const size_t& len)
{
    this->len = len;
    this->list = new ComponentMZ5[this->len];
    for (size_t i = 0; i < this->len; ++i)
    {
        this->list[i] = list[i];
    }
}

void ComponentListMZ5::fill(pwiz::msdata::ComponentList& l,
        const pwiz::msdata::ComponentType t, const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        pwiz::msdata::Component c;
        list[i].fillComponent(c, t, rref);
        l.push_back(c);
    }
}

VarLenType ComponentListMZ5::getType()
{
    CompType c = ComponentMZ5::getType();
    VarLenType ret(&c);
    return ret;
}

ComponentsMZ5::ComponentsMZ5() :
    sources(), analyzers(), detectors()
{
}

ComponentsMZ5::ComponentsMZ5(const ComponentsMZ5& components)
{
    init(components.sources, components.analyzers, components.detectors);
}

ComponentsMZ5::ComponentsMZ5(const pwiz::msdata::ComponentList& cl,
        const ReferenceWrite_mz5& wref)
{
    size_t s = cl.size(), j = 0;
    bool trySource = true, tryAnalyzer = true, tryDetector = true;
    std::vector<ComponentMZ5> sources, analyzers, detectors;
    for (size_t i = 0; i < s && j < s; ++i)
    {
        if (trySource && j < s)
        {
            try
            {
                sources.push_back(ComponentMZ5(cl.source(i), wref));
                j++;
            } catch (std::out_of_range&)
            {
                trySource = false;
            }
        }

        if (tryAnalyzer && j < s)
        {
            try
            {
                analyzers.push_back(ComponentMZ5(cl.analyzer(i), wref));
                j++;
            } catch (std::out_of_range&)
            {
                tryAnalyzer = false;
            }
        }

        if (tryDetector && j < s)
        {
            try
            {
                detectors.push_back(ComponentMZ5(cl.detector(i), wref));
                j++;
            } catch (std::out_of_range&)
            {
                tryDetector = false;
            }
        }
    }
    init(ComponentListMZ5(sources), ComponentListMZ5(analyzers),
            ComponentListMZ5(detectors));
}

ComponentsMZ5& ComponentsMZ5::operator=(const ComponentsMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.sources, rhs.analyzers, rhs.detectors);
    }
    return *this;
}

ComponentsMZ5::~ComponentsMZ5()
{

}

void ComponentsMZ5::init(const ComponentListMZ5& sources,
        const ComponentListMZ5& analyzers, const ComponentListMZ5& detectors)
{
    this->sources = sources;
    this->analyzers = analyzers;
    this->detectors = detectors;
}

void ComponentsMZ5::fill(pwiz::msdata::ComponentList& l,
        const ReferenceRead_mz5& rref)
{
    this->sources.fill(l, pwiz::msdata::ComponentType_Source, rref);
    this->analyzers.fill(l, pwiz::msdata::ComponentType_Analyzer, rref);
    this->detectors.fill(l, pwiz::msdata::ComponentType_Detector, rref);
}

CompType ComponentsMZ5::getType()
{
    CompType ret(sizeof(ComponentsMZ5));
    size_t offset = 0;
    ret.insertMember("sources", offset, ComponentListMZ5::getType());
    offset += sizeof(ComponentListMZ5);
    ret.insertMember("analyzers", offset, ComponentListMZ5::getType());
    offset += sizeof(ComponentListMZ5);
    ret.insertMember("detectors", offset, ComponentListMZ5::getType());
    return ret;
}

InstrumentConfigurationMZ5::InstrumentConfigurationMZ5() :
    id(emptyString()), paramList(), components(), scanSettingRefID(),
            softwareRefID()
{
}

InstrumentConfigurationMZ5::InstrumentConfigurationMZ5(
        const InstrumentConfigurationMZ5& instrumentConfiguration)
{
    init(instrumentConfiguration.paramList, instrumentConfiguration.components,
            instrumentConfiguration.scanSettingRefID,
            instrumentConfiguration.softwareRefID, instrumentConfiguration.id);
}

InstrumentConfigurationMZ5::InstrumentConfigurationMZ5(
        const pwiz::msdata::InstrumentConfiguration& instrumentConfiguration,
        const ReferenceWrite_mz5& wref)
{
    ParamListMZ5 params(instrumentConfiguration.cvParams,
            instrumentConfiguration.userParams,
            instrumentConfiguration.paramGroupPtrs, wref);
    ComponentsMZ5 components(instrumentConfiguration.componentList, wref);
    RefMZ5 scanSettings;
    if (instrumentConfiguration.scanSettingsPtr.get())
    {
        RefMZ5 tmp(*instrumentConfiguration.scanSettingsPtr.get(), wref);
        scanSettings = tmp;
    }
    RefMZ5 software;
    if (instrumentConfiguration.softwarePtr.get())
    {
        RefMZ5 tmp(*instrumentConfiguration.softwarePtr.get(), wref);
        software = tmp;
    }
    init(params, components, scanSettings, software,
            instrumentConfiguration.id.c_str());
    wref.getInstrumentId(instrumentConfiguration, this);
}

InstrumentConfigurationMZ5& InstrumentConfigurationMZ5::operator=(
        const InstrumentConfigurationMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        init(rhs.paramList, rhs.components, rhs.scanSettingRefID,
                rhs.softwareRefID, rhs.id);
    }
    return *this;
}

InstrumentConfigurationMZ5::~InstrumentConfigurationMZ5()
{
    delete[] id;
}

void InstrumentConfigurationMZ5::init(const ParamListMZ5& params,
        const ComponentsMZ5& components, const RefMZ5& refScanSetting,
        const RefMZ5& refSoftware, const char* id)
{
    this->paramList = params;
    this->components = components;
    this->scanSettingRefID = refScanSetting;
    this->softwareRefID = refSoftware;
    this->id = strcpyi(id);
}

pwiz::msdata::InstrumentConfiguration* InstrumentConfigurationMZ5::getInstrumentConfiguration(
        const ReferenceRead_mz5& rref)
{
    pwiz::msdata::InstrumentConfiguration* i =
            new pwiz::msdata::InstrumentConfiguration();
    std::string sid(id);
    if (!sid.empty())
    {
        i->id = sid;
    }
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (*i), rref);
    try
    {
        if (scanSettingRefID.refID != ULONG_MAX)
        {
            i->scanSettingsPtr = scanSettingRefID.getScanSettingPtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }
    try
    {
        if (softwareRefID.refID != ULONG_MAX)
        {
            i->softwarePtr = softwareRefID.getSoftwarePtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }
    this->components.fill(i->componentList, rref);
    return i;
}

CompType InstrumentConfigurationMZ5::getType()
{
    CompType ret(sizeof(InstrumentConfigurationMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("components", offset, ComponentsMZ5::getType());
    offset += sizeof(ComponentsMZ5);
    ret.insertMember("refScanSetting", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    ret.insertMember("refSoftware", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    return ret;
}

void InstrumentConfigurationMZ5::read(const std::vector<
        pwiz::msdata::InstrumentConfigurationPtr>& s,
        const ReferenceWrite_mz5& wref)
{
    std::vector<pwiz::msdata::InstrumentConfigurationPtr>::const_iterator it;
    for (it = s.begin(); it != s.end(); ++it)
    {
        if (it->get())
        {
            InstrumentConfigurationMZ5(*(it->get()), wref);
        }
    }
}

ProcessingMethodMZ5::ProcessingMethodMZ5() :
    paramList(), softwareRefID(), order(ULONG_MAX)
{
}

ProcessingMethodMZ5::ProcessingMethodMZ5(
        const ProcessingMethodMZ5& processingMethod)
{
    init(processingMethod.paramList, processingMethod.softwareRefID,
            processingMethod.order);
}

ProcessingMethodMZ5::ProcessingMethodMZ5(
        const pwiz::msdata::ProcessingMethod& processingMethod,
        const ReferenceWrite_mz5& wref)
{
    ParamListMZ5 paramList(processingMethod.cvParams,
            processingMethod.userParams, processingMethod.paramGroupPtrs, wref);
    RefMZ5 ref;
    if (processingMethod.softwarePtr.get())
    {
        RefMZ5 tmp(*processingMethod.softwarePtr.get(), wref);
        ref = tmp;
    }
    init(paramList, ref, processingMethod.order);
}

ProcessingMethodMZ5& ProcessingMethodMZ5::operator =(
        const ProcessingMethodMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.paramList, rhs.softwareRefID, rhs.order);
    }
    return *this;
}

ProcessingMethodMZ5::~ProcessingMethodMZ5()
{
}

void ProcessingMethodMZ5::init(const ParamListMZ5& params,
        const RefMZ5& refSoftware, const unsigned long order)
{
    this->paramList = params;
    this->softwareRefID = refSoftware;
    this->order = order;
}

void ProcessingMethodMZ5::fillProcessingMethod(
        pwiz::msdata::ProcessingMethod& p, const ReferenceRead_mz5& rref)
{
    p.order = order;
    try
    {
        if (softwareRefID.refID != ULONG_MAX)
        {
            p.softwarePtr = softwareRefID.getSoftwarePtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (p), rref);
}

CompType ProcessingMethodMZ5::getType()
{
    CompType ret(sizeof(ProcessingMethodMZ5));
    size_t offset = 0;
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("refSoftware", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    ret.insertMember("order", offset, PredType::NATIVE_ULONG);
    return ret;
}

ProcessingMethodListMZ5::ProcessingMethodListMZ5() :
    len(0), list(0)
{
}

ProcessingMethodListMZ5::ProcessingMethodListMZ5(
        const ProcessingMethodListMZ5& processingMethodList)
{
    init(processingMethodList.list, processingMethodList.len);
}

ProcessingMethodListMZ5::ProcessingMethodListMZ5(const std::vector<
        pwiz::msdata::ProcessingMethod>& list, const ReferenceWrite_mz5& wref)
{
    std::vector<ProcessingMethodMZ5> l;
    for (size_t i = 0; i < list.size(); ++i)
    {
        l.push_back(ProcessingMethodMZ5(list[i], wref));
    }
    init(&l[0], l.size());
}

ProcessingMethodListMZ5& ProcessingMethodListMZ5::operator=(
        const ProcessingMethodListMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] list;
        init(rhs.list, rhs.len);
    }
    return *this;
}

ProcessingMethodListMZ5::~ProcessingMethodListMZ5()
{
    delete[] list;
}

void ProcessingMethodListMZ5::init(const ProcessingMethodMZ5* list,
        const size_t len)
{
    this->len = len;
    this->list = new ProcessingMethodMZ5[this->len];
    for (unsigned long i = 0; i < this->len; ++i)
    {
        this->list[i] = list[i];
    }
}

void ProcessingMethodListMZ5::fill(
        std::vector<pwiz::msdata::ProcessingMethod>& l,
        const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        pwiz::msdata::ProcessingMethod pm;
        list[i].fillProcessingMethod(pm, rref);
        l.push_back(pm);
    }
}

VarLenType ProcessingMethodListMZ5::getType()
{
    CompType c = ProcessingMethodMZ5::getType();
    VarLenType ret(&c);
    return ret;
}

DataProcessingMZ5::DataProcessingMZ5() :
    id(emptyString()), processingMethodList()
{
}

DataProcessingMZ5::DataProcessingMZ5(const DataProcessingMZ5& dataProcessing)
{
    init(dataProcessing.processingMethodList, dataProcessing.id);
}

DataProcessingMZ5::DataProcessingMZ5(const pwiz::msdata::DataProcessing& dp,
        const ReferenceWrite_mz5& wref)
{
    ProcessingMethodListMZ5 pml(dp.processingMethods, wref);
    init(pml, dp.id.c_str());
    wref.getDataProcessingId(dp, this);
}

DataProcessingMZ5& DataProcessingMZ5::operator =(const DataProcessingMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        init(rhs.processingMethodList, rhs.id);
    }
    return *this;
}

DataProcessingMZ5::~DataProcessingMZ5()
{
    delete[] id;
}

void DataProcessingMZ5::init(const ProcessingMethodListMZ5& method,
        const char* id)
{
    this->processingMethodList = method;
    this->id = strcpyi(id);
}

pwiz::msdata::DataProcessing* DataProcessingMZ5::getDataProcessing(
        const ReferenceRead_mz5& rref)
{
    pwiz::msdata::DataProcessing* dp = new pwiz::msdata::DataProcessing();
    std::string sid(id);
    if (!sid.empty())
    {
        dp->id = sid;
    }
    this->processingMethodList.fill(dp->processingMethods, rref);
    return dp;
}

CompType DataProcessingMZ5::getType()
{
    CompType ret(sizeof(DataProcessingMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("method", offset, ProcessingMethodListMZ5::getType());
    offset += sizeof(ProcessingMethodListMZ5);
    return ret;
}

void DataProcessingMZ5::convert(std::vector<DataProcessingMZ5>& l,
        const std::vector<pwiz::msdata::DataProcessingPtr>& s,
        const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < s.size(); ++i)
    {
        if (s[i].get())
        {
            l.push_back(DataProcessingMZ5(*s[i].get(), wref));
        }
    }
}

void DataProcessingMZ5::read(
        const std::vector<pwiz::msdata::DataProcessingPtr>& s,
        const ReferenceWrite_mz5& wref)
{
    for (size_t i = 0; i < s.size(); ++i)
    {
        if (s[i].get())
        {
            DataProcessingMZ5(*s[i].get(), wref);
        }
    }
}

PrecursorMZ5::PrecursorMZ5() :
    externalSpectrumId(emptyString()), paramList(), activation(), isolationWindow(),
            selectedIonList(), spectrumRefID(), sourceFileRefID()
{
}

PrecursorMZ5::PrecursorMZ5(const PrecursorMZ5& precursor)
{
    init(precursor.paramList, precursor.activation, precursor.isolationWindow,
            precursor.selectedIonList, precursor.spectrumRefID,
            precursor.sourceFileRefID, precursor.externalSpectrumId);
}

PrecursorMZ5::PrecursorMZ5(const pwiz::msdata::Precursor& precursor,
        const ReferenceWrite_mz5& wref)
{
    ParamListMZ5 params(precursor.cvParams,
            precursor.userParams,
            precursor.paramGroupPtrs, wref);
    ParamListMZ5 activation(precursor.activation.cvParams,
            precursor.activation.userParams,
            precursor.activation.paramGroupPtrs, wref);
    ParamListMZ5 isolation(precursor.isolationWindow.cvParams,
            precursor.isolationWindow.userParams,
            precursor.isolationWindow.paramGroupPtrs, wref);
    ParamListsMZ5 selectedIons(precursor.selectedIons, wref);
    RefMZ5 refspectrum;
    if (!precursor.spectrumID.empty())
    {
        RefMZ5 tmp(precursor.spectrumID, wref);
        refspectrum = tmp;
    }
    RefMZ5 refSourceFile;
    if (precursor.sourceFilePtr.get())
    {
        RefMZ5 tmp(*precursor.sourceFilePtr.get(), wref);
        refSourceFile = tmp;
    }
    init(params, activation, isolation, selectedIons, refspectrum, refSourceFile,
            precursor.externalSpectrumID.c_str());
}

PrecursorMZ5& PrecursorMZ5::operator=(const PrecursorMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] externalSpectrumId;
        init(rhs.paramList, rhs.activation, rhs.isolationWindow, rhs.selectedIonList,
                rhs.spectrumRefID, rhs.sourceFileRefID, rhs.externalSpectrumId);
    }
    return *this;
}

PrecursorMZ5::~PrecursorMZ5()
{
    delete[] externalSpectrumId;
}

void PrecursorMZ5::init(const ParamListMZ5& params,
       const ParamListMZ5& activation,
        const ParamListMZ5& isolationWindow,
        const ParamListsMZ5 selectedIonList, const RefMZ5& refSpectrum,
        const RefMZ5& refSourceFile, const char* externalSpectrumId)
{
    this->paramList = params;
    this->activation = activation;
    this->isolationWindow = isolationWindow;
    this->selectedIonList = selectedIonList;
    this->spectrumRefID = refSpectrum;
    this->sourceFileRefID = refSourceFile;
    this->externalSpectrumId = strcpyi(externalSpectrumId);
}

void PrecursorMZ5::fillPrecursor(pwiz::msdata::Precursor& p,
        const ReferenceRead_mz5& rref, const Connection_mz5& conn)
{
    if (conn.getFileInformation().minorVersion >= 10)
        this->paramList.fillParamContainer(dynamic_cast<pwiz::msdata::ParamContainer&> (p), rref);
    this->activation.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (p.activation), rref);
    this->isolationWindow.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (p.isolationWindow),
            rref);
    try
    {
        if (spectrumRefID.refID != ULONG_MAX)
        {
            p.spectrumID = rref.getSpectrumId(spectrumRefID.refID);
        }
    } catch (std::out_of_range&)
    {
    }

    try
    {
        if (sourceFileRefID.refID != ULONG_MAX)
        {
            p.sourceFilePtr = sourceFileRefID.getSourceFilePtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }
    std::string sexternal(externalSpectrumId);
    p.externalSpectrumID = sexternal;
    this->selectedIonList.fill(p.selectedIons, rref);
}

CompType PrecursorMZ5::getType()
{
    CompType ret(sizeof(PrecursorMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("externalSpectrumId", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("activation", offset, ParamListMZ5::getType());
    offset += ParamListMZ5::getType().getSize();
    ret.insertMember("isolationWindow", offset, ParamListMZ5::getType());
    offset += ParamListMZ5::getType().getSize();
    ret.insertMember("selectedIonList", offset, ParamListsMZ5::getType());
    offset += ParamListsMZ5::getType().getSize();
    ret.insertMember("refSpectrum", offset, RefMZ5::getType());
    offset += RefMZ5::getType().getSize();
    ret.insertMember("refSourceFile", offset, RefMZ5::getType());
    offset += RefMZ5::getType().getSize();
    return ret;
}

PrecursorListMZ5::PrecursorListMZ5() :
    len(0), list(0)
{
}

PrecursorListMZ5::PrecursorListMZ5(const PrecursorListMZ5& precursorList)
{
    init(precursorList.list, precursorList.len);
}

PrecursorListMZ5::PrecursorListMZ5(const std::vector<PrecursorMZ5>& list)
{
    init(&list[0], list.size());
}

PrecursorListMZ5::PrecursorListMZ5(
        const std::vector<pwiz::msdata::Precursor>& precursors,
        const ReferenceWrite_mz5& wref)
{
    this->len = precursors.size();
    this->list = new PrecursorMZ5[this->len];
    for (size_t i = 0; i < precursors.size(); ++i)
    {
        this->list[i] = PrecursorMZ5(precursors[i], wref);
    }
}

PrecursorListMZ5& PrecursorListMZ5::operator=(const PrecursorListMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] list;
        init(rhs.list, rhs.len);
    }
    return *this;
}

PrecursorListMZ5::~PrecursorListMZ5()
{
    delete[] list;
}

void PrecursorListMZ5::init(const PrecursorMZ5* list, const size_t len)
{
    this->len = len;
    this->list = new PrecursorMZ5[this->len];
    for (unsigned long i = 0; i < this->len; ++i)
    {
        this->list[i] = list[i];
    }
}

void PrecursorListMZ5::fill(std::vector<pwiz::msdata::Precursor>& l,
        const ReferenceRead_mz5& rref, const Connection_mz5& conn)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        pwiz::msdata::Precursor p;
        list[i].fillPrecursor(p, rref, conn);
        l.push_back(p);
    }
}

VarLenType PrecursorListMZ5::getType()
{
    CompType c(PrecursorMZ5::getType());
    VarLenType ret(&c);
    return ret;
}

ChromatogramMZ5::ChromatogramMZ5() :
    id(emptyString()), paramList(), precursor(), productIsolationWindow(),
            dataProcessingRefID(), index(0)
{
}

ChromatogramMZ5::ChromatogramMZ5(const ChromatogramMZ5& chrom)
{
    init(chrom.paramList, chrom.precursor, chrom.productIsolationWindow,
            chrom.dataProcessingRefID, chrom.index, chrom.id);
}

ChromatogramMZ5::ChromatogramMZ5(const pwiz::msdata::Chromatogram& chrom,
        const ReferenceWrite_mz5& wref)
{
    ParamListMZ5 params(chrom.cvParams, chrom.userParams, chrom.paramGroupPtrs,
            wref);
    PrecursorMZ5 precursor(chrom.precursor, wref);
    ParamListMZ5 productisolationwindow(chrom.product.isolationWindow.cvParams,
            chrom.product.isolationWindow.userParams,
            chrom.product.isolationWindow.paramGroupPtrs, wref);
    RefMZ5 refDp;
    if (chrom.dataProcessingPtr.get())
    {
        RefMZ5 tmp(*chrom.dataProcessingPtr.get(), wref);
        refDp = tmp;
    }
    init(params, precursor, productisolationwindow, refDp, chrom.index,
            chrom.id.c_str());
}

ChromatogramMZ5& ChromatogramMZ5::operator =(const ChromatogramMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        init(rhs.paramList, rhs.precursor, rhs.productIsolationWindow,
                rhs.dataProcessingRefID, rhs.index, rhs.id);
    }
    return *this;
}

ChromatogramMZ5::~ChromatogramMZ5()
{
    delete[] id;
}

void ChromatogramMZ5::init(const ParamListMZ5& params,
        const PrecursorMZ5& precursor,
        const ParamListMZ5& productIsolationWindow,
        const RefMZ5& refDataProcessing, const unsigned long index,
        const char* id)
{
    this->paramList = params;
    this->precursor = precursor;
    this->productIsolationWindow = productIsolationWindow;
    this->dataProcessingRefID = refDataProcessing;
    this->index = index;
    this->id = strcpyi(id);
}

pwiz::msdata::Chromatogram* ChromatogramMZ5::getChromatogram(
        const ReferenceRead_mz5& rref, const Connection_mz5& conn)
{
    pwiz::msdata::Chromatogram* c = new pwiz::msdata::Chromatogram();
    std::string sid(id);
    if (!sid.empty())
    {
        c->id = sid;
    }
    c->index = index;
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (*c), rref);
    try
    {
        if (dataProcessingRefID.refID != ULONG_MAX)
        {
            c->dataProcessingPtr = dataProcessingRefID.getDataProcessingPtr(
                    rref);
        }
    } catch (std::out_of_range&)
    {
    }
    this->precursor.fillPrecursor(c->precursor, rref, conn);
    this->productIsolationWindow.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (c->product.isolationWindow),
            rref);
    return c;
}

pwiz::msdata::ChromatogramIdentity ChromatogramMZ5::getChromatogramIdentity()
{
    pwiz::msdata::ChromatogramIdentity ci;
    std::string sid(id);
    if (!sid.empty())
    {
        ci.id = sid;
    }
    ci.index = index;
    return ci;
}

CompType ChromatogramMZ5::getType()
{
    CompType ret(sizeof(ChromatogramMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("precursor", offset, PrecursorMZ5::getType());
    offset += sizeof(PrecursorMZ5);
    ret.insertMember("productIsolationWindow", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("refDataProcessing", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    ret.insertMember("index", offset, PredType::NATIVE_ULONG);
    offset += sizeof(unsigned long);
    return ret;
}

SpectrumMZ5::SpectrumMZ5() :
    id(emptyString()), spotID(emptyString()), paramList(), scanList(),
            precursorList(), productList(), dataProcessingRefID(),
            sourceFileRefID(), index()
{
}

SpectrumMZ5::SpectrumMZ5(const SpectrumMZ5& spectrum)
{
    init(spectrum.paramList, spectrum.scanList, spectrum.precursorList,
            spectrum.productList, spectrum.dataProcessingRefID,
            spectrum.sourceFileRefID, spectrum.index, spectrum.id,
            spectrum.spotID);
}

SpectrumMZ5::SpectrumMZ5(const pwiz::msdata::Spectrum& spectrum,
        const ReferenceWrite_mz5& wref)
{
    wref.addSpectrumIndexPair(spectrum.id, spectrum.index);
    RefMZ5 refDataProcessing;
    if (spectrum.dataProcessingPtr.get())
    {
        RefMZ5 tmp(*spectrum.dataProcessingPtr.get(), wref);
        refDataProcessing = tmp;
    }
    RefMZ5 refSourceFile;
    if (spectrum.sourceFilePtr.get())
    {
        RefMZ5 tmp(*spectrum.sourceFilePtr.get(), wref);
        refSourceFile = tmp;
    }
    this->paramList = ParamListMZ5(spectrum.cvParams, spectrum.userParams,
            spectrum.paramGroupPtrs, wref);
    this->scanList = ScansMZ5(spectrum.scanList, wref);
    this->precursorList = PrecursorListMZ5(spectrum.precursors, wref);
    this->productList = ParamListsMZ5(spectrum.products, wref);
    this->dataProcessingRefID = refDataProcessing;
    this->sourceFileRefID = refSourceFile;
    this->index = spectrum.index;
    this->id = strcpyi(spectrum.id.c_str());
    this->spotID = strcpyi(spectrum.spotID.c_str());
}

SpectrumMZ5& SpectrumMZ5::operator=(const SpectrumMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        delete[] spotID;
        init(rhs.paramList, rhs.scanList, rhs.precursorList, rhs.productList,
                rhs.dataProcessingRefID, rhs.sourceFileRefID, rhs.index,
                rhs.id, rhs.spotID);
    }
    return *this;
}

SpectrumMZ5::~SpectrumMZ5()
{
    delete[] id;
    delete[] spotID;
}

void SpectrumMZ5::init(const ParamListMZ5& params, const ScansMZ5& scanList,
        const PrecursorListMZ5& precursors,
        const ParamListsMZ5& productIonIsolationWindows,
        const RefMZ5& refDataProcessing, const RefMZ5& refSourceFile,
        const unsigned long index, const char* id, const char* spotID)
{
    this->paramList = params;
    this->scanList = scanList;
    this->precursorList = precursors;
    this->productList = productIonIsolationWindows;
    this->dataProcessingRefID = refDataProcessing;
    this->sourceFileRefID = refSourceFile;
    this->index = index;
    this->id = strcpyi(id);
    this->spotID = strcpyi(spotID);
}

pwiz::msdata::Spectrum* SpectrumMZ5::getSpectrum(const ReferenceRead_mz5& rref, const Connection_mz5& conn)
{
    pwiz::msdata::Spectrum* s = new pwiz::msdata::Spectrum();
    std::string sid = id;
    if (!sid.empty())
    {
        rref.addSpectrumIndexPair(sid, index);
        s->id = sid;
    }
    std::string sspotID(spotID);
    s->spotID = sspotID;
    s->index = index;

    try
    {
        if (dataProcessingRefID.refID != ULONG_MAX)
        {
            s->dataProcessingPtr = dataProcessingRefID.getDataProcessingPtr(
                    rref);
        }
    } catch (std::out_of_range&)
    {
    }

    try
    {
        if (sourceFileRefID.refID != ULONG_MAX)
        {
            s->sourceFilePtr = sourceFileRefID.getSourceFilePtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }

    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (*s), rref);
    this->precursorList.fill(s->precursors, rref, conn);
    this->productList.fill(s->products, rref);
    this->scanList.fill(s->scanList, rref);

    return s;
}

pwiz::msdata::SpectrumIdentity SpectrumMZ5::getSpectrumIdentity()
{
    pwiz::msdata::SpectrumIdentity si;
    std::string sid = id;
    if (!sid.empty())
    {
        si.id = sid;
    }
    std::string sspotID(spotID);
    si.spotID = sspotID;
    si.index = index;
    return si;
}

void SpectrumMZ5::fillSpectrumIdentity(pwiz::msdata::SpectrumIdentity& si)
{
    std::string sid = id;
    if (!sid.empty())
    {
        si.id = sid;
    }
    std::string sspotID(spotID);
    si.spotID = sspotID;
    si.index = index;
}

CompType SpectrumMZ5::getType()
{
    CompType ret(sizeof(SpectrumMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("spotID", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("scanList", offset, ScansMZ5::getType());
    offset += sizeof(ScansMZ5);
    ret.insertMember("precursors", offset, PrecursorListMZ5::getType());
    offset += sizeof(PrecursorListMZ5);
    ret.insertMember("products", offset, ParamListsMZ5::getType());
    offset += sizeof(ParamListsMZ5);
    ret.insertMember("refDataProcessing", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    ret.insertMember("refSourceFile", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    ret.insertMember("index", offset, PredType::NATIVE_ULONG);
    offset += PredType::NATIVE_ULONG.getSize();
    return ret;
}

ScanMZ5::ScanMZ5() :
    externalSpectrumID(emptyString()), paramList(), scanWindowList(),
            instrumentConfigurationRefID(), sourceFileRefID(), spectrumRefID()
{
}

ScanMZ5::ScanMZ5(const ScanMZ5& scan)
{
    init(scan.paramList, scan.scanWindowList,
            scan.instrumentConfigurationRefID, scan.sourceFileRefID,
            scan.spectrumRefID, scan.externalSpectrumID);
}

ScanMZ5::ScanMZ5(const pwiz::msdata::Scan& scan, const ReferenceWrite_mz5& wref)
{
    ParamListMZ5 params(scan.cvParams, scan.userParams, scan.paramGroupPtrs,
            wref);
    ParamListsMZ5 scanWindows(scan.scanWindows, wref);
    RefMZ5 refInstrument;
    if (scan.instrumentConfigurationPtr.get())
    {
        RefMZ5 tmp(*scan.instrumentConfigurationPtr.get(), wref);
        refInstrument = tmp;
    }
    RefMZ5 refSourceFile;
    if (scan.sourceFilePtr.get())
    {
        RefMZ5 tmp(*scan.sourceFilePtr.get(), wref);
        refSourceFile = tmp;
    }
    RefMZ5 refspectrum;
    if (!scan.spectrumID.empty())
    {
        RefMZ5 tmp(scan.spectrumID, wref);
        refspectrum = tmp;
    }
    init(params, scanWindows, refInstrument, refSourceFile, refspectrum,
            scan.externalSpectrumID.c_str());
}

ScanMZ5& ScanMZ5::operator=(const ScanMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] externalSpectrumID;
        init(rhs.paramList, rhs.scanWindowList,
                rhs.instrumentConfigurationRefID, rhs.sourceFileRefID,
                rhs.spectrumRefID, rhs.externalSpectrumID);
    }
    return *this;
}

ScanMZ5::~ScanMZ5()
{
    delete[] externalSpectrumID;
}

void ScanMZ5::init(const ParamListMZ5& params,
        const ParamListsMZ5& scanWindowList, const RefMZ5& refInstrument,
        const RefMZ5& refSourceFile, const RefMZ5& refSpectrum,
        const char* externalSpectrumID)
{
    this->paramList = params;
    this->scanWindowList = scanWindowList;
    this->instrumentConfigurationRefID = refInstrument;
    this->sourceFileRefID = refSourceFile;
    this->spectrumRefID = refSpectrum;
    this->externalSpectrumID = strcpyi(externalSpectrumID);
}

void ScanMZ5::fill(pwiz::msdata::Scan& s, const ReferenceRead_mz5& rref)
{
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (s), rref);

    this->scanWindowList.fill(s.scanWindows, rref);

    try
    {
        if (instrumentConfigurationRefID.refID != ULONG_MAX)
        {
            s.instrumentConfigurationPtr
                    = instrumentConfigurationRefID.getInstrumentPtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }
    try
    {
        if (sourceFileRefID.refID != ULONG_MAX)
        {
            s.sourceFilePtr = sourceFileRefID.getSourceFilePtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }

    try
    {
        if (spectrumRefID.refID != ULONG_MAX)
        {
            s.spectrumID = rref.getSpectrumId(spectrumRefID.refID);
        }
    } catch (std::out_of_range&)
    {
    }

    std::string sexternal(externalSpectrumID);
    s.externalSpectrumID = sexternal;
}

CompType ScanMZ5::getType()
{
    CompType ret(sizeof(ScanMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("externalSpectrumID", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5Data);
    ret.insertMember("scanWindowList", offset, ParamListsMZ5::getType());
    offset += sizeof(ParamListsMZ5);
    ret.insertMember("refInstrumentConfiguration", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    ret.insertMember("refSourceFile", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    ret.insertMember("refSpectrum", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5Data);
    return ret;
}

ScanListMZ5::ScanListMZ5() :
    len(0), list(0)
{
}

ScanListMZ5::ScanListMZ5(const ScanListMZ5& scans)
{
    init(scans.list, scans.len);
}

ScanListMZ5::ScanListMZ5(const std::vector<ScanMZ5>& scans)
{
    init(&scans[0], scans.size());
}

ScanListMZ5::ScanListMZ5(const std::vector<pwiz::msdata::Scan>& scans,
        const ReferenceWrite_mz5& wref)
{
    this->len = scans.size();
    this->list = new ScanMZ5[this->len];
    for (size_t i = 0; i < scans.size(); ++i)
    {
        this->list[i] = ScanMZ5(scans[i], wref);
    }
}

ScanListMZ5& ScanListMZ5::operator=(const ScanListMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] list;
        init(rhs.list, rhs.len);
    }
    return *this;
}

ScanListMZ5::~ScanListMZ5()
{
    delete[] list;
}

void ScanListMZ5::init(const ScanMZ5* list, const size_t len)
{
    this->len = len;
    this->list = new ScanMZ5[this->len];
    for (unsigned long i = 0; i < this->len; ++i)
    {
        this->list[i] = list[i];
    }
}

void ScanListMZ5::fill(std::vector<pwiz::msdata::Scan>& l,
        const ReferenceRead_mz5& rref)
{
    l.reserve(static_cast<hsize_t> (len));
    for (unsigned long i = 0; i < len; ++i)
    {
        pwiz::msdata::Scan s;
        l.push_back(s);
        list[i].fill(l[i], rref);
    }
}

VarLenType ScanListMZ5::getType()
{
    CompType c = ScanMZ5::getType();
    VarLenType ret(&c);
    return ret;
}

ScansMZ5::ScansMZ5() :
    paramList(), scanList()
{
}

ScansMZ5::ScansMZ5(const ScansMZ5& scans)
{
    init(scans.paramList, scans.scanList);
}

ScansMZ5::ScansMZ5(const pwiz::msdata::ScanList& scans,
        const ReferenceWrite_mz5& wref)
{
    this->paramList = ParamListMZ5(scans.cvParams, scans.userParams,
            scans.paramGroupPtrs, wref);
    this->scanList = ScanListMZ5(scans.scans, wref);
}

ScansMZ5& ScansMZ5::operator=(const ScansMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.paramList, rhs.scanList);
    }
    return *this;
}

ScansMZ5::~ScansMZ5()
{
}

void ScansMZ5::init(const ParamListMZ5& params, const ScanListMZ5& scanList)
{
    this->paramList = params;
    this->scanList = scanList;
}

void ScansMZ5::fill(pwiz::msdata::ScanList& sl, const ReferenceRead_mz5& rref)
{
    this->scanList.fill(sl.scans, rref);
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (sl), rref);
}

CompType ScansMZ5::getType()
{
    CompType ret(sizeof(ScansMZ5));
    size_t offset = 0;
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5);
    ret.insertMember("scanList", offset, ScanListMZ5::getType());
    return ret;
}

RunMZ5::RunMZ5() :
    id(emptyString()), startTimeStamp(emptyString()), fid(emptyString()), facc(
            emptyString()), paramList(), defaultSpectrumDataProcessingRefID(),
            defaultChromatogramDataProcessingRefID(),
            defaultInstrumentConfigurationRefID(), sourceFileRefID(),
            sampleRefID()
{
}

RunMZ5::RunMZ5(const RunMZ5& run)
{
    init(run.paramList, run.defaultSpectrumDataProcessingRefID,
            run.defaultChromatogramDataProcessingRefID,
            run.defaultInstrumentConfigurationRefID, run.sourceFileRefID,
            run.sampleRefID, run.id, run.startTimeStamp, run.fid, run.facc);
}

RunMZ5::RunMZ5(const pwiz::msdata::Run& run, const std::string fid,
        const std::string facc, const ReferenceWrite_mz5& wref)
{
    ParamListMZ5 params(run.cvParams, run.userParams, run.paramGroupPtrs, wref);
    RefMZ5 refSpectrumDP;
    if (run.spectrumListPtr.get())
    {
        if (run.spectrumListPtr.get()->dataProcessingPtr().get())
        {
            RefMZ5 tmp(*run.spectrumListPtr.get()->dataProcessingPtr().get(),
                    wref);
            refSpectrumDP = tmp;
        }
    }
    RefMZ5 refChromatogramDP;
    if (run.chromatogramListPtr.get())
    {
        if (run.chromatogramListPtr.get()->dataProcessingPtr().get())
        {
            RefMZ5 tmp(
                    *run.chromatogramListPtr.get()->dataProcessingPtr().get(),
                    wref);
            refChromatogramDP = tmp;
        }
    }
    RefMZ5 refDI;
    if (run.defaultInstrumentConfigurationPtr.get())
    {
        RefMZ5 tmp(*run.defaultInstrumentConfigurationPtr.get(), wref);
        refDI = tmp;
    }
    RefMZ5 refSF;
    if (run.defaultSourceFilePtr.get())
    {
        RefMZ5 tmp(*run.defaultSourceFilePtr.get(), wref);
        refSF = tmp;
    }
    RefMZ5 refS;
    if (run.samplePtr.get())
    {
        RefMZ5 tmp(*run.samplePtr.get(), wref);
        refS = tmp;
    }
    init(params, refSpectrumDP, refChromatogramDP, refDI, refSF, refS,
            run.id.c_str(), run.startTimeStamp.c_str(), fid.c_str(),
            facc.c_str());
}

RunMZ5& RunMZ5::operator=(const RunMZ5& rhs)
{
    if (this != &rhs)
    {
        delete[] id;
        delete[] startTimeStamp;
        delete[] fid;
        delete[] facc;
        init(rhs.paramList, rhs.defaultSpectrumDataProcessingRefID,
                rhs.defaultChromatogramDataProcessingRefID,
                rhs.defaultInstrumentConfigurationRefID, rhs.sourceFileRefID,
                rhs.sampleRefID, rhs.id, rhs.startTimeStamp, rhs.fid, rhs.facc);
    }
    return *this;
}

RunMZ5::~RunMZ5()
{
    delete[] id;
    delete[] startTimeStamp;
    delete[] fid;
    delete[] facc;
}

void RunMZ5::init(const ParamListMZ5& params, const RefMZ5& refSpectrumDP,
        const RefMZ5& refChromatogramDP, const RefMZ5& refDefaultInstrument,
        const RefMZ5& refSourceFile, const RefMZ5& refSample, const char* id,
        const char* startTimeStamp, const char* fid, const char* facc)
{
    this->paramList = params;
    this->defaultSpectrumDataProcessingRefID = refSpectrumDP;
    this->defaultChromatogramDataProcessingRefID = refChromatogramDP;
    this->defaultInstrumentConfigurationRefID = refDefaultInstrument;
    this->sourceFileRefID = refSourceFile;
    this->sampleRefID = refSample;
    this->id = strcpyi(id);
    this->startTimeStamp = strcpyi(startTimeStamp);
    this->fid = strcpyi(fid);
    this->facc = strcpyi(facc);
}

void RunMZ5::addInformation(pwiz::msdata::Run& r, const ReferenceRead_mz5& rref)
{
    std::string sid(id);
    if (!sid.empty())
    {
        r.id = sid;
    }
    std::string sstart(startTimeStamp);
    r.startTimeStamp = sstart;
    this->paramList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (r), rref);
    try
    {
        if (sourceFileRefID.refID != ULONG_MAX)
        {
            r.defaultSourceFilePtr = sourceFileRefID.getSourceFilePtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }
    try
    {
        if (defaultInstrumentConfigurationRefID.refID != ULONG_MAX)
        {
            r.defaultInstrumentConfigurationPtr
                    = defaultInstrumentConfigurationRefID.getInstrumentPtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }
    try
    {
        if (sampleRefID.refID != ULONG_MAX)
        {
            r.samplePtr = sampleRefID.getSamplePtr(rref);
        }
    } catch (std::out_of_range&)
    {
    }
}

CompType RunMZ5::getType()
{
    CompType ret(sizeof(RunMZ5));
    StrType stringtype = getStringType();
    size_t offset = 0;
    ret.insertMember("id", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("startTimeStamp", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("fid", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("facc", offset, stringtype);
    offset += stringtype.getSize();
    ret.insertMember("params", offset, ParamListMZ5::getType());
    offset += sizeof(ParamListMZ5);
    ret.insertMember("refSpectrumDP", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5);
    ret.insertMember("refChromatogramDP", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5);
    ret.insertMember("refDefaultInstrument", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5);
    ret.insertMember("refSourceFile", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5);
    ret.insertMember("refSample", offset, RefMZ5::getType());
    offset += sizeof(RefMZ5);
    return ret;
}

BinaryDataMZ5::BinaryDataMZ5() :
    xParamList(), yParamList(), xDataProcessingRefID(), yDataProcessingRefID()
{
}

BinaryDataMZ5::BinaryDataMZ5(const BinaryDataMZ5& bd)
{
    init(bd.xParamList, bd.yParamList, bd.xDataProcessingRefID,
            bd.yDataProcessingRefID);
}

BinaryDataMZ5::BinaryDataMZ5(const pwiz::msdata::BinaryDataArray& bdax,
        const pwiz::msdata::BinaryDataArray& bday,
        const ReferenceWrite_mz5& wref)
{
    RefMZ5 xref;
    if (bdax.dataProcessingPtr.get())
    {
        RefMZ5 tmp(*bdax.dataProcessingPtr.get(), wref);
        xref = tmp;
    }
    RefMZ5 yref;
    if (bday.dataProcessingPtr.get())
    {
        RefMZ5 tmp(*bday.dataProcessingPtr.get(), wref);
        yref = tmp;
    }
    init(
            ParamListMZ5(bdax.cvParams, bdax.userParams, bdax.paramGroupPtrs,
                    wref), ParamListMZ5(bday.cvParams, bday.userParams,
                    bday.paramGroupPtrs, wref), xref, yref);
}

BinaryDataMZ5& BinaryDataMZ5::operator=(const BinaryDataMZ5& rhs)
{
    if (this != &rhs)
    {
        init(rhs.xParamList, rhs.yParamList, rhs.xDataProcessingRefID,
                rhs.yDataProcessingRefID);
    }
    return *this;
}

BinaryDataMZ5::~BinaryDataMZ5()
{
}

bool BinaryDataMZ5::empty() {
    return this->xParamList.empty()
            && this->yParamList.empty()
            && this->xDataProcessingRefID.refID == ULONG_MAX
            && this->yDataProcessingRefID.refID == ULONG_MAX;
}

void BinaryDataMZ5::init(const ParamListMZ5& xParams,
        const ParamListMZ5& yParams, const RefMZ5& refDPx, const RefMZ5& refDPy)
{
    this->xParamList = xParams;
    this->yParamList = yParams;
    this->xDataProcessingRefID = refDPx;
    this->yDataProcessingRefID = refDPy;
}

CompType BinaryDataMZ5::getType()
{
    CompType ret(sizeof(BinaryDataMZ5));
    size_t offset = 0;
    ret.insertMember("xParams", offset, ParamListMZ5::getType());
    offset += ParamListMZ5::getType().getSize();
    ret.insertMember("yParams", offset, ParamListMZ5::getType());
    offset += ParamListMZ5::getType().getSize();
    ret.insertMember("xrefDataProcessing", offset, RefMZ5::getType());
    offset += RefMZ5::getType().getSize();
    ret.insertMember("yrefDataProcessing", offset, RefMZ5::getType());
    return ret;
}

void BinaryDataMZ5::fill(pwiz::msdata::BinaryDataArray& bdax,
        pwiz::msdata::BinaryDataArray& bday, const ReferenceRead_mz5& rref)
{
    this->xParamList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (bdax), rref);
    try
    {
        if (xDataProcessingRefID.refID != ULONG_MAX)
        {
            bdax.dataProcessingPtr = xDataProcessingRefID.getDataProcessingPtr(
                    rref);
        }
    } catch (std::out_of_range&)
    {
    }
    this->yParamList.fillParamContainer(
            dynamic_cast<pwiz::msdata::ParamContainer&> (bday), rref);
    try
    {
        if (yDataProcessingRefID.refID != ULONG_MAX)
        {
            bday.dataProcessingPtr = yDataProcessingRefID.getDataProcessingPtr(
                    rref);
        }
    } catch (std::out_of_range&)
    {
    }
}

void BinaryDataMZ5::convert(std::vector<BinaryDataMZ5>& l,
        const pwiz::msdata::SpectrumListPtr& sptr,
        const ReferenceWrite_mz5& wref)
{
    if (sptr.get())
    {
        l.reserve(sptr.get()->size());
        l.resize(sptr.get()->size());
        size_t s = sptr.get()->size();
        pwiz::msdata::SpectrumPtr ptr;
        for (size_t i = 0; i < s; ++i)
        {
            ptr = sptr.get()->spectrum(i, false);
            if (ptr.get() && ptr.get()->getMZArray().get()
                    && ptr.get()->getIntensityArray().get())
            {
                l[i] = BinaryDataMZ5(*ptr.get()->getMZArray().get(),
                        *ptr.get()->getIntensityArray().get(), wref);
            }
        }
    }
}

void BinaryDataMZ5::convert(std::vector<BinaryDataMZ5>& cl,
        const pwiz::msdata::ChromatogramListPtr& cptr,
        const ReferenceWrite_mz5& wref)
{
    if (cptr.get())
    {
        cl.reserve(cptr.get()->size());
        cl.resize(cptr.get()->size());
        size_t s = cptr.get()->size();
        pwiz::msdata::ChromatogramPtr ptr;
        for (size_t i = 0; i < s; ++i)
        {
            ptr = cptr.get()->chromatogram(i, false);
            if (ptr.get() && ptr.get()->getTimeArray().get()
                    && ptr.get()->getIntensityArray().get())
            {
                cl[i] = BinaryDataMZ5(*ptr.get()->getTimeArray().get(),
                        *ptr.get()->getIntensityArray().get(), wref);
            }
        }
    }
}

}
}
}
