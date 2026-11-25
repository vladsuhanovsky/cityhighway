# City Generator Lab v0.1

Open **City Highways > City Generator Lab** in Unity outside Play Mode.
Enter a seed and click Generate and validate, or run 1–512 consecutive seeds.
The batch can be cancelled between seeds; the last generated layout is shown as a 2D topology preview.
No generator settings are changed. Requested and actual seed are separate because the existing generator retries with different seeds.
Failures are automatically exported under Library/CityHighways/CitySeedDiagnostics; the selected result can be exported to a chosen directory.
JSON schema 1 contains map name, districts, locations, roads, river centerlines, hub IDs/types, public roads, error messages, elapsed wall-clock time and Unity version.
If generation throws before returning a city, the report contains the seed and error but not rejected internal attempts.
CI range tests exercise 64 seeds plus six runs for determinism; full graph connectivity, location/road intersections, existing water checks and hub references are checked. Public-road connectivity is NOT required (the generator deliberately excludes graph bridges).
A future extension can expose generator layout presets, 3D footprint audit, attempt-by-attempt reports, and a captured build/commit ID. No 3D mesh or performance certification here.
