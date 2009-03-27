///
/// BinTest.cpp
///

#include "Bin.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace std;
using namespace pwiz;
using namespace pwiz::util;

ostream* os_ = 0;

void test()
{
    if (os_) *os_ << "\n[BinTest.cpp] test() ... \n";
    pair<double,double> a = make_pair(1.5,2);
    pair<double,double> b = make_pair(2.5,3);
    pair<double,double> c = make_pair(3,2.0);

    int a1 = 1;
    int b1 = 2;
    int c1 = 3;

    vector<pair<pair<double,double>, int> > stuf;
    stuf.push_back(make_pair(a,a1));
    stuf.push_back(make_pair(b,b1));
    stuf.push_back(make_pair(c,c1));

    Bin<int> bin(stuf, 4, 4);

    vector<int> v;
    bin.getBinContents(pair<double,double>(1.6,2),v);

    vector<int>::iterator it = v.begin();
    
    if (os_)
        {
            *os_ << "\ntesting Bin::getBinContents ... found: \n";
            for(; it != v.end(); ++it)
                *os_ << *it << endl;

        }

    vector<int> truth;
    truth.push_back(1);
    truth.push_back(2);
    truth.push_back(3);

    unit_assert(v == truth);

    // test getAdjacentBinContents
    Bin<int> smallBins(stuf,0.5,0.5);
    vector<int> v2;
    smallBins.getAdjacentBinContents(pair<double,double>(1,2),v2);
    
    vector<int>::iterator it2 = v2.begin();
    
    unit_assert(find(v2.begin(),v2.end(),1) != v2.end());

    if (os_)
        {
            *os_ << "\ntesting Bin::getAdjacentBinContents ... found: \n";
            for(; it2 != v2.end(); ++it2)
                *os_ << *it2 << endl;

        }

    // test update
    
    int n = 4;
    smallBins.update(n, pair<double,double>(1.5,2));

    vector<int> v3;
    smallBins.getAdjacentBinContents(pair<double,double>(1,2), v3);
    vector<int>::iterator it3 = v3.begin();

    unit_assert(find(v3.begin(),v3.end(),4) != v3.end());

    if (os_)
        {
            *os_ << "\ntesting Bin::update ... found: \n";
            for(; it3 != v3.end(); ++it3)
                *os_ << *it3 << endl;

        }


    // test erase
    smallBins.erase(n, pair<double,double>(1.5,2));
    vector<int> v4;
    smallBins.getAdjacentBinContents(pair<double,double>(1,2), v4);
    vector<int>::iterator it4 = v4.begin();

    unit_assert(find(v4.begin(), v4.end(), 4) == v4.end());

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            test();

        }

    catch (std::exception& e)
        {
            cerr << e.what() << endl;
            return 1;

        }

    catch (...)
        {
            cerr << "Caught unknown exception.\n";
            return 1;

        }

    return 0;

}
