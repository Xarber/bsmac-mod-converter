# UADS requirement scope

Baseline: unversioned UADS supplied on 2026-11-24; no historical version is invented. This CLI/library repository records applicable requirements in config/uads-requirements.json and states in uads.json. Section 30 scope and existing functionality rules govern interpretation. The registry is a scoped initial registry, not exhaustive atomic coverage of every policy sentence. No complete UADS conformance claim is made.

No UI, account, hosted service, mobile application, store distribution or public release is introduced. In-app onboarding/PWA/developer pages do not apply to a CLI and compatibility library. Ideas are kept in docs/IDEAS.json; agents should inspect them when resuming. AGENTS.md is explicitly excluded by the user. Structural validation is separate from functionality.

- UADS-DOC-001: Durable project context (§4); Required.
- UADS-GIT-001: Private repository and development branches (§6); Required.
- UADS-GIT-002: Conventional commits and preserved history (§7); Required.
- UADS-QA-001: Observed verification and test reports (§23); Required.
- UADS-QA-002: Objective source quality gates (§42); Required.
- UADS-SEC-001: Preserve originals and backups (§21); Required.
- UADS-SEC-002: Validate untrusted assets and trust boundaries (§43); Required.
- UADS-DATA-001: Distinguish conversion from runtime evidence (§19); Required.
- UADS-DATA-002: Synthetic fixtures isolated from real assets (§20); Required.
- UADS-CI-001: Executable GitHub Actions quality checks (§49); Required.
- UADS-REL-001: Traceable version and release identity (§44); Conditional.
- UADS-DIST-001: Alternative distribution sources (§25); Not Applicable.
- UADS-OPS-001: Production service monitoring (§45); Not Applicable.
- UADS-UI-001: Application UI accessibility (§18); Not Applicable.
- UADS-PLAT-001: Supported platform declaration (§10); Required.
- UADS-DEV-001: In-app developer options (§15); Not Applicable.
- UADS-DEV-002: Persistent Ideas collection (§35); Required.
- UADS-UI-002: In-app release history (§16); Not Applicable.
- UADS-UI-003: Onboarding and screenshots (§37); Not Applicable.
- UADS-PLAT-002: PWA support (§32); Not Applicable.
- UADS-PLAT-003: Hosted web presence (§36); Not Applicable.
- UADS-SEC-003: Backend environment isolation (§38); Not Applicable.
- UADS-DOC-002: Manifest and applicability states (§41); Required.
- UADS-DOC-003: Stable requirement registry (§46); Required.
- UADS-CI-002: Schema and evidence validator (§48); Required.
- UADS-DOC-004: Persistent assignment queue (§52); Required.
