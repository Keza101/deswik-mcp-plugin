# Underground Mining Functionality Roadmap

## Purpose

This backlog extends the existing Deswik CAD bridge into an underground drill-and-blast design, QA, and reconciliation platform. It is intended to be implemented incrementally.

The engineering concepts are informed by the current repository, general underground mining practice, and the public [D&B Optimizer](https://ug-d-b-guidelines.vercel.app/) interface. Values shown by that site must be treated as configurable reference rules, not universal design standards. Every production rule requires validation against the site's geotechnical model, explosive products, equipment limits, statutory requirements, and approved mine standards.

## Delivery decision and status — 2026-09-29

The full underground roadmap below is the build scope **before** the final
three-node pilot. The user approved the tracked synthetic Process Map donor for
that later pilot. The first active milestone is a versioned profile model and
editor, not production ring generation. A synthetic profile is only for
disposable test drawings; changing its numbers never makes it an approved mine
standard.

| Workstream | Current delivery state | Next gate |
|---|---|---|
| Platform reliability (`FND-01`–`FND-08`) | Partly built: stdio MCP, fail-closed modes, guarded writes, async jobs, and tests exist. Full schema/version coverage, audit, and compatibility cleanup remain. | Complete the missing foundation items and live acceptance. |
| Design context and profiles (`CTX-01`–`CTX-06`) | Profile, snapshot, role/metric, raw polyface, expanded polyface/polyline, and mixed simple-figure gates are accepted; native Line is an explicit automated-only gap. Bounded Points collection paging is built with a broad launcher suite pending. | Run the reusable Points collection acceptance launcher, then extract remaining native types. |
| Drill-and-blast design (`DBD-01`–`DBD-09`) | Planned. Existing native-hole write is a guarded primitive, not a design calculator. | Build pure deterministic calculators against explicit profiles and test boundaries. |
| Automated design QA (`QA-01`–`QA-04`) | Planned. | Gate every generated proposal with measured rules and issue reports. |
| As-drilled and as-charged (`ACT-01`–`ACT-05`) | Planned. | Add approved import mappings and reconciliation before readiness claims. |
| Performance and improvement (`PER-01`–`PER-05`) | Planned. | Version observed outcomes and calibration without overwriting approved profiles. |
| Operational integration (`OPS-01`–`OPS-05`) | Planned; Scheduler/LHS services are not verified live adapters. | Capture real provider contracts and pass live integration gates. |
| AI workflows (`AI-01`–`AI-04`) | Planned. | Expose only deterministic, audited and approved actions. |
| Final three-node Process Map pilot | Deferred until the preceding workstreams are accepted. | Run Inspect stope → Preview rings → Approve write on a saved disposable drawing. |

## Delivery gates before, during, and after the pilot

The feature phases below describe **what** is being built. These delivery gates
describe **what evidence is required before the work may advance**. Gates are
cumulative: passing a later test does not waive an earlier gate, and a failed or
changed dependency returns the affected work to its owning gate.

**Current position:** Gate `G1`, design context and geometry extraction. The
profile, selection, role/metric, raw polyface, and expanded polyface/polyline
live gates and the mixed simple-figure suite are accepted. Native Line remains
an explicit automated-only gap until a representative entity is available. The
next sub-gate is bounded native Points collection paging. Platform items still
open in `G0` continue in parallel and must close before pilot packaging.

`Foundation → context → approved standards → calculators → QA/write paths → actuals/integrations → AI boundary → pilot readiness → pilot → evidence review → controlled production → scale`

| Gate | Required outcome and evidence | Current state | What it unlocks |
|---|---|---|---|
| `G0` Platform safety baseline | Versioned contracts; explicit live/demo/disconnected state; bounded jobs; owned preview, default-No approval, token-bound commit, exact-handle rollback; audit/provenance; supported-build compatibility; automated and live-CAD acceptance. | **In progress.** Core bridge safety is accepted; schema coverage, audit, compatibility cleanup, and remaining live acceptance are open. | A stable surface on which domain functions can be accepted. |
| `G1` Complete design context | Bounded extraction for every supported native figure; drawing identity, source handles, units, coordinates, conventions, attributes, and selection state; refusal of unsupported or ambiguous input; no drawing mutation. | **In progress.** Mixed simple-figure live suite is next. | Trustworthy inputs for engineering calculations. |
| `G2` Approved standards and typed domain | Versioned site profile and rule sources; explicit ownership and approval state; units and convention validation; synthetic profiles permanently marked test-only; mining-engineer approval for any production profile. | **Partly built.** Synthetic schema/editor exist; production standards are not approved. | Production-eligible calculator inputs. Synthetic profiles unlock test work only. |
| `G3` Deterministic design calculators | Rise, box-hole, ring, deviation, charge, timing, and quantity calculations implemented as pure libraries with unit, boundary, invalid-input, golden, and repeatability tests. Every result records profile version, assumptions, warnings, and provenance. | **Planned.** | Candidate designs that can be rendered and checked without trusting CAD to calculate them. |
| `G4` Design QA and guarded CAD writes | Rule-by-rule QA with measured evidence; issue locations and severity; CAD preview isolated from production geometry; explicit manifest; approval bound to drawing, sources, parameters, and preview; exact created handles and verified rollback. | **Planned.** Guarded write primitives exist but domain-wide QA/write coverage does not. | Safe end-to-end design workflows on disposable drawings. |
| `G5` Actuals, performance, and operational integration | Approved import mappings; as-drilled/as-charged reconciliation; blast outcome and calibration records; verified Scheduler/LHS/provider contracts; truthful unavailable states; controlled reports and exports. | **Planned.** Existing Scheduler/LHS examples are not live adapters. | Full roadmap evidence rather than design-only evidence. |
| `G6` AI and orchestration boundary | AI may structure requests and explain deterministic results, but cannot invent standards, bypass QA, infer safety clearance, mint approval, or write directly. All exposed actions are typed, audited, and already accepted below the AI layer. | **Planned.** | Safe assembly of the final Process Map pilot. |
| `G7` Pilot release readiness | `G0`–`G6` closed; immutable pilot package and hashes; supported Deswik build recorded; disposable saved drawing and known fixtures; operator guide; recovery/rollback runbook; named operator, engineering reviewer, and evidence owner; rehearsed launcher and clean automated baseline. | **Deferred.** | Authorization to execute the three-node pilot. |
| `G8` Three-node pilot execution | Run **Inspect stope → Preview rings → Approve write** in order. Capture exact source and created handles, drawing identity, profile/calculator/package versions, warnings, manifest, approval actor/time, rollback evidence, and refusal-path results. | **Deferred.** Pilot nodes are not built. | A pilot evidence pack; it does **not** authorize production use. |
| `G9` Pilot evidence review | Reconcile the run record to the drawing; classify every defect and exception; reproduce or close failures; confirm no unowned mutation; obtain operator, engineering, and software-owner sign-off. Any critical safety or data-integrity defect sends work back to its owning gate. | **Post-pilot.** | A production release candidate, subject to operational readiness. |
| `G10` Controlled production readiness | Approved production profiles and provider mappings; deployment package and integrity hashes; supported-build matrix; access controls; audit retention; monitoring; backup/recovery; rollback drill; training and support runbook; explicit scope, owner, and stop criteria. | **Post-pilot.** | A limited, reversible production rollout. |
| `G11` Limited production rollout | Start with one approved site/build/workflow and named operators. Observe read/preview/commit/refusal/rollback telemetry, review every exception, and halt on stop criteria. No automatic expansion from pilot success. | **Post-pilot.** | Evidence for controlled expansion. |
| `G12` Scale and continuous governance | Approve each additional site, Deswik build, profile, provider, and write workflow separately. Re-run automated and live gates after relevant changes; version and retain evidence; periodically rehearse recovery and revoke obsolete packages/profiles. | **Post-pilot.** | Sustained supported operation. |

### How to interpret a gate

- **Built** means code and automated checks exist; it is not live acceptance.
- **Live accepted** means the user ran the current root launcher against the
  supported Deswik build and the archived checks matched the real drawing.
- **Engineering approved** means a qualified reviewer accepted the mine rules,
  assumptions, limits, and production profile; a successful synthetic test does
  not supply that approval.
- **Pilot passed** means one controlled pilot evidence pack passed `G8` and
  `G9`; it is not a general production release.
- **Production ready** means `G10` has a named scope, owners, stop conditions,
  monitoring, audit, recovery, and sign-off. Expansion still requires `G12`.

### Evidence rules at every gate

1. Run the broadest meaningful automated suite first, then live CAD checks.
2. Use the single root `Run-Deswik-Tests.cmd`; rename/update that launcher for
   the active suite instead of adding another root launcher.
3. Test valid, boundary, refusal, repeatability, stale-state, wrong-drawing,
   replay, cancellation, timeout, rollback, and no-mutation cases wherever the
   milestone exposes them. Stop only when a real boundary prevents more useful
   coverage, not after an arbitrary number such as four.
4. Archive test intent, environment, input fixtures, expected results, observed
   results, discrepancies, and disposition in the relevant phase document.
5. A failed check blocks the gate. Fix and repeat the affected checks plus
   regression coverage; do not edit the evidence into a pass.

### Minimum pilot acceptance matrix

| Path | Evidence required |
|---|---|
| Inspect success | Exact drawing identity and selected source handles; resolved units/conventions/profile; deterministic input snapshot; no mutation. |
| Inspect refusal | Empty, mixed, unsupported, ambiguous, stale, or wrong-drawing selections fail clearly and expose no invented design state. |
| Preview success | Owned temporary geometry only; calculation and QA provenance; complete warnings and manifest; repeatable output; production geometry unchanged. |
| Preview refusal/recovery | Invalid profile, failed QA, cancellation, timeout, and bridge interruption leave no unowned residue and recover cleanly. |
| Approval success | Default-No modal; explicit operator action; single-use token bound to the exact drawing, inputs, parameters, preview, and expiry; exact created handles recorded. |
| Approval refusal | Missing/stale/mismatched preview, changed drawing or selection, wrong actor/session, expired token, and replay create nothing. |
| Rollback | Only the recorded created handles are removed or restored; unrelated entities, attributes, selection, and drawing state remain unchanged. |
| Audit closeout | Run record links source → calculation → QA → preview → approval → created handles → rollback/disposition with versions, times, and responsible people. |

## Current Foundation

| Area | Current state |
|---|---|
| Live CAD connection | Working local bridge on `127.0.0.1:9595` |
| CAD reads | Document, layers, entities, attributes, selection, polyface information, paged raw polyface and polyline geometry, slices, and drill-hole details |
| CAD writes | Native `UGDrillHole` preview, human-token commit, and exact-handle rollback; former direct writers are fenced |
| UGDB conventions | Ring, pivot, hole ID, diameter, length, charge fields, and `RINGDESIGN` layer convention supported |
| Scheduler | Service code exists, but the CAD add-in deliberately does not register Scheduler capabilities |
| LHS and generic CAD services | Mostly sample or placeholder responses |
| Standalone bridge | Routes live capabilities and fails closed; demo needs an explicit request |
| MCP protocol | Local stdio MCP adapter with 25 typed tools; bridge TCP remains internal |
| Long-running work | Process-local jobs with progress, cancellation, and deadline handling |
| Write safety | Owned preview, default-No Deswik approval, single-use tokens, and exact-handle rollback |
| Automated tests | Python suites and a C# bridge/MCP harness; live CAD gates remain manual |

## Design Principles

- Keep engineering calculations deterministic and testable outside Deswik.
- Keep CAD access in a thin adapter running on Deswik's UI thread.
- Use `inspect -> calculate -> validate -> preview -> commit` for every write workflow.
- Require explicit confirmation before creating, changing, or deleting production geometry.
- Store units, coordinate system, azimuth convention, dip convention, and tolerance with every result.
- Version every mine-standard profile and record which profile produced each design.
- Return assumptions, warnings, failed checks, and calculation provenance with every result.
- Never silently substitute demo data when the user expects a live Deswik result.

## Phase 0: Platform Reliability

### `FND-01` Native MCP server

Build a local MCP server that exposes the bridge actions as typed tools. Start with stdio for local clients; add authenticated Streamable HTTP only if remote access is approved.

Suggested tools:

- `deswik_get_connection_status`
- `deswik_get_document`
- `deswik_get_layers`
- `deswik_get_selection`
- `deswik_get_entities`
- `deswik_get_entity_details`

### `FND-02` Fail-closed live/demo modes

Expose `live`, `demo`, and `disconnected` explicitly. A live action must fail when its capability provider is unavailable instead of returning plausible demo data.

### `FND-03` Versioned schemas

Define JSON Schema contracts for every request and response. Add protocol, add-in, bridge, Deswik build, and mine-profile versions to health responses.

### `FND-04` Asynchronous jobs

Add job start, progress, cancellation, completion, and error actions for work that can exceed 15 seconds.

Suggested tools:

- `deswik_start_job`
- `deswik_get_job`
- `deswik_cancel_job`

### `FND-05` Preview, commit, and rollback

Write proposed geometry to a temporary preview layer, return a change manifest, and commit only after confirmation. Capture enough state to remove newly created entities or restore modified attributes.

### `FND-06` Audit and provenance

Record request ID, operator, source entities, input parameters, standards profile, warnings, created handles, timestamps, and final disposition. Avoid recording proprietary geometry unless explicitly enabled.

### `FND-07` Compatibility and regression tests

Add unit tests for protocol and calculations, golden JSON fixtures, bridge integration tests, and a manual live-CAD acceptance checklist for each supported Deswik build.

### `FND-08` Build compatibility cleanup

Resolve the current .NET 8 versus Deswik dependency-version warnings, document the supported Deswik build range, and fail builds when required Deswik references are missing.

## Phase 1: Mining Design Context

### `CTX-01` Selected-design context

Convert the current CAD selection into a typed context containing drives, brows, stope solids, voids, rings, holes, survey strings, coordinate system, and relevant attributes.

The first implemented slice is `get_ug_selection_context`: one read-only
UI-thread snapshot of saved drawing identity, selected handles, CAD types,
layers, and available bounds. It does not infer a stope or brow from a layer
name. Every semantic role and the coordinate system/units remain unverified,
so `readyForDesign` is false. The user accepted all four live snapshot checks
on 2026-09-29. Full CTX-01 role mapping and geometry remain planned.

An opt-in follow-up accepts exact selected handles mapped to temporary
operator-supplied role labels. It never infers a role from the CAD type or
layer, never persists the label, and never marks the context design-ready.
Decimal handles and `0x`-prefixed hexadecimal handles are supported; extra,
duplicate, and invalid role assignments fail closed. The user accepted all
four live follow-up checks on 2026-09-29.

### `CTX-02` Complete geometry extraction

Return full geometry for supported entity types rather than only handles and bounding boxes. Include polyline vertices, polyface topology, text, points, circles, lines, arcs, drill holes, and user attributes.

The first accepted opt-in geometry slice reads existing verified polyface API fields:
center of gravity, volume, and vertex count. Unreadable metrics produce an
explicit warning. Vertices and topology are not yet extracted, so CTX-02
remains incomplete. A subsequent `get_cad_polyface_geometry` slice reads
bounded pages of `VertexList` and raw `GetFaceIndexes` values from the
installed 2025.2 DLL. Its original and expanded live suites passed. Index sign
and edge-visibility conventions have not been normalized.
`get_cad_polyline_geometry` reads bounded vertex pages and the native closed
flag; its combined launcher suite passed on 2026-09-29. A strict
`get_cad_figure_geometry` reads native fields and user attributes for Line,
Circle, Arc, Point, Text, and MText. Its broad live suite passed, except native
Line could not be created in the installed UI and remains an explicit
automated-only gap. `get_cad_points_geometry` now reads bounded pages and raw
display metadata from native Points collections, pending live acceptance. None
of these reads is design-ready. Remaining figure types remain planned.

### `CTX-03` Coordinate and angle conventions

Centralize world/local transforms, ring-plane coordinates, azimuth conventions, and dip conversions. The code already handles the important difference between `BlastHole` positive-down dip and `UGDrillHole` positive-up dip; make this a tested shared service.

### `CTX-04` Typed drill-design model

Introduce domain records for:

- Mine, area, level, stope, drive, brow, slot, and void
- Drill setup, rig envelope, ring, pivot, hole, and survey station
- Explosive product, primer, deck, stemming, delay, and firing group
- Design, as-drilled, as-charged, and as-fired states

### `CTX-05` Mine-standard profiles

Store approved ranges and lookup tables in versioned JSON or YAML profiles. Profiles should contain equipment limits, hole diameters, burden/spacing rules, minimum mining width, deviation assumptions, charge products, timing rules, and QA tolerances.

The first schema (`schemaVersion: 1`) is a **synthetic test-only** JSON
profile covering ring geometry and deviation assumptions. The Deswik dock
panel opens an editor, permits parameter changes, and saves each copy as a new
JSON file in the current user's `Documents\DeswikMcp\Profiles` folder. The
embedded starter is never overwritten. The schema and editor reject a claim
that a synthetic profile is approved. Later schema versions must add site
review metadata and the remaining charge, rig, timing, and QA fields before
production calculations can use a profile.

### `CTX-06` Attribute read/write mapping

Add typed attribute updates with validation, allowed-value checks, dry-run output, and before/after values. Support mapping between CAD attributes, UGDB fields, and Scheduler task fields.

## Phase 2: Drill-and-Blast Design

### `DBD-01` Rise and slot optimizer

Support preset and custom relief-hole patterns, equivalent relief diameter, total void area, void ratio, shot-to-relief spacing, and geometric validation.

The reference interface demonstrates:

- Equivalent diameter: `De = D * sqrt(n)`
- Reference expansion limit: `Smax = 1.5 * De`
- Pattern presets using 152 mm or 204 mm reamers
- Custom reamer and shot-hole placement

Implement these as profile-controlled checks with source and assumption metadata.

Suggested tools:

- `calculate_rise_pattern`
- `validate_rise_pattern`
- `preview_rise_pattern`
- `commit_rise_pattern`

### `DBD-02` Box-hole designer

Calculate face burden ratio, validate breakout geometry, design diamond or custom patterns, and check whether each hole fires into a sufficient free face.

The reference interface uses `FBR = free-face width / burden` and highlights a nominal range of `1.4-2.1`. Keep that range configurable.

### `DBD-03` Production-ring generator

Generate rings from a drive pivot and stope slices. Support upholes, downholes, fans, parallel holes, standing-up rings, end rings, cable rings, slot rings, and wall-control holes.

Inputs should include:

- Selected stope solid and drive geometry
- Ring plane, advance, and burden
- Hole diameter and rig configuration
- Collar spacing and exclusion zones
- Toe-spacing limits and minimum mining width
- Brow offset, subdrill, breakthrough, and hole-length limits

Outputs should include native `UGDrillHole` entities, labels, ring metadata, and a complete validation report.

### `DBD-04` Burden and spacing recommender

Provide lookup-table and calculation-based recommendations by stope width, hole diameter, rock domain, explosive, and ring type. Show the source rule, confidence, and nearest approved alternatives. Do not silently select a final pattern.

### `DBD-05` Rig and drillability envelope

Validate pivot position, feed length, tube/rod system, boom reach, articulation, collar access, minimum hole separation, maximum accurate length, and collision with the drive or installed services.

### `DBD-06` Deviation compensation

Model angular and collar-position uncertainty at the toe, show additional effective burden/spacing, and propose constrained collar or angle corrections.

The reference interface illustrates approximately 480 mm additional burden for 1 degree over 30 m and an example 28 m accuracy limit for 89 mm tubes. Both must remain equipment-profile inputs.

### `DBD-07` Charge-plan calculator

Calculate linear charge concentration, total charge mass, charge length, stemming, primers, decks, air decks, decoupling, powder factor, energy factor, and per-delay mass.

Suggested tools:

- `calculate_charge_plan`
- `validate_charge_plan`
- `preview_charge_plan`
- `commit_charge_plan`

### `DBD-08` Timing and firing sequence

Design delay groups against the evolving void, detect holes that fire without relief, and visualize the firing sequence. Include electronic and non-electric detonator constraints, duplicate delays, out-of-order firing, and maximum charge per delay.

The reference interface demonstrates configurable starting points of `25-35 ms/m` for longhole rise charges, `8-14 ms/m` for box holes, and 50 ms intervals for stripping/reaming holes. These are references only and require site approval.

### `DBD-09` Design comparison

Compare two ring, slot, or charge designs by drilled metres, hole count, charge mass, expected tonnes, burden/spacing distribution, risk checks, estimated drilling time, and estimated cost.

## Phase 3: Automated Design QA

### `QA-01` Hole geometry checks

- Zero or negative length
- Hole outside the stope or target volume
- Insufficient or excessive breakthrough
- Collar outside the drive or brow envelope
- Hole-to-hole collision or minimum separation breach
- Excessive length, dip, azimuth, curvature, or rig articulation
- Missing IDs, duplicate IDs, invalid ring linkage, or invalid layer path

### `QA-02` Breakage and void checks

- Burden and spacing against the selected profile
- Effective burden after deviation
- Shot-to-relief distance and available void
- Slot continuity and expected opening sequence
- Unsupported toes, shadow zones, and isolated wedges
- Hanging-wall and footwall damage exposure

### `QA-03` Charge and initiation checks

- Uncharged designed holes and charged excluded holes
- Charge above collar, beyond toe, or across a protected interval
- Missing primer, unsuitable primer position, or excessive deck length
- Excessive charge concentration or maximum instantaneous charge
- Duplicate, reversed, or non-progressive delay assignment
- Firing into an unavailable void

### `QA-04` Design QA report

Create a structured report containing pass/fail checks, severity, entity handles, coordinates, rule source, measured value, allowed range, and suggested correction. Draw issue markers on a temporary CAD layer.

Suggested tools:

- `validate_drill_design`
- `validate_charge_design`
- `validate_timing_design`
- `preview_qa_issues`

## Phase 4: As-Drilled and As-Charged Workflows

### `ACT-01` Survey and MWD import

Import collar and downhole survey data from approved CSV or database mappings. Preserve raw measurements, source file hash, instrument, timestamp, and coordinate transform.

### `ACT-02` Design versus as-drilled reconciliation

Calculate collar offset, toe offset, angular deviation, depth variance, effective burden/spacing, breakthrough, and proximity to neighbouring holes. Display vectors and heat maps in CAD.

### `ACT-03` Redrill and compensation recommendations

Identify blocked, short, deviated, or out-of-tolerance holes and generate options such as accept, redrill, adjust charge, alter delay, or redesign neighbouring holes. Recommendations must require engineering approval.

### `ACT-04` Charging reconciliation

Track actual product, mass, charge length, stemming, decks, primers, delays, loading status, sleep time, and exceptions by hole. Compare actuals with the approved charge plan.

### `ACT-05` Blast readiness gate

Produce a signed checklist covering design approval, drilling QA, hole condition, charge completion, timing validation, exclusion status, void availability, and outstanding exceptions.

## Phase 5: Blast Performance and Continuous Improvement

### `PER-01` CMS or scan reconciliation

Compare post-blast scans with the design solid and calculate overbreak, underbreak, dilution, recovery, remaining toes, brow loss, and spatial variance by ring and geological domain.

### `PER-02` Fragmentation and bogging performance

Store fragmentation observations, hang-ups, oversize, secondary breakage, drawpoint availability, bogging rate, and remote bogging performance against the originating design.

### `PER-03` Drill-and-blast KPI model

Track drilled metres, redrill, metres per shift, charge mass, powder factor, holes per ring, tonnes per ring, dilution, recovery, overbreak, underbreak, cost per tonne, and schedule variance.

### `PER-04` Parameter calibration

Fit site-specific burden, spacing, deviation, charge, and timing recommendations to reconciled outcomes. Preserve sample size and uncertainty; never overwrite approved profiles automatically.

### `PER-05` Repeatable design templates

Create approved templates by mining method, stope geometry, rig, diameter, rock domain, explosive product, and slot type. Templates should generate a preview plus a deviation-from-standard report.

## Phase 6: Operational Integration

### `OPS-01` Scheduler linkage

Link designs and QA states to drill, charge, blast, bog, backfill, and rehabilitation tasks. Replace the current placeholder block-status result with real schedule queries.

### `OPS-02` LHS and material-flow linkage

Replace sample LHS data with live route, dump, stockpile, capacity, and status queries. Connect blasted tonnes and material type to haulage destinations.

### `OPS-03` Ventilation and re-entry workflow

Track blast time, clearance, ventilation status, gas monitoring, re-entry approval, and affected headings. This should integrate with approved operational systems rather than infer safety clearance.

### `OPS-04` Shift handover and exception board

Summarize drilling progress, blocked holes, redrills, charging exceptions, blast readiness, post-blast issues, and actions requiring engineering review.

### `OPS-05` Controlled reporting and export

Generate ring sheets, drill instructions, charge sheets, timing diagrams, QA reports, reconciliation reports, and machine-import files from versioned data.

## Phase 7: AI-Assisted Workflows

### `AI-01` Explain a design

Let a user ask why a hole, burden, delay, or charge was selected. The answer must cite deterministic calculation outputs and the active mine-standard profile.

### `AI-02` Diagnose exceptions

Summarize QA failures, likely causes, affected holes, and available corrective actions without automatically changing the design.

### `AI-03` Guided design creation

Convert a natural-language request into structured calculator inputs, surface missing information, generate a preview, and require confirmation before committing geometry.

### `AI-04` Query operational state

Answer questions across CAD, UGDB, Scheduler, LHS, and reconciliation data while distinguishing live values, cached values, and unavailable systems.

## Recommended Build Order

1. Implement the native MCP server and explicit live/demo health state.
2. Add versioned schemas, validation, audit records, and asynchronous jobs.
3. Add preview/commit/rollback for every CAD write.
4. Build the typed design context and mine-standard profile model.
5. Implement rise, box-hole, ring, deviation, charge, and timing calculators as pure tested libraries.
6. Connect calculators to CAD preview layers and native UGDB entity creation.
7. Add automated design QA before adding more generation features.
8. Add as-drilled and as-charged reconciliation.
9. Add blast-performance feedback and operational integrations.
10. Add AI assistance only over the deterministic, audited tool surface.
11. Freeze a pilot release candidate only after gates `G0`–`G6` close; record
    package hashes, supported build, owners, fixtures, and recovery procedure.
12. Execute the three-node pilot and its success, refusal, stale-state,
    interruption, replay, rollback, and no-mutation cases under `G8`.
13. Review and sign the evidence pack under `G9`; unresolved critical safety or
    data-integrity findings return to the owning feature gate.
14. Prepare and approve a tightly scoped production release under `G10`, then
    perform the monitored and reversible limited rollout in `G11`.
15. Expand by site, Deswik build, profile, provider, and workflow only through
    the separate acceptance and ongoing change-control gate `G12`.

## Definition of Done for Each Feature

- Typed input and output schema with units and coordinate conventions
- Mine-standard profile and rule-source version recorded
- Deterministic unit tests, boundary tests, and invalid-input tests
- Golden protocol fixture and bridge integration test
- Preview output and explicit commit path for writes
- Clear warnings and no silent fallback to demo data
- Audit entry linking source entities to created or modified handles
- Live Deswik.CAD acceptance test on each supported build
- Mining engineer review before production use
- One reusable `Run-Deswik-Tests.cmd` in the repository root; update it for the
  current milestone instead of adding launchers. Archive each test intent and
  observed result in its phase write-up. Exercise all meaningful valid,
  boundary, refusal, repeatability, and no-mutation cases available at that
  milestone; four checks are not a target or a cap.
- Explicit gate disposition: built, live accepted, engineering approved,
  pilot passed, limited-production approved, or scaled. These states are never
  interchangeable.
- Named evidence owner, approver, supported Deswik build, package/profile
  versions, open defects, rollback result, and next allowed scope.
