/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GSYSTEMLEARNER_H__
#define __GSYSTEMLEARNER_H__

#include "GMatrix.h"
#include <vector>

namespace GClasses {

class GRand;
class GIncrementalLearner;
class GNeighborFinderGeneralizing;
class GAgentActionIterator;
class GSupervisedLearner;
class GImage;


/// This is the base class for algorithms that learn to model dynamical systems
class GSystemLearner
{
protected:

public:
	GSystemLearner() {}
	GSystemLearner(GDomNode* pNode) {}
	virtual ~GSystemLearner() {}

	/// Simulates performing the specified action
	virtual void doAction(const double* pAction) = 0;

	/// Predict the current observations for the current context.
	virtual void predict(double* pObs) = 0;

	/// Calibrate the context based on some actual observations.
	virtual void calibrate(const double* pObs) = 0;

protected:
	/// Child classes should use this in their implementation of serialize
	GDomNode* baseDomNode(GDom* pDoc, const char* szClassName);
};




/// This class can be used to implement recurrent neural networks, or recurrent
/// forms of other supervised models.
class GRecurrentModel : public GSystemLearner
{
protected:
	size_t m_actionDims;
	size_t m_contextDims;
	size_t m_obsDims, m_pixels, m_channels;
	GSupervisedLearner* m_pTransitionFunc;
	GSupervisedLearner* m_pObservationFunc;
	size_t m_paramDims;
	size_t* m_pParamRanges;
	double* m_pParams;
	double* m_pContext;
	double* m_pBuf;
	bool m_transitionDelta;
	bool m_useIsomap;
	GRand* m_pRand;
	double m_trainingSeconds;
	double m_validationInterval;
	double m_multiplier;
	std::vector<GMatrix*>* m_pValidationData;

public:
	/// Takes ownership of pTransition and pObservation
	GRecurrentModel(GSupervisedLearner* pTransition, GSupervisedLearner* pObservation, size_t actionDims, size_t contextDims, size_t obsDims, GRand* pRand, std::vector<size_t>* pParamDims = NULL);

	/// Load from a DOM.
	GRecurrentModel(GDomNode* pNode, GRand* pRand);

	virtual ~GRecurrentModel();

	/// See the comment for GSystemLearner::serialize
	virtual GDomNode* serialize(GDom* pDoc);

	/// Returns the transition function
	GSupervisedLearner* transitionFunc() { return m_pTransitionFunc; }

	/// Returns the observation function
	GSupervisedLearner* observationFunc() { return m_pObservationFunc; }

	/// Returns the number of dimensions in the context (state) vector
	size_t contextDims() { return m_contextDims; }

	/// Returns a pointer to the context vector
	double* context() { return m_pContext; }

	double* params() { return m_pParams; }

	/// Returns the number of obs dims
	size_t obsDims() { return m_obsDims; }

	/// Perform validation at periodic intervals with the specified data during training. Results are passed to
	/// the onObtainValidationScore method. pValidationData should refer to some number of validation data pairs,
	/// where each validation data pair is an observation dataset followed by an action dataset.
	void validateDuringTraining(double timeInterval, std::vector<GMatrix*>* pValidationData);

	/// This method is called when trainMoses finishes computing the state estimate. By
	/// default, it does nothing, but you can overload it if you wish to do something with it.
	virtual void onFinishedComputingStateEstimate(GMatrix* pStateEstimate) {}

	/// If validateDuringTraining was called, then this method is called whenever validation data is obtained.
	virtual void onObtainValidationScore(size_t timeSlice, double seconds, double squaredError) {}

	/// Trains with the MOSES algorithm. (Calls onFinishedComputingStateEstimate when the state estimate is computed.)
	void trainMoses(GMatrix* pActions, GMatrix* pObservations);

	/// An experimental training technique
	void trainAaron(GMatrix* pActions, GMatrix* pObservations);

	/// An experimental training technique
	void trainJoshua(GMatrix* pActions, GMatrix* pObservations);

	/// Trains with an evolutionary optimizer
	void trainEvolutionary(GMatrix* pActions, GMatrix* pObservations);

	/// Trains with a hill climber
	void trainHillClimber(GMatrix* pActions, GMatrix* pObservations, double dev, double decay, double seconds, bool climb, bool anneal);

	/// Trains with the back-prop-through-time algorithm. Returns the final sequence length.
	size_t trainBackPropThroughTime(GMatrix* pActions, GMatrix* pObservations, size_t depth, size_t itersPerSeqLen);

	size_t paramDims() { return m_paramDims; }
	size_t* paramRanges() { return m_pParamRanges; }

	/// See the comment for GSystemLearner::doAction
	virtual void doAction(const double* pAction);

	/// See the comment for GSystemLearner::predict
	virtual void predict(double* pObs);

	void predictPixel(const double* pParams, double* pObs);

	/// See the comment for GSystemLearner::calibrate
	virtual void calibrate(const double* pObs);

	/// Generates a film-strip-like sequence of frames that compare the expected observations (left) with predicted observations (right).
	/// If the predictions are not in the form of images, or they are not parameterized with two variables, then this method will throw.
	GImage* frames(GMatrix* pDataAction, GMatrix* pDataObs, bool calibrateContext, unsigned int frameWidth, int stepsPerImage, double scalePredictions);

	/// Computes the mean squared error with respect to some test sequence of actions and observations
	double validate(std::vector<GMatrix*>& validationData, bool calibrateContext, bool monotonic, double multiplier);

	/// Quickly compute an error estimate for use as an optimization heurisitic
	double quickValidate(GMatrix* pDataAction, GMatrix* pDataObs, size_t pixelSamples, double* paramArray, bool monotonic);

	/// Specify whether to use Isomap (instead of BreadthFirstUnfolding). (This only applies when trainMoses is called.)
	void setUseIsomap(bool b) { m_useIsomap = b; }

	/// Set the number of seconds to train
	void setTrainingSeconds(double d) { m_trainingSeconds = d; }

	/// Blur the image vector.
	static void blurImageVector(const double* pIn, double* pOut, int wid, int hgt, int chan, double valueRange, int radius, int iters);

protected:
	GMatrix* mosesEstimateState(GMatrix* pActions, GMatrix* pObservations);
	GMatrix* joshuaEstimateState(GMatrix* pActions, GMatrix* pObservations);
	void trainTransitionFunction(GMatrix* pActions, GMatrix* pEstState);
	void trainObservationFunction(GMatrix* pEstState, GMatrix* pObservations);
	void trainObservationFunctionIteratively(double dStart, GMatrix* pEstState, GMatrix* pObservations);
	void prepareForOptimization(GMatrix* pActions, GMatrix* pObservations);
};



/*
/// An experimental system learner
class GManifoldDynamicsLearner : public GSystemLearner
{
protected:
	sp_relation m_pRelation;
	int m_senseDims;
	int m_actionDims;
	double m_minCorrelation;
	int m_contextDims;
	GMatrix m_longTermMemory;
	GMatrix m_shortTermMemory;
	int m_shortTermMemoryPos;
	int m_shortTermMemoryCount;
	int m_actionVecStart;
	int m_patchSize;
	GAgentActionIterator* m_pActionIterator;
	GRand* m_pRand;
	sp_relation m_pNeighborRelation;
	GNeighborFinderGeneralizing* m_pNeighborFinder;
	size_t m_currentPatch;
	double* m_pContext;
	double* m_pBuf;
	std::vector<double> m_contextStack;
	int m_neighborCount;

public:
	GManifoldDynamicsLearner(sp_relation& pRelation, int actionDims, int shortTermMemorySize, int contextDims, double minCorrelation, GAgentActionIterator* pActionIterator, GRand* pRand);
	virtual ~GManifoldDynamicsLearner();

	virtual void doAction(const double* pActions);
	virtual void predict(double* pSenses);
	virtual void calibrate(const double* pSenses);
	void pushContext();
	void popContext();

protected:
	void makeNewPatch();
	double computeDihedralCorrelation(size_t a, size_t b);
	void recomputeContext(const double* pSenses);
#ifdef _DEBUG
	void logImage(int patch, double a, double b, const char* szMessage);
#endif
};



/// An experimental system learner
class GTemporalInstanceLearner : public GSystemLearner
{
protected:
	sp_relation m_pRelation;
	int m_senseDims;
	int m_actionDims;
	double m_balance;
	GMatrix** m_longTermMemories;
	GMatrix m_shortTermMemory;
	int m_actionCount;
	size_t m_shortTermMemoryPos;
	size_t m_shortTermMemorySize;
	GAgentActionIterator* m_pActionIterator;
	GRand* m_pRand;
	double* m_pActionEffects;

public:
	GTemporalInstanceLearner(sp_relation& pRelation, int actionDims, int shortTermMemorySize, GRand* pRand);
	virtual ~GTemporalInstanceLearner();

	virtual void doAction(const double* pActions);
	virtual void predict(double* pSenses);
	virtual void calibrate(const double* pSenses);
	void setBalance(double d) { m_balance = d; }

protected:
	bool computeAverageActionEffects(double* pVec, int offset);
	void makeNewInstance();
	size_t findBestMatch(int action);
};
*/

} // namespace GClasses

#endif // __GSYSTEMLEARNER_H__
