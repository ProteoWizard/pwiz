"""Walk pwiz Jamfile.jam files and emit a Mermaid graph of inter-module deps,
colored by pwiz-sharp port status.

Each Jamfile is treated as one node, identified by its directory (relative to
pwiz/). `<library>...//...` references inside the same project tree become
edges. External libs (/ext/boost, /ext/zlib, /ext/hdf5, etc.) are dropped.
"""

from __future__ import annotations
import os
import re
import sys
from pathlib import Path

PWIZ_ROOT = Path("C:/dev/pwiz-msconvert-pr/pwiz")
PWIZ_TOOLS_ROOT = Path("C:/dev/pwiz-msconvert-pr/pwiz_tools")

# Hand-coded deps for pwiz_tools/* targets that are C# / .NET projects whose
# Jamfiles don't list explicit `<library>` refs (they wire through CLI bindings
# at build time instead). The Jamfiles for `pwiz_tools/commandline` (msconvert)
# do have <library> refs, so it's parsed normally — these entries cover the
# tools that wouldn't otherwise produce edges.
PWIZ_TOOLS_HARDCODED_DEPS: dict[str, set[str]] = {
    "pwiz_tools/MSConvertGUI": {"pwiz_tools/commandline"},
    "pwiz_tools/SeeMS": {
        "data/vendor_readers/Thermo",   # representative — collapses to the cluster
        "analysis/spectrum_processing",
        "data/msdata",
    },
}

# Hand-curated mapping from pwiz module path -> port status.
# 'full' / 'partial' / 'none'.
STATUS = {
    # utility
    "utility/chemistry": "full",
    "utility/math": "none",
    "utility/proteome": "partial",
    "utility/misc": "partial",
    "utility/minimxml": "full",  # replaced by System.Xml
    "utility/findmf": "none",
    "utility/bindings/CLI": "none",

    # data/common (CV, ParamContainer, OBO, BinaryIndexStream, etc.)
    "data/common": "full",

    # data/...
    "data/msdata": "partial",
    "data/msdata/mz5": "partial",     # read path works (metadata + spectra + delta-decode); chromatograms + write not yet
    "data/msdata/mzmlb": "full",      # HDF5-backed mzML reader+writer, bidirectional cpp parity
    "data/misc": "partial",
    "data/proteome": "none",
    "data/identdata": "full",                 # mzIdentML + pepXML readers/writers, Diff/References, round-trip parity tests
    "data/tradata": "none",

    # vendor readers — every Reader_Foo with a TC test fixture is now green;
    # 'partial' marks readers whose .NET 8 SDK has known feature gaps vs cpp;
    # 'skipped' marks readers we deliberately won't port (rendered with strikethrough).
    "data/vendor_readers": "full",            # ExtendedReaderList dispatcher
    "data/vendor_readers/ABI": "full",        # WIFF1 + WIFF2 paths green; 6/6 tests including simAsSpectra + srmAsSpectra variants
    "data/vendor_readers/ABI/T2D": "skipped", # 32-bit-only, requires SCIEX vendor app on host; not used in modern workflows
    "data/vendor_readers/Agilent": "full",    # 11/11 tests, IM combineIMS done
    "data/vendor_readers/Bruker": "full",     # 10/10 tests
    "data/vendor_readers/Mobilion": "full",   # 2 tests; 4 cpp config variants per fixture, 4-of-4 PARITY with msconvert-cpp
    "data/vendor_readers/Shimadzu": "full",   # 2/2 tests
    "data/vendor_readers/Thermo": "full",     # 13/13 tests
    "data/vendor_readers/UIMF": "full",       # 2 tests; 1/1 fixture (BSA_10ugml_CID)
    "data/vendor_readers/UNIFI": "full",      # 44 tests; 4/4 live-harness fixtures
    "data/vendor_readers/Waters": "full",     # 20/20 tests

    # analysis
    "analysis/calibration": "none",
    "analysis/chromatogram_processing": "none",
    "analysis/common": "none",
    "analysis/demux": "full",                 # NNLS solver + MSX/Overlap demultiplexers
    "analysis/dia_umpire": "none",
    "analysis/eharmony": "none",
    "analysis/findmf": "none",
    "analysis/frequency": "none",
    "analysis/passive": "none",
    "analysis/peakdetect": "full",
    "analysis/peptideid": "none",
    "analysis/proteome_processing": "none",
    "analysis/spectrum_processing": "partial",

    # pwiz_tools (C# / msconvert applications)
    "pwiz_tools/commandline": "full",       # msconvert — ported as msconvert-sharp
    "pwiz_tools/SeeMS": "full",             # ported to .NET 8
    "pwiz_tools/MSConvertGUI": "full",      # ported to .NET 8 + CLI-vs-GUI parity tests
}

LIB_RE = re.compile(r"<library>([^\s/][^\s]*?)//([^\s]+)")

# Edges that are textually present in a foundation module's Jamfile but only because
# its test executables link the higher-layer code; the actual library has no such
# dependency. Drop these so the rendered graph isn't a tangle of false cycles.
WRONG_DIRECTION_EDGES = {
    ("utility/misc", "data/msdata"),
    ("utility/misc", "analysis/spectrum_processing"),
}

def normalize(jam_dir: Path, ref_path: str) -> str | None:
    """Resolve a `<library>relpath//libname` reference back to a pwiz/-relative
    module directory like 'data/msdata' or 'utility/chemistry'. Returns None
    for external refs (/ext/...) or refs that escape the pwiz tree."""
    if ref_path.startswith("/ext/") or ref_path.startswith("/usr/") or ref_path.startswith("ext/"):
        return None
    # Common Jamfile macro: $(PWIZ_ROOT_PATH)/pwiz/<rest> resolves to <rest> under
    # PWIZ_ROOT. Vendor reader Jamfiles use this form to depend on data/msdata.
    if ref_path.startswith("$(PWIZ_ROOT_PATH)/pwiz/"):
        rel_str = ref_path[len("$(PWIZ_ROOT_PATH)/pwiz/"):]
        return rel_str
    # Drop refs that still contain unresolved Jamfile variables (vendor APIs etc.).
    if "$(" in ref_path:
        return None
    # Relative path from the Jamfile's directory.
    target = (jam_dir / ref_path).resolve()
    try:
        rel = target.relative_to(PWIZ_ROOT.resolve())
    except ValueError:
        return None
    rel_str = str(rel).replace("\\", "/")
    # Skip the project root itself.
    if rel_str in (".", ""):
        return None
    return rel_str

def main():
    deps: dict[str, set[str]] = {}
    # Build the canonical module set first: every dir that has its own Jamfile.jam
    # under pwiz/, except the project root and parent-collector Jamfiles.
    modules: set[str] = set()
    jamfiles: list[Path] = []
    SKIP_PREFIXES = (
        "utility/bindings",   # CLI / SWIG language bindings
        "utility/findmf/base",  # internal subprojects we don't trace individually
    )
    SKIP_EXACT = {
        "data",       # parent collector
        "analysis",   # parent collector
        "utility",    # parent collector
        "data/msdata/ramp",  # sub-project, fold into msdata
    }
    # Modules that exist for edge accounting (other modules cite them in their
    # Jamfiles) but should not be drawn as a leaf — the dispatcher target is
    # represented by its cluster.
    NON_RENDERABLE = {"data/vendor_readers"}
    for jam in PWIZ_ROOT.rglob("Jamfile.jam"):
        rel = str(jam.parent.relative_to(PWIZ_ROOT)).replace("\\", "/")
        if rel == ".":
            continue
        if rel in SKIP_EXACT:
            continue
        if any(rel.startswith(p) for p in SKIP_PREFIXES):
            continue
        modules.add(rel)
        jamfiles.append(jam)

    # Add pwiz_tools/* targets as additional modules. These live outside PWIZ_ROOT
    # so they get a "pwiz_tools/<subdir>" key. Only the apps the C# port actually
    # reproduces (msconvert/commandline, MSConvertGUI, SeeMS) are included.
    PWIZ_TOOLS_INCLUDE = {"commandline", "MSConvertGUI", "SeeMS"}
    pwiz_tools_jamfiles: dict[str, Path] = {}
    for sub in PWIZ_TOOLS_INCLUDE:
        jam = PWIZ_TOOLS_ROOT / sub / "Jamfile.jam"
        if jam.exists():
            key = f"pwiz_tools/{sub}"
            modules.add(key)
            pwiz_tools_jamfiles[key] = jam

    def parse_edges(rel: str, jam: Path):
        deps.setdefault(rel, set())
        text = jam.read_text(encoding="utf-8", errors="ignore")
        for m in LIB_RE.finditer(text):
            ref = m.group(1)
            target = normalize(jam.parent, ref)
            if target is None:
                continue
            if target == rel:
                continue
            if target not in modules:
                continue
            if (rel, target) in WRONG_DIRECTION_EDGES:
                continue
            deps[rel].add(target)

    for jam in jamfiles:
        rel = str(jam.parent.relative_to(PWIZ_ROOT)).replace("\\", "/")
        parse_edges(rel, jam)

    # pwiz_tools jamfiles: parsed for any C++ <library> refs back into pwiz/.
    for key, jam in pwiz_tools_jamfiles.items():
        parse_edges(key, jam)

    # Apply hand-coded deps for tools whose Jamfiles don't expose explicit
    # library references (C# projects).
    for key, hardcoded in PWIZ_TOOLS_HARDCODED_DEPS.items():
        if key not in modules:
            continue
        deps.setdefault(key, set())
        for tgt in hardcoded:
            if tgt in modules and tgt != key:
                deps[key].add(tgt)

    fmt = sys.argv[1] if len(sys.argv) > 1 else "mermaid"
    if fmt not in ("mermaid", "dot"):
        sys.exit(f"unknown format: {fmt} (expected 'mermaid' or 'dot')")

    # Compute topological rank: a node's rank is 1 + max(rank of its targets).
    # Leaves (no outgoing edges) get rank 0. The result is the longest-path depth
    # from sinks, which lines nodes up so every arrow points strictly downward.
    rank: dict[str, int] = {}
    def compute_rank(m: str, stack: set[str]) -> int:
        if m in rank:
            return rank[m]
        if m in stack:  # cycle guard (shouldn't happen with DAG, but be defensive)
            return 0
        stack.add(m)
        targets = deps.get(m, set())
        r = 0 if not targets else 1 + max(compute_rank(t, stack) for t in targets)
        stack.discard(m)
        rank[m] = r
        return r
    for m in modules:
        compute_rank(m, set())

    # Node ids must be valid Mermaid identifiers (no slashes). Use a sanitized name.
    def node_id(m: str) -> str:
        return "n_" + m.replace("/", "__").replace("-", "_").replace(".", "_")

    if fmt == "dot":
        # Graphviz dot — true hierarchical layout, arrows always go down.
        STATUS_COLORS = {
            "full":    ("#9ee5a3", "#2c7a32", "#0a3d10"),
            "partial": ("#ffe48a", "#a87000", "#4a3000"),
            "none":    ("#f5a8a8", "#a02020", "#3d0a0a"),
            # Won't-port: muted gray fill, dashed-style border via the node block
            # below (Graphviz `style` is per-node so the strikethrough effect is
            # done in the label HTML, not via fillcolor).
            "skipped": ("#dcdcdc", "#888888", "#555555"),
        }

        # Group modules at the "leaf-1" level — i.e., the directory immediately
        # above the leaf module. Dependency arrows are drawn between groups; port
        # progress is still colored per-leaf inside each group.
        def group_of(m: str) -> str:
            if m == "data/vendor_readers" or m.startswith("data/vendor_readers/"):
                return "data/vendor_readers"
            if m == "data/msdata" or m.startswith("data/msdata/"):
                return "data/msdata"
            # data/common is a foundation library (CV, ParamContainer, OBO) that
            # both data/msdata and the consumer-side data modules (identdata,
            # tradata, proteome) depend on. Promote it to its own cluster so the
            # `data` cluster doesn't appear to cycle with `data/msdata`.
            if m == "data/common":
                return "data/common"
            if m.startswith("pwiz_tools/"):
                return "pwiz_tools"
            return m.split("/")[0]

        def cluster_id(g: str) -> str:
            return "cluster_" + g.replace("/", "_").replace("-", "_")

        # Renderable modules — what actually gets drawn as boxes inside clusters.
        renderable = sorted(m for m in modules if m not in NON_RENDERABLE)

        groups: dict[str, list[str]] = {}
        for m in renderable:
            groups.setdefault(group_of(m), []).append(m)

        # Aggregate edges to the group level, dropping intra-group edges.
        cluster_deps: dict[str, set[str]] = {}
        for src, dsts in deps.items():
            sg = group_of(src)
            for dst in dsts:
                dg = group_of(dst)
                if sg == dg:
                    continue
                cluster_deps.setdefault(sg, set()).add(dg)

        # Transitive reduction: drop A->B when there's a path A->X->...->B that
        # already implies it. We check against the CURRENT reduced graph (not the
        # original) so cycles like data <-> data/msdata don't cause over-pruning
        # of shared targets like utility.
        def transitive_reduce(graph: dict[str, set[str]]) -> dict[str, set[str]]:
            nodes = set(graph) | {t for ts in graph.values() for t in ts}
            reduced = {n: set(graph.get(n, set())) for n in nodes}

            def reachable_excluding(u: str, v: str) -> bool:
                """True if v is reachable from u in `reduced` without taking
                the direct u->v edge."""
                visited = {u}
                stack = [w for w in reduced[u] if w != v]
                while stack:
                    cur = stack.pop()
                    if cur in visited:
                        continue
                    visited.add(cur)
                    if cur == v:
                        return True
                    stack.extend(reduced.get(cur, set()))
                return False

            for u in sorted(reduced):
                for v in sorted(list(reduced[u])):
                    if reachable_excluding(u, v):
                        reduced[u].discard(v)
            return reduced

        cluster_deps = transitive_reduce(cluster_deps)

        print("digraph pwiz_modules {")
        print('  rankdir=TB;')
        print('  labelloc=t;')
        print('  label=<<b>pwiz module dependencies</b><br/><font point-size="10">arrows aggregate at the directory level; foundations on top, consumers on the bottom</font>>;')
        print('  fontname="Segoe UI";')
        print('  node [shape=box, style="filled,rounded", fontname="Segoe UI", fontsize=11];')
        print('  edge [color="#444", penwidth=1.4, arrowsize=0.9];')
        print('  splines=spline;')
        print('  ranksep=0.8; nodesep=0.3;')
        print('  compound=true;')
        print()

        def emit_node(m: str, indent: str = "  "):
            status = STATUS.get(m, "none")
            fill, stroke, text = STATUS_COLORS[status]
            parts = m.split("/")
            # Inside a cluster, the cluster header already shows the parent path —
            # use just the leaf component to keep boxes compact.
            label = parts[-1]
            # 'skipped' nodes get a strikethrough (HTML <s> wrapper) so it reads as
            # "won't port" at a glance even before the legend is consulted.
            # Graphviz HTML labels are written with angle-bracket delimiters, no quotes.
            label_attr = f"<<s>{label}</s>>" if status == "skipped" else f'"{label}"'
            print(f'{indent}{node_id(m)} [label={label_attr}, fillcolor="{fill}", color="{stroke}", fontcolor="{text}"];')

        # Pick a representative node per cluster for use as edge endpoints. Edges
        # use lhead/ltail to attach to the cluster boundary instead of the node.
        rep: dict[str, str] = {g: node_id(ms[0]) for g, ms in groups.items()}

        for g in sorted(groups.keys()):
            cid = cluster_id(g)
            print(f'  subgraph {cid} {{')
            print(f'    label="{g}";')
            print( '    style="rounded,dashed";')
            print( '    color="#888";')
            print( '    fontname="Segoe UI";')
            print( '    fontsize=12;')
            print( '    margin=12;')
            for m in groups[g]:
                emit_node(m, indent="    ")
            print('  }')
            print()

        # Reverse arrow direction so foundations end up at the top under rankdir=TB:
        # original sg "depends on" dg, so we emit dg -> sg ("dg is used by sg").
        for sg in sorted(cluster_deps):
            for dg in sorted(cluster_deps[sg]):
                print(f'  {rep[dg]} -> {rep[sg]} [ltail={cluster_id(dg)}, lhead={cluster_id(sg)}, minlen=2];')
        print("}")
        return

    # Emit Mermaid with ELK renderer (true hierarchical layout — keeps arrows pointing down).
    print("%%{init: {'flowchart': {'defaultRenderer': 'elk'}}}%%")
    print("flowchart TD")
    print("    classDef full fill:#9ee5a3,stroke:#2c7a32,color:#0a3d10")
    print("    classDef partial fill:#ffe48a,stroke:#a87000,color:#4a3000")
    print("    classDef none fill:#f5a8a8,stroke:#a02020,color:#3d0a0a")
    print("    classDef skipped fill:#dcdcdc,stroke:#888888,color:#555555")
    print()
    # Group nodes by rank (highest rank first, so they appear at the top of TD output).
    by_rank: dict[int, list[str]] = {}
    for m in sorted(modules):
        by_rank.setdefault(rank[m], []).append(m)
    for r in sorted(by_rank.keys(), reverse=True):
        for m in by_rank[r]:
            nid = node_id(m)
            status = STATUS.get(m, "none")
            parts = m.split("/")
            label = "/".join(parts[-2:]) if len(parts) > 2 else m
            print(f'    {nid}["{label}"]:::{status}')
    print()
    for src in sorted(deps):
        for dst in sorted(deps[src]):
            print(f"    {node_id(src)} --> {node_id(dst)}")

if __name__ == "__main__":
    main()
