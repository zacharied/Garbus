*The contents of this document differ from the present implementation.*

# Judgement specification

This is the canonical reference for the timing and judgement behaviour of every Garbus hit object. 

## Terminology

- **Input** — a player action evaluated against a hit object: a button press for note-type objects, or
  an analog-stick position or movement for stick-type objects.
- **Judgement** — the graded result assigned to a hit object (or a part of a composite hit object) scoring how well the
  player performed its input. Every Judgement belongs to a **Judgement family** (see below).
- **Hit** — an object (or head / tail / child) is *hit* if it received any non-Miss Judgement.
- **Missed** — an object is *Missed* if it received the **Miss** Judgement, whether from an early-miss
  input (see [Timing windows](#timing-windows)) or from its windows elapsing with no qualifying input.
- **StartTime / EndTime** — the chart times, in milliseconds, at which an object's judgeable span
  begins and (for duration objects) ends.
- **Head** — the part of an object judged around its StartTime.
- **Tail** — the part of a duration object judged at its EndTime
- **Child** — one of a Slider's consecutive tails.
- **Duration object** — an object whose EndTime differs from its StartTime (see [Duration](#duration)).
- **Lane** — an independent track that objects occupy for note-lock purposes; a single input belongs to
  exactly one lane.
- **Activated / Deactivated** — whether, at a given moment, the player is (is not) performing a duration
  object's input.
- **Timing window** — a range of time around an object's timing that maps an input to a Judgement.
  Windows may be **asymmetric** — extending different amounts early (before the timing) and late
  (after it).
- **Note-lock** — the rule preventing one input from interacting with more than one object in a lane
  (see [Note-lock](#note-lock)).

### Judgement families

Every Judgement belongs to one of three families. A family is an ordered set of Judgements, best to
worst. Different hit object types draw from different families. **Perfect** and **Miss** are shared by
all families; the intermediate Judgements are family-specific, so intermediate Judgements from
different families are never ranked against one another.

- **Note** — Critical Perfect, Perfect, Near, Miss. Used by CardinalNotes, ShoulderNotes, and HoldNote
  heads.
- **Hold** — Critical Perfect, Perfect, Bad, Miss. Used by HoldNote tails and Slider children (their
  duration judgements).
- **Early-permissive** — Perfect, Near, Miss. Used by Slam objects.

Catch-timed heads (a Slider head, and the head-style pseudo-judgements of Slider children) yield only
**Perfect** or **Miss**, which are shared by every family.

## Timing windows

Most hit objects have a set of **timing windows**: ranges of time around the object's timing that map an
input to a Judgement. The closer the input is to the object's timing, the better the Judgement.

Each object type lists its timing windows as a table, best Judgement first. Each row gives the distance
from the object's timing to the outer edge of that Judgement's window; the windows are **nested**, a
better Judgement's window lying inside a worse Judgement's. Ranges are inclusive. Unless a row states
otherwise, a range is **symmetric** — extending equally early and late. A row may instead apply to one
side only, marked **(early only)** or **(late only)**, making the overall window asymmetric. For
example, for an object with a StartTime of 100:

| Judgement | +/- ms range |
|-----------|--------------|
| Good      | 10           |
| Bad       | 50           |

Good covers [90, 110]; Bad's window reaches 50 ms to each side, covering [50, 150].

**Resolution.** An input is awarded the Judgement of the **innermost (best)** window that contains it.
If no window contains the input, the object awards nothing and the input does not interact with it.
Note-lock (below) decides which object an input applies to when more than one is eligible.
Expanding on the previous example, Bad's possible ranges are [50, 90) and (110, 150], and Good's is [90, 110].

**Early-miss window.** Note-family objects have a **Miss** window on the early side only (shown as
`Miss (early only)`). Pressing during it registers a Miss on that object immediately.
Late mistimes need no such window: once an object's latest non-Miss window elapses it is Missed
automatically and is no longer eligible (see [Note-lock](#note-lock)). The early-miss extents in the
tables below are provisional and meant to be tuned.

## Note-lock

If objects in the same lane are close enough in time, their timing windows may overlap, which would let
a single input interact with several of them. Note-lock prevents this: an input interacts with only one
object. An input resolves against the **oldest eligible object in its lane** — the unresolved object
with the lowest StartTime **whose window contains the input** — and is judged against that object
alone; newer objects are unaffected. If no object's window contains the input, the input is ignored.

An object stops being eligible once either:

1. it receives a Judgement (including a Miss from an early-miss input), or
2. its latest non-Miss window elapses, at which point it is Missed.

Note-lock resolves a single discrete input to one object's Judgement; it does not constrain
**Activation**. Any number of overlapping duration objects in a lane may be Activated at once, since
each independently tracks whether its input is being performed.

Note-lock governs note-family lanes only. Catch- and early-permissive-timed objects (Sliders, Slams)
are not note-locked.

## Duration

Some objects have a duration; that is, their EndTime differs from their StartTime. Between StartTime
and EndTime, an object with duration is **Activated** while the player performs its input and
**Deactivated** while they do not.

A duration object is a composite of a **head** (judged around StartTime) and a **tail** (judged at
EndTime, receiving the duration judgement). Sliders have multiple consecutive tails, called
**children**.

### Duration judgement

A duration object tracks its activation across its duration. When the duration has elapsed, its tail is
awarded a **duration judgement** based on the proportion of the duration during which the object was
Activated. These proportions are given as a table of absolute percentages. For example, for an object
with a StartTime of 0 and an EndTime of 1000:

| Judgement | Proportion |
|-----------|------------|
| Good      | 90%        |
| Bad       | 30%        |
| Miss      | 0%         |

Activated for at least 900 ms → Good; at least 300 ms but less than 900 ms → Bad; otherwise Miss.

### Grace period

Some duration object types have a **grace period** in milliseconds. When present, the first *x*
milliseconds of the duration are always treated as Activated. The grace period also affects the final
judgement (see below).

### Final judgement

The duration judgement is conceptual; it is not necessarily the final Judgement assigned to the tail.
By default the final judgement **is** the duration judgement, adjusted by a **head reference** — whether
the object's head was hit or missed — per the rules below. Where a rule refers to the head reference's
*largest non-Miss timing window*, it means that window's late (positive) extent — the time after
StartTime by which the head's input must land (e.g. 110 ms for a CardinalNote head, 200 ms for a Slider
head).

- If the object is Activated at the moment EndTime arrives, the final judgement must be better than a
  Miss (in the example above, at least Bad).
- If the object's duration is **shorter** than the head reference's largest non-Miss timing window, the
  duration judgement is discarded: the tail takes the best Judgement if the head was hit, or a Miss if
  the head was missed — the Miss still subject to the floor set by the preceding rule.
- If the object's duration is **at least as long as** the head reference's largest non-Miss timing
  window, and the object was never Activated during `[EndTime - GracePeriod, EndTime]`, the tail cannot
  take the best judgement (in the example above, Bad at best).

## Catch timing

Some objects have **catch timing**: their Judgement depends on whether the input is active during the
object's timing window, not on activating it at a precise instant. A catch window has **no early-miss
region** — holding the input from before StartTime is fine and yields a Perfect immediately at
StartTime. The window instead extends late, an asymmetric **(late only)** Perfect window: the player
may begin the input any time up to its late edge and still earn a Perfect. If the input is never active
by that edge, the object is Missed. That late edge is the object's largest non-Miss timing window.

## Early-permissive timing

Some objects have **early-permissive** timing. This resembles catch timing, but the input is a timed
action (such as a flick) rather than a sustained passive state. Because the action can be both started
and completed before StartTime, an early-permissive object begins evaluating input *before* its
StartTime — as early as the start of its widest early window — so that a correctly timed early action
still counts. Early-permissive objects are judged in the early-permissive family.

## Object types

### CardinalNote

CardinalNotes are button-press prompts that can be in one of four lanes. They are judged in the **note**
family.

#### Timing

| Judgement         | +/- ms range |
|-------------------|--------------|
| Critical Perfect  | 32           |
| Perfect           | 64           |
| Near              | 110          |
| Miss (early only) | 200          |

#### Hit policy

Note-lock applies to each lane individually.

### ShoulderNote

ShoulderNotes behave like CardinalNotes, but can be in one of two lanes. These are separate from
CardinalNote lanes. They are judged in the **note** family.

#### Timing

| Judgement         | +/- ms range |
|-------------------|--------------|
| Critical Perfect  | 40           |
| Perfect           | 80           |
| Near              | 150          |
| Miss (early only) | 200          |

#### Hit policy

Note-lock applies to each lane individually.

### HoldNote

Both CardinalNotes and ShoulderNotes have Hold variants that add a duration. A HoldNote's head is judged
in the **note** family; its tail is judged in the **hold** family.

#### Timing

The head of a `*HoldNote` uses the same timing window table as its parent `*Note` type.

#### Hit policy

HoldNotes exist in either one of the four Cardinal lanes, or one of the two Shoulder lanes. The head
object of a HoldNote is treated the same as a regular Note by its lane's hit policy.

#### Duration

| Judgement        | Proportion |
|------------------|------------|
| Critical Perfect | 100%       |
| Perfect          | 95%        |
| Bad              | 60%        |
| Miss             | 0%         |

A HoldNote's grace period equals the positive (late) timing window of the worst non-Miss judgement of
its parent `*Note` type.

### Slider

Sliders have both catch timing and a duration. A Slider has a head and at least one child; its body is
the set of line segments connecting the head to the first child, and each child to the next. Each line
segment is its own duration object, judged in the **hold** family.

#### Timing

A Slider head is catch-timed: it yields **Perfect** unless Missed. Its Perfect window is asymmetric —
the input may be caught from before StartTime (as a held state) through **200 ms** after StartTime;
failing to catch it by then is a Miss. This 200 ms late extent is the head's largest non-Miss timing
window, used by the Final-judgement rules. Each child's head-style pseudo-judgement uses the same
200 ms window.

#### Duration

| Judgement        | Proportion |
|------------------|------------|
| Critical Perfect | 95%        |
| Perfect          | 90%        |
| Bad              | 50%        |
| Miss             | 0%         |

A Slider body has a grace period of 200 ms, equal to the Slider head's late Perfect extent.

Multi-child Sliders extend the duration rules across the body. Each body segment is judged by the
standard duration rules, and the head reference for each segment is:

- for the **first** segment, the Slider head's judgement (Perfect or Miss);
- for every **subsequent** segment, a **head-style pseudo-judgement** of the child at the segment's
  start.

The head-style pseudo-judgement is derived the way a Slider head is — a catch-style Perfect or Miss
based on whether the input was correctly active at that child's point — and is distinct from that
child's own duration judgement. The pseudo-judgement must not be applied as a real judgement; child
objects are already judged as duration objects, so applying the pseudo-judgement would cause them to
appear to be judged twice.

### SlamCentered

SlamCentered objects are early-permissive hit objects, judged in the **early-permissive** family.

#### Timing

| Judgement         | Range |
|-------------------|-------|
| Perfect           | 200   |
| Near (late only)  | 300   |
| Miss              | n/a   |

### SlamEdge

SlamEdge objects are early-permissive hit objects timed identically to SlamCentered.