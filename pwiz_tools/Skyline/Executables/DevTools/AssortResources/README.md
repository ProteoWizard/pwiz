# AssortResources

Refactoring tool that moves single-use resources from the centralized `Resources.resx` file
into per-dialog/per-form resource files. This reduces the size of the shared resource file
and keeps resources closer to the code that uses them.

## Author

Nicholas Shulman, MacCoss Lab, University of Washington

## Usage

```
AssortResources --projectfile Skyline.csproj --resourcefile Properties\Resources.resx
```

Use `--inspectonly true` to preview what would be moved without modifying any files.

## How It Works

The tool analyzes which `.cs` files reference each resource string. If a resource is only
used by files associated with a single form or dialog, it moves that resource from the
shared `Resources.resx` into the form's own `.resx` file. It also updates the corresponding
`.Designer.cs` files.

Multi-project awareness prevents moving resources that are shared across project boundaries.

## Integration with CodeInspectionTest

`CodeInspectionTest` in the Test project compiles several AssortResources source files as
linked files (`CsProjFile.cs`, `ResourceAssorter.cs`, etc.) to perform resource placement
validation at test time. If misplaced resources are detected, the test can auto-invoke
`AssortResources.exe` to self-heal.
