# Skyline Data Model Architecture

This document describes the core data model architecture of Skyline, focusing on the immutable `SrmDocument` and patterns for efficient change detection.

## SrmDocument Overview

`SrmDocument` is the root object of the primary data model that all Skyline UI runs on. Key characteristics:

- **Instant switching**: The UI is designed to switch to any SrmDocument instantly
- **No UI code**: SrmDocument is pure data model - usable for:
  1. Skyline UI (WinForms)
  2. Command-line interface (SkylineCmd)
  3. Unit testing without UI
- **Immutable**: Once created, an SrmDocument never changes. You can traverse it from any thread without locking concerns.

## Document Tree Structure

SrmDocument is a `DocNodeParent` with a hierarchical tree structure:

```
SrmDocument (root)
└── PeptideGroupDocNode (proteins, peptide lists, molecule lists)
    └── PeptideDocNode (peptides, small molecules)
        └── TransitionGroupDocNode (precursors)
            └── TransitionDocNode (transitions - leaf nodes)
```

**Critical constraint**: No parent pointers in DocNode objects. Parents know their children, but children don't know their parents. This enables the immutable modification pattern (see "Why DocNode Cannot Have Parent Pointers" below).

Views like .NET TreeView can mirror this structure with their own parent pointers - that's fine for UI elements.

## Immutable Modification Pattern

Any modification to the document tree:
1. Clones nodes from the **bottom of the change up to the root**
2. Creates a new SrmDocument
3. Leaves the original document unchanged

Example: Changing a single transition's integration:
```
Original:                    After modification:
SrmDocument₁                 SrmDocument₂ (new)
└── Protein₁                 └── Protein₁' (cloned)
    └── Peptide₁                 └── Peptide₁' (cloned)
        └── Precursor₁               └── Precursor₁' (cloned)
            └── Transition₁              └── Transition₁' (modified, new)
            └── Transition₂              └── Transition₂ (same reference!)
    └── Peptide₂                 └── Peptide₂ (same reference!)
└── Protein₂                 └── Protein₂ (same reference!)
```

Only the path from the modified node to the root is cloned. All unchanged subtrees keep the same object references.

### The Change... Pattern

DocNodes are designed to be constructed through their constructor or XML deserialization, then released for use as immutable objects. **No setters are allowed.**

To create a modified copy, DocNodes use the `Change...` pattern:

```csharp
public TypedImmutable ChangeQuantitative(T prop)
{
    return ChangeProp(ImClone(this), im => im.PropertyName = prop);
}
```

`ChangeProp` and `ImClone` are convenience methods on the `Immutable` base class that make this operation a single-line function - as DRY as possible for adding change methods to new Immutable objects.

## SkylineWindow Document Management

### Key Properties
- `Document` - The current authoritative document
- `DocumentUI` - The document currently active on the UI thread

### ModifyDocument Pattern

`SkylineWindow.ModifyDocument()` takes a `Func<SrmDocument, SrmDocument>` that applies modifications:

```csharp
// Conceptual usage
SkylineWindow.ModifyDocument("Change peak integration",
    doc => doc.ChangePeakIntegration(...));
```

### Eventual Consistency

The core consistency mechanism is in `ModifyDocumentInner()` (Skyline.cs):

```csharp
do
{
    var docOriginal = Document;
    var docNew = ApplyModifications(docOriginal);
    // Attempt atomic swap
} while (!Interlocked.CompareExchange(ref _document, docNew, docOriginal));
```

If another thread modified the document while we were working, the compare-exchange fails and we retry with the new current document.

**Benefits**: Users can continue editing during long operations. For example, during a multi-file import, each completed file is applied to the document, but the user can still undo/redo and make edits.

### Document Locking (Rare)

For transformations requiring user feedback, document locking exists but should be avoided when possible. Most operations work outside the do...while loop, then apply changes quickly.

## Undo-Redo System

Two stacks of `SrmDocument` references:
- Undo stack: Previous document states
- Redo stack: Future document states (after undo)

**Key insight**: No "operation playback" - just document snapshots. The audit log separately records what operations caused each state, but undo/redo simply swaps the current document reference.

## BackgroundLoader Pattern

Classes subclassing `BackgroundLoader` register as document change listeners. Pattern:

1. Quick operation places an "IOU" on the document via `ModifyDocument()`
2. Document is now "incomplete" - needs background work
3. BackgroundLoader wakes up, sees work needed, processes in background
4. Completed work applied via `SetDocument()`
5. Loader waits for next document change event

Search for `BackgroundLoader` subclasses to see all implementations.

## Content Equality vs Reference Equality

A fundamental concept in Skyline's immutable architecture:

- **Content equality** (`Object.Equals()` overrides): Two objects represent the same logical entity (same peptide sequence, same protein name, etc.)
- **Reference equality** (`ReferenceEquals()`, `object.ReferenceEquals()`): Two variables point to the exact same object instance in memory

In an immutable system, reference equality is extremely powerful because unchanged subtrees keep their original object references. If `ReferenceEquals(nodeNew, nodeOld)` returns true, you know the entire subtree is unchanged without examining any content.

## Identity vs DocNode: A Critical Distinction

Every `DocNode` contains an `Identity` object:

```csharp
public abstract class DocNode
{
    public Identity Id { get; }
    // ...
}
```

**Identity and DocNode serve different purposes:**

| Aspect | Identity | DocNode |
|--------|----------|---------|
| **Purpose** | Identifies *which* entity (like a database row ID) | Contains the entity's *current state* |
| **Mutability** | Truly immutable - no setters, no Change... methods | Immutable instance, but new instances created via Change... |
| **GlobalIndex** | Assigned once, never changes for this identity | N/A - uses `Id.GlobalIndex` |
| **Parent pointers** | Allowed (safe because content never changes) | NOT allowed (would break immutable pattern) |

### Identity Objects Are Truly Immutable

Identity objects have **no setters and no ChangeProp methods**. The moment you feel the need for a `Change...` method, the value is not truly identifying and belongs on the DocNode instead.

**Example**: `Transition.Id` (transition identity) was originally designed to include m/z values. But when support for switching between monoisotopic and average mass calculations was needed, m/z had to move to `TransitionDocNode`. Values that remained in the Identity:
- `IonType` - what kind of ion (y, b, precursor, etc.)
- `CleavageOffset` - position in peptide
- `Adduct` - charge and adduct type
- Parent `TransitionGroup` identity reference (without which `CleavageOffset` is meaningless)

These values truly identify *which* transition - changing any of them means you have a fundamentally different transition.

### Why DocNode Cannot Have Parent Pointers

If DocNode had parent pointers, changing a child's *content* (e.g., adding an annotation) would:
1. Clone the child with new content
2. Clone the parent to reference the new child
3. **Force ALL siblings to clone** just to update their parent pointer

Cost explodes from O(tree depth) to O(depth × sibling breadth).

### Why Identity CAN Have Parent Pointers

Identity objects form a **stable scaffolding** that DocNodes "change" around:

- A `Peptide.Identity` can safely reference its parent `Protein.Identity`
- It can include: peptide sequence, position in protein, parent protein reference
- Changing ANY of these creates a fundamentally *new* peptide (new GlobalIndex), not a modification
- Identity objects never change content - they either exist unchanged or are replaced entirely

This is why Identity provides `GlobalIndex` - it uniquely identifies an identity instance that will never change.

### GlobalIndex - For Identity Matching

`Identity.GlobalIndex` provides a unique identifier for each Identity instance:

- Assigned via `Interlocked.Increment()` - **guaranteed unique** per Identity instance
- 32-bit integer with billions of headroom (documents rarely exceed millions of nodes)
- In-memory only, never persisted to disk
- Use for dictionary keys when you need to match nodes by identity across documents

```csharp
// Build a map of nodes by their identity
var nodesByIdentity = new Dictionary<int, PeptideGroupDocNode>();
foreach (var protein in doc.MoleculeGroups)
    nodesByIdentity[protein.Id.GlobalIndex] = protein;
```

**IMPORTANT**: Matching by GlobalIndex tells you the nodes have the same *identity* - it does NOT tell you they are the same object reference. Two different DocNode objects can share the same Identity if one is a modified clone of the other.

### WARNING: Do NOT Use RuntimeHelpers.GetHashCode()

`RuntimeHelpers.GetHashCode()` returns a 32-bit identity hash code, but it is **NOT guaranteed unique**:

- On 64-bit Windows, object addresses don't fit in 32 bits, so collisions are inevitable
- With thousands of objects, collisions become common (observed with ~5,000 proteins in PR #3730)
- Using it as a dictionary key causes intermittent "duplicate key" exceptions

```csharp
// WRONG: RuntimeHelpers.GetHashCode() can have collisions!
var nodeSet = new HashSet<int>();
foreach (var protein in doc.MoleculeGroups)
    nodeSet.Add(RuntimeHelpers.GetHashCode(protein)); // COLLISIONS POSSIBLE!
```

### Identity Subclasses

Each DocNode level has its own Identity subclass with **content-based equality** (for `Equals()`/`GetHashCode()`):
- `PeptideGroup.Id` - protein/list identity
- `Peptide.Id` - peptide/molecule identity
- `TransitionGroup.Id` - precursor identity
- `Transition.Id` - transition identity

### Identity Beyond DocNode

The `Identity` pattern is not limited to `DocNode`. Other immutable objects that need stable identification use the same approach via `XmlNamedIdElement`:

```csharp
public abstract class XmlNamedIdElement : XmlNamedElement, IIdentiyContainer
{
    public Identity Id { get; private set; }
}
```

**Key examples**:
- `ChromatogramSet` - identifies a replicate (extends `XmlNamedIdElement`)
- `ChromatogramSetId` - the Identity subclass for ChromatogramSet

This means `ChromatogramSet` also has `Id.GlobalIndex` for efficient identity-based lookups:

```csharp
// Build map of ChromatogramSets by identity for O(1) lookup
var chromById = measuredResults.Chromatograms.ToDictionary(c => c.Id.GlobalIndex);

// Two-phase check: find by identity, then verify unchanged
if (chromById.TryGetValue(cachedChromSet.Id.GlobalIndex, out var currentChromSet) &&
    ReferenceEquals(currentChromSet, cachedChromSet))
{
    // ChromatogramSet is unchanged
}
```

The same two-phase change detection pattern applies to any object with an `Identity Id` property.

## Two-Phase Change Detection

To detect which nodes changed between two documents, you need **both** identity matching AND reference equality:

```csharp
// Phase 1: Build map of new document's nodes by Identity
var newNodesByIdentity = new Dictionary<int, DocNode>();
foreach (var node in newDoc.MoleculeGroups)
    newNodesByIdentity[node.Id.GlobalIndex] = node;

// Phase 2: Check each prior node
foreach (var priorNode in priorDoc.MoleculeGroups)
{
    var identityKey = priorNode.Id.GlobalIndex;

    if (newNodesByIdentity.TryGetValue(identityKey, out var newNode))
    {
        // Identity exists in both documents
        if (ReferenceEquals(newNode, priorNode))
        {
            // UNCHANGED - exact same object, entire subtree unchanged
        }
        else
        {
            // CHANGED - same identity, but content differs
        }
    }
    else
    {
        // REMOVED - identity no longer exists in new document
    }
}

// Nodes in newDoc not in priorDoc are ADDED
```

This two-phase pattern is used throughout Skyline for efficient incremental updates.

### Real-World Example: SrmTreeNode.UpdateNodes()

This method (written in 2009) updates a .NET TreeView when the document changes. Instead of tearing down and rebuilding:

1. Walk old and new document trees in parallel
2. Match nodes by Identity (GlobalIndex)
3. Use `ReferenceEquals()` on matched DocNodes to skip unchanged subtrees
4. Only update TreeView nodes where actual changes occurred

**Result**: If user changes one peak integration, only the affected protein's tree branch updates. The TreeView doesn't flicker or lose selection state.

## Application to Graph Updates

The same two-phase pattern applies to expensive graph computations:

### Full Recalculation (Simple but Slow)
```csharp
// Every document change triggers full recalculation
void OnDocumentChanged(SrmDocument docNew, SrmDocument docOld)
{
    var data = CalculateAllPoints(docNew); // O(n log n) sort
    UpdateGraph(data);
}
```

### Incremental Update (Efficient)
```csharp
void OnDocumentChanged(SrmDocument docNew, SrmDocument docOld)
{
    if (ReferenceEquals(docNew, docOld))
        return;

    // Build identity map for new document
    var newNodes = BuildIdentityMap(docNew);

    // Find changed nodes using two-phase check
    var unchanged = new List<NodeData>();
    var changed = new List<DocNode>();

    foreach (var prior in priorData)
    {
        if (newNodes.TryGetValue(prior.IdentityKey, out var newNode) &&
            ReferenceEquals(newNode, prior.DocNode))
        {
            unchanged.Add(prior);  // Keep cached calculation
        }
        else
        {
            changed.Add(newNode);  // Needs recalculation
        }
    }

    // Only recalculate changed nodes - O(k log k) where k << n
    MergeResults(unchanged, CalculatePoints(changed));
}
```

## See Also

- `pwiz_tools/Skyline/Model/DocNode.cs` - Base DocNode class
- `pwiz_tools/Skyline/Model/Immutable.cs` - ChangeProp and ImClone implementation
- `pwiz_tools/Skyline/Model/SrmDocument.cs` - Document root
- `pwiz_tools/Skyline/Model/DocSettings/XmlNamedElement.cs` - XmlNamedIdElement base class
- `pwiz_tools/Skyline/Model/Results/Chromatogram.cs` - ChromatogramSet and ChromatogramSetId
- `pwiz_tools/Skyline/Skyline.cs` - SkylineWindow.ModifyDocumentInner()
- `pwiz_tools/Skyline/Controls/TreeView/SrmTreeNode.cs` - UpdateNodes() example
