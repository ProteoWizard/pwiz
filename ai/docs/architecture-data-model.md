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

**Critical constraint**: No parent pointers in the tree. Parents know their children, but children don't know their parents. This enables the immutable modification pattern.

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

## Identity and GlobalIndex

Every `DocNode` contains an `Identity` object representing its reference identity:

```csharp
public abstract class DocNode
{
    public Identity Id { get; }
    // ...
}
```

### GlobalIndex
- Unique `int` per document (in-memory only, never persisted to disk)
- Created before `RuntimeHelpers.GetHashCode()` was known
- Can be used as Dictionary key for ReferenceEquals-based lookups:

```csharp
// Dictionary keyed by identity
var indexByIdentity = new Dictionary<int, int>();
foreach (var protein in doc.MoleculeGroups)
    indexByIdentity[protein.Id.GlobalIndex] = index++;
```

### Identity Subclasses
Each DocNode level has its own Identity subclass with content-based equality:
- `PeptideGroup.Id` - protein/list identity
- `Peptide.Id` - peptide/molecule identity
- `TransitionGroup.Id` - precursor identity
- `Transition.Id` - transition identity

## Power of ReferenceEquals for Change Detection

The immutable pattern makes reference equality extremely powerful:

```csharp
// If same reference, entire subtree is unchanged
if (ReferenceEquals(docNew, docOld))
    return; // Nothing changed anywhere

// Check specific proteins
foreach (var (protNew, protOld) in docNew.MoleculeGroups.Zip(docOld.MoleculeGroups))
{
    if (ReferenceEquals(protNew, protOld))
        continue; // This protein and ALL its children unchanged

    // Only process changed proteins
    ProcessChangedProtein(protNew, protOld);
}
```

### Real-World Example: SrmTreeNode.UpdateNodes()

This method updates a .NET TreeView when the document changes. Instead of tearing down and rebuilding:

1. Walk old and new document trees in parallel
2. Use `ReferenceEquals()` to skip unchanged subtrees
3. Only update TreeView nodes where actual changes occurred

**Result**: If user changes one peak integration, only the affected protein's tree branch updates. The TreeView doesn't flicker or lose selection state.

## Application to Graph Updates

The same pattern applies to expensive graph computations:

### Current Approach (Full Recalculation)
```csharp
// Every document change triggers full recalculation
void OnDocumentChanged(SrmDocument docNew, SrmDocument docOld)
{
    var data = CalculateAllPoints(docNew); // O(n log n) sort
    UpdateGraph(data);
}
```

### Optimized Approach (Incremental Update)
```csharp
void OnDocumentChanged(SrmDocument docNew, SrmDocument docOld)
{
    if (ReferenceEquals(docNew, docOld))
        return;

    // Find only changed proteins using ReferenceEquals
    var changedProteins = FindChangedProteins(docNew, docOld);

    // Update only affected points - O(k log n) where k << n
    UpdateChangedPoints(changedProteins);
}
```

## Reference-Based Set Operations

Using `RuntimeHelpers.GetHashCode(obj)` (or `Identity.GlobalIndex`), you can build dictionaries keyed by object reference. This enables O(1) membership tests across different orderings of the same data.

```csharp
// Build reference set from one document
var nodeSet = new HashSet<int>();
foreach (var protein in doc.MoleculeGroups)
    nodeSet.Add(RuntimeHelpers.GetHashCode(protein));

// O(1) lookup: is this exact node reference in the set?
if (nodeSet.Contains(RuntimeHelpers.GetHashCode(someProtein)))
    // Same object reference exists
```

This pattern enables efficient incremental updates when documents change - you can quickly identify which nodes are unchanged (same reference), modified (different reference, same identity), or added/removed.

## See Also

- `pwiz_tools/Skyline/Model/DocNode.cs` - Base DocNode class
- `pwiz_tools/Skyline/Model/SrmDocument.cs` - Document root
- `pwiz_tools/Skyline/Skyline.cs` - SkylineWindow.ModifyDocumentInner()
- `pwiz_tools/Skyline/Controls/TreeView/SrmTreeNode.cs` - UpdateNodes() example
