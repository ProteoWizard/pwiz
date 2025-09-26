# Skyline C# coding style

This guide captures Skyline-specific C# conventions to keep AI- and human-authored code consistent with `pwiz_tools/Skyline`.

## Control flow
- If statements must not be single-line. If braces are omitted, keep the condition and body on separate lines.
  
  Bad:
  
  ```csharp
  if (condition) DoThing();
  ```
  
  Good (without braces):
  
  ```csharp
  if (condition)
      DoThing();
  ```
  
  Good (with braces):
  
  ```csharp
  if (condition)
  {
      DoThing();
  }
  ```

## File and member ordering (within a class)
Order members to make high-level logic easy to read first:
1) static variables/fields
2) static public interface methods
3) private instance fields
4) constructor(s)
5) public interface (instance) methods and properties
6) private helper methods

Additional guidance:
- Place private helpers after the public methods that use them; keep helpers close to their primary call sites.
- Avoid “old C” style where helpers appear at the top and the main logic at the bottom.

## General guidelines
- Match surrounding file style (indentation, spacing, line breaks).
- Prefer focused edits; do not reformat unrelated code.
- Keep names descriptive and intention-revealing; avoid abbreviations.
- Keep methods small and cohesive; extract helpers as needed (placed after usage as above).

## Naming conventions (mirrors ReSharper rules)
- Private instance fields: prefix with `_` and use `camelCase` (e.g., `_filePath`).
- Private static fields: prefix with `_` and use `camelCase`.
- Constants (any access): `ALL_CAPS_WITH_UNDERSCORES`.
- Static readonly (any access): `ALL_CAPS_WITH_UNDERSCORES` when used like constants.
- Locals and parameters: `camelCase`.
- Types and namespaces: `PascalCase`.
- Interfaces: `I` prefix (e.g., `IResultSet`).
- Type parameters: `T` prefix (e.g., `TItem`).
- Enum members: `snake_case` (e.g., `not_set`).

## Whitespace and formatting
- Tabs are disallowed; use spaces. Do not change existing files’ indentation, but when adding new code use spaces.
- Avoid mixing tabs and spaces. Align with existing file formatting.

## Tools
- We develop with Visual Studio 2022 and ReSharper; aim for warning-free under its inspections.
- Follow the Skyline build guide for environment setup: https://skyline.ms/wiki/home/software/Skyline/page.view?name=HowToBuildSkylineTip

## Executables solutions
Projects under `pwiz_tools/Skyline/Executables` are independent solutions (stand‑alone EXEs, developer tools, or utilities included with Skyline). They are not built by `Skyline.sln`. Prefer the same conventions as Skyline unless a local project requires an override.

EditorConfig
- All solutions inherit repository-wide `.editorconfig` for C# naming/formatting.
- If a specific Executables project needs different rules, add a minimal project-level `.editorconfig` or project `.DotSettings` override local to that solution only.

## Resource strings (localization)
- Add new UI strings for menus/dialogs/pages to `pwiz_tools/Skyline/Menus/MenusResources.resx`.
- Strings will be translated to Chinese/Japanese via our translation process; use clear, concise English.
- Generate resource keys from the English text in a ReSharper-like way:
  - Replace all non-alphanumeric characters with underscores `_`.
  - Collapse sequential underscores into one; trim leading/trailing underscores.
  - Preserve digits; use `PascalCase` word boundaries in the base text where natural.
  - Keys often include the context prefix (e.g., `SkylineWindow_`, `EditMenu_`) followed by the transformed text.
  - Example: "Keyboard Shortcuts" → `Keyboard_Shortcuts`; "File > New" → `File_New`.
- Prefer reusing existing keys when text matches; avoid near-duplicate strings.

### CRITICAL: .resx file workflow
When adding new resource strings to a .resx file, you MUST also add the corresponding public static string properties to the .Designer.cs file. The compiler will fail with CS0117 errors if the Designer.cs file is not updated.

Example workflow:
1. Add `<data name="MyNewString" xml:space="preserve"><value>My New String</value></data>` to .resx
2. Add `public static string MyNewString => ResourceManager.GetString("MyNewString", resourceCulture);` to .Designer.cs
3. Build to verify no CS0117 errors

## User interface guidelines

### Menu items
- All items in `menuMain` and its submenus should have mnemonics (e.g., `&Keyboard Shortcuts`)
- Menu text and action button text should use title-case (e.g., "Keyboard Shortcuts")
- Only menu items in `menuMain` should have mnemonics and keyboard shortcuts
- Context menus should not have mnemonics or keyboard shortcuts
