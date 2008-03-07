#include "HouseholderQR.hpp"

using namespace pwiz::math;

int main()
{
  using namespace boost::numeric::ublas;
  using namespace std;
  matrix<double> A (3,3);
  A(0,0) = 1;
  A(0,1) = 1;
  A(0,2) = 0;
  A(1,1) = 1;
  A(1,0) = 0;
  A(1,2) = 0;
  A(2,2) = 1;
  A(2,0) = 1;
  A(2,1) = 0; 
  cout << "A=" << A << endl;

  cout << "QR decomposition using Householder" << endl;
  matrix<double> Q(3,3), R(3,3);
  HouseholderQR (A,Q,R);
  matrix<double> Z = prod(Q,R) - A;
  float f = norm_1 (Z);
  cout << "Q=" << Q <<endl;
  cout << "R=" << R << endl;
  cout << "|Q*R - A|=" << f << endl;

  return 0;
}

