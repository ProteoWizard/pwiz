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

#ifndef TRANSLATOR_MZ5_HPP_
#define TRANSLATOR_MZ5_HPP_

#include <vector>

namespace pwiz {
namespace msdata {
namespace mz5 {

/**
 * Helper class to translate and filter mz and intensity values.
 */
class Translator_mz5
{
public:
    /**
     * Removes 0 intensity values in a mass spectra.
     * @param mz mz data
     * @param inten intensity data
     */
    static void filter(std::vector<double>& mz, std::vector<double>& inten);
    //	static void filterMZ(std::vector<double>&);
    //	static void filterIntensity(std::vector<double>&);

    /**
     * Translates mz and intensity values.
     * @param mz mz data
     * @param inten intensity data
     */
    static void translate(std::vector<double>& mz, std::vector<double>& inten);

    /**
     * Translates a mz data array with the function
     * f[0] = f[0]
     * f[i] = f[i] - f[i-1]
     *
     * This creates delta mz values in O(n) time but only needs one additional double.
     * @param mz untranslated mz data
     */
    static void translateMZ(std::vector<double>& mz);

    /**
     * Currently empty but can be used to alter intensity values.
     */
    static void translateIntensity(std::vector<double>& inten);

    /**
     * Recalculates mz and intensity data.
     * @param mz mz data
     * @param inten intensity data
     */
    static void reverseTranslate(std::vector<double>& mz,
            std::vector<double>& inten);

    /**
     * Translates a mz data array with the function:
     * f[0] = f[0]
     * f[i] = f[i-1] + f[i]
     *
     * This method creates absolute mz values in O(n) time but only needs one additional double.
     * @param mz translated mz data
     */
    static void reverseTranslateMZ(std::vector<double>&);

    /**
     * Currently empty but can be used to reverse the function translateIntensity()
     */
    static void reverseTranslateIntensity(std::vector<double>&);
};

}
}
}

#endif /* TRANSLATOR_MZ5_HPP_ */
