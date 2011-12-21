/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GSEARCH_H__
#define __GSEARCH_H__

#include "GError.h"
#include "GMatrix.h"
#include <vector>

namespace GClasses {

class GActionPath;
class GAction;
class GRand;


/// The optimizer seeks to find values that minimize this target function.
class GTargetFunction
{
protected:
	sp_relation m_pRelation;

public:
	GTargetFunction(sp_relation& pRelation) : m_pRelation(pRelation) {}
	GTargetFunction(size_t dims);
	virtual ~GTargetFunction() {}

	/// Returns a (smart) pointer to the relation, which specifies the type
	/// (discrete or real) of each element in the vector that is being optimized.
	sp_relation& relation() { return m_pRelation; }

	/// Return true if computeError is completely deterministic with respect to
	/// the vector being optimized. Return false if the error also depends on some
	/// state other than the vector being optimized. This mostly affects whether
	/// the optimization algorithms are permitted to remember old error values for
	/// efficiency purposes.
	virtual bool isStable() = 0;

	/// Return true if this function is constrained to only support certain vectors.
	virtual bool isConstrained() = 0;

	/// Sets pVector to an initial guess
	virtual void initVector(double* pVector) = 0;

	/// Computes the error of the given vector using all patterns
	virtual double computeError(const double* pVector) = 0;

	/// Estimates the error of the given vector using a single (usually randomly selected) pattern
	virtual double computeErrorOnline(const double* pVector, size_t nPattern)
	{
		ThrowError("This critic doesn't support online evaluation");
		return 0.0;
	}

	/// Adjust pVector to the nearest vector that fits the constraints
	virtual void constrain(double* pVector)
	{
	}
};



/// This is the base class of all search algorithms
/// that can jump to any vector in the search space
/// seek the vector that minimizes error.
class GOptimizer
{
protected:
	GTargetFunction* m_pCritic;

public:
	GOptimizer(GTargetFunction* pCritic);
	virtual ~GOptimizer();

	/// Makes another attempt to find a better vector. Returns
	/// the heuristic error. (Usually you will call this method
	/// in a loop until your stopping criteria has been met.)
	virtual double iterate() = 0;

	/// Returns the current vector of the optimizer. In most cases,
	/// this is the best vector yet found.
	virtual double* currentVector() = 0;

	/// This will first call iterate() nBurnInIterations times,
	/// then it will repeatedly call iterate() in blocks of
	/// nIterations times. If the error heuristic has not improved
	/// by the specified ratio after a block of iterations, it will
	/// stop. (For example, if the error before the block of iterations
	/// was 50, and the error after is 49, then training will stop
	/// if dImprovement is > 0.02.) If the error heuristic is not
	/// stable, then the value of nIterations should be large.
	double searchUntil(size_t nBurnInIterations, size_t nIterations, double dImprovement);
};



/// This class simplifies simultaneously solving several optimization problems
class GParallelOptimizers
{
protected:
	sp_relation m_pRelation;
	std::vector<GTargetFunction*> m_targetFunctions;
	std::vector<GOptimizer*> m_optimizers;

public:
	/// If the problems all have the same number of dims, and they're all continuous, you can call
	/// relation() to get a relation for constructing the target functions. Otherwise, use dims=0
	/// and don't call relation().
	GParallelOptimizers(size_t dims);
	~GParallelOptimizers();

	/// Returns the relation associated with these optimizers
	sp_relation& relation() { return m_pRelation; }

	/// Takes ownership of pTargetFunction and pOptimizer
	void add(GTargetFunction* pTargetFunction, GOptimizer* pOptimizer);

	/// Returns a vector of pointers to the optimizers
	std::vector<GOptimizer*>& optimizers() { return m_optimizers; }

	/// Returns a vector of pointers to the target functions
	std::vector<GTargetFunction*>& targetFunctions() { return m_targetFunctions; }

	/// Perform one iteration on all of the optimizers
	double iterateAll();

	/// Optimize until the specified conditions are met
	double searchUntil(size_t nBurnInIterations, size_t nIterations, double dImprovement);
};



class GActionPathState
{
friend class GActionPath;
public:
	GActionPathState() {}
	virtual ~GActionPathState() {}

protected:
	/// Performs the specified action on the state. (so pState holds
	/// both input and output data.) This method is protected because
	/// you should call GActionPath::doAction, and it will call this method.
	virtual void performAction(size_t nAction) = 0;

	/// Creates a deep copy of this state object
	virtual GActionPathState* copy() = 0;

protected:
	/// Evaluate the error of the given path. Many search algorithms
	/// (like GAStarSearch) rely heavily on the heuristic to make the search effective.
	/// For example, if you don't penalize redundant paths to the same state, the search
	/// space becomes exponential and therefore impossible to search. So a good critic
	/// must keep track of which states have already been visited, severely penalize longer
	/// paths to a state that has already been visited by a shorter path, and will carefully
	/// balance between path length and distance from the goal in producing the error value.
	virtual double critiquePath(size_t nPathLen, GAction* pLastAction) = 0;
};




class GActionPath
{
protected:
	GActionPathState* m_pHeadState;
	GAction* m_pLastAction;
	size_t m_nPathLen;

public:
	/// Takes ownership of pState
	GActionPath(GActionPathState* pState);
	~GActionPath();

	/// Makes a copy of this path
	GActionPath* fork();

	/// Returns the number of actions in the path
	size_t length() { return m_nPathLen; }

	/// Gets the first nCount actions of the specified path
	void path(size_t nCount, size_t* pOutBuf);

	/// Returns the head-state of the path
	GActionPathState* state() { return m_pHeadState; }

	/// Adds the specified action to the path and modifies the head state accordingly
	void doAction(size_t nAction);

	/// Computes the error of this path
	double critique();
};




/// This is the base class of search algorithms that can
/// only perform a discreet set of actions (as opposed to jumping
/// to anywhere in the search space), and seeks to minimize the
/// error of a path of actions
class GActionPathSearch
{
protected:
	size_t m_nActionCount;

public:
	/// Takes ownership of pStartState
	GActionPathSearch(GActionPathState* pStartState, size_t nActionCount)
	{
		GAssert(nActionCount > 1); // not enough actions for a meaningful search")
		m_nActionCount = nActionCount;
	}

	virtual ~GActionPathSearch()
	{
	}

	/// Returns the number of possible actions
	inline size_t actionCount() { return m_nActionCount; }

	/// Call this in a loop to do the searching. If it returns
	/// true, then it's done so don't call it anymore.
	virtual bool iterate() = 0;

	/// Returns the best known path so far
	virtual GActionPath* bestPath() = 0;

	/// Returns the error of the best known path
	virtual double bestPathError() = 0;
};


} // namespace GClasses

#endif // __GSEARCH_H__
