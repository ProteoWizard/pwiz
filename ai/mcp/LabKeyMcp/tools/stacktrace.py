"""Stack trace normalization utilities.

This module provides internal utilities for normalizing C# stack traces
to enable pattern matching across variations. NOT exposed as MCP tools -
used internally by other modules (patterns.py, exceptions.py).

The same underlying bug can produce different stack traces due to:
- Line number changes between versions
- Absolute paths varying by machine (C:\\proj\\skyline_25_1 vs D:\\Nightly\\...)
- Async state machine frames (MoveNext, d__0)
- Lambda/closure wrappers (<>c__, b__0)
- Different entry points reaching same buggy code

Normalization produces a "fingerprint" that matches the same bug
across these variations.

Path normalization uses `pwiz_tools` as anchor - all project code lives there:
- `C:\\proj\\skyline_25_1\\pwiz_tools\\Skyline\\Model\\Foo.cs:line 123`
- `D:\\Nightly\\trunk\\pwiz\\pwiz_tools\\Skyline\\Model\\Foo.cs:line 456`
Both normalize to: `pwiz_tools/Skyline/Model/Foo.cs`
"""

import hashlib
import re
from dataclasses import dataclass
from typing import Optional


@dataclass
class NormalizedTrace:
    """Result of stack trace normalization."""
    fingerprint: str  # SHA256 hash for fast matching
    signature_frames: list[str]  # Top N meaningful frames (method names only)
    normalized: str  # Full normalized trace text
    frame_count: int  # Number of frames after filtering


# Patterns for C# stack trace parsing
# Example: "   at pwiz.Skyline.Model.Foo.DoSomething() in C:\proj\pwiz\File.cs:line 123"
# Note: Windows paths have drive letters (C:) so we can't just use [^:] for file path
FRAME_PATTERN = re.compile(
    r'^\s*at\s+'  # "   at "
    r'(?P<method>[^\(]+)'  # Method name (everything before the parenthesis)
    r'\([^)]*\)'  # Parameters in parentheses
    r'(?:\s+in\s+(?P<file>.+?):line\s+(?P<line>\d+))?',  # Optional file:line (non-greedy match to :line)
    re.MULTILINE
)

# Pattern to split stack trace at "Exception caught at:" boundary
# Everything after this is framework re-throw noise
EXCEPTION_CAUGHT_PATTERN = re.compile(r'\nException caught at:', re.IGNORECASE)

# Project path anchor - all project code lives under pwiz_tools
PROJECT_PATH_ANCHOR = 'pwiz_tools'

# Async state machine patterns to filter out
ASYNC_NOISE_PATTERNS = [
    r'\.MoveNext\(\)',  # Async state machine MoveNext
    r'\.<\w+>d__\d+',  # Async state machine class (e.g., <ProcessAsync>d__5)
    r'System\.Runtime\.CompilerServices\.AsyncMethodBuilder',
    r'System\.Runtime\.CompilerServices\.AsyncTaskMethodBuilder',
    r'System\.Runtime\.CompilerServices\.AsyncVoidMethodBuilder',
    r'System\.Threading\.Tasks\.Task\.Execute',
    r'System\.Threading\.ExecutionContext\.Run',
    r'System\.Threading\.ThreadHelper\.ThreadStart',
]

# Lambda/closure patterns to normalize
LAMBDA_PATTERN = re.compile(r'<(\w+)>b__\d+')  # <Method>b__0 -> Method
CLOSURE_CLASS_PATTERN = re.compile(r'\.<>c__DisplayClass\d+_\d+\.')  # <>c__DisplayClass -> .
ANONYMOUS_TYPE_PATTERN = re.compile(r'<>f__AnonymousType\d+')

# Framework frames to filter (low signal, high noise)
FRAMEWORK_PREFIXES = [
    # .NET runtime
    'System.Runtime.',
    'System.Threading.',
    # WinForms internals
    'System.Windows.Forms.Control.OnClick',
    'System.Windows.Forms.Control.Invoke',
    'System.Windows.Forms.Control.MarshaledInvoke',
    'System.Windows.Forms.Control.WmMouseUp',
    'System.Windows.Forms.Control.WndProc',
    'System.Windows.Forms.Button.OnMouseUp',
    'System.Windows.Forms.Button.WndProc',
    'System.Windows.Forms.ButtonBase.WndProc',
    'System.Windows.Forms.ToolStrip',
    'System.Windows.Forms.NativeWindow.Callback',
    'System.Windows.Forms.UnsafeNativeMethods',
    'System.Windows.Forms.Application.',
    # WPF
    'System.Windows.Threading.Dispatcher',
    'MS.Internal.',
    # Test framework
    'Microsoft.VisualStudio.TestTools.UnitTesting.Assert.',
]


def _normalize_file_path(file_path: Optional[str]) -> Optional[str]:
    """Normalize a file path to project-relative form.

    Strips absolute path prefix up to and including the project anchor (pwiz_tools).
    Converts backslashes to forward slashes for consistency.

    Examples:
        "C:\\proj\\skyline_25_1\\pwiz_tools\\Skyline\\Model\\Foo.cs"
        -> "pwiz_tools/Skyline/Model/Foo.cs"

        "D:\\Nightly\\trunk\\pwiz\\pwiz_tools\\Skyline\\Test\\Bar.cs"
        -> "pwiz_tools/Skyline/Test/Bar.cs"

        "C:\\Windows\\System32\\mscorlib.dll"
        -> None (no pwiz_tools anchor)
    """
    if not file_path:
        return None

    # Normalize slashes
    normalized = file_path.replace('\\', '/')

    # Find the project anchor
    anchor_pos = normalized.lower().find(PROJECT_PATH_ANCHOR.lower())
    if anchor_pos == -1:
        return None  # Not project code

    # Return from anchor onwards
    return normalized[anchor_pos:]


def _is_noise_frame(method: str) -> bool:
    """Check if a frame is async/threading noise that should be filtered."""
    for pattern in ASYNC_NOISE_PATTERNS:
        if re.search(pattern, method):
            return True
    return False


def _is_framework_frame(method: str) -> bool:
    """Check if a frame is low-value framework plumbing."""
    for prefix in FRAMEWORK_PREFIXES:
        if method.startswith(prefix):
            return True
    return False


def _normalize_method_name(method: str) -> str:
    """Normalize a method name by collapsing lambdas and closures.

    Examples:
        "Foo.<Bar>b__0" -> "Foo.Bar"
        "Foo.<>c__DisplayClass5_0.<Bar>b__1" -> "Foo.Bar"
        "Foo.<ProcessAsync>d__5.MoveNext" -> "Foo.ProcessAsync"
    """
    # Remove closure class wrappers: <>c__DisplayClass5_0. -> nothing
    normalized = CLOSURE_CLASS_PATTERN.sub('.', method)

    # Collapse lambdas: <Method>b__0 -> Method
    normalized = LAMBDA_PATTERN.sub(r'\1', normalized)

    # Handle async state machine: <Method>d__5.MoveNext -> Method
    # But we filter these out anyway, so this is just for completeness
    normalized = re.sub(r'<(\w+)>d__\d+\.MoveNext', r'\1', normalized)

    # Clean up any double dots from removals
    normalized = re.sub(r'\.\.+', '.', normalized)

    # Remove leading/trailing dots
    normalized = normalized.strip('.')

    return normalized


def _extract_class_method(full_method: str) -> str:
    """Extract just the Class.Method from a fully qualified name.

    Example:
        "pwiz.Skyline.Model.DocSettings.ChangeSettings" -> "DocSettings.ChangeSettings"
    """
    parts = full_method.split('.')
    if len(parts) >= 2:
        return '.'.join(parts[-2:])
    return full_method


def normalize_stack_trace(
    raw_trace: str,
    max_signature_frames: int = 5,
    include_framework: bool = False,
) -> NormalizedTrace:
    """Normalize a C# stack trace for pattern matching.

    Args:
        raw_trace: Raw stack trace text (C# format)
        max_signature_frames: Number of top frames for signature (default: 5)
        include_framework: If True, include framework frames (default: False)

    Returns:
        NormalizedTrace with fingerprint, signature frames, and normalized text.

    Example input:
        at pwiz.Skyline.Model.Foo.DoSomething() in C:\\proj\\skyline_25_1\\pwiz_tools\\Skyline\\Model\\Foo.cs:line 123
        at pwiz.Skyline.Model.Bar.<ProcessAsync>d__5.MoveNext() in C:\\proj\\pwiz\\Bar.cs:line 456
        at System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start[TStateMachine]
        Exception caught at:
        at System.Windows.Forms.Application.ThreadContext.OnThreadException(Exception t)

    Example output:
        NormalizedTrace(
            fingerprint='a1b2c3...',
            signature_frames=['Foo.DoSomething', 'Bar.ProcessAsync'],
            normalized='pwiz.Skyline.Model.Foo.DoSomething [pwiz_tools/Skyline/Model/Foo.cs]\\n...',
            frame_count=2
        )
    """
    if not raw_trace or not raw_trace.strip():
        return NormalizedTrace(
            fingerprint=hashlib.sha256(b'').hexdigest()[:16],
            signature_frames=[],
            normalized='',
            frame_count=0
        )

    # Strip "Exception caught at:" section - it's framework re-throw noise
    trace_to_parse = EXCEPTION_CAUGHT_PATTERN.split(raw_trace)[0]

    normalized_frames = []
    signature_frames = []

    # Parse each frame
    for match in FRAME_PATTERN.finditer(trace_to_parse):
        method = match.group('method').strip()
        file_path = match.group('file')

        # Skip async/threading noise
        if _is_noise_frame(method):
            continue

        # Optionally skip framework frames
        if not include_framework and _is_framework_frame(method):
            continue

        # Normalize the method name (collapse lambdas, etc.)
        normalized_method = _normalize_method_name(method)

        if normalized_method:
            # Normalize file path if present (strip absolute path prefix)
            normalized_file = _normalize_file_path(file_path)

            # Build normalized frame with optional file reference
            if normalized_file:
                frame_text = f"{normalized_method} [{normalized_file}]"
            else:
                frame_text = normalized_method

            normalized_frames.append(frame_text)

            # Build signature from top N frames (short form, method only)
            if len(signature_frames) < max_signature_frames:
                short_name = _extract_class_method(normalized_method)
                signature_frames.append(short_name)

    # Build normalized text (one frame per line)
    normalized_text = '\n'.join(normalized_frames)

    # Generate fingerprint from normalized frames
    # Use signature frames for hash (top N most meaningful, method names only)
    fingerprint_input = '|'.join(signature_frames).encode('utf-8')
    fingerprint = hashlib.sha256(fingerprint_input).hexdigest()[:16]

    return NormalizedTrace(
        fingerprint=fingerprint,
        signature_frames=signature_frames,
        normalized=normalized_text,
        frame_count=len(normalized_frames)
    )


def fingerprint_matches(trace1: str, trace2: str) -> bool:
    """Check if two stack traces have the same fingerprint.

    Convenience function for quick comparison.
    """
    norm1 = normalize_stack_trace(trace1)
    norm2 = normalize_stack_trace(trace2)
    return norm1.fingerprint == norm2.fingerprint


def group_by_fingerprint(traces: list[str]) -> dict[str, list[int]]:
    """Group a list of stack traces by their fingerprint.

    Args:
        traces: List of raw stack trace strings

    Returns:
        Dict mapping fingerprint -> list of indices in the input list

    Example:
        traces = [trace_a, trace_b, trace_c]  # where a and c are "same bug"
        result = {'abc123': [0, 2], 'def456': [1]}
    """
    groups: dict[str, list[int]] = {}

    for i, trace in enumerate(traces):
        norm = normalize_stack_trace(trace)
        if norm.fingerprint not in groups:
            groups[norm.fingerprint] = []
        groups[norm.fingerprint].append(i)

    return groups
