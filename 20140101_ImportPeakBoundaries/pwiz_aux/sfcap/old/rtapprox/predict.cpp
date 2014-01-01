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


// Description:
//    This program attempts to predict the retention time of peptides
// by partitioning the elution into linear sections.
//
// This project is put on hold indefinitely.

#include <xercesc/sax2/XMLReaderFactory.hpp>

#include <boost/program_options.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/matrix_proxy.hpp>

#include <iostream>
#include <fstream>
#include <iomanip>
#include <vector>
#include <string>

#include "util/auto_vector.h"
#include "FitFromPepXML.hpp"
#include "utils.hpp"

namespace po = boost::program_options;
namespace ublas = boost::numeric::ublas;

using namespace std;
using namespace pwiz::rtapprox;

XERCES_CPP_NAMESPACE_USE

/* <------------------ Least squares fit object -------------------> */

auto_ptr<FitFromPepXML> fit;

/* <---------------------------- Main -----------------------------> */

void partitionPeptides(double offset_t, double size_t,
                       vector<Peptide>& source,
                       std::vector< vector<Peptide> >& result)
{
    typedef vector<Peptide>::iterator iterator;
    
    iterator pBegin = source.begin();
    iterator pEnd = source.begin();

    double cutoff_t = offset_t + size_t;

    for (iterator i=source.begin(); i!= source.end(); i++)
    {
        if (atof((*i).getRetentionTime()) < cutoff_t)
        {
            pEnd = i;
        }
        else
        {
            vector<Peptide> temp(pBegin, pEnd);
            result.push_back(temp);
            pBegin = i;
            pEnd = i;
            cutoff_t += size_t;
        }
    }
}

double minTime(const std::vector<Peptide>& peps)
{
    double min = 0.;
    typedef std::vector<Peptide>::const_iterator iterator;

    for (iterator i=peps.begin(); i!= peps.end(); i++)
    {
        double rt = atof((*i).getRetentionTime());
        if (rt < min)
            min = rt;
    }

    return min;
}

void printVector(const ublas::vector<double>& v)
{
    for (ublas::vector<double>::const_iterator i=v.begin(); i!=v.end(); i++)
    {
        cout << std::setw(10) << *i;
    }
}

ublas::vector<double> mean(std::vector< ublas::vector<double> >& data)
{
    typedef std::vector< ublas::vector<double> >::iterator iterator;
    iterator i=data.begin();
    ublas::vector<double> mean = *i;
    for (; i!=data.end(); i++)
    {
        mean += *i;
    }

    return (1./data.size()) * mean;
}

ublas::vector<double> var(std::vector< ublas::vector<double> >& data)
{
    typedef std::vector< ublas::vector<double> >::iterator iterator;

    ublas::vector<double> m = mean(data);
    ublas::vector<double> x(m.size());

    ublas::identity_matrix<double> eye(m.size());

    for (iterator i=data.begin(); i != data.end(); i++)
    {
        ublas::vector<double> a = *i - m;
        x += element_prod(a, a);
    }

    return x;
}

void process(istream* is, double rtmin, double rtmax, double tol)
{
    bool showRMS = false;
    bool showEstimateDiff = true;
    
    typedef std::vector< std::vector<Peptide> > partitioned_vector;
    typedef partitioned_vector::iterator outer_it;
    typedef std::vector<Peptide>::iterator inner_it;

    const double partition_size = 420.;
    
    ublas::vector<double> fit_weights;
    partitioned_vector partitioned_peps;
    vector<Peptide> peps;
    
    readTabFileStream(is, peps, tol, false, true);

    double minRt = minTime(peps);
    partitionPeptides(minRt, partition_size, peps, partitioned_peps);

    std::vector< ublas::vector<double> > estimate_vector;
    std::vector< ublas::vector<double> > rts_vector;
    std::vector< ublas::vector<double> > weights_diff;
    bool estimateTimes = true;
    double est_err = 0.;
    for (outer_it outer=partitioned_peps.begin();
         outer != partitioned_peps.end();
         outer++)
    {
        //for (inner_it inner=(*outer).begin(); inner!=(*outer).end(); inner++)
        //{
        cout << "loop elements: " << (*outer).size() << endl;;
        if ((*outer).size() < 21) continue;
        
        // Load the current set of peptides into 
        fit = FitFromPepXML::create();
        fit->setRtMin((float)rtmin);
        fit->setRtMax((float)rtmax);
        fit->createSystem(*outer);

        ublas::vector<double> next_weights = fit->fit();

        if (estimateTimes)
        {
            // --- System vectors & matrix ---
            cout << "system: " << fit->getSystem() << endl;
            const ublas::matrix<double>& counts = fit->getSystem()->getPeptideCounts();
            const ublas::vector<double>& rts = fit->getSystem()->getRetentionTimes();
            
            //cout << "rts:";
            //printVector(rts);
            
            ublas::vector<double> estimated = ublas::prod(counts, next_weights);
            
            // --- Wieghts ---
            //cout << "\n\nestimate = ";
            //printVector(estimated);
            //cout << "\n" << endl;

            estimate_vector.push_back(estimated);
            rts_vector.push_back(rts);
            if (showRMS)
            {
                double rms = sqrt(inner_prod(estimated, rts));
                cout << "rms = " << rms << endl;
            }
            if (showEstimateDiff)
            {
                ublas::vector<double> diff = next_weights - rts;
                weights_diff.push_back(diff);
                est_err += inner_prod(diff, diff);
            }
        }
        fit_weights = next_weights;
        //}
    }

    est_err /= peps.size();
    cerr << "Estimate error = " << sqrt(est_err) << endl;

    // stuff
    ublas::vector<double> v = var(estimate_vector);
    cout << "var(estimate_vector) = \n";
    for (ublas::vector<double>::iterator i=v.begin(); i!= v.end(); i++)
        cout << setw(10) << *i << "\n";
    cout << endl;
}

void print_help(po::options_description& opts)
{
    std::cout << opts;
}

int main(int argc, char** argv)
{
    double rtmin = -1., rtmax = -1.;
    double tol = 0.9;
    bool haveIn = false;
    std::vector<string> inList;
    
    // Get the command line args
    po::options_description inOptions("Input");
    inOptions.add_options()
        ("in,i", po::value< std::vector<string> >(&inList));

    po::options_description endOptions("Other");
    endOptions.add_options()
        ("help,h", "print this help message.")
        ("version,v", "print the version and exit.");

    po::options_description all("Options");
    all.add(inOptions);
    all.add(endOptions);

    po::positional_options_description p;
    p.add("in", -1);
 
    po::variables_map vm;

    try {
        po::parsed_options parsed = po::command_line_parser(argc, argv).
            options(all).positional(p).allow_unregistered().run();

        
        po::store(parsed, vm);

        std::vector<string> unknown_options =
            po::collect_unrecognized(parsed.options, po::exclude_positional);
        if (unknown_options.size() > 0)
        {
            print_help(all);
            return 1;
        }
    
    } catch (boost::program_options::unknown_option uo) {
        print_help(all);
        exit(1);
    }
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

    // Process input files
    istream* is;
    string inFile;
    if (vm.count("in"))
    {
        vector<string> v = vm["in"].as<vector<string> >();

        assert (v.size()>0);
        inFile = *(v.begin());

        haveIn = true;
        is = new std::ifstream(inFile.c_str());
    }
    else
    {
        is = &cin;
    }

    cout << "checkpoint 1" << endl;
    process(is, rtmin, rtmax, tol);
    cout << "checkpoint 6" << endl;
    
    if (haveIn)
        delete is;
    
    return 0;
}
