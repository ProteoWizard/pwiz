/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GNeuralNet.h"
#include "GMath.h"
#include "GActivation.h"
#include "GDistribution.h"
#include "GError.h"
#include "GRand.h"
#include "GVec.h"
#include "GDom.h"
#include "GHillClimber.h"
#include "GTransform.h"
#include "GSparseMatrix.h"
#include "GDistance.h"

namespace GClasses {

using std::vector;

void GNeuron::resetWeights(GRand* pRand, double inputCenter)
{
	for(vector<double>::iterator weight = m_weights.begin(); weight != m_weights.end(); weight++)
		*weight = pRand->normal() * 0.1;

	// Remove all bias (todo: this has very little effect since the weights are small already--why even bother?)
	double& bias = m_weights[0];
	for(vector<double>::iterator weight = m_weights.begin() + 1; weight != m_weights.end(); weight++)
		bias -= inputCenter * (*weight);
}

// ----------------------------------------------------------------------

void GNeuralNetLayer::resetWeights(GRand* pRand, double inputCenter)
{
	for(vector<GNeuron>::iterator neuron = m_neurons.begin(); neuron != m_neurons.end(); neuron++)
		neuron->resetWeights(pRand, inputCenter);
}

// ----------------------------------------------------------------------

GBackProp::GBackProp(GNeuralNet* pNN)
: m_pNN(pNN)
{
	// Initialize structures to mirror the neural network
	m_layers.resize(m_pNN->m_layers.size());
	for(size_t i = 0; i < m_layers.size(); i++)
	{
		GBackPropLayer& layer = m_layers[i];
		layer.m_neurons.resize(pNN->m_layers[i].m_neurons.size());
		for(size_t j = 0; j < layer.m_neurons.size(); j++)
		{
			GBackPropNeuron& neuron = layer.m_neurons[j];
			neuron.m_weights.resize(pNN->m_layers[i].m_neurons[j].m_weights.size());
		}
	}
}

void GBackProp::backPropLayer(GNeuralNetLayer* pNNFromLayer, GNeuralNetLayer* pNNToLayer, GBackPropLayer* pBPFromLayer, GBackPropLayer* pBPToLayer, size_t fromBegin)
{
	vector<GNeuron>::iterator nnFrom, nnCur;
	vector<GBackPropNeuron>::iterator bpFrom, bpCur;
	vector<double>::iterator nn_w;

	// Sum the error times weight for all the children
	nnFrom = pNNFromLayer->m_neurons.begin();
	bpFrom = pBPFromLayer->m_neurons.begin();
	nn_w = nnFrom->m_weights.begin() + 1 + fromBegin;
	bpCur = pBPToLayer->m_neurons.begin();
	while(bpCur != pBPToLayer->m_neurons.end())
	{
		bpCur->m_error = (*nn_w) * bpFrom->m_error; // use "=" for first pass
		nn_w++;
		bpCur++;
	}
	nnFrom++;
	bpFrom++;
	while(bpFrom != pBPFromLayer->m_neurons.end())
	{
		nn_w = nnFrom->m_weights.begin() + 1 + fromBegin;
		bpCur = pBPToLayer->m_neurons.begin();
		while(bpCur != pBPToLayer->m_neurons.end())
		{
			bpCur->m_error += (*nn_w) * bpFrom->m_error; // use "+=" for subsequent passes
			nn_w++;
			bpCur++;
		}
		nnFrom++;
		bpFrom++;
	}

	// Multiply by the derivative of the activation function
	nnCur = pNNToLayer->m_neurons.begin();
	bpCur = pBPToLayer->m_neurons.begin();
	while(bpCur != pBPToLayer->m_neurons.end())
	{
		bpCur->m_error *= pNNToLayer->m_pActivationFunction->derivativeOfNet(nnCur->m_net, nnCur->m_activation);
		nnCur++;
		bpCur++;
	}
}

void GBackProp::backPropFromSingleNode(GNeuron& nnFrom, GBackPropNeuron& bpFrom, GNeuralNetLayer* pNNToLayer, GBackPropLayer* pBPToLayer)
{
	vector<GNeuron>::iterator nnCur;
	vector<GBackPropNeuron>::iterator bpCur;
	vector<double>::iterator nn_w;

	// Sum the error times weight for all the children
	nn_w = nnFrom.m_weights.begin() + 1;
	bpCur = pBPToLayer->m_neurons.begin();
	while(bpCur != pBPToLayer->m_neurons.end())
	{
		bpCur->m_error = (*nn_w) * bpFrom.m_error;
		nn_w++;
		bpCur++;
	}

	// Multiply by the derivative of the activation function
	nnCur = pNNToLayer->m_neurons.begin();
	bpCur = pBPToLayer->m_neurons.begin();
	while(bpCur != pBPToLayer->m_neurons.end())
	{
		bpCur->m_error *= pNNToLayer->m_pActivationFunction->derivativeOfNet(nnCur->m_net, nnCur->m_activation);
		nnCur++;
		bpCur++;
	}
}

void GBackProp::backPropLayer2(GNeuralNetLayer* pNNFromLayer1, GNeuralNetLayer* pNNFromLayer2, GNeuralNetLayer* pNNToLayer, GBackPropLayer* pBPFromLayer1, GBackPropLayer* pBPFromLayer2, GBackPropLayer* pBPToLayer, size_t pass)
{
	vector<GNeuron>::iterator nnFrom, nnCur;
	vector<GBackPropNeuron>::iterator bpFrom, bpCur;
	vector<double>::iterator nn_w;

	double sum, alpha;
	int w = 1;
	nnCur = pNNToLayer->m_neurons.begin();
	bpCur = pBPToLayer->m_neurons.begin();
	while(bpCur != pBPToLayer->m_neurons.end())
	{
		// Sum error from the first previous layer
		sum = 0;
		nnFrom = pNNFromLayer1->m_neurons.begin();
		bpFrom = pBPFromLayer1->m_neurons.begin();
		while(bpFrom != pBPFromLayer1->m_neurons.end())
		{
			sum += nnFrom->m_weights[w] * bpFrom->m_error;
			nnFrom++;
			bpFrom++;
		}

		// Sum error from the second previous layer
		if(pNNFromLayer2)
		{
			nnFrom = pNNFromLayer2->m_neurons.begin();
			bpFrom = pBPFromLayer2->m_neurons.begin();
			while(bpFrom != pBPFromLayer2->m_neurons.end())
			{
				sum += nnFrom->m_weights[w] * bpFrom->m_error;
				nnFrom++;
				bpFrom++;
			}
		}

		// Multiply by derivative of activation function
		sum *= pNNToLayer->m_pActivationFunction->derivativeOfNet(nnCur->m_net, nnCur->m_activation);

		// Average with error computed from previous passes
		alpha = 1.0 / pass;
		bpCur->m_error *= (1.0 - alpha);
		bpCur->m_error += (alpha * sum);

		nnCur++;
		bpCur++;
		w++;
	}
}

void GBackProp::adjustWeights(GNeuralNetLayer* pNNFromLayer, GNeuralNetLayer* pNNToLayer, GBackPropLayer* pBPFromLayer, double learningRate, double momentum)
{
	vector<GNeuron>::iterator nnFrom;
	vector<GBackPropNeuron>::iterator bpFrom;
	nnFrom = pNNFromLayer->m_neurons.begin();
	bpFrom = pBPFromLayer->m_neurons.begin();
	while(bpFrom != pBPFromLayer->m_neurons.end())
	{
		vector<GBackPropWeight>::iterator bp_w = bpFrom->m_weights.begin();
		vector<double>::iterator nn_w = nnFrom->m_weights.begin();
		bp_w->m_delta *= momentum;
		bp_w->m_delta += (learningRate * bpFrom->m_error);
		*nn_w = std::max(-1e12, std::min(1e12, *nn_w + bp_w->m_delta));
		bp_w++;
		nn_w++;
		for(vector<GNeuron>::iterator k = pNNToLayer->m_neurons.begin(); k != pNNToLayer->m_neurons.end(); k++)
		{
			bp_w->m_delta *= momentum;
			bp_w->m_delta += (learningRate * bpFrom->m_error * k->m_activation);
			*nn_w = std::max(-1e12, std::min(1e12, *nn_w + bp_w->m_delta));
			bp_w++;
			nn_w++;
		}
		nnFrom++;
		bpFrom++;
	}
}

void GBackProp::adjustWeights(GNeuralNetLayer* pNNFromLayer, const double* pFeatures, bool useInputBias, GBackPropLayer* pBPFromLayer, double learningRate, double momentum)
{
	vector<GNeuron>::iterator nnFrom = pNNFromLayer->m_neurons.begin();
	vector<GBackPropNeuron>::iterator bpFrom = pBPFromLayer->m_neurons.begin();
	while(bpFrom != pBPFromLayer->m_neurons.end())
	{
		vector<GBackPropWeight>::iterator bp_w = bpFrom->m_weights.begin();
		vector<double>::iterator nn_w = nnFrom->m_weights.begin();
		bp_w->m_delta *= momentum;
		bp_w->m_delta += (learningRate * bpFrom->m_error);
		*nn_w = std::max(-1e12, std::min(1e12, *nn_w + bp_w->m_delta));
		bp_w++;
		nn_w++;
		const double* k = pFeatures;
		if(useInputBias)
			k++;
		for( ; nn_w != nnFrom->m_weights.end(); k++)
		{
			if(*k != UNKNOWN_REAL_VALUE)
			{
				bp_w->m_delta *= momentum;
				bp_w->m_delta += (learningRate * bpFrom->m_error * (*k));
				*nn_w = std::max(-1e12, std::min(1e12, *nn_w + bp_w->m_delta));
			}
			bp_w++;
			nn_w++;
		}
		nnFrom++;
		bpFrom++;
	}
}

void GBackProp::adjustWeightsSingleNeuron(GNeuron& nnFrom, GNeuralNetLayer* pNNToLayer, GBackPropNeuron& bpFrom, double learningRate, double momentum)
{
	vector<GBackPropWeight>::iterator bp_w = bpFrom.m_weights.begin();
	vector<double>::iterator nn_w = nnFrom.m_weights.begin();
	bp_w->m_delta *= momentum;
	bp_w->m_delta += (learningRate * bpFrom.m_error);
	*nn_w = std::max(-1e12, std::min(1e12, *nn_w + bp_w->m_delta));
	bp_w++;
	nn_w++;
	for(vector<GNeuron>::iterator k = pNNToLayer->m_neurons.begin(); k != pNNToLayer->m_neurons.end(); k++)
	{
		bp_w->m_delta *= momentum;
		bp_w->m_delta += (learningRate * bpFrom.m_error * k->m_activation);
		*nn_w = std::max(-1e12, std::min(1e12, *nn_w + bp_w->m_delta));
		bp_w++;
		nn_w++;
	}
}

void GBackProp::adjustWeightsSingleNeuron(GNeuron& nnFrom, const double* pFeatures, bool useInputBias, GBackPropNeuron& bpFrom, double learningRate, double momentum)
{
	vector<GBackPropWeight>::iterator bp_w = bpFrom.m_weights.begin();
	vector<double>::iterator nn_w = nnFrom.m_weights.begin();
	bp_w->m_delta *= momentum;
	bp_w->m_delta += (learningRate * bpFrom.m_error);
	*nn_w = std::max(-1e12, std::min(1e12, *nn_w + bp_w->m_delta));
	bp_w++;
	nn_w++;
	const double* k = pFeatures;
	if(useInputBias)
		k++;
	for( ; nn_w != nnFrom.m_weights.end(); k++)
	{
		if(*k != UNKNOWN_REAL_VALUE)
		{
			bp_w->m_delta *= momentum;
			bp_w->m_delta += (learningRate * bpFrom.m_error * (*k));
			*nn_w = std::max(-1e12, std::min(1e12, *nn_w + bp_w->m_delta));
		}
		bp_w++;
		nn_w++;
	}
}

void GBackProp::backpropagate()
{
	size_t i = m_layers.size() - 1;
	GNeuralNetLayer* pNNPrevLayer = &m_pNN->m_layers[i];
	GBackPropLayer* pBPPrevLayer = &m_layers[i];
	for(i--; i < m_layers.size(); i--)
	{
		GNeuralNetLayer* pNNCurLayer = &m_pNN->m_layers[i];
		GBackPropLayer* pBPCurLayer = &m_layers[i];
		backPropLayer(pNNPrevLayer, pNNCurLayer, pBPPrevLayer, pBPCurLayer);
		pNNPrevLayer = pNNCurLayer;
		pBPPrevLayer = pBPCurLayer;
	}
}

void GBackProp::backpropagateSingleOutput(size_t outputNode)
{
	size_t i = m_layers.size() - 1;
	GNeuralNetLayer* pNNPrevLayer = &m_pNN->m_layers[i];
	GBackPropLayer* pBPPrevLayer = &m_layers[i];
	if(--i < m_layers.size())
	{
		GNeuralNetLayer* pNNCurLayer = &m_pNN->m_layers[i];
		GBackPropLayer* pBPCurLayer = &m_layers[i];
		backPropFromSingleNode(pNNPrevLayer->m_neurons[outputNode], pBPPrevLayer->m_neurons[outputNode], pNNCurLayer, pBPCurLayer);
		pNNPrevLayer = pNNCurLayer;
		pBPPrevLayer = pBPCurLayer;
		for(i--; i < m_layers.size(); i--)
		{
			pNNCurLayer = &m_pNN->m_layers[i];
			pBPCurLayer = &m_layers[i];
			backPropLayer(pNNPrevLayer, pNNCurLayer, pBPPrevLayer, pBPCurLayer);
			pNNPrevLayer = pNNCurLayer;
			pBPPrevLayer = pBPCurLayer;
		}
	}
}

void GBackProp::descendGradient(const double* pFeatures, double learningRate, double momentum, bool useInputBias)
{
	size_t i = m_layers.size() - 1;
	GNeuralNetLayer* pNNPrevLayer = &m_pNN->m_layers[i];
	GBackPropLayer* pBPPrevLayer = &m_layers[i];
	for(i--; i < m_layers.size(); i--)
	{
		GNeuralNetLayer* pNNCurLayer = &m_pNN->m_layers[i];
		GBackPropLayer* pBPCurLayer = &m_layers[i];
		adjustWeights(pNNPrevLayer, pNNCurLayer, pBPPrevLayer, learningRate, momentum);
		pNNPrevLayer = pNNCurLayer;
		pBPPrevLayer = pBPCurLayer;
	}

	// adjust the weights on the last hidden layer
	adjustWeights(pNNPrevLayer, pFeatures, useInputBias, pBPPrevLayer, m_pNN->learningRate(), m_pNN->momentum());
}

void GBackProp::descendGradientSingleOutput(size_t outputNeuron, const double* pFeatures, double learningRate, double momentum, bool useInputBias)
{
	size_t i = m_layers.size() - 1;
	GNeuralNetLayer* pNNPrevLayer = &m_pNN->m_layers[i];
	GBackPropLayer* pBPPrevLayer = &m_layers[i];
	if(i == 0)
		adjustWeightsSingleNeuron(pNNPrevLayer->m_neurons[outputNeuron], pFeatures, useInputBias, pBPPrevLayer->m_neurons[outputNeuron], learningRate, momentum);
	else
	{
		i--;
		GNeuralNetLayer* pNNCurLayer = &m_pNN->m_layers[i];
		GBackPropLayer* pBPCurLayer = &m_layers[i];
		adjustWeightsSingleNeuron(pNNPrevLayer->m_neurons[outputNeuron], pNNCurLayer, pBPPrevLayer->m_neurons[outputNeuron], learningRate, momentum);
		pNNPrevLayer = pNNCurLayer;
		pBPPrevLayer = pBPCurLayer;
		for(i--; i < m_layers.size(); i--)
		{
			pNNCurLayer = &m_pNN->m_layers[i];
			pBPCurLayer = &m_layers[i];
			adjustWeights(pNNPrevLayer, pNNCurLayer, pBPPrevLayer, learningRate, momentum);
			pNNPrevLayer = pNNCurLayer;
			pBPPrevLayer = pBPCurLayer;
		}

		// adjust the weights on the last hidden layer
		adjustWeights(pNNPrevLayer, pFeatures, useInputBias, pBPPrevLayer, m_pNN->learningRate(), m_pNN->momentum());
	}
}

void GBackProp::adjustFeatures(double* pFeatures, double learningRate, size_t skip, bool useInputBias)
{
	GNeuralNetLayer& nnLayer = m_pNN->m_layers[0];
	GBackPropLayer& bpLayer = m_layers[0];
	vector<GNeuron>::iterator nn = nnLayer.m_neurons.begin();
	vector<GBackPropNeuron>::iterator bp = bpLayer.m_neurons.begin();
	while(nn != nnLayer.m_neurons.end())
	{
		double* pF = pFeatures;
		if(useInputBias)
			*(pF++) += learningRate * bp->m_error;
		for(vector<double>::iterator w = nn->m_weights.begin() + 1 + skip; w != nn->m_weights.end(); w++)
			*(pF++) += learningRate * bp->m_error * (*w);
		nn++;
		bp++;
	}
}

void GBackProp::adjustFeaturesSingleOutput(size_t outputNeuron, double* pFeatures, double learningRate, bool useInputBias)
{
	if(m_layers.size() != 1)
	{
		adjustFeatures(pFeatures, learningRate, 0, useInputBias);
		return;
	}
	GAssert(outputNeuron < m_pNN->m_layers[0].m_neurons.size()); // out of range
	GNeuron& nn = m_pNN->m_layers[0].m_neurons[outputNeuron];
	GBackPropNeuron& bp = m_layers[0].m_neurons[outputNeuron];
	double* pOut = pFeatures;
	if(useInputBias)
		*(pOut++) += learningRate * bp.m_error;
	for(vector<double>::iterator w = nn.m_weights.begin() + 1; w != nn.m_weights.end(); w++)
		*(pOut++) += learningRate * bp.m_error * (*w);
}


// ----------------------------------------------------------------------

GNeuralNet::GNeuralNet(GRand& rand)
: GIncrementalLearner(rand), m_pBackProp(NULL), m_internalFeatureDims(0), m_internalLabelDims(0), m_pActivationFunction(NULL), m_learningRate(0.1), m_momentum(0.0), m_validationPortion(0.35), m_minImprovement(0.002), m_epochsPerValidationCheck(200), m_backPropTargetFunction(squared_error), m_useInputBias(false)
{
	m_layers.resize(1);
}

GNeuralNet::GNeuralNet(GDomNode* pNode, GLearnerLoader& ll)
: GIncrementalLearner(pNode, ll)
{
	// Create the layers
	m_pActivationFunction = NULL;
	m_internalFeatureDims = 0;
	m_internalLabelDims = 0;
	m_layers.resize(1);
	m_pBackProp = NULL;
	m_internalFeatureDims = (size_t)pNode->field("ifd")->asInt();
	GDomNode* pLayerList = pNode->field("layers");
	GDomListIterator it1(pLayerList);
	size_t layerCount = it1.remaining();
	for(size_t i = 0; i < layerCount - 1; i++)
	{
		GDomNode* pActivation = it1.current()->fieldIfExists("af");
		if(pActivation)
			setActivationFunction(GActivationFunction::deserialize(pActivation), true);
		else if(i == 0)
			ThrowError("The first layer is expected to specify an activation function");
		addLayer((size_t)it1.current()->field("nodes")->asInt());
		it1.advance();
	}
	GDomNode* pActivation = it1.current()->fieldIfExists("af");
	if(pActivation)
		setActivationFunction(GActivationFunction::deserialize(pActivation), true);
	else if(layerCount == 1)
		ThrowError("The first layer is expected to specify an activation function");
	m_internalLabelDims = (size_t)it1.current()->field("nodes")->asInt();

	// Enable training
	sp_relation pFeatureRel = new GUniformRelation(m_internalFeatureDims);
	sp_relation pLabelRel = new GUniformRelation(m_internalLabelDims);
	m_useInputBias = pNode->field("ib")->asBool();
	beginIncrementalLearningInner(pFeatureRel, pLabelRel);

	// Set other settings
	m_learningRate = pNode->field("learningRate")->asDouble();
	m_momentum = pNode->field("momentum")->asDouble();
	m_backPropTargetFunction = (TargetFunction)pNode->field("target")->asInt();

	// Set the weights
	GDomNode* pWeightList = pNode->field("weights");
	GDomListIterator it2(pWeightList);
	if(it2.remaining() != countWeights())
		ThrowError("Weights don't line up. (expected ", to_str(countWeights()), ", got ", to_str(it2.remaining()), ".)");
	GTEMPBUF(double, pWeights, it2.remaining());
	for(size_t i = 0; it2.current(); i++)
	{
		pWeights[i] = it2.current()->asDouble();
		it2.advance();
	}
	setWeights(pWeights);
}

GNeuralNet::~GNeuralNet()
{
	releaseTrainingJunk();
	for(vector<GActivationFunction*>::iterator it = m_activationFunctions.begin(); it != m_activationFunctions.end(); it++)
		delete(*it);
}

// virtual
GDomNode* GNeuralNet::serialize(GDom* pDoc)
{
	if(!hasTrainingBegun())
		ThrowError("The network has not been trained");
	GDomNode* pNode = baseDomNode(pDoc, "GNeuralNet");

	// Add the layer sizes
	pNode->addField(pDoc, "ifd", pDoc->newInt(m_internalFeatureDims));
	GDomNode* pLayerList = pNode->addField(pDoc, "layers", pDoc->newList());
	GActivationFunction* pPrevSF = NULL;
	for(size_t i = 0; i < m_layers.size(); i++)
	{
		GDomNode* pLayerObj = pLayerList->addItem(pDoc, pDoc->newObj());
		pLayerObj->addField(pDoc, "nodes", pDoc->newInt(m_layers[i].m_neurons.size()));
		GAssert(i != m_layers.size() - 1 || m_layers[i].m_neurons.size() == m_internalLabelDims);
		if(m_layers[i].m_pActivationFunction != pPrevSF)
		{
			pPrevSF = m_layers[i].m_pActivationFunction;
			pLayerObj->addField(pDoc, "af", m_layers[i].m_pActivationFunction->serialize(pDoc));
		}
	}

	// Add other settings
	pNode->addField(pDoc, "learningRate", pDoc->newDouble(m_learningRate));
	pNode->addField(pDoc, "momentum", pDoc->newDouble(m_momentum));
	pNode->addField(pDoc, "target", pDoc->newInt(m_backPropTargetFunction));
	pNode->addField(pDoc, "ib", pDoc->newBool(m_useInputBias));

	// Add the weights
	{
		size_t wc = countWeights();
		GTEMPBUF(double, pWeights, wc);
		weights(pWeights);
		GDomNode* pWeightList = pNode->addField(pDoc, "weights", pDoc->newList());
		for(size_t i = 0; i < wc; i++)
			pWeightList->addItem(pDoc, pDoc->newDouble(pWeights[i]));
	}

	return pNode;
}

void GNeuralNet::setActivationFunction(GActivationFunction* pSF, bool hold)
{
	m_pActivationFunction = pSF;
	if(hold)
		m_activationFunctions.push_back(pSF);
}

// virtual
bool GNeuralNet::supportedFeatureRange(double* pOutMin, double* pOutMax)
{
	*pOutMin = -1.5;
	*pOutMax = 1.5;
	return false;
}

// virtual
bool GNeuralNet::supportedLabelRange(double* pOutMin, double* pOutMax)
{
	if(m_pActivationFunction)
	{
		double hr = m_pActivationFunction->halfRange();
		if(hr >= 1e50)
			return true;
		double c = m_pActivationFunction->center();
		*pOutMin = c - hr;
		*pOutMax = c + hr;
	}
	else
	{
		// Assume the logistic function is the default
		*pOutMin = 0.0;
		*pOutMax = 1.0;
	}
	return false;
}

void GNeuralNet::releaseTrainingJunk()
{
	delete(m_pBackProp);
	m_pBackProp = NULL;
}

void GNeuralNet::addLayer(size_t nodeCount)
{
	if(hasTrainingBegun())
		ThrowError("Changing the network structure after some training has begun is not yet supported.");
	if(nodeCount < 1)
		ThrowError("Cannot add a layer with fewer than 1 node");

	// Add a new layer to be the new output layer
	size_t i = m_layers.size();
	GAssert(i > 0); // There should already be an output layer
	m_layers.resize(i + 1);

	// Turn the old output layer into the new hidden layer
	GNeuralNetLayer& newLayer = m_layers[i - 1];
	newLayer.m_neurons.resize(nodeCount);
	if(!m_pActivationFunction)
		setActivationFunction(new GActivationLogistic(), true);
	newLayer.m_pActivationFunction = m_pActivationFunction;

	// Give each node in the previous layer a weight for the bias, plus a weight for each node in this layer
	if(i > 1)
	{
		size_t weightCount = 1 + m_layers[i - 2].m_neurons.size(); // bias, plus a connection to each previous node
		for(size_t j = 0; j < newLayer.m_neurons.size(); j++)
			newLayer.m_neurons[j].m_weights.resize(weightCount);
	}
}

void GNeuralNet::addNode(size_t layer)
{
	if(layer >= m_layers.size())
		ThrowError("layer index out of range");

	// Add a new neuron to this layer
	GNeuralNetLayer& l = m_layers[layer];
	size_t n = l.m_neurons.size();
	l.m_neurons.resize(n + 1);
	GNeuron& neuron = l.m_neurons[n];
	neuron.m_weights.resize(l.m_neurons[0].m_weights.size());
	neuron.resetWeights(&m_rand, l.m_pActivationFunction->center());

	// Add another weight to each node in the next layer
	if(layer < m_layers.size() - 1)
	{
		GNeuralNetLayer& layerNext = m_layers[layer + 1];
		for(vector<GNeuron>::iterator it = layerNext.m_neurons.begin(); it != layerNext.m_neurons.end(); it++)
			it->m_weights.push_back(0.05 * m_rand.normal());
	}
}

void GNeuralNet::dropNode(size_t layer, size_t node)
{
	if(layer >= m_layers.size())
		ThrowError("layer index out of range");
	GNeuralNetLayer& l = m_layers[layer];
	if(node >= l.m_neurons.size())
		ThrowError("node index out of range");
	if(l.m_neurons.size() == 1)
		ThrowError("The layer must have at least one node in it");

	// Drop the neuron from this layer
	l.m_neurons.erase(l.m_neurons.begin() + node);

	// Remove the corresponding weight from each node in the next layer
	if(layer < m_layers.size() - 1)
	{
		GNeuralNetLayer& layerNext = m_layers[layer + 1];
		for(vector<GNeuron>::iterator it = layerNext.m_neurons.begin(); it != layerNext.m_neurons.end(); it++)
			it->m_weights.erase(it->m_weights.begin() + node + 1);
	}
}

size_t GNeuralNet::countWeights()
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	size_t wc = 0;
	for(vector<GNeuralNetLayer>::iterator layer = m_layers.begin(); layer != m_layers.end(); layer++)
		wc += layer->m_neurons.size() * layer->m_neurons.begin()->m_weights.size(); // We assume that every node in a layer has the same number of weights
	return wc;
}

void GNeuralNet::weights(double* pOutWeights)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	for(vector<GNeuralNetLayer>::iterator layer = m_layers.begin(); layer != m_layers.end(); layer++)
	{
		for(vector<GNeuron>::iterator neuron = layer->m_neurons.begin(); neuron != layer->m_neurons.end(); neuron++)
		{
			for(vector<double>::iterator weight = neuron->m_weights.begin(); weight != neuron->m_weights.end(); weight++)
			{
				*pOutWeights = *weight;
				pOutWeights++;
			}
		}
	}
}

void GNeuralNet::setWeights(const double* pWeights)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	for(vector<GNeuralNetLayer>::iterator layer = m_layers.begin(); layer != m_layers.end(); layer++)
	{
		for(vector<GNeuron>::iterator neuron = layer->m_neurons.begin(); neuron != layer->m_neurons.end(); neuron++)
		{
			for(vector<double>::iterator weight = neuron->m_weights.begin(); weight != neuron->m_weights.end(); weight++)
			{
				*weight = *pWeights;
				pWeights++;
			}
		}
	}
}

void GNeuralNet::copyWeights(GNeuralNet* pOther)
{
	if(!hasTrainingBegun() || !pOther->hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called on both networks before this method");
	GAssert(m_layers.size() == pOther->m_layers.size());
	vector<GNeuralNetLayer>::iterator layerOther = pOther->m_layers.begin();
	for(vector<GNeuralNetLayer>::iterator layer = m_layers.begin(); layer != m_layers.end(); layer++)
	{
		GAssert(layer->m_neurons.size() == layerOther->m_neurons.size());
		vector<GNeuron>::iterator neuronOther = layerOther->m_neurons.begin();
		for(vector<GNeuron>::iterator neuron = layer->m_neurons.begin(); neuron != layer->m_neurons.end(); neuron++)
		{
			GAssert(neuron->m_weights.size() == neuronOther->m_weights.size());
			vector<double>::iterator weightOther = neuronOther->m_weights.begin();
			for(vector<double>::iterator weight = neuron->m_weights.begin(); weight != neuron->m_weights.end(); weight++)
				*weight = *(weightOther++);
			neuronOther++;
		}
		layerOther++;
	}
}

void GNeuralNet::copyStructure(GNeuralNet* pOther)
{
	if(!pOther->hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	releaseTrainingJunk();
	for(vector<GActivationFunction*>::iterator it = m_activationFunctions.begin(); it != m_activationFunctions.end(); it++)
		delete(*it);
	m_pActivationFunction = NULL;
	m_layers.resize(pOther->m_layers.size());
	for(size_t i = 0; i < m_layers.size(); i++)
	{
		if(pOther->m_layers[i].m_pActivationFunction != m_pActivationFunction)
		{
			setActivationFunction(pOther->m_layers[i].m_pActivationFunction->clone(), true);
			m_layers[i].m_pActivationFunction = m_pActivationFunction;
			setActivationFunction(pOther->m_layers[i].m_pActivationFunction, false);
		}
		else
			m_layers[i].m_pActivationFunction = m_layers[i - 1].m_pActivationFunction;
		m_layers[i].m_neurons.resize(pOther->m_layers[i].m_neurons.size());
		for(size_t j = 0; j < m_layers[i].m_neurons.size(); j++)
			m_layers[i].m_neurons[j].m_weights.resize(pOther->m_layers[i].m_neurons[j].m_weights.size());
	}
	setActivationFunction(m_layers[m_layers.size() - 1].m_pActivationFunction, false);
	m_internalFeatureDims = pOther->m_internalFeatureDims;
	m_internalLabelDims = pOther->m_internalLabelDims;
	m_learningRate = pOther->m_learningRate;
	m_momentum = pOther->m_momentum;
	m_validationPortion = pOther->m_validationPortion;
	m_minImprovement = pOther->m_minImprovement;
	m_epochsPerValidationCheck = pOther->m_epochsPerValidationCheck;
	m_backPropTargetFunction = pOther->m_backPropTargetFunction;
	if(pOther->m_pBackProp)
		m_pBackProp = new GBackProp(this);
}

void GNeuralNet::perturbAllWeights(double deviation)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	for(vector<GNeuralNetLayer>::iterator layer = m_layers.begin(); layer != m_layers.end(); layer++)
	{
		for(vector<GNeuron>::iterator neuron = layer->m_neurons.begin(); neuron != layer->m_neurons.end(); neuron++)
			for(vector<double>::iterator weight = neuron->m_weights.begin(); weight != neuron->m_weights.end(); weight++)
				(*weight) += (m_rand.normal() * deviation);
	}
}

void GNeuralNet::clipWeights(double max)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	for(vector<GNeuralNetLayer>::iterator layer = m_layers.begin(); layer != m_layers.end(); layer++)
	{
		for(vector<GNeuron>::iterator neuron = layer->m_neurons.begin(); neuron != layer->m_neurons.end(); neuron++)
		{
			for(vector<double>::iterator weight = neuron->m_weights.begin() + 1; weight != neuron->m_weights.end(); weight++)
				(*weight) = std::max(-max, std::min(max, *weight));
		}
	}
}

void GNeuralNet::swapNodes(size_t layer, size_t a, size_t b)
{
	GNeuralNetLayer& layerCur = m_layers[layer];
	std::swap(layerCur.m_neurons[a], layerCur.m_neurons[b]);
	if(layer < m_layers.size())
	{
		GNeuralNetLayer& layerNext = m_layers[layer + 1];
		for(vector<GNeuron>::iterator it = layerNext.m_neurons.begin(); it != layerNext.m_neurons.end(); it++)
			std::swap(it->m_weights[a + 1], it->m_weights[b + 1]);
	}
}

void GNeuralNet::align(GNeuralNet& that)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	if(layerCount() != that.layerCount())
		ThrowError("mismatching number of layers");
	for(size_t i = 0; i + 1 < m_layers.size(); i++)
	{
		// Copy weights into matrices
		GNeuralNetLayer& layerThisCur = m_layers[i];
		GNeuralNetLayer& layerThatCur = that.m_layers[i];
		if(layerThisCur.m_neurons.size() != layerThatCur.m_neurons.size())
			ThrowError("mismatching layer size");
		GMatrix thisWeights(layerThisCur.m_neurons.size(), layerThisCur.m_neurons[0].m_weights.size());
		GMatrix thatWeights(layerThisCur.m_neurons.size(), layerThisCur.m_neurons[0].m_weights.size());
		for(size_t j = 0; j < layerThisCur.m_neurons.size(); j++)
		{
			GNeuron& nThis = layerThisCur.m_neurons[j];
			GNeuron& nThat = layerThatCur.m_neurons[j];
			double* pThisRow = thisWeights.row(j);
			double* pThatRow = thatWeights.row(j);
			vector<double>::iterator wThis = nThis.m_weights.begin();
			vector<double>::iterator wThat = nThat.m_weights.begin();
			while(wThis != nThis.m_weights.end())
			{
				*(pThisRow++) = *(wThis++);
				*(pThatRow++) = *(wThat++);
			}
		}

		// Do bipartite matching
		GRowDistance metric;
		size_t* pIndexes = GMatrix::bipartiteMatching(thatWeights, thisWeights, metric);
		ArrayHolder<size_t> hIndexes(pIndexes);

		// Align this layer with that layer
		for(size_t j = 0; j < thisWeights.rows(); j++)
		{
			size_t k = pIndexes[j];
			if(k != j)
			{
				// Fix up the indexes
				size_t m = j + 1;
				for( ; m < thisWeights.rows(); m++)
				{
					if(pIndexes[m] == j)
						break;
				}
				GAssert(m < thisWeights.rows());
				pIndexes[m] = k;

				// Swap nodes j and k
				swapNodes(i, j, k);
			}
		}
	}
}

void GNeuralNet::decayWeights(double lambda, double gamma)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	for(vector<GNeuralNetLayer>::iterator layer = m_layers.begin(); layer != m_layers.end(); layer++)
	{
		double d = (1.0 - lambda * m_learningRate);
		for(vector<GNeuron>::iterator neuron = layer->m_neurons.begin(); neuron != layer->m_neurons.end(); neuron++)
		{
			for(vector<double>::iterator weight = neuron->m_weights.begin() + 1; weight != neuron->m_weights.end(); weight++)
				*weight *= d;
		}
		lambda *= gamma;
	}
}

void GNeuralNet::decayWeightsSingleOutput(size_t output, double lambda)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	double d = (1.0 - lambda * m_learningRate);
	GNeuron& neuron = layer(m_layers.size() - 1).m_neurons[output];
	for(vector<double>::iterator weight = neuron.m_weights.begin(); weight != neuron.m_weights.end(); weight++)
		(*weight) *= d;
	for(size_t l = m_layers.size() - 2; l < m_layers.size(); l--)
	{
		GNeuralNetLayer& layer = m_layers[l];
		double d = (1.0 - lambda * m_learningRate);
		for(vector<GNeuron>::iterator neuron = layer.m_neurons.begin(); neuron != layer.m_neurons.end(); neuron++)
		{
			for(vector<double>::iterator weight = neuron->m_weights.begin() + 1; weight != neuron->m_weights.end(); weight++)
				*weight *= d;
		}
	}
}

void GNeuralNet::forwardProp(const double* pRow)
{
	// Propagate from the feature vector to the first layer
	vector<GNeuralNetLayer>::iterator pLayer = m_layers.begin();
	double net;
	for(vector<GNeuron>::iterator i = pLayer->m_neurons.begin(); i != pLayer->m_neurons.end(); i++)
	{
		vector<double>::iterator j = i->m_weights.begin();
		net = *(j++); // (the first weight is the bias)
		const double* pR = pRow;
		if(m_useInputBias)
			net += *(pR++);
		while(j != i->m_weights.end())
		{
			if(*pR != UNKNOWN_REAL_VALUE)
				net += (*j) * (*pR);
			j++;
			pR++;
		}
		i->m_net = net;
		i->m_activation = pLayer->m_pActivationFunction->squash(net);
	}

	// Do the rest of the hidden layers
	vector<GNeuralNetLayer>::iterator pPrevLayer = pLayer;
	for(pLayer++; pLayer != m_layers.end(); pLayer++)
	{
		for(vector<GNeuron>::iterator i = pLayer->m_neurons.begin(); i != pLayer->m_neurons.end(); i++)
		{
			vector<double>::iterator j = i->m_weights.begin();
			net = *(j++); // (the first weight is the bias)
			vector<GNeuron>::iterator k = pPrevLayer->m_neurons.begin();
			while(k != pPrevLayer->m_neurons.end())
			{
				net += *(j++) * k->m_activation;
				k++;
			}
			i->m_net = net;
			i->m_activation = pLayer->m_pActivationFunction->squash(net);
		}
		pPrevLayer = pLayer;
	}
}

double GNeuralNet::forwardPropSingleOutput(const double* pRow, size_t output)
{
	vector<GNeuralNetLayer>::iterator pLayer = m_layers.begin();
	GAssert(pLayer->m_pActivationFunction);
	if(pLayer + 1 == m_layers.end())
	{
		// Propagate from the feature vector to the specified output node
		GNeuron& neuron = pLayer->m_neurons[output];
		vector<double>::iterator j = neuron.m_weights.begin();
		double net = *(j++); // (the first weight is the bias)
		if(m_useInputBias)
			net += *(pRow++);
		while(j != neuron.m_weights.end())
			net += *(j++) * *(pRow++);
		neuron.m_net = net;
		neuron.m_activation = pLayer->m_pActivationFunction->squash(net);
		return neuron.m_activation;
	}
	else
	{
		// Propagate from the feature vector to the first layer
		double net;
		for(vector<GNeuron>::iterator i = pLayer->m_neurons.begin(); i != pLayer->m_neurons.end(); i++)
		{
			vector<double>::iterator j = i->m_weights.begin();
			net = *(j++); // (the first weight is the bias)
			const double* pR = pRow;
			if(m_useInputBias)
				net += *(pR++);
			while(j != i->m_weights.end())
				net += *(j++) * *(pR++);
			i->m_net = net;
			i->m_activation = pLayer->m_pActivationFunction->squash(net);
		}

		// Do the rest of the hidden layers
		vector<GNeuralNetLayer>::iterator pPrevLayer = pLayer;
		for(pLayer++; true; pLayer++)
		{
			if(pLayer + 1 == m_layers.end())
			{
				GNeuron& neuron = pLayer->m_neurons[output];
				vector<double>::iterator j = neuron.m_weights.begin();
				net = *(j++); // (the first weight is the bias)
				vector<GNeuron>::iterator k = pPrevLayer->m_neurons.begin();
				while(k != pPrevLayer->m_neurons.end())
				{
					net += *(j++) * k->m_activation;
					k++;
				}
				neuron.m_net = net;
				neuron.m_activation = pLayer->m_pActivationFunction->squash(net);
				return neuron.m_activation;
			}
			else
			{
				for(vector<GNeuron>::iterator i = pLayer->m_neurons.begin(); i != pLayer->m_neurons.end(); i++)
				{
					vector<double>::iterator j = i->m_weights.begin();
					net = *(j++); // (the first weight is the bias)
					vector<GNeuron>::iterator k = pPrevLayer->m_neurons.begin();
					while(k != pPrevLayer->m_neurons.end())
					{
						net += *(j++) * k->m_activation;
						k++;
					}
					i->m_net = net;
					i->m_activation = pLayer->m_pActivationFunction->squash(net);
				}
				pPrevLayer = pLayer;
			}
		}
	}
}

// virtual
void GNeuralNet::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");

	// Do the evaluation
	forwardProp(pIn);

	// Convert outputs to external data
	GNeuralNetLayer& outputLayer = m_layers[m_layers.size() - 1];
	for(vector<GNeuron>::iterator i = outputLayer.m_neurons.begin(); i != outputLayer.m_neurons.end(); i++)
	{
		GNormalDistribution* pNorm = pOut->makeNormal();
		pNorm->setMeanAndVariance(i->m_activation, 1.0);
		pOut++;
	}
}

void GNeuralNet::copyPrediction(double* pOut)
{
	GNeuralNetLayer& outputLayer = m_layers[m_layers.size() - 1];
	for(vector<GNeuron>::iterator i = outputLayer.m_neurons.begin(); i != outputLayer.m_neurons.end(); i++)
		*(pOut++) = i->m_activation;
}

double GNeuralNet::sumSquaredPredictionError(const double* pTarget)
{
	GNeuralNetLayer& outputLayer = m_layers[m_layers.size() - 1];
	double sse = 0.0;
	for(vector<GNeuron>::iterator i = outputLayer.m_neurons.begin(); i != outputLayer.m_neurons.end(); i++)
	{
		double d = *(pTarget++) - i->m_activation;
		sse += (d * d);
	}
	return sse;
}

// virtual
void GNeuralNet::predictInner(const double* pIn, double* pOut)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	forwardProp(pIn);
	copyPrediction(pOut);
}

// virtual
void GNeuralNet::trainInner(GMatrix& features, GMatrix& labels)
{
	size_t validationRows = (size_t)(m_validationPortion * features.rows());
	if(validationRows > 0)
	{
		GMatrix validateFeatures(features.relation());
		GMatrix validateLabels(labels.relation());
		{
			GMergeDataHolder hFeatures(features, validateFeatures);
			GMergeDataHolder hLabels(labels, validateLabels);
			features.splitBySize(&validateFeatures, validationRows);
			labels.splitBySize(&validateLabels, validationRows);
			trainWithValidation(features, labels, validateFeatures, validateLabels);
		}
	}
	else
		trainWithValidation(features, labels, features, labels);
}

// virtual
void GNeuralNet::trainSparse(GSparseMatrix& features, GMatrix& labels)
{
	if(features.rows() != labels.rows())
		ThrowError("Expected the features and labels to have the same number of rows");
	sp_relation pFeatureRel = new GUniformRelation(features.cols());
	beginIncrementalLearning(pFeatureRel, labels.relation());

	GTEMPBUF(size_t, indexes, features.rows());
	GIndexVec::makeIndexVec(indexes, features.rows());
	GTEMPBUF(double, pFullRow, features.cols());
	for(size_t epochs = 0; epochs < 100; epochs++) // todo: need a better stopping criterion
	{
		GIndexVec::shuffle(indexes, features.rows(), &m_rand);
		for(size_t i = 0; i < features.rows(); i++)
		{
			features.fullRow(pFullRow, indexes[i]);
			forwardProp(pFullRow);
			setErrorOnOutputLayer(labels.row(indexes[i]), m_backPropTargetFunction);
			m_pBackProp->backpropagate();
			m_pBackProp->descendGradient(pFullRow, m_learningRate, m_momentum, m_useInputBias);
		}
	}
}

double GNeuralNet::validationSquaredError(GMatrix& features, GMatrix& labels)
{
	double sse = 0;
	GNeuralNetLayer& outputLayer = m_layers[m_layers.size() - 1];
	size_t nCount = features.rows();
	for(size_t n = 0; n < nCount; n++)
	{
		forwardProp(features[n]);
		const double* pLabels = labels[n];
		for(size_t i = 0; i < m_internalLabelDims; i++)
		{
			double d = *(pLabels++) - outputLayer.m_neurons[i].m_activation;
			sse += (d * d);
		}
	}
	return sse;
}

size_t GNeuralNet::trainWithValidation(GMatrix& trainFeatures, GMatrix& trainLabels, GMatrix& validateFeatures, GMatrix& validateLabels)
{
	if(trainFeatures.rows() != trainLabels.rows() || validateFeatures.rows() != validateLabels.rows())
		ThrowError("Expected the features and labels to have the same number of rows");
	beginIncrementalLearningInner(trainFeatures.relation(), trainLabels.relation());

	// Make a random ordering
	size_t rowCount = trainFeatures.rows();
	size_t* pIndexes = new size_t[rowCount];
	ArrayHolder<size_t> hIndexes(pIndexes);
	GIndexVec::makeIndexVec(pIndexes, rowCount);

	// Do the epochs
	size_t nEpochs;
	double dBestError = 1e308;
	size_t nEpochsSinceValidationCheck = 0;
	double dSumSquaredError;
	for(nEpochs = 0; true; nEpochs++)
	{
		GIndexVec::shuffle(pIndexes, rowCount, &m_rand);
		size_t* pIndex = pIndexes;
		for(size_t n = 0; n < rowCount; n++)
		{
			const double* pFeatures = trainFeatures[*pIndex];
			forwardProp(pFeatures);
			setErrorOnOutputLayer(trainLabels[*pIndex], m_backPropTargetFunction);
			m_pBackProp->backpropagate();
			m_pBackProp->descendGradient(pFeatures, m_learningRate, m_momentum, m_useInputBias);
			pIndex++;
		}

		// Check for termination condition
		if(nEpochsSinceValidationCheck >= m_epochsPerValidationCheck)
		{
			nEpochsSinceValidationCheck = 0;
			dSumSquaredError = validationSquaredError(validateFeatures, validateLabels);
			if(1.0 - dSumSquaredError / dBestError < m_minImprovement)
				break;
			if(dSumSquaredError < dBestError)
				dBestError = dSumSquaredError;
		}
		else
			nEpochsSinceValidationCheck++;
	}

	releaseTrainingJunk();
	return nEpochs;
}

// virtual
void GNeuralNet::beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel)
{
	if(pLabelRel->size() < 1)
		ThrowError("The label relation must have at least 1 attribute");
	if(!pFeatureRel->areContinuous(0, pFeatureRel->size()) || !pLabelRel->areContinuous(0, pLabelRel->size()))
		ThrowError("Only continuous values are supported. (Using the GNominalToCat transform may be a good solution to this problem.)");

	// Adjust the size of the output layer
	m_internalFeatureDims = pFeatureRel->size();
	m_internalLabelDims = pLabelRel->size();
	GNeuralNetLayer& layerOut = m_layers[m_layers.size() - 1];
	layerOut.m_neurons.resize(m_internalLabelDims);
	if(!m_pActivationFunction)
		setActivationFunction(new GActivationLogistic(), true);
	layerOut.m_pActivationFunction = m_pActivationFunction;
	if(m_layers.size() > 1)
	{
		// Establish the number of weights on the output layer
		size_t weightCount = 1 + m_layers[m_layers.size() - 2].m_neurons.size();
		for(size_t i = 0; i < m_internalLabelDims; i++)
			layerOut.m_neurons[i].m_weights.resize(weightCount);
	}

	// Establish the number of weights on the first layer
	GNeuralNetLayer& layerIn = m_layers[0];
	for(size_t i = 0; i < layerIn.m_neurons.size(); i++)
		layerIn.m_neurons[i].m_weights.resize(1 + m_internalFeatureDims - (m_useInputBias ? 1 : 0));

	// Initialize the weights with small random values
	double inputCenter = 0.5; // Assume inputs have a range from 0 to 1. If this not correct, learning may be slightly slower.
	for(size_t i = 0; i < m_layers.size(); i++)
	{
		m_layers[i].resetWeights(&m_rand, inputCenter);
		inputCenter = m_layers[i].m_pActivationFunction->center();
	}

	// Make the training junk
	releaseTrainingJunk();
	m_pBackProp = new GBackProp(this);
}

// virtual
void GNeuralNet::trainIncrementalInner(const double* pIn, const double* pOut)
{
	if(!hasTrainingBegun())
		ThrowError("train or beginIncrementalLearning must be called before this method");
	forwardProp(pIn);
	setErrorOnOutputLayer(pOut, m_backPropTargetFunction);
	m_pBackProp->backpropagate();
	m_pBackProp->descendGradient(pIn, m_learningRate, m_momentum, m_useInputBias);
}

void GNeuralNet::setErrorOnOutputLayer(const double* pTarget, TargetFunction eTargetFunction)
{
	// Compute error on output layer
	GBackPropLayer& bpOutputLayer = m_pBackProp->layer(m_layers.size() - 1);
	GNeuralNetLayer& nnOutputLayer = m_layers[m_layers.size() - 1];
	switch(eTargetFunction)
	{
		case squared_error:
			{
				vector<GNeuron>::iterator itNN = nnOutputLayer.m_neurons.begin();
				vector<GBackPropNeuron>::iterator itBP = bpOutputLayer.m_neurons.begin();
				while(itNN != nnOutputLayer.m_neurons.end())
				{
					if(*pTarget == UNKNOWN_REAL_VALUE)
						itBP->m_error = 0.0;
					else
						itBP->m_error = (*pTarget - itNN->m_activation) * nnOutputLayer.m_pActivationFunction->derivativeOfNet(itNN->m_net, itNN->m_activation);
					pTarget++;
					itNN++;
					itBP++;
				}
			}
			break;

		case cross_entropy:
			{
				vector<GNeuron>::iterator itNN = nnOutputLayer.m_neurons.begin();
				vector<GBackPropNeuron>::iterator itBP = bpOutputLayer.m_neurons.begin();
				while(itNN != nnOutputLayer.m_neurons.end())
				{
					if(*pTarget == UNKNOWN_REAL_VALUE)
						itBP->m_error = 0.0;
					else
						itBP->m_error = *pTarget - itNN->m_activation;
					pTarget++;
					itNN++;
					itBP++;
				}
			}
			break;

		case sign:
			{
				vector<GNeuron>::iterator itNN = nnOutputLayer.m_neurons.begin();
				vector<GBackPropNeuron>::iterator itBP = bpOutputLayer.m_neurons.begin();
				while(itNN != nnOutputLayer.m_neurons.end())
				{
					if(*pTarget == UNKNOWN_REAL_VALUE)
						itBP->m_error = 0.0;
					else
						itBP->m_error = nnOutputLayer.m_pActivationFunction->derivativeOfNet(itNN->m_net, itNN->m_activation) * (*pTarget >= itNN->m_activation ? 0.1 : -0.1);
					pTarget++;
					itNN++;
					itBP++;
				}
			}

		case physical:
			{
				double force = 0.0;
				const double* pT = pTarget;
				for(vector<GNeuron>::iterator itNN = nnOutputLayer.m_neurons.begin(); itNN != nnOutputLayer.m_neurons.end(); itNN++)
				{
					double d = *(pT++) - itNN->m_activation;
					force += (d * d);
				}
				force = std::min(1.0, 0.0001 / force);
				vector<GNeuron>::iterator itNN = nnOutputLayer.m_neurons.begin();
				vector<GBackPropNeuron>::iterator itBP = bpOutputLayer.m_neurons.begin();
				while(itNN != nnOutputLayer.m_neurons.end())
				{
					if(*pTarget == UNKNOWN_REAL_VALUE)
						itBP->m_error = 0.0;
					else
						itBP->m_error = force * (*pTarget - itNN->m_activation) * nnOutputLayer.m_pActivationFunction->derivativeOfNet(itNN->m_net, itNN->m_activation);
					pTarget++;
					itNN++;
					itBP++;
				}
			}
			break;

		default:
			ThrowError("Unrecognized target function for back-propagation");
			break;
	}
}

void GNeuralNet::setErrorSingleOutput(double target, size_t output, TargetFunction eTargetFunction)
{
	// Compute error on output layer
	GBackPropLayer& bpOutputLayer = m_pBackProp->layer(m_layers.size() - 1);
	GBackPropNeuron& bpNeuron = bpOutputLayer.m_neurons[output];
	GNeuralNetLayer& nnOutputLayer = m_layers[m_layers.size() - 1];
	GNeuron& nnNeuron = nnOutputLayer.m_neurons[output];
	switch(eTargetFunction)
	{
		case squared_error:
			bpNeuron.m_error = (target - nnNeuron.m_activation) * nnOutputLayer.m_pActivationFunction->derivativeOfNet(nnNeuron.m_net, nnNeuron.m_activation);
			break;

		case cross_entropy:
			bpNeuron.m_error = target - nnNeuron.m_activation;
			break;

		case sign:
			bpNeuron.m_error = nnOutputLayer.m_pActivationFunction->derivativeOfNet(nnNeuron.m_net, nnNeuron.m_activation) * (target >= nnNeuron.m_activation ? 0.1 : -0.1);

		case physical:
			// Note: Physical error requires knowing all the outputs, but this hack sort of approximates it without knowing them.
			bpNeuron.m_error = GMath::signedRoot(target - nnNeuron.m_activation) * nnOutputLayer.m_pActivationFunction->derivativeOfNet(nnNeuron.m_net, nnNeuron.m_activation);
			break;

		default:
			ThrowError("Unrecognized target function for back-propagation");
			break;
	}
}

void GNeuralNet::autoTune(GMatrix& features, GMatrix& labels)
{
	// Try a plain-old single-layer network
	size_t hidden = std::max((size_t)4, (features.cols() + 3) / 4);
	Holder<GNeuralNet> hCand0(new GNeuralNet(m_rand));
	Holder<GNeuralNet> hCand1;
	double scores[2];
	scores[0] = hCand0.get()->heuristicValidate(features, labels);
	scores[1] = 1e308;

	// Try increasing the number of hidden units until accuracy decreases twice
	size_t failures = 0;
	while(true)
	{
		GNeuralNet* cand = new GNeuralNet(m_rand);
		cand->addLayer(hidden);
		double d = cand->heuristicValidate(features, labels);
		if(d < scores[0])
		{
			hCand1.reset(hCand0.release());
			scores[1] = scores[0];
			hCand0.reset(cand);
			scores[0] = d;
		}
		else
		{
			if(d < scores[1])
			{
				hCand1.reset(cand);
				scores[1] = d;
			}
			else
				delete(cand);
			if(++failures >= 2)
				break;
		}
		hidden *= 4;
	}

	// Try narrowing in on the best number of hidden units
	while(true)
	{
		size_t a = hCand0.get()->layerCount() > 1 ? hCand0.get()->layer(0).m_neurons.size() : 0;
		size_t b = hCand1.get()->layerCount() > 1 ? hCand1.get()->layer(0).m_neurons.size() : 0;
		size_t dif = b < a ? a - b : b - a;
		if(dif <= 1)
			break;
		size_t c = (a + b) / 2;
		GNeuralNet* cand = new GNeuralNet(m_rand);
		cand->addLayer(c);
		double d = cand->heuristicValidate(features, labels);
		if(d < scores[0])
		{
			hCand1.reset(hCand0.release());
			scores[1] = scores[0];
			hCand0.reset(cand);
			scores[0] = d;
		}
		else if(d < scores[1])
		{
			hCand1.reset(cand);
			scores[1] = d;
		}
		else
		{
			delete(cand);
			break;
		}
	}
	hCand1.reset(NULL);

	// Try two hidden layers
	size_t hu1 = hCand0.get()->layerCount() > 1 ? hCand0.get()->layer(0).m_neurons.size() : 0;
	size_t hu2 = 0;
	if(hu1 > 12)
	{
		size_t c1 = 16;
		size_t c2 = 16;
		if(labels.cols() < features.cols())
		{
			double d = sqrt(double(features.cols()) / labels.cols());
			c1 = std::max(size_t(9), size_t(double(features.cols()) / d));
			c2 = size_t(labels.cols() * d);
		}
		else
		{
			double d = sqrt(double(labels.cols()) / features.cols());
			c1 = size_t(features.cols() * d);
			c2 = std::max(size_t(9), size_t(double(labels.cols()) / d));
		}
		if(c1 < 16 && c2 < 16)
		{
			c1 = 16;
			c2 = 16;
		}
		GNeuralNet* cand = new GNeuralNet(m_rand);
		cand->addLayer(c1);
		cand->addLayer(c2);
		double d = cand->heuristicValidate(features, labels);
		if(d < scores[0])
		{
			hCand0.reset(cand);
			scores[0] = d;
			hu1 = c1;
			hu2 = c2;
		}
		else
			delete(cand);
	}

	// Try a gaussian activation function
	GActivationFunction* pActiv = new GActivationLogistic();
	{
		GNeuralNet* cand = new GNeuralNet(m_rand);
		cand->setActivationFunction(new GActivationGaussian(), true);
		if(hu1 > 0) cand->addLayer(hu1);
		if(hu2 > 0) cand->addLayer(hu2);
		double d = cand->heuristicValidate(features, labels);
		if(d < scores[0])
		{
			hCand0.reset(cand);
			scores[0] = d;
			delete(pActiv);
			pActiv = new GActivationGaussian();
		}
		else
			delete(cand);
	}

	// Try with momentum
	{
		GNeuralNet* cand = new GNeuralNet(m_rand);
		cand->setActivationFunction(pActiv, false);
		if(hu1 > 0) cand->addLayer(hu1);
		if(hu2 > 0) cand->addLayer(hu2);
		cand->setMomentum(0.8);
		double d = cand->heuristicValidate(features, labels);
		if(d < scores[0])
		{
			hCand0.reset(cand);
			scores[0] = d;
		}
		else
			delete(cand);
	}

	delete(pActiv);
	copyStructure(hCand0.get());
}

#ifndef NO_TEST_CODE
void GNeuralNet_testMath()
{
	GMatrix features(0, 2);
	double* pVec = features.newRow();
	pVec[0] = 0.0;
	pVec[1] = -0.7;
	GMatrix labels(0, 1);
	labels.newRow()[0] = 1.0;

	// Make the Neural Network
	GRand prng(0);
	GNeuralNet nn(prng);
	nn.setLearningRate(0.175);
	nn.setMomentum(0.9);
	nn.addLayer(3);
	nn.beginIncrementalLearning(features.relation(), labels.relation());
	if(nn.countWeights() != 13)
		ThrowError("Wrong number of weights");
	GNeuralNetLayer& layerOut = nn.layer(1);
	layerOut.m_neurons[0].m_weights[0] = 0.02; // w_0
	layerOut.m_neurons[0].m_weights[1] = -0.01; // w_1
	layerOut.m_neurons[0].m_weights[2] = 0.03; // w_2
	layerOut.m_neurons[0].m_weights[3] = 0.02; // w_3
	GNeuralNetLayer& layerHidden = nn.layer(0);
	layerHidden.m_neurons[0].m_weights[0] = -0.01; // w_4
	layerHidden.m_neurons[0].m_weights[1] = -0.03; // w_5
	layerHidden.m_neurons[0].m_weights[2] = 0.03; // w_6
	layerHidden.m_neurons[1].m_weights[0] = 0.01; // w_7
	layerHidden.m_neurons[1].m_weights[1] = 0.04; // w_8
	layerHidden.m_neurons[1].m_weights[2] = -0.02; // w_9
	layerHidden.m_neurons[2].m_weights[0] = -0.02; // w_10
	layerHidden.m_neurons[2].m_weights[1] = 0.03; // w_11
	layerHidden.m_neurons[2].m_weights[2] = 0.02; // w_12

	bool useCrossEntropy = false;

	// Test forward prop
	double tol = 1e-12;
	double pat[3];
	GVec::copy(pat, features[0], 2);
	nn.predict(pat, pat + 2);
	// Here is the math (done by hand) for why these results are expected:
	// Row: {0, -0.7, 1}
	// o_1 = squash(w_4*1+w_5*x+w_6*y) = 1/(1+exp(-(-.01*1-.03*0+.03*(-.7)))) = 0.4922506205862
	// o_2 = squash(w_7*1+w_8*x+w_9*y) = 1/(1+exp(-(.01*1+.04*0-.02*(-.7)))) = 0.50599971201659
	// o_3 = squash(w_10*1+w_11*x+w_12*y) = 1/(1+exp(-(-.02*1+.03*0+.02*(-.7)))) = 0.49150081873869
	// o_0 = squash(w_0*1+w_1*o_1+w_2*o_2+w_3*o_3) = 1/(1+exp(-(.02*1-.01*.4922506205862+.03*.50599971201659+.02*.49150081873869))) = 0.51002053349535
	if(std::abs(pat[2] - 0.51002053349535) > tol) ThrowError("forward prop problem");

	// Test that the output error is computed properly
	nn.trainIncremental(features[0], labels[0]);
	GBackProp* pBP = nn.backProp();
	// Here is the math (done by hand) for why these results are expected:
	// e_0 = output*(1-output)*(target-output) = .51002053349535*(1-.51002053349535)*(1-.51002053349535) = 0.1224456672531
	if(useCrossEntropy)
	{
		// Here is the math for why these results are expected:
		// e_0 = target-output = 1-.51002053349535 = 0.4899794665046473
		if(std::abs(pBP->layer(1).m_neurons[0].m_error - 0.4899794665046473) > tol) ThrowError("problem computing output error");
	}
	else
	{
		// Here is the math for why these results are expected:
		// e_0 = output*(1-output)*(target-output) = .51002053349535*(1-.51002053349535)*(1-.51002053349535) = 0.1224456672531
		if(std::abs(pBP->layer(1).m_neurons[0].m_error - 0.1224456672531) > tol) ThrowError("problem computing output error");
	}

	// Test Back Prop
	if(useCrossEntropy)
	{
		if(std::abs(pBP->layer(0).m_neurons[0].m_error + 0.0012246544194742083) > tol) ThrowError("back prop problem");
		// e_2 = o_2*(1-o_2)*(w_2*e_0) = 0.00091821027577176
		if(std::abs(pBP->layer(0).m_neurons[1].m_error - 0.0036743168717579557) > tol) ThrowError("back prop problem");
		// e_3 = o_3*(1-o_3)*(w_3*e_0) = 0.00061205143636003
		if(std::abs(pBP->layer(0).m_neurons[2].m_error - 0.002449189448583718) > tol) ThrowError("back prop problem");
	}
	else
	{
		// e_1 = o_1*(1-o_1)*(w_1*e_0) = .4922506205862*(1-.4922506205862)*(-.01*.1224456672531) = -0.00030604063598154
		if(std::abs(pBP->layer(0).m_neurons[0].m_error + 0.00030604063598154) > tol) ThrowError("back prop problem");
		// e_2 = o_2*(1-o_2)*(w_2*e_0) = 0.00091821027577176
		if(std::abs(pBP->layer(0).m_neurons[1].m_error - 0.00091821027577176) > tol) ThrowError("back prop problem");
		// e_3 = o_3*(1-o_3)*(w_3*e_0) = 0.00061205143636003
		if(std::abs(pBP->layer(0).m_neurons[2].m_error - 0.00061205143636003) > tol) ThrowError("back prop problem");
	}

	// Test weight update
	if(useCrossEntropy)
	{
		if(std::abs(layerOut.m_neurons[0].m_weights[0] - 0.10574640663831328) > tol) ThrowError("weight update problem");
		if(std::abs(layerOut.m_neurons[0].m_weights[1] - 0.032208721880745944) > tol) ThrowError("weight update problem");
	}
	else
	{
		// d_0 = (d_0*momentum)+(learning_rate*e_0*1) = 0*.9+.175*.1224456672531*1
		// w_0 = w_0 + d_0 = .02+.0214279917693 = 0.041427991769293
		if(std::abs(layerOut.m_neurons[0].m_weights[0] - 0.041427991769293) > tol) ThrowError("weight update problem");
		// d_1 = (d_1*momentum)+(learning_rate*e_0*o_1) = 0*.9+.175*.1224456672531*.4922506205862
		// w_1 = w_1 + d_1 = -.01+.0105479422563 = 0.00054794224635029
		if(std::abs(layerOut.m_neurons[0].m_weights[1] - 0.00054794224635029) > tol) ThrowError("weight update problem");
		if(std::abs(layerOut.m_neurons[0].m_weights[2] - 0.040842557664356) > tol) ThrowError("weight update problem");
		if(std::abs(layerOut.m_neurons[0].m_weights[3] - 0.030531875498533) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[0].m_weights[0] + 0.010053557111297) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[0].m_weights[1] + 0.03) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[0].m_weights[2] - 0.030037489977908) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[1].m_weights[0] - 0.01016068679826) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[1].m_weights[1] - 0.04) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[1].m_weights[2] + 0.020112480758782) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[2].m_weights[0] + 0.019892890998637) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[2].m_weights[1] - 0.03) > tol) ThrowError("weight update problem");
		if(std::abs(layerHidden.m_neurons[2].m_weights[2] - 0.019925023699046) > tol) ThrowError("weight update problem");
	}
}

void GNeuralNet_testInputGradient(GRand* pRand)
{
	for(int i = 0; i < 20; i++)
	{
		// Make the neural net
		GNeuralNet nn(*pRand);
//		nn.addLayer(5);
//		nn.addLayer(10);
		sp_relation pFeatureRel = new GUniformRelation(5);
		sp_relation pLabelRel = new GUniformRelation(10);
		nn.beginIncrementalLearning(pFeatureRel, pLabelRel);

		// Init with random weights
		size_t weightCount = nn.countWeights();
		double* pWeights = new double[weightCount + 5 + 10 + 10 + 5 + 5];
		ArrayHolder<double> hWeights(pWeights);
		double* pFeatures = pWeights + weightCount;
		double* pTarget = pFeatures + 5;
		double* pOutput = pTarget + 10;
		double* pFeatureGradient = pOutput + 10;
		double* pEmpiricalGradient = pFeatureGradient + 5;
		for(size_t j = 0; j < weightCount; j++)
			pWeights[j] = pRand->normal() * 0.8;
		nn.setWeights(pWeights);

		// Compute target output
		GVec::setAll(pFeatures, 0.0, 5);
		nn.predict(pFeatures, pTarget);

		// Move away from the goal and compute baseline error
		for(int i = 0; i < 5; i++)
			pFeatures[i] += pRand->normal() * 0.1;
		nn.predict(pFeatures, pOutput);
		double sseBaseline = GVec::squaredDistance(pTarget, pOutput, 10);

		// Compute the feature gradient
		nn.forwardProp(pFeatures);
		nn.setErrorOnOutputLayer(pTarget);
		nn.backProp()->backpropagate();
		GVec::copy(pFeatureGradient, pFeatures, 5);
		nn.backProp()->adjustFeatures(pFeatureGradient, 1.0, 0, false);
		GVec::subtract(pFeatureGradient, pFeatures, 5);
		GVec::multiply(pFeatureGradient, -2.0, 5);

		// Empirically measure gradient
		for(int i = 0; i < 5; i++)
		{
			pFeatures[i] += 0.0001;
			nn.predict(pFeatures, pOutput);
			double sse = GVec::squaredDistance(pTarget, pOutput, 10);
			pEmpiricalGradient[i] = (sse - sseBaseline) / 0.0001;
			pFeatures[i] -= 0.0001;
		}

		// Check it
		double corr = GVec::correlation(pFeatureGradient, pEmpiricalGradient, 5);
		if(corr > 1.0)
			ThrowError("pathological results");
		if(corr < 0.999)
			ThrowError("failed");
	}
}

void GNeuralNet_testBinaryClassification(GRand* pRand)
{
	vector<size_t> vals;
	vals.push_back(2);
	GMatrix features(vals);
	GMatrix labels(vals);
	for(size_t i = 0; i < 100; i++)
	{
		double d = (double)pRand->next(2);
		features.newRow()[0] = d;
		labels.newRow()[0] = 1.0 - d;
	}
	GNeuralNet nn(*pRand);
	nn.train(features, labels);
	double r;
	nn.accuracy(features, labels, &r);
	if(r != 1)
		ThrowError("Failed simple sanity test");
}

// static
void GNeuralNet::test()
{
	GRand prng(0);
	GNeuralNet_testMath();
	GNeuralNet_testBinaryClassification(&prng);

	// Test with no hidden layers (logistic regression)
	{
		GNeuralNet nn(prng);
		nn.basicTest(0.745, 0.77);
	}

	// Test NN with one hidden layer
	{
		GNeuralNet nn(prng);
		nn.addLayer(3);
		nn.basicTest(0.76, 0.75);
	}

	GNeuralNet_testInputGradient(&prng);
}

#endif





GNeuralNetPseudoInverse::GNeuralNetPseudoInverse(GNeuralNet* pNN, double padding)
: m_padding(padding)
{
	size_t maxNodes = 0;
	size_t i;
	for(i = 0; i < pNN->layerCount(); i++)
	{
		GNeuralNetLayer& nnLayer = pNN->layer(i);
		maxNodes = std::max(maxNodes, nnLayer.m_neurons.size());
		GNeuralNetInverseLayer* pLayer = new GNeuralNetInverseLayer();
		m_layers.push_back(pLayer);
		pLayer->m_pActivationFunction = nnLayer.m_pActivationFunction;
		GMatrix weights(nnLayer.m_neurons.size(), nnLayer.m_neurons[0].m_weights.size() - 1);
		size_t r = 0;
		for(vector<GNeuron>::iterator it = nnLayer.m_neurons.begin(); it != nnLayer.m_neurons.end(); it++)
		{
			vector<double>::iterator itW = it->m_weights.begin();
			double unbias = -*itW;
			itW++;
			double* pRow = weights.row(r);
			for( ; itW != it->m_weights.end(); itW++)
			{
				*(pRow++) = *itW;
				unbias -= nnLayer.m_pActivationFunction->center() * (*itW);
			}
			pLayer->m_unbias.push_back(unbias);
			r++;
		}
		pLayer->m_pInverseWeights = weights.pseudoInverse();
	}
	m_pBuf1 = new double[2 * maxNodes];
	m_pBuf2 = m_pBuf1 + maxNodes;
}

GNeuralNetPseudoInverse::~GNeuralNetPseudoInverse()
{
	for(vector<GNeuralNetInverseLayer*>::iterator it = m_layers.begin(); it != m_layers.end(); it++)
		delete(*it);
	delete[] std::min(m_pBuf1, m_pBuf2);
}

void GNeuralNetPseudoInverse::computeFeatures(const double* pLabels, double* pFeatures)
{
	size_t inCount = 0;
	vector<GNeuralNetInverseLayer*>::iterator it = m_layers.end() - 1;
	GVec::copy(m_pBuf2, pLabels, (*it)->m_pInverseWeights->cols());
	for(; true; it--)
	{
		GNeuralNetInverseLayer* pLayer = *it;
		inCount = pLayer->m_pInverseWeights->rows();
		std::swap(m_pBuf1, m_pBuf2);

		// Invert the layer
		double* pT = m_pBuf1;
		for(vector<double>::iterator ub = pLayer->m_unbias.begin(); ub != pLayer->m_unbias.end(); ub++)
		{
			*pT = pLayer->m_pActivationFunction->inverse(*pT) + *ub;
			pT++;
		}
		pLayer->m_pInverseWeights->multiply(m_pBuf1, m_pBuf2);

		// Clip and uncenter the value
		pLayer = *it;
		double halfRange = pLayer->m_pActivationFunction->halfRange();
		double center = pLayer->m_pActivationFunction->center();
		pT = m_pBuf2;
		for(size_t i = 0; i < inCount; i++)
		{
			*pT = std::max(m_padding - halfRange, std::min(halfRange - m_padding, *pT)) + center;
			pT++;
		}

		if(it == m_layers.begin())
			break;
	}
	GVec::copy(pFeatures, m_pBuf2, inCount);
}

#ifndef NO_TEST_CODE
// static
void GNeuralNetPseudoInverse::test()
{
	GRand prng(0);
	GNeuralNet nn(prng);
	nn.addLayer(5);
	nn.addLayer(7);
	sp_relation pFeatureRel = new GUniformRelation(3);
	sp_relation pLabelRel = new GUniformRelation(12);
	nn.beginIncrementalLearning(pFeatureRel, pLabelRel);
	nn.decayWeights(-9.0 * nn.learningRate()); // multiply all non-bias weights by 10
	GNeuralNetPseudoInverse nni(&nn, 0.001);
	double labels[12];
	double features[3];
	double features2[3];
	for(size_t i = 0; i < 20; i++)
	{
		for(size_t j = 0; j < 3; j++)
			features[j] = prng.uniform() * 0.98 + 0.01;
		nn.predict(features, labels);
		nni.computeFeatures(labels, features2);
		if(GVec::squaredDistance(features, features2, 3) > 1e-8)
			ThrowError("failed");
	}
}
#endif


} // namespace GClasses

