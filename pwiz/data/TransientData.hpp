//
// TransientData.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _TRANSIENTDATA_HPP_
#define _TRANSIENTDATA_HPP_


#include <string>
#include <vector>
#include <memory>


namespace pwiz {
namespace data {


class FrequencyData;


/// class for accessing/creating MIDAS format FT transient data
class TransientData
{
    public:


    /// \name Instantiation 
    //@{
    TransientData();
    TransientData(const std::string& filename);
    ~TransientData();
    //@}


    /// \name Header values
    //@{
    double startTime() const;
    void startTime(double value);

    double observationDuration() const;
    void observationDuration(double value);

    double A() const;
    void A(double value);
    
    double B() const;
    void B(double value);

    double bandwidth() const;
    double magneticField() const;
    //@}
    

    /// \name Data access 
    //@{
    const std::vector<double>& data() const;
    std::vector<double>& data();
    //@}


    /// \name Auxilliary functions 
    //@{

    /// fills in FrequencyData with fft of the transient data
    void computeFFT(unsigned int zeroPadding, FrequencyData& result) const;

    /// interface for a signal function
    class Signal
    {
        public:
        virtual double operator()(double t) const = 0;
        virtual ~Signal(){}
    };

    /// add signal to transient data  
    void add(const Signal& signal);
    //@}


    /// \name Output functions 
    //@{
    enum Format {Text, Binary};
    void write(std::ostream& os, Format format = Binary);
    void write(const std::string& filename, Format format = Binary);
    //@}


    private:
    struct Impl;
    std::auto_ptr<Impl> impl_;

    // no copying
    TransientData(const TransientData&);
    TransientData& operator=(const TransientData&);
};


} // namespace data 
} // namespace pwiz


#endif // _TRANSIENTDATA_HPP_

