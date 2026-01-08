# TODO: Scatter Plot Label Positioning

## Objective
Improve the automatic label positioning algorithm for scatter plots (Relative Abundance, Volcano Plot) to eliminate clipping, overlaps, and obviously suboptimal placements.

## Problem
Current label positioning frequently produces poor results:
- **Clipped labels** - Labels extend outside graph bounds and get cut off
- **Overlapping labels** - Multiple labels stack on each other in dense regions
- **Suboptimal placement** - Labels placed where a human would obviously choose a better position
- **Inconsistent behavior** - Same data can produce different layouts

Example: In the PeakImputationDia tutorial cover.png, the protein label "sp|Q8WUM4|PDC6I_HUMAN" is clipped at the bottom edge of the Relative Abundance plot.

## Current State
- A developer has been working on this for over a year
- Improvements have been made but edge cases persist
- Uses some form of automatic layout algorithm

## Affected Components
- `SummaryRelativeAbundanceGraphPane` - Relative Abundance plot
- `FoldChangeVolcanoPlot` - Volcano plot
- Possibly other scatter/dot plots with labels

## Constraints
Label placement must:
1. Stay fully inside graph bounds (no clipping)
2. Avoid overlapping other labels
3. Avoid obscuring important data points
4. Remain close enough to identify which point is labeled
5. Be readable (appropriate size, orientation)
6. Perform reasonably fast (not block UI)

## Potential Approaches

### Force-Directed Layout
Labels and bounds exert repulsive forces; labels settle into equilibrium positions.
- Pros: Handles complex cases, natural-looking results
- Cons: Can be slow, may not converge

### Simulated Annealing
Random perturbations with gradual "cooling" to find good global placement.
- Pros: Escapes local minima, finds good solutions
- Cons: Non-deterministic, tuning required

### Greedy with Fallbacks
Try preferred positions in order (right, above, left, below), use first that fits.
- Pros: Fast, predictable
- Cons: Can fail in dense regions

### Leader Lines
Allow labels to be placed farther from points with connecting lines.
- Pros: More placement freedom, cleaner dense regions
- Cons: Visual complexity, line crossings

### Priority/Culling
Only label most important points; hide or defer others.
- Pros: Reduces problem complexity
- Cons: User may want to see all labels

### Hybrid Approach
Combine techniques: greedy for simple cases, force-directed for conflicts, culling for extreme density.

## Investigation Needed
1. Review current algorithm implementation
2. Identify specific failure modes
3. Collect test cases (screenshots of bad placements)
4. Benchmark current performance
5. Evaluate alternative algorithms

## Files to Investigate
- Label positioning code (likely in `DotPlotUtil` or similar)
- `SummaryRelativeAbundanceGraphPane.cs` - uses `AdjustLabelSpacings`
- `FoldChangeVolcanoPlot` - may share or have separate logic
- ZedGraph label/text object handling

## Success Criteria
- No labels clipped by graph bounds
- No overlapping labels (or graceful degradation with many labels)
- Placement that a human reviewer would consider "reasonable"
- Performance acceptable for documents with thousands of points

## Related
- Design review TODO (overall UI consistency)
- Relative Abundance performance TODO (large document handling)
