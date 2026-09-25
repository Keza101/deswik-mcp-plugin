# Underground Mining Functionality Roadmap

## Purpose

This backlog extends the existing Deswik CAD bridge into an underground drill-and-blast design, QA, and reconciliation platform. It is intended to be implemented incrementally.

The engineering concepts are informed by the current repository, general underground mining practice, and the public [D&B Optimizer](https://ug-d-b-guidelines.vercel.app/) interface. Values shown by that site must be treated as configurable reference rules, not universal design standards. Every production rule requires validation against the site's geotechnical model, explosive products, equipment limits, statutory requirements, and approved mine standards.

## Current Foundation

| Area | Current state |
|---|---|
| Live CAD connection | Working local bridge on `127.0.0.1:9595` |
| CAD reads | Document, layers, entities, attributes, selection, polyface information, slices, and drill-hole details |
| CAD writes | Layers, text, polylines, `BlastHole`, and native `UGDrillHole` entities |
| UGDB conventions | Ring, pivot, hole ID, diameter, length, charge fields, and `RINGDESIGN` layer convention supported |
| Scheduler | Service code exists, but the CAD add-in deliberately does not register Scheduler capabilities |
| LHS and generic CAD services | Mostly sample or placeholder responses |
| Standalone bridge | Routes registered capabilities; otherwise falls back to demo responses |
| MCP protocol | Not implemented yet; the current protocol is custom newline-delimited JSON over TCP |
| Long-running work | Limited by a 15-second bridge timeout |
| Write safety | No preview/commit transaction, rollback, or Deswik undo integration |
| Automated tests | No test project or live-CAD regression harness exists |

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

### `CTX-02` Complete geometry extraction

Return full geometry for supported entity types rather than only handles and bounding boxes. Include polyline vertices, polyface topology, text, points, circles, lines, arcs, drill holes, and user attributes.

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

