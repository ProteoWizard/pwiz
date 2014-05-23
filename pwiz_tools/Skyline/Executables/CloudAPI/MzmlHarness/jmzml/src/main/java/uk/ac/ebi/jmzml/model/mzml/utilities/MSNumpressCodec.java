/*
 * Date: 05/01/2014
 * Author: sperkins
 * File: uk.ac.ebi.jmzml.model.mzml.utilities.MSNumpressCodec
 *
 * jmzml is Copyright 2008 The European Bioinformatics Institute
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 *
 *
 */

package uk.ac.ebi.jmzml.model.mzml.utilities;

import java.util.ArrayList;
import java.util.List;
import uk.ac.ebi.jmzml.model.mzml.CVParam;

/**
 *
 * @author sperkins
 */
public final class MSNumpressCodec {
    private static final List<String> NUMPRESS_ACS = getMSNumpressACs();
    public static String getMSNumpressEncodingAccession(List<CVParam> params) {
        for (CVParam param : params) {
            if (NUMPRESS_ACS.contains(param.getAccession())) {
                return param.getAccession();
            }
        }
        
        return null;
    }
    
    public static Double[] decode(String numpressAccession, byte[] data) {
        return MSNumpress.decode(numpressAccession, data);
    }
    
    public static byte[] encode(double[] data, String numpressParamAccession) {
        return MSNumpress.encode(data, numpressParamAccession);
    }
    
    private static List<String> getMSNumpressACs() {
        List<String> msNumpressACs = new ArrayList<String>();
        msNumpressACs.add(MSNumpress.ACC_NUMPRESS_LINEAR);
        msNumpressACs.add(MSNumpress.ACC_NUMPRESS_PIC);
        msNumpressACs.add(MSNumpress.ACC_NUMPRESS_SLOF);
        return msNumpressACs;
    }
    
    
}
