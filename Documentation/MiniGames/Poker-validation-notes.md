# Poker milestone 3 - validation notes

## Engine-envelope initialization

The protocol suite uses the real `IntersectPacket.Data` / `MessagePacker.Deserialize`
engine envelope, not only generic MessagePack DTO serialization. The in-process two-client
model test traverses that envelope in both directions. It is not a socket or graphical test.

The initial envelope run on b31c767 failed because the standalone test executable discovered
packet types but did not populate `PackedIntersectPacket.KnownTypes`. The actual game already
performs `PackedIntersectPacket.AddKnownTypes(PacketHelper.AvailablePacketTypes)` during
`ApplicationContext<TContext,TStartupOptions>.Start` (Intersect (Core)/Core/ApplicationContext`2.cs).
The test startup now mirrors that step using the discovered registry.Types, and checks both
request/state key mappings. No hard-coded packet IDs or production registration changes are
needed. The existing sorted type discovery also means all participants must use matching
client/server/editor binaries and plugin lists; do not mix old and new builds.

## Additional transport checks

A valid Leave request is exempt from the ordinary eight-per-second action limit: closing a
window must not trap an online player at a table. It still requires the current table/view,
authenticated session, and a new request ID. A stale/repeated Leave is rejected.

Runtime presence reads occur outside the runtime gate. Observations are rechecked against
view object identity before applying them. All table mutations, including timer advancement,
and delivery sequence assignment then occur under one runtime gate. Network sends happen
after releasing it. This avoids reversing the revision order between a timer and an action
acknowledgement without taking a player's lock from inside the runtime gate.

## Dependency / build warnings

The first milestone-3 job completed all 35 core/registry groups and 14 typed-MessagePack groups
successfully for a80ddd78307803d1d9c1700d4215d851251b4efe. The later engine-envelope coverage is
stronger and must be checked independently. Its build logs reported NU1902/NU1903 vulnerability
warnings for the repository's MessagePack 3.1.3 dependency, a NU1902 warning for NCalcSync 3.8.0,
and MessagePack source-generator/analyzer warnings.

Those dependency versions are unchanged by the poker work. Their warnings have NOT been
suppressed or remediated in this feature branch. A passing build is not a security audit;
review and update dependencies, with regression tests, before a production release. Refer to
the actual workflow log and the advisories linked there rather than assuming these packages
are safe because the test process exited successfully.

Consult the PR/checks for the final commit's results; earlier success does not validate later
changes. Real client/server/editor builds and the manual matrix in Poker.md remain separate
acceptance gates. The branch stays a draft and all stakes remain disposable tests.
