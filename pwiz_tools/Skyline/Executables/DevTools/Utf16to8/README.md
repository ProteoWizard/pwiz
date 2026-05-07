# Utf16to8

Simple encoding converter that rewrites UTF-16 encoded files as UTF-8 with BOM, updating
any XML encoding declarations in the content.

## Usage

```
Utf16to8 <sourceFile> <destinationFile>
```

Useful for converting XML files that were saved in UTF-16 encoding to the UTF-8 encoding
expected by the Skyline build and resource tooling.
