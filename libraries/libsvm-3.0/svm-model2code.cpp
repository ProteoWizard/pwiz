//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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

#include "svm.h"
#include <vector>
#include <fstream>
#include <boost/format.hpp>
#include <boost/lexical_cast.hpp>
#include <boost/algorithm/string.hpp>
#include <boost/archive/text_oarchive.hpp>
#include <boost/serialization/serialization.hpp>
#include <boost/serialization/split_free.hpp>
#include <boost/iostreams/filtering_stream.hpp>
#include <boost/iostreams/filter/zlib.hpp>
#include <boost/iostreams/filter/base64.hpp>
#include <boost/iostreams/device/back_inserter.hpp>
#include <boost/iostreams/device/array.hpp>
#include <boost/iostreams/device/file.hpp>
#include <boost/iostreams/copy.hpp>
#include <boost/iostreams/compose.hpp>
#include <boost/filesystem.hpp>


using namespace std;
using boost::format;
using boost::lexical_cast;
using pwiz::svm;
namespace bio = boost::iostreams;


namespace boost { namespace serialization {
template<typename Archive>
void serialize(Archive& a, svm_parameter& param, const unsigned int version)
{
    a & param.svm_type & param.kernel_type & param.degree & param.gamma & param.coef0;
}

template<typename Archive>
void serialize(Archive& a, svm_node& node, const unsigned int version)
{
    a & node.index & node.value;
}

template<typename Archive>
void load(Archive& a, svm_model& model, const unsigned int version)
{
    a & model.param & model.nr_class & model.l;

    model.SV = (svm_node**) malloc(model.l * sizeof(svm_node*));
    for (int i=0; i < model.l; ++i)
    {
        vector<svm_node> nodes;
        for (int j=0;;++j)
        {
            nodes.push_back(svm_node());
            a & nodes.back();
            if (nodes.back().index == -1)
                break;
        }
        model.SV[i] = (svm_node*) malloc(nodes.size() * sizeof(svm_node));
        copy(nodes.begin(), nodes.end(), model.SV[i]);
    }

    model.sv_coef = (double**) malloc((model.nr_class-1) * sizeof(double*));
    for (int i=0; i < model.nr_class-1; ++i)
    {
        model.sv_coef[i] = (double*) malloc(model.l * sizeof(double));
        for (int j=0; j < model.l; ++j)
            a & model.sv_coef[i][j];
    }

    int numRho = model.nr_class*(model.nr_class-1)/2;
    model.rho = (double*) malloc(numRho * sizeof(double));
    model.probA = (double*) malloc(numRho * sizeof(double));
    model.probB = (double*) malloc(numRho * sizeof(double));
    for (int i=0; i < numRho; ++i)
        a & model.rho[i] & model.probA[i] & model.probB[i];
    
    model.label = (int*) malloc(model.nr_class * sizeof(int));
    model.nSV = (int*) malloc(model.nr_class * sizeof(int));
    for (int i=0; i < model.nr_class; ++i)
        a & model.label[i] & model.nSV[i];

    a & model.free_sv;
}

template<typename Archive>
void save(Archive& a, const svm_model& model, const unsigned int version)
{
    a & model.param & model.nr_class & model.l;

    for (int i=0; i < model.l; ++i)
        for (int j=0;;++j)
        {
            a & model.SV[i][j];
            if (model.SV[i][j].index == -1)
                break;
        }

    for (int i=0; i < model.nr_class-1; ++i)
        for (int j=0; j < model.l; ++j)
            a & model.sv_coef[i][j];

    for (int i=0, end=model.nr_class*(model.nr_class-1)/2; i < end; ++i)
        a & model.rho[i] & model.probA[i] & model.probB[i];

    for (int i=0; i < model.nr_class; ++i)
        a & model.label[i] & model.nSV[i];

    a & model.free_sv;
}

}}

BOOST_SERIALIZATION_SPLIT_FREE(svm_model)


int main(int argc, char* argv[])
{
    if (argc != 2)
    {
        cerr << "Usage: svm-model2code <model filepath>" << endl;
        return 1;
    }

    boost::filesystem::path filepath(argv[1]);

    // load the model from disk
    svm_model* model = svm_load_model(filepath.string().c_str());

    // serialize the model
    stringstream decoded;
    boost::archive::text_oarchive a(decoded);
    a & *model;
    svm_free_and_destroy_model(&model);

    // compress and base64 encode the serialized model
    stringstream encoded;
    bio::copy(decoded,
              bio::compose(bio::zlib_compressor(bio::zlib_params::zlib_params(bio::zlib::best_compression)),
              bio::compose(bio::base64_encoder(),
                           encoded)));
    string encodedString = encoded.str();

    // write a dummy header file for the model, limit each line to 4000 characters
    ofstream code((filepath.string() + ".hpp").c_str());
    code << "const std::string model =\nstd::string(\"";
    copy(encodedString.begin(), min(encodedString.end(), encodedString.begin()+4000), ostream_iterator<char>(code));
    code << "\") +\n";
    for (size_t i=4000; i < encodedString.length(); i += 4090)
    {
        code << "\"";
        copy(encodedString.begin()+i, min(encodedString.end(), encodedString.begin()+i+4090), ostream_iterator<char>(code));
        code << "\"" << (encodedString.end() > encodedString.begin()+i+4090 ? " +\n" : ";\n");
    }

    return 0;
}