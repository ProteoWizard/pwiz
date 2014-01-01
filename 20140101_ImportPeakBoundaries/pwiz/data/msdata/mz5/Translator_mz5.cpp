//
// $Id$
//
//
// Original authors: Mathias Wilhelm <mw@wilhelmonline.com>
//                   Marc Kirchner <mail@marc-kirchner.de>
//
// Copyright 2011 Proteomics Center
//                Children's Hospital Boston, Boston, MA 02135
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

#include "Translator_mz5.hpp"
#include <iostream>

namespace pwiz {
namespace msdata {
namespace mz5 {

void Translator_mz5::translate(std::vector<double>& mz,
        std::vector<double>& inten)
{
    translateMZ(mz);
    translateIntensity(inten);
}

void Translator_mz5::translateMZ(std::vector<double>& mz)
{
    double s = 0;
    size_t ms = mz.size();
    for (size_t i = 0; i < ms; ++i)
    {
        mz[i] = mz[i] - s;
        s += mz[i];
    }
}

void Translator_mz5::translateIntensity(std::vector<double>& inten)
{

}

void Translator_mz5::reverseTranslate(std::vector<double>& mz, std::vector<
        double>& inten)
{
    reverseTranslateMZ(mz);
    reverseTranslateIntensity(inten);
}

void Translator_mz5::reverseTranslateMZ(std::vector<double>& mz)
{
    size_t ms = mz.size();
    double s = 0;
    for (size_t i = 0; i < ms; ++i)
    {
        mz[i] = mz[i] + s;
        s = mz[i];
    }
}

void Translator_mz5::reverseTranslateIntensity(std::vector<double>&)
{

}

}
}
}
