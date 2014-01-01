//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

#ifndef _BIN_HPP_
#define _BIN_HPP_

#include "boost/shared_ptr.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>

namespace pwiz{

template <typename T> // T: objects in bins
class Bin
{

public:

    Bin(){}
    Bin(const vector<pair<pair<double,double>, T> >& objects, double binSizeX, double binSizeY);
    Bin(const vector<pair<pair<double,double>, boost::shared_ptr<T> > >& objects, double binSizeX, double binSizeY);
    Bin(const Bin<T>& b) : _allContents(b.getAllContents()), _objects(b.getObjects()),  _data(b.getData()), _binSizeX(b.getBinSizes().first), _binSizeY(b.getBinSizes().second) {}

    double getBinSizeX()
    {
        return _binSizeX;
    }

    double getBinSizeY()
    {
        return _binSizeY;
    }

    void update(const T& t, pair<double,double> coordinates);
    void erase(const T& t, pair<double,double> coordinates);
    void rebin(const double& binSizeX, const double& binSizeY);
    size_t size() const { return _data.size();}
    size_t count(pair<int, int> coordinates) const
    {
        return _data.count(coordinates);
    }
    
    void getBinContents(const pair<int, int>& coordinates, vector<T>& result) const;
    void getBinContents(const pair<double,double>& coordinates, vector<boost::shared_ptr<T> >& result) const;
    void getAdjacentBinContents(pair<double,double> coordinates, vector<boost::shared_ptr<T> >& result) ; // gets bin and all adjacent bin coordinates

    // accessors
    const vector<boost::shared_ptr<T> >& getAllContents() const { return _allContents;}
    const vector<pair<pair<double,double>, boost::shared_ptr<T> > >& getObjects() const { return _objects;}
    const multimap<const pair<int, int>,boost::shared_ptr<T> >& getData() const { return _data;}
    pair<double,double> getBinSizes() const { return make_pair(_binSizeX, _binSizeY);}

    bool operator==(const Bin& that);
    bool operator!=(const Bin& that);

private:

    vector<boost::shared_ptr<T> > _allContents;
    vector<pair<pair<double,double>, boost::shared_ptr<T> > > _objects;

    multimap<const pair<int,int>, boost::shared_ptr<T> > _data;
    double _binSizeX;
    double _binSizeY;
};

///
/// inline implementation
///

template <typename T>
Bin<T>::Bin(const vector<pair<pair<double,double>, T> >& objects, double binSizeX, double binSizeY)
    : _binSizeX(binSizeX), _binSizeY(binSizeY)
{
    typename vector<pair<pair<double,double>,  T> >::const_iterator it = objects.begin();
    for(; it!= objects.end(); ++it)
        {
            int binXCoord = int(floor(it->first.first/_binSizeX));
            int binYCoord = int(floor(it->first.second/_binSizeY));
            
            boost::shared_ptr<T>  t(new T(it->second));
            boost::shared_ptr<T> sp_t(new T(it->second));

            _data.insert(pair<pair<int,int>, boost::shared_ptr<T> >(pair<int,int>(binXCoord,binYCoord), t));
            _allContents.push_back(sp_t);
            _objects.push_back(make_pair(it->first, t));
           
        }

}

template <typename T>
Bin<T>::Bin(const vector<pair<pair<double,double>, boost::shared_ptr<T> > >& objects, double binSizeX, double binSizeY) : _binSizeX(binSizeX), _binSizeY(binSizeY)
{
    typename vector<pair<pair<double,double>, boost::shared_ptr<T> > >::const_iterator it = objects.begin();
    for(; it!= objects.end(); ++it)
        {
            int binXCoord = int(floor(it->first.first/_binSizeX));
            int binYCoord = int(floor(it->first.second/_binSizeY));

            _data.insert(pair<pair<int,int>, boost::shared_ptr<T> >(pair<int,int>(binXCoord,binYCoord), it->second));
            _allContents.push_back(it->second);
            _objects.push_back(*it);

        }

}

template <typename T>
void Bin<T>::update(const T& t, pair<double,double> coordinates)
{    
    int binXCoord = int(floor(coordinates.first/_binSizeX));
    int binYCoord = int(floor(coordinates.second/_binSizeY));

    boost::shared_ptr<T> tp_sp(new T(t));
    const pair<const pair<int,int>, boost::shared_ptr<T> >& entry = make_pair(make_pair(binXCoord,binYCoord), tp_sp);
    _data.insert(entry);
    _objects.push_back(entry);
    _allContents.push_back(tp_sp);

}

template <typename T>
struct SecondIs
{
    T _t;
    SecondIs(const T& t) : _t(t) {}
    bool operator()(pair<const pair<double,double>,boost::shared_ptr<T> > entry) { return (*(entry.second) == _t); }

};

template <typename T>
struct IsObject
{
    T _t;
    IsObject(const T& t) : _t(t) {}
    bool operator()(boost::shared_ptr<T> entry) { return (*entry == _t); }

};

template <typename T>
void Bin<T>::erase(const T& t, pair<double,double> coordinates)
{
    int binXCoord = int(floor(coordinates.first/_binSizeX));
    int binYCoord = int(floor(coordinates.second/_binSizeY));

    pair<int,int> intCoordinates = make_pair(binXCoord, binYCoord);
    pair<typename multimap<const pair<int,int>, boost::shared_ptr<T> >::iterator, typename multimap<const pair<int,int>,boost::shared_ptr<T> >::iterator> its = _data.equal_range(intCoordinates);
    
    typename multimap<const pair<int,int>,boost::shared_ptr<T> >::iterator search_it = find_if(its.first, its.second, SecondIs<T>(t));
    if (search_it != its.second) 
        {
            _data.erase(search_it);

            ////////////////////////
            typename vector<pair<pair<double,double>, boost::shared_ptr<T> > >::iterator objects_eraser = find_if(_objects.begin(), _objects.end(), SecondIs<T>(t));
            _objects.erase(objects_eraser);

            ////////////////////////
            typename vector<boost::shared_ptr<T> >::iterator allContents_erase = find_if(_allContents.begin(), _allContents.end(), IsObject<T>(t));
            _allContents.erase(allContents_erase);
   
        }

    else cerr << "[Bin<T>::erase] Object to erase was not found." << endl;

}

template <typename T>
void Bin<T>::rebin(const double& binSizeX, const double& binSizeY)
{
    Bin<T> rebinned(_objects, binSizeX, binSizeY);
    _data.clear();
    _data = rebinned.getData(); 
    
    _binSizeX = binSizeX;
    _binSizeY = binSizeY;

    return;

}

template <typename T>
void Bin<T>::getBinContents(const pair<int, int>& coordinates,
                            vector<T>& result)  const
{
    pair<typename multimap<const pair<int,int>,boost::shared_ptr<T> >::const_iterator, typename multimap<const pair<int,int>,boost::shared_ptr<T> >::const_iterator> its = _data.equal_range(coordinates);
   
    typename multimap<const pair<int, int>, boost::shared_ptr<T> >::const_iterator it = its.first;
    for(; it != its.second; ++it)
        {            
            result.push_back(*(it->second));

        }
    
    return;

} 

template <typename T>
void Bin<T>::getBinContents(const pair<double,double>& coordinates,
                            vector<boost::shared_ptr<T> >& result) const
{ 
    int binXCoord = int(floor(coordinates.first/_binSizeX));
    int binYCoord = int(floor(coordinates.second/_binSizeY));
    pair<int,int> intCoordinates = make_pair(binXCoord, binYCoord);
   
    pair<typename multimap<const pair<int,int>,boost::shared_ptr<T> >::const_iterator, typename multimap<const pair<int,int>,boost::shared_ptr<T> >::const_iterator> its = _data.equal_range(intCoordinates);
   
    typename multimap<const pair<int, int>, boost::shared_ptr<T> >::const_iterator it = its.first;
    for(; it != its.second; ++it)
        {            
            result.push_back((it->second));

        }
    
    return;

} 

template <typename T>
void Bin<T>::getAdjacentBinContents(pair<double,double> coordinates, vector<boost::shared_ptr<T> >& result) 
{

    // 8 adjacent bins:
    // dasher  | dancer      | prancer
    // blitzen | coordinates | vixen
    // donner  | cupid       | comet

    pair<double,double> dasher = make_pair(coordinates.first - _binSizeX, coordinates.second + _binSizeY);
    pair<double,double> dancer = make_pair(coordinates.first + _binSizeX, coordinates.second);
    pair<double,double> prancer = make_pair(coordinates.first + _binSizeX, coordinates.second + _binSizeY);
    pair<double,double> vixen = make_pair(coordinates.first, coordinates.second + _binSizeY);
    pair<double,double> comet = make_pair(coordinates.first + _binSizeX, coordinates.second - _binSizeY);
    pair<double,double> cupid = make_pair(coordinates.first, coordinates.second - _binSizeY);
    pair<double,double> donner = make_pair(coordinates.first - _binSizeX, coordinates.second - _binSizeY);
    pair<double,double> blitzen = make_pair(coordinates.first - _binSizeX, coordinates.second);

    vector<pair<double,double> > sleigh;
    sleigh.push_back(dasher);
    sleigh.push_back(dancer);
    sleigh.push_back(prancer);
    sleigh.push_back(vixen);
    sleigh.push_back(comet);
    sleigh.push_back(cupid);
    sleigh.push_back(donner);
    sleigh.push_back(blitzen);

    vector<boost::shared_ptr<T> > santa;
    getBinContents(coordinates, santa);
    copy(santa.begin(), santa.end(),  back_inserter(result));

    vector<pair<double,double> >::iterator it = sleigh.begin();
    for(; it!= sleigh.end(); ++it)
        {            
  	    vector<boost::shared_ptr<T> > rudolph;
            getBinContents(*it, rudolph);
            copy(rudolph.begin(), rudolph.end(), back_inserter(result));
         
        }

    return;

}
template <typename T>
bool Bin<T>::operator==(const Bin& that)
{
    return _allContents == that._allContents &&
      _objects == that._objects &&
      _data == that._data &&
      _binSizeX == that._binSizeX &&
      _binSizeY == that._binSizeY;

}

template <typename T>
bool Bin<T>::operator!=(const Bin& that)
{
    return !(*this == that);

}

} // namespace pwiz

#endif // _BIN_HPP_

