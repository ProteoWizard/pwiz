//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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


#include "LinearLeastSquares.hpp"
#include "extstd/unit.hpp"
#include <iostream>
#include <vector>
#include <string>
#include <fstream>
#include <iterator>
#include <algorithm>


using namespace std;
using namespace pwiz::extstd;
//using namespace pwiz::extmath;
using pwiz::extmath::LinearLeastSquares;
using pwiz::extmath::LinearLeastSquaresType_QR;


//namespace ublas = boost::numeric::ublas;


const double epsilon = 1e-16;


struct Pair
{
    double x;
    double y;

    Pair() : x(0), y(0) {}
};


typedef vector<Pair> Pairs;


istream& operator>>(istream& is, Pair& pair)
{
    is >> pair.x >> pair.y;
    return is;
}


ostream& operator<<(ostream& os, const Pair& pair)
{
    os <<  pair.x << ", " << pair.y;
    return os;
}


vector<Pair> read(const string& filename)
{
    Pairs result;
    ifstream is(filename.c_str());
    if (!is)
        throw runtime_error("can't read file");

    copy(istream_iterator<Pair>(is), istream_iterator<Pair>(), back_inserter(result));

    return result;
}

/*
void testDouble()
{
    cout << "testDouble()\n";

    LinearLeastSquares<> lls;
    ublas::matrix<double> A(2, 2);
    A(0,0) = 1; A(0,1) = 2;
    A(1,0) = 3; A(1,1) = 4;
   
    ublas::vector<double> y(2);
    y(0) = 5;
    y(1) = 11;

    ublas::vector<double> x = lls.solve(A, y);

    cout << "A: " << A << endl;
    cout << "y: " << y << endl;
    cout << "x: " << x << endl;

    unit_assert_equal(x(0), 1., 1e-13);
    unit_assert_equal(x(1), 2., 1e-13);
}
*/


void initializeMatrix(ublas::matrix<double>& A, Pairs pairs)
{
    for (unsigned int i=0; i<A.size1(); i++)
    for (unsigned int j=0; j<A.size2(); j++)
    {
        double x = pairs[i].x;
        A(i,j) = pow(x, (double)j); 
    }
}


void initializeVector(ublas::vector<double>& y, Pairs pairs)
{
    for (unsigned int i=0; i<y.size(); i++)
        y[i] = pairs[i].y;
}


double evaluatePoly(const ublas::vector<double>& a, double x)
{
    double result = 0;
    
    for (unsigned int i=0; i<a.size(); ++i)
        result += a[i] * pow(x,(double)i);

    return result;
}


int main(int argc, char* argv[])
{
    if (argc != 2)
    {
        cout << "usage: error degree\n";
        return 1;
    }

    int degree = atoi(argv[1]);

    Pairs pairs = read("xyvalues.txt");

    ublas::matrix<double> A(pairs.size(), degree+1);
    initializeMatrix(A, pairs); 

    ublas::vector<double> y(pairs.size());
    initializeVector(y, pairs);

    //LinearLeastSquares<LinearLeastSquaresType_QR> lls;
    LinearLeastSquares<> lls;
    ublas::vector<double> a = lls.solve(A, y);

    cout << "# a: " << a << endl;

    double sum = 0;
    double sumSquares = 0;

    for (unsigned int i=0; i<pairs.size(); i++)
    {
        double x = pairs[i].x;
        double y1 = evaluatePoly(a, x); 
        double error = pairs[i].y - y1;
        sum += error;
        sumSquares += error*error;
        cout << x << " " << error << endl; 
    }

    double N = (double)pairs.size();
    double average = sum / N; 
    double variance = sumSquares/N - average*average;

    cout << "# average: " << average << endl;
    cout << "# variance: " << variance << endl;
    cout << "# sd: " << sqrt(variance) << endl;

    return 0;
}


