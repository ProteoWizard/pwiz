/*
 * Date: 22/7/2008
 * Author: rcote
 * File: uk.ac.ebi.jmzml.xml.xxindex.MzMLIndexer
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

package uk.ac.ebi.jmzml.xml.xxindex;

import psidev.psi.tools.xxindex.index.IndexElement;

import java.util.Iterator;
import java.util.List;
import java.util.Set;

public interface MzMLIndexer {

    public Iterator<String> getXmlStringIterator(String xpathExpression);

    public String getXmlString(String ID, Class clazz);

    public String getXmlString(IndexElement indexElement);

    public int getCount(String xpathExpression);

    public String getXmlString(String xpath, long offset);

    public List<IndexElement> getIndexElements(String xpathExpression);

    public Set<String> getXpath();

    public Set<String> getSpectrumIDs();

    public Set<Integer> getSpectrumIndexes();

    public String getSpectrumIDFromSpectrumIndex(Integer index);

    public Set<String> getChromatogramIDs();

    public String getMzMLAttributeXMLString();

    public String getStartTag(String xpath);

    /**
     * @param id    the unique ID (from the id attribute) of an XML element.
     * @param clazz the Java Class representing the element.
     * @return the complete start tag for the XML element with all specified attributes.
     */
    public String getStartTag(String id, Class clazz);

}
