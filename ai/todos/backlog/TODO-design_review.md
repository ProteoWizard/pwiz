# TODO: Skyline UI/UX Design Review

## Objective
Conduct a comprehensive design review of the Skyline UI to improve consistency, accessibility, and visual polish across the application.

## Background
Skyline's design has emerged organically over 17+ years:
- Original branding (logo, icons, website) from contract designer
- Color palettes improvised for chromatograms/bar graphs
- Various developers added features without consistent design guidance
- No dedicated designer available for day-to-day feedback

## Areas to Review

### Icons
- **Missing icons** - Some features (e.g., audit logging report, grid views) use default .NET icons or no icon
- **Consistency** - Ensure all similar features have similar icon styles
- **16x16 toolbar icons** - Review for clarity and consistency
- **Tool**: `Skyline\Executables\ImageExtractor` can access VisualStudio ImageCatalog for base icons

### Color Palettes
- **Chromatogram colors** - Original palette works but not optimal for projected screens
- **"Distinct" color set** - Better for teaching/projection, consider making default or more prominent
- **Scatter plot points** - Consider hollow circles instead of filled gray for density visualization (Relative Abundance, Volcano plots)
- **Accessibility** - Review for colorblind-friendly options

### Form Design
- **Control placement** - Inconsistent layouts across dialogs
- **Mnemonics** - Inconsistent use of keyboard accelerators (Alt+letter)
- **Tab order** - May not be logical in all forms
- **Sizing/spacing** - Inconsistent margins and padding

### Graph/Plot Design
- **Point styles** - Hollow vs filled markers for dense plots
- **Legend placement** - Consistency across graph types
- **Axis labels** - Font sizes, formatting consistency
- **Grid lines** - When to show/hide

### General UI
- **Toolbar organization** - Logical groupings
- **Menu structure** - Consistency in naming and organization
- **Status indicators** - Progress bars, "Calculating..." text, etc.
- **Tooltips** - Coverage and helpfulness

## Approach
1. **Audit current state** - Document existing patterns and inconsistencies
2. **Establish style guide** - Document preferred patterns for future development
3. **Prioritize fixes** - Focus on most visible/impactful issues first
4. **Incremental improvement** - Fix issues as features are touched

## Potential Claude Code Assistance
- Review forms for layout consistency
- Suggest color palette improvements
- Help create design documentation
- Identify missing icons or inconsistent patterns
- Generate icon suggestions (though 16x16 remains challenging for LLMs)

## Related
- ImageExtractor tool for accessing VisualStudio ImageCatalog
- Existing customizable color palettes feature
- Tutorial screenshot reviews often reveal design issues
