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
- **Node** — a Slider's head, or one of its children. Every node is judged the same way, by
  [catch timing](#catch-timing).
- **Child** — one of a Slider's nodes after the head. A child is also the tail of the segment ending
  at it.
- **Segment** — the span between two consecutive Slider nodes. A segment may have a duration of 0.
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
- **Hold** — Critical Perfect, Perfect, Bad, Miss. Used by HoldNote tails, Slider segments, and Slider
  nodes.
- **Early-permissive** — Perfect, Near, Miss. Used by Slam objects.

Slider nodes draw from the hold family but can never take **Critical Perfect** — a node is graded
Perfect, Bad, or Miss (see [Catch timing](#catch-timing)).

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

A HoldNote is a composite of a **head** (judged around StartTime) and a **tail** (judged at EndTime,
receiving the duration judgement). A Slider is instead a chain of **nodes** joined by **segments**,
each segment being a duration whose tail is the node it ends at (see [Slider](#slider)).

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

Some duration object types have a **grace period** in milliseconds: an opening span, starting at
StartTime and capped at the object's duration, that forgives a player who begins the input a little
after StartTime.

The grace period is credited **only if the object was Activated at some point within it**. When it is
credited, the whole grace period counts as Activated. When it is not, none of it does. An object that
received no input during its grace period is therefore graded on its real activation alone, and one
that received no input at all is Missed rather than carried by free credit.

The credit is all-or-nothing, so there is a step at the grace period's outer edge: a player who first
Activates just inside it keeps the full credit, and one who first Activates just outside it keeps none.
This is deliberate — past that edge the player has missed the object's start.

The grace period also affects the final judgement (see below).

### Final judgement

The duration judgement is conceptual; it is not necessarily the final Judgement assigned to the tail.
By default the final judgement **is** the duration judgement, adjusted per the rules below.

- If the object is Activated at the moment EndTime arrives, the final judgement must be better than a
  Miss (in the example above, at least Bad).
- If the object was never Activated during `[EndTime - GracePeriod, EndTime]`, the tail cannot take the
  best judgement (in the example above, Bad at best).

A tail's Judgement never depends on how its head, or any neighbouring object, was judged — it follows
from the input performed over its own duration. A duration shorter than the grace period needs no
special case: the grace period is capped at the duration, so a player who arrives in time is credited
the whole span and a player who never arrives is credited none of it.

## Catch timing

Some objects have **catch timing**: their Judgement depends on whether the input — a sustained state
rather than a triggered action — was active at the object's position, not on triggering it at a precise
instant. A catch window has **no early-miss region**: holding the input from before StartTime is fine
and is in fact how a catch-timed object is played best.

A catch window is **symmetric**, and grades on when the input covered the object's position:

- **Perfect** — the input covers the object's position at StartTime itself.
- The family's **worst non-Miss** Judgement — otherwise, if the input covered the object's position at
  some point within the window.
- **Miss** — otherwise.

The window's extent to each side is the object's largest non-Miss timing window. Arriving after
StartTime and leaving before it grade alike; only covering the position across StartTime earns a
Perfect.

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

A Slider is a chain of **nodes** — a head, then zero or more children — joined by **segments**. Every
node is catch-timed, and every node **yields exactly one Judgement**, so a Slider's Judgement count
always equals its node count. A head-only Slider — no children — is judged by its head alone.

The head and the children are judged identically as nodes; the head is distinguished only by having no
segment before it. The Slider's drawn path between nodes is presentation, not a judgement surface: what
is judged is the nodes, and the input held across the time between them.

#### Timing

Every node is catch-timed with a **200 ms** window to each side of its StartTime:

| Judgement | Condition                                                                              |
|-----------|----------------------------------------------------------------------------------------|
| Perfect   | the input covers the node's angle at the node's StartTime                                |
| Bad       | otherwise, the input covered the node's angle at some point within 200 ms of StartTime   |
| Miss      | otherwise                                                                                |

Reaching a node early and leaving before its StartTime grades the same as arriving after it. Perfect is
a check of the input's *state* as StartTime is reached, not of an action performed at that instant: a
player who covers the node's angle before StartTime and is still covering it when StartTime arrives
earns the Perfect. This 200 ms is the node window, and is also the Slider segment grace period below.

One final rule applies to Slider judgement: if a Slam with the same **Side** exists at the same
StartTime as a node (head or child) and that Slam is not a Miss, the node cannot be a Miss either.
Normal judgement rules otherwise apply to the node. The Slam is still judged independently as its own
hit object.

#### Duration

Each child owns the segment ending at it. That child's Judgement is:

- its **node judgement**, if the segment's duration is 0;
- the segment's **duration judgement**, otherwise.

The head's Judgement is always its node judgement — it has no segment before it.

| Judgement        | Proportion |
|------------------|------------|
| Critical Perfect | 95%        |
| Perfect          | 90%        |
| Bad              | 50%        |
| Miss             | 0%         |

A Slider segment has a grace period of 200 ms, equal to the node window, capped at the segment's
duration. Every segment is judged by the standard duration rules, independently of the other segments
and of how any node was judged.

A segment is **zero-length** when its child sits at the same time as the preceding node (TimeOffset 0)
— a **jump**, which may occur between two children as readily as after the head. A zero-length segment
spans no time and so computes no activation proportion; its child is graded purely as a node. Reaching
a jump's angle late, or early and then leaving, is therefore a Bad rather than a Perfect or a Miss.

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
