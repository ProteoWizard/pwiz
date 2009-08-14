//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
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


#import "../xdk/XRawFile2.dll" rename_namespace("XRawfile")
#include <iostream>


using namespace std;
using namespace XRawfile;


void main()
{
    cout << "xdktest\n";
    CoInitialize(NULL);
    IXRawfilePtr rawfile("XRawfile.XRawfile.1");

    const char* filename = "data02.RAW";
    if (rawfile->Open(filename))
    {
        cout << "Unable to open file " << filename << endl;
        return;
    }

    _bstr_t bstr;
    rawfile->GetFileName(bstr.GetAddress());
    cout << (const char*)(bstr) << " opened.\n";

    rawfile->GetCreatorID(bstr.GetAddress());
    cout << "CreatorID: " << (const char*)(bstr) << endl;

    long first = 0;
    long last = 0;

    if (rawfile->SetCurrentController(0, 1))
        cout << "Error in SetCurrentController()\n";
    if (rawfile->GetFirstSpectrumNumber(&first))
        cout << "Error in GetFirstSpectrumNumber()\n";
    if (rawfile->GetLastSpectrumNumber(&last))
        cout << "Error in GetLastSpectrumNumber()\n";

    cout << "first: " << first << "  last: " << last << endl;
    rawfile->Close();

    rawfile = NULL;
    CoUninitialize();
}

