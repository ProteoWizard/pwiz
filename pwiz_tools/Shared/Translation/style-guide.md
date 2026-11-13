# Japanese and Chinese translation style guide

Derived from Skyline's reviewed translations, not written from scratch: every rule below gives
the share of reviewed `.ja.resx` / `.zh-Hans.resx` values that follow it (9,888 ja and 10,058 zh
English-to-translation pairs, measured 2026-09-28). Where the reviewers split, the majority is the
rule and the minority is named, so a reviewer can overturn it knowingly.

Terms are in `glossary.tsv` beside this file. A `reviewed` row records what Skyline's native-speaker
reviewers settled on, including deliberate non-literal choices (zh 划定 for "imputation"); a `new`
row is a first proposal for a term Skyline never translated and needs a reviewer's decision.

## Never translate

- Placeholders and format items: `{0}`, `{1:N0}`, `{2:P1}`, `{3:F4}` stay byte-identical (33/33
  reviewed values with format items keep them verbatim). Their order may change to suit the
  sentence; their set may not.
- Anything the user types or searches for: command-line flags, task names (`PerFileScoring`,
  `FirstPassFDR`), file extensions, column and metadata names. Osprey passes these as `{N}`
  arguments, so they normally never appear in the text at all.
- Product, format and algorithm names: Osprey, Skyline, Percolator, Mokapot, BiblioSpec,
  DIA-NN, ProteoWizard, msconvert, mzML, Parquet, JSON, SVM, LDA, LOESS.
- Acronyms: FDR, PEP, RT, MS1, MS/MS, DIA, DDA, ppm, CV. Exception: zh writes "m/z" as `质荷比`
  (181/181 reviewed values); ja keeps `m/z` (161/181).
- A leading `Error:` / `Warning:` becomes exactly `エラー：` / `警告：` (ja) or `错误：` / `警告：` (zh)
  (163/163 and 31/31 in both languages). Osprey matches these prefixes to decide a message's
  severity, so no variation is allowed.

## Punctuation and spacing

| Rule | ja | zh |
|---|---|---|
| Colon | full-width `：` (93%) | full-width `：` (97%) |
| Sentence end | `。` (98%) | `。` (99%) |
| Quoted value `'{0}'` | `「{0}」` (93%) | `“{0}”` (85%; `'{0}'` 13%) |
| Command-line token | bare | in `“ ”` (Skyline's zh CLI help: `“--reintegrate-model-name”`) |
| Space between CJK and Latin letters or digits | none (3,784 vs 32) | one space (3,367 vs 58) |
| Space between CJK and `{n}` | none (2,828 vs 52) | one space (1,681 vs 1,323) |
| Parentheses in running text | full-width `（ ）` (73%) | full-width `（ ）` (71%) |
| Trailing ellipsis | `...` (96%) | `…` (98%) |
| Enumeration comma | `、` (88%) | `，` (84%) |

Parentheses that enclose only a formula or data (`(FDR={2:F4})`, `(log10)`) may stay half-width;
the reviewed values do this in about a quarter of cases.

## Sentence patterns

- Progress lines ("Loading the library...", "Scoring {0} peaks"): ja uses `〜中...`
  (`データを読み込み中...`); zh uses `正在〜…` (`正在加载数据…`).
- "Failed to X" / "X failed": ja `Xに失敗しました` or `Xできませんでした`; zh `X失败` or `无法X`.
- Imperative remedies ("Delete the file and run again."): ja `〜してください。`; zh `请〜。`.
- Counts: zh puts a measure word between a number and its noun (`{0} 个峰`); ja attaches the
  noun directly (`{0}個のピーク` or `ピーク{0}個`).
- Keep the English sentence's information order when a log line is scanned by eye (the number,
  then what it counts), but not at the cost of grammar.

## Osprey-specific

- "run" in the sense of an MS acquisition is `ラン` in ja (tutorial usage: `DIAラン`); an Osprey
  invocation ("this run") is `実行`. zh uses `运行` for both, `本次运行` for "this run".
- "intermediate file" is the user-facing name for Osprey's per-stage files; never "sidecar".
- Log output is read in a console and grepped; do not add decorative punctuation.
