# Stage 2 — targeted accessibility hardening

Verified gaps: DisplaySettings applied arbitrary scales while the selector defaulted to 100% for unmatched values; Palette.TextOn used a gamma-space brightness threshold that allowed low contrast on solid fills; floating totals, the Classic luck-card flip and map flash ignored reduced motion.

- Use the same supported 90/100/110/120/130% scale grid for persisted, copied, applied and selected settings. Invalid/nonpositive values default to 100%; finite out-of-range values clamp and intermediate values snap to nearest. Manually supplied 80/140% now normalize to supported menu options. Valid saved options remain unchanged.
- Compute opaque sRGB relative luminance and contrast. Select black/white foreground, retaining softer dark text if it reaches 4.5:1. Player/resource backgrounds and editable InterfaceTheme remain unchanged.
- Reduced motion keeps floating totals stationary, displays the luck-card result at the center for its existing hold time without flight/flip, retains result audio and blocking cleanup, and suppresses map border flash. Normal mode timings remain unchanged.
- Fourteen new Edit Mode cases cover scale, compatibility, contrast and stationary floating text. No save/replay schema or rules change.

Manual acceptance: check all scales in menu and both modes at 1280×800 and desktop, restart and verify persistence; inspect illustrated cards, player markings and custom Interface Editor themes. Opaque color math does not establish accessibility conformance. Compare motion modes during card draw, luck, toll, traffic, map events and Undo/Redo. Hardware/controller QA remains pending. Alternate colorblind palettes and redundant ownership symbols are not added here.
