//
// Timer.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _TIMER_H_
#define _TIMER_H_


#include <ctime>
#include <iostream>


namespace pwiz {
namespace util {


class Timer
{
    public:
    Timer() {time(&start_);}
    double elapsed() const {time_t now; time(&now); return difftime(now, start_);}

    private:
    time_t start_;
    time_t finish_;
};


} // namespace util
} // namespace pwiz


#endif //_TIMER_H_
