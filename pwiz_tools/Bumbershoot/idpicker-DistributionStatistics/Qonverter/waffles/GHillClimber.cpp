/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GHillClimber.h"
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include "GImage.h"
#include "GBitTable.h"
#include <cmath>

using namespace GClasses;

GMomentumGreedySearch::GMomentumGreedySearch(GTargetFunction* pCritic)
: GOptimizer(pCritic)
{
	if(!pCritic->relation()->areContinuous(0, pCritic->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	if(pCritic->isConstrained())
		ThrowError("Sorry, this optimizer doesn't support constrained problems");
	m_nDimensions = pCritic->relation()->size();
	m_nCurrentDim = 0;
	m_pVector = new double[2 * m_nDimensions];
	m_pStepSizes = m_pVector + m_nDimensions;
	m_dChangeFactor = .87;
	reset();
}

/*virtual*/ GMomentumGreedySearch::~GMomentumGreedySearch()
{
	delete[] m_pVector;
}

void GMomentumGreedySearch::reset()
{
	setAllStepSizes(0.1);
	m_pCritic->initVector(m_pVector);
	if(m_pCritic->isStable())
		m_dError = m_pCritic->computeError(m_pVector);
	else
		m_dError = 1e308;
}

void GMomentumGreedySearch::setAllStepSizes(double dStepSize)
{
	GVec::setAll(m_pStepSizes, dStepSize, m_nDimensions);
}

double* GMomentumGreedySearch::stepSizes()
{
	return m_pStepSizes;
}

double GMomentumGreedySearch::iterateOneDim()
{
	m_pVector[m_nCurrentDim] += m_pStepSizes[m_nCurrentDim];
	double dError = m_pCritic->computeError(m_pVector);
	if(dError >= m_dError)
	{
		m_pVector[m_nCurrentDim] -= m_pStepSizes[m_nCurrentDim];
		m_pVector[m_nCurrentDim] -= m_pStepSizes[m_nCurrentDim];
		dError = m_pCritic->computeError(m_pVector);
		if(dError >= m_dError)
			m_pVector[m_nCurrentDim] += m_pStepSizes[m_nCurrentDim];
	}
	if(dError >= m_dError)
		m_pStepSizes[m_nCurrentDim] *= m_dChangeFactor;
	else
	{
		m_pStepSizes[m_nCurrentDim] /= m_dChangeFactor;
		if(m_pStepSizes[m_nCurrentDim] > 1e12)
			m_pStepSizes[m_nCurrentDim] = 1e12;
		m_dError = dError;
	}
	if(++m_nCurrentDim >= m_nDimensions)
		m_nCurrentDim = 0;
	return m_dError;
}

/*virtual*/ double GMomentumGreedySearch::iterate()
{
	for(size_t i = 1; i < m_nDimensions; i++)
		iterateOneDim();
	return iterateOneDim();
}


// --------------------------------------------------------------------------------


GHillClimber::GHillClimber(GTargetFunction* pCritic)
: GOptimizer(pCritic)
{
	if(!pCritic->relation()->areContinuous(0, pCritic->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	m_nDims = pCritic->relation()->size();
	m_pVector = new double[(2 + (m_pCritic->isConstrained() ? 1 : 0)) * m_nDims];
	m_pStepSizes = m_pVector + m_nDims;
	m_dChangeFactor = .83;
	m_pAnnealCand = NULL;
	reset();
}

/*virtual*/ GHillClimber::~GHillClimber()
{
	delete[] m_pVector;
	delete[] m_pAnnealCand;
}

void GHillClimber::reset()
{
	setStepSizes(0.1);
	m_pCritic->initVector(m_pVector);
	m_pCritic->constrain(m_pVector);
	if(m_pCritic->isStable())
		m_dError = m_pCritic->computeError(m_pVector);
	else
		m_dError = 1e308;
}

void GHillClimber::setStepSizes(double size)
{
	GVec::setAll(m_pStepSizes, size, m_nDims);
}

double* GHillClimber::stepSizes()
{
	return m_pStepSizes;
}

/*virtual*/ double GHillClimber::iterate()
{
	if(m_pCritic->isConstrained())
	{
		double* pTemp = m_pStepSizes + m_nDims;
		double decel, accel, decScore, accScore;
		for(size_t dim = 0; dim < m_nDims; dim++)
		{
			decel = m_pStepSizes[dim] * m_dChangeFactor;
			if(std::abs(decel) < 1e-16)
				decel = 0.1;
			accel = m_pStepSizes[dim] / m_dChangeFactor;
			if(std::abs(accel) > 1e14)
				accel = 0.1;
			if(!m_pCritic->isStable())
				m_dError = m_pCritic->computeError(m_pVector); // Current spot
			GVec::copy(pTemp, m_pVector, m_nDims);
			pTemp[dim] += decel;
			m_pCritic->constrain(pTemp);
			decScore = m_pCritic->computeError(pTemp); // Forward declerated
			GVec::copy(pTemp, m_pVector, m_nDims);
			pTemp[dim] += accel;
			m_pCritic->constrain(pTemp);
			accScore = m_pCritic->computeError(pTemp); // Forward accelerated
			if(m_dError < decScore && m_dError < accScore)
			{
				GVec::copy(pTemp, m_pVector, m_nDims);
				pTemp[dim] -= decel;
				m_pCritic->constrain(pTemp);
				decScore = m_pCritic->computeError(pTemp); // Reverse decelerated
				GVec::copy(pTemp, m_pVector, m_nDims);
				pTemp[dim] -= accel;
				m_pCritic->constrain(pTemp);
				accScore = m_pCritic->computeError(pTemp); // Reverse accelerated
				if(m_dError < decScore && m_dError < accScore)
				{
					// Stay put and decelerate
					m_pStepSizes[dim] = decel;
				}
				else if(decScore < accScore)
				{
					// Reverse and decelerate
					m_pVector[dim] -= decel;
					m_pCritic->constrain(m_pVector);
					m_dError = decScore;
					m_pStepSizes[dim] = -decel;
				}
				else
				{
					// Reverse and accelerate
					m_pVector[dim] -= accel;
					m_pCritic->constrain(m_pVector);
					m_dError = accScore;
					m_pStepSizes[dim] = -accel;
				}
			}
			else if(decScore < accScore)
			{
				// Forward and decelerate
				m_pVector[dim] += decel;
				m_pCritic->constrain(m_pVector);
				m_dError = decScore;
				m_pStepSizes[dim] = decel;
			}
			else if(decScore == accScore)
			{
				if(m_dError == decScore)
				{
					// Neither accelerate nor decelerate. If we're on a temporary plateau
					// on the target function, slowing down would be bad because we might
					// never get off the plateau. If we're at the max error, speeding up
					// would be bad, because we'd just run off to infinity.
					m_pVector[dim] += accel;
					m_pCritic->constrain(m_pVector);
				}
				else
				{
					// Forward and accelerate
					m_dError = accScore;
					m_pStepSizes[dim] = accel;
				}
			}
			else
			{
				// Forward and accelerate
				m_pVector[dim] += accel;
				m_pCritic->constrain(m_pVector);
				m_dError = accScore;
				m_pStepSizes[dim] = accel;
			}
		}
	}
	else
	{
		double decel, accel, decScore, accScore;
		for(size_t dim = 0; dim < m_nDims; dim++)
		{
			decel = m_pStepSizes[dim] * m_dChangeFactor;
			if(std::abs(decel) < 1e-16)
				decel = 0.1;
			accel = m_pStepSizes[dim] / m_dChangeFactor;
			if(std::abs(accel) > 1e14)
				accel = 0.1;
			if(!m_pCritic->isStable())
				m_dError = m_pCritic->computeError(m_pVector); // Current spot
			m_pVector[dim] += decel;
			decScore = m_pCritic->computeError(m_pVector); // Forward decelerated
			m_pVector[dim] -= decel; // undo
			m_pVector[dim] += accel;
			accScore = m_pCritic->computeError(m_pVector); // Forward accelerated
			if(m_dError < decScore && m_dError < accScore)
			{
				m_pVector[dim] -= accel; // undo
				m_pVector[dim] -= decel;
				decScore = m_pCritic->computeError(m_pVector); // Reverse decelerated
				m_pVector[dim] += decel; // undo
				m_pVector[dim] -= accel;
				accScore = m_pCritic->computeError(m_pVector); // Reverse accelerated
				if(m_dError < decScore && m_dError < accScore)
				{
					// Stay put and decelerate
					m_pVector[dim] += accel;
					m_pStepSizes[dim] = decel;
				}
				else if(decScore < accScore)
				{
					// Reverse and decelerate
					m_pVector[dim] += accel;
					m_pVector[dim] -= decel;
					m_dError = decScore;
					m_pStepSizes[dim] = -decel;
				}
				else
				{
					// Reverse and accelerate
					m_dError = accScore;
					m_pStepSizes[dim] = -accel;
				}
			}
			else if(decScore < accScore)
			{
				// Forward and decelerate
				m_pVector[dim] -= accel;
				m_pVector[dim] += decel;
				m_dError = decScore;
				m_pStepSizes[dim] = decel;
			}
			else if(decScore == accScore)
			{
				if(m_dError == decScore)
				{
					// Neither accelerate nor decelerate. If we're on a temporary plateau
					// on the target function, slowing down would be bad because we might
					// never get off the plateau. If we're at the max error, speeding up
					// would be bad, because we'd just run off to infinity. We will, however
					// move at the accelerated rate, just so we're going somewhere.
				}
				else
				{
					// Forward and accelerate
					m_dError = accScore;
					m_pStepSizes[dim] = accel;
				}
			}
			else
			{
				// Forward and accelerate
				m_dError = accScore;
				m_pStepSizes[dim] = accel;
			}
		}
	}

	return m_dError;
}

double GHillClimber::anneal(double dev, GRand* pRand)
{
	if(!m_pCritic->isStable())
		m_dError = m_pCritic->computeError(m_pVector); // Current spot
	if(!m_pAnnealCand)
		m_pAnnealCand = new double[m_nDims];
	for(size_t i = 0; i < m_nDims; i++)
		m_pAnnealCand[i] = m_pVector[i] + pRand->normal() * dev;
	double err = m_pCritic->computeError(m_pAnnealCand);
	if(err < m_dError)
	{
		m_dError = err;
		std::swap(m_pAnnealCand, m_pVector);
	}
	return m_dError;
}


// --------------------------------------------------------------------------------


GAnnealing::GAnnealing(GTargetFunction* pTargetFunc, double initialDeviation, double decay, GRand* pRand)
: GOptimizer(pTargetFunc), m_initialDeviation(initialDeviation), m_decay(decay), m_pRand(pRand)
{
	if(!pTargetFunc->relation()->areContinuous(0, pTargetFunc->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	if(pTargetFunc->isConstrained())
		ThrowError("Sorry, this optimizer doesn't support constrained problems");
	m_dims = pTargetFunc->relation()->size();
	m_pBuf = new double[m_dims * 2];
	m_pVector = m_pBuf;
	m_pCandidate = m_pVector + m_dims;
	reset();
}

/*virtual*/ GAnnealing::~GAnnealing()
{
	delete[] m_pBuf;
}

void GAnnealing::reset()
{
	m_deviation = m_initialDeviation;
	m_pCritic->initVector(m_pVector);
	if(m_pCritic->isStable())
		m_dError = m_pCritic->computeError(m_pVector);
}

/*virtual*/ double GAnnealing::iterate()
{
	if(!m_pCritic->isStable())
		m_dError = m_pCritic->computeError(m_pVector);
	for(size_t i = 0; i < m_dims; i++)
		m_pCandidate[i] = m_pVector[i] + m_pRand->normal() * m_deviation;
	double cand = m_pCritic->computeError(m_pCandidate);
	if(cand < m_dError)
	{
		std::swap(m_pVector, m_pCandidate);
		m_dError = cand;
	}
	m_deviation *= m_decay;
	return m_dError;
}


// --------------------------------------------------------------------------------

GEmpiricalGradientDescent::GEmpiricalGradientDescent(GTargetFunction* pCritic, GRand* pRand)
: GOptimizer(pCritic)
{
	if(!pCritic->relation()->areContinuous(0, pCritic->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	if(pCritic->isConstrained())
		ThrowError("Sorry, this optimizer doesn't support constrained problems");
	m_nDimensions = pCritic->relation()->size();
	m_pVector = new double[m_nDimensions * 3];
	m_pGradient = m_pVector + m_nDimensions;
	m_pDelta = m_pGradient + m_nDimensions;
	m_dFeelDistance = 0.03125;
	m_dMomentum = 0.8;
	m_dLearningRate = 0.1;
	m_pRand = pRand;
	reset();
}

/*virtual*/ GEmpiricalGradientDescent::~GEmpiricalGradientDescent()
{
	delete[] m_pVector;
}

void GEmpiricalGradientDescent::reset()
{
	m_pCritic->initVector(m_pVector);
	GVec::setAll(m_pDelta, 0.0, m_nDimensions);
}

/*virtual*/ double GEmpiricalGradientDescent::iterate()
{
	// Feel the gradient in each dimension using one random pattern
	size_t nPattern = (size_t)m_pRand->next();
	double dCurrentError = m_pCritic->computeErrorOnline(m_pVector, nPattern);
	double d = m_dFeelDistance * m_dLearningRate;
	for(size_t i = 0; i < m_nDimensions; i++)
	{
		m_pVector[i] += d;
		m_pGradient[i] = (m_pCritic->computeErrorOnline(m_pVector, nPattern) - dCurrentError) / d;
		m_pVector[i] -= d;
		m_pDelta[i] = m_dMomentum * m_pDelta[i] - m_dLearningRate * m_pGradient[i];
		m_pVector[i] += m_pDelta[i];
	}
	return dCurrentError;
}

// --------------------------------------------------------------------------------

GSampleClimber::GSampleClimber(GTargetFunction* pCritic, GRand* pRand)
: GOptimizer(pCritic), m_pRand(pRand)
{
	if(!pCritic->relation()->areContinuous(0, pCritic->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	if(pCritic->isConstrained())
		ThrowError("Sorry, this optimizer doesn't support constrained problems");
	m_dims = pCritic->relation()->size();
	m_pVector = new double[m_dims * 4];
	m_pDir = m_pVector + m_dims;
	m_pCand = m_pDir + m_dims;
	m_pGradient = m_pCand + m_dims;
	m_dStepSize = 0.1;
	m_alpha = 0.01;
	reset();
}

// virtual
GSampleClimber::~GSampleClimber()
{
	delete[] m_pVector;
}

void GSampleClimber::reset()
{
	m_dStepSize = .1;
	m_pCritic->initVector(m_pVector);
	m_error = m_pCritic->computeError(m_pVector);
}

// virtual
double GSampleClimber::iterate()
{
	// Improve our moving gradient estimate with a new sample
	m_pRand->spherical(m_pDir, m_dims);
	GVec::copy(m_pCand, m_pVector, m_dims);
	GVec::addScaled(m_pCand, m_dStepSize * 0.015625, m_pDir, m_dims);
	GVec::add(m_pCand, m_pVector, m_dims);
	double w;
	double err = m_pCritic->computeError(m_pCand);
	for(size_t i = 0; i < m_dims; i++)
	{
		w = m_alpha * m_pDir[i] * m_pDir[i];
		m_pGradient[i] *= (1.0 - w);
		m_pGradient[i] += w * (err - m_error) / m_pDir[i];
	}
	GVec::copy(m_pDir, m_pGradient, m_dims);
	GVec::safeNormalize(m_pDir, m_dims, m_pRand);

	// Step
	GVec::addScaled(m_pVector, m_dStepSize, m_pDir, m_dims);
	err = m_pCritic->computeError(m_pVector);
	if(m_error < err)
	{
		// back up and slow down
		GVec::addScaled(m_pVector, -m_dStepSize, m_pDir, m_dims);
		m_dStepSize *= 0.87;
	}
	else
	{
		m_error = err;
		m_dStepSize *= 1.15;
	}
	return m_error;
}
