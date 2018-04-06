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

#ifndef _BLOSUM_MATRIX_H
#define _BLOSUM_MATRIX_H

#include "stdafx.h"

namespace freicore {

    typedef multimap<size_t,string> Header;
    typedef multimap<pair<size_t,size_t>,int> LogOddsMatrix;
    typedef pair<size_t,size_t> IntCellIndex;
    typedef multimap<pair<string,string>,int> Matrix;
    typedef pair<string,string> MatrixCellIndex;

    /**
    Class BlosumMatrix parses out the FASTA/SSEARCH formatted blosum matrix file
    and stores the log odds ratios in a matrix. These ratios are used to select
    most likely substitutions for a delta mass.
    */
    class BlosumMatrix {

        // A matrix to store the log odds substitution matrix
        Matrix blosumMatrix;

        // Blosum matrix filename
        string filename;

    public:
        // Constructors and destructors
        BlosumMatrix(string file);
        ~BlosumMatrix();

        // A function to parse out the blosum matrix
        void parseBlosumMatrix();

        // Returns the log odds of an amino acid substitution
        int getLogOdds(string aminoAcidFrom, string aminoAcidTo);

        // Overloading << for printing.
        friend ostream& operator <<(ostream &os, const BlosumMatrix &obj) {
            for(Matrix::const_iterator iter = obj.blosumMatrix.begin(); iter != obj.blosumMatrix.end(); iter++) {
                os << "(" << (*iter).first.first << "," << (*iter).first.second << "):" << (*iter).second << endl;
            }
            return os;
        }

        static void testBlosumMatrix(string file) {
            BlosumMatrix matrix(file);
            matrix.parseBlosumMatrix();
            cout << matrix;
        }
    };
}

#endif // _BLOSUM_MATRIX_H
