/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// MethodBuilder.h
//     Abstract base class for building native instrument methods from
//     Skyline exported transition lists.

#pragma once

#include <string>
#include <vector>
#include <iostream>

using namespace std;

struct MethodTransitions
{
    string outputMethod;
    string finalMethod;
    vector<vector<string>> tableTranList;
};

class MethodBuilder
{
public:
    virtual void usage();
    virtual void parseCommandArgs(int argc, char* argv[]);
    virtual void build();

protected:
    /**
     * This method performs the work of writing a single native
     * method for a single transition list.  All subclasses must
     * implement this method.
     */
    virtual void createMethod(string templateMethod, string outputMethod,
        const vector<vector<string>>& tableTranList) = 0;

    void readFile(string inputFile, bool multiFile);
    void readTransitions(istream& instream, string outputMethod);
    void readTransitions(istream& instream, vector<vector<string>>& tableTranList);

private:
    string _templateMethod;
    vector<MethodTransitions> _vMethodTrans;
};