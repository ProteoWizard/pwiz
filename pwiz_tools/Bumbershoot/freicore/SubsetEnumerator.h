//
// $Id$
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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

#ifndef _SUBSETENUMERATOR_H
#define _SUBSETENUMERATOR_H

template <typename Type>
static inline void  swap2(Type &x, Type &y)
// swap values
{ Type t(x);  x = y;  y = t; }

//: With delta sets: default is to print zeros as dots.
static const char n01[] = {'.', '1'};
//static const char n01[] = {'0', '1'};

inline void print_set(const char *bla, const vector<size_t> x, size_t n, size_t off=0)
// Print x[0,..,n-1] as set, n is the number of elements in the set.
// Example:  x[]=[0,1,3,4,8]  ==> "{0,1,3,4,8}"
{
	if ( bla )  cout << bla;

	cout << "{ ";
	for (size_t k=0; k<n; ++k)
	{
		cout << x[k+1]-off;
		if ( k<n-1 )  cout << ", ";
	}
	cout << " }";
}
// -------------------------    

inline void print_set1_as_deltaset(const char *bla, const vector<size_t> x, size_t n, size_t N, const char *c01=0)
// Print x[0,..,n-1], a subset of {1,...,N} as delta-set,
// n is the number of elements in the set.
// Example:  x[]=[1,2,4,5,9]  ==> "11.11...1"
{
	if ( bla )  cout << bla;

	const char *d = ( 0==c01 ?  n01 : c01 );
	size_t j = 0;
	for (size_t k=0; k<n; ++k)
	{
		for (  ; j<x[k+1]-1; ++j)  cout << d[0];
		cout << d[1];
		++j;
	}

	while ( j++ < N )  cout << d[0];

}
// -------------------------
class SubsetEnumerator
	// k-subsets (kmin<=k<=kmax) of the set {1,2,...,n}
	// in minimal-change (Gray code) order.
	// Algorithm following Jenkyns ("Loopless Gray Code Algorithms")
	// Limitation: cannot mix calls to next() and prev().
{
public:
	size_t n_;   // k-subsets of {1, 2, ..., n}s
	size_t kmin_, kmax_;  // kmin <= k <= kmax
	size_t k_;   // k elements in current set
	vector<size_t> S_;  // set in S[1,2,...,k] with elements \in {1,2,...,n}
	size_t j_;   // aux
	vector<size_t> modPositions;    //Position of modifications
	bool setFitness;  //A flag that keeps track whether the set is fit for making a PTM variant

public:

	SubsetEnumerator(){}

	SubsetEnumerator(size_t n, size_t kmin, size_t kmax, const vector<size_t>& modPos)
	{
		n_ = (n>0 ? n : 1);
		// Must have 1<=kmin<=kmax<=n
		kmin_ = kmin;
		kmax_ = kmax>n_?n_:kmax;
		if ( kmax_ < kmin_ )  swap2(kmin_, kmax_);
		if ( kmin_==0 )  kmin_ = 1;


		S_.resize(kmax_+1);
		S_[0] = 0;  // sentinel: != 1
		modPositions = modPos;

		first();
	}

	//~SubsetEnumerator()  { delete [] S_; }
	//const size_t * data()  const  { return S_+1; }
	//const size_t num()  const  { return k_; }

	size_t last()
	{
		S_[1] = 1;  k_ = kmin_;

		if ( kmin_==1 )  { j_ = 1; }
		else
		{
			for (size_t i=2; i<=kmin_; ++i)  { 
				S_[i] = n_ - kmin_ + i; 
			}
			j_ = 2;
		}

		setFitness = true;
		for(size_t i=1; i<=k_; ++i) { if((i-1)!=0 && modPositions[S_[i]-1]==modPositions[S_[i-1]-1]) { setFitness=false; break;}}
		return k_;
	}


	size_t first()
	{

		k_ = kmin_;
		setFitness = true;
		for (size_t i=1; i<=kmin_; ++i)  { 
			S_[i] = n_ - kmin_ + i; 
			//cout << S_[i] << "," << S_[i-1] << endl;
			if((i-1)!=0 && modPositions[S_[i]-1]==modPositions[S_[i-1]-1])
				setFitness=false;
		}
		j_ = 1;
		return k_;
	}

	bool is_first()  const  { return ( S_[1] == n_ - kmin_ + 1 );  }

	bool is_last()  const
	{
		if  ( S_[1] != 1  )   return 0;
		if ( kmin_<=1 )  return (k_==1);
		return  (S_[2]==n_-kmin_+2);
	}

private:
	void prev_even()
	{
		size_t &n=n_, &kmin=kmin_, &kmax=kmax_, &j=j_;
		if ( S_[j-1] == S_[j]-1 )  // can touch sentinel S[0]
		{
			S_[j-1] = S_[j];
			if ( j > kmin )
			{
				if ( S_[kmin] == n )  {  j = j-2; }  else  {  j = j-1; }
			}
			else
			{
				S_[j] = n - kmin + j;
				if ( S_[j-1]==S_[j]-1 )  { j = j-2; }
			}
		}
		else
		{
			S_[j] = S_[j] - 1;
			if ( j < kmax )
			{
				S_[j+1] = S_[j] + 1;
				if ( j >= kmin-1 )  {j = j+1; }  else  {j = j+2; }
			}
		}

	}

	void prev_odd()
	{
		size_t &n=n_, &kmin=kmin_, &kmax=kmax_, &j=j_;
		if ( S_[j] == n )  {  j = j-1; }
		else
		{
			if ( j < kmax )
			{
				S_[j+1] = n;
				j = j+1;
			}
			else
			{
				S_[j] = S_[j]+1;
				if ( S_[kmin]==n )  {  j = j-1; }
			}
		}
	}

public:
	size_t prev()
	{
		setFitness = true;

		if ( is_first() )  { last(); return 0; }

		if ( j_&1 )  prev_odd();
		else         prev_even();

		if ( j_<kmin_ )  { k_ = kmin_; }  else  { k_ = j_; };

		for(size_t i=1; i<=k_; ++i) { if((i-1)!=0 && modPositions[S_[i]-1]==modPositions[S_[i-1]-1]) { setFitness=false; break;}}

		return k_;
	}

	size_t next()
	{
		if ( is_last() )  { first();  return 0; }

		if ( j_&1 )  prev_even();
		else         prev_odd();

		if ( j_<kmin_ )  { k_ = kmin_; }  else  { k_ = j_; };

		setFitness = true;
		for(size_t i=1; i<=k_; ++i) { if((i-1)!=0 && modPositions[S_[i]-1]==modPositions[S_[i-1]-1]) { setFitness=false; break;}}
		return k_;
	}

	void print_set(const char *bla=0)  const { 
		::print_set(bla, S_, k_); 
	}

	void print_deltaset(const char *bla=0)  const { 
		::print_set1_as_deltaset(bla, S_, k_, n_); 
	}
};

#endif
