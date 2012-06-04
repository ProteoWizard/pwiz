/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GReinforcement.h"
#include "GVec.h"
#include "GNeuralNet.h"
#include "GKNN.h"
#include "GRand.h"

namespace GClasses {

GQLearner::GQLearner(sp_relation& pRelation, int actionDims, double* pInitialState, GRand* pRand, GAgentActionIterator* pActionIterator)
: GPolicyLearner(pRelation, actionDims), m_pRand(pRand), m_pActionIterator(pActionIterator)
{
	m_learningRate = 1;
	m_discountFactor = 0.98;
	m_pSenses = new double[m_senseDims + m_actionDims];
	m_pAction = m_pSenses + m_senseDims;
	GVec::copy(m_pSenses, pInitialState, m_senseDims);
	m_actionCap = 50;
}

// virtual
GQLearner::~GQLearner()
{
	delete[] m_pSenses;
}

void GQLearner::setLearningRate(double d)
{
	m_learningRate = d;
}

// Sets the factor for discounting future rewards.
void GQLearner::setDiscountFactor(double d)
{
	m_discountFactor = d;
}

// virtual
void GQLearner::refinePolicyAndChooseNextAction(const double* pSenses, double* pOutActions)
{
	double reward;
	if(m_teleported)
		reward = UNKNOWN_REAL_VALUE;
	else
		reward = rewardFromLastAction();
	if(reward != UNKNOWN_REAL_VALUE)
	{
		// Find the best next action
		double maxQ = 0;
		double q;
		m_pActionIterator->reset(pSenses);
		int i;
		for(i = 0; i < m_actionCap; i++)
		{
			if(!m_pActionIterator->nextAction(pOutActions))
				break;
			q = getQValue(pSenses, pOutActions);
			if(q > maxQ)
				maxQ = q;
		}

		// Update the Q-values
		q = reward + m_discountFactor * maxQ;
		setQValue(m_pSenses, m_pAction, (1.0 - m_learningRate) * getQValue(m_pSenses, m_pAction) + m_learningRate * q);
	}

	// Decide what to do next
	GVec::copy(m_pSenses, pSenses, m_senseDims);
	chooseAction(pSenses, pOutActions);
	GVec::copy(m_pAction, pOutActions, m_actionDims);
	m_teleported = false;
}

// -----------------------------------------------------------------

GIncrementalLearnerQAgent::GIncrementalLearnerQAgent(sp_relation& pObsControlRelation, GIncrementalLearner* pQTable, int actionDims, double* pInitialState, GRand* pRand, GAgentActionIterator* pActionIterator, double softMaxThresh)
: GQLearner(pObsControlRelation, actionDims, pInitialState, pRand, pActionIterator)
{
	// Enable incremental learning
	m_pQTable = pQTable;
	sp_relation pQRelation = new GUniformRelation(1);
	pQTable->beginIncrementalLearning(pObsControlRelation, pQRelation);

	// Init other stuff
	m_pBuf = new double[m_senseDims + m_actionDims];
	m_softMaxThresh = softMaxThresh;
	m_pActionIterator = pActionIterator;
	pActionIterator->reset(pInitialState);
}

// virtual
GIncrementalLearnerQAgent::~GIncrementalLearnerQAgent()
{
	delete[] m_pBuf;
}

// virtual
double GIncrementalLearnerQAgent::getQValue(const double* pState, const double* pAction)
{
	GVec::copy(m_pBuf, pState, m_senseDims);
	GVec::copy(m_pBuf + m_senseDims, pAction, m_actionDims);
	double out;
	m_pQTable->predict(m_pBuf, &out);
	GAssert(out > -1e200);
	return out;
}

// virtual
void GIncrementalLearnerQAgent::setQValue(const double* pState, const double* pAction, double qValue)
{
	GVec::copy(m_pBuf, pState, m_senseDims);
	GVec::copy(m_pBuf + m_senseDims, pAction, m_actionDims);
	m_pQTable->trainIncremental(m_pBuf, &qValue);
}

// virtual
void GIncrementalLearnerQAgent::chooseAction(const double* pSenses, double* pActions)
{
	m_pActionIterator->reset(pSenses);
	if(m_explore && m_pRand->uniform() >= m_softMaxThresh)
	{
		// Explore
		m_pActionIterator->randomAction(pActions, m_pRand);
	}
	else
	{
		// Exploit
		double bestQ = -1e200;
		double q;
		int i;
		GTEMPBUF(double, pCand, m_actionDims);
		for(i = 1; i < m_actionCap; i++)
		{
			if(!m_pActionIterator->nextAction(pCand))
				break;
			q = getQValue(pSenses, pCand);
			if(q > bestQ)
			{
				bestQ = q;
				GVec::copy(pActions, pCand, m_actionDims);
			}
		}
	}
}

} // namespace GClasses

