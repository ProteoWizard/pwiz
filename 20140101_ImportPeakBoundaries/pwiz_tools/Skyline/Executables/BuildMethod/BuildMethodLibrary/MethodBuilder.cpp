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

// MethodBuilder.cpp
//     Abstract base class for building native instrument methods from
//     Skyline exported transition lists.

#include <sys/types.h>
#include <sys/stat.h>
#include <errno.h>
#include <io.h>
#include "MethodBuilder.h"
#include "StringUtil.h"
#include "Verbosity.h"

void MethodBuilder::usage()
{
    const char* usage = "\n"
            "   -o <output file> New method is written to the specified output file\n"
            "   -s               Transition list is read from stdin.\n"
            "                    e.g. cat TranList.csv | BuildLTQMethod -s -o new.ext temp.ext\n"
            "\n"
            "   -m               Multiple lists concatenated in the format:\n"
            "                    file1.ext\n"
            "                    <transition list>\n"
            "\n"
            "                    file2.ext\n"
            "                    <transition list>\n"
            "                    ...\n"
            "\n"
            "   -v <level>       Level of output to stderr (silent, error, status, warn)\n"
            "                    Default status\n";

    cerr << usage << endl;
    exit(1);
}

void MethodBuilder::parseCommandArgs(int argc, char* argv[])
{
    // Default to stdin for transition list input
    string outputMethod;
    bool readStdin = false;
    bool multiFile = false;

    int i = 1;
    while (i < argc && *argv[i] == '-')
    {
        switch (*(argv[i++]+1))
        {
        case 'o':
            if (i >= argc) usage();
            outputMethod = argv[i++];
            break;
        case 's':
            readStdin = true;
            break;
        case 'm':
            multiFile = true;
            break;
        case 'v':
            if (i >= argc) usage();
            Verbosity::set_verbosity(argv[i++], false);
            break;
        default:
            usage();
        }
    }

    if (multiFile && !outputMethod.empty())
    {
        cerr << "Multi-file and specific output are not compatibile." << endl << endl;
        usage();
    }

    int argcLeft = argc - i;
    if (argcLeft < 1 || (!readStdin && argcLeft < 2))
        usage();

    _templateMethod = argv[i++];

    // Read input into a list of lists of fields
    if (readStdin)
    {
        if (!multiFile && outputMethod.empty())
        {
            cerr << "Reading from standard in without multi-file format must specify an output file." << endl << endl;
            usage();
        }
        readTransitions(cin, outputMethod);
    }
    else
    {
        for (; i < argc; i++)
        {
            string inputFile = argv[i];
            string filter;
            if (inputFile.find('*') != string::npos)
                filter = inputFile;
            else
            {
                struct _stat buffer;
                int result = _stat(inputFile.c_str(), &buffer);
                if (result != 0)
                {
                    switch (errno)
                    {
                        case ENOENT:
                            Verbosity::error("The input file %s does not exist", inputFile.c_str());
                            break;
                        case EINVAL:
                        default:
                            Verbosity::error("Unexpected error processing input file %s", inputFile.c_str());
                    }
                }
                if ((buffer.st_mode & _S_IFDIR) != 0)
                    filter = inputFile + "/*.csv";
            }

            if (filter.empty())
                readFile(inputFile, multiFile);
            else
            {
                struct _finddata_t c_file;
                long hFile = _findfirst(filter.c_str(), &c_file);
                if (hFile == -1L)
                    Verbosity::error("No files found matching %s", filter.c_str());
                else
                {
                    string dirPath = ".";
                    size_t slash = filter.find_last_of("\\/");
                    if (slash != string.npos)
                        dirPath = filter.substr(0, slash);

                    do
                    {
                        readFile(dirPath + "/" + c_file.name, multiFile);
                    }
                    while (_findnext(hFile, &c_file) == 0);

                    _findclose(hFile);
                }
            }
        }
    }
}

void MethodBuilder::readFile(string inputFile, bool multiFile)
{
    string outputMethod;
    if (!multiFile)
    {
        outputMethod = inputFile;
        outputMethod.erase(outputMethod.rfind("."));
        outputMethod += ".meth";
    }
    ifstream infile;
    infile.open(inputFile.c_str(), ifstream::in);
    if (!infile.good())
    {
        Verbosity::error("Failure opening input file %s", inputFile.c_str());
    }
    readTransitions(infile, outputMethod);
    infile.close();
}

void MethodBuilder::readTransitions(istream& instream, string outputMethod)
{
    string line;
    while (instream.good())
    {
        MethodTransitions methodTrans;
        if (outputMethod.empty())
        {
            // Read output file path from a line in the file
            getline(instream, line);
            trim(line);
            methodTrans.outputMethod = line;
            // Read final file path from a line in the file
            getline(instream, line);
            trim(line);
            methodTrans.finalMethod = line;
        }
        else
        {
            // Only one file, if outputMethod specified
            if (!_vMethodTrans.empty())
                break;
            methodTrans.outputMethod = outputMethod;
            methodTrans.finalMethod = outputMethod;
        }
        readTransitions(instream, methodTrans.tableTranList);
        if (methodTrans.tableTranList.empty())
            Verbosity::error("Failure reading transition list. No transitions found.");
        _vMethodTrans.push_back(methodTrans);
    }

    // Read remaining contents of stream, in case it is stdin
    while (instream.good())
        getline(instream, line);
}

void MethodBuilder::readTransitions(istream& instream, vector<vector<string>>& tableTranList)
{
    string line;
    while (instream.good())
    {
        getline(instream, line);
        trim(line);
        if (line.empty())
            break;

        vector<string> fields;
        split(line, fields);
        tableTranList.push_back(fields);
    }
}

void MethodBuilder::build()
{
    vector<MethodTransitions>::iterator it = _vMethodTrans.begin();
    for (; it != _vMethodTrans.end(); it++)
    {
		size_t slash = it->finalMethod.find_last_of("\\/");
		string methodName = (slash == string::npos ? it->finalMethod : it->finalMethod.substr(slash));
        cerr << "MESSAGE: Exporting method " << methodName << endl;

        // Copy the template into place, because ILCQMethod::SaveAs() strips
        // auto-sampler and LC pump settings.
        string outputMethod = it->outputMethod;

        ifstream templateStream(_templateMethod.c_str(), ios::in | ios::binary);
        if (!templateStream.good())
        {
            Verbosity::error("Failure opening method template %s", _templateMethod.c_str());
        }
        ofstream outputStream(outputMethod.c_str(), ios::out | ios::binary);
        if (!outputStream.good())
        {
            Verbosity::error("Failure opening output method %s", outputMethod.c_str());
        }
        outputStream << templateStream.rdbuf();
        templateStream.close();
        outputStream.close();

        createMethod(_templateMethod, outputMethod, it->tableTranList);

        // Skyline uses a segmented progress status, which expects 100% for each
        // segment, with one segment per file.
        cerr << "100%" << endl;
    }
}
