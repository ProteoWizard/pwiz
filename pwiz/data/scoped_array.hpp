//
// scoped_array.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
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

