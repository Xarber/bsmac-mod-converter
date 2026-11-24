# Releases and rollback

This is a public source tooling project, not a redistributed game or mod pack. Repository publication is authorized; binary releases are not currently authorized. New repository branches are `testing`, `beta`, `main`; development lands on testing. Quality checks may run there; full application/release builds do not run automatically on testing.

Version 0.1.0 identifies development source only. Do not treat it as a published release. Any future promotion follows testing → beta → main and needs applicable validation and publication authorization. Public visibility, alternative-store distribution and game/mod redistribution are distinct permissions.

Only source and synthetic fixtures may be uploaded. Native compatibility libraries can be built from source against user-supplied game references; CI must not fetch or publish proprietary references. Local transactions preserve per-file hashes and backups. Roll back with the CLI receipt, then re-sign the local app. Do not overwrite files modified after installation, remove user preferences, or downgrade unrelated working mods.
