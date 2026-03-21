# UI Refresh Notes

## What changed

- Replaced the placeholder split window with a real Avalonia app shell: top header, left library sidebar, central search/results workspace, and a right details pane.
- Added a lightweight MVVM-style layer in the app project so the window stays mostly focused on UI events and platform-specific actions.
- Made search the primary workflow with a prominent header search box, active-library visibility, richer result cards, and better metadata grouping.
- Added first-run, search-prompt, no-results, and no-selection empty states instead of blank panels.
- Added a small add-library dialog for offline INPX libraries and preset online sources, including file/folder browse actions, smarter default naming, and local path validation.
- Added status, loading, error, and action feedback for searching, opening content, and copying links.
- Surfaced active-library source health in the shell so users can see whether an offline index, archive set, or online endpoint is ready before they start searching.

## UX improvements

- The active library is always visible in the header and sidebar.
- The shell now communicates source readiness instead of only showing the selected profile name.
- Library selection now reads like intentional navigation instead of raw text.
- Results are easier to scan thanks to title hierarchy, author/meta lines, source/format badges, and stronger spacing.
- The details pane groups actions, description, metadata, and source information into readable sections.
- First-run users immediately see what to do and how offline versus online sources differ.
- Busy states now disable conflicting search and selection interactions so repeated clicks do not fight with library activation or detail loading.

## Next recommended visual enhancements

- Add lightweight provider icons or monograms per source for even faster scanning.
- Introduce optional cover thumbnails for providers that expose image URLs.
- Add a compact preferences screen for theme density, search defaults, and library management.
