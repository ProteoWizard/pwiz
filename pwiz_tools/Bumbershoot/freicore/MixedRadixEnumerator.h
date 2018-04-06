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
// Contributor(s): Matt Chambers
//

#ifndef _MIXEDRADIXENUMERATOR_H
#define _MIXEDRADIXENUMERATOR_H
#include "stdafx.h"

// Utility functions
void print_mixedradix(const char *bla, const size_t *f, size_t n, bool dfz/*=false*/)
// Print n-digit mixed radix number in f[].
// If dfz is true then Dots are printed For Zeros.
{
    if ( bla )  cout << bla;
    cout << "[ ";
    for (size_t k=0; k<n; ++k)
    {
        size_t t = f[k];
        if ( t!=0 )  cout << t;
        else         cout << (dfz?'.':'0');
        cout << " ";
    }
    cout << "]";
}

size_t mixedradix2num(const size_t *x, const size_t *m1, size_t n)
// Convert n-digit mixed radix number in x[] to (unsigned) integer.
// Radices minus one (that is, "nines") must be given in m1[].
{
    size_t r = 0;
    size_t p = 1;  // multiplier

    for (size_t k=0; k<n; ++k)
    {
        size_t t = x[k];
        r += p*t;
        p *= m1[k]+1;  // nines are given
    }

    return r;
}
// -------------------------

void num2mixedradix(size_t N, size_t *x, const size_t *m1, size_t n)
// Convert N to n-digit mixed radix number in x[].
// Radices minus one (that is, "nines") must be given in m1[].
{
    for (size_t k=0; k<n; ++k)
    {
        size_t t = (m1[k]+1);  // nines are given
        x[k] = N % t;
        N /= t;
    }
}
// -------------------------

class MixedRadixEnumerator
    // Gray code for mixed radix numbers.
    // Loopless algorithm. Implementation following Knuth.
{
public:
    size_t *a_;  // digits
    size_t *m1_; // radix minus one ('nines')
    size_t *f_;  // focus pointer
    size_t *d_;  // direction
    size_t n_;   // number of digits
    size_t j_;   // position of last change
    int dm_;    // direction of last move
    int numNonZeros_; // number of non-zero elements in the number
    int limit_;    // Number of maxmimum non-zero elements in the number
    int iteration; // A variable to keep track of how many iterations have been performed
    int maxIterations; // A variable to limit the maximum number of iterations
    int maxPossiblePermutations; // A variable to compute the maximum number of premutations

public:

    void  mixedradix_init(size_t n, size_t mm, const size_t *m, size_t *m1)
        // Auxiliary function used to initialize vector of nines in mixed radix classes.
    {
        maxPossiblePermutations = 1;
        if ( m )  // all radices given
        {
            for (size_t k=0; k<n; ++k) {  
                m1[k] = m[k] - 1;
                maxPossiblePermutations *= m[k];
            }
        }
        else
        {
            if ( mm>1 )  // use mm as radix for all digits:
                for (size_t k=0; k<n; ++k)  m1[k] = mm - 1;
            else
            {
                if ( mm==0 )  // falling factorial basis
                    for (size_t k=0; k<n; ++k)  m1[k] = n - k;
                else // rising factorial basis
                    for (size_t k=0; k<n; ++k)  m1[k] = k + 1;
            }
        }
    }

    MixedRadixEnumerator(size_t n, size_t mm, int limit, vector <size_t> m, size_t iterLimit)
    {
        n_ = n;
        a_ = new size_t[n_];
        m1_ = new size_t[n_];
        d_ = new size_t[n_];    // m1_[j] == m[j] - 1 in Knuth
        f_ = new size_t[n_+1];  // n_ + 1 elements
        limit_ = limit;
        iteration = 0;
        maxIterations = iterLimit;
        maxPossiblePermutations = 1;

        for (size_t k=0; k<n; ++k) {
            m1_[k] = m[k] - 1;
            maxPossiblePermutations *= m[k];
        }

        //mixedradix_init(n_, mm, m, m1_);

        first();
    }

    ~MixedRadixEnumerator()
    {
        delete [] a_;
        delete [] m1_;
        delete [] d_;
        delete [] f_;
    }

    const size_t * data()  const  { return a_; }


    void first()
    {
        for (size_t k=0; k<n_; ++k)  a_[k] = 0;
        for (size_t k=0; k<n_; ++k)  d_[k] = 1;
        for (size_t k=0; k<=n_; ++k)  f_[k] = k;
        dm_ = 0;
        j_ = n_;
        numNonZeros_ = 0;
        ++iteration;
    }

    inline bool next()
    {
        const size_t j = f_[0];
        f_[0] = 0;

        if ( j>=n_ )  { first();  return false; }

        const size_t dj = d_[j];
        const size_t aj = a_[j] + dj;
        (a_[j]==0&&aj>0)?numNonZeros_++:((a_[j]>0&&aj==0)?numNonZeros_--:numNonZeros_);
        a_[j] = aj;

        dm_ = (int)dj;  // save for dir()
        j_ = j;         // save for pos()

        if ( aj+dj > m1_[j] )  // was last move?
        {
            d_[j] = -dj;      // change direction
            f_[j] = f_[j+1];  // lookup next position
            f_[j+1] = j + 1;
        }

        ++iteration;

        return true;
    }

    bool smartNext()
    {
        bool ret;
        do {
            ret = next();
        }while(numNonZeros_>limit_ && iteration <= maxIterations);

        return (ret && (iteration <= maxIterations));
    }

     bool smartNext1()
    {
        bool ret;
        do {
            ret = next();
        }while(numNonZeros_>limit_);

        return (ret);
    }

    size_t pos()  const  { return j_; }  // position of last change
    int dir()  const  { return dm_; }

    void print(const char *bla, bool dfz=false)  const
        // If dfz is true then Dots are printed For Zeros.
    { ::print_mixedradix(bla, a_, n_, dfz); }

    void print_nines(const char *bla)  const
    { ::print_mixedradix(bla, m1_, n_, false); }

    size_t to_num()  const
        // Return (integer) value of mixed radix number.
    { return ::mixedradix2num(a_, m1_, n_); }


};

#endif

