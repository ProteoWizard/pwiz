#ifndef __GPARTICLESWARM_H__
#define __GPARTICLESWARM_H__

#include "GOptimizer.h"
#include "GMatrix.h"

namespace GClasses {

class GRand;


/// An optimization algorithm inspired by flocking birds
class GParticleSwarm : public GOptimizer
{
protected:
	double m_dMin, m_dRange;
	double m_dLearningRate;
	size_t m_nDimensions;
	size_t m_nPopulation;
	double* m_pPositions;
	double* m_pVelocities;
	double* m_pBests;
	double* m_pErrors;
	size_t m_nGlobalBest;
	GRand* m_pRand;

public:
	GParticleSwarm(GTargetFunction* pCritic, size_t nPopulation, double dMin, double dRange, GRand* pRand);
	virtual ~GParticleSwarm();

	/// Perform a little more optimization
	virtual double iterate();

	/// Specify the learning rate
	void setLearningRate(double d) { m_dLearningRate = d; }

protected:
	void reset();
};


/// This is an algorithm for finding good starting points within a constrained
/// optimization problem. It works by simulating "rubber balls" which bounce
/// around inside the constrained region. After many iterations, they tend to
/// be spread somewhat uniformly, even with very complex constrained shapes.
/// The balls learn to approximate the
/// shape of the shell, so if the room is wider than it is tall, the balls will learn
/// to bounce sideways more often than vertically.
class GRubberBallSwarm
{
protected:
	GMatrix m_balls;
	GRand* m_pRand;
	int m_dims;
	int m_currentBall;
	int m_compareBall;
	int m_condemnedBall;
	int m_wallPrecisionIters;
	double* m_pTemp;
	double* m_pNormal;
	double m_condemnedDistance;
	double m_acceleration;
	double m_deceleration;
	double m_learningRate;

public:
	/// All of the simulated rubber balls are initialized to the zero-vector,
	/// which is assumed to be within the constrained region.
	GRubberBallSwarm(int dims, int ballCount, GRand* pRand);
	virtual ~GRubberBallSwarm();

	/// This method should be implemented to return true iff pVec is within the constrained region
	virtual bool isInside(double* pVec) = 0;

	/// This method is called when a ball bounces off of the constraint shell
	virtual void onBounce(double* pVec) {}

	/// Simulate another unit of time
	void iterate();

	/// Returns the current vector of the specified ball
	double* ball(int index);

protected:
	void initBall(double* pBall, double speed);
	void mutateBall(int dest, int source);
	void advanceBall(double* pBallPos);
};

} // namespace GClasses

#endif // __GPARTICLESWARM_H__
