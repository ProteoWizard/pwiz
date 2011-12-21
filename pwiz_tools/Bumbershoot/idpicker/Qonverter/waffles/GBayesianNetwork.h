/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GBAYESIANNETWORK_H__
#define __GBAYESIANNETWORK_H__

//#include "GLearner.h"

namespace GClasses {

class GUnivariateDistribution;
class GRand;

/// This is the base class for all nodes in a Bayesian network. Classes that
/// inherit from this class must implement three pure virtual methods. Note
/// that the GUnivariateDistribution class has an IsDiscrete and an IsSupported
/// method, so if your class wraps a GUnivariateDistribution then two of them
/// are taken care of for you. In order to implement ComputeLogLikelihood, your
/// class will probably need references to its parent nodes so that it can obtain
/// their values to use as parameters for its distribution. You can implement
/// your network structure however you like. When you have your network set up,
/// you're ready to use MCMC to infer values for the network. To do this,
/// just create a loop that calls Sample on each node in the network, and the
/// whole network should eventually converge to good values.
/// (Also, you need to make GBayesianNetworkChildIterator work, which I haven't
/// worked out yet.)
class GBayesianNetworkNode
{
protected:
	double m_currentMean, m_currentDeviation;
	unsigned int m_nSamples;
	unsigned int m_nNewValues;
	double m_sumOfValues, m_sumOfSquaredValues;

public:
	GBayesianNetworkNode(double priorMean, double priorDeviation);
	virtual ~GBayesianNetworkNode();

	/// This should return true iff this node supports only discrete values
	virtual bool isDiscrete() = 0;

	/// This should return true iff val is within the range of supported values.
	/// (If IsDiscrete returns true, val will contain only discrete values, so
	/// you don't need to check for that.)
	virtual bool isSupported(double val) = 0;

	/// Compute the log-likelihood of the value "x" given the current values
	/// of all of this node's parent nodes, (except, to facilitate Gibb's Sampling,
	/// if one of the parent nodes is "pSpecialParent", then "specialParentValue"
	/// should be used for that parent's value instead of that parent's current value.
	/// If this function returns nan or anything <= -1e200, the sample will be
	/// rejected without consideration. (Note that these likelihoods don't need to
	/// be normalized. It's okay of they sum/integrate to a constant instead of to 1.)
	virtual double logLikelihood(double x, GBayesianNetworkNode* pSpecialParent, double specialParentValue) = 0;

	double currentValue() { return m_currentMean; }

	/// Uses a combination of Metropolis, and Gibb's Sampling to resample the node.
	/// (Also, this dynamically adjusts the sampling distribution variance.)
	void sample(GRand* pRand);

protected:
	/// Computes the log-probability of x (as a value for this node) given
	/// the current values for the entire rest of the network (aka the
	/// complete conditional), which according to Gibbs, is equal to
	/// the log-probability of x given the Markov-Blanket of this node,
	/// which we can compute efficiently.
	double gibbs(double x);

	/// Sample the network in a manner that can be proven to converge to a
	/// true joint distribution for the network. Returns true if the new candidate
	/// value is selected.
	bool metropolis(GRand* pRand);
};



/// Iterates through all the children of the specified node in a Bayesian network
class GBayesianNetworkChildIterator
{
public:
	GBayesianNetworkChildIterator(GBayesianNetworkNode* pNode);
	~GBayesianNetworkChildIterator();

	GBayesianNetworkNode* GetNextChild();
};


/*
class GBayesianNetwork : public GSupervisedGeneralizingLearner
{
public:
	GBayesianNetwork(GRelation* pRelation, int nOutputCount);
	virtual ~GBayesianNetwork();

	virtual void train(GMatrix& data);
	virtual void predictDistribution(const double* pIn, GPrediction* pOut);
};
*/

} // namespace GClasses

#endif // __GBAYESIANNETWORK_H__
