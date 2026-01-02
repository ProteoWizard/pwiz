# TODO-screenshot_consistency_followup.md

## Overview
- **Category**: Enhancement
- **Priority**: Low
- **Origin**: Deferred from TODO-20251218_screenshot_consistency_improvements.md
- **Created**: 2026-01-01

## Description

Follow-up items from the screenshot consistency sprint that were not critical for achieving consistent ACG screenshots but would improve the overall screenshot testing infrastructure.

## Tasks

### 1. X-Axis Label Orientation Inconsistency
**Affected screenshots**: MS1Filtering s-21 (ja, zh-CHS only)

X-axis labels on graphs can flip between horizontal and vertical orientation depending on available space. This causes screenshot diffs even when no functional change occurred.

**Proposed solutions**:
- Make graphs slightly wider to ensure labels consistently remain horizontal
- Implement deterministic label orientation during screenshot capture mode
- Force a specific orientation based on locale/graph type

### 2. ImageComparer Diff Amplification Features
**Affected screenshots**: DDASearch s-16 (1px diff), ExistingQuant s-07 (1px diff), IMSFiltering s-14/s-17

When ImageComparer shows only 1-2 pixel differences, it's nearly impossible to visually locate the changed pixels.

**Proposed enhancements**:
1. Add a "diff-only" view mode that removes the image and paints only the diff pixels on a white background
2. Add an "Amplification" slider that expands the visual area around each changed pixel (radius 1-10)
3. This would help identify exact pixel locations for debugging

### 3. Fix CleanupBorder Algorithm
The `ScreenshotProcessingExtensions.CleanupBorder` algorithm that draws borders around screenshots may have an off-by-one error for certain window sizes. In Windows 11, curved corners are translucent and must be reproduced consistently.

### 4. Remove Unused `timeout` Parameter
Remove the unused `timeout` parameter from all `PauseFor*ScreenShot` functions for API cleanliness.

### 5. Disable Graph Animation When ACG is Frozen
Consider fully stopping the timer in AsyncChromatogramsGraph2 when frozen for screenshots, rather than just freezing the values. This could reduce test flakiness.

## Notes

These items are not blocking screenshot consistency for the main ACG use cases. The primary sprint (TODO-20251218) successfully achieved:
- Frozen progress display (file bars, total bar, elapsed time)
- Y-axis locking with graphIntensityMax parameter
- X-axis freezing for progressive (DIA) data
- Proper handling of SRM vs progressive data modes
- Background thread blocking during screenshot capture
