/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GPolicyLearner.h"
#include "GNeuralNet.h"
#include "GKNN.h"
#include "GDecisionTree.h"
#include "GNeighborFinder.h"
#include "GOptimizer.h"
#include <stdlib.h>
#include "GVec.h"
#include "GRand.h"
//#include "GImage.h"
#include "GHeap.h"
#include "GHillClimber.h"
#include "GDom.h"
#include <deque>
#include <math.h>

namespace GClasses {

using std::deque;

// virtual
void GDiscreteActionIterator::reset(const double* pState)
{
	m_action = 0;
}

// virtual
void GDiscreteActionIterator::randomAction(double* pOutAction, GRand* pRand)
{
	*pOutAction = (double)pRand->next(m_count);
}

// virtual
bool GDiscreteActionIterator::nextAction(double* pOutAction)
{
	if(m_action < m_count)
	{
		*pOutAction = m_action++;
		return true;
	}
	else
		return false;
}

// -----------------------------------------------------------------------------

GPolicyLearner::GPolicyLearner(sp_relation& pRelation, int actionDims)
: m_pRelation(pRelation)
{
	m_senseDims = (int)pRelation->size() - actionDims;
	if(m_senseDims < 0)
		ThrowError("more action dims than relation dims");
	m_actionDims = actionDims;
	m_teleported = true;
	m_explore = true;
}

GPolicyLearner::GPolicyLearner(GDomNode* pAgent)
{
	m_pRelation = GRelation::deserialize(pAgent->field("relation"));
	m_actionDims = (int)pAgent->field("actionDims")->asInt();
	m_senseDims = (int)m_pRelation->size() - m_actionDims;
	m_teleported = true;
}

GDomNode* GPolicyLearner::baseDomNode(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "actionDims", pDoc->newInt(m_actionDims));
	pNode->addField(pDoc, "relation", m_pRelation->serialize(pDoc));
	return pNode;
}

// ------------------------------------------------------------------------------------------
/*
#define PEACH_TOO_CLOSE 0.007
#define PEACH_ACTION_CAP 10
#define PEACH_SEARCH_RADIUS 2 // relative to the distance between the goal and the current state

GPeachAgent::GPeachAgent(sp_relation& pRelation, int actionDims, GRand* pRand, double* pGoal, GAgentActionIterator* pActionIterator)
: GPolicyLearner(pRelation, actionDims)
{
	GAssert(actionDims >= 1); // expected at least one action dimension
	GAssert(pGoal);
	m_pRand = pRand;
	m_pActionIterator = pActionIterator;

	// Make the physics relation
	int i;
	GMixedRelation* pRelPhysics = new GMixedRelation();
	m_pRelPhysics = pRelPhysics;
	pRelPhysics->addAttrs(pRelation.get());
	for(i = 0; i < m_senseDims; i++)
		pRelPhysics->addAttr(0);

	// Make the state relation
	GMixedRelation* pRelState = new GMixedRelation();
	m_pRelState = pRelState;
	pRelState->addAttrs(pRelation.get(), 0, m_senseDims);

	//GNeuralNet* pModel = new GNeuralNet(&m_relAct, m_senseDims, m_pRand);
	//pModel->AddLayer(6);
	//pModel->AddLayer(6);
	GKNN* pModel = new GKNN(3, pRand);
	pModel->setOptimizeScaleFactors(true);
	pModel->setElbowRoom(.003);
	m_pPhysicsModel = pModel;

	m_pGoal = new double[m_senseDims + m_pRelPhysics->size()];
	m_pTrainingRow = m_pGoal + m_senseDims;
	setGoal(pGoal);
	m_burnIn = 100;
}

// virtual
GPeachAgent::~GPeachAgent()
{
	delete[] m_pGoal;
	delete(m_pPhysicsModel);
}

void GPeachAgent::setGoal(double* pNewGoal)
{
	int i;
	for(i = 0; i < m_senseDims; i++)
	{
		if(pNewGoal[i] == UNKNOWN_REAL_VALUE)
			ThrowError("GPeachAgent doesn't support unknown goal dimensions");
	}
	GVec::copy(m_pGoal, pNewGoal, m_senseDims);
}

GIncrementalLearner* GPeachAgent::physicsModel()
{
	return m_pPhysicsModel;
}

void GPeachAgent::chooseAction(const double* pSenses, double* pActions)
{
	if(m_burnIn > 0)
		m_burnIn--;
	else if(m_pRand->next(15) == 0)
		m_burnIn = 1;
	if(m_explore && m_burnIn > 0)
	{
		m_pActionIterator->randomAction(pActions, m_pRand);
		return;
	}

	// Do a breadth-first search from the goal to pSenses
	GHeap heap(2048);
	GMatrix points(m_pRelState, &heap);
	points.reserve(4096);
	deque<double*> q;
	GKdTree kdTree(&points, 0, 1, NULL, true);
	double* pPoint = points.newRow();
	GVec::copy(pPoint, m_pGoal, m_senseDims);
	q.push_back(pPoint);
	double dRange = GVec::squaredDistance(pSenses, m_pGoal, m_senseDims) * PEACH_SEARCH_RADIUS * PEACH_SEARCH_RADIUS;
	double dBest = 1e200;
	double d;
	double* pTarget;
	int i;
	GTEMPBUF(double, pCand, m_senseDims + m_actionDims + m_senseDims);

#ifdef _DEBUG
//	GImage tmpImage;
//	tmpImage.SetSize(512, 512);
//	tmpImage.Clear(0xff000000);
//	GPlotWindow pw(&tmpImage, 0, 0, 3, 3);
//	float f = 0;
#endif

	while(q.size() > 0)
	{
		pTarget = q.front();
		q.pop_front();
		if(GVec::squaredDistance(pSenses, pTarget, m_senseDims) + GVec::squaredDistance(m_pGoal, pTarget, m_senseDims) > dRange)
			continue;

#ifdef _DEBUG
//		f = f + 0.0002;
//		if(f >= 1)
//			f = 0;
//		pw.PlotPoint(pTarget[0], pTarget[1], GetSpectrumColor(f));
#endif

		m_pActionIterator->reset(pSenses);
		for(i = 0; i < PEACH_ACTION_CAP; i++)
		{
			if(!m_pActionIterator->nextAction(pCand + m_senseDims))
				break;
			GVec::copy(pCand, pTarget, m_senseDims);
			m_pPhysicsModel->predict(pCand, pCand + m_senseDims + m_actionDims);
			GVec::add(pCand, pCand + m_senseDims + m_actionDims, m_senseDims);

			// See if it is too close to an existing row
			size_t neighbor;
			double squaredDist;
			kdTree.neighbors(&neighbor, &squaredDist, pCand);
			if(neighbor < 0 || squaredDist >= PEACH_TOO_CLOSE * PEACH_TOO_CLOSE)
			{
				// Add it to the set of visited points
				double* pVec = points.newRow();
				GVec::copy(pVec, pCand, m_senseDims);
				kdTree.addVector(pVec);
			
				// Add it to the queue
				q.push_back(pVec);
				d = GVec::squaredDistance(pSenses, pVec, m_senseDims);
				if(d < dBest)
				{
					dBest = d;
					GVec::copy(pActions, pCand + m_senseDims, m_actionDims);

#ifdef _DEBUG
//					pw.DrawArrow(pCand[0], pCand[1], pTarget[0], pTarget[1], 0xffffffff, 5);
#endif

				}
			}
		}
	}

#ifdef _DEBUG
//	tmpImage.SavePNGFile("breadth.png");
//	cout << "search image saved to breadth.png\n";
#endif

}

// virtual
void GPeachAgent::refinePolicyAndChooseNextAction(const double* pSenses, double* pActions)
{
	// Learn to map from actions to consequences in the context of the senses
	// Input: senses_before + actions
	// Output: sense_delta
	if(!m_teleported)
	{
		GVec::copy(m_pTrainingRow, pSenses, m_senseDims);
		GVec::subtract(m_pTrainingRow + m_senseDims + m_actionDims, pSenses, m_senseDims);
		m_pPhysicsModel->trainIncremental(m_pTrainingRow, m_pTrainingRow + m_senseDims + m_actionDims);
	}

	// Choose the next action
	chooseAction(pSenses, pActions);

	// Remember the sense vector and action vector for next time
	GVec::copy(m_pTrainingRow + m_senseDims + m_actionDims, pSenses, m_senseDims);
	GVec::copy(m_pTrainingRow + m_senseDims, pActions, m_actionDims);
	m_teleported = false;
}
*/

} // namespace GClasses

