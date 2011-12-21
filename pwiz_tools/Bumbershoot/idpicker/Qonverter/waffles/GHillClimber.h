/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GGREEDYSEARCH_H__
#define __GGREEDYSEARCH_H__

#include "GOptimizer.h"
#include "GVec.h"
#include "GRand.h"

namespace GClasses {


/// At each iteration this algorithm moves in only one
/// dimension. If the situation doesn't improve it tries
/// the opposite direction. If both directions are worse,
/// it decreases the step size for that dimension, otherwise
/// it increases the step size for that dimension.
class GMomentumGreedySearch : public GOptimizer
{
protected:
	size_t m_nDimensions;
	size_t m_nCurrentDim;
	double* m_pStepSizes;
	double* m_pVector;
	double m_dError;
	double m_dChangeFactor;

public:
	GMomentumGreedySearch(GTargetFunction* pCritic);
	virtual ~GMomentumGreedySearch();

	/// Returns a pointer to the state vector
	virtual double* currentVector() { return m_pVector; }

	/// Set all the current step sizes to this value
	void setAllStepSizes(double dStepSize);

	/// Returns the vector of step sizes
	double* stepSizes();

	virtual double iterate();

	/// d should be a value between 0 and 1
	void setChangeFactor(double d) { m_dChangeFactor = d; }

protected:
	void reset();
	double iterateOneDim();
};



class GHillClimber : public GOptimizer
{
protected:
	size_t m_nDims;
	double* m_pStepSizes;
	double* m_pVector;
	double* m_pAnnealCand;
	double m_dError;
	double m_dChangeFactor;

public:
	GHillClimber(GTargetFunction* pCritic);
	virtual ~GHillClimber();

	/// Returns a pointer to the current vector
	virtual double* currentVector() { return m_pVector; }

	/// Returns the error for the current vector
	double currentError() { return m_dError; }

	/// Set all the current step sizes to this value
	void setStepSizes(double size);

	/// Returns the vector of step sizes
	double* stepSizes();

	virtual double iterate();

	/// You can call this method to simulate one annealing jump with the
	/// specified deviation in all dimensions.
	double anneal(double dev, GRand* pRand);

	/// d should be a value between 0 and 1
	void setChangeFactor(double d) { m_dChangeFactor = d; }

protected:
	void reset();
};



/// This algorithm tries the current direction and a slightly
/// perturbed direction at each step. If the perturbed direction
/// resulted in faster improvement, it becomes the new current
/// direction. As long as the current direction yields improvement,
/// it accelerates, otherwise it decelerates.
class GAnnealing : public GOptimizer
{
protected:
	double m_initialDeviation;
	double m_deviation;
	double m_decay;
	size_t m_dims;
	double* m_pBuf;
	double* m_pVector;
	double* m_pCandidate;
	double m_dError;
	GRand* m_pRand;

public:
	GAnnealing(GTargetFunction* pTargetFunc, double initialDeviation, double decay, GRand* pRand);
	virtual ~GAnnealing();

	/// Performs a little more optimization. (Call this in a loop until
	/// acceptable results are found.)
	virtual double iterate();

	/// Returns the best vector yet found.
	virtual double* currentVector() { return m_pVector; }

	/// Specify the current deviation to use for annealing. (A random vector
	/// from a Normal distribution with the specified deviation will be added to each
	/// candidate vector in order to simulate annealing.)
	void setDeviation(double d) { m_deviation = d; }

protected:
	void reset();
};




/// This algorithm does a gradient descent by feeling a small distance
/// out in each dimension to measure the gradient. For efficiency reasons,
/// it only measures the gradient in one dimension (which it cycles
/// round-robin style) per iteration and uses the remembered gradient
/// in the other dimensions.
class GEmpiricalGradientDescent : public GOptimizer
{
protected:
	double m_dLearningRate;
	size_t m_nDimensions;
	double* m_pVector;
	double* m_pGradient;
	double* m_pDelta;
	double m_dFeelDistance;
	double m_dMomentum;
	GRand* m_pRand;

public:
	GEmpiricalGradientDescent(GTargetFunction* pCritic, GRand* pRand);
	virtual ~GEmpiricalGradientDescent();

	/// Returns the best vector yet found.
	virtual double* currentVector() { return m_pVector; }

	/// Performs a little more optimization. (Call this in a loop until
	/// acceptable results are found.)
	virtual double iterate();

	/// Sets the learning rate
	void setLearningRate(double d) { m_dLearningRate = d; }

	/// Sets the momentum value
	void setMomentum(double d) { m_dMomentum = d; }
protected:
	void reset();
};



/// This is a variant of empirical gradient descent that tries to estimate
/// the gradient using a minimal number of samples. It is more efficient
/// than empirical gradient descent, but it only works well if the optimization
/// surface is quite locally linear.
class GSampleClimber : public GOptimizer
{
protected:
	GRand* m_pRand;
	double m_dStepSize;
	double m_alpha;
	double m_error;
	size_t m_dims;
	double* m_pVector;
	double* m_pDir;
	double* m_pCand;
	double* m_pGradient;

public:
	GSampleClimber(GTargetFunction* pCritic, GRand* pRand);
	virtual ~GSampleClimber();

	/// Returns the best vector yet found
	virtual double* currentVector() { return m_pVector; }
	
	/// Performs a little more optimization. (Call this in a loop until
	/// acceptable results are found.)
	virtual double iterate();

	/// Sets the current step size
	void setStepSize(double d) { m_dStepSize = d; }

	/// Sets the alpha value. It should be small (like 0.01)
	/// A very small value updates the gradient estimate
	/// slowly, but precisely. A bigger value updates the
	/// estimate quickly, but never converges very close to
	/// the precise gradient.
	void setAlpha(double d) { m_alpha = d; }

protected:
	void reset();
};


} // namespace GClasses

#endif // __GGREEDYSEARCH_H__
