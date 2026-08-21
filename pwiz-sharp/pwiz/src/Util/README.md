# Pwiz.Util

Foundation port of the C++ `pwiz/utility/{misc,chemistry,math}` trees for the pwiz-sharp project.

## Layout

| Folder | Ports from | Notes |
|---|---|---|
| `Chemistry/` | `pwiz/utility/chemistry/` | Full port. `ElementData.generated.cs` is generated from `ChemistryData.cpp` by `Chemistry/generate_elements.py`. |
| `Numerics/` | `pwiz/utility/math/` | Partial port — see below. Named `Numerics` to avoid clashing with `System.Math` inside `Pwiz.Util`-rooted code. |
| `Misc/` | `pwiz/utility/misc/` | Only items without a clean BCL equivalent. See below. |

## What's ported

### Chemistry
- `PhysicalConstants` — proton/neutron/electron masses.
- `Ion` — m/z ↔ neutral-mass conversions.
- `MZTolerance` — absolute or ppm tolerance (readonly record struct with `Parse`/`TryParse`).
- `ElementType` — enum of 118 elements plus labeled isotope variants (`_2H`, `_13C`, `_15N`, `_18O`, `_3H`).
- `MassAbundance`, `ElementRecord`, `ElementInfo` — data tables built from the upstream C++ isotope tables.
- `Formula` — parse (`H2O`, `_13C6H12O6`, `D2O`), arithmetic (+, −, scalar ×), monoisotopic/molecular mass, canonical `ToString`.
- `IsotopeTable` — multinomial-expansion isotope distribution cache.
- `IsotopeCalculator` — per-element distributions convolved into a full molecular envelope with normalization options.

### Numerics
- `Parabola` — fit a parabola to 3+ samples (optionally weighted), evaluate, vertex center.

### Misc
- `IntegerSet` — sorted interval-union container with pwiz-compatible `Parse` (`"[-3,2] 5 8-9 10-"`).
- `IterationListener` / `IterationListenerRegistry` — progress callbacks with iteration-count or time-based throttling and cancel propagation.
- `FloatingPoint.AlmostEqual` — machine-epsilon-based float comparison.

## What's deferred

Not needed by Phase 2 (`Pwiz.Data.MsData`) — port when the first consumer needs them.

- `IsotopeEnvelopeEstimator` (empirical envelope fitting).
- `HouseholderQR`, `LinearSolver`, `MatrixInverse`, `MatchedFilter`, `Stats` — when needed, prefer `MathNet.Numerics` types over new ports.
- `erf` — use `MathNet.Numerics.SpecialFunctions.Erf`.
- `OrderedPair`, `Types.hpp` — covered by `System.Numerics.Vector2` / tuples.

## What intentionally uses BCL instead

Skipping these ports because the BCL does the same job better/safer:

| pwiz C++ | Use instead |
|---|---|
| `misc/String.hpp` (trim, split, to_lower) | `string.Trim()`, `string.Split()`, `string.ToLower(CultureInfo.InvariantCulture)` |
| `misc/Filesystem.hpp` | `System.IO.Path`, `System.IO.File`, `System.IO.Directory` |
| `misc/DateTime.hpp` | `System.DateTime` / `DateTimeOffset` |
| `misc/Base64.hpp` | `Convert.ToBase64String` / `Convert.FromBase64String` |
| `misc/SHA1Calculator.hpp` | `System.Security.Cryptography.SHA1.HashData` |
| `misc/endian.hpp` | `BitConverter.IsLittleEndian` / `System.Buffers.Binary.BinaryPrimitives` |
| `misc/Timer.hpp` | `System.Diagnostics.Stopwatch` |
| `misc/Environment.hpp` | `System.Environment` |
| `misc/Singleton.hpp`, `Once.hpp` | `Lazy<T>` |
| `misc/Stream.hpp` (nowide) | `StreamWriter`/`StreamReader` with `Encoding.UTF8` |
| `misc/span.hpp` | `System.Span<T>` / `System.Memory<T>` |
| `misc/BinaryData.hpp` | `double[]` / `List<double>` / `Span<double>` as appropriate — no extra type needed. |
| `misc/shared_map.hpp`, `virtual_map.hpp`, `mru_list.hpp` | `Dictionary<K,V>`, `LinkedList<T>` + `Dictionary` for MRU caches |
| `misc/CharIndexedVector.hpp` | `T[256]` indexed by `(byte)c` |
| `misc/sort_together.hpp` | `Array.Sort(Array, Array, IComparer)` overloads |
| `misc/optimized_lexical_cast.hpp` | `double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, ...)` |
| `misc/random_access_compressed_ifstream.hpp` | `System.IO.Compression.GZipStream` (when cross-platform random-access gzip is needed, revisit) |
| `misc/TabReader.hpp` | Hand-written `StreamReader.ReadLine() + line.Split('\t')` loop per call site — the abstraction wasn't carrying its weight. |
| `misc/IPIFASTADatabase.hpp` | Belongs with `Pwiz.Data.Proteome` (Phase 2); port there. |
| `utility/minimxml/` | `System.Xml.XmlReader` / `XmlWriter` (streaming). Phase 2 will use these directly. |
| `utility/proteome/` | Moves into `Pwiz.Data.Proteome` in Phase 2. |
| `utility/math/round.hpp` | `Math.Round`, `Math.Floor`, `Math.Ceiling` |

## Generators

- `Chemistry/generate_elements.py` — rebuilds `ElementData.generated.cs` from `pwiz/utility/chemistry/ChemistryData.cpp`. Re-run after any edit to the upstream C++ file.
- Upstream `Pwiz.Data.Common/Cv/generate_cvid.py` rebuilds the CVID enum from `cv.hpp`; it's in the other project but follows the same convention.

## Testing

All ports have MSTest tests in `test/Pwiz.Util.Tests/` mirroring the source folder layout. Key verifications:
- Chemistry: real-mass spot checks (water 18.0106, glucose 180.0634) against reference values.
- Formula: round-trip parse, arithmetic, deuterium/isotope-label recognition.
- IsotopeCalculator: monoisotopic peak dominates for small molecules, M+1 delta ≈ 1.003 for C₁₀₀, L2-normalization sums to 1.
- IntegerSet: full pwiz parse syntax, coalescing semantics, predefined sets.
- IterationListener: period-based + time-based throttling, cancel propagation.
