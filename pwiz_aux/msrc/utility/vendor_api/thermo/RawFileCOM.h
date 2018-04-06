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


#ifndef _RAWFILECOM_H_
#define _RAWFILECOM_H_


#include "RawFile.h"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace vendor_api {
namespace Thermo {


class ManagedSafeArray
{
    public:
    ManagedSafeArray()
    :   size_(0),
        array_(0),
        data_(0)
    {
    }

    ManagedSafeArray(VARIANT& v, long size)
    :   size_(size),
        array_(0),
        data_(0)
    {
        if (size_ > 0)
        {
            if (!(v.vt & VT_ARRAY) || !v.parray)
                throw RawEgg("ManagedSafeArray(): VARIANT error.");

            HRESULT hr = SafeArrayAccessData(v.parray, &data_);
            if (FAILED(hr) || !data_)
                throw RawEgg("ManagedSafeArray(): Data access error.");
        }

        array_ = v.parray;
        v.parray = NULL;
    }

    ~ManagedSafeArray()
    {
        if (size_ > 0)
        {
            SafeArrayUnaccessData(array_);
            SafeArrayDestroy(array_);
        }
    }

    long size() const {return size_;}
    void* data() const {return data_;}

    private:
    long size_;
    SAFEARRAY FAR* array_;
    void* data_;

    ManagedSafeArray(ManagedSafeArray&);
    ManagedSafeArray& operator=(ManagedSafeArray&);
};


class VariantStringArray : public StringArray
{
    public:
    VariantStringArray()
    {
    }

    VariantStringArray(VARIANT& v, long size)
    :   msa_(v, size)
    {
        if (v.vt != (VT_ARRAY | VT_BSTR))
            throw RawEgg("VariantStringArray(): VARIANT error.");
    }

    virtual int size() const {return msa_.size();}

    virtual string item(int index) const
    {
        if (index<0 || index>=msa_.size())
            throw RawEgg("VariantStringArray: Array out of bounds.");

        BSTR* p = (BSTR*)msa_.data();
        _bstr_t bstr(p[index]);
        return (const char*)(bstr);
    }

    private:
    ManagedSafeArray msa_;

    VariantStringArray(VariantStringArray&);
    VariantStringArray& operator=(VariantStringArray&);
};

/*
class VariantLabelValueArray : public LabelValueArray
{
    public:

    VariantLabelValueArray(VARIANT& variantLabels, VARIANT& variantValues, long size)
    :   labels_(variantLabels, size),
        values_(variantValues, size)
    {}

    virtual int size() const {return labels_.size();}
    virtual std::string label(int index) const {return labels_.item(index);}
    virtual std::string value(int index) const {return values_.item(index);}

    private:

    VariantStringArray labels_;
    VariantStringArray values_;

    VariantLabelValueArray(VariantLabelValueArray&);
    VariantLabelValueArray& operator=(VariantLabelValueArray&);
};
*/

} // namespace Thermo
} // namespace vendor_api
} // namespace pwiz 


#endif // _RAWFILECOM_H_
