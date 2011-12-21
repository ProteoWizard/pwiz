/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GNEURALNET_H__
#define __GNEURALNET_H__

#include "GLearner.h"
#include <vector>

namespace GClasses {

class GNeuralNet;
class GRand;
class GBackProp;
class GImage;
class GActivationFunction;



/// Represents a single neuron in a neural network
class GNeuron
{
public:
	double m_activation;
	double m_net;
	std::vector<double> m_weights; // Weight zero is always the bias weight

	void resetWeights(GRand* pRand, double inputCenter);
};

/// Represents a layer of neurons in a neural network
class GNeuralNetLayer
{
public:
	std::vector<GNeuron> m_neurons;
	GActivationFunction* m_pActivationFunction;

	void resetWeights(GRand* pRand, double inputCenter);
};

/// An internal class used by GBackProp
class GBackPropWeight
{
public:
	double m_delta;

	GBackPropWeight()
	{
		m_delta = 0;
	}
};

/// An internal class used by GBackProp
class GBackPropNeuron
{
public:
	double m_error;
	std::vector<GBackPropWeight> m_weights;
};

/// An internal class used by GBackProp
class GBackPropLayer
{
public:
	std::vector<GBackPropNeuron> m_neurons;
};

/// This class performs backpropagation on a neural network. (I made it a separate
/// class because it is only needed during training. There is no reason to waste
/// this space after training is complete, or if you choose to use a different
/// technique to train the neural network.)
class GBackProp
{
friend class GNeuralNet;
protected:
	GNeuralNet* m_pNN;
	std::vector<GBackPropLayer> m_layers;

public:
	/// This class will adjust the weights in pNN
	GBackProp(GNeuralNet* pNN);

	~GBackProp()
	{
	}

	/// Returns a layer (not a layer of the neural network, but a corresponding layer of values used for back-prop)
	GBackPropLayer& layer(size_t layer)
	{
		return m_layers[layer];
	}

	/// Backpropagates the error from the "from" layer to the "to" layer. (If the "to" layer has fewer units than the "from"
	/// layer, then it will begin propagating with the (fromBegin+1)th weight and stop when the "to" layer runs out of units.
	/// It would be an error if the number of units in the "from" layer is less than the number of units in the "to" layer
	/// plus fromBegin.
	static void backPropLayer(GNeuralNetLayer* pNNFromLayer, GNeuralNetLayer* pNNToLayer, GBackPropLayer* pBPFromLayer, GBackPropLayer* pBPToLayer, size_t fromBegin = 0);

	/// Backpropagates the error from a single output node to a hidden layer.
	void backPropFromSingleNode(GNeuron& nnFrom, GBackPropNeuron& bpFrom, GNeuralNetLayer* pNNToLayer, GBackPropLayer* pBPToLayer);

	/// This is another implementation of backPropLayer. This one is somewhat more flexible, but slightly less efficient.
	/// It supports backpropagating error from one or two layers. (pNNFromLayer2 should be NULL if you are backpropagating from just one
	/// layer.) It also supports temporal backpropagation by unfolding in time and then averaging the error across all of the unfolded
	/// instantiations. "pass" specifies how much of the error for this pass to accept. 1=all of it, 2=half of it, 3=one third, etc.
	static void backPropLayer2(GNeuralNetLayer* pNNFromLayer1, GNeuralNetLayer* pNNFromLayer2, GNeuralNetLayer* pNNToLayer, GBackPropLayer* pBPFromLayer1, GBackPropLayer* pBPFromLayer2, GBackPropLayer* pBPToLayer, size_t pass);

	/// Adjust weights in pNNFromLayer. (The error for pNNFromLayer layer must have already been computed.) (If you are
	/// backpropagating error from two layers, you can just call this method twice, once for each previous layer.)
	static void adjustWeights(GNeuralNetLayer* pNNFromLayer, GNeuralNetLayer* pNNToLayer, GBackPropLayer* pBPFromLayer, double learningRate, double momentum);

	/// Adjust weights in pNNFromLayer. (The error for pNNFromLayer layer must have already been computed.) (If you are
	/// backpropagating error from two layers, you can just call this method twice, once for each previous layer.)
	static void adjustWeights(GNeuralNetLayer* pNNFromLayer, const double* pFeatures, bool useInputBias, GBackPropLayer* pBPFromLayer, double learningRate, double momentum);

	/// Adjust the weights of a single neuron that follows a hidden layer. (Assumes the error of this neuron has already been computed).
	void adjustWeightsSingleNeuron(GNeuron& nnFrom, GNeuralNetLayer* pNNToLayer, GBackPropNeuron& bpFrom, double learningRate, double momentum);

	/// Adjust the weights of a single neuron when there are no hidden layers. (Assumes the error of this neuron has already been computed).
	void adjustWeightsSingleNeuron(GNeuron& nnFrom, const double* pFeatures, bool useInputBias, GBackPropNeuron& bpFrom, double learningRate, double momentum);

	/// This method assumes that the error term is already set at every unit in the output layer. It uses back-propagation
	/// to compute the error term at every hidden unit. (It does not update any weights.)
	void backpropagate();

	/// Backpropagates error from a single output node over all of the hidden layers. (Assumes the error term is already set on
	/// the specified output node.)
	void backpropagateSingleOutput(size_t outputNode);

	/// This method assumes that the error term is already set for every network unit. It adjusts weights to descend the
	/// gradient of the error surface with respect to the weights.
	void descendGradient(const double* pFeatures, double learningRate, double momentum, bool useInputBias);

	/// This method assumes that the error term has been set for a single output network unit, and all units that feed into
	/// it transitively. It adjusts weights to descend the gradient of the error surface with respect to the weights.
	void descendGradientSingleOutput(size_t outputNeuron, const double* pFeatures, double learningRate, double momentum, bool useInputBias);

	/// This method assumes that the error term is already set for every network unit. It descends the gradient
	/// by adjusting the features (not the weights).
	void adjustFeatures(double* pFeatures, double learningRate, size_t skip, bool useInputBias);

	/// This adjusts the features (not the weights) to descend the gradient, assuming that the error is computed
	/// from only one of the output units of the network.
	void adjustFeaturesSingleOutput(size_t outputNeuron, double* pFeatures, double learningRate, bool useInputBias);
};



/// An artificial neural network
class GNeuralNet : public GIncrementalLearner
{
friend class GBackProp;
public:
	enum TargetFunction
	{
		squared_error, /// (default) best for regression
		cross_entropy, /// best for classification
		sign, /// uses the sign of the error, as in the perceptron training rule
		physical, /// squared error scaled by the inverse square of the distance
	};

protected:
	std::vector<GNeuralNetLayer> m_layers;
	GBackProp* m_pBackProp;
	size_t m_internalFeatureDims, m_internalLabelDims;
	std::vector<GActivationFunction*> m_activationFunctions;
	GActivationFunction* m_pActivationFunction;
	double m_learningRate;
	double m_momentum;
	double m_validationPortion;
	double m_minImprovement;
	size_t m_epochsPerValidationCheck;
	TargetFunction m_backPropTargetFunction;
	bool m_useInputBias;

public:
	GNeuralNet(GRand& rand);

	/// Load from a text-format
	GNeuralNet(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GNeuralNet();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Saves the model to a text file.
	virtual GDomNode* serialize(GDom* pDoc);

	/// Sets the activation function to use with all subsequently added
	/// layers. (Note that the activation function for the output layer is
	/// set when train or beginIncrementalLearning is called, so if you
	/// only wish to set the squshing function for the output layer, call
	/// this method after all hidden layers have been added, but before you call train.)
	/// If hold is true, then the neural network will hold on to this instance
	/// of the activation function and delete it when the neural network is deleted.
	void setActivationFunction(GActivationFunction* pSF, bool hold);

	/// Adds a hidden layer to the network. (The first hidden layer
	/// that you add will be adjacent to the input features. The last
	/// hidden layer that you add will be adjacent to the output
	/// layer.)
	void addLayer(size_t nNodes);

	/// Returns the number of layers in this neural network. (Every network has
	/// at least one output layer, plus all of the hidden layers that you add by calling
	/// addLayer. The input vector does not count as a layer, even though it may be
	/// common to visualize it as a layer.)
	size_t layerCount() { return m_layers.size(); }

	/// Returns a reference to the specified layer.
	GNeuralNetLayer& layer(size_t n) { return m_layers[n]; }

	/// Adds a new node at the end of the specified layer. (The new node is initialized
	/// with small weights, so this operation should initially have little impact on
	/// predictions.)
	void addNode(size_t layer);

	/// Removes the specified node from the specified layer. (An exception will be thrown
	/// the layer only has one node.)
	void dropNode(size_t layer, size_t node);

	/// Returns the backprop object associated with this neural net (if there is one)
	GBackProp* backProp() { return m_pBackProp; }

	/// Set the portion of the data that will be used for validation. If the
	/// value is 0, then all of the data is used for both training and validation.
	void setValidationPortion(double d) { m_validationPortion = d; }

	/// Counts the number of weights in the network. (This value is not cached, so
	/// you should cache it rather than frequently call this method.)
	size_t countWeights();

	/// Perturbs all weights in the network by a random normal offset with the
	/// specified deviation.
	void perturbAllWeights(double deviation);

	/// Clips all non-bias weights to fall within the range [-max, max].
	void clipWeights(double max);

	/// Multiplies all non-bias weights by (1.0 - (learning_rate * lambda)),
	/// starting with the output layer, and ending with the first hidden layer.
	/// Typical values for lambda are small (like 0.001.)
	/// After each layer, the value of lambda is multiplied by gamma.
	/// (If gamma is greater than 1.0, then weights in hidden layers will decay
	/// faster, and if gamma is less than 1.0, then weights in hidden layers will
	/// decay slower.) It may be significant to note that if a regularizing
	/// penalty is added to the error of lambda times the sum-squared values of
	/// non-bias weights, then on-line weight updating works out to the same as
	/// decaying the weights after each application of back-prop.
	void decayWeights(double lambda, double gamma = 1.0);

	/// Just like decayWeights, except it only decays the weights in one of
	/// the output units.
	void decayWeightsSingleOutput(size_t output, double lambda);

	/// Returns the current learning rate
	double learningRate() { return m_learningRate; }

	/// Set the learning rate
	void setLearningRate(double d) { m_learningRate = d; }

	/// Returns the current momentum value
	double momentum() { return m_momentum; }

	/// Momentum has the effect of speeding convergence and helping
	/// the gradient descent algorithm move past some local minimums
	void setMomentum(double d) { m_momentum = d; }

	/// Returns the threshold ratio for improvement. 
	double improvementThresh() { return m_minImprovement; }

	/// Specifies the threshold ratio for improvement that must be
	/// made since the last validation check for training to continue.
	/// (For example, if the mean squared error at the previous validation check
	/// was 50, and the mean squared error at the current validation check
	/// is 49, then training will stop if d is > 0.02.)
	void setImprovementThresh(double d) { m_minImprovement = d; }

	/// Returns the number of epochs to perform before the validation data
	/// is evaluated to see if training should stop.
	size_t windowSize() { return m_epochsPerValidationCheck; }

	/// Sets the number of epochs that will be performed before
	/// each time the network is tested again with the validation set
	/// to determine if we have a better best-set of weights, and
	/// whether or not it's achieved the termination condition yet.
	/// (An epochs is defined as a single pass through all rows in
	/// the training set.)
	void setWindowSize(size_t n) { m_epochsPerValidationCheck = n; }

	/// Specify the target function to use for back-propagation. The default is squared_error.
	/// cross_entropy tends to be faster, and is well-suited for classification tasks.
	void setBackPropTargetFunction(TargetFunction eTF) { m_backPropTargetFunction = eTF; }

	/// Returns the enumeration of the target function used for backpropagation
	TargetFunction backPropTargetFunction() { return m_backPropTargetFunction; }

	/// See the comment for GIncrementalLearner::trainSparse
	/// Assumes all attributes are continuous.
	virtual void trainSparse(GSparseMatrix& features, GMatrix& labels);

	/// See the comment for GSupervisedLearner::clear
	virtual void clear() {}

	/// Train the network until the termination condition is met.
	/// Returns the number of epochs required to train it.
	size_t trainWithValidation(GMatrix& trainFeatures, GMatrix& trainLabels, GMatrix& validateFeatures, GMatrix& validateLabels);

	/// Some extra junk is allocated when training to make it efficient.
	/// This method is called when training is done to get rid of that
	/// extra junk.
	void releaseTrainingJunk();

	/// Gets the internal training data set
	GMatrix* internalTraininGMatrix();

	/// Gets the internal validation data set
	GMatrix* internalValidationData();

	/// Sets all the weights from an array of doubles. The number of
	/// doubles in the array can be determined by calling countWeights().
	void setWeights(const double* pWeights);

	/// Copy the weights from pOther. It is assumed (but not checked) that
	/// pOther has the same network structure as this neural network.
	void copyWeights(GNeuralNet* pOther);

	/// Copies the layers, nodes, and settings from pOther (but not the
	/// weights). beginIncrementalLearning must have been called on pOther
	/// so that it has a complete structure.
	void copyStructure(GNeuralNet* pOther);

	/// Serializes the network weights into an array of doubles. The
	/// number of doubles in the array can be determined by calling
	/// countWeights().
	void weights(double* pOutWeights);

	/// Evaluates a feature vector. (The results will be in the nodes of the output layer.)
	void forwardProp(const double* pInputs);

	/// This is the same as forwardProp, except it only propagates to a single output node.
	/// It returns the value that this node outputs.
	double forwardPropSingleOutput(const double* pInputs, size_t output);

	/// This method assumes forwardProp has been called. It copies the predicted vector into pOut.
	void copyPrediction(double* pOut);

	/// This method assumes forwardProp has been called. It computes the sum squared prediction error
	/// with the specified target vector.
	double sumSquaredPredictionError(const double* pTarget);

	/// This method assumes that forwardProp has already been called. (Note that
	/// the predict method calls forwardProp). It computes the error
	/// values at each node in the output layer. After calling this method,
	/// it is typical to call backProp()->backpropagate(), to compute the error on
	/// the hidden nodes, and then to call backProp()->descendGradient to update
	/// the weights. pTarget contains the target values for the ouptut nodes.
	void setErrorOnOutputLayer(const double* pTarget, TargetFunction eTargetFunction = squared_error);

	/// This is teh same as setErrorOnOutputLayer, except that it only sets
	/// the error on a single output node.
	void setErrorSingleOutput(double target, size_t output, TargetFunction eTargetFunction = squared_error);

	/// Uses cross-validation to find a set of parameters that works well with
	/// the provided data. That is, this method will add a good number of hidden
	/// layers, pick a good momentum value, etc.
	void autoTune(GMatrix& features, GMatrix& labels);

	/// Specify whether to use an input bias. (The default is false.) This feature is
	/// used with generative-backpropagation, which adjusts inputs to create latent features.
	void setUseInputBias(bool b) { m_useInputBias = b; }

	/// Returns whether this neural network utilizes an input bias.
	bool useInputBias() { return m_useInputBias; }

	/// Returns true iff train or beginIncrementalTraining has been called.
	bool hasTrainingBegun() { return m_internalLabelDims > 0; }

	/// Swaps two nodes in the specified layer. If layer specifies one of the hidden
	/// layers, then this will have no net effect on the output of the network.
	/// (Assumes this model is already trained.)
	void swapNodes(size_t layer, size_t a, size_t b);

	/// Swaps nodes in hidden layers of this neural network to align with those in
	/// that neural network, as determined using bipartite matching. (This might
	/// be done, for example, before averaging weights together.)
	void align(GNeuralNet& that);

protected:
	/// Measures the sum squared error against the specified dataset
	double validationSquaredError(GMatrix& features, GMatrix& labels);

	/// See the comment for GSupervisedLearner::trainInner
	virtual void trainInner(GMatrix& features, GMatrix& labels);

	/// See the comment for GSupervisedLearner::predictInner
	virtual void predictInner(const double* pIn, double* pOut);

	/// See the comment for GSupervisedLearner::predictDistributionInner
	virtual void predictDistributionInner(const double* pIn, GPrediction* pOut);

	/// See the comment for GTransducer::canImplicitlyHandleNominalFeatures
	virtual bool canImplicitlyHandleNominalFeatures() { return false; }

	/// See the comment for GTransducer::supportedFeatureRange
	virtual bool supportedFeatureRange(double* pOutMin, double* pOutMax);

	/// See the comment for GTransducer::canImplicitlyHandleMissingFeatures
	virtual bool canImplicitlyHandleMissingFeatures() { return false; }

	/// See the comment for GTransducer::canImplicitlyHandleNominalLabels
	virtual bool canImplicitlyHandleNominalLabels() { return false; }

	/// See the comment for GTransducer::supportedFeatureRange
	virtual bool supportedLabelRange(double* pOutMin, double* pOutMax);

	/// See the comment for GIncrementalLearner::beginIncrementalLearningInner
	virtual void beginIncrementalLearningInner(sp_relation& pFeatureRel, sp_relation& pLabelRel);

	/// See the comment for GIncrementalLearner::trainIncrementalInner
	virtual void trainIncrementalInner(const double* pIn, const double* pOut);
};


/// A helper class used by GNeuralNetPseudoInverse
class GNeuralNetInverseLayer
{
public:
	GActivationFunction* m_pActivationFunction;
	std::vector<double> m_unbias;
	GMatrix* m_pInverseWeights;

	~GNeuralNetInverseLayer()
	{
		delete(m_pInverseWeights);
	}
};

/// Computes the pseudo-inverse of a neural network.
class GNeuralNetPseudoInverse
{
protected:
	double m_padding;
	std::vector<GNeuralNetInverseLayer*> m_layers;
	double* m_pBuf1;
	double* m_pBuf2;

public:
	/// padding specifies a margin in which label values will be clipped inside
	/// the activation function output range to avoid extreme feature values (-inf, inf, etc.).
	GNeuralNetPseudoInverse(GNeuralNet* pNN, double padding = 0.01);
	~GNeuralNetPseudoInverse();

	/// Computes the input features from the output labels. In cases of
	/// under-constraint, the feature vector with the minimum magnitude is chosen.
	/// In cases of over-constraint, the feature vector is chosen with a corresponding
	/// label vector that minimizes sum-squared error with the specified label
	/// vector.
	void computeFeatures(const double* pLabels, double* pFeatures);

#ifndef NO_TEST_CODE
	static void test();
#endif
};

/*
/// This is an experimental neural network that has the ability to adjust features (inputs) as well as weights
/// in order to make good predictions. The idea is that this should prevent outliers from having too much
/// influence on the model. The value of this idea, however, is not yet well-established.
class GModerateNet : public GNeuralNet
{
protected:
	double m_lambda;

public:
	GModerateNet(GRand* pRand);
	virtual ~GModerateNet();
	double lambda() { return m_lambda; }
	void setLambda(double d) { m_lambda = d; }
	virtual GDomNode* serialize(GDom* pDoc);
	virtual void train(GMatrix& data, int labelDims);
	virtual void predictDistribution(const double* pIn, GPrediction* pOut);
	virtual void clear();
	virtual void enableIncrementalLearning(sp_relation& pRelation, size_t labelDims, double* pMins, double* pRanges);
	virtual void trainIncremental(const double* pIn, const double* pOut);
	virtual void trainSparse(GSparseMatrix& features, GMatrix& labels);
};
*/
} // namespace GClasses

#endif // __GNEURALNET_H__

