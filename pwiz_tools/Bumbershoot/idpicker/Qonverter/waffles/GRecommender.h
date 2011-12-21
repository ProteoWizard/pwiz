/*
	Copyright (C) 2010, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GRECOMMENDER_H__
#define __GRECOMMENDER_H__

#include "GError.h"
#include <vector>

namespace GClasses {

class GSparseMatrix;
class GSparseClusterer;
class GSparseSimilarity;
class GRand;
class GNeuralNet;
class GMatrix;
class Rating;
class GClusterer;
class GDom;
class GDomNode;
class GLearnerLoader;


/// The base class for collaborative filtering recommender systems.
class GCollaborativeFilter
{
protected:
	GRand& m_rand;

public:
	GCollaborativeFilter(GRand& rand) : m_rand(rand) {}
	GCollaborativeFilter(GDomNode* pNode, GLearnerLoader& ll);
	virtual ~GCollaborativeFilter() {}

	/// Trains this recommender system. Let R be an m-by-n sparse
	/// matrix of known ratings from m users of n items. pData should
	/// contain 3 columns, and one row for each known element in R.
	/// Column 0 in pData specifies the user index from 0 to m-1, column 1
	/// in pData specifies the item index from 0 to n-1, and column 2
	/// in pData specifies the rating vector for that user-item pair. All
	/// attributes in pData should be continuous.
	virtual void train(GMatrix& data) = 0;

	/// Train from an m-by-n dense matrix, where m is the number of users
	/// and n is the number of items. All attributes must be
	/// continuous. Missing values are indicated with UNKNOWN_REAL_VALUE.
	/// If pLabels is non-NULL, then the labels will be appended as
	/// additional items.
	void trainDenseMatrix(GMatrix& data, GMatrix* pLabels = NULL);

	/// This returns a prediction for how the specified user
	/// will rate the specified item. (The model must be trained before
	/// this method is called. Also, some values for that user and
	/// item should have been included in the training set, or else
	/// this method will have no basis to make a good prediction.)
	virtual double predict(size_t user, size_t item) = 0;

	/// pVec should be a vector of n real values, where n is the number of
	/// items/attributes/columns in the data that was used to train the model.
	/// to UNKNOWN_REAL_VALUE. This method will evaluate the known elements
	/// and impute (predict) values for the unknown elements. (The model should
	/// be trained before this method is called. Unlike the predict method,
	/// this method can operate on row-vectors that were not part of the training
	/// data.)
	virtual void impute(double* pVec, size_t dims) = 0;

	/// Marshal this object into a DOM that can be converted to a variety
	/// of formats. (Implementations of this method should use baseDomNode.)
	virtual GDomNode* serialize(GDom* pDoc) = 0;

	/// This randomly assigns each rating to one of the folds. Then,
	/// for each fold, it calls train with a dataset that contains
	/// everything except for the ratings in that fold. It predicts
	/// values for the items in the fold, and returns the mean-squared
	/// difference between the predictions and the actual ratings.
	/// If pOutMAE is non-NULL, it will be set to the mean-absolute error.
	double crossValidate(GMatrix& data, size_t folds, double* pOutMAE = NULL);

	/// This trains on the training set, and then tests on the test set.
	/// Returns the mean-squared difference between actual and target predictions.
	double trainAndTest(GMatrix& train, GMatrix& test, double* pOutMAE = NULL);

	/// This divides the data into two equal-size parts. It trains on one part, and
	/// then measures the precision/recall using the other part. It returns a
	/// three-column data set with recall scores in column 0 and corresponding
	/// precision scores in column 1. The false-positive rate is in column 2. (So,
	/// if you want a precision-recall plot, just drop column 2. If you want an
	/// ROC curve, drop column 1 and swap the remaining two columns.) This method
	/// assumes the ratings range from 0 to 1, so be sure to scale the ratings to
	/// fit that range before calling this method. If ideal is true, then it will
	/// ignore your model and report the ideal results as if your model always
	/// predicted the correct rating. (This is useful because it shows the best
	/// possible results.)
	GMatrix* precisionRecall(GMatrix& data, bool ideal = false);

	/// Pass in the data returned by the precisionRecall function (unmodified), and
	/// this will compute the area under the ROC curve.
	static double areaUnderCurve(GMatrix& data);

#ifndef NO_TEST_CODE
	/// Performs a basic unit test on this collaborative filter
	void basicTest(double minMSE);
#endif

protected:
	/// Child classes should use this in their implementation of serialize
	GDomNode* baseDomNode(GDom* pDoc, const char* szClassName);
};


/// This class always predicts the average rating for each item, no matter
/// to whom it is making the recommendation. The purpose of this algorithm
/// is to serve as a baseline for comparison
class GBaselineRecommender : public GCollaborativeFilter
{
protected:
	double* m_pRatings;
	size_t m_items;

public:
	/// General-purpose constructor
	GBaselineRecommender(GRand& rand);

	/// Deserialization constructor
	GBaselineRecommender(GDomNode* pNode, GLearnerLoader& ll);

	/// Destructor
	virtual ~GBaselineRecommender();

	/// See the comment for GCollaborativeFilter::train
	virtual void train(GMatrix& data);

	/// See the comment for GCollaborativeFilter::predict
	virtual double predict(size_t user, size_t item);

	/// See the comment for GCollaborativeFilter::impute
	virtual void impute(double* pVec, size_t dims);

	/// See the comment for GCollaborativeFilter::serialize
	virtual GDomNode* serialize(GDom* pDoc);

#ifndef NO_TEST_CODE
	/// Performs unit tests. Throws if a failure occurs. Returns if successful.
	static void test();
#endif
};


/// This class makes recommendations by finding the nearest-neighbors (as
/// determined by evaluating only overlapping ratings), and assuming that
/// the ratings of these neighbors will be predictive of your ratings.
class GInstanceRecommender : public GCollaborativeFilter
{
protected:
	size_t m_neighbors;
	GSparseSimilarity* m_pMetric;
	bool m_ownMetric;
	GSparseMatrix* m_pData;
	GBaselineRecommender* m_pBaseline;

public:
	GInstanceRecommender(size_t neighbors, GRand& rand);
	GInstanceRecommender(GDomNode* pNode, GLearnerLoader& ll);
	virtual ~GInstanceRecommender();

	/// Sets the similarity metric to use. if own is true, then this object will take care
	/// of deleting it as appropriate.
	void setMetric(GSparseSimilarity* pMetric, bool own);

	/// Returns the current similarity metric. (This might be useful, for example, if you
	/// want to modify the regularization value.)
	GSparseSimilarity* metric() { return m_pMetric; }

	/// See the comment for GCollaborativeFilter::train
	virtual void train(GMatrix& data);

	/// See the comment for GCollaborativeFilter::predict
	virtual double predict(size_t user, size_t item);

	/// See the comment for GCollaborativeFilter::impute
	virtual void impute(double* pVec, size_t dims);

	/// See the comment for GCollaborativeFilter::serialize
	virtual GDomNode* serialize(GDom* pDoc);

#ifndef NO_TEST_CODE
	/// Performs unit tests. Throws if a failure occurs. Returns if successful.
	static void test();
#endif
};


/// This class clusters the rows according to a sparse similarity metric,
/// then uses the baseline vector in each cluster to make predictions.
class GSparseClusterRecommender : public GCollaborativeFilter
{
protected:
	size_t m_clusters;
	GMatrix* m_pPredictions;
	GSparseClusterer* m_pClusterer;
	bool m_ownClusterer;
	size_t m_users, m_items;

public:
	GSparseClusterRecommender(size_t clusters, GRand& rand);
	virtual ~GSparseClusterRecommender();

	/// Returns the number of clusters
	size_t clusterCount() { return m_clusters; }

	/// Set the clustering algorithm to use
	void setClusterer(GSparseClusterer* pClusterer, bool own);

	/// See the comment for GCollaborativeFilter::train
	virtual void train(GMatrix& data);

	/// See the comment for GCollaborativeFilter::predict
	virtual double predict(size_t user, size_t item);

	/// See the comment for GCollaborativeFilter::impute
	virtual void impute(double* pVec, size_t dims);

	/// See the comment for GCollaborativeFilter::serialize
	virtual GDomNode* serialize(GDom* pDoc);

#ifndef NO_TEST_CODE
	/// Performs unit tests. Throws if a failure occurs. Returns if successful.
	static void test();
#endif
};



/// This class clusters the rows according to a dense distance metric,
/// then uses the baseline vector in each cluster to make predictions.
class GDenseClusterRecommender : public GCollaborativeFilter
{
protected:
	size_t m_clusters;
	GMatrix* m_pPredictions;
	GClusterer* m_pClusterer;
	bool m_ownClusterer;
	size_t m_users, m_items;

public:
	GDenseClusterRecommender(size_t clusters, GRand& rand);
	virtual ~GDenseClusterRecommender();

	/// Returns the number of clusters
	size_t clusterCount() { return m_clusters; }

	/// Set the clustering algorithm to use
	void setClusterer(GClusterer* pClusterer, bool own);

	/// See the comment for GCollaborativeFilter::train
	virtual void train(GMatrix& data);

	/// See the comment for GCollaborativeFilter::predict
	virtual double predict(size_t user, size_t item);

	/// See the comment for GCollaborativeFilter::impute
	virtual void impute(double* pVec, size_t dims);

	/// See the comment for GCollaborativeFilter::serialize
	virtual GDomNode* serialize(GDom* pDoc);

#ifndef NO_TEST_CODE
	/// Performs unit tests. Throws if a failure occurs. Returns if successful.
	static void test();
#endif
};



/// This factors the sparse matrix of ratings, M, such that M = PQ^T
/// where each row in P gives the principal preferences for the corresponding
/// user, and each row in Q gives the linear combination of those preferences
/// that map to a rating for an item. (Actually, P and Q also contain an extra column
/// added for a bias.) This class is implemented according to the specification on
/// page 631 in Takacs, G., Pilaszy, I., Nemeth, B., and Tikk, D. Scalable collaborative
/// filtering approaches for large recommender systems. The Journal of Machine Learning
/// Research, 10:623–656, 2009. ISSN 1532-4435., except with the addition of learning-rate
/// decay and a different stopping criteria.
class GMatrixFactorization : public GCollaborativeFilter
{
protected:
	size_t m_intrinsicDims;
	double m_regularizer;
	GMatrix* m_pP;
	GMatrix* m_pQ;
	bool m_useInputBias;

public:
	/// General-purpose constructor
	GMatrixFactorization(size_t intrinsicDims, GRand& rand);

	/// Deserialization constructor
	GMatrixFactorization(GDomNode* pNode, GLearnerLoader& ll);

	/// Destructor
	virtual ~GMatrixFactorization();

	/// Set the regularization value
	void setRegularizer(double d) { m_regularizer = d; }

	/// See the comment for GCollaborativeFilter::train
	virtual void train(GMatrix& data);

	/// See the comment for GCollaborativeFilter::predict
	virtual double predict(size_t user, size_t item);

	/// See the comment for GCollaborativeFilter::impute
	virtual void impute(double* pVec, size_t dims);

	/// Returns the matrix of user preference vectors
	GMatrix* getP() { return m_pP; }

	/// Returns the matrix of item weight vectors
	GMatrix* getQ() { return m_pQ; }

	/// See the comment for GCollaborativeFilter::serialize
	virtual GDomNode* serialize(GDom* pDoc);

	/// Specify to use no bias value with the inputs
	void noInputBias() { m_useInputBias = false; }

#ifndef NO_TEST_CODE
	/// Performs unit tests. Throws if a failure occurs. Returns if successful.
	static void test();
#endif

protected:
	/// Returns the sum-squared error for the specified set of ratings
	double validate(GMatrix& data);

};



/// This class trains a neural network to fit to the ratings. Although the name
/// implies that it is an extension of PCA, I think it is better described as a
/// non-linear generalization of matrix factorization. This algorithm was published
/// in Scholz, M. Kaplan, F. Guy, C. L. Kopka, J. Selbig, J., Non-linear PCA: a missing
/// data approach, In Bioinformatics, Vol. 21, Number 20, pp. 3887-3895, Oxford
/// University Press, 2005.
class GNonlinearPCA : public GCollaborativeFilter
{
protected:
	size_t m_intrinsicDims;
	size_t m_items;
	double* m_pMins;
	double* m_pMaxs;
	GNeuralNet* m_pModel;
	GMatrix* m_pUsers;
	bool m_useInputBias;
	bool m_useThreePass;

public:
	/// General-purpose constructor
	GNonlinearPCA(size_t intrinsicDims, GRand& rand);

	/// Deserialization constructor
	GNonlinearPCA(GDomNode* pNode, GLearnerLoader& ll);

	/// Destructor
	virtual ~GNonlinearPCA();

	/// Returns a pointer to the neural net that is used to model the recommendation space.
	/// You may want to use this method to add hidden layers, set the learning rate, or change
	/// activation functions before the model is trained.
	GNeuralNet* model() { return m_pModel; }

	/// Returns a pointer to the matrix of user preference vectors.
	GMatrix* users() { return m_pUsers; }

	/// See the comment for GCollaborativeFilter::train
	virtual void train(GMatrix& data);

	/// See the comment for GCollaborativeFilter::predict
	virtual double predict(size_t user, size_t item);

	/// See the comment for GCollaborativeFilter::impute
	virtual void impute(double* pVec, size_t dims);

	/// See the comment for GCollaborativeFilter::serialize
	virtual GDomNode* serialize(GDom* pDoc);

	/// Specify to use no bias value with the inputs
	void noInputBias() { m_useInputBias = false; }

	/// Specify not to use three-pass training. (It will just use one pass instead.)
	void noThreePass() { m_useThreePass = false; }

#ifndef NO_TEST_CODE
	/// Performs unit tests. Throws if a failure occurs. Returns if successful.
	static void test();
#endif

protected:
	/// Returns the sum-squared error for the specified set of ratings
	double validate(GNeuralNet* pNN, GMatrix& data);
};



/// This class performs bootstrap aggregation with collaborative filtering algorithms.
class GBagOfRecommenders : public GCollaborativeFilter
{
protected:
	std::vector<GCollaborativeFilter*> m_filters;
	size_t m_itemCount;

public:
	/// General-purpose constructor
	GBagOfRecommenders(GRand& rand);

	/// Deserialization constructor
	GBagOfRecommenders(GDomNode* pNode, GLearnerLoader& ll);

	/// Destructor
	virtual ~GBagOfRecommenders();

	/// Returns the vector of filters
	std::vector<GCollaborativeFilter*>& filters() { return m_filters; }

	/// Add a filter to the bag
	void addRecommender(GCollaborativeFilter* pRecommender);

	/// See the comment for GCollaborativeFilter::train
	virtual void train(GMatrix& data);

	/// See the comment for GCollaborativeFilter::predict
	virtual double predict(size_t user, size_t item);

	/// See the comment for GCollaborativeFilter::impute
	virtual void impute(double* pVec, size_t dims);

	/// Delete all of the filters
	void clear();

	/// See the comment for GCollaborativeFilter::serialize
	virtual GDomNode* serialize(GDom* pDoc);

#ifndef NO_TEST_CODE
	/// Performs unit tests. Throws if a failure occurs. Returns if successful.
	static void test();
#endif
};


} // namespace GClasses

#endif // __GRECOMMENDER_H__
