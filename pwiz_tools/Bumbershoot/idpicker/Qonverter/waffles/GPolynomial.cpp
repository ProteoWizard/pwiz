/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GError.h"
#include "GPolynomial.h"
#include "GMatrix.h"
#include "GVec.h"
#include <math.h>
#include <stdlib.h>
#include "GDistribution.h"
#include "GMath.h"
#include "GHillClimber.h"
#include "GDom.h"

using std::vector;

namespace GClasses {

/// This is an internal helper-class used by GPolynomial
class GPolynomialSingleLabel
{
protected:
	size_t m_featureDims;
	size_t m_nControlPoints;
	size_t m_nCoefficients;
	double* m_pCoefficients;

public:
	/// It will have the same number of control points in every dimension
	GPolynomialSingleLabel(size_t nControlPoints);

	/// Load from a DOM.
	GPolynomialSingleLabel(GDomNode* pNode, GRand& rand);

	virtual ~GPolynomialSingleLabel();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // NO_TEST_CODE

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	GDomNode* serialize(GDom* pDoc);

	/// Specify the number of input and output features
	void init(size_t featureDims);

	/// Returns the number of feature dims
	size_t featureDims() { return m_featureDims; }

	/// Returns the total number of coefficients in this polynomial
	size_t coefficientCount() { return m_nCoefficients; }

	/// sets the number of control points
	void setControlPointCount(size_t n) { m_nControlPoints = n; }

	/// Returns the number of control points (per dimension)
	size_t controlPointCount() { return m_nControlPoints; }

	/// Returns the coefficient at the specified coordinates. pCoords should
	/// be an array of size m_nDimensions, and each value should be from 0 to m_nControlPoints - 1
	double coefficient(size_t* pCoords);

	/// Returns the full array of coefficients
	double* coefficientArray() { return m_pCoefficients; }

	/// Sets the coefficient at the specified coordinates. pCoords should
	/// be an array of size m_nDimensions, and each value should be from 0 to m_nControlPoints - 1
	void setCoefficient(size_t* pCoords, double dVal);

	/// Copies pOther into this polynomial. Both polynomials must have the
	/// same dimensionality, and this polynomial must have >= 
	void copy(GPolynomialSingleLabel* pOther);

	/// See the comment for GSupervisedLearner::clear
	void clear();

	/// Sets all the coefficients. pVector must be of size GetCoefficientCount()
	void setCoefficients(const double* pVector);

	/// Converts to a multi-dimensional Bezier curve
	void toBezierCoefficients();

	/// Converts from a multi-dimensional Bezier curve
	void fromBezierCoefficients();

	/// Differentiates the polynomial with respect to every dimension
	void differentiate();

	/// Integrates the polynomial in every dimension. This assumes the
	/// constant of integration is always zero. It also assumes that all
	/// of the highest-order coefficients are zero. If that isn't true,
	/// this polynomial won't be big enough to hold the answer, and the
	/// highest-order coefficients will be dropped. The best way to ensure
	/// that doesn't happen is to copy into a bigger (one more control point)
	/// polynomial before integrating.
	void integrate();

	void train(GMatrix& features, GMatrix& labels);

	double predict(const double* pIn);

protected:
	/// This converts from control-point-lattice coordinates to an array index.
	/// The array is stored with lattice position (0, 0, 0, ...) (the constant coefficient)
	/// in array position 0, and lattice position (1, 0, 0, ...) in position 1, etc.
	size_t calcIndex(size_t* pDegrees);
};


class GPolynomialLatticeIterator
{
protected:
	size_t* m_pCoords;
	size_t m_nDimensions;
	size_t m_nControlPoints;
	size_t m_nSkipDimension;

public:
	GPolynomialLatticeIterator(size_t* pCoords, size_t nDimensions, size_t nControlPoints, size_t nSkipDimension)
	{
		m_pCoords = pCoords;
		m_nDimensions = nDimensions;
		m_nControlPoints = nControlPoints;
		m_nSkipDimension = nSkipDimension;
		for(size_t i = 0; i < nDimensions; i++)
		{
			if(i == nSkipDimension)
				continue;
			m_pCoords[i] = nControlPoints - 1;
		}
	}

	~GPolynomialLatticeIterator()
	{
	}

	bool Advance()
	{
		// Move on to the next point in the lattice
		size_t i = 0;
		if(i == m_nSkipDimension)
			i++;
		while(true)
		{
			if(m_pCoords[i]-- == 0)
			{
				m_pCoords[i] = m_nControlPoints - 1;
				if(++i == m_nSkipDimension)
					++i;
				if(i < m_nDimensions)
					continue;
				else
					return false;
			}
			return true;
		}
	}
};

// ---------------------------------------------------------------------------

GPolynomialSingleLabel::GPolynomialSingleLabel(size_t controlPoints)
: m_featureDims(0), m_nControlPoints(controlPoints), m_nCoefficients(0), m_pCoefficients(NULL)
{
	GAssert(controlPoints > 0);
}

GPolynomialSingleLabel::GPolynomialSingleLabel(GDomNode* pNode, GRand& rand)
{
	m_nControlPoints = (int)pNode->field("controlPoints")->asInt();
	m_nCoefficients = 1;
	m_featureDims = (int)pNode->field("featureDims")->asInt();
	size_t i = m_featureDims;
	while(i > 0)
	{
		m_nCoefficients *= m_nControlPoints;
		i--;
	}
	m_pCoefficients = new double[m_nCoefficients];
	GDomListIterator it(pNode->field("coefficients"));
	GVec::deserialize(m_pCoefficients, it);
}

GPolynomialSingleLabel::~GPolynomialSingleLabel()
{
	clear();
}

// virtual
GDomNode* GPolynomialSingleLabel::serialize(GDom* pDoc)
{
	if(m_featureDims == 0)
		ThrowError("train has not been called");
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "featureDims", pDoc->newInt(m_featureDims));
	pNode->addField(pDoc, "controlPoints", pDoc->newInt(m_nControlPoints));
	pNode->addField(pDoc, "coefficients", GVec::serialize(pDoc, m_pCoefficients, m_nCoefficients));
	return pNode;
}

size_t GPolynomialSingleLabel::calcIndex(size_t* pCoords)
{
	size_t nIndex = 0;
	for(size_t n = m_featureDims - 1; n < m_featureDims; n--)
	{
		nIndex *= m_nControlPoints;
		GAssert(pCoords[n] >= 0 && pCoords[n] < m_nControlPoints); // out of range
		nIndex += pCoords[n];
	}
	return nIndex;
}

double GPolynomialSingleLabel::coefficient(size_t* pCoords)
{
	if(m_featureDims == 0)
		ThrowError("init has not been called");
	return m_pCoefficients[calcIndex(pCoords)];
}

void GPolynomialSingleLabel::setCoefficient(size_t* pCoords, double dVal)
{
	if(m_featureDims == 0)
		ThrowError("init has not been called");
	m_pCoefficients[calcIndex(pCoords)] = dVal;
}

class GPolynomialRegressCritic : public GTargetFunction
{
protected:
	GPolynomialSingleLabel* m_pPolynomial;
	GMatrix& m_features;
	GMatrix& m_labels;

public:
	GPolynomialRegressCritic(GPolynomialSingleLabel* pPolynomial, GMatrix& features, GMatrix& labels)
	: GTargetFunction(pPolynomial->coefficientCount()), m_features(features), m_labels(labels)
	{
		m_pPolynomial = pPolynomial;
	}

	virtual ~GPolynomialRegressCritic()
	{
	}

	virtual bool isStable() { return true; }
	virtual bool isConstrained() { return false; }

protected:
	virtual void initVector(double* pVector)
	{
		GVec::setAll(pVector, 0.0, relation()->size());
	}

	virtual double computeError(const double* pVector)
	{
		m_pPolynomial->setCoefficients(pVector);
		m_pPolynomial->fromBezierCoefficients();
		double d;
		double dSumSquaredError = 0;
		for(size_t i = 0; i < m_features.rows(); i++)
		{
			double* pVec = m_features[i];
			double prediction = m_pPolynomial->predict(pVec);
			d = m_labels[i][0] - prediction;
			dSumSquaredError += (d * d);
		}
		return dSumSquaredError / m_features.rows();
	}
};

void GPolynomialSingleLabel::setCoefficients(const double* pVector)
{
	if(m_featureDims == 0)
		ThrowError("init has not been called");
	GVec::copy(m_pCoefficients, pVector, m_nCoefficients);
}

void GPolynomialSingleLabel::clear()
{
	delete[] m_pCoefficients;
	m_pCoefficients = NULL;
}

void GPolynomialSingleLabel::init(size_t featureDims)
{
	m_featureDims = featureDims;
	m_nCoefficients = 1;
	size_t i = m_featureDims;
	while(i > 0)
	{
		m_nCoefficients *= m_nControlPoints;
		i--;
	}
	m_pCoefficients = new double[m_nCoefficients];
	GVec::setAll(m_pCoefficients, 0.0, m_nCoefficients);
}

void GPolynomialSingleLabel::train(GMatrix& features, GMatrix& labels)
{
	GAssert(labels.cols() == 1);
	init(features.cols());
	GPolynomialRegressCritic critic(this, features, labels);
	//GStochasticGreedySearch search(&critic);
	GMomentumGreedySearch search(&critic);
	search.searchUntil(100, 30, .01);
	setCoefficients(search.currentVector());
	fromBezierCoefficients();
}

// (Warning: this method relies on the order in which GPolynomialLatticeIterator
// visits coefficients in the lattice)
double GPolynomialSingleLabel::predict(const double* pIn)
{
	if(m_featureDims == 0)
		ThrowError("init has not been called");
	GTEMPBUF(size_t, pCoords, m_featureDims);
	GPolynomialLatticeIterator iter(pCoords, m_featureDims, m_nControlPoints, (size_t)-1);
	double dSum = 0;
	double dVar;
	for(size_t nCoeff = m_nCoefficients - 1; nCoeff < m_nCoefficients; nCoeff--)
	{
		dVar = 1;
		for(size_t n = 0; n < m_featureDims; n++)
		{
			for(size_t i = pCoords[n]; i > 0; i--)
				dVar *= pIn[n];
		}
		dVar *= m_pCoefficients[nCoeff];
		dSum += dVar;
		iter.Advance();
	}
	return dSum;
}

void GPolynomialSingleLabel::toBezierCoefficients()
{
	if(m_featureDims == 0)
		ThrowError("init has not been called");
	// Make Pascal's triangle
	GTEMPBUF(size_t, pCoords, m_featureDims);
	GTEMPBUF(size_t, pPascalsTriangle, m_nControlPoints);

	// In each dimensional direction...
	GMath::pascalsTriangle(pPascalsTriangle, m_nControlPoints - 1);
	for(size_t n = 0; n < m_featureDims; n++)
	{
		// Across that dimension...
		for(size_t j = 0; j < m_nControlPoints; j++)
		{
			// Iterate over the entire lattice of coefficients (except in dimension n)
			GPolynomialLatticeIterator iter(pCoords, m_featureDims, m_nControlPoints, n);
			while(true)
			{
				// Divide by the corresponding row of Pascal's triangle
				pCoords[n] = j;
				m_pCoefficients[calcIndex(pCoords)] /= pPascalsTriangle[j];
				if(!iter.Advance())
					break;
			}
		}
	}

	// Forward sum the coefficients
	double d;
	for(size_t i = m_nControlPoints - 1; i >= 1; i--)
	{
		// In each dimensional direction...
		for(size_t n = 0; n < m_featureDims; n++)
		{
			// Subtract the neighbor of lesser-significance from each coefficient
			for(size_t j = i; j < m_nControlPoints; j++)
			{
				// Iterate over the entire lattice of coefficients (except in dimension n)
				GPolynomialLatticeIterator iter(pCoords, m_featureDims, m_nControlPoints, n);
				while(true)
				{
					// Subtract the neighbor of lesser-significance from this coefficient
					pCoords[n] = j - 1;
					d = m_pCoefficients[calcIndex(pCoords)];
					pCoords[n] = j;
					m_pCoefficients[calcIndex(pCoords)] += d;
					if(!iter.Advance())
						break;
				}
			}
		}
	}
}

void GPolynomialSingleLabel::fromBezierCoefficients()
{
	if(m_featureDims == 0)
		ThrowError("init has not been called");
	// Forward difference the coefficients
	GTEMPBUF(size_t, pCoords, m_featureDims);
	GTEMPBUF(size_t, pPascalsTriangle, m_nControlPoints);
	double d;
	for(size_t i = 1; i < m_nControlPoints; i++)
	{
		// In each dimensional direction...
		for(size_t n = 0; n < m_featureDims; n++)
		{
			// Subtract the neighbor of lesser-significance from each coefficient
			for(size_t j = m_nControlPoints - 1; j >= i; j--)
			{
				// Iterate over the entire lattice of coefficients (except in dimension n)
				GPolynomialLatticeIterator iter(pCoords, m_featureDims, m_nControlPoints, n);
				while(true)
				{
					// Subtract the neighbor of lesser-significance from this coefficient
					pCoords[n] = j - 1;
					d = m_pCoefficients[calcIndex(pCoords)];
					pCoords[n] = j;
					m_pCoefficients[calcIndex(pCoords)] -= d;
					if(!iter.Advance())
						break;
				}
			}
		}
	}

	// In each dimensional direction...
	GMath::pascalsTriangle(pPascalsTriangle, m_nControlPoints - 1);
	for(size_t n = 0; n < m_featureDims; n++)
	{
		// Across that dimension...
		for(size_t j = 0; j < m_nControlPoints; j++)
		{
			// Iterate over the entire lattice of coefficients (except in dimension n)
			GPolynomialLatticeIterator iter(pCoords, m_featureDims, m_nControlPoints, n);
			while(true)
			{
				// Multiply by the corresponding row of Pascal's triangle
				pCoords[n] = j;
				m_pCoefficients[calcIndex(pCoords)] *= pPascalsTriangle[j];
				if(!iter.Advance())
					break;
			}
		}
	}
}

void GPolynomialSingleLabel::differentiate()
{
	if(m_featureDims == 0)
		ThrowError("init has not been called");
	GTEMPBUF(size_t, pCoords, m_featureDims);
	double d;
	for(size_t n = 0; n < m_featureDims; n++)
	{
		// Iterate over the entire lattice of coefficients (except in dimension n)
		GPolynomialLatticeIterator iter(pCoords, m_featureDims, m_nControlPoints, n);
		while(true)
		{
			// Differentiate with respect to the n'th dimension
			for(size_t j = 1; j < m_nControlPoints; j++)
			{
				pCoords[n] = j;
				d = m_pCoefficients[calcIndex(pCoords)];
				pCoords[n] = j - 1;
				m_pCoefficients[calcIndex(pCoords)] = d * j;
			}
			pCoords[n] = m_nControlPoints - 1;
			m_pCoefficients[calcIndex(pCoords)] = 0;
			if(!iter.Advance())
				break;
		}
	}
}

void GPolynomialSingleLabel::integrate()
{
	if(m_featureDims == 0)
		ThrowError("init has not been called");
	GTEMPBUF(size_t, pCoords, m_featureDims);
	double d;
	for(size_t n = 0; n < m_featureDims; n++)
	{
		// Iterate over the entire lattice of coefficients (except in dimension n)
		GPolynomialLatticeIterator iter(pCoords, m_featureDims, m_nControlPoints, n);
		while(true)
		{
			// Integrate in the n'th dimension
			pCoords[n] = 0;
			m_pCoefficients[calcIndex(pCoords)] = 0;
			for(size_t j = m_nControlPoints - 1; j > 0; j--)
			{
				pCoords[n] = j - 1;
				d = m_pCoefficients[calcIndex(pCoords)];
				pCoords[n] = j;
				size_t index = calcIndex(pCoords);
				GAssert(j < m_nControlPoints - 1 || m_pCoefficients[index] == 0); // There's a non-zero value in a highest-order coefficient. This polynomial, therefore, isn't big enough to hold the integral
				m_pCoefficients[index] = d / j;
			}
			if(!iter.Advance())
				break;
		}
	}
}

void GPolynomialSingleLabel::copy(GPolynomialSingleLabel* pOther)
{
	m_featureDims = pOther->m_featureDims;
	if(controlPointCount() >= pOther->controlPointCount())
		ThrowError("this polynomial must have at least as many control points as pOther");
	if(controlPointCount() > pOther->controlPointCount())
		GVec::setAll(m_pCoefficients, 0.0, m_nCoefficients);
	GTEMPBUF(size_t, pCoords, m_featureDims);
	GPolynomialLatticeIterator iter(pCoords, m_featureDims, pOther->m_nControlPoints, (size_t)-1);
	while(true)
	{
		m_pCoefficients[calcIndex(pCoords)] = pOther->m_pCoefficients[pOther->calcIndex(pCoords)];
		if(!iter.Advance())
			break;
	}
}

#ifndef NO_TEST_CODE
// static
void GPolynomialSingleLabel::test()
{
	// This test involves a two-dimensional polynomial with three controll points in each dimension.
	// In other words, there is a 3x3 lattice of control points, so there are 9 total control points.
	// In this case, we arbitrarily use {1,2,3,4,5,6,7,8,9} for the coefficients.
	GPolynomialSingleLabel gp(3);
	gp.init(2);
	size_t degrees[2];
	degrees[0] = 0;
	degrees[1] = 0;
	gp.setCoefficient(degrees, 1);
	degrees[0] = 1;
	degrees[1] = 0;
	gp.setCoefficient(degrees, 2);
	degrees[0] = 2;
	degrees[1] = 0;
	gp.setCoefficient(degrees, 3);
	degrees[0] = 0;
	degrees[1] = 1;
	gp.setCoefficient(degrees, 4);
	degrees[0] = 1;
	degrees[1] = 1;
	gp.setCoefficient(degrees, 5);
	degrees[0] = 2;
	degrees[1] = 1;
	gp.setCoefficient(degrees, 6);
	degrees[0] = 0;
	degrees[1] = 2;
	gp.setCoefficient(degrees, 7);
	degrees[0] = 1;
	degrees[1] = 2;
	gp.setCoefficient(degrees, 8);
	degrees[0] = 2;
	degrees[1] = 2;
	gp.setCoefficient(degrees, 9);
	double vars[2];
	vars[0] = 7;
	vars[1] = 11;
	double prediction = gp.predict(vars);
	// 1 + 2 * (7) + 3 * (7 * 7) +
	// 4 * (11) + 5 * (11 * 7) + 6 * (11 * 7 * 7) +
	// 7 * (11 * 11) + 8 * (11 * 11 * 7) + 9 * (11 * 11 * 7 * 7)
	// = 64809
	if(prediction != 64809)
		throw "wrong answer";
}
#endif // NO_TEST_CODE


















GPolynomial::GPolynomial(GRand& rand)
: GSupervisedLearner(rand), m_controlPoints(3)
{
}

GPolynomial::GPolynomial(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_controlPoints = (size_t)pNode->field("controlPoints")->asInt();
	GDomNode* pPolys = pNode->field("polys");
	for(GDomListIterator it(pPolys); it.current(); it.advance())
		m_polys.push_back(new GPolynomialSingleLabel(it.current(), ll.rand()));
}

// virtual
GPolynomial::~GPolynomial()
{
	clear();
}

// virtual
GDomNode* GPolynomial::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GPolynomial");
	pNode->addField(pDoc, "controlPoints", pDoc->newInt(m_controlPoints));
	GDomNode* pPolys = pNode->addField(pDoc, "polys", pDoc->newList());
	for(size_t i = 0; i < m_polys.size(); i++)
		pPolys->addItem(pDoc, m_polys[i]->serialize(pDoc));
	return pNode;
}

void GPolynomial::setControlPoints(size_t n)
{
	m_controlPoints = n;
}

size_t GPolynomial::controlPoints()
{
	return m_controlPoints;
}

// virtual
void GPolynomial::clear()
{
	for(vector<GPolynomialSingleLabel*>::iterator it = m_polys.begin(); it != m_polys.end(); it++)
		delete(*it);
	m_polys.clear();
}

// virtual
void GPolynomial::trainInner(GMatrix& features, GMatrix& labels)
{
	GMatrix labelCol(labels.rows(), 1);
	clear();
	for(size_t i = 0; i < labels.cols(); i++)
	{
		GPolynomialSingleLabel* pPSL = new GPolynomialSingleLabel(m_controlPoints);
		m_polys.push_back(pPSL);
		labelCol.copyColumns(0, &labels, i, 1);
		pPSL->train(features, labelCol);
	}
}

// virtual
void GPolynomial::predictInner(const double* pIn, double* pOut)
{
	for(size_t i = 0; i < m_polys.size(); i++)
		pOut[i] = m_polys[i]->predict(pIn);
}

// virtual
void GPolynomial::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	ThrowError("Sorry, this model cannot predict a distribution");
}

void GPolynomial::autoTune(GMatrix& features, GMatrix& labels)
{
	// Find the best value for controlPoints
	size_t bestCP = 3;
	double bestErr = 1e308;
	for(size_t i = 3; i < 7; i++)
	{
		m_controlPoints = i;
		double d = heuristicValidate(features, labels);
		if(d < bestErr)
		{
			bestErr = d;
			bestCP = i;
		}
		else
			break;
	}

	// Set the best values
	m_controlPoints = bestCP;
}

#ifndef NO_TEST_CODE
// static
void GPolynomial::test()
{
	GPolynomialSingleLabel::test();
	GRand prng(0);
	GPolynomial poly(prng);
	poly.basicTest(0.78, -1.0/*skip it*/);
}
#endif // NO_TEST_CODE


} // namespace GClasses

