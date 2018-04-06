//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#ifndef _SCOPED_ARRAY_HPP_
#define _SCOPED_ARRAY_HPP_


namespace pwiz {
namespace util {


template <typename T>
class scoped_array
{
    public:

    explicit scoped_array(unsigned long size = 0)
    :   size_(size),
        data_(new T[size_])
    {}

    unsigned long size() const {return size_;}

    T* data() {return data_;}
    const T* data() const {return data_;}

    T* begin() {return data_;}
    const T* begin() const {return data_;}
    T* end() {return data_+size_;}
    const T* end() const {return data_+size_;}

    T& operator[](int index) {return data_[index];} 
    const T& operator[](int index) const {return data_[index];} 

    void resize(unsigned long size)
    {
        T* temp = new T[size];
        delete [] data_;
        data_ = temp;
        size_ = size;
    }

    ~scoped_array() {delete [] data_;}

    private:
    unsigned long size_;
    T* data_;

    scoped_array(scoped_array&);
    scoped_array& operator=(scoped_array&);
};


} // namespace util
} // namespace pwiz


#endif // _SCOPED_ARRAY_HPP_

