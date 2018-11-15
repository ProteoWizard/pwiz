//
// $Id$
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
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


#ifndef _RAWFILEVALUES_H_
#define _RAWFILEVALUES_H_


#include "RawFile.h"
#include "XRawFile2.tlh"
#include <map>


namespace pwiz {
namespace vendor_api {
namespace Thermo {
namespace RawFileValues {


template<typename id_type>
struct ValueTypeTraits;


template<>
struct ValueTypeTraits<ValueID_Long>
{
    typedef long return_type;
    typedef HRESULT (XRawfile::IXRawfile::*function_type)(long*);
};


template<>
struct ValueTypeTraits<ValueID_Double>
{
    typedef double return_type;
    typedef HRESULT (XRawfile::IXRawfile::*function_type)(double*);
};


template<>
struct ValueTypeTraits<ValueID_String>
{
    typedef std::string return_type;
    typedef HRESULT (XRawfile::IXRawfile::*function_type)(BSTR*);
};


template<typename id_type>
struct ValueDescriptor
{
    id_type id;
    typename ValueTypeTraits<id_type>::function_type function;
    const char* name;
};


template<typename id_type>
struct ValueData
{
    ValueData();
    std::vector< ValueDescriptor<id_type> > descriptors_;
    typedef std::map<id_type, const ValueDescriptor<id_type>*> map_type;
    map_type descriptorMap_;
};


template<typename id_type>
const ValueDescriptor<id_type>* descriptor(id_type id)
{
    static ValueData<id_type> valueData;
    const ValueDescriptor<id_type>* d = valueData.descriptorMap_[id];
    if (d)
        return d;

    ostringstream os;
    os << "RawFile.cpp::descriptor(): null descriptor, " << typeid(id).name() << "=" << id;
    throw RawEgg(os.str());
}


} // namespace values
} // namespace Thermo
} // namespace vendor_api
} // namespace pwiz 


#endif // _RAWFILEVALUES_H_
