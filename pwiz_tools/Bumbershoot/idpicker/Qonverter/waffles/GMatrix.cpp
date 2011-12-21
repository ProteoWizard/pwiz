/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GMatrix.h"
#include "GError.h"
#include "GMath.h"
#include "GDistribution.h"
#include "GVec.h"
#include "GFile.h"
#include "GHeap.h"
#include "GDom.h"
#include "GHashTable.h"
#include <math.h>
#include "GBits.h"
#include "GLearner.h"
#include "GRand.h"
#include "GTokenizer.h"
#include "GNeighborFinder.h"
#include "GDistance.h"
#include <algorithm>
#include <fstream>
#include <sstream>
#include <cmath>
#include <set>

using namespace GClasses;
using std::vector;
using std::string;
using std::ostream;
using std::ostringstream;

// static
smart_ptr<GRelation> GRelation::deserialize(GDomNode* pNode)
{
	if(pNode->fieldIfExists("attrs"))
	{
		sp_relation sp;
		sp = new GUniformRelation(pNode);
		return sp;
	}
	else
	{
		sp_relation sp;
		sp = new GMixedRelation(pNode);
		return sp;
	}
}

void GRelation::print(ostream& stream, GMatrix* pData, size_t precision)
{
	stream.precision(precision);

	// Write the relation title
	stream << "@RELATION Untitled\n\n";

	// Write the attributes
	for(size_t i = 0; i < size(); i++)
	{
		stream << "@ATTRIBUTE ";
		printAttrName(stream, i);
		stream << "\t";
		if(valueCount(i) == 0)
			stream << "real";
		else
		{
			stream << "{";
			for(size_t j = 0; j < valueCount(i); j++)
			{
				if(j > 0)
					stream << ",";
				printAttrValue(stream, i, (double)j);
			}
			stream << "}";
		}
		stream << "\n";
	}

	// Write the data
	stream << "\n@DATA\n";
	if(!pData)
		return;
	for(size_t i = 0; i < pData->rows(); i++)
		printRow(stream, pData->row(i), ",");
}

// virtual
void GRelation::printAttrName(std::ostream& stream, size_t column)
{
	stream << "attr_" << column;
}

// virtual
void GRelation::printAttrValue(ostream& stream, size_t column, double value)
{
	size_t valCount = valueCount(column);
	if(valCount == 0)
	{
		if(value == UNKNOWN_REAL_VALUE)
			stream << "?";
		else
			stream << value;
	}
	else
	{
		int val = (int)value;
		if(val < 0)
			stream << "?";
		else if(val >= (int)valCount)
			ThrowError("value out of range");
		else if(val < 26)
		{
			char tmp[2];
			tmp[0] = 'a' + val;
			tmp[1] = '\0';
			stream << tmp;
		}
		else
			stream << "_" << val;
	}
}

// virtual
bool GRelation::isCompatible(GRelation& that)
{
	if(this == &that)
		return true;
	if(size() != that.size())
		return false;
	for(size_t i = 0; i < size(); i++)
	{
		if(valueCount(i) != that.valueCount(i))
			return false;
	}
	return true;
}

void GRelation::printRow(ostream& stream, double* pRow, const char* separator)
{
	size_t j = 0;
	if(j < size())
	{
		printAttrValue(stream, j, *pRow);
		pRow++;
		j++;
	}
	for(; j < size(); j++)
	{
		stream << separator;
		printAttrValue(stream, j, *pRow);
		pRow++;
	}
	stream << "\n";
}

size_t GRelation::countRealSpaceDims(size_t nFirstAttr, size_t nAttrCount)
{
	size_t nDims = 0;
	for(size_t i = 0; i < nAttrCount; i++)
	{
		size_t nValues = valueCount(nFirstAttr + i);
		if(nValues == 0)
			nDims += 2;
		else
			nDims += nValues;
	}
	return nDims;
}

void GRelation::toRealSpace(const double* pIn, double* pOut, size_t nFirstAttr, size_t nAttrCount)
{
	size_t nDims = 0;
	size_t i, j, k, nValues;
	for(i = 0; i < nAttrCount; i++)
	{
		nValues = valueCount(nFirstAttr + i);
		if(nValues == 0)
		{
			pOut[nDims++] = pIn[i];
			if(pIn[i] == UNKNOWN_REAL_VALUE)
				pOut[nDims++] = UNKNOWN_REAL_VALUE;
			else
				pOut[nDims++] = pIn[i] * pIn[i];
		}
		else
		{
			k = nDims;
			for(j = 0; j < nValues; j++)
				pOut[nDims++] = 0;
			if(pIn[i] >= 0) // For unknown discrete values, set to all zeros.
			{
				GAssert(pIn[i] >= 0 && pIn[i] < nValues);
				pOut[k + (size_t)pIn[i]] = 1;
			}
		}
	}
}

void GRelation::fromRealSpace(const double* pIn, double* pOut, size_t nFirstAttr, size_t nAttrCount, GRand* pRand)
{
	size_t nDims = 0;
	size_t i, nValues;
	for(i = 0; i < nAttrCount; i++)
	{
		nValues = valueCount(nFirstAttr + i);
		if(nValues == 0)
		{
			pOut[i] = pIn[nDims++];
			nDims++;
		}
		else
		{
			pOut[i] = (double)GVec::indexOfMax(&pIn[nDims], nValues, pRand);
			nDims += nValues;
		}
	}
}

void GRelation::fromRealSpace(const double* pIn, GPrediction* pOut, size_t nFirstAttr, size_t nAttrCount)
{
	size_t nDims = 0;
	size_t i, nValues;
	for(i = 0; i < nAttrCount; i++)
	{
		nValues = valueCount(nFirstAttr + i);
		if(nValues == 0)
		{
			GNormalDistribution* pNorm = pOut[i].makeNormal();
			double mean = pIn[nDims++];
			double variance = pIn[nDims++] - (mean * mean);
			pNorm->setMeanAndVariance(mean, variance);
		}
		else
		{
			GCategoricalDistribution* pCat = pOut[i].makeCategorical();
			pCat->setValues(nValues, &pIn[nDims]);
			nDims += nValues;
		}
	}
}

void GRelation::save(GMatrix* pData, const char* szFilename, size_t precision)
{
	std::ofstream stream;
	stream.exceptions(std::ios::failbit|std::ios::badbit);
	try
	{
		stream.open(szFilename, std::ios::binary);
	}
	catch(const std::exception&)
	{
		ThrowError("Error creating file: ", szFilename);
	}
	print(stream, pData, precision);
}

#ifndef NO_TEST_CODE
//static
void GRelation::test()
{
	typedef std::string s;
	TestEqual
		("the",quote("the"),
		 "GRelation::quote gets (the) wrong");								

	TestEqual("'the rain'", quote("the rain"),
							"GRelation::quote gets (the rain) wrong");

	TestEqual("the\\ rain\\'s\\ \\\\mom",
							quote("the rain's \\mom"),
							"GRelation::quote gets 'the rain's \\mom' wrong");

	TestEqual("'%'", quote("%"), "GRelation::quote gets (%) wrong");

	TestEqual("','", quote(","), "GRelation::quote gets (,) wrong");

	TestEqual("' '", quote(" "), "GRelation::quote gets ( ) wrong");

	TestEqual("\\'", quote("'"), "GRelation::quote gets (') wrong");

	TestEqual("'\\'", quote("\\"), "GRelation::quote gets (\\) wrong");

	TestEqual("'\"'", quote("\""), "GRelation::quote gets (\") wrong");

	TestEqual("Dow\\'s\\ rise\\ (\\%)",
							quote("Dow's rise (%)"),
							"GRelation::quote gets 'Dow's rise (%)' wrong");

	TestEqual("\\\"Rise\\'\\\"\\,\\\"Run\\'\\\"",
							quote("\"Rise'\",\"Run'\""),
							"GRelation::quote gets '\"Rise'\",\"Run'\"' wrong");
}
#endif // !NO_TEST_CODE










GUniformRelation::GUniformRelation(GDomNode* pNode)
{
	m_attrCount = (size_t)pNode->field("attrs")->asInt();
	m_valueCount = (size_t)pNode->field("vals")->asInt();
}

GDomNode* GUniformRelation::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "attrs", pDoc->newInt(m_attrCount));
	pNode->addField(pDoc, "vals", pDoc->newInt(m_valueCount));
	return pNode;
}

// virtual
void GUniformRelation::deleteAttribute(size_t index)
{
	if(index >= m_attrCount)
		ThrowError("Index out of range");
	m_attrCount--;
}

// virtual
bool GUniformRelation::isCompatible(GRelation& that)
{
	if(that.type() == GRelation::UNIFORM)
	{
		if(m_attrCount == ((GUniformRelation*)&that)->m_attrCount && m_valueCount == ((GUniformRelation*)&that)->m_valueCount)
			return true;
		else
			return false;
	}
	else
		return GRelation::isCompatible(that);
}




GMixedRelation::GMixedRelation()
{
}

GMixedRelation::GMixedRelation(vector<size_t>& attrValues)
{
	m_valueCounts.reserve(attrValues.size());
	for(vector<size_t>::iterator it = attrValues.begin(); it != attrValues.end(); it++)
		addAttr(*it);
}

GMixedRelation::GMixedRelation(GDomNode* pNode)
{
	m_valueCounts.clear();
	GDomNode* pValueCounts = pNode->field("valueCounts");
	GDomListIterator it(pValueCounts);
	m_valueCounts.reserve(it.remaining());
	for( ; it.current(); it.advance())
		m_valueCounts.push_back((size_t)it.current()->asInt());
}

GMixedRelation::GMixedRelation(GRelation* pCopyMe)
{
	copy(pCopyMe);
}

GMixedRelation::GMixedRelation(GRelation* pCopyMe, size_t firstAttr, size_t attrCount)
{
	addAttrs(pCopyMe, firstAttr, attrCount);
}

// virtual
GMixedRelation::~GMixedRelation()
{
}

GDomNode* GMixedRelation::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	GDomNode* pValueCounts = pNode->addField(pDoc, "valueCounts", pDoc->newList());
	for(size_t i = 0; i < m_valueCounts.size(); i++)
		pValueCounts->addItem(pDoc, pDoc->newInt(m_valueCounts[i]));
	return pNode;
}

// virtual
GRelation* GMixedRelation::clone()
{
	GMixedRelation* pNewRelation = new GMixedRelation();
	pNewRelation->addAttrs(this, 0, size());
	return pNewRelation;
}

// virtual
GRelation* GMixedRelation::cloneSub(size_t start, size_t count)
{
	GMixedRelation* pNewRelation = new GMixedRelation();
	pNewRelation->addAttrs(this, start, count);
	return pNewRelation;
}

void GMixedRelation::addAttrs(GRelation* pCopyMe, size_t firstAttr, size_t attrCount)
{
	if(firstAttr + attrCount > pCopyMe->size())
	{
		if(attrCount == (size_t)-1)
			attrCount = pCopyMe->size() - firstAttr;
		else
			ThrowError("out of range");
	}
	for(size_t i = 0; i < attrCount; i++)
		copyAttr(pCopyMe, firstAttr + i);
}

void GMixedRelation::addAttrs(size_t attrCount, size_t valueCount)
{
	for(size_t i = 0; i < attrCount; i++)
		addAttr(valueCount);
}

void GMixedRelation::copy(GRelation* pCopyMe)
{
	flush();
	if(pCopyMe)
		addAttrs(pCopyMe);
}

// virtual
void GMixedRelation::flush()
{
	m_valueCounts.clear();
}

void GMixedRelation::addAttr(size_t nValues)
{
	if(type() == ARFF)
	{
		ostringstream oss;
		oss << "attr_";
		oss << size();
		string s = oss.str();
		((GArffRelation*)this)->addAttribute(s.c_str(), nValues, NULL);
	}
	else
		m_valueCounts.push_back(nValues);
}

// virtual
void GMixedRelation::copyAttr(GRelation* pThat, size_t nAttr)
{
	if(nAttr >= pThat->size())
		ThrowError("attribute index out of range");
	addAttr(pThat->valueCount(nAttr));
}

// virtual
bool GMixedRelation::areContinuous(size_t first, size_t count)
{
	size_t c = first;
	for(size_t i = 0; i < count; i++)
	{
		if(valueCount(c) != 0)
			return false;
		c++;
	}
	return true;
}

// virtual
bool GMixedRelation::areNominal(size_t first, size_t count)
{
	size_t c = first;
	for(size_t i = 0; i < count; i++)
	{
		if(valueCount(c) == 0)
			return false;
		c++;
	}
	return true;
}

// virtual
void GMixedRelation::swapAttributes(size_t nAttr1, size_t nAttr2)
{
	std::swap(m_valueCounts[nAttr1], m_valueCounts[nAttr2]);
}

// virtual
void GMixedRelation::deleteAttribute(size_t nAttr)
{
	m_valueCounts.erase(m_valueCounts.begin() + nAttr);
}

// virtual
void GMixedRelation::setAttrValueCount(size_t nAttr, size_t nValues)
{
	m_valueCounts[nAttr] = nValues;
}

// ------------------------------------------------------------------

GArffRelation::GArffRelation()
{
}

GArffRelation::~GArffRelation()
{
}

// virtual
void GArffRelation::flush()
{
	GAssert(m_attrs.size() == m_valueCounts.size());
	m_attrs.clear();
	GMixedRelation::flush();
}

// virtual
GRelation* GArffRelation::clone()
{
	GArffRelation* pNewRelation = new GArffRelation();
	pNewRelation->addAttrs(this);
	pNewRelation->setName(name());
	return pNewRelation;
}

// virtual
GRelation* GArffRelation::cloneSub(size_t start, size_t count)
{
	GArffRelation* pNewRelation = new GArffRelation();
	pNewRelation->addAttrs(this, start, count);
	pNewRelation->setName(name());
	return pNewRelation;
}

void GArffRelation::addAttributeInternal(const char* pName, size_t nameLen, size_t valueCount)
{
	GAssert(m_attrs.size() == m_valueCounts.size());
	size_t index = m_valueCounts.size();
	m_valueCounts.push_back(valueCount);
	m_attrs.resize(index + 1);
	m_attrs[index].m_name.append(pName, nameLen);
}

void GArffRelation::addAttribute(const char* szName, size_t nValues, vector<const char*>* pValues)
{
	GAssert(m_attrs.size() == m_valueCounts.size());
	size_t index = m_valueCounts.size();
	m_valueCounts.push_back(nValues);
	m_attrs.resize(index + 1);
	if(szName)
		m_attrs[index].m_name = szName;
	else
	{
		m_attrs[index].m_name = "attr_";
		std::ostringstream oss;
		oss << index;
		m_attrs[index].m_name += oss.str();
	}
	if(pValues)
	{
		if(nValues != pValues->size())
			ThrowError("mismatching value counts");
		for(size_t i = 0; i < nValues; i++)
			m_attrs[index].m_values.push_back((*pValues)[i]);
	}
}

// virtual
void GArffRelation::copyAttr(GRelation* pThat, size_t nAttr)
{
	if(nAttr >= pThat->size())
		ThrowError("attribute index out of range");
	if(pThat->type() == ARFF)
	{
		size_t index = m_valueCounts.size();
		GArffRelation* pOther = (GArffRelation*)pThat;
		addAttributeInternal(pOther->m_attrs[nAttr].m_name.c_str(), pOther->m_attrs[nAttr].m_name.length(), pOther->m_valueCounts[nAttr]);
		for(size_t i = 0; i < pOther->m_attrs[nAttr].m_values.size(); i++)
			m_attrs[index].m_values.push_back(pOther->m_attrs[nAttr].m_values[i]);
	}
	else
		addAttribute(NULL, pThat->valueCount(nAttr), NULL);
}

void GArffRelation::setName(const char* szName)
{
	m_name = szName;
}

void GArffRelation::parseAttribute(GTokenizer& tok)
{
	GCharSet& spaces = tok.charSet(" \t");
	GCharSet& valEnd = tok.charSet(",}\n");
	GCharSet& whitespace = tok.charSet("\t\n\r ");
	GCharSet& argEnd = tok.charSet(" \t\n{\r");
	tok.skip(spaces);
	string name = tok.nextArg(argEnd);
	//std::cerr << "Attr:" << name << "\n"; //DEBUG
	tok.skip(spaces);
	char c = tok.peek();
	if(c == '{')
	{
		tok.advance(1);
		GAssert(m_attrs.size() == m_valueCounts.size());
		size_t index = m_valueCounts.size();
		m_attrs.resize(index + 1);
		while(true)
		{
			tok.nextArg(valEnd);
			char* szVal = tok.trim(whitespace);
			if(*szVal == '\0')
				ThrowError("Empty value specified on line ", to_str(tok.line()));
			if(*szVal == '\'')
			{
				size_t len = strlen(szVal);
				if(len > 1 && szVal[len - 1] == '\'')
				{
					szVal[len - 1] = '\0';
					szVal++;
				}
			}
			else if(*szVal == '"')
			{
				size_t len = strlen(szVal);
				if(len > 1 && szVal[len - 1] == '"')
				{
					szVal[len - 1] = '\0';
					szVal++;
				}
			}
			m_attrs[index].m_values.push_back(szVal);
			char c = tok.peek();
			if(c == ',')
				tok.advance(1);
			else if(c == '}')
				break;
			else if(c == '\n')
				ThrowError("Expected a '}' but got new-line on line ", to_str(tok.line()));
			else
				ThrowError("inconsistency");
		}
		m_valueCounts.push_back(m_attrs[index].m_values.size());
		if(name.length() > 0)
			m_attrs[index].m_name = name;
		else
		{
			m_attrs[index].m_name = "attr_";
			std::ostringstream oss;
			oss << index;
			m_attrs[index].m_name += oss.str();
		}
	}
	else
	{
		const char* szType = tok.nextUntil(whitespace);
		if(	_stricmp(szType, "CONTINUOUS") == 0 ||
			_stricmp(szType, "REAL") == 0 ||
			_stricmp(szType, "NUMERIC") == 0 ||
			_stricmp(szType, "INTEGER") == 0)
		{
			addAttribute(name.c_str(), 0, NULL);
		}
		else
			ThrowError("Unsupported attribute type: (", szType, "), at line ", to_str(tok.line()));
	}
	tok.skipTo(tok.charSet("\n"));
	tok.advance(1);
}

// virtual
void GArffRelation::printAttrName(std::ostream& stream, size_t column)
{
	stream << GRelation::quote(attrName(column));
}

// static
std::string GRelation::quote(const std::string aString){
	typedef std::string::const_iterator iter;

	//If the string has no bad characters, just return a copy
	std::size_t firstBad = aString.find_first_of(",' %\\\"");
	std::string ret(aString);
	if(firstBad == string::npos){
		return ret;
	}

	//The string has bad characters, start over
	ret.clear();

	//If the string has no apostrophes, just quote it with single quotes
	std::size_t firstApostrophe = aString.find_first_of('\'');
	if(firstApostrophe == string::npos){
		ret.push_back('\'');
		ret.append(aString);
		ret.push_back('\'');
		return ret;
	}


	//Otherwise, use backslash to quote every character
	ret.reserve(2*aString.size());
	for(iter c=aString.begin();c != aString.end(); ++c)
	{
		if(*c == ','  || *c == '\'' ||
			 *c == ' '  || *c == '%' ||
			 *c == '\\' || *c == '"')
		{
			ret.push_back('\\');
		}
		ret.push_back(*c);
	}
	return ret;
}


// virtual
void GArffRelation::printAttrValue(ostream& stream, size_t column, double value)
{
	size_t valCount = valueCount(column);
	if(valCount == 0)
	{
		if(value == UNKNOWN_REAL_VALUE)
			stream << "?";
		else
			stream << value;
	}
	else
	{
		int val = (int)value;
		if(val < 0)
			stream << "?";
		else if(val >= (int)valCount)
			ThrowError("value out of range");
		else if(m_attrs[column].m_values.size() > 0)
			stream << GRelation::quote(m_attrs[column].m_values[val]);
		else if(val < 26)
		{
			char tmp[2];
			tmp[0] = 'a' + val;
			tmp[1] = '\0';
			stream << tmp;
		}
		else
			stream << "_" << val;
	}
}

// virtual
bool GArffRelation::isCompatible(GRelation& that)
{
	if(that.type() == GRelation::ARFF)
	{
		if(this == &that)
			return true;
		if(!GRelation::isCompatible(that))
			return false;
		for(size_t i = 0; i < size() ; i++)
		{
			if(((GArffRelation*)this)->attrName(i)[0] != '\0' && ((GArffRelation*)&that)->attrName(i)[0] != '\0' && strcmp(((GArffRelation*)this)->attrName(i), ((GArffRelation*)&that)->attrName(i)) != 0)
				return false;
			size_t vals = valueCount(i);
			if(vals != 0)
			{
				for(size_t j = 0; j < vals; j++)
				{
					GArffAttribute& attrThis = m_attrs[j];
					GArffAttribute& attrThat = ((GArffRelation*)&that)->m_attrs[j];
					if(attrThis.m_values.size() >= j &&
						attrThat.m_values.size() >= j &&
						attrThis.m_values[j].length() != 0 &&
						attrThat.m_values[j].length() != 0 &&
						strcmp(attrThis.m_values[j].c_str(), attrThat.m_values[j].c_str()) != 0)
						return false;
				}
			}
		}
		return true;
	}
	else
		return GRelation::isCompatible(that);
}

int GArffRelation::findEnumeratedValue(size_t nAttr, const char* szValue)
{
	size_t nValueCount = valueCount(nAttr);
	size_t actualValCount = m_attrs[nAttr].m_values.size();
	if(nValueCount > actualValCount)
		ThrowError("some values have no names");
	size_t i;
	for(i = 0; i < nValueCount; i++)
	{
		if(_stricmp(m_attrs[nAttr].m_values[i].c_str(), szValue) == 0)
			return (int)i;
	}
	return UNKNOWN_DISCRETE_VALUE;
}

const char* GArffRelation::attrName(size_t nAttr)
{
	return m_attrs[nAttr].m_name.c_str();
}

int GArffRelation::addAttrValue(size_t nAttr, const char* szValue)
{
	int val = (int)m_valueCounts[nAttr]++;
	GAssert(m_attrs[nAttr].m_values.size() == (size_t)val);
	m_attrs[nAttr].m_values.push_back(szValue);
	return val;
}

// virtual
void GArffRelation::setAttrValueCount(size_t nAttr, size_t nValues)
{
	m_attrs[nAttr].m_values.clear();
	GMixedRelation::setAttrValueCount(nAttr, nValues);
}

// virtual
void GArffRelation::swapAttributes(size_t nAttr1, size_t nAttr2)
{
	GMixedRelation::swapAttributes(nAttr1, nAttr2);
	std::swap(m_attrs[nAttr1], m_attrs[nAttr2]);
}

// virtual
void GArffRelation::deleteAttribute(size_t nAttr)
{
	m_attrs.erase(m_attrs.begin() + nAttr);
	GMixedRelation::deleteAttribute(nAttr);
}

double GArffRelation::parseValue(size_t attr, const char* val)
{
	size_t values = valueCount(attr);
	if(values == 0)
	{
		if(strcmp(val, "?") == 0)
			return UNKNOWN_REAL_VALUE;
		else
		{
			if((*val >= '0' && *val <= '9') || *val == '-' || *val == '.')
			{
			}
			else
				ThrowError("Invalid real value, ", val, ". Expected it to start with one of {0-9,.,-}.");
			return atof(val);
		}
	}
	else
	{
		if(strcmp(val, "?") == 0)
			return UNKNOWN_DISCRETE_VALUE;
		else
		{
			size_t v = (size_t)-1;
			for(size_t j = 0; j < values; j++)
			{
				if(_stricmp(val, m_attrs[attr].m_values[j].c_str()) == 0)
				{
					v = j;
					break;
				}
			}
			if(v == (size_t)-1)
			{
				if(*val >= '0' && *val <= '9')
					v = atoi(val);
				else
				{
					string sChoices;
					for(size_t j = 0; j < values; j++)
					{
						if(j != 0)
							sChoices += ',';
						sChoices += m_attrs[attr].m_values[j].c_str();
					}
					ThrowError("Invalid categorical value, ", val, ". Expected one of {", sChoices, "}");
				}
			}
			return double(v);
		}
	}
}

// ------------------------------------------------------------------

GMatrix::GMatrix(sp_relation& pRelation, GHeap* pHeap)
: m_pRelation(pRelation), m_pHeap(pHeap)
{
}

GMatrix::GMatrix(size_t rows, size_t cols, GHeap* pHeap)
: m_pHeap(pHeap)
{
	m_pRelation = new GUniformRelation(cols, 0);
	newRows(rows);
}

GMatrix::GMatrix(vector<size_t>& attrValues, GHeap* pHeap)
: m_pHeap(pHeap)
{
	m_pRelation = new GMixedRelation(attrValues);
}

GMatrix::GMatrix(GDomNode* pNode, GHeap* pHeap)
: m_pHeap(pHeap)
{
	m_pRelation = GRelation::deserialize(pNode->field("rel"));
	GDomNode* pRows = pNode->field("pats");
	GDomListIterator it(pRows);
	reserve(it.remaining());
	size_t dims = (size_t)m_pRelation->size();
	double* pPat;
	for(size_t i = 0; it.current(); it.advance())
	{
		GDomNode* pRow = it.current();
		GDomListIterator it2(pRow);
		if(it2.remaining() != dims)
			ThrowError("Row ", to_str(i), " has an unexpected number of values");
		pPat = newRow();
		for( ; it2.current(); it2.advance())
		{
			*pPat = it2.current()->asDouble();
			pPat++;
		}
		i++;
	}
}

GMatrix::~GMatrix()
{
	flush();
}

void GMatrix::flush()
{
	if(!m_pHeap)
	{
		for(vector<double*>::iterator it = m_rows.begin(); it != m_rows.end(); it++)
			delete[] (*it);
	}
	m_rows.clear();
}

inline bool IsRealValue(const char* szValue)
{
	if(*szValue == '-')
		szValue++;
	if(*szValue == '.')
		szValue++;
	if(*szValue >= '0' && *szValue <= '9')
		return true;
	return false;
}

double GMatrix_parseValue(GArffRelation* pRelation, size_t col, const char* szVal, GTokenizer& tok)
{
	size_t vals = pRelation->valueCount(col);
	if(vals == 0)
	{
		// Continuous attribute
		if(*szVal == '\0' || (*szVal == '?' && szVal[1] == '\0'))
			return UNKNOWN_REAL_VALUE;
		else
		{
			if(!IsRealValue(szVal))
				ThrowError("Expected a numeric value at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
			return atof(szVal);
		}
	}
	else
	{
		// Nominal attribute
		if(*szVal == '\0' || (*szVal == '?' && szVal[1] == '\0'))
			return UNKNOWN_DISCRETE_VALUE;
		else
		{
			int nVal = pRelation->findEnumeratedValue(col, szVal);
			if(nVal == UNKNOWN_DISCRETE_VALUE)
				ThrowError("Unrecognized enumeration value '", szVal, "' for attribute ", to_str(col), " at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
			return (double)nVal;
		}
	}
}

GMatrix* GMatrix_parseArff(GTokenizer& tok)
{
	// Parse the meta data
	GCharSet& whitespace = tok.charSet("\t\n\r ");
	GCharSet& spaces = tok.charSet(" \t");
	GCharSet& space = tok.charSet(" ");
	GCharSet& newline = tok.charSet("\n");
	GCharSet& valEnder = tok.charSet(" ,\t}\n");
	GCharSet& valHardEnder = tok.charSet(",}\t\n");
	GCharSet& commaNewlineTab = tok.charSet(",\n\t");
	GArffRelation* pRelation = new GArffRelation();
	sp_relation sp_rel = pRelation;
	while(true)
	{
		tok.skip(whitespace);
		char c = tok.peek();
		if(c == '\0')
			ThrowError("Invalid ARFF file--contains no data");
		else if(c == '%')
		{
			tok.advance(1);
			tok.skipTo(newline);
		}
		else if(c == '@')
		{
			tok.advance(1);
			const char* szTok = tok.nextUntil(whitespace);
			if(_stricmp(szTok, "ATTRIBUTE") == 0)
				pRelation->parseAttribute(tok);
			else if(_stricmp(szTok, "RELATION") == 0)
			{
				tok.skip(spaces);
				pRelation->setName(tok.nextArg(tok.charSet("\t\n\r ")));
				tok.advance(1);
			}
			else if(_stricmp(szTok, "DATA") == 0)
			{
				tok.skipTo(newline);
				tok.advance(1);
				break;
			}
		}
		else
			ThrowError("Expected a '%' or a '@' at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
	}

	// Parse the data section
	GMatrix* pData = new GMatrix(sp_rel);
	Holder<GMatrix> hData(pData);
	size_t cols = pRelation->size();
	while(true)
	{
		tok.skip(whitespace);
		char c = tok.peek();
		if(c == '\0')
			break;
		else if(c == '%')
		{
			tok.advance(1);
			tok.skipTo(newline);
		}
		else if(c == '{')
		{
			tok.advance(1);
			double* pRow = pData->newRow();
			GVec::setAll(pRow, 0.0, cols);
			while(true)
			{
				tok.skip(space);
				char c = tok.peek();
				if(c >= '0' && c <= '9')
				{
					const char* szTok = tok.nextUntil(valEnder);
#ifdef WIN32
					size_t col = (size_t)_strtoui64(szTok, (char**)NULL, 10);
#else
					size_t col = strtoull(szTok, (char**)NULL, 10);
#endif
					if(col >= cols)
						ThrowError("Column index out of range at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
					tok.skip(spaces);
					const char* szVal = tok.nextArg(valEnder);
					pRow[col] = GMatrix_parseValue(pRelation, col, szVal, tok);
					tok.skipTo(valHardEnder);
					c = tok.peek();
					if(c == ',' || c == '\t')
						tok.advance(1);
				}
				else if(c == '}')
				{
					tok.advance(1);
					break;
				}
				else if(c == '\n' || c == '\0')
					ThrowError("Expected a matching '}' at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
				else
					ThrowError("Unexpected token at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
			}
		}
		else
		{
			double* pRow = pData->newRow();
			size_t col = 0;
			while(true)
			{
				if(col >= cols)
					ThrowError("Too many values on line ", to_str(tok.line()), ", col ", to_str(tok.col()));
				tok.nextArg(commaNewlineTab);
				const char* szVal = tok.trim(whitespace);
				*pRow = GMatrix_parseValue(pRelation, col, szVal, tok);
				pRow++;
				col++;
				char c = tok.peek();
				if(c == ',' || c == '\t')
					tok.advance(1);
				else if(c == '\n' || c == '\0')
					break;
				else
					ThrowError("inconsistency");
			}
			if(col < cols)
				ThrowError("Not enough values on line ", to_str(tok.line()), ", col ", to_str(tok.col()));
		}
	}
	return hData.release();
}

// static
GMatrix* GMatrix::loadArff(const char* szFilename)
{
	GTokenizer tok(szFilename);
	return GMatrix_parseArff(tok);
}

// static
GMatrix* GMatrix::loadCsv(const char* szFilename, char separator, bool columnNamesInFirstRow, bool tolerant)
{
	size_t nLen;
	char* szFile = GFile::loadFile(szFilename, &nLen);
	ArrayHolder<char> hFile(szFile);
	return parseCsv(szFile, nLen, separator, columnNamesInFirstRow, tolerant);
}

void GMatrix::saveArff(const char* szFilename)
{
	m_pRelation->save(this, szFilename, 14);
}

// static
GMatrix* GMatrix::parseArff(const char* szFile, size_t nLen)
{
	GTokenizer tok(szFile, nLen);
	return GMatrix_parseArff(tok);
}

class ImportRow
{
public:
	vector<const char*> m_elements;
};

// static
GMatrix* GMatrix::parseCsv(const char* pFile, size_t len, char separator, bool columnNamesInFirstRow, bool tolerant)
{
	// Extract the elements
	GHeap heap(2048);
	vector<ImportRow> rows;
	size_t elementCount = (size_t)-1;
	size_t nFirstDataLine = 1;
	size_t nLine = 1;
	size_t nPos = 0;
	while(true)
	{
		// Skip Whitespace
		while(nPos < len && pFile[nPos] <= ' ' && pFile[nPos] != separator)
		{
			if(pFile[nPos] == '\n')
				nLine++;
			nPos++;
		}
		if(nPos >= len)
			break;

		// Count the elements
		if(elementCount == (size_t)-1)
		{
			if(separator == '\0')
			{
				// Elements are separated by an arbitrary amount of whitespace, element values contain no whitespace, and there are no missing elements
				size_t i = nPos;
				elementCount = 0;
				while(true)
				{
					elementCount++;
					while(i < len && pFile[i] > ' ')
						i++;
					while(i < len && pFile[i] <= ' ' && pFile[i] != '\n')
						i++;
					if(pFile[i] == '\n')
						break;
				}
			}
			else
			{
				// Elements are separated by the specified character
				nFirstDataLine = nLine;
				elementCount = 1;
				for(size_t i = 0; pFile[nPos + i] != '\n' && pFile[nPos + i] != '\0'; i++)
				{
					if(pFile[nPos + i] == separator)
						elementCount++;
				}
			}
		}

		// Extract the elements from the row
		rows.resize(rows.size() + 1);
		ImportRow& row = rows[rows.size() - 1];
		while(true)
		{
			// Skip Whitespace
			while(nPos < len && pFile[nPos] <= ' ' && pFile[nPos] != separator)
			{
				if(pFile[nPos] == '\n')
					break;
				nPos++;
			}
			if(nPos >= len || pFile[nPos] == '\n')
				break;

			// Extract the element
			size_t i, l;
			if(separator == '\0')
			{
				for(l = 0; pFile[nPos + l] > ' '; l++)
				{
				}
				for(i = l; pFile[nPos + i] <= ' ' && pFile[nPos + i] != '\n' && pFile[nPos + i] != '\0'; i++)
				{
				}
			}
			else
			{
				for(i = 0; pFile[nPos + i] != separator && pFile[nPos + i] != '\n' && pFile[nPos + i] != '\0'; i++)
				{
				}
				for(l = i; l > 0 && pFile[nPos + l - 1] <= ' '; l--)
				{
				}
			}
			char* el = heap.add(pFile + nPos, l);
			row.m_elements.push_back(el);
			if(row.m_elements.size() > elementCount)
				break;
			nPos += i;
			if(separator != '\0' && pFile[nPos] == separator)
				nPos++;
		}
		if(tolerant)
		{
			while(row.m_elements.size() < elementCount)
				row.m_elements.push_back("");
		}
		else
		{
			if(row.m_elements.size() != (size_t)elementCount)
				ThrowError("Line ", to_str(nLine), " has a different number of elements than line ", to_str(nFirstDataLine));
		}

		// Move to next line
		for(; nPos < len && pFile[nPos] != '\n'; nPos++)
		{
		}
		continue;
	}

	// Parse it all
	GArffRelation* pRelation = new GArffRelation();
	sp_relation pRel;
	pRel = pRelation;
	GMatrix* pData = new GMatrix(pRel);
	Holder<GMatrix> hData(pData);
	size_t rowCount = rows.size();
	if(columnNamesInFirstRow)
		rowCount--;
	pData->reserve(rowCount);
	for(size_t i = 0; i < rowCount; i++)
		pData->m_rows.push_back(new double[elementCount]);
	for(size_t attr = 0; attr < elementCount; attr++)
	{
		// Determine if the attribute can be real
		bool real = true;
		for(size_t pat = columnNamesInFirstRow ? 1 : 0; pat < rows.size(); pat++)
		{
			const char* el = rows[pat].m_elements[attr];
			if(el[0] == '\0')
				continue; // unknown value
			if(strcmp(el, "?") == 0)
				continue; // unknown value
			if(GBits::isValidFloat(el, strlen(el)))
				continue;
			real = false;
			break;
		}

		// Make the attribute
		if(real)
		{
			if(columnNamesInFirstRow)
				pRelation->addAttribute(rows[0].m_elements[attr], 0, NULL);
			else
			{
				string attrName = "attr";
				attrName += to_str(attr);
				pRelation->addAttribute(attrName.c_str(), 0, NULL);
			}
			size_t i = 0;
			for(size_t pat = columnNamesInFirstRow ? 1 : 0; pat < rows.size(); pat++)
			{
				const char* el = rows[pat].m_elements[attr];
				double val;
				if(el[0] == '\0')
					val = UNKNOWN_REAL_VALUE;
				else if(strcmp(el, "?") == 0)
					val = UNKNOWN_REAL_VALUE;
				else
					val = atof(el);
				pData->row(i)[attr] = val;
				i++;
			}
		}
		else
		{
			// Make the data
			vector<const char*> values;
			GConstStringHashTable ht(31, true);
			void* pVal;
			uintptr_t n;
			size_t i = 0;
			size_t valueCount = 0;
			for(size_t pat = columnNamesInFirstRow ? 1 : 0; pat < rows.size(); pat++)
			{
				const char* el = rows[pat].m_elements[attr];
				if(el[0] == '\0')
					pData->row(i)[attr] = UNKNOWN_DISCRETE_VALUE;
				else if(strcmp(el, "?") == 0)
					pData->row(i)[attr] = UNKNOWN_DISCRETE_VALUE;
				else
				{
					if(ht.get(el, &pVal))
						n = (uintptr_t)pVal;
					else
					{
						values.push_back(el);
						n = valueCount++;
						ht.add(el, (const void*)n);
					}
					pData->row(i)[attr] = (double)n;
				}
				i++;
			}

			// Make the attribute
			if(columnNamesInFirstRow)
				pRelation->addAttribute(rows[0].m_elements[attr], valueCount, &values);
			else
			{
				string attrName = "attr";
				attrName += to_str(attr);
				pRelation->addAttribute(attrName.c_str(), valueCount, &values);
			}
		}
	}
	return hData.release();
}

GDomNode* GMatrix::serialize(GDom* pDoc)
{
	GDomNode* pData = pDoc->newObj();
	size_t attrCount = m_pRelation->size();
	pData->addField(pDoc, "rel", m_pRelation->serialize(pDoc));
	GDomNode* pPats = pData->addField(pDoc, "pats", pDoc->newList());
	GDomNode* pRow;
	double* pPat;
	for(size_t i = 0; i < rows(); i++)
	{
		pPat = row(i);
		pRow = pPats->addItem(pDoc, pDoc->newList());
		for(size_t j = 0; j < attrCount; j++)
			pRow->addItem(pDoc, pDoc->newDouble(pPat[j]));
	}
	return pData;
}

void GMatrix::col(size_t index, double* pOutVector)
{
	for(size_t i = 0; i < rows(); i++)
		*(pOutVector++) = row(i)[index];
}

void GMatrix::setCol(size_t index, const double* pVector)
{
	for(size_t i = 0; i < rows(); i++)
		row(i)[index] = *(pVector++);
}

void GMatrix::add(GMatrix* pThat, bool transpose)
{
	if(transpose)
	{
		size_t c = (size_t)cols();
		if(rows() != (size_t)pThat->cols() || c != pThat->rows())
			ThrowError("expected matrices of same size");
		for(size_t i = 0; i < rows(); i++)
		{
			double* pRow = row(i);
			for(size_t j = 0; j < c; j++)
				*(pRow++) += pThat->row(j)[i];
		}
	}
	else
	{
		size_t c = cols();
		if(rows() != pThat->rows() || c != pThat->cols())
			ThrowError("expected matrices of same size");
		for(size_t i = 0; i < rows(); i++)
			GVec::add(row(i), pThat->row(i), c);
	}
}

void GMatrix::subtract(GMatrix* pThat, bool transpose)
{
	if(transpose)
	{
		size_t c = (size_t)cols();
		if(rows() != (size_t)pThat->cols() || c != pThat->rows())
			ThrowError("expected matrices of same size");
		for(size_t i = 0; i < rows(); i++)
		{
			double* pRow = row(i);
			for(size_t j = 0; j < c; j++)
				*(pRow++) -= pThat->row(j)[i];
		}
	}
	else
	{
		size_t c = cols();
		if(rows() != pThat->rows() || c != pThat->cols())
			ThrowError("expected matrices of same size");
		for(size_t i = 0; i < rows(); i++)
			GVec::subtract(row(i), pThat->row(i), c);
	}
}

void GMatrix::multiply(double scalar)
{
	size_t c = cols();
	for(size_t i = 0; i < rows(); i++)
		GVec::multiply(row(i), scalar, c);
}

void GMatrix::multiply(const double* pVectorIn, double* pVectorOut, bool transpose)
{
	size_t rowCount = rows();
	size_t colCount = cols();
	if(transpose)
	{
		GVec::setAll(pVectorOut, 0.0, colCount);
		for(size_t i = 0; i < rowCount; i++)
			GVec::addScaled(pVectorOut, *(pVectorIn++), row(i), colCount);
	}
	else
	{
		for(size_t i = 0; i < rowCount; i++)
			*(pVectorOut++) = GVec::dotProduct(row(i), pVectorIn, colCount);
	}
}

// static
GMatrix* GMatrix::multiply(GMatrix& a, GMatrix& b, bool transposeA, bool transposeB)
{
	if(transposeA)
	{
		if(transposeB)
		{
			size_t dims = a.rows();
			if((size_t)b.cols() != dims)
				ThrowError("dimension mismatch");
			size_t w = b.rows();
			size_t h = a.cols();
			GMatrix* pOut = new GMatrix(h, w);
			for(size_t y = 0; y < h; y++)
			{
				double* pRow = pOut->row(y);
				for(size_t x = 0; x < w; x++)
				{
					double* pB = b[x];
					double sum = 0;
					for(size_t i = 0; i < dims; i++)
						sum += a[i][y] * pB[i];
					*(pRow++) = sum;
				}
			}
			return pOut;
		}
		else
		{
			size_t dims = a.rows();
			if(b.rows() != dims)
				ThrowError("dimension mismatch");
			size_t w = b.cols();
			size_t h = a.cols();
			GMatrix* pOut = new GMatrix(h, w);
			for(size_t y = 0; y < h; y++)
			{
				double* pRow = pOut->row(y);
				for(size_t x = 0; x < w; x++)
				{
					double sum = 0;
					for(size_t i = 0; i < dims; i++)
						sum += a[i][y] * b[i][x];
					*(pRow++) = sum;
				}
			}
			return pOut;
		}
	}
	else
	{
		if(transposeB)
		{
			size_t dims = (size_t)a.cols();
			if((size_t)b.cols() != dims)
				ThrowError("dimension mismatch");
			size_t w = b.rows();
			size_t h = a.rows();
			GMatrix* pOut = new GMatrix(h, w);
			for(size_t y = 0; y < h; y++)
			{
				double* pRow = pOut->row(y);
				double* pA = a[y];
				for(size_t x = 0; x < w; x++)
					*(pRow++) = GVec::dotProduct(pA, b[x], dims);
			}
			return pOut;
		}
		else
		{
			size_t dims = (size_t)a.cols();
			if(b.rows() != dims)
				ThrowError("dimension mismatch");
			size_t w = b.cols();
			size_t h = a.rows();
			GMatrix* pOut = new GMatrix(h, w);
			for(size_t y = 0; y < h; y++)
			{
				double* pRow = pOut->row(y);
				double* pA = a[y];
				for(size_t x = 0; x < w; x++)
				{
					double sum = 0;
					for(size_t i = 0; i < dims; i++)
						sum += pA[i] * b[i][x];
					*(pRow++) = sum;
				}
			}
			return pOut;
		}
	}
}

GMatrix* GMatrix::transpose()
{
	size_t r = rows();
	size_t c = (size_t)cols();
	GMatrix* pTarget = new GMatrix(c, r);
	for(size_t i = 0; i < c; i++)
	{
		double* pRow = pTarget->row(i);
		for(size_t j = 0; j < r; j++)
			*(pRow++) = row(j)[i];
	}
	return pTarget;
}

double GMatrix::trace()
{
	size_t min = std::min((size_t)cols(), rows());
	double sum = 0;
	for(size_t n = 0; n < min; n++)
		sum += row(n)[n];
	return sum;
}

size_t GMatrix::toReducedRowEchelonForm()
{
	size_t nLead = 0;
	double* pRow;
	size_t rowCount = rows();
	size_t colCount = cols();
	for(size_t nRow = 0; nRow < rowCount; nRow++)
	{
		// Find the next pivot (swapping rows as necessary)
		size_t i = nRow;
		while(std::abs(row(i)[nLead]) < 1e-9)
		{
			if(++i >= rowCount)
			{
				i = nRow;
				if(++nLead >= colCount)
					return nRow;
			}
		}
		if(i > nRow)
			swapRows(i, nRow);

		// Scale the pivot to 1
		pRow = row(nRow);
		GVec::multiply(pRow + nLead, 1.0 / pRow[nLead], colCount - nLead);

		// Elliminate all values above and below the pivot
		for(i = 0; i < rowCount; i++)
		{
			if(i != nRow)
				GVec::addScaled(row(i) + nLead, -row(i)[nLead], pRow + nLead, colCount - nLead);
		}

		nLead++;
	}
	return rowCount;
}

bool GMatrix::gaussianElimination(double* pVector)
{
	if(rows() != (size_t)cols())
		ThrowError("Expected a square matrix");
	double d;
	double* pRow;
	size_t rowCount = rows();
	size_t colCount = cols();
	for(size_t nRow = 0; nRow < rowCount; nRow++)
	{
		size_t i;
		for(i = nRow; i < rowCount && std::abs(row(i)[nRow]) < 1e-4; i++)
		{
		}
		if(i >= rowCount)
			continue;
		if(i > nRow)
		{
			swapRows(i, nRow);
			d = pVector[i];
			pVector[i] = pVector[nRow];
			pVector[nRow] = d;
		}

		// Scale the pivot to 1
		pRow = row(nRow);
		d = 1.0 / pRow[nRow];
		GVec::multiply(pRow + nRow, d, colCount - nRow);
		pVector[nRow] *= d;

		// Elliminate all values above and below the pivot
		for(i = 0; i < rowCount; i++)
		{
			if(i != nRow)
			{
				d = -row(i)[nRow];
				GVec::addScaled(row(i) + nRow, d, pRow + nRow, colCount - nRow);
				pVector[i] += d * pVector[nRow];
			}
		}
	}

	// Arbitrarily assign null-space values to 1
	for(size_t nRow = 0; nRow < rowCount; nRow++)
	{
		if(row(nRow)[nRow] < 0.5)
		{
			if(std::abs(pVector[nRow]) >= 1e-4)
				return false;
			for(size_t i = 0; i < rowCount; i++)
			{
				if(i == nRow)
				{
					pVector[nRow] = 1;
					row(nRow)[nRow] = 1;
				}
				else
				{
					pVector[i] -= row(i)[nRow];
					row(i)[nRow] = 0;
				}
			}
		}
	}
	return true;
}

GMatrix* GMatrix::cholesky()
{
	size_t rowCount = rows();
	size_t colCount = (size_t)cols();
	GMatrix* pOut = new GMatrix(m_pRelation);
	pOut->newRows(rowCount);
	double d;
	for(size_t j = 0; j < rowCount; j++)
	{
		size_t i;
		for(i = 0; i < j; i++)
		{
			d = 0;
			for(size_t k = 0; k < i; k++)
				d += (pOut->row(i)[k] * pOut->row(j)[k]);
			pOut->row(j)[i] = (1.0 / pOut->row(i)[i]) * (row(i)[j] - d);
		}
		d = 0;
		for(size_t k = 0; k < i; k++)
			d += (pOut->row(i)[k] * pOut->row(j)[k]);
		d = row(j)[i] - d;
		if(d < 0)
		{
			if(d > -1e-12)
				d = 0; // it's probably just rounding error
			else
				ThrowError("not positive definite");
		}
		pOut->row(j)[i] = sqrt(d);
		for(i++; i < colCount; i++)
			pOut->row(j)[i] = 0;
	}
	return pOut;
}

void GMatrix::LUDecomposition()
{
	size_t colCount = cols();
	double* pRow = row(0);
	for(size_t i = 1; i < colCount; i++)
		pRow[i] /= pRow[0];
	for(size_t i = 1; i < colCount; i++)
	{
		for(size_t j = i; j < colCount; j++)
		{ // do a column of L
			double sum = 0.0;
			for(size_t k = 0; k < i; k++)
				sum += row(j)[k] * row(k)[i];
			row(j)[i] -= sum;
		}
		if(i == colCount - 1)
			continue;
		for(size_t j = i + 1; j < colCount; j++)
		{ // do a row of U
			double sum = 0.0;
			for(size_t k = 0; k < i; k++)
				sum += row(i)[k] * row(k)[j];
			row(i)[j] = (row(i)[j] - sum) / row(i)[i];
		}
	}
}

/*
void GMatrix::invert()
{
	if(rows() != (size_t)cols())
		ThrowError("only square matrices supported");
	if(rows() == 1)
	{
		row(0)[0] = 1.0 / row(0)[0];
		return;
	}

	// Do LU decomposition (I think this is the Doolittle algorithm)
	int colCount = cols();
	double* pRow = row(0);
	for(int i = 1; i < colCount; i++)
		pRow[i] /= pRow[0];
	for(int i = 1; i < colCount; i++)
	{
		for(int j = i; j < colCount; j++)
		{ // do a column of L
			double sum = 0.0;
			for(int k = 0; k < i; k++)
				sum += row(j)[k] * row(k)[i];
			row(j)[i] -= sum;
		}
		if(i == colCount - 1)
			continue;
		for(int j = i + 1; j < colCount; j++)
		{ // do a row of U
			double sum = 0.0;
			for(int k = 0; k < i; k++)
				sum += row(i)[k] * row(k)[j];
			row(i)[j] = (row(i)[j] - sum) / row(i)[i];
		}
	}

	// Invert L
	for(int i = 0; i < colCount; i++)
	{
		for(int j = i; j < colCount; j++ )
		{
			double x = 1.0;
			if ( i != j )
			{
				x = 0.0;
				for(int k = i; k < j; k++ )
					x -= row(j)[k] * row(k)[i];
			}
			row(j)[i] = x / row(j)[j];
		}
	}

	// Invert U
	for(int i = 0; i < colCount; i++)
	{
		for(int j = i; j < colCount; j++ )
		{
			if( i == j )
				continue;
			double sum = 0.0;
			for (int k = i; k < j; k++ )
				sum += row(k)[j] * ((i == k) ? 1.0 : row(i)[k]);
			row(i)[j] = -sum;
		}
	}

	// A^-1 = U^-1 x L^-1
	for(int i = 0; i < colCount; i++ )
	{
		for(int j = 0; j < colCount; j++ )
		{
			double sum = 0.0;
			for(int k = ((i > j) ? i : j); k < colCount; k++)
				sum += ((j == k) ? 1.0 : row(j)[k]) * row(k)[i];
			row(j)[i] = sum;
		}
	}
}
*/
void GMatrix::inPlaceSquareTranspose()
{
	size_t size = rows();
	if(size != (size_t)cols())
		ThrowError("Expected a square matrix");
	for(size_t a = 0; a < size; a++)
	{
		for(size_t b = a + 1; b < size; b++)
			std::swap(row(a)[b], row(b)[a]);
	}
}

double GMatrix_pythag(double a, double b)
{
	double at = std::abs(a);
	double bt = std::abs(b);
	if(at > bt)
	{
		double ct = bt / at;
		return at * sqrt(1.0 + ct * ct);
	}
	else if(bt > 0.0)
	{
		double ct = at / bt;
		return bt * sqrt(1.0 + ct * ct);
	}
	else
		return 0.0;
}

double GMatrix_takeSign(double a, double b)
{
	return (b >= 0.0 ? std::abs(a) : -std::abs(a));
}

void GMatrix::singularValueDecomposition(GMatrix** ppU, double** ppDiag, GMatrix** ppV, bool throwIfNoConverge, size_t maxIters)
{
	if(rows() >= (size_t)cols())
		singularValueDecompositionHelper(ppU, ppDiag, ppV, throwIfNoConverge, maxIters);
	else
	{
		GMatrix* pTemp = transpose();
		Holder<GMatrix> hTemp(pTemp);
		pTemp->singularValueDecompositionHelper(ppV, ppDiag, ppU, throwIfNoConverge, maxIters);
		(*ppV)->inPlaceSquareTranspose();
		(*ppU)->inPlaceSquareTranspose();
	}
}

double GMatrix_safeDivide(double n, double d)
{
	if(d == 0.0 && n == 0.0)
		return 0.0;
	else
	{
		double t = n / d;
		//GAssert(t > -1e200, "prob");
		return t;
	}
}

void GMatrix::fixNans()
{
	size_t colCount = cols();
	for(size_t i = 0; i < rows(); i++)
	{
		double* pRow = row(i);
		for(size_t j = 0; j < colCount; j++)
		{
			if(*pRow >= -1e308 && *pRow < 1e308)
			{
			}
			else
				*pRow = (i == (size_t)j ? 1.0 : 0.0);
			pRow++;
		}
	}
}

void GMatrix::singularValueDecompositionHelper(GMatrix** ppU, double** ppDiag, GMatrix** ppV, bool throwIfNoConverge, size_t maxIters)
{
	int m = (int)rows();
	int n = (int)cols();
	if(m < n)
		ThrowError("Expected at least as many rows as columns");
	int i, j, k;
	int l = 0;
	int p, q;
	double c, f, h, s, x, y, z;
	double norm = 0.0;
	double g = 0.0;
	double scale = 0.0;
	GMatrix* pU = new GMatrix(m, m);
	Holder<GMatrix> hU(pU);
	pU->setAll(0.0);
	pU->copyColumns(0, this, 0, n);
	double* pSigma = new double[n];
	ArrayHolder<double> hSigma(pSigma);
	GMatrix* pV = new GMatrix(n, n);
	Holder<GMatrix> hV(pV);
	pV->setAll(0.0);
	GTEMPBUF(double, temp, n);

	// Householder reduction to bidiagonal form
	for(int i = 0; i < n; i++)
	{
		// Left-hand reduction
		temp[i] = scale * g;
		l = i + 1;
		g = 0.0;
		s = 0.0;
		scale = 0.0;
		if(i < m)
		{
			for(k = i; k < m; k++)
				scale += std::abs(pU->row(k)[i]);
			if(scale != 0.0)
			{
				for(k = i; k < m; k++)
				{
					pU->row(k)[i] = GMatrix_safeDivide(pU->row(k)[i], scale);
					double t = pU->row(k)[i];
					s += t * t;
				}
				f = pU->row(i)[i];
				g = -GMatrix_takeSign(sqrt(s), f);
				h = f * g - s;
				pU->row(i)[i] = f - g;
				if(i != n - 1)
				{
					for(j = l; j < n; j++)
					{
						s = 0.0;
						for(k = i; k < m; k++)
							s += pU->row(k)[i] * pU->row(k)[j];
						f = GMatrix_safeDivide(s, h);
						for(k = i; k < m; k++)
							pU->row(k)[j] += f * pU->row(k)[i];
					}
				}
				for(k = i; k < m; k++)
					pU->row(k)[i] *= scale;
			}
		}
		pSigma[i] = scale * g;

		// Right-hand reduction
		g = 0.0;
		s = 0.0;
		scale = 0.0;
		if(i < m && i != n - 1)
		{
			for(k = l; k < n; k++)
				scale += std::abs(pU->row(i)[k]);
			if(scale != 0.0)
			{
				for(k = l; k < n; k++)
				{
					pU->row(i)[k] = GMatrix_safeDivide(pU->row(i)[k], scale);
					double t = pU->row(i)[k];
					s += t * t;
				}
				f = pU->row(i)[l];
				g = -GMatrix_takeSign(sqrt(s), f);
				h = f * g - s;
				pU->row(i)[l] = f - g;
				for(k = l; k < n; k++)
					temp[k] = GMatrix_safeDivide(pU->row(i)[k], h);
				if(i != m - 1)
				{
					for(j = l; j < m; j++)
					{
						s = 0.0;
						for(k = l; k < n; k++)
							s += pU->row(j)[k] * pU->row(i)[k];
						for(k = l; k < n; k++)
							pU->row(j)[k] += s * temp[k];
					}
				}
				for(k = l; k < n; k++)
					pU->row(i)[k] *= scale;
			}
		}
		norm = std::max(norm, std::abs(pSigma[i]) + std::abs(temp[i]));
	}

	// Accumulate right-hand transform
	for(int i = n - 1; i >= 0; i--)
	{
		if(i < n - 1)
		{
			if(g != 0.0)
			{
				for(j = l; j < n; j++)
					pV->row(i)[j] = GMatrix_safeDivide(GMatrix_safeDivide(pU->row(i)[j], pU->row(i)[l]), g); // (double-division to avoid underflow)
				for(j = l; j < n; j++)
				{
					s = 0.0;
					for(k = l; k < n; k++)
						s += pU->row(i)[k] * pV->row(j)[k];
					for(k = l; k < n; k++)
						pV->row(j)[k] += s * pV->row(i)[k];
				}
			}
			for(j = l; j < n; j++)
			{
				pV->row(i)[j] = 0.0;
				pV->row(j)[i] = 0.0;
			}
		}
		pV->row(i)[i] = 1.0;
		g = temp[i];
		l = i;
	}

	// Accumulate left-hand transform
	for(i = n - 1; i >= 0; i--)
	{
		l = i + 1;
		g = pSigma[i];
		if(i < n - 1)
		{
			for(j = l; j < n; j++)
				pU->row(i)[j] = 0.0;
		}
		if(g != 0.0)
		{
			g = GMatrix_safeDivide(1.0, g);
			if(i != n - 1)
			{
				for(j = l; j < n; j++)
				{
					s = 0.0;
					for(k = l; k < m; k++)
						s += pU->row(k)[i] * pU->row(k)[j];
					f = GMatrix_safeDivide(s, pU->row(i)[i]) * g;
					for(k = i; k < m; k++)
						pU->row(k)[j] += f * pU->row(k)[i];
				}
			}
			for(j = i; j < m; j++)
				pU->row(j)[i] *= g;
		}
		else
		{
			for(j = i; j < m; j++)
				pU->row(j)[i] = 0.0;
		}
		pU->row(i)[i] += 1.0;
	}

	// Diagonalize the bidiagonal matrix
	for(k = n - 1; k >= 0; k--) // For each singular value
	{
		for(size_t iter = 1; iter <= maxIters; iter++)
		{
			// Test for splitting
			bool flag = true;
			for(l = k; l >= 0; l--)
			{
				q = l - 1;
				if(std::abs(temp[l]) + norm == norm)
				{
					flag = false;
					break;
				}
				if(std::abs(pSigma[q]) + norm == norm)
					break;
			}

			if(flag)
			{
				c = 0.0;
				s = 1.0;
				for(i = l; i <= k; i++)
				{
					f = s * temp[i];
					temp[i] *= c;
					if(std::abs(f) + norm == norm)
						break;
					g = pSigma[i];
					h = GMatrix_pythag(f, g);
					pSigma[i] = h;
					h = GMatrix_safeDivide(1.0, h);
					c = g * h;
					s = -f * h;
					for(j = 0; j < m; j++)
					{
						y = pU->row(j)[q];
						z = pU->row(j)[i];
						pU->row(j)[q] = y * c + z * s;
						pU->row(j)[i] = z * c - y * s;
					}
				}
			}

			z = pSigma[k];
			if(l == k)
			{
				// Detect convergence
				if(z < 0.0)
				{
					// Singular value should be positive
					pSigma[k] = -z;
					for(j = 0; j < n; j++)
						pV->row(k)[j] *= -1.0;
				}
				break;
			}
			if(throwIfNoConverge && iter >= maxIters)
				ThrowError("failed to converge");

			// Shift from bottom 2x2 minor
			x = pSigma[l];
			q = k - 1;
			y = pSigma[q];
			g = temp[q];
			h = temp[k];
			f = GMatrix_safeDivide(((y - z) * (y + z) + (g - h) * (g + h)), (2.0 * h * y));
			g = GMatrix_pythag(f, 1.0);
			f = GMatrix_safeDivide(((x - z) * (x + z) + h * (GMatrix_safeDivide(y, (f + GMatrix_takeSign(g, f))) - h)), x);

			// QR transform
			c = 1.0;
			s = 1.0;
			for(j = l; j <= q; j++)
			{
				i = j + 1;
				g = temp[i];
				y = pSigma[i];
				h = s * g;
				g = c * g;
				z = GMatrix_pythag(f, h);
				temp[j] = z;
				c = GMatrix_safeDivide(f, z);
				s = GMatrix_safeDivide(h, z);
				f = x * c + g * s;
				g = g * c - x * s;
				h = y * s;
				y = y * c;
				for(p = 0; p < n; p++)
				{
					x = pV->row(j)[p];
					z = pV->row(i)[p];
					pV->row(j)[p] = x * c + z * s;
					pV->row(i)[p] = z * c - x * s;
				}
				z = GMatrix_pythag(f, h);
				pSigma[j] = z;
				if(z != 0.0)
				{
					z = GMatrix_safeDivide(1.0, z);
					c = f * z;
					s = h * z;
				}
				f = c * g + s * y;
				x = c * y - s * g;
				for(p = 0; p < m; p++)
				{
					y = pU->row(p)[j];
					z = pU->row(p)[i];
					pU->row(p)[j] = y * c + z * s;
					pU->row(p)[i] = z * c - y * s;
				}
			}
			temp[l] = 0.0;
			temp[k] = f;
			pSigma[k] = x;
		}
	}

	// Sort the singular values from largest to smallest
	for(i = 1; i < n; i++)
	{
		for(j = i; j > 0; j--)
		{
			if(pSigma[j - 1] >= pSigma[j])
				break;
			pU->swapColumns(j - 1, j);
			pV->swapRows(j - 1, j);
			std::swap(pSigma[j - 1], pSigma[j]);
		}
	}

	// Return results
	pU->fixNans();
	pV->fixNans();
	*ppU = hU.release();
	*ppDiag = hSigma.release();
	*ppV = hV.release();
}

GMatrix* GMatrix::pseudoInverse()
{
	GMatrix* pU;
	double* pDiag;
	GMatrix* pV;
	size_t colCount = cols();
	size_t rowCount = rows();
	if(rowCount < (size_t)colCount)
	{
		GMatrix* pTranspose = transpose();
		Holder<GMatrix> hTranspose(pTranspose);
		pTranspose->singularValueDecompositionHelper(&pU, &pDiag, &pV, false, 80);
	}
	else
		singularValueDecompositionHelper(&pU, &pDiag, &pV, false, 80);
	Holder<GMatrix> hU(pU);
	ArrayHolder<double> hDiag(pDiag);
	Holder<GMatrix> hV(pV);
	GMatrix sigma(rowCount < (size_t)colCount ? colCount : rowCount, rowCount < (size_t)colCount ? rowCount : colCount);
	sigma.setAll(0.0);
	size_t m = std::min(rowCount, colCount);
	for(size_t i = 0; i < m; i++)
	{
		if(std::abs(pDiag[i]) > 1e-9)
			sigma[i][i] = GMatrix_safeDivide(1.0, pDiag[i]);
		else
			sigma[i][i] = 0.0;
	}
	GMatrix* pT = GMatrix::multiply(*pU, sigma, false, false);
	Holder<GMatrix> hT(pT);
	if(rowCount < (size_t)colCount)
		return GMatrix::multiply(*pT, *pV, false, false);
	else
		return GMatrix::multiply(*pV, *pT, true, true);
}

// static
GMatrix* GMatrix::kabsch(GMatrix* pA, GMatrix* pB)
{
	GMatrix* pCovariance = GMatrix::multiply(*pA, *pB, true, false);
	Holder<GMatrix> hCov(pCovariance);
	GMatrix* pU;
	double* pDiag;
	GMatrix* pV;
	pCovariance->singularValueDecomposition(&pU, &pDiag, &pV);
	Holder<GMatrix> hU(pU);
	delete[] pDiag;
	Holder<GMatrix> hV(pV);
	GMatrix* pK = GMatrix::multiply(*pV, *pU, true, true);
	return pK;
}

// static
GMatrix* GMatrix::align(GMatrix* pA, GMatrix* pB)
{
	size_t columns = pA->cols();
	GTEMPBUF(double, mean, columns);
	pA->centroid(mean);
	GMatrix* pAA = pA->clone();
	Holder<GMatrix> hAA(pAA);
	pAA->centerMeanAtOrigin();
	GMatrix* pBB = pB->clone();
	Holder<GMatrix> hBB(pBB);
	pBB->centerMeanAtOrigin();
	GMatrix* pK = GMatrix::kabsch(pBB, pAA);
	Holder<GMatrix> hK(pK);
	hAA.reset(NULL);
	GMatrix* pAligned = GMatrix::multiply(*pBB, *pK, false, true);
	Holder<GMatrix> hAligned(pAligned);
	hBB.reset(NULL);
	for(vector<double*>::iterator it = pAligned->m_rows.begin(); it != pAligned->m_rows.end(); it++)
		GVec::add(*it, mean, columns);
	return hAligned.release();
}

double GMatrix::determinant()
{
	// Check size
	size_t n = rows();
	if(n != cols())
		ThrowError("Only square matrices are supported");

	// Convert to a triangular matrix
	double epsilon = 1e-10;
	GMatrix* pC = this->clone();
	Holder<GMatrix> hC(pC);
	GMatrix& C = *pC;
	GTEMPBUF(size_t, Kp, 2 * n);
	size_t* Lp = Kp + n;
	size_t l, ko, lo;
	double po,t0;
	bool nonSingular = true;
	size_t k = 0;
	while(nonSingular && k < n)
	{
		po = C[k][k];
		lo = k;
		ko = k;
		for(size_t i = k; i < n; i++)
			for(size_t j = k; j < n; j++)
				if(std::abs(C[i][j]) > std::abs(po))
				{
					po = C[i][j];
					lo = i;
					ko = j;
				}
		Lp[k] = lo;
		Kp[k] = ko;
		if(std::abs(po) < epsilon)
		{
			nonSingular = false;
			//ThrowError("Failed to compute determinant. Pivot too small.");
		}
		else
		{
			if(lo != k)
			{
				for(size_t j = k; j < n; j++)
				{
					t0 = C[k][j];
					C[k][j] = C[lo][j];
					C[lo][j] = t0;
				}
			}
			if(ko != k)
			{
				for(size_t i = 0; i < n; i++)
				{
					t0 = C[i][k];
					C[i][k] = C[i][ko];
					C[i][ko] = t0;
				}
			}
			for(size_t i = k + 1; i < n; i++)
			{
				C[i][k] /= po;
				for(size_t j = k + 1; j < n; j++)
					C[i][j] -= C[i][k] * C[k][j];
			}
			k++;
		}
	}
	if(nonSingular && std::abs(C[n - 1][n - 1]) < epsilon)
		nonSingular = false;

	// Compute determinant
	if(!nonSingular)
		return 0.0;
	else
	{
		double det = 1.0;
		for(k = 0; k < n; k++)
			det *= C[k][k];
		l = 0;
		for(k = 0; k < n - 1; k++)
		{
			if(Lp[k] != k)
				l++;
			if(Kp[k] != k)
				l++;
		}
		if((l % 2) != 0)
			det = -det;
		return det;
	}
}

void GMatrix::makeIdentity()
{
	size_t rowCount = rows();
	size_t colCount = cols();
	for(size_t nRow = 0; nRow < rowCount; nRow++)
		GVec::setAll(row(nRow), 0.0, colCount);
	size_t nMin = std::min((size_t)colCount, rowCount);
	for(size_t i = 0; i < nMin; i++)
		row(i)[i] = 1.0;
}

void GMatrix::mirrorTriangle(bool upperToLower)
{
	size_t n = std::min(rows(), (size_t)cols());
	if(upperToLower)
	{
		for(size_t i = 0; i < n; i++)
		{
			for(size_t j = i + 1; j < n; j++)
				row(j)[i] = row(i)[j];
		}
	}
	else
	{
		for(size_t i = 0; i < n; i++)
		{
			for(size_t j = i + 1; j < n; j++)
				row(i)[j] = row(j)[i];
		}
	}
}

double GMatrix::eigenValue(const double* pEigenVector)
{
	// Find the element with the largest magnitude
	size_t nEl = 0;
	size_t colCount = cols();
	for(size_t i = 1; i < colCount; i++)
	{
		if(std::abs(pEigenVector[i]) > std::abs(pEigenVector[nEl]))
			nEl = i;
	}
	return GVec::dotProduct(row(nEl), pEigenVector, colCount) / pEigenVector[nEl];
}

void GMatrix::eigenVector(double eigenvalue, double* pOutVector)
{
	GAssert(rows() == (size_t)cols()); // Expected a square matrix
	size_t rowCount = rows();
	for(size_t i = 0; i < rowCount; i++)
		row(i)[i] = row(i)[i] - eigenvalue;
	GVec::setAll(pOutVector, 0.0, rowCount);
	if(!gaussianElimination(pOutVector))
		ThrowError("no solution");
	GVec::normalize(pOutVector, rowCount);
}

GMatrix* GMatrix::eigs(size_t nCount, double* pEigenVals, GRand* pRand, bool mostSignificant)
{
	size_t dims = cols();
	if(nCount > dims)
		ThrowError("Can't have more eigenvectors than columns");
	if(rows() != (size_t)dims)
		ThrowError("expected a square matrix");

/*
	// The principle components of the Cholesky (square-root) matrix are the same as
	// the eigenvectors of this matrix.
	GMatrix* pDeviation = cholesky();
	Holder<GMatrix> hDeviation(pDeviation);
	GMatrix* pData = pDeviation->transpose();
	Holder<GMatrix> hData(pData);
	size_t s = pData->rows();
	for(size_t i = 0; i < s; i++)
	{
		double* pRow = pData->newRow();
		GVec::copy(pRow, pData->row(i), dims);
		GVec::multiply(pRow, -1, dims);
	}

	// Extract the principle components
	GMatrix* pOut = new GMatrix(m_pRelation);
	pOut->newRows(nCount);
	for(size_t i = 0; i < nCount; i++)
	{
		pData->principalComponentAboutOrigin(pOut->row(i), dims, pRand);
		pData->removeComponentAboutOrigin(pOut->row(i), dims);
	}
*/

	// Use the power method to compute the first few eigenvectors. todo: we really should use the Lanczos method instead
	GMatrix* pOut = new GMatrix(m_pRelation);
	pOut->newRows(nCount);
	GMatrix* pA;
	if(mostSignificant)
		pA = clone();
	else
		pA = pseudoInverse();
	Holder<GMatrix> hA(pA);
	GTEMPBUF(double, pTemp, dims);
	for(size_t i = 0; i < nCount; i++)
	{
		// Use the power method to compute the next eigenvector
		double* pX = pOut->row(i);
		pRand->spherical(pX, dims);
		for(size_t j = 0; j < 100; j++) // todo: is there a better way to detect convergence?
		{
			pA->multiply(pX, pTemp);
			GVec::copy(pX, pTemp, dims);
			GVec::safeNormalize(pX, dims, pRand);
		}

		// Compute the corresponding eigenvalue
		double lambda = pA->eigenValue(pX);
		if(pEigenVals)
			pEigenVals[i] = lambda;

		// Deflate (subtract out the eigenvector)
		for(size_t j = 0; j < dims; j++)
		{
			double* pRow = pA->row(j);
			for(size_t k = 0; k < dims; k++)
			{
				*pRow = *pRow - lambda * pX[j] * pX[k];
				pRow++;
			}
		}
	}

	return pOut;
}
/*
GMatrix* GMatrix::leastSignificantEigenVectors(size_t nCount, GRand* pRand)
{
	GMatrix* pInv = clone();
	Holder<GMatrix> hInv(pInv);
	pInv->invert();
	GMatrix* pOut = pInv->mostSignificantEigenVectors(nCount, pRand);
	double eigenvalue;
	for(size_t i = 0; i < nCount; i++)
	{
		eigenvalue = 1.0 / pInv->eigenValue(pOut->row(i));
		GMatrix* cp = clone();
		Holder<GMatrix> hCp(cp);
		cp->eigenVector(eigenvalue, pOut->row(i));
	}
	return pOut;
}
*/
double* GMatrix::newRow()
{
	size_t nAttributes = m_pRelation->size();
	double* pNewRow;
	if(m_pHeap)
		pNewRow = (double*)m_pHeap->allocate(sizeof(double) * nAttributes);
	else
		pNewRow = new double[nAttributes];
	m_rows.push_back(pNewRow);
	return pNewRow;
}

void GMatrix::takeRow(double* pRow)
{
	m_rows.push_back(pRow);
}

void GMatrix::newRows(size_t nRows)
{
	reserve(m_rows.size() + nRows);
	for(size_t i = 0; i < nRows; i++)
		newRow();
}

void GMatrix::fromVector(const double* pVec, size_t nRows)
{
	if(rows() < nRows)
		newRows(nRows - rows());
	else
	{
		while(rows() > nRows)
			deleteRow(0);
	}
	size_t nCols = m_pRelation->size();
	for(size_t r = 0; r < nRows; r++)
	{
		double* pRow = row(r);
		GVec::copy(pRow, pVec, nCols);
		pVec += nCols;
	}
}

void GMatrix::toVector(double* pVec)
{
	size_t nCols = cols();
	for(size_t i = 0; i < rows(); i++)
	{
		GVec::copy(pVec, row(i), nCols);
		pVec += nCols;
	}
}

void GMatrix::setAll(double val)
{
	size_t colCount = cols();
	for(size_t i = 0; i < rows(); i++)
		GVec::setAll(row(i), val, colCount);
}

void GMatrix::copy(GMatrix* pThat)
{
	m_pRelation = pThat->m_pRelation;
	flush();
	newRows(pThat->rows());
	copyColumns(0, pThat, 0, m_pRelation->size());
}

GMatrix* GMatrix::clone()
{
	GMatrix* pOther = new GMatrix(relation());
	pOther->newRows(rows());
	pOther->copyColumns(0, this, 0, cols());
	return pOther;
}

GMatrix* GMatrix::cloneSub(size_t rowStart, size_t colStart, size_t rowCount, size_t colCount)
{
	if(rowStart + rowCount > rows())
		ThrowError("row index out of range");
	sp_relation pSubRel = (colCount == cols() ? m_pRelation : m_pRelation->cloneSub(colStart, colCount));
	GMatrix* pThat = new GMatrix(pSubRel);
	pThat->newRows(rowCount);
	for(size_t i = 0; i < rowCount; i++)
		GVec::copy(pThat->row(i), row(rowStart + i) + colStart, colCount);
	return pThat;
}

void GMatrix::copyRow(const double* pRow)
{
	double* pNewRow = newRow();
	GVec::copy(pNewRow, pRow, m_pRelation->size());
}

void GMatrix::copyColumns(size_t nDestStartColumn, GMatrix* pSource, size_t nSourceStartColumn, size_t nColumnCount)
{
	if(rows() != pSource->rows())
		ThrowError("expected datasets to have the same number of rows");
	size_t count = rows();
	for(size_t i = 0; i < count; i++)
		GVec::copy(row(i) + nDestStartColumn, pSource->row(i) + nSourceStartColumn, nColumnCount);
}

GMatrix* GMatrix::attrSubset(size_t firstAttr, size_t attrCount)
{
	if(firstAttr + attrCount > m_pRelation->size())
		ThrowError("index out of range");
	sp_relation relNew;
	if(relation()->type() == GRelation::UNIFORM)
	{
		GUniformRelation* pNewRelation = new GUniformRelation(attrCount, relation()->valueCount(firstAttr));
		relNew = pNewRelation;
	}
	else
	{
		GMixedRelation* pNewRelation = new GMixedRelation();
		pNewRelation->addAttrs(m_pRelation.get(), firstAttr, attrCount);
		relNew = pNewRelation;
	}
	GMatrix* pNewData = new GMatrix(relNew);
	pNewData->newRows(rows());
	pNewData->copyColumns(0, this, firstAttr, attrCount);
	return pNewData;
}

void GMatrix::swapRows(size_t a, size_t b)
{
	std::swap(m_rows[a], m_rows[b]);
}

void GMatrix::swapColumns(size_t nAttr1, size_t nAttr2)
{
	if(nAttr1 == nAttr2)
		return;
	m_pRelation = m_pRelation->clone();
	m_pRelation->swapAttributes(nAttr1, nAttr2);
	size_t nCount = rows();
	double tmp;
	double* pRow;
	for(size_t i = 0; i < nCount; i++)
	{
		pRow = row(i);
		tmp = pRow[nAttr1];
		pRow[nAttr1] = pRow[nAttr2];
		pRow[nAttr2] = tmp;
	}
}

void GMatrix::deleteColumn(size_t index)
{
	m_pRelation = m_pRelation->clone();
	m_pRelation->deleteAttribute(index);
	size_t nCount = rows();
	double* pRow;
	size_t nAttrCountBefore = m_pRelation->size();
	for(size_t i = 0; i < nCount; i++)
	{
		pRow = row(i);
		for(size_t j = index; j < nAttrCountBefore; j++)
			pRow[j] = pRow[j + 1];
	}
}

double* GMatrix::releaseRow(size_t index)
{
	size_t last = m_rows.size() - 1;
	double* pRow = m_rows[index];
	m_rows[index] = m_rows[last];
	m_rows.pop_back();
	return pRow;
}

void GMatrix::deleteRow(size_t index)
{
	double* pRow = releaseRow(index);
	if(!m_pHeap)
		delete[] pRow;
}

double* GMatrix::releaseRowPreserveOrder(size_t index)
{
	double* pRow = m_rows[index];
	m_rows.erase(m_rows.begin() + index);
	return pRow;
}

void GMatrix::deleteRowPreserveOrder(size_t index)
{
	double* pRow = releaseRowPreserveOrder(index);
	if(!m_pHeap)
		delete[] pRow;
}

void GMatrix::releaseAllRows()
{
	m_rows.clear();
}

// static
GMatrix* GMatrix::mergeHoriz(GMatrix* pSetA, GMatrix* pSetB)
{
	if(pSetA->rows() != pSetB->rows())
		ThrowError("Expected same number of rows");
	GArffRelation* pRel = new GArffRelation();
	sp_relation spRel;
	spRel = pRel;
	GRelation* pRelA = pSetA->relation().get();
	GRelation* pRelB = pSetB->relation().get();
	size_t nSetADims = pRelA->size();
	size_t nSetBDims = pRelB->size();
	pRel->addAttrs(pRelA);
	pRel->addAttrs(pRelB);
	GMatrix* pNewSet = new GMatrix(spRel);
	Holder<GMatrix> hNewSet(pNewSet);
	pNewSet->reserve(pSetA->rows());
	double* pNewRow;
	for(size_t i = 0; i < pSetA->rows(); i++)
	{
		pNewRow = pNewSet->newRow();
		GVec::copy(pNewRow, pSetA->row(i), nSetADims);
		GVec::copy(&pNewRow[nSetADims], pSetB->row(i), nSetBDims);
	}
	return hNewSet.release();
}

void GMatrix::shuffle(GRand& rand, GMatrix* pExtension)
{
	if(pExtension)
	{
		if(pExtension->rows() != rows())
			ThrowError("Expected pExtension to have the same number of rows");
		for(size_t n = m_rows.size(); n > 0; n--)
		{
			size_t r = (size_t)rand.next(n);
			std::swap(m_rows[r], m_rows[n - 1]);
			std::swap(pExtension->m_rows[r], pExtension->m_rows[n - 1]);
		}
	}
	else
	{
		for(size_t n = m_rows.size(); n > 0; n--)
			std::swap(m_rows[(size_t)rand.next(n)], m_rows[n - 1]);
	}
}

void GMatrix::shuffle2(GRand& rand, GMatrix& other)
{
	for(size_t n = m_rows.size(); n > 0; n--)
	{
		size_t r = (size_t)rand.next(n);
		std::swap(m_rows[r], m_rows[n - 1]);
		std::swap(other.m_rows[r], other.m_rows[n - 1]);
	}
}

void GMatrix::shuffleLikeCards()
{
	for(size_t i = 0; i < rows(); i++)
	{
		size_t n = i;
		while(n & 1)
			n = (n >> 1);
		n = (n >> 1) + rows() / 2;
		std::swap(m_rows[i], m_rows[n]);
	}
}

double GMatrix::entropy(size_t nColumn)
{
	// Count the number of occurrences of each value
	GAssert(m_pRelation->valueCount(nColumn) > 0); // continuous attributes are not supported
	size_t nPossibleValues = m_pRelation->valueCount(nColumn);
	GTEMPBUF(size_t, pnCounts, nPossibleValues);
	size_t nTotalCount = 0;
	memset(pnCounts, '\0', m_pRelation->valueCount(nColumn) * sizeof(size_t));
	size_t nRows = rows();
	for(size_t n = 0; n < nRows; n++)
	{
		int nValue = (int)row(n)[nColumn];
		if(nValue < 0)
		{
			GAssert(nValue == UNKNOWN_DISCRETE_VALUE);
			continue;
		}
		GAssert(nValue < (int)nPossibleValues);
		pnCounts[nValue]++;
		nTotalCount++;
	}
	if(nTotalCount == 0)
		return 0;

	// Total up the entropy
	double dEntropy = 0;
	double dRatio;
	for(size_t n = 0; n < nPossibleValues; n++)
	{
		if(pnCounts[n] > 0)
		{
			dRatio = (double)pnCounts[n] / nTotalCount;
			dEntropy -= dRatio * log(dRatio);
		}
	}
	return M_LOG2E * dEntropy;
}

void GMatrix::splitByPivot(GMatrix* pGreaterOrEqual, size_t nAttribute, double dPivot, GMatrix* pExtensionA, GMatrix* pExtensionB)
{
	if(pExtensionA && pExtensionA->rows() != rows())
		ThrowError("Expected pExtensionA to have the same number of rows as this dataset");
	GAssert(pGreaterOrEqual->m_pHeap == m_pHeap);
	size_t nUnknowns = 0;
	double* pRow;
	size_t n;
	for(n = rows() - 1; n >= nUnknowns && n < rows(); n--)
	{
		pRow = row(n);
		if(pRow[nAttribute] == UNKNOWN_REAL_VALUE)
		{
			std::swap(m_rows[nUnknowns], m_rows[n]);
			if(pExtensionA)
				std::swap(pExtensionA->m_rows[nUnknowns], pExtensionA->m_rows[n]);
			nUnknowns++;
			n++;
		}
		else if(pRow[nAttribute] >= dPivot)
		{
			pGreaterOrEqual->takeRow(releaseRow(n));
			if(pExtensionA)
				pExtensionB->takeRow(pExtensionA->releaseRow(n));
		}
	}

	// Send all the unknowns to the side with more rows
	if(pGreaterOrEqual->rows() > rows() - nUnknowns)
	{
		for(; n < rows(); n--)
		{
			pGreaterOrEqual->takeRow(releaseRow(n));
			if(pExtensionA)
				pExtensionB->takeRow(pExtensionA->releaseRow(n));
		}
	}
}

void GMatrix::splitByNominalValue(GMatrix* pSingleClass, size_t nAttr, int nValue, GMatrix* pExtensionA, GMatrix* pExtensionB)
{
	GAssert(pSingleClass->m_pHeap == m_pHeap);
	for(size_t i = rows() - 1; i < rows(); i--)
	{
		double* pVec = row(i);
		if((int)pVec[nAttr] == nValue)
		{
			pSingleClass->takeRow(releaseRow(i));
			if(pExtensionA)
				pExtensionB->takeRow(pExtensionA->releaseRow(i));
		}
	}
}

void GMatrix::splitBySize(GMatrix* pOtherData, size_t nOtherRows)
{
	GAssert(pOtherData->m_pHeap == m_pHeap);
	if(nOtherRows > rows())
		ThrowError("row count out of range");
	size_t targetSize = pOtherData->rows() + nOtherRows;
	pOtherData->reserve(targetSize);
	while(pOtherData->rows() < targetSize)
		pOtherData->takeRow(releaseRow(rows() - 1));
}

void GMatrix::mergeVert(GMatrix* pData)
{
	if(relation()->type() == GRelation::ARFF && pData->relation()->type() == GRelation::ARFF && relation().get() != pData->relation().get())
	{
		// Make an value mapping for pData
		GArffRelation* pThis = (GArffRelation*)relation().get();
		GArffRelation* pThat = (GArffRelation*)pData->relation().get();
		if(pThis->size() != pThat->size())
			ThrowError("Mismatching number of columns");
		vector< vector<size_t> > valueMap;
		valueMap.resize(pThis->size());
		for(size_t i = 0; i < pThis->size(); i++)
		{
			if(strcmp(pThis->attrName(i), pThat->attrName(i)) != 0)
				ThrowError("The name of attribute ", to_str(i), " does not match");
			if(pThis->valueCount(i) == 0 && pThat->valueCount(i) != 0)
				ThrowError("Attribute ", to_str(i), " is continuous in one matrix and nominal in the other");
			if(pThis->valueCount(i) != 0 && pThat->valueCount(i) == 0)
				ThrowError("Attribute ", to_str(i), " is continuous in one matrix and nominal in the other");
			vector<size_t>& vm = valueMap[i];
			GArffAttribute& attrThis = pThis->m_attrs[i];
			GArffAttribute& attrThat = pThat->m_attrs[i];
			for(size_t j = 0; j < pThat->valueCount(i); j++)
			{
				if(attrThis.m_values.size() >= pThis->valueCount(i) && attrThat.m_values.size() >= j && attrThat.m_values[j].length() > 0)
				{
					int newVal = pThis->findEnumeratedValue(i, attrThat.m_values[j].c_str());
					if(newVal == UNKNOWN_DISCRETE_VALUE)
						newVal = pThis->addAttrValue(i, attrThat.m_values[j].c_str());
					vm.push_back(newVal);
				}
				else
					vm.push_back(j);
			}
		}

		// Merge the data and map the values in pData to match those in this Matrix with the same name
		for(size_t j = 0; j < pData->rows(); j++)
		{
			double* pRow = pData->row(j);
			takeRow(pRow);
			for(size_t i = 0; i < pThis->size(); i++)
			{
				if(pThis->valueCount(i) != 0 && *pRow != UNKNOWN_DISCRETE_VALUE)
				{
					vector<size_t>& vm = valueMap[i];
					int oldVal = (int)*pRow;
					GAssert(oldVal >= 0 && (size_t)oldVal < vm.size());
					*pRow = (double)vm[oldVal];
				}
				pRow++;
			}
		}
		pData->releaseAllRows();
	}
	else
	{
		if(!relation()->isCompatible(*pData->relation().get()))
			ThrowError("The two matrices have incompatible relations");
		for(size_t i = 0; i < pData->rows(); i++)
			takeRow(pData->row(i));
		pData->releaseAllRows();
	}
}

double GMatrix::mean(size_t nAttribute)
{
	if(nAttribute >= cols() || nAttribute <  0)
		ThrowError("attribute index out of range");
	double sum = 0;
	size_t missing = 0;
	for(vector<double*>::iterator it = m_rows.begin(); it != m_rows.end(); it++)
	{
		if((*it)[nAttribute] == UNKNOWN_REAL_VALUE)
			missing++;
		else
			sum += (*it)[nAttribute];
	}
	size_t count = m_rows.size() - missing;
	if(count > 0)
		return sum / count;
	else
	{
		ThrowError("at least one value is required to compute a mean");
		return 0.0;
	}
}

double GMatrix::median(size_t nAttribute)
{
	if(nAttribute >= cols() || nAttribute <  0)
		ThrowError("attribute index out of range");
	vector<double> vals;
	vals.reserve(rows());
	for(vector<double*>::iterator it = m_rows.begin(); it != m_rows.end(); it++)
	{
		double d = (*it)[nAttribute];
		if(d != UNKNOWN_REAL_VALUE)
			vals.push_back(d);
	}
	if(vals.size() < 1)
		ThrowError("at least one value is required to compute a median");
	if(vals.size() & 1)
	{
		vector<double>::iterator med = vals.begin() + (vals.size() / 2);
		std::nth_element(vals.begin(), med, vals.end());
		return *med;
	}
	else
	{
		vector<double>::iterator a = vals.begin() + (vals.size() / 2 - 1);
		std::nth_element(vals.begin(), a, vals.end());
		vector<double>::iterator b = std::min_element(a + 1, vals.end());
		return 0.5 * (*a + *b);
	}
}

void GMatrix::centroid(double* pOutMeans)
{
	size_t c = cols();
	for(size_t n = 0; n < c; n++)
		pOutMeans[n] = mean(n);
}

double GMatrix::variance(size_t nAttr, double mean)
{
	double d;
	double dSum = 0;
	size_t nMissing = 0;
	for(vector<double*>::iterator it = m_rows.begin(); it != m_rows.end(); it++)
	{
		if((*it)[nAttr] == UNKNOWN_REAL_VALUE)
		{
			nMissing++;
			continue;
		}
		d = (*it)[nAttr] - mean;
		dSum += (d * d);
	}
	size_t nCount = m_rows.size() - nMissing;
	if(nCount > 1)
		return dSum / (nCount - 1);
	else
		return 0; // todo: wouldn't UNKNOWN_REAL_VALUE be better here?
}

void GMatrix::minAndRange(size_t nAttribute, double* pMin, double* pRange)
{
	double dMin = 1e300;
	double dMax = -1e300;
	for(vector<double*>::iterator it = m_rows.begin(); it != m_rows.end(); it++)
	{
		if((*it)[nAttribute] == UNKNOWN_REAL_VALUE)
			continue;
		if((*it)[nAttribute] < dMin)
			dMin = (*it)[nAttribute];
		if((*it)[nAttribute] > dMax)
			dMax = (*it)[nAttribute];
	}
	if(dMax >= dMin)
	{
		*pMin = dMin;
		*pRange = dMax - dMin;
	}
	else
	{
		*pMin = UNKNOWN_REAL_VALUE;
		*pRange = UNKNOWN_REAL_VALUE;
	}
}

void GMatrix::minAndRangeUnbiased(size_t nAttribute, double* pMin, double* pRange)
{
	double min, range, d;
	minAndRange(nAttribute, &min, &range);
	d = .5 * (range * (rows() + 1) / (rows() - 1) - range);
	*pMin = (min - d);
	*pRange = (range + d);
}

void GMatrix::normalize(size_t nAttribute, double dInputMin, double dInputRange, double dOutputMin, double dOutputRange)
{
	GAssert(dInputRange > 0);
	double dScale = dOutputRange / dInputRange;
	for(vector<double*>::iterator it = m_rows.begin(); it != m_rows.end(); it++)
	{
		(*it)[nAttribute] -= dInputMin;
		(*it)[nAttribute] *= dScale;
		(*it)[nAttribute] += dOutputMin;
	}
}

/*static*/ double GMatrix::normalize(double dVal, double dInputMin, double dInputRange, double dOutputMin, double dOutputRange)
{
	GAssert(dInputRange > 0);
	dVal -= dInputMin;
	dVal /= dInputRange;
	dVal *= dOutputRange;
	dVal += dOutputMin;
	return dVal;
}

double GMatrix::baselineValue(size_t nAttribute)
{
	if(m_pRelation->valueCount(nAttribute) == 0)
		return mean(nAttribute);
	int j;
	int val;
	int nValues = (int)m_pRelation->valueCount(nAttribute);
	GTEMPBUF(size_t, counts, nValues + 1);
	memset(counts, '\0', sizeof(size_t) * (nValues + 1));
	for(vector<double*>::iterator it = m_rows.begin(); it != m_rows.end(); it++)
	{
		val = (int)(*it)[nAttribute] + 1;
		counts[val]++;
	}
	val = 1;
	for(j = 2; j <= nValues; j++)
	{
		if(counts[j] > counts[val])
			val = j;
	}
	return (double)(val - 1);
}

bool GMatrix::isAttrHomogenous(size_t col)
{
	if(m_pRelation->valueCount(col) > 0)
	{
		int d;
		vector<double*>::iterator it = m_rows.begin();
		for( ; it != m_rows.end(); it++)
		{
			d = (int)(*it)[col];
			if(d != UNKNOWN_DISCRETE_VALUE)
			{
				it++;
				break;
			}
		}
		for( ; it != m_rows.end(); it++)
		{
			int t = (int)(*it)[col];
			if(t != d && t != UNKNOWN_DISCRETE_VALUE)
				return false;
		}
	}
	else
	{
		double d;
		vector<double*>::iterator it = m_rows.begin();
		for( ; it != m_rows.end(); it++)
		{
			d = (*it)[col];
			if(d != UNKNOWN_REAL_VALUE)
			{
				it++;
				break;
			}
		}
		for( ; it != m_rows.end(); it++)
		{
			double t = (*it)[col];
			if(t != d && t != UNKNOWN_REAL_VALUE)
				return false;
		}
	}
	return true;
}

bool GMatrix::isHomogenous()
{
	for(size_t i = 0; i < cols(); i++)
	{
		if(!isAttrHomogenous(i))
			return false;
	}
	return true;
}

void GMatrix::replaceMissingValuesWithBaseline(size_t nAttr)
{
	double bl = baselineValue(nAttr);
	size_t count = rows();
	if(m_pRelation->valueCount(nAttr) == 0)
	{
		for(size_t i = 0; i < count; i++)
		{
			if(row(i)[nAttr] == UNKNOWN_REAL_VALUE)
				row(i)[nAttr] = bl;
		}
	}
	else
	{
		for(size_t i = 0; i < count; i++)
		{
			if(row(i)[nAttr] == UNKNOWN_DISCRETE_VALUE)
				row(i)[nAttr] = bl;
		}
	}
}

void GMatrix::replaceMissingValuesRandomly(size_t nAttr, GRand* pRand)
{
	GTEMPBUF(size_t, indexes, rows());

	// Find the rows that are not missing values in this attribute
	size_t* pCur = indexes;
	double dOk = m_pRelation->valueCount(nAttr) == 0 ? -1e300 : 0;
	for(size_t i = 0; i < rows(); i++)
	{
		if(row(i)[nAttr] >= dOk)
		{
			*pCur = i;
			pCur++;
		}
	}

	// Replace missing values
	size_t nonMissing = pCur - indexes;
	for(size_t i = 0; i < rows(); i++)
	{
		if(row(i)[nAttr] < dOk)
			row(i)[nAttr] = row(indexes[(size_t)pRand->next(nonMissing)])[nAttr];
	}
}

void GMatrix::principalComponent(double* pOutVector, size_t dims, const double* pMean, GRand* pRand)
{
	// Initialize the out-vector to a random direction
	pRand->spherical(pOutVector, dims);

	// Iterate
	double* pVector;
	size_t nCount = rows();
	GTEMPBUF(double, pAccumulator, dims);
	double d;
	double mag = 0;
	for(size_t iters = 0; iters < 200; iters++)
	{
		GVec::setAll(pAccumulator, 0.0, dims);
		for(size_t n = 0; n < nCount; n++)
		{
			pVector = row(n);
			d = GVec::dotProduct(pMean, pVector, pOutVector, dims);
			double* pAcc = pAccumulator;
			const double* pM = pMean;
			for(size_t j = 0; j < dims; j++)
				*(pAcc++) += d * (*(pVector++) - *(pM++));
		}
		GVec::copy(pOutVector, pAccumulator, dims);
		GVec::safeNormalize(pOutVector, dims, pRand);
		d = GVec::squaredMagnitude(pAccumulator, dims);
		if(iters < 6 || d > mag)
			mag = d;
		else
			break;
	}
}

void GMatrix::principalComponentAboutOrigin(double* pOutVector, size_t dims, GRand* pRand)
{
	// Initialize the out-vector to a random direction
	pRand->spherical(pOutVector, dims);

	// Iterate
	double* pVector;
	size_t nCount = rows();
	GTEMPBUF(double, pAccumulator, dims);
	double d;
	double mag = 0;
	for(size_t iters = 0; iters < 200; iters++)
	{
		GVec::setAll(pAccumulator, 0.0, dims);
		for(size_t n = 0; n < nCount; n++)
		{
			pVector = row(n);
			d = GVec::dotProduct(pVector, pOutVector, dims);
			double* pAcc = pAccumulator;
			for(size_t j = 0; j < dims; j++)
				*(pAcc++) += d * *(pVector++);
		}
		GVec::copy(pOutVector, pAccumulator, dims);
		GVec::safeNormalize(pOutVector, dims, pRand);
		d = GVec::squaredMagnitude(pAccumulator, dims);
		if(iters < 6 || d > mag)
			mag = d;
		else
			break;
	}
}

void GMatrix::principalComponentIgnoreUnknowns(double* pOutVector, size_t dims, const double* pMean, GRand* pRand)
{
	// Initialize the out-vector to a random direction
	pRand->spherical(pOutVector, dims);

	// Iterate
	double* pVector;
	size_t nCount = rows();
	GTEMPBUF(double, pAccumulator, dims);
	double d;
	double mag = 0;
	for(size_t iters = 0; iters < 200; iters++)
	{
		GVec::setAll(pAccumulator, 0.0, dims);
		for(size_t n = 0; n < nCount; n++)
		{
			pVector = row(n);
			d = GVec::dotProductIgnoringUnknowns(pMean, pVector, pOutVector, dims);
			double* pAcc = pAccumulator;
			const double* pM = pMean;
			for(size_t j = 0; j < dims; j++)
			{
				if(*pVector != UNKNOWN_REAL_VALUE)
					(*pAcc) += d * (*pVector - *pM);
				pVector++;
				pAcc++;
				pM++;
			}
		}
		GVec::copy(pOutVector, pAccumulator, dims);
		GVec::safeNormalize(pOutVector, dims, pRand);
		d = GVec::squaredMagnitude(pAccumulator, dims);
		if(iters < 6 || d > mag)
			mag = d;
		else
			break;
	}
}

void GMatrix::weightedPrincipalComponent(double* pOutVector, size_t dims, const double* pMean, const double* pWeights, GRand* pRand)
{
	// Initialize the out-vector to a random direction
	pRand->spherical(pOutVector, dims);

	// Iterate
	double* pVector;
	size_t nCount = rows();
	GTEMPBUF(double, pAccumulator, dims);
	double d;
	double mag = 0;
	for(size_t iters = 0; iters < 200; iters++)
	{
		GVec::setAll(pAccumulator, 0.0, dims);
		const double* pW = pWeights;
		for(size_t n = 0; n < nCount; n++)
		{
			pVector = row(n);
			d = GVec::dotProduct(pMean, pVector, pOutVector, dims);
			double* pAcc = pAccumulator;
			const double* pM = pMean;
			for(size_t j = 0; j < dims; j++)
				*(pAcc++) += (*pW) * d * (*(pVector++) - *(pM++));
			pW++;
		}
		GVec::copy(pOutVector, pAccumulator, dims);
		GVec::safeNormalize(pOutVector, dims, pRand);
		d = GVec::squaredMagnitude(pAccumulator, dims);
		if(iters < 6 || d > mag)
			mag = d;
		else
			break;
	}
}

double GMatrix::eigenValue(const double* pMean, const double* pEigenVector, size_t dims, GRand* pRand)
{
	// Use the element of the eigenvector with the largest magnitude,
	// because that will give us the least rounding error when we compute the eigenvalue.
	size_t index = GVec::indexOfMaxMagnitude(pEigenVector, dims, pRand);

	// The eigenvalue is the factor by which the eigenvector is scaled by the covariance matrix,
	// so we compute just the part of the covariance matrix that we need to see how much the
	// max-magnitude element of the eigenvector is scaled.
	double d = 0;
	for(size_t i = 0; i < dims; i++)
		d += covariance(index, pMean[index], i, pMean[i]) * pEigenVector[i];
	return d / pEigenVector[index];
}

void GMatrix::removeComponent(const double* pMean, const double* pComponent, size_t dims)
{
	size_t nCount = rows();
	for(size_t i = 0; i < nCount; i++)
	{
		double* pVector = row(i);
		double d = GVec::dotProductIgnoringUnknowns(pMean, pVector, pComponent, dims);
		for(size_t j = 0; j < dims; j++)
		{
			if(*pVector != UNKNOWN_REAL_VALUE)
				(*pVector) -= d * pComponent[j];
			pVector++;
		}
	}
}

void GMatrix::removeComponentAboutOrigin(const double* pComponent, size_t dims)
{
	size_t nCount = rows();
	for(size_t i = 0; i < nCount; i++)
	{
		double* pVector = row(i);
		double d = GVec::dotProduct(pVector, pComponent, dims);
		for(size_t j = 0; j < dims; j++)
		{
			(*pVector) -= d * pComponent[j];
			pVector++;
		}
	}
}

void GMatrix::centerMeanAtOrigin()
{
	//Calculate mean
	size_t dims = cols();
	GTEMPBUF(double, mean, dims);
	centroid(mean);
	//Skip non-continuous rows by setting their mean to 0
	for(unsigned i = 0; i < dims; ++i){
	  if(relation()->valueCount(i) != 0){ mean[i] = 0; }
	}
	//Subtract the new mean from all rows
	for(vector<double*>::iterator it = m_rows.begin(); 
	    it != m_rows.end(); it++){
	  GVec::subtract(*it, mean, dims);
	}
}

size_t GMatrix::countPrincipalComponents(double d, GRand* pRand)
{
	size_t dims = cols();
	GMatrix tmpData(relation(), heap());
	tmpData.copy(this);
	tmpData.centerMeanAtOrigin();
	GTEMPBUF(double, vec, dims);
	double thresh = d * d * tmpData.sumSquaredDistance(NULL);
	size_t i;
	for(i = 1; i < dims; i++)
	{
		tmpData.principalComponentAboutOrigin(vec, dims, pRand);
		tmpData.removeComponentAboutOrigin(vec, dims);
		if(tmpData.sumSquaredDistance(NULL) < thresh)
			break;
	}
	return i;
}

double GMatrix::sumSquaredDistance(const double* pPoint)
{
	size_t dims = relation()->size();
	double err = 0;
	if(pPoint)
	{
		for(size_t i = 0; i < rows(); i++)
			err += GVec::squaredDistance(pPoint, row(i), dims);
	}
	else
	{
		for(size_t i = 0; i < rows(); i++)
			err += GVec::squaredMagnitude(row(i), dims);
	}
	return err;
}

double GMatrix::columnSumSquaredDifference(GMatrix& that, size_t col)
{
	if(that.rows() != rows())
		ThrowError("Mismatching number of rows");
	if(col >= cols() || col >= that.cols())
		ThrowError("column index out of range");
	double sse = 0.0;
	if(relation()->valueCount(col) == 0)
	{
		for(size_t i = 0; i < rows(); i++)
		{
			double d = row(i)[col] - that.row(i)[col];
			sse += (d * d);
		}
	}
	else
	{
		for(size_t i = 0; i < rows(); i++)
		{
			if((int)row(i)[col] != (int)that.row(i)[col])
				sse++;
		}
	}
	return sse;
}

double GMatrix::sumSquaredDifference(GMatrix& that, bool transpose)
{
	if(transpose)
	{
		size_t colCount = (size_t)cols();
		if(rows() != (size_t)that.cols() || colCount != that.rows())
			ThrowError("expected matrices of same size");
		double err = 0;
		for(size_t i = 0; i < rows(); i++)
		{
			double* pRow = row(i);
			for(size_t j = 0; j < colCount; j++)
			{
				double d = *(pRow++) - that[j][i];
				err += (d * d);
			}
		}
		return err;
	}
	else
	{
		if(this->rows() != that.rows() || this->cols() != that.cols())
			ThrowError("mismatching sizes");
		size_t colCount = cols();
		double d = 0;
		for(size_t i = 0; i < rows(); i++)
			d += GVec::squaredDistance(this->row(i), that[i], colCount);
		return d;
	}
}

double GMatrix::linearCorrelationCoefficient(size_t attr1, double attr1Origin, size_t attr2, double attr2Origin)
{
	double sx = 0;
	double sy = 0;
	double sxy = 0;
	double mx, my;
	double* pPat;
	size_t count = rows();
	size_t i;
	for(i = 0; i < count; i++)
	{
		pPat = row(i);
		mx = pPat[attr1] - attr1Origin;
		my = pPat[attr2] - attr2Origin;
		if(pPat[attr1] == UNKNOWN_REAL_VALUE || pPat[attr2] == UNKNOWN_REAL_VALUE)
			continue;
		break;
	}
	if(i >= count)
		return 0;
	double d, x, y;
	size_t j = 1;
	for(i++; i < count; i++)
	{
		pPat = row(i);
		if(pPat[attr1] == UNKNOWN_REAL_VALUE || pPat[attr2] == UNKNOWN_REAL_VALUE)
			continue;
		x = pPat[attr1] - attr1Origin;
		y = pPat[attr2] - attr2Origin;
		d = (double)j / (j + 1);
		sx += (x - mx) * (x - mx) * d;
		sy += (y - my) * (y - my) * d;
		sxy += (x - mx) * (y -  my) * d;
		mx += (x - mx) / (j + 1);
		my += (y - my) / (j + 1);
		j++;
	}
	if(sx == 0 || sy == 0 || sxy == 0)
		return 0;
	return (sxy / j) / (sqrt(sx / j) * sqrt(sy / j));
}

double GMatrix::covariance(size_t nAttr1, double dMean1, size_t nAttr2, double dMean2)
{
	size_t nRowCount = rows();
	double* pVector;
	double dSum = 0;
	for(size_t i = 0; i < nRowCount; i++)
	{
		pVector = row(i);
		dSum += ((pVector[nAttr1] - dMean1) * (pVector[nAttr2] - dMean2));
	}
	return dSum / (nRowCount - 1);
}

GMatrix* GMatrix::covarianceMatrix()
{
	size_t colCount = cols();
	GMatrix* pOut = new GMatrix(colCount, colCount);

	// Compute the deviations
	GTEMPBUF(double, pMeans, colCount);
	for(size_t i = 0; i < colCount; i++)
		pMeans[i] = mean(i);

	// Compute the covariances for half the matrix
	for(size_t i = 0; i < colCount; i++)
	{
		double* pRow = pOut->row(i);
		pRow += i;
		for(size_t n = i; n < colCount; n++)
			*(pRow++) = covariance(i, pMeans[i], n, pMeans[n]);
	}

	// Fill out the other half of the matrix
	for(size_t i = 1; i < colCount; i++)
	{
		double* pRow = pOut->row(i);
		for(size_t n = 0; n < i; n++)
			*(pRow++) = pOut->row(n)[i];
	}
	return pOut;
}

class Row_Binary_Predicate_Functor
{
protected:
	size_t m_dim;

public:
	Row_Binary_Predicate_Functor(size_t dim) : m_dim(dim)
	{
	}

	bool operator() (double* pA, double* pB) const
	{
		return pA[m_dim] < pB[m_dim];
	}
};

void GMatrix::sort(size_t dim)
{
	Row_Binary_Predicate_Functor comparer(dim);
	std::sort(m_rows.begin(), m_rows.end(), comparer);
}

void GMatrix::sortPartial(size_t row, size_t col)
{
	Row_Binary_Predicate_Functor comparer(col);
	vector<double*>::iterator targ = m_rows.begin() + row;
	std::nth_element(m_rows.begin(), targ, m_rows.end(), comparer);
}

void GMatrix::reverseRows()
{
	std::reverse(m_rows.begin(), m_rows.end());
}

double GMatrix_PairedTTestHelper(void* pThis, double x)
{
	double v = *(double*)pThis;
	return pow(1.0 + x * x / v, -(v + 1) / 2);
}

void GMatrix::pairedTTest(size_t* pOutV, double* pOutT, size_t attr1, size_t attr2, bool normalize)
{
	double* pPat;
	double a, b, m;
	double asum = 0;
	double asumOfSquares = 0;
	double bsum = 0;
	double bsumOfSquares = 0;
	size_t rowCount = rows();
	for(size_t i = 0; i < rowCount; i++)
	{
		pPat = row(i);
		a = pPat[attr1];
		b = pPat[attr2];
		if(normalize)
		{
			m = (a + b) / 2;
			a /= m;
			b /= m;
		}
		asum += a;
		asumOfSquares += (a * a);
		bsum += b;
		bsumOfSquares += (b * b);
	}
	double amean = asum / rowCount;
	double avariance = (asumOfSquares / rowCount - amean * amean) * rowCount / (rowCount - 1);
	double bmean = bsum / rowCount;
	double bvariance = (bsumOfSquares / rowCount - bmean * bmean) * rowCount / (rowCount - 1);
	double grand = sqrt((avariance + bvariance) / rowCount);
	*pOutV = 2 * rowCount - 2;
	*pOutT = std::abs(bmean - amean) / grand;
}

void GMatrix::wilcoxonSignedRanksTest(size_t attr1, size_t attr2, double tolerance, int* pNum, double* pWMinus, double* pWPlus)
{
	// Make sorted list of differences
	GHeap heap(1024);
	GMatrix tmp(0, 2, &heap); // col 0 holds the absolute difference. col 1 holds the sign.
	for(size_t i = 0; i < rows(); i++)
	{
		double* pPat = row(i);
		double absdiff = std::abs(pPat[attr2] - pPat[attr1]);
		if(absdiff >= tolerance)
		{
			double* pStat = tmp.newRow();
			pStat[0] = absdiff;
			if(pStat[0] < tolerance)
				pStat[1] = 0;
			else if(pPat[attr1] < pPat[attr2])
				pStat[1] = -1;
			else
				pStat[1] = 1;
		}
	}

	// Convert column 0 to ranks
	tmp.sort(0);
	double prev = UNKNOWN_REAL_VALUE;
	size_t index = 0;
	size_t j;
	double ave;
	for(size_t i = 0; i < tmp.rows(); i++)
	{
		double* pPat = tmp[i];
		if(std::abs(pPat[0] - prev) >= tolerance)
		{
			ave = (double)(index + 1 + i) / 2;
			for(j = index; j < i; j++)
			{
				double* pStat = tmp[j];
				pStat[0] = ave;
			}
			prev = pPat[0];
			index = i;
		}
	}
	ave = (double)(index + 1 + tmp.rows()) / 2;
	for(j = index; j < tmp.rows(); j++)
	{
		double* pStat = tmp[j];
		pStat[0] = ave;
	}

	// Sum up the scores
	double a = 0;
	double b = 0;
	for(size_t i = 0; i < tmp.rows(); i++)
	{
		double* pStat = tmp[i];
		if(pStat[1] > 0)
			a += pStat[0];
		else if(pStat[1] < 0)
			b += pStat[0];
		else
		{
			a += 0.5 * pStat[0];
			b += 0.5 * pStat[0];
		}
	}
	*pNum = tmp.rows();
	*pWMinus = b;
	*pWPlus = a;
}

size_t GMatrix::countValue(size_t attribute, double value)
{
	size_t count = 0;
	for(size_t i = 0; i < rows(); i++)
	{
		if(row(i)[attribute] == value)
			count++;
	}
	return count;
}

bool GMatrix::doesHaveAnyMissingValues()
{
	size_t dims = m_pRelation->size();
	for(size_t j = 0; j < dims; j++)
	{
		if(m_pRelation->valueCount(j) == 0)
		{
			for(size_t i = 0; i < rows(); i++)
			{
				if(row(i)[j] == UNKNOWN_REAL_VALUE)
					return true;
			}
		}
		else
		{
			for(size_t i = 0; i < rows(); i++)
			{
				if(row(i)[j] == UNKNOWN_DISCRETE_VALUE)
					return true;
			}
		}
	}
	return false;
}

void GMatrix::ensureDataHasNoMissingReals()
{
	size_t dims = m_pRelation->size();
	for(size_t i = 0; i < rows(); i++)
	{
		double* pPat = row(i);
		for(size_t j = 0; j < dims; j++)
		{
			if(m_pRelation->valueCount(j) != 0)
				continue;
			if(pPat[i] == UNKNOWN_REAL_VALUE)
				ThrowError("Missing values in continuous attributes are not supported");
		}
	}
}

void GMatrix::ensureDataHasNoMissingNominals()
{
	size_t dims = m_pRelation->size();
	for(size_t i = 0; i < rows(); i++)
	{
		double* pPat = row(i);
		for(size_t j = 0; j < dims; j++)
		{
			if(m_pRelation->valueCount(j) == 0)
				continue;
			if((int)pPat[i] == UNKNOWN_DISCRETE_VALUE)
				ThrowError("Missing values in nominal attributes are not supported");
		}
	}
}

void GMatrix::print(ostream& stream)
{
	m_pRelation->print(stream, this, 14);
}

double GMatrix::measureInfo()
{
	size_t c = cols();
	double dInfo = 0;
	for(size_t n = 0; n < c; n++)
	{
		if(m_pRelation->valueCount(n) == 0)
		{
			if(rows() > 1)
			{
				double m = mean(n);
				dInfo += variance(n, m);
			}
		}
		else
			dInfo += entropy(n);
	}
	return dInfo;
}

double GMatrix::sumSquaredDiffWithIdentity()
{
	size_t m = std::min(rows(), (size_t)cols());
	double err = 0;
	double d;
	for(size_t i = 0; i < m; i++)
	{
		double* pRow = row(i);
		for(size_t j = 0; j < m; j++)
		{
			d = *(pRow++);
			if(i == j)
				d -= 1;
			err += (d * d);
		}
	}
	return err;
}
/*
bool GMatrix::leastCorrelatedVector(double* pOut, GMatrix* pThat, GRand* pRand)
{
	if(rows() != pThat->rows() || cols() != pThat->cols())
		ThrowError("Expected matrices with the same dimensions");
	GMatrix* pC = GMatrix::multiply(*pThat, *this, false, true);
	Holder<GMatrix> hC(pC);
	GMatrix* pE = GMatrix::multiply(*pC, *pC, true, false);
	Holder<GMatrix> hE(pE);
	double d = pE->sumSquaredDiffWithIdentity();
	if(d < 0.001)
		return false;
	GMatrix* pF = pE->mostSignificantEigenVectors(rows(), pRand);
	Holder<GMatrix> hF(pF);
	GVec::copy(pOut, pF->row(rows() - 1), rows());
	return true;
}
*/
bool GMatrix::leastCorrelatedVector(double* pOut, GMatrix* pThat, GRand* pRand)
{
	if(rows() != pThat->rows() || cols() != pThat->cols())
		ThrowError("Expected matrices with the same dimensions");
	GMatrix* pC = GMatrix::multiply(*pThat, *this, false, true);
	Holder<GMatrix> hC(pC);
	GMatrix* pD = GMatrix::multiply(*pThat, *pC, true, false);
	Holder<GMatrix> hD(pD);
	double d = pD->sumSquaredDifference(*this, true);
	if(d < 1e-9)
		return false;
	pD->subtract(this, true);
	pD->principalComponentAboutOrigin(pOut, rows(), pRand);
	return true;
/*
	GMatrix* pE = GMatrix::multiply(*pD, *pD, true, false);
	Holder<GMatrix> hE(pE);
	GMatrix* pF = pE->mostSignificantEigenVectors(1, pRand);
	Holder<GMatrix> hF(pF);
	GVec::copy(pOut, pF->row(0), rows());
	return true;
*/
}

double GMatrix::dihedralCorrelation(GMatrix* pThat, GRand* pRand)
{
	size_t colCount = cols();
	if(rows() == 1)
		return std::abs(GVec::dotProduct(row(0), pThat->row(0), colCount));
	GTEMPBUF(double, pBuf, rows() + 2 * colCount);
	double* pA = pBuf + rows();
	double* pB = pA + colCount;
	if(!leastCorrelatedVector(pBuf, pThat, pRand))
		return 1.0;
	multiply(pBuf, pA, true);
	if(!pThat->leastCorrelatedVector(pBuf, this, pRand))
		return 1.0;
	pThat->multiply(pBuf, pB, true);
	return std::abs(GVec::correlation(pA, pB, colCount));
}

void GMatrix::project(double* pDest, const double* pPoint)
{
	size_t dims = cols();
	GVec::setAll(pDest, 0.0, dims);
	for(size_t i = 0; i < rows(); i++)
	{
		double* pBasis = row(i);
		GVec::addScaled(pDest, GVec::dotProduct(pPoint, pBasis, dims), pBasis, dims);
	}
}

void GMatrix::project(double* pDest, const double* pPoint, const double* pOrigin)
{
	size_t dims = cols();
	GVec::copy(pDest, pOrigin, dims);
	for(size_t i = 0; i < rows(); i++)
	{
		double* pBasis = row(i);
		GVec::addScaled(pDest, GVec::dotProduct(pOrigin, pPoint, pBasis, dims), pBasis, dims);
	}
}

/// This is a helper class used only by GMatrix::bipartiteMatching
class GBMNode
{
public:
	double m_dist;
	size_t m_a;
	size_t m_b;
	GBMNode* m_pPrevDist;
	GBMNode* m_pNextDist;
	GBMNode* m_pPrevA;
	GBMNode* m_pNextA;
	GBMNode* m_pPrevB;
	GBMNode* m_pNextB;

	GBMNode(double dist, size_t a, size_t b, GBMNode** ppHead, GBMNode** ppAB, GBMNode** ppBA)
	: m_dist(dist), m_a(a), m_b(b), m_pPrevDist(NULL)
	{
		// Link by distance
		m_pNextDist = *ppHead;
		*ppHead = this;
		if(m_pNextDist)
			m_pNextDist->m_pPrevDist = this;

		// Link by a-b
		if(*ppAB)
		{
			m_pNextA = *ppAB;
			m_pPrevA = m_pNextA->m_pPrevA;
			m_pNextA->m_pPrevA = this;
			m_pPrevA->m_pNextA = this;
		}
		else
		{
			m_pNextA = this;
			m_pPrevA = this;
			*ppAB = this;
		}

		// Link by b-a
		if(*ppBA)
		{
			m_pNextB = *ppBA;
			m_pPrevB = m_pNextB->m_pPrevB;
			m_pNextB->m_pPrevB = this;
			m_pPrevB->m_pNextB = this;
		}
		else
		{
			m_pNextB = this;
			m_pPrevB = this;
			*ppBA = this;
		}
	}

	void nix(GBMNode** ppNextDist, size_t* pResults, size_t* pCount)
	{
		if(*ppNextDist == this)
			*ppNextDist = m_pNextDist;
		GBMNode* pNextA = m_pNextA;
		GBMNode* pNextB = m_pNextB;
		if(m_pPrevDist)
			m_pPrevDist->m_pNextDist = m_pNextDist;
		if(m_pNextDist)
			m_pNextDist->m_pPrevDist = m_pPrevDist;
		m_pNextA->m_pPrevA = m_pPrevA;
		m_pPrevA->m_pNextA = m_pNextA;
		m_pNextA = this;
		m_pPrevA = this;
		if(pNextA != this && pNextA->m_pNextA == pNextA)
		{
			if(pResults[pNextA->m_a] == size_t(-1))
			{
				pResults[pNextA->m_a] = pNextA->m_b;
				(*pCount)++;
				while(pNextA->m_pNextB != pNextA)
					pNextA->m_pNextB->nix(ppNextDist, pResults, pCount);
			}
		}
		m_pNextB->m_pPrevB = m_pPrevB;
		m_pPrevB->m_pNextB = m_pNextB;
		m_pNextB = this;
		m_pPrevB = this;
		if(pNextB != this && pNextB->m_pNextB == pNextB)
		{
			if(pResults[pNextB->m_a] == size_t(-1))
			{
				pResults[pNextB->m_a] = pNextB->m_b;
				(*pCount)++;
				while(pNextB->m_pNextA != pNextB)
					pNextB->m_pNextA->nix(ppNextDist, pResults, pCount);
			}
		}
	}

	static GBMNode* mergeSort(GBMNode* pFirst, size_t len)
	{
		// Split
		size_t firstLen = len / 2;
		size_t secondLen = len - firstLen;
		GBMNode* pSecond = pFirst;
		for(size_t i = 0; i < firstLen; i++)
			pSecond = pSecond->m_pNextDist;

		// Recurse
		if(firstLen >= 2)
			pFirst = mergeSort(pFirst, firstLen);
		else if(firstLen == 0)
			return pSecond;
		if(secondLen >= 2)
			pSecond = mergeSort(pSecond, secondLen);

		// Merge
		GBMNode* pHead = pFirst;
		while(true)
		{
			if(pFirst->m_dist < pSecond->m_dist)
			{
				// Unlink the second
				GBMNode* pNextSecond = pSecond->m_pNextDist;
				if(pSecond->m_pNextDist)
					pSecond->m_pNextDist->m_pPrevDist = pSecond->m_pPrevDist;
				if(pSecond->m_pPrevDist)
					pSecond->m_pPrevDist->m_pNextDist = pSecond->m_pNextDist;

				// Link in the new spot
				pSecond->m_pPrevDist = pFirst->m_pPrevDist;
				pSecond->m_pNextDist = pFirst;
				if(pFirst->m_pPrevDist)
					pFirst->m_pPrevDist->m_pNextDist = pSecond;
				pFirst->m_pPrevDist = pSecond;
				if(pHead == pFirst)
					pHead = pSecond;
				pSecond = pNextSecond;
				if(--secondLen == 0)
					break;
			}
			else
			{
				pFirst = pFirst->m_pNextDist;
				if(--firstLen == 0)
					break;
			}
		}
		return pHead;
	}
};

// static
size_t* GMatrix::bipartiteMatching(GMatrix& a, GMatrix& b, GDistanceMetric& metric, size_t k)
{
	if(a.rows() == 0)
		return NULL;
	if(a.cols() != b.cols())
		ThrowError("Expected two matrices with the same number of columns");
	if(b.rows() < a.rows())
		ThrowError("Matrix b must have at least as many rows as matrix a");
	metric.init(a.relation());
	size_t ka = std::min(a.rows(), k);
	if(ka == 0)
		ka = a.rows();
	size_t kb = std::min(b.rows(), k);
	if(kb == 0)
		kb = b.rows();
	vector<GBMNode*> a_b; // Loop of every edge from a to b
	a_b.resize(a.rows(), NULL);
	vector<GBMNode*> b_a; // Loop of every edge from b to a
	b_a.resize(b.rows(), NULL);
	GHeap heap(4096);
	GBMNode* pHead = NULL;
	size_t candCount = 0;
	if(ka >= a.rows())
	{
		// Fully-connect every row in 'a' with every row in 'b'
		for(size_t aa = 0; aa < a.rows(); aa++)
		{
			double* pRowA = a[aa];
			for(size_t bb = 0; bb < b.rows(); bb++)
			{
				new (heap.allocAligned(sizeof(GBMNode))) GBMNode(metric.squaredDistance(pRowA, b[bb]), aa, bb, &pHead, &a_b[aa], &b_a[bb]); // allocate with placement new
				candCount++;
			}
		}
	}
	else
	{
		size_t* pNeighbors = new size_t[kb];
		ArrayHolder<size_t> hNeighbors(pNeighbors);
		double* pDistances = new double[kb];
		ArrayHolder<double> hDistances(pDistances);
		vector< std::set<size_t> > used;
		used.resize(a.rows());
		{
			// Add the k-nearest neighbors in b of each row in a
			GKdTree nf(&b, kb, &metric, false);
			for(size_t aa = 0; aa < a.rows(); aa++)
			{
				nf.neighbors(pNeighbors, pDistances, a[aa]);
				size_t* pB = pNeighbors;
				double* pDist = pDistances;
				std::set<size_t>& usedSet = used[aa];
				for(size_t j = 0; j < kb; j++)
				{
					if(*pB < b.rows())
					{
						new (heap.allocAligned(sizeof(GBMNode))) GBMNode(*pDist, aa, *pB, &pHead, &a_b[aa], &b_a[*pB]); // allocate with placement new
						usedSet.insert(*pB);
						candCount++;
					}
					pB++;
					pDist++;
				}
			}
		}
		{
			// Add the k-nearest neighbors in a of each row in b
			GKdTree nf(&a, ka, &metric, false);
			for(size_t bb = 0; bb < b.rows(); bb++)
			{
				nf.neighbors(pNeighbors, pDistances, b[bb]);
				size_t* pA = pNeighbors;
				double* pDist = pDistances;
				for(size_t j = 0; j < ka; j++)
				{
					if(*pA < a.rows())
					{
						std::set<size_t>& usedSet = used[*pA];
						if(usedSet.find(bb) == usedSet.end())
						{
							new (heap.allocAligned(sizeof(GBMNode))) GBMNode(*pDist, *pA, bb, &pHead, &a_b[*pA], &b_a[bb]); // allocate with placement new
							candCount++;
						}
					}
					pA++;
					pDist++;
				}
			}
		}
	}

	// Sort the distance list by distance (greatest first)
	pHead = GBMNode::mergeSort(pHead, candCount);

	// Discard the worst matchings until all rows are matched exactly once
	size_t* pResults = new size_t[a.rows()];
	ArrayHolder<size_t> hResults(pResults);
	if(a.rows() == 1)
	{
		pResults[0] = 0;
		return hResults.release();
	}
	size_t* pRes = pResults;
	for(size_t i = 0; i < a.rows(); i++)
		*(pRes++) = size_t(-1);
	size_t resultCount = 0;
	size_t iter = 0;
	while(pHead)
	{
		GBMNode* pNext = pHead;
		pHead->nix(&pNext, pResults, &resultCount);
		if(resultCount >= a.rows())
			break;
		pHead = pNext;
		iter++;
	}
	if(resultCount < a.rows())
		ThrowError("not enough neighbors for a complete solution");
	else if(resultCount > a.rows())
		ThrowError("internal error");
	return hResults.release();
}

#ifndef NO_TEST_CODE
void GMatrix_stressBipartiteMatching()
{
	// This test does bipartite matching with a bunch of
	// random matrices, to make sure there are no crashes
	// or endless loops. The results are not checked since
	// correct results are not known.
	GRand rand(0);
	GRowDistance metric;
	for(size_t i = 0; i < 200; i++)
	{
		size_t rowsa = (size_t)rand.next(20);
		size_t rowsb = std::max(rowsa, (size_t)rand.next(20));
		size_t cols = (size_t)rand.next(8);
		GMatrix a(rowsa, cols);
		GMatrix b(rowsb, cols);
		for(size_t j = 0; j < rowsa; j++)
		{
			double* pA = a[j];
			for(size_t k = 0; k < cols; k++)
				*(pA++) = rand.normal();
		}
		for(size_t j = 0; j < rowsb; j++)
		{
			double* pB = b[j];
			for(size_t k = 0; k < cols; k++)
				*(pB++) = rand.normal();
		}
		size_t* pResults = GMatrix::bipartiteMatching(a, b, metric);
		ArrayHolder<size_t> hResults(pResults);
	}
}

void GMatrix_testBipartiteMatching()
{
	GMatrix a(7, 2);
	a[0][0] = 0; a[0][1] = 0;
	a[1][0] = 2; a[1][1] = 1;
	a[2][0] = 5; a[2][1] = 2;
	a[3][0] = 3; a[3][1] = 5;
	a[4][0] = 4; a[4][1] = 7;
	a[5][0] = 1; a[5][1] = 4;
	a[6][0] = 4; a[6][1] = 2;
	GMatrix b(7, 2);
	b[0][0] = 4; b[0][1] = 2;
	b[1][0] = 2; b[1][1] = 1;
	b[2][0] = 5.01; b[2][1] = 2;
	b[3][0] = 6; b[3][1] = 2;
	b[4][0] = 3.9; b[4][1] = 6.9;
	b[5][0] = 3.1; b[5][1] = 5.1;
	b[6][0] = 2.9; b[6][1] = 4.9;

	GRowDistance metric;
	size_t* pResults = GMatrix::bipartiteMatching(a, b, metric, 5);
	ArrayHolder<size_t> hResults(pResults);
	if(pResults[0] != 1)
		ThrowError("failed");
	if(pResults[1] != 0)
		ThrowError("failed");
	if(pResults[2] != 3)
		ThrowError("failed");
	if(pResults[3] != 5)
		ThrowError("failed");
	if(pResults[4] != 4)
		ThrowError("failed");
	if(pResults[5] != 6)
		ThrowError("failed");
	if(pResults[6] != 2)
		ThrowError("failed");
}

void GMatrix_testMultiply()
{
	GMatrix a(2, 2);
	a[0][0] = 2; a[0][1] = 17;
	a[1][0] = 7; a[1][1] = 19;
	GMatrix b(2, 2);
	b[0][0] = 11; b[0][1] = 3;
	b[1][0] = 5; b[1][1] = 13;
	GMatrix* pC;
	pC = GMatrix::multiply(a, b, false, false);
	if(pC->rows() != 2 || pC->cols() != 2)
		ThrowError("wrong size");
	if(pC->row(0)[0] != 107 || pC->row(0)[1] != 227 ||
		pC->row(1)[0] != 172 || pC->row(1)[1] != 268)
		ThrowError("wrong answer");
	delete(pC);
	GMatrix* pA = a.transpose();
	pC = GMatrix::multiply(*pA, b, true, false);
	if(pC->rows() != 2 || pC->cols() != 2)
		ThrowError("wrong size");
	if(pC->row(0)[0] != 107 || pC->row(0)[1] != 227 ||
		pC->row(1)[0] != 172 || pC->row(1)[1] != 268)
		ThrowError("wrong answer");
	delete(pC);
	GMatrix* pB = b.transpose();
	pC = GMatrix::multiply(a, *pB, false, true);
	if(pC->rows() != 2 || pC->cols() != 2)
		ThrowError("wrong size");
	if(pC->row(0)[0] != 107 || pC->row(0)[1] != 227 ||
		pC->row(1)[0] != 172 || pC->row(1)[1] != 268)
		ThrowError("wrong answer");
	delete(pC);
	pC = GMatrix::multiply(*pA, *pB, true, true);
	if(pC->rows() != 2 || pC->cols() != 2)
		ThrowError("wrong size");
	if(pC->row(0)[0] != 107 || pC->row(0)[1] != 227 ||
		pC->row(1)[0] != 172 || pC->row(1)[1] != 268)
		ThrowError("wrong answer");
	delete(pC);
	delete(pA);
	delete(pB);
}

void GMatrix_testCholesky()
{
	GMatrix m1(3, 3);
	m1[0][0] = 3;	m1[0][1] = 0;	m1[0][2] = 0;
	m1[1][0] = 1;	m1[1][1] = 4;	m1[1][2] = 0;
	m1[2][0] = 2;	m1[2][1] = 2;	m1[2][2] = 7;
	GMatrix* pM3 = GMatrix::multiply(m1, m1, false, true);
	Holder<GMatrix> hM3(pM3);
	GMatrix* pM4 = pM3->cholesky();
	Holder<GMatrix> hM4(pM4);
	if(m1.sumSquaredDifference(*pM4, false) >= .0001)
		ThrowError("Cholesky decomposition didn't work right");
}

void GMatrix_testInvert()
{
	GMatrix i1(3, 3);
	i1[0][0] = 2;	i1[0][1] = -1;	i1[0][2] = 0;
	i1[1][0] = -1;	i1[1][1] = 2;	i1[1][2] = -1;
	i1[2][0] = 0;	i1[2][1] = -1;	i1[2][2] = 2;
//	i1.invert();
	GMatrix* pInv = i1.pseudoInverse();
	Holder<GMatrix> hInv(pInv);
	GMatrix i2(3, 3);
	i2[0][0] = .75;	i2[0][1] = .5;	i2[0][2] = .25;
	i2[1][0] = .5;	i2[1][1] = 1;	i2[1][2] = .5;
	i2[2][0] = .25;	i2[2][1] = .5;	i2[2][2] = .75;
	if(pInv->sumSquaredDifference(i2, false) >= .0001)
		ThrowError("Not good enough");
//	i1.invert();
	GMatrix* pInvInv = pInv->pseudoInverse();
	Holder<GMatrix> hInvInv(pInvInv);
	GMatrix* pI3 = GMatrix::multiply(*pInvInv, i2, false, false);
	Holder<GMatrix> hI3(pI3);
	GMatrix i4(3, 3);
	i4.makeIdentity();
	if(pI3->sumSquaredDifference(i4, false) >= .0001)
		ThrowError("Not good enough");
}

void GMatrix_testDeterminant()
{
	const double dettest[] =
	{
		1,2,3,4,
		5,6,7,8,
		2,6,4,8,
		3,1,1,2,
	};
	GMatrix d1(0, 4);
	d1.fromVector(dettest, 4);
	double det = d1.determinant();
	if(std::abs(det - 72.0) >= .0001)
		ThrowError("wrong");
	const double dettest2[] =
	{
		3,2,
		5,7,
	};
	GMatrix d2(0, 2);
	d2.fromVector(dettest2, 2);
	det = d2.determinant();
	if(std::abs(det - 11.0) >= .0001)
		ThrowError("wrong");
	const double dettest3[] =
	{
		1,2,3,
		4,5,6,
		7,8,9,
	};
	GMatrix d3(0, 3);
	d3.fromVector(dettest3, 3);
	det = d3.determinant();
	if(std::abs(det - 0.0) >= .0001)
		ThrowError("wrong");
}

void GMatrix_testReducedRowEchelonForm()
{
	const double reducedrowechelonformtest[] =
	{
		1,-1,1,0,2,
		2,-2,0,2,2,
		-1,1,2,-3,1,
		-2,2,1,-3,-1,
	};
	const double reducedrowechelonformanswer[] =
	{
		1,-1,0,1,1,
		0,0,1,-1,1,
		0,0,0,0,0,
		0,0,0,0,0,
	};
	GMatrix r1(0, 5);
	r1.fromVector(reducedrowechelonformtest, 4);
	if(r1.toReducedRowEchelonForm() != 2)
		ThrowError("wrong answer");
	GMatrix r2(0, 5);
	r2.fromVector(reducedrowechelonformanswer, 4);
	if(r1.sumSquaredDifference(r2) > .001)
		ThrowError("wrong answer");
	const double reducedrowechelonformtest2[] =
	{
		-2, -4, 4,
		2, -8, 0,
		8, 4, -12,
	};
	const double reducedrowechelonformanswer2[] =
	{
		1, 0, -4.0/3,
		0, 1, -1.0/3,
		0, 0, 0,
	};
	GMatrix r3(0, 3);
	r3.fromVector(reducedrowechelonformtest2, 3);
	if(r3.toReducedRowEchelonForm() != 2)
		ThrowError("wrong answer");
	GMatrix r4(0, 3);
 	r4.fromVector(reducedrowechelonformanswer2, 3);
	if(r4.sumSquaredDifference(r3) > .001)
		ThrowError("wrong answer");
}

void GMatrix_testPrincipalComponents(GRand& prng)
{
	// Test principal components
	GHeap heap(1000);
	GMatrix data(0, 2, &heap);
	for(size_t i = 0; i < 100; i++)
	{
		double* pNewRow = data.newRow();
		pNewRow[0] = prng.uniform();
		pNewRow[1] = 2 * pNewRow[0];
	}
	double mean[2];
	mean[0] = data.mean(0);
	mean[1] = data.mean(1);
	double eig[2];
	data.principalComponent(eig, 2, mean, &prng);
	if(std::abs(eig[0] * 2 - eig[1]) > .0001)
		ThrowError("incorrect value");

	// Compute principal components via eigenvectors of covariance matrix, and
	// make sure they're the same
	GMixedRelation rel;
	rel.addAttr(0);
	rel.addAttr(0);
	GMatrix* pM = data.covarianceMatrix();
	Holder<GMatrix> hM(pM);
	double ev;
	GMatrix* pEigenVecs = pM->eigs(1, &ev, &prng, true);
	Holder<GMatrix> hEigenVecs(pEigenVecs);
	if(std::abs(pEigenVecs->row(0)[0] * pEigenVecs->row(0)[1] - eig[0] * eig[1]) > .0001)
		ThrowError("answers don't agree");

	// Test most significant eigenvector computation
	GMatrix e1(2, 2);
	e1[0][0] = 1;	e1[0][1] = 1;
	e1[1][0] = 1;	e1[1][1] = 4;
	double ev2[2];
	GMatrix* pE2 = e1.eigs(2, ev2, &prng, true);
	Holder<GMatrix> hE2(pE2);
	if(std::abs(pE2->row(0)[0] * pE2->row(0)[0] + pE2->row(0)[1] * pE2->row(0)[1] - 1) > .0001)
		ThrowError("answer not normalized");
	if(std::abs(pE2->row(0)[0] * pE2->row(0)[1] - .27735) >= .0001)
		ThrowError("wrong answer");
	if(std::abs(pE2->row(1)[0] * pE2->row(1)[0] + pE2->row(1)[1] * pE2->row(1)[1] - 1) > .0001)
		ThrowError("answer not normalized");
	if(std::abs(pE2->row(1)[0] * pE2->row(1)[1] + .27735) >= .0001)
		ThrowError("wrong answer");

	// Test least significant eigenvector computation and gaussian ellimination
	GMatrix e3(2, 2);
	e3[0][0] = 9;	e3[0][1] = 3;
	e3[1][0] = 3;	e3[1][1] = 5;
	GMatrix* pE4 = e3.eigs(2, ev2, &prng, true);
	Holder<GMatrix> hE4(pE4);
	GMatrix* pE5 = e3.eigs(2, ev2, &prng, false);
	Holder<GMatrix> hE5(pE5);
	if(std::abs(std::abs(pE4->row(0)[0]) - std::abs(pE5->row(1)[0])) >= .0001)
		ThrowError("failed");
	if(std::abs(std::abs(pE4->row(0)[1]) - std::abs(pE5->row(1)[1])) >= .0001)
		ThrowError("failed");
	if(std::abs(std::abs(pE4->row(1)[0]) - std::abs(pE5->row(0)[0])) >= .0001)
		ThrowError("failed");
	if(std::abs(std::abs(pE4->row(1)[1]) - std::abs(pE5->row(0)[1])) >= .0001)
		ThrowError("failed");
}

void GMatrix_testDihedralCorrelation(GRand& prng)
{
	// Test dihedral angle computation
	for(size_t iter = 0; iter < 500; iter++)
	{
		// Make a random set of orthonormal basis vectors
		size_t dims = 5;
		GMatrix basis(dims, dims);
		for(size_t i = 0; i < dims; i++)
		{
			prng.spherical(basis[i], dims);
			for(size_t j = 0; j < i; j++)
			{
				GVec::subtractComponent(basis[i], basis[j], dims);
				GVec::normalize(basis[i], dims);
			}
		}

		// Make two planes with a known dihedral angle
		double angle = prng.uniform() * 0.5 * M_PI;
		double angle2 = prng.uniform() * 2.0 * M_PI;
		GMatrix p1(2, dims);
		GVec::setAll(p1[0], 0.0, dims);
		p1[0][0] = cos(angle2);
		p1[0][2] = sin(angle2);
		GVec::setAll(p1[1], 0.0, dims);
		p1[1][0] = -sin(angle2);
		p1[1][2] = cos(angle2);
		GMatrix p2(2, dims);
		GVec::setAll(p2[0], 0.0, dims);
		p2[0][0] = cos(angle);
		p2[0][1] = sin(angle);
		GVec::setAll(p2[1], 0.0, dims);
		p2[1][2] = 1.0;

		// Transform the planes with the basis matrix
		GMatrix p3(2, dims);
		basis.multiply(p1[0], p3[0]);
		basis.multiply(p1[1], p3[1]);
		GMatrix p4(2, dims);
		basis.multiply(p2[0], p4[0]);
		basis.multiply(p2[1], p4[1]);

		// Measure the angle
		double actual = cos(angle);
		double measured = p3.dihedralCorrelation(&p4, &prng);
		if(std::abs(measured - actual) > 1e-8)
			ThrowError("failed");
	}

	// Measure the dihedral angle of two 3-hyperplanes in 5-space
	double angle = 0.54321;
	GMatrix bas(5, 5);
	bas.makeIdentity();
	bas[2][2] = cos(angle);
	bas[2][4] = sin(angle);
	bas[4][2] = -sin(angle);
	bas[4][4] = cos(angle);
	GMatrix sp1(3, 5);
	sp1.makeIdentity();
	GMatrix* sp3 = GMatrix::multiply(sp1, bas, false, true);
	Holder<GMatrix> hSp3(sp3);
	double cosangle = sp1.dihedralCorrelation(sp3, &prng);
	double measured = acos(cosangle);
	if(std::abs(measured - angle) > 1e-8)
		ThrowError("failed");

	// Make sure dihedral angles are computed correctly with parallel planes
	static const double aa[] = {1.0, 0.0, 0.0, 0.0, -1.0, 0.0};
	static const double bb[] = {0.6, 0.8, 0.0, -0.8, 0.6, 0.0};
	GMatrix planeA(0, 3);
	planeA.fromVector(aa, 2);
	GMatrix planeB(0, 3);
	planeB.fromVector(bb, 2);
	cosangle = planeA.dihedralCorrelation(&planeB, &prng);
	if(std::abs(cosangle - 1.0) > 1e-8)
		ThrowError("failed");
	cosangle = planeB.dihedralCorrelation(&planeA, &prng);
	if(std::abs(cosangle - 1.0) > 1e-8)
		ThrowError("failed");
}

void GMatrix_testSingularValueDecomposition()
{
	GMatrix* pU;
	double* pDiag;
	GMatrix* pV;
	GMatrix M(2, 2);
	M[0][0] = 4.0; M[0][1] = 3.0;
	M[1][0] = 0.0; M[1][1] = -5.0;
	M.singularValueDecomposition(&pU, &pDiag, &pV);
	Holder<GMatrix> hU(pU);
	ArrayHolder<double> hDiag(pDiag);
	Holder<GMatrix> hV(pV);

	// Test that the diagonal values are correct
	if(std::abs(pDiag[0] - sqrt(40.0)) > 1e-8)
		ThrowError("pDiag is not correct");
	if(std::abs(pDiag[1] - sqrt(10.0)) > 1e-8)
		ThrowError("pDiag is not correct");

	// Test that U is unitary
	GMatrix* pT1 = GMatrix::multiply(*pU, *pU, false, true);
	Holder<GMatrix> hT1(pT1);
	if(pT1->sumSquaredDiffWithIdentity() > 1e-8)
		ThrowError("U is not unitary");

	// Test that V is unitary
	GMatrix* pT2 = GMatrix::multiply(*pV, *pV, false, true);
	Holder<GMatrix> hT2(pT2);
	if(pT2->sumSquaredDiffWithIdentity() > 1e-8)
		ThrowError("V is not unitary");
}

void GMatrix_testPseudoInverse()
{
	{
		GMatrix M(2, 2);
		M[0][0] = 1.0; M[0][1] = 1.0;
		M[1][0] = 2.0; M[1][1] = 2.0;
		GMatrix* A = M.pseudoInverse();
		Holder<GMatrix> hA(A);
		GMatrix B(2, 2);
		B[0][0] = 0.1; B[0][1] = 0.2;
		B[1][0] = 0.1; B[1][1] = 0.2;
		if(A->sumSquaredDifference(B, false) > 1e-8)
			ThrowError("failed");
	}
	{
		GMatrix M(3, 2);
		M[0][0] = 1.0; M[0][1] = 2.0;
		M[1][0] = 3.0; M[1][1] = 4.0;
		M[2][0] = 5.0; M[2][1] = 6.0;
		GMatrix* A = M.pseudoInverse();
		Holder<GMatrix> hA(A);
		if(A->rows() != 2 || A->cols() != 3)
			ThrowError("wrong size");
		GMatrix B(2, 3);
		B[0][0] = -16.0/12.0; B[0][1] = -4.0/12.0; B[0][2] = 8.0/12.0;
		B[1][0] = 13.0/12.0; B[1][1] = 4.0/12.0; B[1][2] = -5.0/12.0;
		if(A->sumSquaredDifference(B, false) > 1e-8)
			ThrowError("failed");
	}
	{
		GMatrix M(2, 3);
		M[0][0] = 1.0; M[0][1] = 3.0; M[0][2] = 5.0;
		M[1][0] = 2.0; M[1][1] = 4.0; M[1][2] = 6.0;
		GMatrix* A = M.pseudoInverse();
		Holder<GMatrix> hA(A);
		if(A->rows() != 3 || A->cols() != 2)
			ThrowError("wrong size");
		GMatrix B(3, 2);
		B[0][0] = -16.0/12.0; B[0][1] = 13.0/12.0;
		B[1][0] = -4.0/12.0; B[1][1] = 4.0/12.0;
		B[2][0] = 8.0/12.0; B[2][1] = -5.0/12.0;
		if(A->sumSquaredDifference(B, false) > 1e-8)
			ThrowError("failed");
	}
}

void GMatrix_testKabsch(GRand& prng)
{
	GMatrix a(20, 5);
	for(size_t i = 0; i < 20; i++)
	{
		prng.spherical(a[i], 5);
		GVec::multiply(a[i], prng.uniform() + 0.5, 5);
	}
	GMatrix rot(5, 5);
	static const double rr[] = {
		0.0, 1.0, 0.0, 0.0, 0.0,
		0.0, 0.0, 0.0, 1.0, 0.0,
		0.0, 0.0, 0.8, 0.0, -0.6,
		1.0, 0.0, 0.0, 0.0, 0.0,
		0.0, 0.0, 0.6, 0.0, 0.8
	};
	rot.fromVector(rr, 5);
	GMatrix* pB = GMatrix::multiply(a, rot, false, false);
	Holder<GMatrix> hB(pB);
	GMatrix* pK = GMatrix::kabsch(&a, pB);
	Holder<GMatrix> hK(pK);
	if(pK->sumSquaredDifference(rot, true) > 1e-6)
		ThrowError("Failed to recover rotation matrix");
	GMatrix* pC = GMatrix::multiply(*pB, *pK, false, false);
	Holder<GMatrix> hC(pC);
	if(a.sumSquaredDifference(*pC, false) > 1e-6)
		ThrowError("Failed to align data");
}

void GMatrix_testLUDecomposition(GRand& prng)
{
	GMatrix a(5, 5);
	for(size_t i = 0; i < 5; i++)
	{
		for(size_t j = 0; j < 5; j++)
			a[i][j] = prng.normal();
	}
	GMatrix* pB = a.clone();
	Holder<GMatrix> hB(pB);
	pB->LUDecomposition();
	GMatrix l(5, 5);
	l.setAll(0.0);
	GMatrix u(5, 5);
	u.setAll(0.0);
	for(size_t i = 0; i < 5; i++)
	{
		for(size_t j = 0; j < 5; j++)
		{
			if(i < j)
				u[i][j] = pB->row(i)[j];
			else
				l[i][j] = pB->row(i)[j];
		}
		u[i][i] = 1.0;
	}
	GMatrix* pProd = GMatrix::multiply(l, u, false, false);
	Holder<GMatrix> hProd(pProd);
	if(pProd->sumSquaredDifference(a, false) > 0.00001)
		ThrowError("failed");
}

// static
void GMatrix::test()
{
	GRand prng(0);
	GMatrix_testMultiply();
	GMatrix_testCholesky();
	GMatrix_testInvert();
	GMatrix_testDeterminant();
	GMatrix_testReducedRowEchelonForm();
	GMatrix_testPrincipalComponents(prng);
	GMatrix_testDihedralCorrelation(prng);
	GMatrix_testSingularValueDecomposition();
	GMatrix_testPseudoInverse();
	GMatrix_testKabsch(prng);
	GMatrix_testLUDecomposition(prng);
	GMatrix_testBipartiteMatching();
	GMatrix_stressBipartiteMatching();
}
#endif // !NO_TEST_CODE




GMatrixArray::GMatrixArray(sp_relation& pRelation)
: m_pRelation(pRelation)
{
}

GMatrixArray::GMatrixArray(size_t cols)
{
	m_pRelation = new GUniformRelation(cols, 0);
}

GMatrixArray::~GMatrixArray()
{
	flush();
}

void GMatrixArray::flush()
{
	for(vector<GMatrix*>::iterator it = m_sets.begin(); it != m_sets.end(); it++)
		delete(*it);
	m_sets.clear();
}

GMatrix* GMatrixArray::newSet(size_t rows)
{
	GMatrix* pData = new GMatrix(m_pRelation);
	m_sets.push_back(pData);
	pData->newRows(rows);
	return pData;
}

void GMatrixArray::newSets(size_t count, size_t rows)
{
	m_sets.reserve(m_sets.size() + count);
	for(size_t i = 0; i < count; i++)
		newSet(rows);
}

size_t GMatrixArray::largestSet()
{
	vector<GMatrix*>::iterator it = m_sets.begin();
	size_t biggestRows = (*it)->rows();
	size_t biggestIndex = 0;
	size_t i = 1;
	for(it++; it != m_sets.end(); it++)
	{
		if((*it)->rows() > biggestRows)
		{
			biggestRows = (*it)->rows();
			biggestIndex = i;
		}
		i++;
	}
	return biggestIndex;
}
