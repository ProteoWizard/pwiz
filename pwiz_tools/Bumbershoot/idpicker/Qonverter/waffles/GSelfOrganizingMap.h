/*                                                                     -*-c++-*-
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __SELFORGANIZINGMAP_H__
#define __SELFORGANIZINGMAP_H__

#include "GError.h"
#include "GTransform.h"
#include <vector>
#include <list>
#include <set>
#include <cmath>
#include <cassert>
#include <limits>

namespace GClasses {

class GMatrix;
class GRand;
class GDistanceMetric;

  class GSelfOrganizingMap;

  namespace SOM{
    ///A node in a self-organizing map
    class Node{
    public:
      /// The location in the map.  This is the grid coordinates in the
      /// original specification of the map algorithm from Kohonen.
      std::vector<double> outputLocation;

      /// The location in the input weight-space.
      std::vector<double> weights;

      /// Return a new dom node generated in pDoc representing this
      /// node object
      GDomNode* serialize(GDom*pDoc) const;

      /// Just make an empty node
      Node(){}

      /// Generate this node from the one serialized in the dom object
      Node(GDomNode* domObject);
    };

    ///Used for creating an array of nodes sorted by nearness to a
    ///source node.
    class NodeAndDistance{
    public:
      ///The index of the node to which this object gives the
      ///distance.  I don't use a pointer in case the node array gets
      ///copied or reallocated.
      std::size_t nodeIdx;
      ///The distance to the node from another point
      double distance;

      ///Create a NodeAndDistance object with the given index and
      ///distance
      NodeAndDistance(std::size_t nodeIdx, double distance)
	:nodeIdx(nodeIdx),distance(distance){}
      
      ///Return true if this node has a smaller distance than rhs, or
      ///on equal distance compares their indices, breaking ties.
      bool operator<(const NodeAndDistance& rhs) const{
	return 
	  distance < rhs.distance || 
	  (distance == rhs.distance && nodeIdx < rhs.nodeIdx);
      }   
    };
    






    ///Way of initializing the node positions according to a given
    ///topology - for example: points on a grid, on a triangular
    ///lattice, or random points in space.  
    ///
    ///outputAxesMax are taken to be non-negative numbers
    class NodeLocationInitialization{
    public:
      ///Initializes the locations in the given vector of nodes
      ///according to this topology and the maximum values of the
      ///output axes.  If there are not enough nodes for the given
      ///topology, the node vector will be reallocated.
      virtual void setLocations(std::vector<double> outputAxesMax, 
				std::vector<Node>& nodes) = 0;

      ///Virtual destructor for good memory hygiene
      virtual ~NodeLocationInitialization(){}
    };

    ///Set the nodes to lie on an integer grid within the given
    ///maxima.  A grid with 10,10 maximum is assumed to go from 0..9.
    ///NOTE: if the difference between a dimensional maximum and the
    ///nearest integer is less than 1e-6 then the maximum is taken to
    ///be that integer.  Otherwise it is taken to be the maximum
    ///rounded down
    class GridTopology: public NodeLocationInitialization{
      ///When an axis maximum is within epsilon of the nearest
      ///integer, it is taken to be that integer, otherwise, it is
      ///rounded down.
      inline double epsilon() const { return 1e-6; }
    public:
      ///see comment on NodeLocationInitialization::setLocations
      virtual void setLocations(std::vector<double> outputAxesMax, 
				std::vector<Node>& nodes);

      ///Virtual destructor for good memory hygiene
      virtual ~GridTopology(){}
    };










    ///Algorithm to initialize the weights of the nodes in the network
    ///before training.  
    class NodeWeightInitialization{
    public:
      ///Sets the weights of the nodes in the vector nodes according
      ///to this algorithm, assuming the network will be trained on
      ///the data from pIn.  Also sets weightDistance's relation
      ///attribute according to the relation for the matrix
      virtual void setWeights(std::vector<Node>& nodes, 
			      GDistanceMetric& weightDistance,
			      GMatrix* pIn) const = 0;

      ///Virtual destructor for good memory hygiene
      virtual ~NodeWeightInitialization(){}
    };

    ///Initializes the weights to a random sample of rows from the
    ///training set
    class NodeWeightInitializationTrainingSetSample
      :public NodeWeightInitialization{
      //The random number generator used for the initialization.  It
      //is mutable because the state of the RNG is not really
      //considered part of the state of the object from the user's
      //point of view, thus it can change and the object is
      //semantically the same.
      mutable GRand* pRand; 
    public:
      ///Create a TraningSetSample object that uses the random numbers
      ///generated by pRand.  pRand is assumed to have a lifetime
      ///exceeding the lifetime of this object.  If pRand is NULL,
      ///uses the global random number generator.
      NodeWeightInitializationTrainingSetSample(GRand* pRand = NULL);

      ///Sets the weights of the nodes in the vector nodes to the a
      ///random sample of the rows of pIn chosen without replacement.
      ///Note that there must be at least as many vectors in pIn as
      ///there are nodes in nodes.  Also, note that in the current
      ///implementation, if the number of nodes is close to the number
      ///of weights, selecting without replacement can take a very
      ///long time.
      ///
      ///see comment on NodeWeightInitialization::setWeights
      virtual void setWeights(std::vector<Node>& nodes, 
			      GDistanceMetric& weightDistance,
			      GMatrix* pIn) const;

      ///Virtual destructor for good memory hygiene
      virtual ~NodeWeightInitializationTrainingSetSample(){}
    };










    ///Reports periodically on the training of a self-organizing map -
    ///writing status to a stream every so many seconds or iterations,
    ///writing visualizations of the network or the network itself to
    ///sequentially named files.
    ///
    ///TODO: write class for print stream
    ///reporters, network writing reporters and visualization writing
    ///reporters, and classes for only calling reporters at different
    ///intervals
    class Reporter{
    public:
      ///Reset this reporter to the beginning and tell it that there
      ///will be maxIterations iterations each consisting of
      ///maxSubIterations sub-iterations.  (An iteration count is
      ///unknown if given as -1).  Give the reporter access to the
      ///training data on which the map will be trained.  The reporter
      ///does not own the training data.
      virtual void start(const GMatrix* trainingData, 
			 int maxIterations = -1, int maxSubIterations = -1){};

      ///Tell the reporter the current status of the training
      virtual void newStatus(unsigned iteration, unsigned subIteration, 
			     const GSelfOrganizingMap& map) = 0;

      ///Tell the reporter that the training has stopped at the given
      ///iteration and sub-iteration
      virtual void stop(unsigned iteration, unsigned subIteration, 
			const GSelfOrganizingMap& map){};

      ///Destruct this reporter object
      virtual ~Reporter(){};
    };

    /// Calls its sub-reporter on start, the first iteration of a
    /// block of "interval" iterations, and finally on stop
    class IterationIntervalReporter: public Reporter{
      ///The reporter called by this IterationIntervalReporter
      smart_ptr<Reporter> m_subReporter;
      ///The reporting interval, only the first in every m_interval
      ///calls to newStatus results in a call to the subReporter
      unsigned m_interval;
      ///The number of calls to newStatus received since the last time
      ///m_subReporter->newStatus was called
      unsigned m_callsSinceLastReport;
    public:
      /// Sets up this reporter to call the subReporter the first out
      /// of every interval status updates
      IterationIntervalReporter(smart_ptr<Reporter>& subReporter, 
				unsigned interval)
	:m_subReporter(subReporter), 
	 m_interval(interval), m_callsSinceLastReport(interval-1){}

      ///Call the sub-reporter's start
      virtual void start(const GMatrix* trainingData, 
			 int maxIterations = -1, int maxSubIterations = -1){
	m_subReporter->start(trainingData, maxIterations, maxSubIterations);
      };

      ///Call the newStatus of the subreporter the first time this is
      ///called, then wait interval-1 calls before calling it again.
      virtual void newStatus(unsigned iteration, unsigned subIteration, 
			     const GSelfOrganizingMap& map){
	if(++m_callsSinceLastReport >= m_interval){
	  m_callsSinceLastReport = 0;
	  m_subReporter->newStatus(iteration,subIteration, map);
	}
      }

      ///Call the sub-reporter's stop
      virtual void stop(unsigned iteration, unsigned subIteration, 
			const GSelfOrganizingMap& map){
	  m_subReporter->stop(iteration,subIteration, map);
      };

      ///Destruct this reporter object
      virtual ~IterationIntervalReporter(){};
    };


    ///A ReporterChain contains a list of Reporter objects.  When a
    ///method is called on the ReporterChain, it calls the same method
    ///on each of its sub-objects in turn.
    class ReporterChain:public Reporter{
      ///The list of reporters chained by this ReporterChain
      std::vector<Reporter*> m_reporters;
      typedef std::vector<Reporter*>::iterator iterator;
    public:
      ///Create an empty reporter chain
      ReporterChain():m_reporters(){}

      ///Add a reporter to the chain.  The chain is responsible for
      ///deleting the reporter.
      virtual void add(Reporter*toAdd);
      
      ///Call start on each of the sub-reporters in turn
      virtual void start(const GMatrix* trainingData, 
			 int maxIterations = -1, int maxSubIterations = -1);

      ///Call newStatus with these parameters on each of the
      ///sub-reporters in turn
      virtual void newStatus(unsigned iteration, unsigned subIteration, 
			     const GSelfOrganizingMap& map);

      ///Call stop with these parameters on each of the sub-reporters
      ///in turn
      virtual void stop(unsigned iteration, unsigned subIteration, 
			const GSelfOrganizingMap& map);

      ///Delete all sub-reporters then dispose of self
      virtual ~ReporterChain();
    };
    

    /// Writes out sequentially numbered svg files giving the weight
    /// locations in 2 dimensions of input space connected by a mesh
    /// that connects each weight with its nearest neighbors.  Writes
    /// one file each time newStatus is called and once when stop is
    /// called.  The output of stop may duplicate the last newStatus's
    /// output, but is not guaranteed to.
    ///
    /// Generated filenames will be of the form: base_d+.svg (where d+
    /// means 1 or more decimal digits).  If the training algorithm
    /// gives a good estimate of the number of iterations and
    /// sub-iterations, the number of digits in the name will be the
    /// same for all generated filenames.  If the estimate is low,
    /// then the filename size may increase.  If it does not give any
    /// estimate, more digits will be added at the 10 millionth
    /// filename.  The digits will start counting at 1.
    class SVG2DWeightReporter:public Reporter{
    public:
      struct Point{ double x; double y; };
    private:
      /// The base filename to use.  
      const std::string m_baseFilename;
      /// The next number to use in a filename
      unsigned m_nextNumberToOutput;
      /// The minimum number of digits to use in the filename
      unsigned m_totalDigits;
      /// The index of the dimension to use as the x dimension
      const std::size_t m_xDim;
      /// The index of the dimension to use as the y dimension
      const std::size_t m_yDim;
      
      /// True if should show the training data on the output graphic,
      /// false otherwise.
      const bool m_showTrainingData;
      /// If should show training data and start has been called, this
      /// holds a copy of the x and y coordinates of the training data
      /// passed in at the last call to start.  Otherwise it is empty.
      std::vector<Point> m_trainingData;

      /// Return the number of neighbors needed to request from the
      /// given map to get a connected graph.  Otherwise, return
      /// m_neighborsNeeded.
      unsigned neighborsNeeded(const GSelfOrganizingMap& map);
    public:
      ///Create a weight reporter that will output the xDim,yDim
      ///dimensions to a filename created from baseFilename.  If
      ///showTrainingData is true then the projection of the training
      ///data will also be displayed in the output files along with
      ///the network.
      SVG2DWeightReporter(std::string baseFilename, 
			  std::size_t xDim, std::size_t yDim, 
			  bool showTrainingData = false)
	:m_baseFilename(baseFilename), m_nextNumberToOutput(1), 
	 m_xDim(xDim), m_yDim(yDim), 
	 m_showTrainingData(showTrainingData), m_trainingData(){}

      ///See comment on Reporter::start(GMatrix*,int,int)
      virtual void start(const GMatrix* trainingData, 
			 int maxIterations = -1, int maxSubIterations = -1);

      ///Output the weight visualization to the next filename
      virtual void newStatus(unsigned iteration, unsigned subIteration, 
			     const GSelfOrganizingMap& map);


      ///Output the weight visualization for the final state
      virtual void stop(unsigned iteration, unsigned subIteration, 
			const GSelfOrganizingMap& map);

      ///Virtual destructor for good memory hygiene      
      virtual ~SVG2DWeightReporter(){}
    };

    ///A reporter that does nothing
    class NoReporting:public Reporter{
    public:
      NoReporting(){}

      //Do nothing
      virtual void start(const GMatrix* trainingData, 
			 int maxIterations = -1, int maxSubIterations = -1){}

      //Do nothing
      virtual void newStatus(unsigned /*iteration*/, unsigned /*subIteration*/, 
			     const GSelfOrganizingMap& /*map*/){};

      //Do nothing
      virtual void stop(unsigned /*iteration*/, unsigned /*subIteration*/, 
			const GSelfOrganizingMap& /*map*/){}

      ///Virtual destructor for good memory hygiene
      virtual ~NoReporting(){}
    };

    ///Function that given a width, and a distance from the center of
    ///the neighborhood returns a weight to be used to calculate the
    ///influence of neighboring nodes at that distance.  For each
    ///radius, can tell a distance d (possibly infinity) from the
    ///center for which all weights for distances greater than or
    ///equal to d will be 0.
    class NeighborhoodWindowFunction:public std::binary_function<double,double,double>{
    public:
      ///Returns the weight of this window function at the given width
      ///and distance.
      virtual double operator()(double width, double distance) const = 0;
      ///If d >= minZeroDistance(width) then operator()(width, d) == 0
      ///
      ///This is essential for avoiding unnecesary computations at
      ///smaller window sizes
      virtual double minZeroDistance(double width) const  = 0;

      ///Virtual destructor for good memory hygiene      
      virtual ~NeighborhoodWindowFunction(){}
    };

    ///Uses a unit-height, zero-mean Gaussian weighting with the width
    ///as sigma truncated to 0 at 5 standard deviations
    class GaussianWindowFunction:public NeighborhoodWindowFunction{
      const static unsigned truncationSigmas = 5;
    public:
      ///Returns exp(-0.5(distance/width)^2) if distance < 5*width, 0
      ///otherwise
      ///
      ///See NeighborhoodWindowFunction::operator()
      virtual double operator()(double width, double distance) const;

      ///The Gaussian is truncated to 0 after 5 standard deviations
      ///
      ///See NeighborhoodWindowFunction::minZeroDistance()
      virtual double minZeroDistance(double width) const{ 
	return truncationSigmas*width; }

      ///Virtual destructor for good memory hygiene
      virtual ~GaussianWindowFunction(){}
    };

    ///Uses a unit-height, zero-mean Uniform weighting with the width
    ///being the radius of the circle anything beyond width is 0.
    class UniformWindowFunction:public NeighborhoodWindowFunction{
    public:
      ///Returns  if distance < width, 1, otherwise 0
      ///
      ///See NeighborhoodWindowFunction::operator()
      virtual double operator()(double width, double distance) const{
	return (distance < width? 1: 0); }

      ///The Uniform is truncated to 0 at a distance of width
      ///
      ///See NeighborhoodWindowFunction::minZeroDistance()
      virtual double minZeroDistance(double width) const{ 
	return width; }

      ///Virtual destructor for good memory hygiene
      virtual ~UniformWindowFunction(){}
    };

    /// An algorithm for training self-organizing maps.  Before
    /// training is started, it is expected that the nodes are
    /// allocated and that the geometry of the map has been set by
    /// giving each node a position and a distance function.  However,
    /// the weight vectors and the output dimensionality will be
    /// completely overwritten by training.
    class TrainingAlgorithm{
    public:
      /// Add this training algorithm to pDoc and return the resulting
      /// node Right now, default implementation is the only one there
      /// and it just adds an object with no fields.  
      /// TODO: make serialize a pure virtual method and implement it in all the training algorithm subclasses
      virtual GDomNode* serialize(GDom* pDoc) const;

      /// Create the correct type of training algorithm from the given dom node.
      /// Right now just returns a pointer to a DummyTrainingAlgorithm
      /// TODO: fix deserialize so training algorithms are really serialized
      static TrainingAlgorithm* deserialize(GDomNode* pNode);

      /// Train the map.  Subclassers see also
      /// TrainingAlgorithm::setPRelationBefore
      virtual void train(GSelfOrganizingMap& map, GMatrix* pIn) = 0;

      /// Virtual destructor
      virtual ~TrainingAlgorithm(){}
    protected:
      /// Return the weight distance function so it's dimensionality
      /// can be modified by training algorithms.
      GDistanceMetric& weightDistance(GSelfOrganizingMap& map);

      /// Set map.m_pRelationBefore to newval.  All subclasses must
      /// call this in their train methods so that the map will appear
      /// trained for the purposes of GIncrementalTransform
      void setPRelationBefore(GSelfOrganizingMap& map, sp_relation& newval);
    };

    /// A training algorithm that throws an exception when train is
    /// called - stub for fully serializing training algorithms
    class DummyTrainingAlgorithm: public TrainingAlgorithm{
    public:
      ///Create a dummy training algorithm (and achieve whirled peas)
      DummyTrainingAlgorithm(){}

      /// Throw error
      virtual void train(GSelfOrganizingMap& map, GMatrix* pIn){
	ThrowError("Training of self-organizing maps loaded from files is currently unsupported");
      }
      ///Destructor to ensure good memory hygiene
      virtual ~DummyTrainingAlgorithm(){}      
    };

    ///Implements the batch training algorithm for self-organizing
    ///maps as described in T. Kohonen "Self Organizing Maps" Third
    ///Edition, 2001, published by Springer
    class BatchTraining:public TrainingAlgorithm{
      /// The initial neighborhood size
      const double m_initialNeighborhoodSize;

      /// The final neighborhood size
      const double m_finalNeighborhoodSize;

      /// The factor in the exponential decay equation: curSize =
      /// initialNeighborhoodSize*exp(timeFactor*iterationNumber) --
      /// where iterationNumber starts at 0.
      const double m_timeFactor;

      /// Number of iterations for which to run the training
      const unsigned m_numIterations;

      /// Maximum number of sub-iterations to wait for convergence at
      /// a fixed neighborhood size before going on
      const unsigned m_maxSubIterationsBeforeChangingNeighborhood;

      /// Weight initialization function to be applied at the start of
      /// training
      /// Owned by this object
      const NodeWeightInitialization* m_weightInitialization;

      /// Window function used with the neighborhood size to determine
      /// influence of one neighbor on another
      /// Owned by this object
      const NeighborhoodWindowFunction* m_windowFunc;

      /// Reporter informed of training progress
      /// Owned by this object
      Reporter* m_reporter;
    public:
      /// Create a batch algorithm that starts its neighborhood width
      /// at initialNeighborhoodSize and decreases it exponentially to
      /// finalNeighborhoodSize over numIterations steps.  In each
      /// iteration, at most maxSubIterationsBeforeChangingNeighborhood
      /// (which must be at least 1) passes of the algorithm will be
      /// done with a fixed neighborhood before the next iteration with
      /// a different neighborhood size is started.  If convergence
      /// occurs, less sub-iterations will be used.
      /// weightInitialization is the initialization function that will
      /// be used to initialize the node weights at the start of
      /// training.  windowFunc is the window function used to
      /// determine the influence of neighbors on one another.
      /// reporter is the Reporter object that will be called to report
      /// progress during training.
      ///
      /// The training object owns weightInialization, windowFunc, and
      /// reporter and so is responsible for deleting them.
      BatchTraining(double initialNeighborhoodSize, 
		    double finalNeighborhoodSize,
		    unsigned numIterations,
		    unsigned maxSubIterationsBeforeChangingNeighborhood,
		    NodeWeightInitialization* weightInitialization,
		    NeighborhoodWindowFunction* windowFunc,
		    Reporter* reporter);
      
      /// Train the map
      virtual void train(GSelfOrganizingMap& map, GMatrix* pIn);

      virtual ~BatchTraining();
    };


    /// Implments the traditional step-wise training of self-organized maps 
    /// //TODO: finish this comment
    class TraditionalTraining:public TrainingAlgorithm{
      /// The initial neighborhood size
      const double m_initialWidth;

      /// The final neighborhood size
      const double m_finalWidth;

      /// The factor in the exponential decay equation: curWidth =
      /// initialWidth*exp(widthFactor*iterationNumber) --
      /// where iterationNumber starts at 0.
      const double m_widthFactor;

      /// The initial learning rate
      const double m_initialRate;

      /// The final learning rate
      const double m_finalRate;

      /// The factor in the exponential decay equation: curRate =
      /// initialRate*exp(rateFactor*iterationNumber) --
      /// where iterationNumber starts at 0.
      const double m_rateFactor;

      /// Number of iterations for which to run the training
      const unsigned m_numIterations;

      /// Weight initialization function to be applied at the start of
      /// training
      /// Owned by this object
      const NodeWeightInitialization* m_weightInitialization;

      /// Window function used with the neighborhood size to determine
      /// influence of one neighbor on another
      /// Owned by this object
      const NeighborhoodWindowFunction* m_windowFunc;

      /// Reporter informed of training progress
      /// Owned by this object
      Reporter* m_reporter;
    public:
      /// Create a traditional SOM training algorithm that starts its
      /// learning rate and neighborhood width at initialWidth and
      /// initialRate then decreases them exponentially so that they
      /// both reach finalWidth and finalRate after numIterations
      /// iterations.  Each iteration consists of one presentation of
      /// an input datum to the network and one weight update of the
      /// neighbors of the winning neuron at the current learning
      /// rate.
      ///
      /// weightInitialization is the initialization function that
      /// will be used to initialize the node weights at the start of
      /// training.  windowFunc is the window function used to
      /// determine the influence of neighbors on one another.
      /// reporter is the Reporter object that will be called to
      /// report progress during training.
      ///
      /// The training object owns weightInialization, windowFunc, and
      /// reporter and so is responsible for deleting them.
      TraditionalTraining(double initialWidth, double finalWidth, 
			  double initialRate, double finalRate,
			  unsigned numIterations,
			  NodeWeightInitialization* weightInitialization,
			  NeighborhoodWindowFunction* windowFunc,
			  Reporter* reporter);
      
      /// Train the map
      virtual void train(GSelfOrganizingMap& map, GMatrix* pIn);

      virtual ~TraditionalTraining();
    };


  } //namespace SOM

/// An implementation of a Kohonen self-organizing map
///
/// Note: does not support more than 2^52 nodes -- I don't believe
/// this will be a problem within the next 20 years.
///
/// See: T. Kohonen "Self Organizing Maps" Third Edition, 2001,
/// published by Springer
class GSelfOrganizingMap : public GIncrementalTransform
{
  friend class SOM::TrainingAlgorithm;
protected:
  ///Number of input dimensions
  unsigned m_nInputDims;
  ///Vector containing the maximum number in each output axis - so
  ///axis #0 would have a range 0..m_vOutputAxes[0].  Remember to call
  ///m_pNodeDistance->init whenever you change the number of output
  ///axes.
  std::vector<double> m_outputAxes;
  ///The algorithm to be used to train this map on the next call to
  ///train. This object owns the training algorithm and is responsable
  ///for deleting it.
  SOM::TrainingAlgorithm* m_pTrainer;
  ///The distance function to use when finding the closest weight to a
  ///given input point - owned by this object.  Remember to call init
  ///on this whenever you change the number of weights.
  GDistanceMetric* m_pWeightDistance;
  ///The distance function to use when calculating the distance
  ///between nodes in the map.  If you change this, remember to
  ///invalidate the neighbor structure. - owned by this object.
  ///Remember to call init whenever you change the number of output
  ///axes.
  GDistanceMetric* m_pNodeDistance;

  ///The nodes in the map.  Do not change the order of nodes once
  ///sortedNeighbors has been created unless you also invalidate
  ///sortedNeighbors.  Remember to call m_pWeightDistance->init if you
  ///change the number of weights.
  std::vector<SOM::Node> m_nodes;

  //True if m_sortedNeighbors is valid, false if it needs to be
  //regenerated from m_nodes and m_pNodeDistance
  mutable bool m_sortedNeighborsIsValid;

  //The data holding the sorted neighbors.  
  ///Each entry sortedNeighbors[i] contains a vector of all the other
  ///nodes sorted by their distance from i in outputSpace.
  //Check m_sortedNeighborsIsValid before using directly - or use
  //sortedNeighbors() to automatically create a valid version if the
  //current one is invalid
  mutable std::vector<std::vector<SOM::NodeAndDistance> > m_sortedNeighbors;
  
  ///Marks sortedNeighbors as invalid and need of regeneration
  void invalidateSortedNeighbors(){ m_sortedNeighborsIsValid = false; }

  ///Eliminates the current contents of m_sortedNeighbors and
  ///regenerates it from m_pNodeDistance and m_nodes.  sets
  ///m_sortedNeighborsIsValid on completion.  Note that the members
  ///this method changes are mutable, thus it can be marked as a const
  ///method and called from const methods.
  void regenerateSortedNeighbors() const;

  ///Return a sorted neighbor structure in which entry i is a list of
  ///the node indices sorted by their distance from node i
  const std::vector<std::vector<SOM::NodeAndDistance> > &sortedNeighbors() 
    const
  {
    if(!m_sortedNeighborsIsValid){ regenerateSortedNeighbors(); }
    return m_sortedNeighbors;
  }

  
public:
  /// Creates a map whose nodes are on a grid that uses euclidean
  /// distance to find the nearest point and is trained with the batch
  /// SOM algorithm with neighborhood decreasing exponentially from
  /// 2*nMapWidth to 1 in 100 super-epochs with each super-epoch
  /// involving waiting up to 100 iterations for convergence at that
  /// neighborhood width.  The neighbors affect one another through a
  /// unit height Gaussian with sigma=neighborhood size.  Every epoch
  /// and super-epoch, the reporter is notified.  This map will be
  /// responsible for deleting the dynamically allocated reporter.
  ///
  /// nMapDims specifies the number of dimensions for the map.
  ///
  /// nMapWidth specifies the size in one dimension of the map.
  ///
  /// (so if nMapDims is 3 and nMapWidth is 10, the map will
  /// contain 1000 (10^3) nodes.)
  GSelfOrganizingMap(int nMapDims, int nMapWidth, GRand* pRand, 
		     SOM::Reporter *r = new SOM::NoReporting());

  /// Create a self-organizing map.
  ///
  /// @param outputAxes each node in the network will have a vector
  /// location and component i of that vector will range from
  /// 0..outputAxes[i]
  /// 
  /// @param numNodes the number of nodes in the network
  /// 
  /// @param topology determines the locations assigned to each node
  ///
  /// @param trainer algorithm to train the network on data
  ///
  /// @param weightDistance the distance used in determining which
  /// node's weight-set is closest to an input point
  ///
  /// @param nodeDistance the distance used to determine the influence
  /// of two nodes with different coordinates on one another.
  GSelfOrganizingMap(const std::vector<double>& outputAxes,
		     std::size_t numNodes,
		     SOM::NodeLocationInitialization* topology,
		     SOM::TrainingAlgorithm* trainer,
		     GDistanceMetric* weightDistance,
		     GDistanceMetric* nodeDistance);

  /// Reconstruct this self-organizing map from its serialized form in
  /// a dom document
  GSelfOrganizingMap(GDomNode* pNode);
		     
  virtual ~GSelfOrganizingMap();
  
#ifndef NO_TEST_CODE
  /// Performs unit tests for this class. Throws an exception if there is a failure.
  static void test();
#endif
  
  /// Transforms pIn after training on it
  virtual GMatrix* doit(GMatrix& in);
  
  /// Add this map to a dom document and return the pointer to the
  /// tree added.
  /// <br/><br/>
  /// <strong>WARNING</strong>: the current imlementation DOES NOT
  /// SERIALIZE THE TRAINING ALGORITHM - do not train a
  /// GSelfOrganizingMap obtained from a dom file
  ///
  ///TODO: make all serialize's const
  virtual GDomNode* serialize(GDom*);

  ///see comment on GIncrementalTransform::train(GMatrix&)
  virtual void train(GMatrix& in){ 
    if(m_pTrainer != NULL){
      m_pTrainer->train(*this, &in);
    }
  }

  ///TODO: implement enableIncrementalTraining 
  virtual void beginIncrementalTraining(sp_relation&, double*, double*){
    ThrowError("BeginIncrementalTraining not yet implemented");
  }

  ///Transform the given vector from input coordinates to map
  ///coordinates by finding the best match among the nodes.
  ///
  ///see comment on GIncrementalTransform::(const double*, double*)
  void transform(const double*pIn, double*pOut);


  ///Return the index of the node whose weight vector best matches in
  ///under the distance metric for this SOM.  Assumes there is at
  ///least one node.
  std::size_t bestMatch(const double*pIn) const;

  ///Given a matrix containing input data of the correct dimensions,
  ///returns a vector v of nodes.size() indices into that matrix.
  ///v[i] is the index of the data in pData that gives the strongest
  ///response for node i.  That is, pData->row(v[i]) is the data
  ///element that best matches node i.
  ///
  ///Assumes there is at least one row in pData
  std::vector<std::size_t> bestData(const GMatrix*pData) const;

  /// Inspector giving the indices of the nearest neighbors in output
  /// space of the node at index nodeIdx.  If any neighbor with a
  /// given distance is returned all neighbors at that distance will
  /// be returned.  Any distances within epsilon of one another are
  /// considered equivalent.
  ///
  /// If there are enough nodes in the network, at least
  /// numNeigbors neighbors will be returned.  If there are fewer
  /// nodes in the network than the number of neighbors, a list of all
  /// indices will be returned.
  std::vector<std::size_t> nearestNeighbors(unsigned nodeIdx, 
					    unsigned numNeighbors, 
					    double epsilon = 1e-8) const;

  /// Return all neighbors of the node at that have a distance from
  /// nodeIdx of less than radius
  std::vector<SOM::NodeAndDistance> neighborsInCircle(unsigned nodeIdx,
						      double radius) const;

  /// Return the number of dimensions this takes as input
  unsigned inputDimensions() const { return m_nInputDims; }

  /// Return the number of dimensions this returns as output
  unsigned outputDimensions() const { return m_outputAxes.size(); }

  /// Return the maximum for each output axis
  std::vector<double> outputAxes() const{ return m_outputAxes; }

  /// Inspector for the nodes making up this map
  const std::vector<SOM::Node>& nodes() const{ return m_nodes; }

  /// Accessor for the nodes making up this map 
  std::vector<SOM::Node>& nodes() { return m_nodes; }

  /// Inspector for the distance metric used in input space, that is,
  /// between an input point and a weight for determining the winner.
  const GDistanceMetric* weightDistance() const { 
    return m_pWeightDistance; }

  /// Inspector for the distance metric used in output space, that is,
  /// between two nodes for their relative influence
  const GDistanceMetric* nodeDistance() const { 
    return m_pNodeDistance; }  
};

inline GDistanceMetric& 
SOM::TrainingAlgorithm::weightDistance(GSelfOrganizingMap& map){
  return *(map.m_pWeightDistance);
}

inline void 
SOM::TrainingAlgorithm::setPRelationBefore(GSelfOrganizingMap& map, sp_relation& newval){
  map.m_pRelationBefore = newval;
}



} // namespace GClasses

#endif // __SELFORGANIZINGMAP_H__
