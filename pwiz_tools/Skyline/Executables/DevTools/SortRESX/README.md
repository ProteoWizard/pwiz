# SortRESX

Sorts `.resx` (XML resource) files alphabetically by key name to reduce merge conflicts
in version control. When multiple developers add resource strings, unsorted entries cause
unnecessary diff noise and merge pain.

## Author

Nicholas Shulman, MacCoss Lab

## Usage

```
SortRESX [--preserveOrderInResourcesResx] [folder1 folder2 ...]
```

With no arguments, processes the current directory recursively. Reports the number of
modified files and elapsed time.

The `--preserveOrderInResourcesResx` flag skips sorting the main `Resources.resx` file,
which may have a deliberately maintained order.
