/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GHMM_H__
#define __GHMM_H__

#include <vector>

namespace GClasses {

class GHiddenMarkovModel
{
protected:
	int m_stateCount;
	int m_symbolCount;
	double* m_pInitialStateProbabilities;
	double* m_pTransitionProbabilities;
	double* m_pSymbolProbabilities;
	double* m_pTrainingBuffer;
	int m_maxLen;

public:
	GHiddenMarkovModel(int stateCount, int symbolCount);
	~GHiddenMarkovModel();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif

	/// Returns the current vector of initial state probabilities
	double* initialStateProbabilities() { return m_pInitialStateProbabilities; }

	/// Returns the current vector of transition probabilities, such that
	/// pTransitionProbabilities[stateCount * i + j] is the probability of
	/// transitioning from state i to state j.
	double* transitionProbabilities() { return m_pTransitionProbabilities; }

	/// Returns the current vector of symbol probabilities, such that
	/// pSymbolProbabilities[stateCount * i + j] is the probability of
	/// observing symbol j when in state i.
	double* symbolProbabilities() { return m_pSymbolProbabilities; }

	/// Calculates the log probability that the specified observation
	/// sequence would occur with this model.
	double forwardAlgorithm(const int* pObservations, int len);

	/// Finds the most likely state sequence to explain the specified
	/// observation sequence, and also returns the log probability of
	/// that state sequence given the observation sequence.
	double viterbi(int* pMostLikelyStates, const int* pObservations, int len);

	/// Uses expectation maximization to refine the model based on
	/// a training set of observation sequences. (You should have already
	/// set prior values for the initial, transition and symbol probabilites
	/// before you call this method.)
	void baumWelch(std::vector<int*>& sequences, std::vector<int>& lengths, int maxPasses = 0x7fffffff);

protected:
	void backwardAlgorithm(const int* pObservations, int len);
	void baumWelchBeginTraining(int maxLen);
	void baumWelchBeginPass();
	void baumWelchAddSequence(const int* pObservations, int len);
	double baumWelchEndPass();
	void baumWelchEndTraining();
};

} // namespace GClasses

#endif
