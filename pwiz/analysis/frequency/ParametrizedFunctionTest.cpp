//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#include "DerivativeTest.hpp"
#include "TruncatedLorentzian.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::frequency;


ostream* os_ = 0;


void testDifferenceQuotient()
{
    if (os_) *os_ << "****************************************************\n";
    if (os_) *os_ << "testDifferenceQuotient()\n";

    using namespace DerivativeTest;

    class TestFunction : public VectorFunction<double>
    {
        public:

        // f(x,y) = (x^2, xy, y^2)

        virtual unsigned int argumentCount() const {return 2;}
        virtual unsigned int valueCount() const {return 3;}

        virtual ublas::vector<double> operator()(ublas::vector<double> x) const
        {
            if (x.size() != argumentCount())
                throw logic_error("[TestFunction::()] Wrong argument count.");

            ublas::vector<double> result(3);
            result(0) = x(0)*x(0);
            result(1) = x(0)*x(1);
            result(2) = x(1)*x(1);
            return result;
        }
    };


    TestFunction f;
    ublas::vector<double> args(2);
    args(0) = 5; args(1) = 7;
    if (os_) *os_ << "f(5,7): " << f(args) << endl;

    if (os_) f.printDifferenceQuotientSequence(args, *os_);

    // f'(x,y) = ((2x, y, 0), (0, x, 2y))
    // f'(5,7) = ((10, 7, 0), (0, 5, 14))

    ublas::matrix<double> d(2,3);
    d(0,0) = 10;
    d(0,1) = 7;
    d(0,2) = 0;
    d(1,0) = 0;
    d(1,1) = 5;
    d(1,2) = 14;

    const double delta = 1e-9;
    const double epsilon = 1e-5;
    unit_assert_matrices_equal(d, f.differenceQuotient(args,delta), epsilon);
}


class ParametrizedCosine: public ParametrizedFunction<double>
{
    // F(x) = Acos(Bx), p = <A, B>

    public:

    virtual unsigned int parameterCount() const {return 2;}

    virtual double operator()(double x, const ublas::vector<double>& p) const
    {
        preprocess(x,p);
        return A_*cosBx_;
    }


    virtual ublas::vector<double> dp(double x, const ublas::vector<double>& p) const
    {
        preprocess(x,p);
        ublas::vector<double> v(2);
        v(0) = cosBx_;          // dF/dA
        v(1) = -A_*x_*sinBx_;   // dF/dB
        return v;
    }


    virtual ublas::matrix<double> dp2(double x, const ublas::vector<double>& p) const
    {
        preprocess(x,p);
        ublas::matrix<double> m(2,2);
        m(0,0) = 0;                     // d2F/dA2
        m(1,0) = m(0,1) = -x_*sinBx_;   // d2F/dAdB
        m(1,1) = -A_*x_*x_*cosBx_;      // d2F/dB2
        return m;
    }


    private:

    void preprocess(double x, const ublas::vector<double>& p) const
    {
        // check parameter size
        if (p.size() != parameterCount())
            throw logic_error("[Parabola] Wrong parameter size.");

        // cache arguments and do expensive calculations
        if (x!=x_ || p(0)!=A_ || p(1)!=B_)
        {
            x_ = x;
            A_ = p(0);
            B_ = p(1);
            sinBx_ = sin(B_*x);
            cosBx_ = cos(B_*x);
        }
        else
        {
            //if (os_) *os_ << "cache hit!\n";
        }
    }

    // cached values
    mutable double x_;
    mutable double A_;
    mutable double B_;
    mutable double sinBx_;
    mutable double cosBx_;
};


void testDerivatives()
{
    if (os_) *os_ << "****************************************************\n";
    if (os_) *os_ << "testDerivatives()\n";

    ParametrizedCosine f;

    ublas::vector<double> p(2);
    p(0) = 5;
    p(1) = M_PI/4;

    for (int i=0; i<8; i++)
        DerivativeTest::testDerivatives(f, i, p, os_);
}


void testErrorFunction()
{
    if (os_) *os_ << "****************************************************\n";
    if (os_) *os_ << "testErrorFunction()\n";

    ParametrizedCosine f;

    ublas::vector<double> p(2);
    p(0) = 4;
    p(1) = 30;

    ParametrizedCosine::ErrorFunction::Data data;
    typedef ParametrizedCosine::ErrorFunction::Datum Datum;
    data.push_back(Datum(0,3));
    data.push_back(Datum(M_PI/2,0));

    ParametrizedCosine::ErrorFunction e(f, data);
    if (os_) *os_ << "error: " << e(p) << endl;

    DerivativeTest::testDerivatives<double>(e, p, os_);

    if (os_) *os_ << "8*pi^2: " << 8*M_PI*M_PI << endl;
}


void testErrorLorentzian()
{
    if (os_) *os_ << "****************************************************\n";
    if (os_) *os_ << "testErrorLorentzian()\n";

    TruncatedLorentzian f(1);

    ublas::vector<double> p(4);
    p(TruncatedLorentzian::AlphaR) = 1;
    p(TruncatedLorentzian::AlphaI) = 5;
    p(TruncatedLorentzian::Tau) = 2;
    p(TruncatedLorentzian::F0) = 0;

    TruncatedLorentzian::ErrorFunction::Data data;
    typedef TruncatedLorentzian::ErrorFunction::Datum Datum;
    data.push_back(Datum(0,3));
    data.push_back(Datum(M_PI/2,0));

    TruncatedLorentzian::ErrorFunction e(f, data);
    if (os_) *os_ << "error: " << e(p) << endl;

    DerivativeTest::testDerivatives< complex<double> >(e, p, os_);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "ParametrizedFunctionTest\n";
        testDifferenceQuotient();
        testDerivatives();
        testErrorFunction();
        testErrorLorentzian();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

