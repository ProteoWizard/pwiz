/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GBayesianNetwork.h"
#include "GRand.h"
#include "GMath.h"
#include <stddef.h>

using namespace GClasses;

GBayesianNetworkNode::GBayesianNetworkNode(double priorMean, double priorDeviation)
{
	m_currentMean = priorMean;
	m_currentDeviation = priorDeviation;
	m_nSamples = 0;
	m_nNewValues = 0;
	m_sumOfValues = 0;
	m_sumOfSquaredValues = 0;
}

// virtual
GBayesianNetworkNode::~GBayesianNetworkNode()
{
}

#define MIN_LOG_PROB -1e200

double GBayesianNetworkNode::gibbs(double x)
{
	double d;
	double logSum = logLikelihood(x, NULL, 0);
	if(logSum >= MIN_LOG_PROB)
	{
		GBayesianNetworkChildIterator iter(this);
		GBayesianNetworkNode* pChild;
		for(pChild = iter.GetNextChild(); pChild; pChild = iter.GetNextChild())
		{
			d = pChild->logLikelihood(pChild->currentValue(), this, x);
			if(d >= MIN_LOG_PROB)
			{
				logSum += d;
				pChild = iter.GetNextChild();
			}
			else
				return MIN_LOG_PROB;
		}
		return logSum;
	}
	else
		return MIN_LOG_PROB;
}

bool GBayesianNetworkNode::metropolis(GRand* pRand)
{
	double dCandidateValue = pRand->normal() * m_currentDeviation + m_currentMean;
	if(isDiscrete())
		dCandidateValue = floor(dCandidateValue + 0.5);
	if(!isSupported(dCandidateValue))
		return false;
	if(dCandidateValue == m_currentMean)
		return false;
	double cand = gibbs(dCandidateValue);
	if(cand >= MIN_LOG_PROB)
	{
		double curr = gibbs(m_currentMean);
		if(curr >= MIN_LOG_PROB)
		{
			if(log(pRand->uniform()) < cand - curr)
			{
				m_currentMean = dCandidateValue;
				return true;
			}
			else
				return false;
		}
		else
			return false;
	}
	else
		return false;
}

void GBayesianNetworkNode::sample(GRand* pRand)
{
	if(metropolis(pRand))
	{
		if(++m_nNewValues >= 10)
		{
			double dMean = m_sumOfValues / m_nSamples;
			m_currentDeviation = sqrt(m_sumOfSquaredValues / m_nSamples - (dMean * dMean));
			m_nNewValues = 0;
		}
	}
	if(m_nSamples < 0xffffffff)
	{
		m_sumOfValues += m_currentMean;
		m_sumOfSquaredValues += (m_currentMean * m_currentMean);
		m_nSamples++;
	}
}

// -------------------------------------------------------------------------------

GBayesianNetworkChildIterator::GBayesianNetworkChildIterator(GBayesianNetworkNode* pNode)
{
}

GBayesianNetworkChildIterator::~GBayesianNetworkChildIterator()
{
}

GBayesianNetworkNode* GBayesianNetworkChildIterator::GetNextChild()
{
	return NULL;
}

// -------------------------------------------------------------------------------
/*
GBayesianNetwork::GBayesianNetwork(GRelation* pRelation, int nOutputCount)
 : GSupervisedLearner(pRelation, nOutputCount)
{
}

GBayesianNetwork::~GBayesianNetwork()
{
}

void GBayesianNetwork::train(GMatrix& data)
{
	ThrowError("todo: GBayesianNetwork::Train isn't implemented yet. It should use a genetic algorithm or tabu search, or something like that to find a suitable network, and use GBayesianNetworkNode::Sample to infer parameter values for the network");
}

void GBayesianNetwork::predictDistribution(const double* pIn, GPrediction* pOut)
{
	ThrowError("todo: GBayesianNetwork::Train isn't implemented yet");
}
*/

