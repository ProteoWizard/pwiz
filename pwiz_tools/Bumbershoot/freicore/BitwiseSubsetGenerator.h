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

#ifndef _BITWISESUBSETGENERATOR_H
#define _BITWISESUBSETGENERATOR_H

namespace freicore {

    typedef unsigned long ulong;
    typedef  unsigned long long   uint64;

    /**
        This class takes an array of integers. Each position in the array corresponds to an amino acid
        and the value of the position corresponds to the number of possible modifications on that amino
        acid. It computes the total number of possible peptide variants with a set number of maximum number
        of amino acids modified. The class implements the functional using a bit-wise shift subset generator
        and also some general combinotorial mathematics. The bit-wise shift subset generator function is
        used to test the combinotorial math.
    */
    class CountTotalVariants {

    private: 
        ulong n;  // number of bits
        ulong N;  // 2**n
        ulong c;
        // Array of integers
        vector<size_t> positionalRadix;
        // Max total number of modifications allowed per peptide
        size_t totalModifiedPepitdes;

    public:

        CountTotalVariants(vector<size_t> pos, size_t maxPTMCount) {
            positionalRadix = pos;
            n = pos.size();
            c= maxPTMCount;
            N = 1UL<<n;
            totalModifiedPepitdes = 0;
        }

        // A simple getter function
        size_t getTotalNumModifiedPeptides() {
            return totalModifiedPepitdes;
        }

        /**
            This function is part of the combinatorial math that computes
            the total number of peptide variants possible if atmost
            maxPTMCount number of modifications are allowed.
        */
        size_t getMultiplier(size_t start, size_t wordSize) {
            size_t multipler = 0;
            for(size_t i = start; i < positionalRadix.size()-wordSize; ++i) {
                size_t local = positionalRadix[i];
                for(size_t j = i+1; j <= i+wordSize-1; ++j) {
                    local *= positionalRadix[j];
                }
                multipler+=local;
            }
            return multipler;
        }

        /**
            This function is part of the combinatorial math that computes
            the total number of peptide variants possible if atmost
            maxPTMCount number of modifications are allowed.
        */

        size_t alternativeCounting() {

            // Return if the maxPTMCount is 0
            if(c==0) {
                return 0;
            }

            // The general mathematics is: Given that a peptid ABCD can be modified 
            // with A in N1 ways and B in N2 ways and D in N3 ways. Assume that
            // we can not have more than 2 modifications per peptide then the total
            // number of peptide variants is;
            // N1+N2+N3+(N1*N2)+(N1*N3)+(N2*N3)
            size_t alternativeCount = 0;
            for(size_t i = 0; i < positionalRadix.size(); ++i) {
                alternativeCount+=positionalRadix[i];
            }
            size_t ptmCount = 2;
            while(ptmCount <= c) {
                for(size_t i = 0; i <= positionalRadix.size()-ptmCount; ++i) {
                    size_t multiplier = getMultiplier(i+1,ptmCount-1);
                    alternativeCount+=multiplier*positionalRadix[i];
                }
                ++ptmCount;
            }
            return alternativeCount;
        }


        // This function counts the number of bits in a word
        inline ulong bitCount(ulong x) {
            x -=  (x>>1) & 0x55555555UL;                        // 0-2 in 2 bits
            x  = ((x>>2) & 0x33333333UL) + (x & 0x33333333UL);  // 0-4 in 4 bits
            x  = ((x>>4) + x) & 0x0f0f0f0fUL;                   // 0-8 in 8 bits
            x *= 0x01010101UL;
            return  x>>24;
        }

        // Print x as index sequence:
        //   x=....1..11  ==>  [0, 1, 4]
        // With offsets off!=0 start index with off instead of zero.
        void print_idx_seq(const char *bla, unsigned long long x, ulong off=0) {
            cout << bla;
            cout << "[";
            ulong j = 0;
            do
            {
                if ( x & 1UL )
                {
                    cout << j + off;
                    if ( x>1 )  cout << ", ";
                }
                ++j;
                x >>= 1;
            }
            while ( x );

            cout << "]";
        }

        // Bit-wise addition of PTM radices
        void addPTMRadix(uint64 x) {
            ulong j = 0;
            size_t count = 1;
            do
            {
                if ( x & 1UL )
                {
                    count *= positionalRadix[j];
                    //cout << j + off;
                    //if ( x>1 )  cout << ", ";
                }
                ++j;
                x >>= 1;
            }
            while ( x );
            totalModifiedPepitdes += count;
        }

        void visitAndPrint(ulong x)
        {
            static ulong z = 0;
            if ( c && (bitCount(x)>c) )  return;

            print_idx_seq("Seq:",(uint64) x);
            cout << endl;
            z = x;
        }

        void visit(ulong x)
        {
            if ( c && (bitCount(x)>c) )  return;
            addPTMRadix(x);
        }

        // F() and G():  Gray shifts-order for subsets
        // These functions generate all possible subsets of a bitset
        void F(ulong x)
        {
            if ( x>=N )  return;
            visit(x);
            F(2*x);
            G(2*x+1);
        }
        // -------------------------
        void G(ulong x)
        {
            if ( x>=N )  return;
            F(2*x+1);
            G(2*x);
            visit(x);
        }
    };
}
#endif
