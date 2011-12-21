/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GEVOLUTIONARY_H__
#define __GEVOLUTIONARY_H__

#include "GOptimizer.h"
#include <vector>

namespace GClasses {

class GRand;
class GEvolutionaryOptimizerNode;
class GDiscreteEvolutionaryOptimizerNode;

/// Uses an evolutionary process to optimize a vector.
class GEvolutionaryOptimizer : public GOptimizer
{
protected:
	double m_tournamentProbability;
	GRand* m_pRand;
	std::vector<GEvolutionaryOptimizerNode*> m_population;
	double m_bestErr;
	size_t m_bestIndex;

public:
	/// moreFitSurvivalRate is the probability that the more fit member (in a tournament selection) survives
	GEvolutionaryOptimizer(GTargetFunction* pCritic, size_t population, GRand* pRand, double moreFitSurvivalRate);
	virtual ~GEvolutionaryOptimizer();

	/// Returns the best vector found in recent iterations.
	virtual double* currentVector();

	/// Do a little bit more optimization. (This method is typically called in a loop
	/// until satisfactory results are obtained.)
	virtual double iterate();

protected:
	/// Returns the index of the tournament loser (who should typically die and be replaced).
	size_t doTournament();

	void recomputeError(size_t index, GEvolutionaryOptimizerNode* pNode, const double* pVec);

	GEvolutionaryOptimizerNode* node(size_t index);
};


} // namespace GClasses

#endif // __GEVOLUTIONARY_H__
