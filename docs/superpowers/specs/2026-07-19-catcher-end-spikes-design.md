# Slider contact spikes — design

## Summary

Replace the decorative spikes on the ends of the stick-catcher paddles with a slider-owned contact
effect. Whenever a slider body reaches the outer ring, a split spike emerges from that slider's exact
ring-contact point and follows the point as the slider sweeps around the ring.

The effect consists of two narrow, flared triangles pointing inward. A visible gap runs between them
along the contact angle, giving the spike a forked silhouette rather than a single solid needle.

A complementary stick spike runs from the playfield centre to the ring. It is widest at the centre,
tapers to a point at the ring, follows the analog stick's angle, and occupies the fork's centre gap
when the stick aligns with slider contact.

## Behaviour

- Stick-catcher paddles retain their existing arcs but have no spikes attached to their angular ends.
- A slider contact spike appears when chart time reaches the slider's first node.
- It remains visible while chart time lies between the slider's first and last nodes, because that is
  the interval during which the slider body intersects the ring.
- It follows `DrawableSliderBody.AngleDegAt(Time.Current)` every frame. That method evaluates the same
  eased/smoothed sweep used to draw the body, so the effect cannot drift away from the visible contact.
- It disappears once the slider's last node passes the ring or the slider leaves its idle/alive state.
- Each simultaneous slider owns its own effect. Paddle activation and catch success do not control it.
- Each stick owns one centre-origin spike. It appears with the catcher arc outside the deadzone, follows
  `SliderCatcher.Angle`, and hides when the stick returns to the deadzone.

## Geometry and appearance

- The pair is centred on the slider/ring contact angle at exactly `1.0×` ring radius.
- Two triangle bases sit on opposite sides of the contact angle with an empty angular gap between them.
- Each triangle follows its own nearby radial line, so both point toward the playfield centre and the
  physical gap naturally narrows as the tips approach the centre.
- Both triangles use the slider side colour: blue for Left (`Constants.LeftColour`) and magenta for
  Right (`Constants.RightColour`).
- Each triangle gradients from the opaque side colour at the ring to bright transparent white at its
  inward tip. A small additive glow softens the lateral edges.
- The ring stroke draws over the triangle bases, visually capping them at the contact point.
- The stick spike is a single triangular wedge with its wide base at radius zero and its apex on the
  ring. Its centre width is derived from a `0.5°` half-width at ring scale, keeping the taper inside
  the split contact spike's `0.7°` half-gap wherever the two effects overlap.
- The stick spike's additive blur uses its side's blue or magenta colour.

Initial tuning values, expressed relative to the current ring radius:

| Parameter | Value |
| --- | --- |
| Tip radius | `0.22×` ring radius |
| Triangle half-width | `1.15°` |
| Gap half-width | `0.7°` (`1.4°` total gap) |
| Glow opacity | `0.9` |
| Glow blur sigma | `8px` |
| Stick-spike half-width | `0.5°` |
| Stick-spike radial span | centre (`0×R`) to ring (`1×R`) |

## Architecture

`DrawableSliderBody` owns one `SliderContactSpikes` drawable. This keeps the effect beside the
authoritative slider geometry and avoids scanning `Ring.AliveHitObjects` or duplicating state in the
stick indicators.

During `DrawableSliderBody.updatePath()`:

```csharp
bool hasRingContact = State.Value == ArmedState.Idle &&
                      Time.Current >= nodeTimes[0] &&
                      Time.Current <= nodeTimes[^1];

contactSpikes.SetContact(
    toRadians(AngleDegAt(Time.Current)),
    scrollingContainer.ScrollLength,
    hasRingContact);
```

`SliderContactSpikes` offsets its two blade angles equally around the contact angle. Each blade owns a
gradient `Triangle` wrapped in `GlowEffect`.

The triangle inside the glow wrapper must remain top-left anchored. Centring a child inside an
auto-sized effect wrapper creates a circular required-size dependency and can make the wrapper grow
every layout frame. The wrapper itself receives the centre anchor, bottom-centre origin, radial
position, and rotation.

`StickIndicator` owns one `StickCentreSpike`, listed before its `Arc` so the paddle stroke caps the
outer tip. It reuses `SpikeBlade` with a base radius of zero and a tip radius at the ring, and receives
the current stick angle, ring radius, and activation state each frame. This is directional stick
feedback only; it does not depend on a slider being present or caught.

## Testing

Headless gameplay coverage should verify:

- Stick-catcher paddles contain no endpoint spikes; each owns exactly one centre-origin wedge.
- The contact effect is hidden before the slider reaches the ring.
- Exactly two triangles appear at the first-node time with one on either side of the contact angle.
- Both triangles are bounded inside the ring and use the slider's side colour.
- The pair follows `AngleDegAt(Time.Current)` as the slider sweeps.
- The effect disappears after the final node passes the ring.
- Each centre spike is hidden in the deadzone, appears at the active stick angle, has its wide base at
  centre and point at the ring, has a non-zero side-colour blur, and follows stick rotation.
- The centre spike's half-width is smaller than the slider contact gap half-width.

Prefer state and geometry assertions over pixel comparison because glow output is not deterministic in
headless rendering.

## Out of scope

- No change to slider catch detection, judgement, escape-band rendering, or the existing consumed-tip
  marker.
- No pulse or catch-dependent intensification in this version; both effects are direct state feedback.
- No settings toggle.
