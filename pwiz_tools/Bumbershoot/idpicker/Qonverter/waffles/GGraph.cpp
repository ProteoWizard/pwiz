/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GGraph.h"
#include <stddef.h>
#include <stdlib.h>
#include "GError.h"
#include "GBitTable.h"
#include "GHashTable.h"
#include "GRegion.h"
#include "GHeap.h"
#include "GMatrix.h"
#include "GNeighborFinder.h"
#include "GRand.h"
#include "GVec.h"
#include <vector>
#include <deque>
#include <cmath>

using namespace GClasses;
using std::vector;
using std::deque;

namespace GClasses {
struct GGraphCutNode
{
public:
	GGraphCutNode* m_pRoot;
	GGraphCutEdge* m_pParent;
	size_t m_nSibling;
	size_t m_nChild;
	struct GGraphCutEdge* m_pEdges;


	GGraphCutNode()
		: m_pRoot(NULL), m_pParent(NULL), m_nSibling(INVALID_INDEX), m_nChild(INVALID_INDEX), m_pEdges(NULL)
	{
	}

	struct GGraphCutNode* GetRoot()
	{
		return m_pRoot;
	}

	void SetRoot(struct GGraphCutNode* pRoot)
	{
		m_pRoot = pRoot;
	}

	struct GGraphCutEdge* GetParentEdge()
	{
		return m_pParent;
	}

	struct GGraphCutEdge* GetFirstEdge()
	{
		return m_pEdges;
	}

	void SetFirstEdge(struct GGraphCutEdge* pEdge)
	{
		m_pEdges = pEdge;
	}

	size_t GetFirstChild()
	{
		return m_nChild;
	}

	size_t GetSibling()
	{
		return m_nSibling;
	}
};

struct GGraphCutEdge
{
public:
	size_t m_nNode1;
	size_t m_nNode2;
	GGraphCutEdge* m_pNext1;
	GGraphCutEdge* m_pNext2;
	float m_fCapacity;

	struct GGraphCutEdge* GetNextEdge(size_t nNode)
	{
		if(nNode == m_nNode1)
			return m_pNext1;
		else if(nNode == m_nNode2)
			return m_pNext2;
		else
		{
			GAssert(false); // This edge isn't connected to that node
			return NULL;
		}
	}

	size_t GetOtherNode(size_t nNode)
	{
		if(nNode == m_nNode1)
			return m_nNode2;
		else if(nNode == m_nNode2)
			return m_nNode1;
		else
		{
			GAssert(false); // This edge isn't connected to that node
			return INVALID_INDEX;
		}
	}
};
}


// ---------------------------------------------------------------------------


GGraphCut::GGraphCut(size_t nNodes)
{
	m_pHeap = new GHeap(1024);
	m_nNodes = nNodes;
	m_pNodes = new GGraphCutNode[nNodes];
	m_pSource = NULL;
	m_pSink = NULL;
}

GGraphCut::~GGraphCut()
{
	delete(m_pHeap);
	delete[] m_pNodes;
}

void GGraphCut::addEdge(size_t nNode1, size_t nNode2, float fCapacity)
{
	GAssert(nNode1 != nNode2); // edge to itself?
	GAssert(nNode1 != INVALID_INDEX && nNode1 < m_nNodes); // out of range
	GAssert(nNode2 != INVALID_INDEX && nNode2 < m_nNodes); // out of range
	struct GGraphCutEdge* pNewEdge = (struct GGraphCutEdge*)m_pHeap->allocate(sizeof(struct GGraphCutEdge));
	pNewEdge->m_nNode1 = nNode1;
	pNewEdge->m_nNode2 = nNode2;
	pNewEdge->m_pNext1 = m_pNodes[nNode1].GetFirstEdge();
	m_pNodes[nNode1].SetFirstEdge(pNewEdge);
	pNewEdge->m_pNext2 = m_pNodes[nNode2].GetFirstEdge();
	m_pNodes[nNode2].SetFirstEdge(pNewEdge);
	pNewEdge->m_fCapacity = fCapacity;
}

void GGraphCut::getEdgesFromRegionList(GRegionAjacencyGraph* pRegionList)
{
	size_t nNode1, nNode2;
	float r1, g1, b1, r2, g2, b2;
	for(size_t i = 0; i < pRegionList->ajacencyCount(); i++)
	{
		pRegionList->ajacency(i, &nNode1, &nNode2);
		pRegionList->averageColor(nNode1, &r1, &g1, &b1);
		pRegionList->averageColor(nNode2, &r2, &g2, &b2);
		r1 -= r2;
		g1 -= g2;
		b1 -= b2;
		r1 *= r1;
		g1 *= g1;
		b1 *= b1;
		addEdge(nNode1, nNode2, (float)1 / (r1 + g1 + b1 + 1));
	}
}

void GGraphCut::recycleTree(size_t nChild, size_t nParent)
{
	struct GGraphCutNode* pChild = &m_pNodes[nChild];
	struct GGraphCutNode* pParent = &m_pNodes[nParent];
	GAssert(pChild->GetRoot()); // Expected the child node to be part of a tree

	// Unlink the child from the parent
	size_t nPrev = INVALID_INDEX;
	size_t nCurrent;
	for(nCurrent = pParent->m_nChild; nCurrent != nChild && nCurrent != INVALID_INDEX; nCurrent = m_pNodes[nCurrent].m_nSibling)
		nPrev = nCurrent;
	if(nCurrent == INVALID_INDEX)
	{
		GAssert(false); // Failed to find the child
		return;
	}
	if(nPrev != INVALID_INDEX)
		m_pNodes[nPrev].m_nSibling = pChild->m_nSibling;
	else
		pParent->m_nChild = pChild->m_nSibling;
	pChild->m_nSibling = INVALID_INDEX;
	pChild->m_pParent = NULL;
	pChild->m_pRoot = NULL;
	//GAssert(!pChild->IsAncestor(pParent)); // still linked

/*
	// Attempt to find another suitable parent for the tree
	struct GGraphCutEdge* pCandEdge;
	struct GGraphCutNode* pCandidate;
	for(pCandEdge = pChild->GetFirstEdge(); pCandEdge; pCandEdge = pCandEdge->GetNextEdge(pChild))
	{
		if(pCandEdge->m_fCapacity <= 0)
			continue;
		pCandidate = pCandEdge->GetOtherNode(pChild);
		if(pCandidate->GetRoot() != pOldRoot)
			continue;
		if(pCandidate->IsAncestor(pChild))
			continue;
		pCandidate->LinkChild(pCandEdge);
		return;
	}
*/

	// Recursively recycle the children
	size_t nGrandChild;
	while(true)
	{
		nGrandChild = pChild->GetFirstChild();
		if(nGrandChild == INVALID_INDEX)
			break;
		recycleTree(nGrandChild, nChild);
	}
	GAssert(pChild->GetFirstChild() == INVALID_INDEX && !pChild->GetParentEdge()); // Didn't recycle properly

	// Any rooted neighbors of the child now become active since they have an unclaimed neighbor
	struct GGraphCutEdge* pEdge;
	size_t nOther;
	for(pEdge = pChild->GetFirstEdge(); pEdge; pEdge = pEdge->GetNextEdge(nChild))
	{
		if(pEdge->m_fCapacity <= 0)
			continue;
		nOther = pEdge->GetOtherNode(nChild);
		if(m_pNodes[nOther].GetRoot())
			m_q.push_back(nOther);
	}
}

// pEdge is the edge that joins the two trees
void GGraphCut::augmentPath(struct GGraphCutEdge* pEdge)
{
	// Find the bottle-neck
	float fBottleneck = pEdge->m_fCapacity;
	size_t nNode = pEdge->m_nNode1;
	struct GGraphCutEdge* pParentEdge = m_pNodes[nNode].GetParentEdge();
	while(pParentEdge)
	{
		GAssert(pParentEdge->m_fCapacity > 0); // Expected a non-saturated edge
		if(pParentEdge->m_fCapacity < fBottleneck)
			fBottleneck = pParentEdge->m_fCapacity;
		nNode = pParentEdge->GetOtherNode(nNode);
		pParentEdge = m_pNodes[nNode].GetParentEdge();
	}
	nNode = pEdge->m_nNode2;
	pParentEdge = m_pNodes[nNode].GetParentEdge();
	while(pParentEdge)
	{
		GAssert(pParentEdge->m_fCapacity > 0); // Expected a non-saturated edge
		if(pParentEdge->m_fCapacity < fBottleneck)
			fBottleneck = pParentEdge->m_fCapacity;
		nNode = pParentEdge->GetOtherNode(nNode);
		pParentEdge = m_pNodes[nNode].GetParentEdge();
	}

	// Augment
	pEdge->m_fCapacity -= fBottleneck;
	nNode = pEdge->m_nNode1;
	pParentEdge = m_pNodes[nNode].GetParentEdge();
	while(pParentEdge)
	{
		pParentEdge->m_fCapacity -= fBottleneck;
		if(pParentEdge->m_fCapacity <= 0)
			recycleTree(nNode, pParentEdge->GetOtherNode(nNode));
		nNode = pParentEdge->GetOtherNode(nNode);
		pParentEdge = m_pNodes[nNode].GetParentEdge();
	}
	nNode = pEdge->m_nNode2;
	pParentEdge = m_pNodes[nNode].GetParentEdge();
	while(pParentEdge)
	{
		pParentEdge->m_fCapacity -= fBottleneck;
		if(pParentEdge->m_fCapacity <= 0)
			recycleTree(nNode, pParentEdge->GetOtherNode(nNode));
		nNode = pParentEdge->GetOtherNode(nNode);
		pParentEdge = m_pNodes[nNode].GetParentEdge();
	}
}

void GGraphCut::growNode(size_t nNode)
{
	struct GGraphCutNode* pNode = &m_pNodes[nNode];
	struct GGraphCutEdge* pEdge;
	size_t nCandidate;
	struct GGraphCutNode* pCandidate;
	struct GGraphCutNode* pCandRoot;
	bool bRedo = false;
	for(pEdge = pNode->GetFirstEdge(); pEdge; pEdge = bRedo ? pEdge : pEdge->GetNextEdge(nNode))
	{
		bRedo = false;
		if(pEdge->m_fCapacity <= 0)
			continue; // this edge has no residual capacity
		nCandidate = pEdge->GetOtherNode(nNode);
		pCandidate = &m_pNodes[nCandidate];
		pCandRoot = pCandidate->GetRoot();
		if(pCandRoot == pNode->GetRoot())
			continue; // the candidate node is already part of this tree
		if(pCandRoot)
		{
			// The two trees now touch, so augment the path in order to break up the connection
			augmentPath(pEdge);
			if(!pNode->GetRoot())
				return; // this node is no longer part of a tree, so it can't grow anymore
			bRedo = true; // this edge may be valid now, so try it again
		}
		else
		{
			// Link the candidate as a new child of pNode
			GAssert(pCandidate->m_nChild == INVALID_INDEX); // That node has children of its own
			GAssert(pNode != pCandidate); // Circular edge!
			GAssert(pNode->m_pParent != pEdge); // That's the node's parent edge!
			pCandidate->m_pRoot = pNode->m_pRoot;
			pCandidate->m_pParent = pEdge;
			pCandidate->m_nSibling = pNode->m_nChild;
			pCandidate->m_nChild = INVALID_INDEX;
			pNode->m_nChild = nCandidate;

			// The candidate is now active
			m_q.push_back(nCandidate);
		}
	}
}

void GGraphCut::cut(size_t nSourceNode, size_t nSinkNode)
{
	// Push the source and sink into the queue
	struct GGraphCutNode* pNode;
	pNode = &m_pNodes[nSourceNode];
	pNode->SetRoot(pNode);
	m_pSource = pNode;
	m_q.push_back(nSourceNode);
	pNode = &m_pNodes[nSinkNode];
	pNode->SetRoot(pNode);
	m_pSink = pNode;
	m_q.push_back(nSinkNode);

	// Grow the trees
	size_t nNode;
	while(m_q.size() > 0)
	{
		nNode = m_q.front();
		m_q.pop_front();
		if(!m_pNodes[nNode].GetRoot())
			continue; // The node is no longer part of either tree
		growNode(nNode);
	}
}

void GGraphCut::findAHome(size_t nNode)
{
	// Find a new root for this node
	struct GGraphCutNode* pNode = &m_pNodes[nNode];
	struct GGraphCutNode* pNewRoot = NULL;
	struct GGraphCutNode* pOther;
	struct GGraphCutEdge* pEdge;
	struct GGraphCutEdge* pFollowEdge;
	size_t nCurrent = nNode;
	size_t nSafety = 500;
	while(!pNewRoot && --nSafety > 0)
	{
		pFollowEdge = m_pNodes[nCurrent].GetFirstEdge();
		size_t edgeCount = 0;
		for(pEdge = pFollowEdge; pEdge; pEdge = pEdge->GetNextEdge(nCurrent))
		{
			pOther = &m_pNodes[pEdge->GetOtherNode(nCurrent)];
			pNewRoot = pOther->GetRoot();
			if(pNewRoot)
				break;
			edgeCount++;
			if((rand() % edgeCount) == 0)
				pFollowEdge = pEdge;
		}
		if(!pFollowEdge)
		{
			pNode->SetRoot(m_pSink);
			return;
		}
		nCurrent = pFollowEdge->GetOtherNode(nCurrent);
	}
	if(!pNewRoot)
		pNewRoot = m_pSink;

	// Give neighbors the same root
	GAssert(m_q.size() == 0); // queue still in use
	pNode->SetRoot(pNewRoot);
	m_q.push_back(nNode);
	size_t nOther;
	while(m_q.size() > 0)
	{
		nCurrent = m_q.front();
		m_q.pop_front();
		for(pEdge = m_pNodes[nCurrent].GetFirstEdge(); pEdge; pEdge = pEdge->GetNextEdge(nCurrent))
		{
			nOther = pEdge->GetOtherNode(nCurrent);
			pOther = &m_pNodes[nOther];
			if(!pOther->GetRoot())
			{
				pOther->SetRoot(pNewRoot);
				m_q.push_back(nOther);
			}
		}
	}
}

bool GGraphCut::isSource(size_t nNode)
{
	if(!m_pSource)
		ThrowError("You must call 'Cut' before calling this method");
	GAssert(nNode != INVALID_INDEX && nNode < m_nNodes); // out of range
	struct GGraphCutNode* pNode = &m_pNodes[nNode];
	if(!pNode->GetRoot())
		findAHome(nNode);
	return(pNode->GetRoot() == m_pSource);
}

bool GGraphCut::doesBorderTheCut(size_t nNode)
{
	bool bSource = isSource(nNode);
	struct GGraphCutNode* pNode = &m_pNodes[nNode];
	struct GGraphCutEdge* pEdge;
	for(pEdge = pNode->GetFirstEdge(); pEdge; pEdge = pEdge->GetNextEdge(nNode))
	{
		if(isSource(pEdge->GetOtherNode(nNode)) != bSource)
			return true;
	}
	return false;
}


#ifndef NO_TEST_CODE
//     2--3--4
//   / |  |  | \        .
// 0   |  |  |  1
//   \ |  |  | /
//     5--6--7
// static
void GGraphCut::test()
{
	GGraphCut graph(8);
	graph.addEdge(0, 2, 3);
	graph.addEdge(0, 5, 3);
	graph.addEdge(1, 4, 3);
	graph.addEdge(1, 7, 3);
	graph.addEdge(2, 3, 3);
	graph.addEdge(3, 4, 1);
	graph.addEdge(5, 6, 1);
	graph.addEdge(6, 7, 3);
	graph.addEdge(2, 5, 3);
	graph.addEdge(3, 6, 1);
	graph.addEdge(4, 7, 3);
	graph.cut(0, 1);
	if(!graph.isSource(0))
		throw "got the source wrong!";
	if(!graph.isSource(2))
		throw "got the sink wrong!";
	if(!graph.isSource(3))
		throw "wrong cut";
	if(!graph.isSource(5))
		throw "wrong cut";
	if(graph.isSource(1))
		throw "wrong cut";
	if(graph.isSource(4))
		throw "wrong cut";
	if(graph.isSource(6))
		throw "wrong cut";
	if(graph.isSource(7))
		throw "wrong cut";
}
#endif // !NO_TEST_CODE

// --------------------------------------------------------------------

GGraphEdgeIterator::GGraphEdgeIterator(GGraphCut* pGraph, size_t nNode)
{
	m_pGraph = pGraph;
	reset(nNode);
}

GGraphEdgeIterator::~GGraphEdgeIterator()
{
}

void GGraphEdgeIterator::reset(size_t nNode)
{
	GAssert(nNode != INVALID_INDEX && nNode < m_pGraph->nodeCount()); // out of range
	m_nNode = nNode;
	m_pCurrentEdge = m_pGraph->m_pNodes[nNode].GetFirstEdge();
}

bool GGraphEdgeIterator::next(size_t* pNode, float* pEdgeWeight, bool* pOutgoing)
{
	if(!m_pCurrentEdge)
		return false;
	if(m_pCurrentEdge->m_nNode1 == m_nNode)
	{
		*pNode = m_pCurrentEdge->m_nNode2;
		*pOutgoing = true;
	}
	else
	{
		GAssert(m_pCurrentEdge->m_nNode2 == m_nNode); // bad edge
		*pNode = m_pCurrentEdge->m_nNode1;
		*pOutgoing = false;
	}
	*pEdgeWeight = m_pCurrentEdge->m_fCapacity;
	m_pCurrentEdge = m_pCurrentEdge->GetNextEdge(m_nNode);
	return true;
}









GFloydWarshall::GFloydWarshall(size_t nodes)
{
	m_nodes = nodes;
	m_pCosts = new GMatrix(nodes, nodes);
	m_pCosts->setAll(1e300);
	for(size_t i = 0; i < nodes; i++)
		m_pCosts->row(i)[i] = 0;
	m_pPaths = new size_t[nodes * nodes];
	memset(m_pPaths, 0xff, sizeof(size_t) * nodes * nodes);
}

GFloydWarshall::~GFloydWarshall()
{
	delete(m_pCosts);
	delete[] m_pPaths;
}

GMatrix* GFloydWarshall::releaseCostMatrix()
{
	m_pCosts = NULL;
	return m_pCosts;
}

void GFloydWarshall::addDirectedEdge(size_t from, size_t to, double cost)
{
	if(cost < m_pCosts->row(from)[to])
	{
		m_pCosts->row(from)[to] = cost;
		m_pPaths[from * m_nodes + to] = to;
	}
}

void GFloydWarshall::compute()
{
	// Compute the paths
	double t;
	for(size_t cand = 0; cand < m_nodes; cand++)
	{
		for(size_t i = 0; i < m_nodes; i++)
		{
			for(size_t j = 0; j < m_nodes; j++)
			{
				t = m_pCosts->row(i)[cand] + m_pCosts->row(cand)[j];
				if(t < m_pCosts->row(i)[j])
				{
					m_pCosts->row(i)[j] = t;
					m_pPaths[i * m_nodes + j] = m_pPaths[i * m_nodes + cand];
				}
			}
		}
	}
}

bool GFloydWarshall::isConnected()
{
	for(size_t i = 0; i < m_nodes; i++)
	{
		double* pRow = m_pCosts->row(i);
		for(size_t j = 0; j < m_nodes; j++)
		{
			if(*pRow == 1e300)
				return false;
			pRow++;
		}
	}
	return true;
}

double GFloydWarshall::cost(size_t from, size_t to)
{
	return m_pCosts->row(from)[to];
}

size_t GFloydWarshall::next(size_t from, size_t goal)
{
	return m_pPaths[from * m_nodes + goal];
}

#ifndef NO_TEST_CODE
#define NODE_COUNT 12
// static
void GFloydWarshall::test()
{
	GRand prng(0);
	for(size_t i = 0; i < 100; i++)
	{
		GFloydWarshall g1(NODE_COUNT);
		GDijkstra g2(NODE_COUNT);
		GBitTable edgesUsed(NODE_COUNT * NODE_COUNT);
		size_t edgeCount = (size_t)prng.next(NODE_COUNT * NODE_COUNT);
		for(size_t j = 0; j < edgeCount; j++)
		{
			size_t a = (size_t)prng.next(NODE_COUNT);
			size_t b = (size_t)prng.next(NODE_COUNT);
			if(a == b || edgesUsed.bit(a * NODE_COUNT + b))
				continue;
			edgesUsed.set(a * NODE_COUNT + b);
			double c = prng.uniform();
			g1.addDirectedEdge(a, b, c);
			g2.addDirectedEdge(a, b, c);
		}
		g1.compute();
		size_t origin = (size_t)prng.next(NODE_COUNT);
		g2.compute(origin);
		for(size_t j = 0; j < NODE_COUNT; j++)
		{
			double floyd = g1.cost(origin, j);
			double dijkstra = g2.cost(j);
			if(std::abs(floyd - dijkstra) > 1e-8)
				ThrowError("wrong");
		}
	}
}
#endif






GDijkstra::GDijkstra(size_t nodes)
{
	m_nodes = nodes;
	m_pNeighbors = new vector<size_t>[nodes];
	m_pEdgeCosts = new vector<double>[nodes];
	m_pCosts = new double[nodes];
	m_pPrevious = new size_t[nodes];
}

GDijkstra::~GDijkstra()
{
	delete[] m_pNeighbors;
	delete[] m_pEdgeCosts;
	delete[] m_pCosts;
	delete[] m_pPrevious;
}

void GDijkstra::addDirectedEdge(size_t from, size_t to, double cost)
{
	m_pNeighbors[from].push_back(to);
	m_pEdgeCosts[from].push_back(cost);
}

void GDijkstra::compute(size_t origin)
{
	for(size_t i = 0; i < m_nodes; i++)
	{
		m_pPrevious[i] = INVALID_INDEX;
		m_pCosts[i] = 1e300;
	}
	size_t* q = new size_t[2 * m_nodes];
	ArrayHolder<size_t> hQ(q);
	size_t* map = q + m_nodes;
	for(size_t i = 0; i < m_nodes; i++)
	{
		q[i] = i;
		map[i] = i + 1;
	}
	m_pCosts[origin] = 0;
	std::swap(q[0], q[origin]);
	std::swap(map[0], map[origin]);
	q--;
	size_t qSize = m_nodes;
	while(qSize > 0)
	{
		size_t u = q[1];
		if(m_pCosts[u] >= 1e300)
			break;
		
		// Pop from the front of the heap
		size_t index = 1;
		while(2 * index <= qSize)
		{
			if(2 * index == qSize || m_pCosts[q[2 * index]] < m_pCosts[q[2 * index + 1]])
			{
				map[q[2 * index]] = index;
				q[index] = q[2 * index];
				index = 2 * index;
			}
			else
			{
				map[q[2 * index + 1]] = index;
				q[index] = q[2 * index + 1];
				index = 2 * index + 1;
			}
		}
		if(qSize > index)
		{
			map[q[qSize]] = index;
			q[index] = q[qSize];
			while(index > 1 && m_pCosts[q[index / 2]] > m_pCosts[q[index]])
			{
				std::swap(map[q[index / 2]], map[q[index]]);
				std::swap(q[index / 2], q[index]);
				index /= 2;
			}
		}
		qSize--;

		// Test alternate routes
		vector<size_t>::iterator itNeigh = m_pNeighbors[u].begin();
		vector<double>::iterator itEdgeCost = m_pEdgeCosts[u].begin();
		while(itNeigh != m_pNeighbors[u].end())
		{
			size_t v = *itNeigh;
			double alt = m_pCosts[u] + *itEdgeCost;
			if(alt < m_pCosts[v])
			{
				m_pPrevious[v] = u;
				m_pCosts[v] = alt;
				while(map[v] > 1 && m_pCosts[q[map[v] / 2]] > alt)
				{
					size_t a = map[v];
					size_t b = a / 2;
					std::swap(map[q[a]], map[q[b]]);
					std::swap(q[a], q[b]);
				}
			}
			itNeigh++;
			itEdgeCost++;
		}
	}
}

double GDijkstra::cost(size_t target)
{
	return m_pCosts[target];
}

size_t GDijkstra::previous(size_t vertex)
{
	return m_pPrevious[vertex];
}

#ifndef NO_TEST_CODE
// static
void GDijkstra::test()
{
	GDijkstra g(6);
	g.addDirectedEdge(4, 1, 0.1);
	g.addDirectedEdge(1, 4, 0.1);
	g.addDirectedEdge(2, 1, 0.7);
	g.addDirectedEdge(3, 2, 2.2);
	g.addDirectedEdge(2, 3, 2.1);
	g.addDirectedEdge(2, 0, 4.4);
	g.addDirectedEdge(0, 2, 4.3);
	g.addDirectedEdge(4, 5, 3.3);
	g.addDirectedEdge(5, 4, 8.1);
	g.addDirectedEdge(5, 3, 0.8);
	g.addDirectedEdge(3, 5, 7.1);
	g.addDirectedEdge(5, 2, 0.9);
	g.addDirectedEdge(5, 1, 3.1);
	g.addDirectedEdge(1, 3, 6.6);
	g.addDirectedEdge(3, 4, 0.1);
	g.addDirectedEdge(4, 3, 1.1);
	g.addDirectedEdge(3, 0, 0.5);
	g.compute(5);
	if(g.previous(1) != 4) ThrowError("failed");
	if(g.previous(2) != 5) ThrowError("failed");
	if(g.previous(4) != 3) ThrowError("failed");
	if(g.previous(3) != 5) ThrowError("failed");
	if(g.previous(0) != 3) ThrowError("failed");
	if(g.previous(5) != INVALID_INDEX) ThrowError("failed");
	if(g.cost(4) != 0.9) ThrowError("failed");
	if(g.cost(1) != 1.0) ThrowError("failed");
	if(g.cost(0) != 1.3) ThrowError("failed");
	if(g.cost(5) != 0.0) ThrowError("failed");
	if(g.cost(2) != 0.9) ThrowError("failed");
	if(g.cost(3) != 0.8) ThrowError("failed");
}
#endif







GBrandesBetweennessCentrality::GBrandesBetweennessCentrality(size_t nodes)
: m_nodeCount(nodes), m_pVertexBetweenness(NULL), m_pEdgeBetweenness(NULL)
{
	m_pNeighbors = new std::vector<size_t>[m_nodeCount];
}

GBrandesBetweennessCentrality::~GBrandesBetweennessCentrality()
{
	delete[] m_pNeighbors;
	delete[] m_pVertexBetweenness;
	delete[] m_pEdgeBetweenness;
}

size_t GBrandesBetweennessCentrality::nodeCount()
{
	return m_nodeCount;
}

void GBrandesBetweennessCentrality::addDirectedEdge(size_t from, size_t to)
{
	m_pNeighbors[from].push_back(to);
}

void GBrandesBetweennessCentrality::addDirectedEdgeIfNotDupe(size_t from, size_t to)
{
	for(vector<size_t>::iterator it = m_pNeighbors[from].begin(); it != m_pNeighbors[from].end(); it++)
	{
		if(*it == to)
			return;
	}
	addDirectedEdge(from, to);
}

void GBrandesBetweennessCentrality::compute()
{
	// Initialize all the betweennesses to zero
	delete[] m_pVertexBetweenness;
	m_pVertexBetweenness = new double[m_nodeCount];
	GVec::setAll(m_pVertexBetweenness, 0.0, m_nodeCount);
	delete[] m_pEdgeBetweenness;
	m_pEdgeBetweenness = new vector<double>[m_nodeCount];
	for(size_t i = 0; i < m_nodeCount; i++)
	{
		for(size_t j = 0; j < m_pNeighbors[i].size(); j++)
			m_pEdgeBetweenness[i].push_back(0);
	}

	// Compute the betweennesses
	vector<size_t> stack;
	stack.reserve(m_nodeCount);
	for(size_t s = 0; s < m_nodeCount; s++)
	{
		vector<size_t>* lists = new vector<size_t>[m_nodeCount];
		ArrayHolder< vector<size_t> > hLists(lists);
		double* sigma = new double[2 * m_nodeCount];
		ArrayHolder<double> hSigma(sigma);
		double* d = sigma + m_nodeCount;
		GVec::setAll(sigma, 0.0, m_nodeCount);
		sigma[s] = 1.0;

		// Initialize distances
		GVec::setAll(d, -1.0, m_nodeCount);
		d[s] = 0.0;

		// Find the shortest paths from s to all other vertices. (If this
		// were a weighted graph, Dijkstra's algorithm would be more appropriate
		// here, but since it's unweighted, we'll just use breadth-first-search.)
		deque<size_t> q;
		q.push_back(s);
		while(q.size() > 0)
		{
			size_t v = q.front();
			q.pop_front();
			stack.push_back(v);
			size_t neighborIndex = 0;
			for(vector<size_t>::iterator it = m_pNeighbors[v].begin(); it != m_pNeighbors[v].end(); it++)
			{
				size_t w = *it;
				if(d[w] < 0)
				{
					q.push_back(w);
					d[w] = d[v] + 1;
				}
				if(d[w] == d[v] + 1)
				{
					sigma[w] += sigma[v];
					lists[w].push_back(v);
					lists[w].push_back(neighborIndex);
				}
				neighborIndex++;
			}
		}

		// Update the betweenness values
		GVec::setAll(d, 0.0, m_nodeCount);
		while(stack.size() > 0)
		{
			size_t w = stack.back();
			stack.pop_back();
			for(size_t i = 0; i < lists[w].size(); i += 2)
			{
				size_t v = lists[w][i];
				size_t neighborIndex = lists[w][i + 1];
				double f = (sigma[v] / sigma[w]) * (1.0 + d[w]);
				d[v] += f;
				m_pEdgeBetweenness[v][neighborIndex] += f;
			}
			if(w != s)
				m_pVertexBetweenness[w] += d[w];
		}
	}
}

double GBrandesBetweennessCentrality::vertexBetweenness(size_t vertex)
{
	return m_pVertexBetweenness[vertex];
}

size_t GBrandesBetweennessCentrality::neighborIndex(size_t from, size_t to)
{
	size_t index = 0;
	for(vector<size_t>::iterator it = m_pNeighbors[from].begin(); it != m_pNeighbors[from].end(); it++)
	{
		if(*it == to)
			return index;
		index++;
	}
	return INVALID_INDEX;
}

double GBrandesBetweennessCentrality::edgeBetweennessByNeighbor(size_t vertex, size_t neighborIndex)
{
	return m_pEdgeBetweenness[vertex][neighborIndex];
}

double GBrandesBetweennessCentrality::edgeBetweennessByVertex(size_t vertex1, size_t vertex2)
{
	size_t index = neighborIndex(vertex1, vertex2);
	if(index == INVALID_INDEX)
	{
		ThrowError("There is no edge between vertices ", to_str(vertex1), " and ", to_str(vertex2)); // todo: should we just return 0 here?
		return 0.0;
	}
	else
		return edgeBetweennessByNeighbor(vertex1, index);
}

#ifndef NO_TEST_CODE
// static
void GBrandesBetweennessCentrality::test()
{
	//   _        _
	//  0 \      / 5
	//  |  >2--3<  |
	//  1_/      \_4
	GBrandesBetweennessCentrality graph(6);
	graph.addDirectedEdge(0, 1); graph.addDirectedEdge(1, 0);
	graph.addDirectedEdge(1, 2); graph.addDirectedEdge(2, 1);
	graph.addDirectedEdge(0, 2); graph.addDirectedEdge(2, 0);
	graph.addDirectedEdge(2, 3); graph.addDirectedEdge(3, 2);
	graph.addDirectedEdge(3, 4); graph.addDirectedEdge(4, 3);
	graph.addDirectedEdge(4, 5); graph.addDirectedEdge(5, 4);
	graph.addDirectedEdge(3, 5); graph.addDirectedEdge(5, 3);
	graph.compute();
	if(std::abs(graph.edgeBetweennessByNeighbor(0, 0) - 1) > 1e-5)
		ThrowError("failed");
	if(std::abs(graph.edgeBetweennessByNeighbor(0, 1) - 4) > 1e-5)
		ThrowError("failed");
	if(std::abs(graph.edgeBetweennessByNeighbor(2, 0) - 4) > 1e-5)
		ThrowError("failed");
	if(std::abs(graph.edgeBetweennessByNeighbor(2, 1) - 4) > 1e-5)
		ThrowError("failed");
	if(std::abs(graph.edgeBetweennessByNeighbor(2, 2) - 9) > 1e-5)
		ThrowError("failed");
	if(std::abs(graph.edgeBetweennessByNeighbor(5, 0) - 1) > 1e-5)
		ThrowError("failed");
	if(std::abs(graph.edgeBetweennessByNeighbor(5, 1) - 4) > 1e-5)
		ThrowError("failed");
}
#endif





GAtomicCycleFinder::GAtomicCycleFinder(size_t nodes)
: m_nodeCount(nodes)
{
	m_pNeighbors = new vector<size_t>[m_nodeCount];
	m_pMirrors = new vector<size_t>[m_nodeCount];
}

// virtual
GAtomicCycleFinder::~GAtomicCycleFinder()
{
	delete[] m_pNeighbors;
	delete[] m_pMirrors;
}

size_t GAtomicCycleFinder::nodeCount()
{
	return m_nodeCount;
}

void GAtomicCycleFinder::addEdge(size_t a, size_t b)
{
	size_t aSize = m_pNeighbors[a].size();
	size_t bSize = m_pNeighbors[b].size();
	m_pNeighbors[a].push_back(b);
	m_pMirrors[a].push_back(bSize);
	m_pNeighbors[b].push_back(a);
	m_pMirrors[b].push_back(aSize);
}

void GAtomicCycleFinder::addEdgeIfNotDupe(size_t a, size_t b)
{
	if(m_pNeighbors[a].size() < m_pNeighbors[b].size())
	{
		for(vector<size_t>::iterator it = m_pNeighbors[a].begin(); it != m_pNeighbors[a].end(); it++)
		{
			if(*it == b)
				return;
		}
	}
	else
	{
		for(vector<size_t>::iterator it = m_pNeighbors[b].begin(); it != m_pNeighbors[b].end(); it++)
		{
			if(*it == a)
				return;
		}
	}
	addEdge(a, b);
}

void GAtomicCycleFinder::compute()
{
	size_t* pEdgeStarts = new size_t[m_nodeCount];
	ArrayHolder<size_t> hEdgeStarts(pEdgeStarts);
	size_t edgeCount = 0;
	for(size_t i = 0; i < m_nodeCount; i++)
	{
		pEdgeStarts[i] = edgeCount;
		edgeCount += m_pNeighbors[i].size();
	}

	// Outer breadth-first-search. (An atomic cycle is detected each
	// time this search discovers a node that was already discovered.)
	vector<size_t> cycle;
	GBitTable visited(m_nodeCount);
	GBitTable edges(edgeCount);
	deque<size_t> q;
	visited.set(0);
	q.push_back(0);
	while(q.size() > 0)
	{
		size_t a = q.front();
		q.pop_front();
		size_t index = 0;
		for(vector<size_t>::iterator it = m_pNeighbors[a].begin(); it != m_pNeighbors[a].end(); it++)
		{
			size_t b = *it;
			if(!edges.bit(pEdgeStarts[a] + index)) // (This edge may have been flagged when its mirror edge is crossed.)
			{
				if(visited.bit(b))
				{
					// Inner breadth-first-search. (We have detected one edge of an atomic cycle. Now,
					// find the whole cycle by searching for the shortest path from one end
					// to the other, while only crossing edges that the outer breadth-first-search
					// has crossed, not including the edge of this cycle that was just detected.)
					size_t* pPrevs = new size_t[m_nodeCount];
					ArrayHolder<size_t> hPrevs(pPrevs);
					pPrevs[b] = INVALID_INDEX;
					GBitTable visited2(m_nodeCount);
					deque<size_t> q2;
					visited2.set(b);
					q2.push_back(b);
					while(q2.size() > 0)
					{
						size_t a2 = q2.front();
						q2.pop_front();
						size_t index2 = 0;
						for(vector<size_t>::iterator it2 = m_pNeighbors[a2].begin(); it2 != m_pNeighbors[a2].end(); it2++)
						{
							if(edges.bit(pEdgeStarts[a2] + index2))
							{
								size_t b2 = *it2;
								if(!visited2.bit(b2))
								{
									visited2.set(b2);
									pPrevs[b2] = a2;
									if(b2 == a)
									{
										// Report the cycle
										cycle.clear();
										size_t i = b2;
										while(pPrevs[i] < m_nodeCount)
										{
											cycle.push_back(i);
											i = pPrevs[i];
										}
										GAssert(i == b);
										cycle.push_back(i);
										if(!onDetectAtomicCycle(cycle))
											return;
										q2.clear();
										break;
									}
									else
										q2.push_back(b2);
								}
							}
							index2++;
						}
					}
				}
				else
				{
					visited.set(b);
					q.push_back(b);
				}
				edges.set(pEdgeStarts[a] + index);
				edges.set(pEdgeStarts[b] + m_pMirrors[a][index]);
			}
			index++;
		}
	}
}

#ifndef NO_TEST_CODE
class GTestAtomicCycleFinder : public GAtomicCycleFinder
{
protected:
	bool m_three, m_four, m_five;

public:
	GTestAtomicCycleFinder(size_t nodes) : GAtomicCycleFinder(nodes), m_three(false), m_four(false), m_five(false)
	{
	}

	virtual ~GTestAtomicCycleFinder()
	{
	}

	virtual bool onDetectAtomicCycle(std::vector<size_t>& cycle)
	{
		if(cycle.size() == 3 && !m_three)
			m_three = true;
		else if(cycle.size() == 4 && !m_four)
			m_four = true;
		else if(cycle.size() == 5 && !m_five)
			m_five = true;
		else
			ThrowError("Unexpected cycle");
		return true;
	}

	void gotEmAll()
	{
		if(!m_three || !m_four || !m_five)
			ThrowError("missed a cycle");
	}
};

// static
void GAtomicCycleFinder::test()
{
	// 6-7-0-1
	// |/ /  |
	// 5-4-3-2
	GTestAtomicCycleFinder graph(8);
	graph.addEdge(0, 1);
	graph.addEdge(1, 2);
	graph.addEdge(2, 3);
	graph.addEdge(3, 4);
	graph.addEdge(4, 5);
	graph.addEdge(5, 6);
	graph.addEdge(6, 7);
	graph.addEdge(7, 0);
	graph.addEdge(0, 4);
	graph.addEdge(7, 5);
	graph.compute();
	graph.gotEmAll();
}
#endif
