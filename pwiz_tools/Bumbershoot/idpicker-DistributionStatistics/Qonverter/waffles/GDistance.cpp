/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GDistance.h"
#include "GDom.h"
#include "GVec.h"
#include <math.h>
#include <cassert>

using std::map;

namespace GClasses {

GDistanceMetric::GDistanceMetric(GDomNode* pNode)
{
	m_pRelation = GRelation::deserialize(pNode->field("relation"));
}

double GDistanceMetric::squaredDistance(const std::vector<double> & x, const std::vector<double> & y) const{
	assert(x.size() == y.size());
	const std::size_t numDim = x.size();
	const double* firstX = numDim==0?0:&(x.front());
	const double* firstY = numDim==0?0:&(y.front());
	return squaredDistance(firstX, firstY);
}


GDomNode* GDistanceMetric::baseDomNode(GDom* pDoc, const char* szClassName)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "class", pDoc->newString(szClassName));
	pNode->addField(pDoc, "relation", m_pRelation->serialize(pDoc));
	return pNode;
}

// static
GDistanceMetric* GDistanceMetric::deserialize(GDomNode* pNode)
{
	const char* szClass = pNode->field("class")->asString();
	if(strcmp(szClass, "GRowDistanceScaled") == 0)
		return new GRowDistanceScaled(pNode);
	if(strcmp(szClass, "GRowDistance") == 0)
		return new GRowDistance(pNode);
	if(strcmp(szClass, "GLNormDistance") == 0)
		return new GLNormDistance(pNode);
	ThrowError("Unrecognized class: ", szClass);
	return NULL;
}

// --------------------------------------------------------------------

GRowDistance::GRowDistance()
: GDistanceMetric(), m_diffWithUnknown(1.0)
{
}

GRowDistance::GRowDistance(GDomNode* pNode)
: GDistanceMetric(pNode)
{
	m_diffWithUnknown = pNode->field("dwu")->asDouble();
}

// virtual
GDomNode* GRowDistance::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GRowDistance");
	pNode->addField(pDoc, "dwu", pDoc->newDouble(m_diffWithUnknown));
	return pNode;
}

// virtual
void GRowDistance::init(sp_relation& pRelation)
{
	m_pRelation = pRelation;
}

// virtual
double GRowDistance::squaredDistance(const double* pA, const double* pB) const
{
	GRelation* pRel = m_pRelation.get();
	double sum = 0;
	size_t count = pRel->size();
	double d;
	for(size_t i = 0; i < count; i++)
	{
		if(pRel->valueCount(i) == 0)
		{
			if(*pA == UNKNOWN_REAL_VALUE || *pB == UNKNOWN_REAL_VALUE)
				d = m_diffWithUnknown;
			else
				d = *pB - *pA;
		}
		else
		{
			if((int)*pA == UNKNOWN_DISCRETE_VALUE || (int)*pB == UNKNOWN_DISCRETE_VALUE)
				d = 1;
			else
				d = ((int)*pB == (int)*pA ? 0 : 1);
		}
		pA++;
		pB++;
		sum += (d * d);
	}
	return sum;
}

// --------------------------------------------------------------------

GRowDistanceScaled::GRowDistanceScaled(GDomNode* pNode)
: GDistanceMetric(pNode)
{
	GDomNode* pScaleFactors = pNode->field("scaleFactors");
	GDomListIterator it(pScaleFactors);
	size_t dims = m_pRelation->size();
	if(it.remaining() != dims)
		ThrowError("wrong number of scale factors");
	m_pScaleFactors = new double[dims];
	for(size_t i = 0; i < dims; i++)
	{
		m_pScaleFactors[i] = it.current()->asDouble();
		it.advance();
	}
}

// virtual
GDomNode* GRowDistanceScaled::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GRowDistance");
	size_t dims = m_pRelation->size();
	GDomNode* pScaleFactors = pNode->addField(pDoc, "scaleFactors", pDoc->newList());
	for(size_t i = 0; i < dims; i++)
		pScaleFactors->addItem(pDoc, pDoc->newDouble(m_pScaleFactors[i]));
	return pNode;
}

// virtual
void GRowDistanceScaled::init(sp_relation& pRelation)
{
	m_pRelation = pRelation;
	delete[] m_pScaleFactors;
	m_pScaleFactors = new double[pRelation->size()];
	GVec::setAll(m_pScaleFactors, 1.0, pRelation->size());
}

// virtual
double GRowDistanceScaled::squaredDistance(const double* pA, const double* pB) const
{
	double sum = 0;
	size_t count = m_pRelation->size();
	double d;
	const double* pSF = m_pScaleFactors;
	for(size_t i = 0; i < count; i++)
	{
		if(m_pRelation->valueCount(i) == 0)
			d = (*pB - *pA) * (*pSF);
		else
			d = ((int)*pB == (int)*pA ? 0 : *pSF);
		pA++;
		pB++;
		pSF++;
		sum += (d * d);
	}
	return sum;
}

// --------------------------------------------------------------------

GLNormDistance::GLNormDistance(double norm)
: GDistanceMetric(), m_norm(norm), m_diffWithUnknown(1.0)
{
}

GLNormDistance::GLNormDistance(GDomNode* pNode)
: GDistanceMetric(pNode), m_norm(pNode->field("norm")->asDouble()), m_diffWithUnknown(pNode->field("dwu")->asDouble())
{
}

// virtual
GDomNode* GLNormDistance::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GLNormDistance");
	pNode->addField(pDoc, "norm", pDoc->newDouble(m_norm));
	pNode->addField(pDoc, "dwu", pDoc->newDouble(m_diffWithUnknown));
	return pNode;
}

// virtual
void GLNormDistance::init(sp_relation& pRelation)
{
	m_pRelation = pRelation;
}

// virtual
double GLNormDistance::squaredDistance(const double* pA, const double* pB) const
{
	GRelation* pRel = m_pRelation.get();
	double sum = 0;
	size_t count = pRel->size();
	double d;
	for(size_t i = 0; i < count; i++)
	{
		if(pRel->valueCount(i) == 0)
		{
			if(*pA == UNKNOWN_REAL_VALUE || *pB == UNKNOWN_REAL_VALUE)
				d = m_diffWithUnknown;
			else
				d = *pB - *pA;
		}
		else
		{
			if((int)*pA == UNKNOWN_DISCRETE_VALUE || (int)*pB == UNKNOWN_DISCRETE_VALUE)
				d = 1;
			else
				d = ((int)*pB == (int)*pA ? 0 : 1);
		}
		pA++;
		pB++;
		sum += pow(d, m_norm);
	}
	d = pow(sum, 1.0 / m_norm);
	return (d * d);
}

// --------------------------------------------------------------------

// static
GSparseSimilarity* GSparseSimilarity::deserialize(GDomNode* pNode)
{
	const char* szClass = pNode->field("class")->asString();
	GSparseSimilarity* pObj = NULL;
	if(strcmp(szClass, "GCosineSimilarity") == 0)
		return new GCosineSimilarity(pNode);
	else if(strcmp(szClass, "GPearsonCorrelation") == 0)
		return new GPearsonCorrelation(pNode);
	else
		ThrowError("Unrecognized class: ", szClass);
	pObj->m_regularizer = pNode->field("reg")->asDouble();
	return pObj;
}

GDomNode* GSparseSimilarity::baseDomNode(GDom* pDoc, const char* szClassName)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "class", pDoc->newString(szClassName));
	pNode->addField(pDoc, "reg", pDoc->newDouble(m_regularizer));
	return pNode;
}

// --------------------------------------------------------------------

// virtual
GDomNode* GCosineSimilarity::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GCosineSimilarity");
	return pNode;
}

// virtual
double GCosineSimilarity::similarity(const map<size_t,double>& a, const map<size_t,double>& b)
{
	map<size_t,double>::const_iterator itA = a.begin();
	map<size_t,double>::const_iterator itB = b.begin();
	if(itA == a.end())
		return 0.0;
	if(itB == b.end())
		return 0.0;
	double sum_sq_a = 0.0;
	double sum_sq_b = 0.0;
	double sum_co_prod = 0.0;
	while(true)
	{
		if(itA->first < itB->first)
		{
			if(++itA == a.end())
				break;
		}
		else if(itB->first < itA->first)
		{
			if(++itB == b.end())
				break;
		}
		else
		{
			sum_sq_a += (itA->second * itA->second);
			sum_sq_b += (itB->second * itB->second);
			sum_co_prod += (itA->second * itB->second);
			if(++itA == a.end())
				break;
			if(++itB == b.end())
				break;
		}
	}
	double denom = sqrt(sum_sq_a * sum_sq_b) + m_regularizer;
	if(denom > 0.0)
		return sum_co_prod / denom;
	else
		return 0.0;
}

// virtual
double GCosineSimilarity::similarity(const map<size_t,double>& a, const double* pB)
{
	map<size_t,double>::const_iterator itA = a.begin();
	if(itA == a.end())
		return 0.0;
	double sum_sq_a = 0.0;
	double sum_sq_b = 0.0;
	double sum_co_prod = 0.0;
	while(itA != a.end())
	{
		sum_sq_a += (itA->second * itA->second);
		sum_sq_b += (pB[itA->first] * pB[itA->first]);
		sum_co_prod += (itA->second * pB[itA->first]);
		itA++;
	}
	double denom = sqrt(sum_sq_a * sum_sq_b) + m_regularizer;
	if(denom > 0.0)
		return sum_co_prod / denom;
	else
		return 0.0;
}

// --------------------------------------------------------------------

// virtual
GDomNode* GPearsonCorrelation::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GPearsonCorrelation");
	return pNode;
}

// virtual
double GPearsonCorrelation::similarity(const map<size_t,double>& a, const map<size_t,double>& b)
{
	// Compute the mean of the overlapping portions
	map<size_t,double>::const_iterator itA = a.begin();
	map<size_t,double>::const_iterator itB = b.begin();
	if(itA == a.end())
		return 0.0;
	if(itB == b.end())
		return 0.0;
	double mean_a = 0.0;
	double mean_b = 0.0;
	size_t count = 0;
	while(true)
	{
		if(itA->first < itB->first)
		{
			if(++itA == a.end())
				break;
		}
		else if(itB->first < itA->first)
		{
			if(++itB == b.end())
				break;
		}
		else
		{
			mean_a += itA->second;
			mean_b += itB->second;
			count++;
			if(++itA == a.end())
				break;
			if(++itB == b.end())
				break;
		}
	}
	double d = count > 0 ? 1.0 / count : 0.0;
	mean_a *= d;
	mean_b *= d;

	// Compute the similarity
	itA = a.begin();
	itB = b.begin();
	double sum = 0.0;
	double sum_of_sq = 0.0;
	while(true)
	{
		if(itA->first < itB->first)
		{
			if(++itA == a.end())
				break;
		}
		else if(itB->first < itA->first)
		{
			if(++itB == b.end())
				break;
		}
		else
		{
			d = (itA->second - mean_a) * (itB->second - mean_b);
			sum += d;
			sum_of_sq += (d * d);
			if(++itA == a.end())
				break;
			if(++itB == b.end())
				break;
		}
	}
	double denom = sqrt(sum_of_sq) + m_regularizer;
	if(denom > 0.0)
		return std::max(-1.0, std::min(1.0, sum / denom));
	else
		return 0.0;
}

// virtual
double GPearsonCorrelation::similarity(const map<size_t,double>& a, const double* pB)
{
	// Compute the mean of the overlapping portions
	map<size_t,double>::const_iterator itA = a.begin();
	double mean_a = 0.0;
	double mean_b = 0.0;
	size_t count = 0;
	while(itA != a.end())
	{
		mean_a += itA->second;
		mean_b += pB[itA->first];
		count++;
		itA++;
	}
	double d = 1.0 / count;
	mean_a *= d;
	mean_b *= d;

	// Compute the similarity
	itA = a.begin();
	double sum = 0.0;
	double sum_of_sq = 0.0;
	while(itA != a.end())
	{
		d = (itA->second - mean_a) * (pB[itA->first] - mean_b);
		sum += d;
		sum_of_sq += (d * d);
		itA++;
	}
	double denom = sqrt(sum_of_sq) + m_regularizer;
	if(denom > 0.0)
		return sum / denom;
	else
		return 0.0;
}

} // namespace GClasses
