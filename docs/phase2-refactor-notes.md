# Phase 2 Refactor Notes

## What changed

- Added `IActiveLibraryContext` and `ActiveLibraryContext` in the application layer so use-case services can resolve the current library without pushing `LibraryProfile` through every call.
- Refactored `LibraryBooksService` into an orchestration service with active-library-aware methods and shared provider resolution.
- Added clearer application-facing result models: `BookSearchResult`, `BookDetails`, `BookSourceDescriptor`, and `BookContentHandle`.
- Introduced `OfflineCatalogCache` so offline INPX catalogs are cached by library identity plus file metadata instead of being reparsed per provider instance.
- Reworked `BookProviderFactory` to use provider registrations instead of a growing switch.
- Moved schema initialization out of DI registration and into explicit startup initialization.
- Added focused tests for active library context, active-library service flow, offline cache reuse/invalidation, and logical identity / dedup behavior.

## Why

- The application layer now owns orchestration responsibilities instead of acting as a thin pass-through to providers.
- Offline provider performance is stable across repeated operations because the parsed catalog survives provider recreation.
- Provider result models are clearer for future UI and aggregation work while existing provider contracts stay intact.
- Provider registration is easier to extend as new sources are added.
- Startup behavior is more explicit and service registration is closer to pure wiring.

## Next recommended step

- Introduce a dedicated cross-source aggregation service on top of `LibraryBooksService` that consumes multiple provider registrations and uses the logical identity builder to merge ranked results across offline and online libraries.
