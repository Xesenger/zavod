# Scanner v2 - Evidence Cartographer (v1)

Status: **frozen MVP contract**
Created: 2026-04-24
Frozen: 2026-04-25
Scope: scanner / import evidence / project cartography / task scoping

This document is preserved as the Scanner v2 MVP design contract. Active
follow-up work now lives in `docs/plans/scanner-v2-followup-v1.md`.

---

## Purpose

Scanner v2 should not be a full AST brain.
It should be a cold evidence cartographer for the project.

The scanner does not explain what the project means. It builds a
verifiable structural map that humans, Importer, Lead, Worker, and QC
can rely on.

Core boundary:

```text
Scanner: what exists
Importer: what it may mean
```

The scanner may report:

- files, manifests, symbols, imports, exports, routes, entry point
  candidates, dependency edges, module candidates, noise zones, and
  uncertainty

The scanner must not report:

- unsupported project intent, subsystem purpose, architecture stories,
  or LLM-inferred meaning without concrete evidence

---

## Product Name

Scanner v2 is the **Evidence Cartographer**.

Its primary artifact is the **Structural Project Index**.

Formula:

```text
Scanner v2 does not know what the project means.
Scanner v2 knows what is provably connected in the project.
```

---

## Structural Project Index

The scanner should produce layered artifacts rather than one giant
opaque graph:

```text
scan/
  scanrun.json
  files.index.json
  manifests.index.json
  symbols.index.json
  edges.index.json
  entrypoints.index.json
  project_units.index.json
  modules.map.json
  runprofiles.index.json
  uncertainty.report.json
  scan.summary.md
```

Rules:

- JSON artifacts are system input.
- `scan.summary.md` is a human projection of the scan result.
- The summary is not project truth.
- Cache may accelerate scans, but cache is not truth.

---

## Index Layers

### 1. File Inventory

Record the cold filesystem base:

- path
- extension
- size
- hash
- modified time
- language guess
- source / test / docs / generated / vendor / ignored zone
- scan priority

Noise zones should be marked, not erased:

- `.git/`
- `bin/`
- `obj/`
- `node_modules/`
- `dist/`
- `build/`
- generated files
- vendored dependencies

### 2. Manifest / Build Index

Manifests are often stronger evidence than raw file presence because
they describe what is actually built, run, or packaged.

Initial manifest targets:

- .NET: `.sln`, `.csproj`, `Directory.Build.props`, `appsettings.json`
- JS/TS: `package.json`, `tsconfig.json`, Vite/Webpack config
- Rust: `Cargo.toml`, `Cargo.lock`, workspace members
- Python: `pyproject.toml`, `requirements.txt`, `setup.py`
- C/C++: `CMakeLists.txt`, `compile_commands.json`, `Makefile`
- Java: `pom.xml`, `build.gradle`

### 3. Shallow Symbol Index

Use shallow language parsers to extract declarations and references
without attempting full semantic analysis.

Collect:

- classes
- functions
- methods
- interfaces
- enums
- imports / using / requires
- exports
- route declarations
- DI registrations
- test names
- config keys

Do not attempt:

- complete control flow
- complete type-system modeling
- all generic constraints
- all runtime effects
- full macro expansion

### 4. Dependency Edge Index

Build edges from observed evidence:

- file A imports file B
- file A declares symbol X
- file A references symbol Y
- file A is test evidence for file/module B
- file A is configured by file C
- file A is an entry point candidate

#### Edge Resolution Levels

Edge kinds should distinguish evidence strength:

- `lexical_import` - import/include text was observed
- `resolved_import` - import/include was resolved to a known file
- `symbol_reference` - symbol reference was observed
- `manifest_dependency` - dependency came from a build/package manifest
- `runtime_config_reference` - runtime/config file references another
  file or command
- `test_target` - test file or command appears to target a file/module
- `doc_mention` - documentation mentions a path, symbol, command, or
  unit

Each edge should also carry resolution state:

- `lexical`
- `resolved`
- `ambiguous`
- `unresolved`

Example:

```json
{
  "from": "src/ui/LoginViewModel.cs",
  "to": "src/core/AuthService.cs",
  "edgeType": "uses_symbol",
  "edgeResolution": "resolved",
  "evidence": "constructor parameter AuthService",
  "confidence": "confirmed"
}
```

### 5. Entry Point Ranking

Entry point ranking is the first priority for Scanner v2 because weak
entry point evidence causes weak import summaries, weak capsules, and
poor Worker context.

Entry point candidates should be deterministic:

```text
EntryPointCandidate:
- path
- kind
- evidence
- score
- confidence
```

Example weights:

- `+50` referenced by build manifest
- `+40` contains `Main`, `Program`, app bootstrap, or runtime start
- `+30` referenced by startup script
- `+20` mentioned by README or docs
- `+15` located in a conventional path
- `-30` inside test folder
- `-50` inside generated or vendor zone

README/docs are evidence, not truth.

### 6. Module Boundary Map

The scanner should produce module candidates, not invented architecture.

```text
ModuleCandidate:
- label
- labelSource
- root paths
- files
- dominant symbols
- inbound edges
- outbound edges
- related tests
- evidence
- confidence
```

Labels may come from:

- folder names
- manifest targets
- dominant symbol terms
- repeated route or namespace prefixes

If a label is inferred from symbol terms rather than explicit folder or
manifest evidence, confidence should be lower.

### 7. Project Unit / Workspace Model

Large repositories often contain multiple project units. Scanner v2
must not collapse a workspace into one project narrative.

```text
ProjectUnitCandidate:
- id
- rootPath
- kindSignals
- manifests
- build targets
- entrypoints
- internal dependencies
- external dependencies
- evidence
- confidence
```

Examples:

```text
repo/
  apps/web/
  apps/desktop/
  packages/core/
  tools/importer/
```

Scanner may report that multiple units are visible. Importer may decide
whether they form one product, a workspace, or an ambiguous container.

### 8. Run/Test Profile Index

Scanner v2 should extract runnable profiles as evidence, not execute
them.

```text
RunProfileCandidate:
- id
- command
- kind: build | test | run | lint | format | dev
- workingDirectory
- source: package_script | cargo_manifest | csproj | sln | makefile | cmake | pyproject | docs
- evidence
- confidence
```

Initial sources:

- package scripts
- Cargo workspace/package commands
- .NET solution/project files
- Makefile targets
- CMake presets or common targets
- pyproject scripts
- README command mentions as low-confidence evidence

This index connects scanner evidence to future run/test/QC workflows
without giving the scanner authority to execute anything.

### 9. Task Scope Resolver

Task Scope Resolver v0 should answer:

```text
Which files are minimally relevant for this task?
```

Input:

- validated intent
- Structural Project Index
- known anchors
- allowed paths

Output:

```text
TaskScope:
- primary files
- related files
- likely relevant files
- explicitly excluded files
- suggested checks
- uncertainty
- evidence
```

This is a context-selection tool, not an executor. It helps Worker
receive bounded context instead of the entire project.

Guardrails:

- never hide high-uncertainty relevant files
- excluded files are soft exclusions unless enforced by task
  `allowedPaths`
- always explain why each primary file was selected
- always include uncertainty when evidence is weak
- cap scope size and explain truncation
- `Do not touch` is a scope recommendation, not a scanner-enforced ban
- real write restrictions must come from Lead/task policy/allowed paths

### 10. Drift / Rescan State

Scanner v2 should support incremental rescans:

- file hash cache
- changed-file reparse
- graph edge invalidation
- stale module reevaluation
- stale task-scope markers

Rule:

```text
cache = acceleration, not truth
```

### 11. Scan Performance Budgets

Scanner v2 must stay bounded on real repositories.

Budget inputs:

- max files per scan
- max file size for content parsing
- max lines per file
- max parse time per file
- max total scan time
- max artifact size
- binary handling policy

Budget outcomes:

- `skipped_due_to_size`
- `partial_scan`
- `timeout`
- `degraded_parser_mode`
- `binary_metadata_only`
- `generated_or_vendor_suppressed`

Skipped or degraded evidence must appear in uncertainty reports.

---

## Language Scanner Model

Use language plugins with bounded responsibility:

```text
ILanguageScanner
- CanScan(file)
- ExtractSymbols(file)
- ExtractEdges(file)
- ExtractEntrypoints(file)
- ExtractTests(file)
```

Parser preference:

1. Official compiler or language parser when available.
2. Tree-sitter or lexical parser when suitable.
3. Regex-level extraction with low confidence when no better option is
   available.

Scanner v2 should start with 2-3 high-value language paths and expand
only when field evidence justifies it.

---

## Confidence Model

Every scanner statement should carry evidence-based confidence:

- `confirmed` - directly observed in code, manifest, or structured data
- `likely` - supported by multiple weak signals or convention
- `unknown` - insufficient evidence
- `conflict` - evidence sources disagree

Examples:

- `confirmed`: file A imports file B.
- `likely`: folder `src/runtime` contains a runtime subsystem.
- `unknown`: multiple possible startup paths exist.
- `conflict`: README describes a CLI app while package scripts start a
  web server.

The scanner must be able to report conflict. A conflicted map is better
than a false clean story.

---

## Evidence Statements

The core unit of scanner output is an `EvidenceStatement`.

```json
{
  "id": "EV-9d4b7d1a",
  "displayId": "EV-00142",
  "scanRunId": "SCAN-2026-04-24T12-00-00Z",
  "subject": "src/App/Program.cs",
  "predicate": "declares_entrypoint",
  "object": "Main",
  "source": "csharp_symbol_parser",
  "extractorVersion": "0.1.0",
  "basedOnFileHash": "sha256:...",
  "confidence": "confirmed",
  "location": {
    "file": "src/App/Program.cs",
    "line": 12
  }
}
```

Evidence statements can feed:

- Structural Project Index
- project map UI
- import preview docs
- task scope packs
- agent anchors
- uncertainty reports

### Stable Evidence IDs

Evidence IDs should be stable across scans when the observed fact is the
same.

Rule:

```text
id = EV-{hash(subject + predicate + object + location + source)}
```

A human-friendly sequential `displayId` may exist for reports, but it
must not be used as the durable reference.

Stable IDs allow:

- comparing scan A and scan B
- detecting disappeared evidence
- preserving task-scope references
- preserving uncertainty-report references

### ScanRun / Provenance

Every scan should produce a `ScanRun` record:

```json
{
  "scanRunId": "SCAN-2026-04-24T12-00-00Z",
  "repoRootHash": "sha256:...",
  "scannerVersion": "2.0.0",
  "extractorVersions": {
    "csharp": "0.1.0",
    "typescript": "0.1.0"
  },
  "startedAt": "2026-04-24T12:00:00Z",
  "completedAt": "2026-04-24T12:00:04Z",
  "mode": "full"
}
```

Evidence statements should include:

- `scanRunId`
- `basedOnFileHash`
- `extractor`
- `extractorVersion`

This makes drift and stale evidence traceable instead of approximate.

### Predicate Registry

Predicates must come from a registry, not free-form strings.

Initial registry:

- `declares_symbol`
- `references_symbol`
- `imports_module`
- `exports_symbol`
- `declares_entrypoint`
- `defines_build_target`
- `declares_route`
- `registers_dependency`
- `declares_test`
- `configures_file`
- `mentions_in_docs`
- `classifies_noise_zone`

Each predicate should define:

- allowed subject type
- allowed object type
- confidence rules
- valid sources/extractors

This prevents drift like `declares_entry_point`,
`has_entrypoint`, `entry_point_declared`, and `contains_main` all
meaning the same thing.

---

## Deterministic Scoring

Scores must be deterministic and explainable.

Do not use LLM scoring inside the scanner.

Good:

```text
entryPointScore =
  manifest_reference
  + startup_symbol
  + conventional_filename
  + incoming_references
  - generated_vendor_penalty
```

Bad:

```text
LLM importance score: 0.82
```

---

## Scanner Config / User-Approved Overrides

Scanner v2 should support explicit scan configuration without treating
configuration as project truth.

Location:

```text
.zavod/scanner/config.json
```

Example:

```json
{
  "primaryUnits": ["src/Zavod.App"],
  "ignoreZones": ["samples/old"],
  "vendorZones": ["third_party/"],
  "generatedPatterns": ["*.g.cs"],
  "docPriority": {
    "README.md": "high",
    "docs/archive/**": "low"
  }
}
```

Rules:

- overrides affect scanning priority and classification
- overrides do not create canonical project truth
- override effects must be recorded as evidence/provenance
- unsafe broad ignores should require user review

---

## Sensitive File / Secret Handling

Scanner v2 may encounter secrets. It must protect content by default.

Sensitive candidates:

- `.env`
- `*.pem`
- `*.key`
- `secrets.json`
- credentials files
- token-bearing configs
- private deployment configs

Policy:

- detect sensitive files deterministically
- mark them as sensitive
- do not include content in evidence snippets or context packs
- include metadata/hash only when useful
- warn the user when sensitive files are present
- allow explicit user-approved handling only through governed paths

The scanner must not leak secrets to Importer, Worker, QC, prompts, or
preview documents.

---

## User Surface

The user should not see a thousand-node graph first.

The user should see a compact scan result:

```text
Detected:
- 3 likely entry points
- 7 module candidates
- 2 build targets
- 1 uncertainty area
- 4 generated/noise zones

Main confidence:
- Project kind signals:
  - .NET desktop signals: likely
  - CLI entry signals: confirmed
  - Web frontend signals: unknown/conflict
- Runtime entry signals: confirmed
- Documentation consistency signals: partial
```

Detailed view:

- modules
- project units
- entry points
- run/test profiles
- uncertainty
- evidence
- generated/noise zones
- stale scan markers

---

## Worker Surface

Worker should receive a Task Scope Pack:

```text
Primary:
- X
- Y

Related:
- A
- B

Do not touch:
- C
- D

Evidence:
- X references Y
- Y writes preview docs
- A displays status

Uncertainty:
- reject flow may also touch Layer D journal
```

The scanner gives boundaries. The Worker still has to reason and verify.

---

## MVP

Scanner v2 MVP:

1. File inventory + noise zones
2. Manifest parser + project-unit candidates
3. Shallow symbol parser for 2-3 primary languages
4. Import/reference edges
5. Entry point ranking
6. Module candidates from folders, symbol terms, manifests, and edges
7. Task Scope Resolver v0
8. Run/Test Profile Index v0
9. Uncertainty report
10. ScanRun provenance

First alpha pressure points:

- Cargo workspace `default-members`
- `bin/*` runtime entrypoints
- root README product identity as evidence, not truth
- package descriptions as evidence
- `.github/` as low-priority workflow/support zone, not a product module

### Real Repository Smoke Discipline

Real repositories may be used as smoke targets, but not as behavioral
oracles.

Rules:

- real repo scans may reveal missing general evidence rules
- real repo names, product names, and path quirks must not become scanner
  conditions
- behavior must be specified through synthetic, general-case fixtures before
  it is treated as a scanner contract
- smoke results may confirm that a general rule still works on real data
- smoke results must not force deterministic scores to match one repository's
  preferred story

Allowed:

- "manifest-backed top-level `bin/` can be a workspace unit"
- "automation/support folders should not become primary source roots"
- "Cargo `default-members` boosts matching entry candidates"

Not allowed:

- "`spiceai` uses `bin/spiced`, so prefer that path"
- "`svix-webhooks` root README says X, so assume this repo type"
- path/name special-casing for ZAVOD's current test repositories

The scanner must improve by extracting reusable evidence rules, not by
memorizing the repositories used to test it.

---

## Non-Goals

Do not build:

- full AST over the entire project
- LLM interpretation inside the scanner
- unsupported project-purpose claims
- project-type claims without Importer interpretation
- hidden heuristics without explainable evidence
- confidence labels without reasons
- one giant graph without human projections
- README-as-truth behavior
- project auto-fixes during scan
- secret-content prompt injection
- execution or auto-apply behavior during scan
- repository-specific heuristics for current smoke targets

---

## Relationship To Existing Layers

Project Cartography:

- Scanner v2 is the concrete path toward project cartography.
- The Structural Project Index is the machine-readable cartography
  artifact.

Importer:

- Importer may interpret scanner evidence into preview documents.
- Importer must preserve uncertainty and conflicts from the scan.

Context Builder / Work Packet:

- Task Scope Resolver should eventually feed bounded context packs.
- Scanner output should reduce context accumulation and improve context
  selection.

External Change Awareness:

- Drift and rescan state should mark stale evidence before agents rely
  on it.

Safe Execution / Unattended Work:

- Scanner output may guide sandboxed work.
- Scanner output must not authorize apply.
