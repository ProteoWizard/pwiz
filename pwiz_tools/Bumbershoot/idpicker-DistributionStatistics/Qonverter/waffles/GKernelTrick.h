#ifndef __GKERNELTRICK_H__
#define __GKERNELTRICK_H__

#include "GLearner.h"
#include "GVec.h"
#include <math.h>
#include <cmath>

namespace GClasses {

class GRand;

/// The base class for kernel functions. Classes which implement this
/// must provide an "apply" method that applies the kernel to two
/// vectors. Kernels may be combined together to form a more complex
/// kernel, to which the kernel trick will still apply.
class GKernel
{
public:
	GKernel() {}
	virtual ~GKernel() {}

	/// Applies the kernel to the two specified vectors
	virtual double apply(const double* pA, const double* pB) = 0;
};

/// The identity kernel
class GKernelIdentity : public GKernel
{
protected:
	size_t m_dims;

public:
	GKernelIdentity(size_t dims) : GKernel(), m_dims(dims) {}
	virtual ~GKernelIdentity() {}

	/// Computes A*B
	virtual double apply(const double* pA, const double* pB)
	{
		return GVec::dotProduct(pA, pB, m_dims);
	}
};

/// A polynomial kernel
class GKernelPolynomial : public GKernel
{
protected:
	size_t m_dims;
	double m_offset;
	unsigned int m_order;

public:
	GKernelPolynomial(size_t dims, double offset, unsigned int order) : GKernel(), m_dims(dims), m_offset(std::abs(offset)), m_order(order) {}
	virtual ~GKernelPolynomial() {}

	/// Computes (A * B + offset)^order
	virtual double apply(const double* pA, const double* pB)
	{
		return pow(GVec::dotProduct(pA, pB, m_dims) + m_offset, (int)m_order);
	}
};

/// A Gaussian RBF kernel
class GKernelGaussianRBF : public GKernel
{
protected:
	size_t m_dims;
	double m_variance;

public:
	GKernelGaussianRBF(size_t dims, double variance) : GKernel(), m_dims(dims), m_variance(std::abs(variance)) {}
	virtual ~GKernelGaussianRBF() {}

	/// Computes e^(-0.5 * ||A - B||^2 / variance)
	virtual double apply(const double* pA, const double* pB)
	{
		return exp(-0.5 * GVec::squaredDistance(pA, pB, m_dims) / m_variance);
	}
};

/// A translation kernel
class GKernelTranslate : public GKernel
{
protected:
	GKernel* m_pK;
	double m_value;

public:
	/// Takes ownership of pK
	GKernelTranslate(GKernel* pK, double value) : GKernel(), m_pK(pK), m_value(std::abs(value)) {}
	virtual ~GKernelTranslate() {}

	/// Computes K(A, B) + value
	virtual double apply(const double* pA, const double* pB)
	{
		return m_pK->apply(pA, pB) + m_value;
	}
};

/// A scalar kernel
class GKernelScale : public GKernel
{
protected:
	GKernel* m_pK;
	double m_value;

public:
	/// Takes ownership of pK
	GKernelScale(GKernel* pK, double value) : GKernel(), m_pK(pK), m_value(std::abs(value)) {}
	virtual ~GKernelScale() {}

	/// Computes K(A, B) * value
	virtual double apply(const double* pA, const double* pB)
	{
		return m_pK->apply(pA, pB) * m_value;
	}
};

/// An addition kernel
class GKernelAdd : public GKernel
{
protected:
	GKernel* m_pK1;
	GKernel* m_pK2;

public:
	/// Takes ownership of pK1 and pK2
	GKernelAdd(GKernel* pK1, GKernel* pK2) : GKernel(), m_pK1(pK1), m_pK2(pK2) {}
	virtual ~GKernelAdd() {}

	/// Computes K1(A, B) + K2(A, B)
	virtual double apply(const double* pA, const double* pB)
	{
		return m_pK1->apply(pA, pB) + m_pK2->apply(pA, pB);
	}
};

/// A multiplication kernel
class GKernelMultiply : public GKernel
{
protected:
	GKernel* m_pK1;
	GKernel* m_pK2;

public:
	/// Takes ownership of pK1 and pK2
	GKernelMultiply(GKernel* pK1, GKernel* pK2) : GKernel(), m_pK1(pK1), m_pK2(pK2) {}
	virtual ~GKernelMultiply() {}

	/// Computes K1(A, B) * K2(A, B)
	virtual double apply(const double* pA, const double* pB)
	{
		return m_pK1->apply(pA, pB) * m_pK2->apply(pA, pB);
	}
};

/// A power kernel
class GKernelPow : public GKernel
{
protected:
	GKernel* m_pK;
	unsigned int m_value;

public:
	GKernelPow(GKernel* pK, unsigned int value) : GKernel(), m_pK(pK), m_value(value) {}
	virtual ~GKernelPow() {}

	/// Computes K(A, B)^value
	virtual double apply(const double* pA, const double* pB)
	{
		return pow(m_pK->apply(pA, pB), (int)m_value);
	}
};

/// The Exponential kernel
class GKernelExp : public GKernel
{
protected:
	GKernel* m_pK;

public:
	GKernelExp(GKernel* pK) : GKernel(), m_pK(pK) {}
	virtual ~GKernelExp() {}

	/// Computes e^K(A, B)
	virtual double apply(const double* pA, const double* pB)
	{
		return exp(m_pK->apply(pA, pB));
	}
};

/// A Normalizing kernel
class GKernelNormalize : public GKernel
{
protected:
	GKernel* m_pK;

public:
	GKernelNormalize(GKernel* pK) : GKernel(), m_pK(pK) {}
	virtual ~GKernelNormalize() {}

	/// Computes K(A, B) / sqrt(K(A, A) * K(B, B))
	virtual double apply(const double* pA, const double* pB)
	{
		return m_pK->apply(pA, pB) / sqrt(m_pK->apply(pA, pA) * m_pK->apply(pB, pB));
	}
};

} // namespace GClasses

#endif // __GKERNELTRICK_H__
