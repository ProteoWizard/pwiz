//
// RawFileValues.h
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _RAWFILEVALUES_H_
#define _RAWFILEVALUES_H_


#include "RawFile.h"
#import "xdk/XRawFile2.dll" rename_namespace("XRawfile")
#include <map>


namespace pwiz {
namespace raw {
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
    typedef std::map<id_type, ValueDescriptor<id_type>*> map_type;
    static ValueDescriptor<id_type> descriptors_[];
    static map_type descriptorMap_;
};


void initializeMaps();


template<typename id_type>
ValueDescriptor<id_type>* descriptor(id_type id)
{
    ValueDescriptor<id_type>* d = ValueData<id_type>::descriptorMap_[id];
    if (d)
        return d;

    ostringstream os;
    os << "RawFile.cpp::descriptor(): null descriptor, " << typeid(id).name() << "=" << id;
    throw RawEgg(os.str());
}


} // namespace values
} // namespace raw
} // namespace pwiz 


#endif // _RAWFILEVALUES_H_
