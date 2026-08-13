# NormalizeResxWhitespace

Normalizes `.resx` file formatting to reduce diff noise in version control:

- Converts encoding to UTF-8 without BOM
- Converts tabs in comments to spaces
- Removes extraneous text/comment nodes from top-level XML elements
- Applies consistent XML indentation

## Usage

```
NormalizeResxWhitespace file1.resx [file2.resx ...]
```

Modifies files in place.
