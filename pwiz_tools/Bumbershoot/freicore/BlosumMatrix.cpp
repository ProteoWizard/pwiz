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

#include "BlosumMatrix.h"

namespace freicore {

    BlosumMatrix::BlosumMatrix(string file) {
        filename = file;
    }

    BlosumMatrix::~BlosumMatrix() {}

    /**
    parseBlosumMatrix parses out the blosum file in FASTA/SSEARCH format and
    puts the log odds values in a matrix. The matrix is indexed using the
    from and to amino acid names.
    */
    void BlosumMatrix::parseBlosumMatrix() {

        ifstream fileStream;
        Header header;
        string inputLine;
        bool parsingLogOdds = false;

        // Open the file.
        fileStream.open(filename.c_str());
        if(!fileStream.is_open()) {
            throw invalid_argument( string( "unable to open blosum file \"" ) + filename + "\"" );
        }

        // Row index for the log odds
        size_t rowIndex = 0;
        while(!fileStream.eof()) {
            // Get the line
            getlinePortable(fileStream,inputLine);
            // If the line is start of a header
            if(inputLine.find("A R N D C")!=string::npos) {
                // Tokenize the header using white space as delimiter
                size_t colIndex = 0;
                string buffer;
                stringstream sTokenizer(inputLine);
                // For each amino acid token
                while(sTokenizer >> buffer) {
                    // Load it into the header with the column index.
                    header.insert(Header::value_type(colIndex,buffer));
                    colIndex++;
                }

                // Skip the line after the header
                getlinePortable(fileStream, inputLine);
                // We will be parsing log odds right after
                parsingLogOdds = true;
            } else if(parsingLogOdds) {

                size_t colIndex = 0;
                // Tokenize the line using white space as delimiter
                string buffer;
                stringstream sTokenizer(inputLine);

                // Get the from amino acid using the row index
                Header::iterator from = header.find(rowIndex);
                while(sTokenizer >> buffer) {
                    // Find the to amino acid using the column index
                    Header::iterator to = header.find(colIndex);
                    // Create a cell index with (from, to).
                    MatrixCellIndex cellIndex((*from).second,(*to).second);
                    // Get the value
                    int value = lexical_cast<int> (buffer);
                    // Store it in the matrix
                    blosumMatrix.insert(Matrix::value_type(cellIndex,value));
                    // Create a cell index with (to, from) and store the log odds
                    // This is because, traditionally Blosum matrices are either
                    // upper or lower triangular matrices. We have to fill the
                    // other half of the matrix while parsing its counterpart.
                    MatrixCellIndex revCellIndex((*to).second,(*from).second);
                    blosumMatrix.insert(Matrix::value_type(revCellIndex,value));
                    // Increment the column index
                    colIndex++;
                }
                // Increment the column index
                rowIndex++;
            }
        } // End while
    } // End parseBlosumMatrix

    /**
    getLogOdds takes two amino acids and returns the log odds score of
    substitution.
    */
    int BlosumMatrix::getLogOdds(string aminoAcidFrom, string aminoAcidTo) {
        MatrixCellIndex cellIndex(aminoAcidFrom, aminoAcidTo);
        Matrix::iterator iter = blosumMatrix.find(cellIndex);
        return (*iter).second;
    }
}
