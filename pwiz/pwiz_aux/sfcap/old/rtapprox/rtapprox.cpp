//
// $Id$
//
//
// Robert Burke <robert.burke@cshs.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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


#include <xercesc/sax2/XMLReaderFactory.hpp>

#include <boost/program_options.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/matrix_proxy.hpp>

#include <iostream>
#include <fstream>
#include <iomanip>
#include <vector>
#include <string>

#include "FitFromPepXML.hpp"
#include "utils.hpp"

namespace po = boost::program_options;
namespace ublas = boost::numeric::ublas;

using namespace std;
using namespace pwiz::rtapprox;
using namespace pwiz::pepxml;

XERCES_CPP_NAMESPACE_USE


/* <-------------------- Command line options ---------------------> */

const char* GEN_OPTION_ARG = "gen,g";
const char* GEN_OPTION = "gen";
const char* GEN_DESC = "generate peptide component parameters";

const char* SANITY_OPTION_ARG = "sanity,s";
const char* SANITY_OPTION = "sanity";
const char* SANITY_DESC = "";

const char* PEPMATRIX_OPTION_ARG = "pepm,p";
const char* PEPMATRIX_OPTION = "pepm";
const char* PEPMATRIX_DESC = "print out peptide count matrix and quit";

const char* IN_OPTION_ARG = "in,i";
const char* IN_OPTION = "in";
const char* IN_DESC = "(default) file to use as input for "
    "generating or fitting";

const char* OUT_OPTION_ARG = "out,o";
const char* OUT_OPTION = "out";
const char* OUT_DESC = "specifies a file to direct output to.";

const char* LIMIT_OPTION_ARG = "limit,l";
const char* LIMIT_OPTION = "limit";
const char* LIMIT_DESC = "The upper limit for rows to use for "
    "generating fit vector";

const char* TOL_OPTION_ARG = "tol,t";
const char* TOL_OPTION = "tol";
const char* TOL_DESC = "tolerance to use when selecting peptides";

const char* RT_MIN_OPTION_ARG = "rtmin";
const char* RT_MIN_OPTION = "rtmin";
const char* RT_MIN_DESC = "set the minimum retention time accepted";

const char* RT_MAX_OPTION_ARG = "rtmax";
const char* RT_MAX_OPTION = "rtmax";
const char* RT_MAX_DESC = "set the maximum retention time accepted";

const char* RT_OPTION_ARG = "rt,r";
const char* RT_OPTION = "rt";
const char* RT_DESC = "print retention time vector";

const char* TAB_OPTION_ARG = "tab,T";
const char* TAB_OPTION = "tab";
const char* TAB_DESC = "Indicates the input file is in tab delimited format";

const char* TEST_OPTION_ARG = "test";
const char* TEST_OPTION = "test";
const char* TEST_DESC = "Loads a test dataset to check program";

const char* VERBOSE_OPTION_ARG = "verbose,v";
const char* VERBOSE_OPTION = "verbose";
const char* VERBOSE_DESC = "prints additional information";

/* <----------------------- Command enums -------------------------> */

enum print_option_type {
    fit_vector = 0x01, peptide_matrix = 0x02, rt_vector = 0x04, verbose = 0x08,
    print_all = 0x0F
};

enum input_format {
    pepxml_format = 0x01, tab_format = 0x02
};

/* <------------------ Least squares fit object -------------------> */

auto_ptr<FitFromPepXML> fit;

int print_options = fit_vector;

/* <--------------------- Utility functions -----------------------> */

// prints a ublas::matrix<double> to stdout in row major order. Row values are
// comma, space delimited. Rows are new line delimited.
void printMatrix(const ublas::matrix<double>& A, ostream* out)
{
    typedef ublas::matrix<double>::size_type size_type;

    if (out == NULL)
        out = &cout;

    for (size_type x=0; x<A.size1(); x++)
    {
        for (size_type y=0; y<A.size2(); y++)
        {
            *out << A(x,y);
            if (y < A.size2() - 1)
                *out << ", ";            
        }
        *out << ";\n";
    }
    *out << "\n";
}

// prints a ublas::vector<double> to stdout. The values are comma, space delimited.
void printVector(const ublas::vector<double>& v, ostream* out)
{
    typedef ublas::vector<double>::size_type size_type;

    if (out == NULL)
        out = &cout;

    for (size_type i=0; i<v.size(); i++)
    {
        *out << v(i);
        if (i < v.size() - 1)
            *out << ", ";
    }

    *out << endl;
}

// Reads a tab delimited file. This format is based on fake data files
// provided by Parag where the fields were:
//    ^(unknown)\t(peptide)\t(retention time)$
//
// The rest of the line is ignored after the third word or line break.
void readTabFile(string& in, vector<Peptide>& peptides)
{
    ifstream fin(in.c_str());

    //const char* delim = "\t";
    string line;
    while(getline(fin, line))
    {
        istringstream ss(line);
        string junk;
        string peptide;
        string rt;

        ss >> junk;
        ss >> peptide;
        ss >> rt;

        Peptide p;
        p.setPeptide(peptide);
        p.setRetentionTime(rt);
        peptides.push_back(p);
    }
}

// Generates a fit vector from the peptide/retention times from a
// pepxml or tab file.
void gen(string& in, ostream* out, int limit, float tol,
         float rtmin, float rtmax,
         int command, int in_format)
{
    cout << "limit=" << limit << "\ntol=" << tol << endl;
    
    cout << "rt min=" << rtmin << ", rt max=" << rtmax << endl;;
    
    if (out == NULL)
        out = &cout;

    ublas::vector<double> result;

    //try {
    if (in_format == pepxml_format)
    {
        fit = FitFromPepXML::create(in.c_str(), tol, rtmin, rtmax, limit);

        result = fit->load();
    }
    else
    {
        vector<Peptide> peps;
        readTabFile(in, peps);

        fit = FitFromPepXML::create();
        fit->setRtMin(rtmin);
        fit->setRtMax(rtmax);
        fit->createSystem(peps);
        result = fit->fit();
    }
    //}
    //catch (std::bad_alloc ba)
    //{
    //    cerr << ba.what() << endl;
    //    printStackTrace();
    //}
    
    ublas::vector<double>::iterator j=result.begin();

    if (command & peptide_matrix)
    {
        printMatrix(fit->getPeptideMatrix(), out);
        cout << "\n";
    }

    if (command & rt_vector)
    {
        printVector(fit->getRTVector(), out);
        cout << "\n";
    }

    if (command & fit_vector)
    {
        //for (ublas::vector<double>::iterator i=result.begin(); i!=result.end(); i++)
        //{
        //    *out << *i;
        //    if (i != --result.end())
        //        *out << ", ";
        //}
        //*out << "\n";
        printVector(result, out);
    }
}

// Conducts a sanity check from a pepxml or tab file. A linear fit
// vector  is calculated from a subset of the peptides and checks the
// estimated  retention time against the actual retention time.
void sanity(string& in, ostream* out, float tol,
            float rtmin, float rtmax, int limit,
            int print_command, int in_format)
{
    if (out == NULL)
        out = &cout;

    gen(in, out, limit, tol, rtmin, rtmax, 0, in_format);

    const ublas::matrix<double>& pepm = fit->getPeptideMatrix();
    const ublas::vector<double>& rtv = fit->getRTVector();
    const ublas::vector<double>& result = fit->fit();

    *out << "Limit=" << limit << "\n";

    if (print_command & peptide_matrix)
    {
        *out << "Peptide matrix (size "
             << pepm.size1() << "x" << pepm.size2() << ") =\n";
    
        printMatrix(pepm, out);
    }

    if (print_command & rt_vector)
    {
        *out << "\nRetention time vector (size " << rtv.size() << ") =\n";

        printVector(rtv, out);
    }
    
    *out << "\nResult (size " << result.size() <<") =\n";

    printVector(result, out);

    *out << "\npeptide\tobserved\tpredicted\terror\tpvalue\n";

    vector<Peptide>& peptideList =
        const_cast<vector<Peptide>& >(fit->getPeptideList());
    vector<Peptide>::iterator i;

    double error_sum = 0;
    for(i = peptideList.begin(); i != peptideList.end(); i++)
    {
        const char* peptide = (*i).getPeptide();
        const char* rt_str = (*i).getRetentionTime();
        const char* pval = (*i).getPValue();
        double rt_real = atof(rt_str);

        ublas::vector<double> v = fit->countPeptide(peptide);
        double rt_approx =  inner_prod(v, result);

        // Calculate error
        double error = (rt_real - rt_approx);
        error_sum += error * error;
        
        *out << peptide << ", " << rt_real << ", " << rt_approx
             << ", " << error << ", " << pval << "\n";
    }

    // Calculate error RMS 
    double error_rms = sqrt(error_sum / peptideList.size());
    *out << "\nerror rms=" << error_rms << "\n";

    *out << "# peptides = " << fit->getPeptideList().size() << endl;
    
    *out << endl;
}


// Conducts a test of the linear least squares code
void test(string& inFile, ostream* os)
{
    ublas::matrix<double> pm(30, 20);
    ublas::vector<double> v(30);
    ublas::identity_matrix<double> eye(20);
    
    ublas::subrange(pm, 0, 20, 0, 20) = eye;

    for (int i=0; i<10; i++)
    {
        v(i) = i;
        v(i+10) = i+10;
    }

    fit = FitFromPepXML::createFromMatrix(pm, v);

    const ublas::vector<double>& r = fit->fit();

    cout << "Result vector=\n[";
    for (ublas::vector<double>::size_type i=0; i<r.size(); i++)
    {
        cout << setw(5) << r(i);
    }

    cout << "]" << endl;
}

int main(int argc, char** argv)
{
    int limit = 0;
    int in_format = pepxml_format;
    
    //float rt = 0;
    float rtmin = -1;
    float rtmax = -1;
    float tol = 0;
    
    string outFile;
    string inFile;
    vector<string> inList;

    // Fetch the command line options.
    po::options_description options("General options");
    options.add_options()
        ("help", "produce help message")
        (SANITY_OPTION_ARG, SANITY_DESC)
        (GEN_OPTION_ARG,GEN_DESC)
        (RT_OPTION_ARG, RT_DESC)
        (RT_MIN_OPTION_ARG, po::value<float>(&rtmin), RT_MIN_DESC)
        (RT_MAX_OPTION_ARG, po::value<float>(&rtmax), RT_MAX_DESC)
        (PEPMATRIX_OPTION_ARG, PEPMATRIX_DESC)
        (LIMIT_OPTION_ARG, po::value<int>(&limit), LIMIT_DESC)
        (OUT_OPTION_ARG, po::value<string>(&outFile), OUT_DESC)
        (TOL_OPTION_ARG, po::value<float>(&tol), TOL_DESC)
        (TEST_OPTION_ARG, TEST_DESC)
        (IN_OPTION_ARG, po::value< vector<string> >(&inList))
        ;

    po::positional_options_description p;
    p.add(IN_OPTION, -1);
    
    po::variables_map vm;
    po::store(po::command_line_parser(argc, argv).
              options(options).positional(p).run(), vm);
    po::notify(vm);

    // Initialize the XML4C2 system
    try
    {
        XMLPlatformUtils::Initialize();
    }

    catch (const XMLException& toCatch)
    {
        const char *message = XMLString::transcode(toCatch.getMessage());
        XERCES_STD_QUALIFIER cerr << "Error during initialization! :\n"
                                  << message
                                  << XERCES_STD_QUALIFIER endl;

        delete message;
        return 1;
    }

    bool haveIn=false;

    //istream* is = NULL;
    ostream* os = NULL;

    // Set print commands
    if (vm.count(RT_OPTION))
    {
        print_options |= rt_vector;
    }
    
    if (vm.count(PEPMATRIX_OPTION))
    {
        print_options |= peptide_matrix;
    }

    // Get input format options (if available)
    if (vm.count(TAB_OPTION))
    {
        in_format = tab_format;
    }

    // Set up variables for requested I/O
    if (vm.count(IN_OPTION))
    {
        vector<string> v = vm[IN_OPTION].as<vector<string> >();

        assert (v.size()>0);
        inFile = *(v.begin());
          
        haveIn=true;
    }

    if (vm.count(OUT_OPTION))
    {
        os = new ofstream(outFile.c_str());
    }

    // Complete action requested
    if (vm.count(TEST_OPTION))
    {
        test(inFile, os);
    }
    else if (!haveIn)
    {
        cout << options << "\n";
        return 1;
    }
    else if (vm.count(SANITY_OPTION))
    {
        sanity(inFile, os, tol, rtmin, rtmax, limit, print_options, in_format);
    }
    else if (vm.count(GEN_OPTION))
    {
        gen(inFile, os, limit, tol, rtmin, rtmax, print_options, in_format);
    }
    else
    {
        cout << options << "\n";
        return 1;
    }

    if (vm.count(OUT_OPTION))
    {
        ((ofstream*)os)->close();
    }
    
    return 0;
}
