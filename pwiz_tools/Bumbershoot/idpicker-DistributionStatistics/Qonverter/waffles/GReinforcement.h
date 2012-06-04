/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __REINFORCEMENT_H__
#define __REINFORCEMENT_H__

#include "GPolicyLearner.h"
#include "GLearner.h"

namespace GClasses {

class GRand;
class GNeuralNet;
class GKNN;
class Bogie;


/// The base class of a Q-Learner. To use this class, there are four
/// abstract methods you'll need to implement. See also the comment for GPolicyLearner.
class GQLearner : public GPolicyLearner
{
protected:
	GRand* m_pRand;
	GAgentActionIterator* m_pActionIterator;
	double m_learningRate;
	double m_discountFactor;
	double* m_pSenses;
	double* m_pAction;
	int m_actionCap;

public:
	GQLearner(sp_relation& pRelation, int actionDims, double* pInitialState, GRand* pRand, GAgentActionIterator* pActionIterator);
	virtual ~GQLearner();

	/// Sets the learning rate (often called "alpha"). If state is deterministic and actions
	/// have deterministic consequences, then this should be 1. If there is any non-determinism,
	/// there are three common approaches for picking the learning rate: 1- use a fairly
	/// small value (perhaps 0.1), 2- decay it over time (by calling this method before every
	/// iteration), 3- remember how many times 'n' each state has already been visited, and set the
	/// learning rate to 1/(n+1) before each iteration. The third technique is the best, but
	/// is awkward with continuous state spaces.
	void setLearningRate(double d);

	/// Sets the factor for discounting future rewards (often called "gamma").
	void setDiscountFactor(double d);

	/// You must implement some kind of structure to store q-values. This method
	/// should return the current q-value for the specified state and action
	virtual double getQValue(const double* pState, const double* pAction) = 0;

	/// This is the complement to GetQValue.
	virtual void setQValue(const double* pState, const double* pAction, double qValue) = 0;

	/// See GPolicyLearner::refinePolicyAndChooseNextAction
	virtual void refinePolicyAndChooseNextAction(const double* pSenses, double* pOutActions);

	/// This specifies a cap on how many actions to sample. (If actions are
	/// continuous, you obviously don't want to try them all.)
	void setActionCap(int n) { m_actionCap = n; }

protected:
	/// This method picks the action during training.
	/// This method is called by refinePolicyAndChooseNextAction.
	/// (If it makes things easier, the agent may actually
	/// perform the action here, but it's a better practise to
	/// wait until refinePolicyAndChooseNextAction
	/// returns, because that keeps the "thinking" and "acting" stages separated
	/// from each other.) One way to pick the next action is to call
	/// GetQValue for all possible actions in the current state, and pick the one with
	/// the highest Q-value. But if you always pick the best action, you'll never discover
	/// things you don't already know about, so you need to find some balance between
	/// exploration and exploitation. One way to do this is to usually pick the best
	/// action, but sometimes pick a random action.
	virtual void chooseAction(const double* pSenses, double* pOutActions) = 0;

	/// A reward is obtained when the agent performs a particular action in a particular
	/// state. (A penalty is a negative reward. A reward of zero is no reward.)
	/// This method returns the reward that was obtained when the last action was
	/// performed. If you return UNKNOWN_REAL_VALUE, then the q-table
	/// will not be updated for that action.
	virtual double rewardFromLastAction() = 0;
};



/// This is an implementation of GQLearner that uses an incremental learner for
/// its Q-table and a SoftMax (usually pick the best action, but sometimes randomly
/// pick the action) strategy to balance between exploration vs exploitation. To use
/// this class, you need to supply an incremental learner (see the comment for the
/// constructor for more details) and to implement the GetRewardForLastAction method.
class GIncrementalLearnerQAgent : public GQLearner
{
protected:
	GIncrementalLearner* m_pQTable;
	double* m_pBuf;
	double m_softMaxThresh;

public:
	/// pQTable must be an incremental learner. If the relation for pQTable has n
	/// attributes, then the first (n-1) attributes refer to the sense (state) and action,
	/// and the last attribute refers to the Q-value (the current estimate of the
	/// utility of performing that action in that state).
	/// For actionDims, see the comment for GPolicyLearner::GPolicyLearner.
	/// pInitialState is the initial sense vector.
	/// If softMaxThresh is 0, it always picks a random action. If softMaxThresh is 1, it
	/// always picks the best action. For values in between, it does something in between.
	GIncrementalLearnerQAgent(sp_relation& pObsControlRelation, GIncrementalLearner* pQTable, int actionDims, double* pInitialState, GRand* pRand, GAgentActionIterator* pActionIterator, double softMaxThresh);
	virtual ~GIncrementalLearnerQAgent();

	/// See the comment for GQLearner::GetQValue
	virtual double getQValue(const double* pState, const double* pAction);
	
	/// See the comment for GQLearner::SetQValue
	virtual void setQValue(const double* pState, const double* pAction, double qValue);

protected:
	virtual void chooseAction(const double* pSenses, double* pOutActions);
};


} // namespace GClasses

#endif // __REINFORCEMENT_H__
