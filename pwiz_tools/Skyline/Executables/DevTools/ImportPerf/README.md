# ImportPerf

Performance testing tool for benchmarking parallel chromatogram extraction in Skyline.
Distributes raw MS data files across multiple Skyline worker processes and measures
import throughput.

## Background

This tool was created to compare process-based vs. thread-based chromatogram extraction
strategies. Thread-based extraction eventually won when .NET server-GC mode provided
performance comparable to separate processes. However, out-of-process extraction is being
reconsidered because keeping all instrument vendor DLLs in-process prevents Skyline from
moving past .NET 4.7.2 and provides limited control over vendor memory usage.

This tool supersedes the older `MultiLoad` tool (now removed) which served a similar
purpose.

## Usage

Command-line tool that spawns multiple Skyline processes, distributes data files across
workers, and aggregates timing results. Configure via command-line arguments for number
of processes, file lists, and output logging.
