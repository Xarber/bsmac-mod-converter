# Testing

Run `python -m pytest tests` for synthetic, redistributable regression fixtures covering CRC validation, map allowlists, preservation of originals, corruption rejection, content-addressed keys, library manifest preservation and safe installation/rollback. Generate real coverage with `coverage run --branch --source=bsmac_converter -m pytest tests` and `coverage report`.

CI runs synthetic safety tests and schema/registry validation. It cannot reconstruct private game assets or the proprietary backend. Legacy integration, Metal compilation, managed-reference builds, runtime startup, shader appearance, audio reactivity and headset gameplay require the local environment. Record these separately; missing runtime evidence is not a pass.

Initial targets for changed testable business logic are 80% line and 70% branch coverage. Overall source coverage includes uninstrumented backend-dependent legacy conversion. Do not substitute its percentage for changed-code coverage. Narrow tested-module reports and integration counts must be labeled with their exact scope.

Native test procedure: preserve hashes and backups; stop the game; install only validated staged files; sign and verify; launch the same instance; inspect new logs; test `you — Swifter` then `One of These Mornings`, each from a clean session if a prior abort leaked bundles. Confirm the bridge's success message separately from successful level initialization. For AudioLink verify provider creation, live audio sample input, CustomRenderTexture updates and visible reactive effects. For CustomWalls test all built-in walls, including procedural particle instancing. Test working mods for regressions. Never infer headset appearance from successful bundling.
