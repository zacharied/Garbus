# Multi-value-aware inspector controls

## Problem

The editor's right-toolbox `Inspector` (`Garbus.Game/Edit/Inspector.cs`) exposes editable
controls for the current selection — currently three enum dropdowns: **Side** (`IHasSide`
objects), **Direction** (`GarbusSlamEdge`), and **Easing** (selected slider control-point
nodes).

Two gaps:

1. The **Side** and **Direction** dropdowns only render for a *single* selected object
   (`objects.Length == 1`). With a multi-object selection they disappear, so a parameter shared
   across many objects can't be edited in bulk.
2. When a control *does* span multiple targets (the **Easing** dropdown already applies to all
   selected nodes), there is no representation of a *mixed* state. The dropdown just shows the
   first target's value, hiding the fact that the targets disagree. The current Easing code even
   computes a `shared` value but both branches return `firstNode.SweepEasing`
   (`Inspector.cs:257–259`), so mixed nodes never surface as mixed — a latent no-op bug.

We want inspector controls to be **multi-select aware**: when the selection holds differing
values for a parameter, the control shows `<multiple>`; picking a value applies it to the whole
selection as one undo step. This applies to dropdowns now and to checkboxes when boolean
parameters exist later.

## Goal

A small **reusable multi-value control kit** so any current or future inspector control gets
`<multiple>` behaviour for free:

- A pure aggregation helper (shared-value vs mixed).
- A multi-value-aware enum dropdown.
- A multi-value-aware (tri-state) checkbox.

Retrofit the existing Side / Direction / Easing dropdowns onto the kit.

## Decisions

- **Architecture:** aggregation helper + two thin control wrappers (not a bindable bridge — Garbus
  hit-object properties are plain `get/set` propagated via `editorChart.Update()`, so a
  `Bindable` layer would need an awkward property↔bindable adapter).
- **Common-property rule:** a control renders only when **every** selected object exposes the
  property. A control never silently affects a subset of the selection.
- **Checkbox is infra-only for now:** no boolean hit-object property exists today, so the checkbox
  ships tested and ready to drop in, but is **not wired to a live property**. We do not invent a
  property to justify it.
- **Mixed representation:** dropdown shows a transient `<multiple>` item; checkbox shows the
  indeterminate (dash) state via the existing `TernaryState` enum
  (`Edit/Compose/TernaryState.cs`).

## Design

### 1. Aggregation helper — `Garbus.Game/Edit/Inspector/MultiValue.cs`

Pure, framework-free, unit-testable:

```csharp
public readonly struct MultiValue<T>
{
    public readonly bool IsMixed;
    public readonly T Value;   // meaningful only when !IsMixed (holds first target's value otherwise)
}

public static class MultiValue
{
    // Returns IsMixed=false + shared Value when all targets agree; IsMixed=true when they differ.
    // Caller must not invoke on an empty list (control isn't rendered for an empty selection).
    public static MultiValue<T> Aggregate<TObj, T>(IReadOnlyList<TObj> objs, Func<TObj, T> get);
}
```

Equality via `EqualityComparer<T>.Default` (works for enums).

### 2. `MultiValueEnumDropdown<T>` where `T : struct, Enum`

Wraps `BasicDropdown<T?>`:

- Normal items = `Enum.GetValues<T>()` cast to `T?`.
- When constructed with a mixed `MultiValue<T>`, prepend a `null` sentinel to `Items` and set
  `Current = null`.
- Override the dropdown's item-text generation so `null` renders as `"<multiple>"` and real values
  render via their normal name.
- Selecting a real value invokes `onChange(value)`.
- No manual sentinel cleanup needed: after the Inspector applies the value to all targets, it
  rebuilds on `HitObjectUpdated`; the now-shared value produces a non-mixed `MultiValue`, so the
  sentinel is simply not built next time.

Constructor shape: `(string label, MultiValue<T> state, Action<T> onChange)`.

### 3. `MultiValueCheckbox` — tri-state, `TernaryState`-driven

osu-framework's `BasicCheckbox` is bool-only, so this is a small custom `CompositeDrawable`:

- Layout: a box + label (matching the dropdown label styling in the inspector).
- Visual states from `TernaryState`: `False` → empty, `Indeterminate` → dash (mixed), `True` →
  check.
- Click behaviour: from `Indeterminate` or `False` → `onChange(true)`; from `True` →
  `onChange(false)`.
- Constructor shape: `(string label, MultiValue<bool> state, Action<bool> onChange)` — maps
  `IsMixed` → `Indeterminate`, else `Value` → `True`/`False`.

Ships tested but unwired (no boolean property to bind).

### 4. Retrofit `Inspector.addControls`

Replace the single-object gating and the private `addEnumDropdown` helper with an
`addMultiValueDropdown` that takes an aggregated `MultiValue<T>`:

- **Side** — render when the selection is non-empty and **all** selected objects implement
  `IHasSide`. Aggregate their `Side`. `onChange`: one `BeginChange`, set `Side` on each target and
  `editorChart.Update(target)` per object, one `EndChange`.
- **Direction** — same pattern, gated on all selected objects being `GarbusSlamEdge`.
- **Easing** (nodes) — keep the existing multi-node apply, but source the displayed value from the
  aggregation helper so differing nodes show `<multiple>`. Fixes the `Inspector.cs:257–259` no-op.

The batching helper (BeginChange → per-target set + `Update` → EndChange) is shared by all three so
each bulk edit is a single undo step.

The text summary block (`writeSummary`) is untouched.

## Testing

Headless (`Garbus.Game.Tests/Editor/`):

- `MultiValue.Aggregate`: all-agree → not mixed with shared value; differing → mixed; single
  element → not mixed.
- Inspector: multi-select of objects with differing `Side` shows `<multiple>`; with a shared
  `Side` shows that value; applying a value to a mixed multi-select unifies all targets and is a
  single undo step (one `BeginChange`/`EndChange`).
- Easing: a slider with two nodes of differing `SweepEasing` shows `<multiple>` (regression guard
  for the old no-op).

Visual scene: `MultiValueCheckbox` tri-state cycling (empty / dash / check), since the checkbox is
otherwise unwired.

## Non-goals

- No new hit-object properties (including no boolean property to back the checkbox).
- No bindable bridge / `MultiValueBindable`.
- No change to the inspector text summary.
- No change to node-selection semantics.
