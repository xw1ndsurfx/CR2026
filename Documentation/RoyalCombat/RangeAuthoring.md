# Royal Archery: range authoring and timing

This slice extends free solo training. It does not enable multiplayer tournaments, rewards
or production artwork. Existing inventory arrows, bows and paperdolls remain unchanged.

## Author a range

Open Mini-Games > Archery Mini-Game Editor, select the range, then **Range rules and targets**.
The modal edits a separate draft: Cancel discards changes; Save validates and returns the
draft to the parent editor. Save the parent configuration to send it to the server.

* Round settings: distance 5–100 metres, 1–10 arrows, 15–300 seconds and optional wind
  override (−3 to +3 m/s²). An empty wind override inherits the range's wind.
* Add/remove rounds within 1–6. Maximum score is ten times the total arrow count.
* Each round has 1–8 targets. Select a target, drag its centre in the perspective preview,
  or edit X/Y in metres. Radius is 0.2–1.5 metres. All faces share that round's distance.
* MotionX/MotionY are horizontal/vertical amplitudes in metres; zero keeps that axis still.
  PeriodSeconds is 3–60 and PhaseRadians changes the starting phase. Horizontal motion
  uses sine and vertical motion uses cosine, allowing an ellipse or single-axis oscillation.
  Preview time/Animate shows the resulting path. Invalid geometry is rejected on Save.

Coordinates: X is right, Y is up, Z is distance. Target centres plus their motion must stay
within ±min(8, distance×0.35) horizontally and ±min(4, distance×0.25) vertically.
The preview uses the same perspective projection as the client. It is diagnostic geometry,
not the production scenery/asset editor. Arbitrary drawn paths are not implemented.

Existing configurations without Rules retain the default 10/25/50 m, five-arrow,
100-second rounds. Each match deep-copies its validated rules at entry; editing the range
does not alter a match in progress. No account/item migration is required.

## Play and score

The highlighted target is the only scoring face for the current arrow. Targets rotate
in list order for each shot. The server calculates the arrow crossing of the target plane
and the moving target's position at that exact arrival time. Only then is the pending score
published. Aiming at another face cannot earn points. Planted arrows are target-local and
follow that face's motion. A new round starts a fresh impact display.

The server owns the round clock. At the deadline, unused arrows score zero and the next
round starts. A release accepted before the deadline may land afterward, and the next round
receives its full clock when that late flight completes. A draw held for over ten seconds
abandons training unless the round has already expired. Round/shot tokens prevent delayed
inputs from drawing or releasing a different shot after a transition.

Right click cycles 1×/2×/4× zoom, preserving the world aim under the pointer and increasing
pointer precision. It changes client projection only: target radii, trajectory and scoring
stay authoritative on the server. Zoom resets when a new match begins. The HUD shows
remaining time, active target, zoom, dynamic round count and maximum score.

## Verification and remaining limits

The independent rules harness covers moving-target interception, off-target misses,
configuration cloning/validation, late arrivals, forfeits, stale inputs and anchored zoom.
Server tests exercise authored rules through the actual player handler and verify that a
timed-out match releases its hold without consuming inventory arrows. Native editor tests
exercise dragging, draft isolation and invalid-motion rendering; client tests render custom
round/target states at every zoom using the existing headless renderer.

These are automated code/control checks. Production-world visual QA, real Admurin art,
audio, cinematic sequences, multiplayer, rankings and network/load testing remain required.
