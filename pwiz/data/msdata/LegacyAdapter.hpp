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
//


#ifndef _LEGACYADAPTER_HPP_
#define _LEGACYADAPTER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include "boost/shared_ptr.hpp"


namespace pwiz { namespace data { class CVTranslator; } }


namespace pwiz {
namespace msdata {


using pwiz::data::CVTranslator;


///
/// interface for legacy access to Instrument
/// 
/// mzXML/RAMP encode instrument information as 5 strings:
/// manufacturer, model, ionisation, analyzer, detector 
///
/// In mzML, the equivalent information is encoded as cvParams in
/// various locations in the <instrument> element.  One important 
/// difference is that the manufacturer information is implicit in 
/// the CV term used to encode the model.
/// 
/// The "set" methods use CVTranslator to translate the string(s) to an 
/// appropriate cvParam.  If no CV term can be found, the information is
/// encoded as a userParam.
/// 
/// The "get" methods look for the cvParam first, then the userParam.
///
class PWIZ_API_DECL LegacyAdapter_Instrument
{
    public:

    LegacyAdapter_Instrument(InstrumentConfiguration& instrumentConfiguration, 
                             const CVTranslator& cvTranslator);

    std::string manufacturer() const;
    std::string model() const;
    void manufacturerAndModel(const std::string& valueManufacturer,
                              const std::string& valueModel);

    std::string ionisation() const;
    void ionisation(const std::string& value);

    std::string analyzer() const;
    void analyzer(const std::string& value);

    std::string detector() const;
    void detector(const std::string& value);

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    LegacyAdapter_Instrument(LegacyAdapter_Instrument&);
    LegacyAdapter_Instrument& operator=(LegacyAdapter_Instrument&);
};


///
/// interface for legacy access to Software
///
/// mzXML:
/// <software type="acquisition" name="XCalibur" version="4.20">
///
/// MSData:
///   name: Software::cvParams(0)
///   version: Software::version
///   type: DataProcessing::processingMethods[?].userParams[?]
///
/// Note: setting 'type' may create a DataProcessing object, which needs an id.
/// Since the id is generated from 'name', it is an error to set 'type' before
/// setting 'name'.
///
class PWIZ_API_DECL LegacyAdapter_Software
{
    public:

    LegacyAdapter_Software(SoftwarePtr software, MSData& msd, const CVTranslator& cvTranslator);

    std::string name() const;
    void name(const std::string& value);

    std::string version() const;
    void version(const std::string& value);

    std::string type() const;
    void type(const std::string& value);

    private:
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    LegacyAdapter_Software(LegacyAdapter_Software&);
    LegacyAdapter_Software& operator=(LegacyAdapter_Software&);
};


} // namespace msdata
} // namespace pwiz


#endif // _LEGACYADAPTER_HPP_

