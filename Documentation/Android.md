# CR2026 Android distribution

The Android app compiles the shared client source against MonoGame Android. It does
not load DesktopGL, SDL or Steamworks. Desktop projects keep their existing build.
Player releases are sideloadable APKs signed with a permanent local release key.
Google Play publishing is not part of this workflow.

## Build an APK

Install .NET 10, its Android workload, Android SDK API 36 and the JDK required by
that workload. Then, from the repository root:

```powershell
./scripts/build-android-client.ps1 -Resources 'C:/path/to/player-client/resources'
```

The complete player resources directory must include `config.json`,
`client_strings.json`, `fonts` and `gui`. The repository's partial resources alone
are sufficient for a compilation check, but do not constitute a playable client.
The script reads resources without modifying the original directory. Assets are
packaged in the APK and extracted into app-private storage on first launch and
after an app update. Preferences live separately and survive upgrades.
Database caches, private translation configuration and server key files are excluded.

Local defaults can be saved in the git-ignored `.local/android-client.json` with
`Resources` and `NetworkPublicKey` string properties. Explicit script arguments
override those defaults. This machine is configured with the supplied player
resources and the public key extracted from its distributed PC client.

The server must accept ordinary game accounts: Android cannot authenticate through
Steam. Use the **same public network handshake key as the deployed server**:

```powershell
./scripts/build-android-client.ps1 -Resources 'C:/path/to/resources' `
    -NetworkPublicKey 'C:/path/to/server/network.handshake.bkey.pub' -VersionCode 2
```

Without that parameter the build uses this checkout's network key. A new key will
not connect to a server using another key. Never package the private server key.

The script defaults to Release, matching deployed Release servers. Debug and
Release intentionally use different packet protocol fingerprints; a Debug APK
cannot communicate with a Release server even when the source and keys match.
Use `-Configuration Debug` only for a matching Debug server.

Signed preview APKs are copied to `artifacts/android/Release/android-arm64/`.
Use `-Runtime android-x64` for an x64 emulator. `-CompileOnly` checks the source
without requiring the full asset directory. Debug signing is for local tests;
use `-Distribution` for permanent release signing and the player ZIP.

Standalone APKs use the Mono JIT with managed debugging symbols disabled. On the
Pixel 10 test device, enabling the current toolchain's managed debugging support
stalled worker callbacks even in a minimal application. Both Release and Debug
without that support passed the isolated thread tests. Use app logs for these
device tests; attaching a managed debugger needs separate toolchain validation.
JNI callbacks use runtime registration. Generated marshal-method optimization is
disabled because incremental rebuilds in this workload could omit activity entry
points, producing `UnsatisfiedLinkError` before `OnCreate`.


## Publication with the usual dotnet command

Run from the repository root using PowerShell 7 (`pwsh`), .NET 10, Java 21 and the
Android workload/SDK 36. `after.Intersect.sln.targets` routes mobile and combined
RIDs; ordinary linux-x64/win-x64 solution commands keep their existing behavior.

```powershell
# Phones (including Pixel): the player APK
 dotnet publish -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r android-arm64
# Intel/x64 emulator or compatible x64 device, NOT ARM64 phones
 dotnet publish -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r android-x64
# Linux x64 server/client, Windows x64 server/client/editor, Android ARM64 player APK
 dotnet publish -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r all-x64
```

`all-x64` is a dispatcher, not a real .NET RID. Its Android output deliberately
uses ARM64 for phones. Each project restores with its actual RID; the editor is
published only for Windows. Desktop projects retain their usual publish folders.
For a dry run append `-p:CRPublishPlanOnly=true`; it prints the plan without
restoring or compiling those projects. Normal Android commands perform the real
build, sign and package operations.

For Android alone, local settings select the complete resources and deployed
server public key. A combined build instead uses the public key generated with
its new server build, matching the desktop clients. Do not deploy that new server
key onto an existing server inadvertently; the standalone Android build continues
to target the current deployed server. Signing keys and network keys are separate.

## Permanent signing and player package

Initial setup on another workstation:

```powershell
./scripts/initialize-android-signing.ps1
./scripts/build-android-client.ps1 -Distribution -Resources 'C:/player/resources' `
    -NetworkPublicKey 'C:/server/network.handshake.bkey.pub'
```

The initializer refuses to overwrite an existing key. On this workstation the
permanent key is already configured in `.local/android-client.json`.
**Back up the entire private `.local/android-signing` directory securely**, including
`corps-royaux-release.keystore` AND `keystore-password.txt`. The password file is
plain text for unattended signing; Windows ACLs restrict directory access to the
creating account. Never distribute or commit this directory. Losing the key or
password prevents signing compatible updates. Restore the same files when moving
the build to another workstation, and update the local settings paths.

`-SigningKeyStore`, `-SigningKeyAlias` and `-SigningPasswordFile` override local
settings. CI can instead supply `CR2026_ANDROID_SIGNING_PASSWORD`; the build passes
an `env:` reference to MSBuild, never the password value on the command line.
Without `-Distribution`, the standalone build script remains a diagnostic build.

`versionCode` advances automatically using the ignored
`.build-versions/Intersect.Client.Android.package.txt` counter (starting above diagnostic version 14).
`-VersionCode` can explicitly reserve a larger number; distribution rejects a
number that does not exceed the saved counter. Preserve this counter and advance
beyond the largest version ever sent to players when switching build machines/CI.
The display version comes from `-p:Version`.

Find player releases under `artifacts/android/distribution/`:

- APK with the game icon, full resources and permanent signature.
- ZIP containing the APK, French/English/Spanish installation instructions,
  SHA256SUMS.txt, release.json and the existing client license notices.
- No private signing/network keys, database caches or developer configuration.

Give players the APK directly or the ZIP. Future release APKs update in place.
The earlier diagnostic APKs use another signing key: Android requires removing
them before installing the first permanent release. That removes local settings,
while accounts, characters and progress remain server-side. This preparation does
not uninstall the validated diagnostic application from the test Pixel.

## GitHub Actions

The main workflow retains the Android compilation check with the same generated
network keys as its desktop/server jobs. Player distribution is built locally by
the dotnet commands above, using this machine's full resources and permanent key.
No signing credentials or full licensed player resources have been uploaded to
GitHub, and remote CI execution/publication has not been performed here.

The application icon uses the existing in-game shield/swords logo, with an adaptive
foreground/background for Android 8+ and a fallback for older supported devices.

## Device validation

Install the APK with Android's package installer, or `adb install -r <apk>`.
Allow room for both the APK and extracted resources (the current client resources
are approximately 693 MB before packaging).

Verify first launch, account login, character selection, map loading, movement,
combat, text entry, audio, background/resume, rotation between landscape directions,
and reconnect. Test English, French and Spanish, including saved preferences.

Validation on the Pixel 10 (2026-11-12) confirmed complete resource extraction,
the animated menu, and opening Settings by touch. The initial Debug APK remained
Offline against the deployed Release server. Inspection confirmed that the current
Release protocol fingerprint exactly matches the working Windows client, while
Debug differs. The full Release APK version 4 then received Online status, completed
the connection handshake with the existing server, opened Login, and displayed the
native Android keyboard when its username field was touched. The user then confirmed
successful account authentication, character selection, and arrival on the game map.
Movement initially produced stale frames, freezes and long diagonal terrain
triangles while audio and server-side movement continued. Tile buffers inherited
the framework's `LineList` default while MonoGame drew `TriangleList`: the count
calculation requested three triangles from each six-index tile instead of two.
Tile buffers now explicitly select `TriangleList`. The user confirmed a normal
image and fluid movement on the diagnostic APK with normal texture readback.
Removing the Android frame-composition target in full APK version 5 regressed
the world to black while the UI and minimap remained visible. Android therefore
keeps the preserved full-frame target and presents it once to the backbuffer;
intermediate minimap/lighting target switches must not discard the world pass.
Version 6 restores that composition alongside the triangle-count correction.
The user confirmed a normal map and fluid movement for approximately 30 seconds
on the full version 6 APK, with diagnostics disabled and normal texture readback.
The user also confirmed normal rendering and movement after background/resume.
Landscape rotation, reconnect and locale coverage remain to be validated.

The mobile UI candidate scales the canvases to approximately 540 logical pixels
high (2x on the Pixel), with matching touch coordinates. Menu opens the game menu
directly, using two columns of taller entries. The labeled Interact, Block and
Pick up controls use the existing E/Q/Space actions; a fixed bottom-left joystick
now supplies movement directions, replacing the arrows. World taps no
longer synthesize mouse clicks. Interface taps still operate windows, and a finger
remains assigned to its initial control until released. Gameplay controls hide
while a closable window, modal input or the game menu is open. Physical keyboards
and controllers remain supported. This interface candidate needs device testing.
Version 7's screenshot exposed clipping in cached panels: cache textures used
logical dimensions while their contents were drawn at canvas scale. Version 8
allocates caches at physical dimensions and keeps text measurement in logical
coordinates. It also uses Android system-bar/cutout insets for canvas/control
bounds and a compact mobile minimap. Whole-image flicker reported on version 7
remained present on version 8, while the user confirmed that its layout fits.
A regression test then reproduced a separate nested-cache destination bug:
the outer panel was drawn into its own texture because a shared "last target"
overrode the correctly restored destination. The cache renderer now keeps the
destination restored by its stack, including when clean caches are reused on a
later frame. Device confirmation of the flicker fix remains required.
The user also reported offset menu taps and a fog-free strip at the left edge.
Mobile canvases now retain Gwen's required zero origin, with touch input already
in backbuffer coordinates (no second desktop mouse conversion). The menu canvas
covers the full screen for weather/shading; game HUDs reserve right/bottom bars
and the game menu/control layout separately respects left/top cutouts. The
combined version 10 candidate includes these fixes and the cache correction;
version 9 was an intermediate build and was not installed. Headless tests confirm
the displayed center of a button is hit on a 2x zero-origin canvas.
The user confirmed aligned taps on version 10, but movement flashes persisted.
A 40-second recording (2,343 decoded frames) showed upper scenery and sprites
missing for one frame while ground and HUD remained present. At 34.6969 seconds,
the sampled world luminance jumped from 86.86 to 95.30 and returned to 86.85 on
the next frame. This is consistent with later draws going to the wrong target.
[MonoGame 3.8.2's GLES pixel readback](https://github.com/MonoGame/MonoGame/blob/v3.8.2/MonoGame.Framework/Platform/Graphics/Texture2D.OpenGL.cs#L208-L224)
binds and deletes a temporary framebuffer without restoring the prior binding.
Android texture reads now capture and restore that native binding in `finally`,
without changing MonoGame's cached render-target state or flushing queued sprites.
Sprite bounds, icon pixels, font metrics and falling-tree reads use the wrapper.
Tests cover the default/non-default binding, failures and nested reads. Version 11
was built and verified but not installed before the additional joystick request;
the GPU fix is included in the combined version 12 candidate. Device confirmation
on version 12 confirmed that the scenery flashes are gone during joystick movement.

The joystick captures one finger, clamps thumb travel, uses an 18% neutral zone
and supports eight directions. Releasing/cancelling stops its movement keys; a
second finger can operate an action button without stealing joystick ownership.
Boat steering previously read only W/A/S/D, unlike deck walking, which already
accepted arrows. Helm input now accepts both sets, with unchanged driver/seat,
busy and server authority checks. While steering, the pickup button is labeled
Anchor (en/fr/es) and retains the existing Space action. Character/NPC names,
titles and guild labels are 25% larger on Android with matching measured spacing.
Tests cover joystick directions/ownership/release, WASD/arrow boat equivalence,
anchoring and busy guards. The user confirmed correct joystick movement/release.
The user also confirmed that holding the joystick straight up for 3–5 seconds
moves the boat correctly. Version 13 changes the mobile helm joystick to choose
a world heading: left selects west, the rudder stops within eight degrees of
the selected heading, and forward thrust continues while held. Opposite headings
turn before advancing. Keyboard helm controls retain forward/reverse and rudder
semantics. Driver, busy and anchor checks are preserved. Tests cover cardinal
headings, the shortest turn across north and a sustained leftward input that
settles instead of circling. The user validated version 13 on Pixel 10: holding left takes the selected
heading and then advances straight without circling.
The desktop resource updater is disabled in this APK; install a newer APK to update.

The main CI workflow checks Android compilation using the same generated network
keys as the desktop/server jobs. Full player distribution packages are built locally;
no assets or signing credentials have been uploaded to GitHub.

Version 14 fixes ship-of-the-line atlas loading. These resources are present in
full APKs, but their roughly 96 KiB metadata exceeded the shared reader's obsolete
64 KiB limit. The reader now uses ShipSpriteAtlas.MaximumMetadataBytes (128 KiB),
matching the format while retaining bounded input and structural validation.
Regression tests load all 28 shipped ship-of-the-line atlases, including normal,
idle, damaged and sinking variants. The user confirmed on Pixel 10 with version 14 that ships of the line are now visible at the same location as in the Windows client.

Local distribution validation (2026-11-12): the actual root-level
`dotnet publish ... -r android-arm64` command built versionCode 16 with zero errors.
The APK uses the permanent Corps Royaux RSA-3072 certificate (SHA-256
6AC58BC20DD9CAB29763FC2947663F44A5606DC6F3BB326D5EBFD5A625B62B9A), passes
apksigner v1/v2/v3 verification and 16 KiB native ZIP alignment, contains 8,326
resource files and 24 bitmap-font XNBs, and declares the application icon.
The packaged icon is pixel-equivalent to the source logo after AAPT optimization.
The distribution ZIP passes CRC and its APK matches release.json/SHA256SUMS.txt.
The android-x64 and all-x64 solution dispatch plans pass. The Android x64
compile-only check also completed with zero errors; no x64 emulator playtest was
performed. The combined publication was subsequently executed successfully as
documented below.
At the user’s explicit request, diagnostic version 14 was uninstalled from the
Pixel and permanent release 16 was installed. The APK hash was verified on the
phone before uninstalling. Installation/version checks passed, resources finished
preparing and GameActivity resumed. The user confirmed that release 16 works normally on Pixel 10, including movement,
absence of scenery flashes and visibility of large ships.

Full local all-x64 validation (2026-11-12): the exact root-level command
`dotnet publish -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -r all-x64`
completed with exit code 0 after correcting desktop Git revision propagation.
The dispatcher resolves the revision using a repository-scoped safe.directory
argument and passes CommitSha explicitly, avoiding ownership-error text being
stored as assembly metadata. No global Git configuration was changed.

All six publishes succeeded: Linux x64 server/client, Windows x64
server/client/editor, and the full permanently signed Android ARM64 APK 17.
Published desktop apphosts have verified ELF/PE x86-64 headers; SHA-256 values
are recorded in the local validation report. APK 17 passes apksigner v1/v2/v3,
16 KiB ZIP alignment, ABI/icon/full-resource checks, and ZIP CRC/hash checks.
Existing compiler/dependency warnings remain. This verifies compilation and
publication; desktop applications were not launched as part of this check.

The new clients and APK 17 use the freshly compiled server's network key
(SHA-256 570EB56B5ED6B8739FF5B38CE51A7A82BAE82655AD6FD69A20EF1663C8852292),
which differs from the currently deployed server key. Keep distributing the
user-validated APK 16 with the current deployed server. APK 17 belongs with the
new compiled server/client set; no server deployment or Pixel update was performed.
The APK 16 SHA-256 was rechecked and remains unchanged. Detailed local evidence:
`.local/publish-all-x64-full-retry.log` and `.local/publish-all-x64-validation.json`.

## Grouped desktop/mobile distribution and explicit mobile actions

The root README now contains the complete Windows/Linux/Android/all-x64 commands
and the reproducible grouping command `python scripts/package-cr2026-distribution.py`.
The grouping script combines complete APK resources with the desktop publication
resource overlays. This preparation found two missing files and 209 newer resource
files in desktop outputs; the synchronized clients contain 8,328 resource files.
Windows/Linux receive matching Steamworks.NET 2024.8.0 native libraries and their
licenses. Linux archives preserve executable permissions and include jouer.sh.

Player clients/archives and administration-only server binaries are separated in
artifacts/distribution. Server world/account databases are not included; published
appsettings templates are under Configuration-exemples so existing server settings
can be retained. No live server is deployed by packaging.

Android inventory, equipped-item and spell-book taps now pin the existing detailed
information panel with contextual action and Close buttons. Equipment supports
equip/unequip, consumables/events use the existing item-use path, bags open, spell
items learn/quick-cast, and learned spells cast using the existing player APIs.
Action labels are in EN/FR/ES. The action row is placed above long descriptions;
hide-on-hover-leave is suppressed while pinned. Closing the owner hides the panel.
Selected slot/instance checks prevent acting on a changed inventory/spell slot.
The action is one-shot, closes its panel and retains player/server cooldown and
permission checks. Android hotbar shortcuts activate with one tap. Desktop input
bindings retain their existing behavior. UI tests exercise actual 2x canvas taps,
contextual labels, stale guards, duplicate prevention and pinned description lifetime.

Bundle APK20 targets the newly compiled server key. A separate permanently signed
APK21 was built with the current live server public key, using -OutputDirectory
.local/android-phone21 so the grouped APK is preserved. APK21 was updated on the
Pixel via pm install -r, versionCode21 confirmed, and its resources finished loading.
No uninstall was needed for this update. Manual testing of the new actions is pending.

Final grouping validation: artifacts/distribution/CR2026-0.8.0-pack20 contains
Joueurs (Windows/Linux/Android20), Archives-joueurs and administration-only Serveur.
All 8,328 resources are hash-identical across the three clients. The Windows ZIP
passes CRC; the Linux tar includes executable modes for jouer.sh and the apphost.
Desktop binary hashes match the successful all-x64 build; grouped APK20 signature
v1/v2/v3 matches the permanent release certificate. No private keys/account/world
DB files are present in player folders; server binaries exclude game databases.
README.md contains the full commands and deployment/key compatibility information.
APK21 (same mobile features/resources, live-server public key) was installed on
Pixel by pm install -r, version21 confirmed and resource preparation completed.
The grouped APK20 remains intact for the corresponding newly compiled server.

## Mobile action caption readability follow-up

Pixel testing of APK21 confirmed the inventory action works, but both the primary
action and Close captions were unreadable. Spell descriptions also displayed their
two buttons with unreadable captions. The action buttons now explicitly select
the bundled Source Sans Pro Black font at size 14, use white text over an opaque
dark background with a gold border, and size their widths from measured captions.
The UI harness passes with dark skin colors, EN/FR/ES captions and actual 2x taps.
APK22 uses the compiled-server public key; its permanent release signature was
verified with APK signature schemes v1/v2/v3. APK23 is the corresponding live-server
test variant. On-device caption readability and spell casting still need confirmation.

The updated distribution folder is artifacts/distribution/CR2026-0.8.0-pack22.
Its desktop clients/archives and server binaries are unchanged; APK22 replaces
APK20. All 8,328 APK resource entries retain their previous sizes/CRCs, and the
new APK hash matches release.json. APK23 compiled successfully with the live-server
key and its file hash/permanent v1/v2/v3 signature were verified. It is not yet
installed: the Pixel is absent from both USB ADB and automatic Wi-Fi discovery.
The Pixel was subsequently reconnected over USB. APK23 was transferred, its
on-device SHA-256 verified, and installed successfully with pm install -r.
versionCode23/versionName0.8.0 were confirmed and LauncherActivity started normally.
The transferred temporary APK was removed. The player confirmed on Pixel 10 that
all item, spell and Close button captions are now readable in APK23. The grouped
APK22 contains the same typography/contrast correction. Actual spell execution
has not yet been separately confirmed.

## Fullscreen in-game windows on mobile

Mobile windows use the physical safe area converted to logical canvas coordinates.
Both Window subclasses and legacy WindowControl panels receive the presentation;
non-closable HUD panels are excluded, while transaction-locked inventory remains
available. Existing content and logical owners are preserved inside a clipped,
scrollable viewport. A fixed 48-unit header keeps the close target accessible.
The header selector switches between simultaneously open panels without closing
the bank/trade session. Existing localized window titles provide selector labels.

Touch scroll gestures defer row activation until release; a swipe suppresses the
click and scrolls the nearest list, then the outer viewport when the list reaches
its edge. Native scrollbar/slider dragging remains available. ToCanvas now follows
the inner panel's actual ancestry so nested viewport offsets match ToLocal and
physical hit testing. The UI harness passes with scaled clicks after scrolling,
fixed safe bounds/header, dynamic content, narrow-screen horizontal overflow,
linked-window switching and original close callbacks. Pixel playtesting is pending.

APK24 stopped at compilation because Canvas resolved to the inherited property
instead of the control type in an Android-only pattern. The qualified type fixes
this; APK25 (live-server key) and APK26 (compiled-server key) compile with zero
errors. APK25 signature v1/v2/v3 matches the permanent release certificate. Its USB
transfer was interrupted before installation; Pixel remains on the validated APK23.
APK26 is prepared separately. The validated grouped pack22 is retained until
fullscreen playtesting succeeds; no server deployment was performed.

The subsequent Wi-Fi pairing succeeded and the test Pixel became
available. APK25 was transferred completely; the on-device SHA-256 matched
693E6DF822B3D739CD33E1C254BBCD0B4B5DD5FB8BAB0D4B0040578C28B8E6E1.
pm install -r returned Success, versionCode25/versionName0.8.0 were confirmed,
and LauncherActivity started normally. The transferred temporary APK was removed.
Fullscreen shop/naval scrolling and header window switching await player feedback.

APK25 playtesting exposed an exit regression: opening a shop also opened inventory
in front of it, while transaction inventory disables the native close icon. APK26
has the same issue and is not approved for distribution. The follow-up removes
automatic inventory opening for the Android merchant (its Sell column already
provides inventory), opens linked inventory before bank/crafting/trade panels, and
adds an explicit localized mobile Close button independent of the native icon.
Fullscreen presentation forces the header visible. Closing transaction inventory
ends its shop/bank/bag/trade session through existing close APIs before hiding it;
active crafting retains its existing close guard. A scaled-tap regression test
covers a hidden desktop titlebar with native close disabled. APK27 is the next
live-server test candidate; validated distribution pack22 remains unchanged.

APK27 compiled with zero errors. The UI harness passes, including an actual 2x
tap on the mobile Close button with the native close disabled and titlebar hidden.
Its live-server key, file hash and permanent release certificate were verified;
the complete transfer and on-device hash check passed. pm install -r returned
Success, versionCode27/versionName0.8.0 were confirmed and LauncherActivity started.
The temporary transferred APK was removed. Shop opening/closing on Pixel awaits
player feedback. Do not use canonical APK26 for distribution; it contains the
previous regression. Prepare the corresponding compiled-server successor after
APK27 playtesting, then update the validated grouped distribution.

The player subsequently confirmed in APK27 that the merchant opens directly and
the mobile Close button returns to the game. This validates the reported shop /
locked-inventory exit regression. Naval/other-panel scrolling and linked-window
switching still need separate on-device confirmation; their software tests pass.
APK28 is being prepared with the compiled-server key for the grouped distribution.

## Emote HUD placement and explicit Android Quit

The player confirmed the APK27 merchant fix, then reported that Interact covers
the emote picker and that in-game Quit only backgrounds the Android app. APK28
was built for the compiled server but has not replaced the validated pack22;
the new fixes are first prepared as live-server APK29.

The mobile emote picker uses a larger 136x36 target/font12 above the chat. Its
placement avoids the actual action-button and joystick bounds within the safe
viewport. Software tests cover overlap avoidance on three viewport geometries.

MonoGame's Android platform Exit backgrounds the task (platform source:
https://github.com/MonoGame/MonoGame/blob/v3.8.4/MonoGame.Framework/Platform/Android/AndroidGamePlatform.cs).
Explicit client Quit now skips the duplicate early GWEN teardown, performs normal
network/UI/renderer cleanup once, and posts FinishAndRemoveTask on the UI thread
(Android API: https://developer.android.com/reference/android/app/Activity#finishAndRemoveTask()).
After Activity destruction/disposal, the explicitly quitting client process ends
so relaunch cannot reuse disposed static graphics/network state. This final step
is guarded by an explicit Quit flag; Home/backgrounding does not set it.
On-device exit/task removal and fresh relaunch still need verification.

APK29 compiled with zero errors, and the UI harness passed. Its live-server key,
SHA256 and permanent signature were verified. A slow Wi-Fi transfer was replaced
by USB; the competing transfer cleanup removed a shared temporary filename, so a
distinct USB temporary path was used. That transfer completed, its phone hash
matched, pm install -r returned Success, versionCode29 was confirmed and the game
launched. The temporary file was removed; the helper now generates unique names
per transfer. Emote accessibility and Quit/fresh relaunch await player feedback.
The matching compiled-server APK30 is built and signature-verified; pack22 is
retained until the new phone checks finish.

The player confirmed on Pixel10/APK29 that emotes are unobstructed and accessible,
explicit Quit returns to Android, and the game relaunches normally. APK30 contains
the same source with the compiled-server public key. The grouped distribution is
updated to artifacts/distribution/CR2026-0.8.0-pack30; desktop clients, archives,
server binaries and the synchronized resource set are preserved. Only the Android
package and its metadata/manifests are replaced. Pixel retains live-server APK29.
Naval/other-panel scrolling and the window selector still lack separate manual
confirmation; automated interface tests cover those layouts and interactions.

## Emote dropdown closes on its first click (shared PC/mobile fix)

The player reported that the first emote dropdown click opens then immediately
closes it on both Android and PC. The click requests refreshed menu entries; the
reply clears/repopulates the ComboBox, whose automatic first-item selection hides
the open menu. ApplyEmotes now preserves whether the menu was open when the reply
arrives and restores that state after repopulation. It leaves dismissed menus
closed and does not execute an emote during automatic selection.
Regression tests use actual PC/2x taps and real ApplyEmotes packets, including
one-shot row selection, dismissed late replies and empty responses. APK31 is the
live-server test candidate; Windows/Linux clients and a matching compiled-server
APK are rebuilt so the grouped distribution includes the shared correction.

The player confirmed APK31 keeps the emote dropdown open on the first tap.
Windows/Linux clients were republished successfully and their resource overlays
match the canonical complete resources. Player archives were rebuilt; ZIP CRC,
native binary hashes/formats, duplicate-free members and Linux executable modes
were verified. The grouped distribution was advanced through pack32 to pack34.

## Native chat keyboard hookup

Chat previously focused its Gwen TextBox without requesting the Android keyboard.
Android chat taps and chat-focus actions now call the existing native keyboard
provider, like the login fields. The callback preserves the draft, positions the
cursor, guards against disposed controls/changed scenes, and does not send a
message automatically; the existing Send button/Enter chooses submission/channel.
The native provider exposes its visible state to avoid duplicate dialogs.

APK33 (current live-server key) and APK34 (compiled-server key) compile with zero
errors and their permanent v1/v2/v3 release signatures are verified. Pack34 retains
the newly rebuilt Windows/Linux archives and matching server binaries. Pixel
still has APK31: installation attempts for APK33 never started because automatic
permission review repeatedly expired, including a user-requested retry. The
restricted ADB fallback could not access its Android profile. Native keyboard
display/draft handling therefore remain unverified on device. No server deployment
or external publication was performed.

Permission review subsequently recovered. After the player reconnected the Pixel,
the streamed installation failed, then a distinct staged APK copy completed at
25.3MB/s. The phone SHA256 matched, pm install-r returned Success, versionCode33
was confirmed and LauncherActivity started. The staged file was removed. Pixel
now has APK33; native chat keyboard/draft confirmation is pending. Pack34 hashes,
desktop executable hashes/archives, signatures, synchronized resources and matching
server binaries are verified. No server deployment or external publication occurred.

## Direct Send from the mobile chat keyboard

The player confirmed the native keyboard opens in APK33, and requested sending
directly from its validation button instead of requiring a separate chat Send tap.
The chat-only dialog now labels that button with the existing localized Send
caption and invokes TrySendMessage once. This retains the selected channel,
empty-message checks and cooldowns. Chat cancellation returns a distinct null
result, preserves the original draft and never sends it. Both paths release chat
focus so movement controls return. Other native keyboard callers retain Okay and
their original cancel behavior. APK35 is the next live-server phone candidate;
APK36 is the matching compiled-server distribution variant.

APK35 was installed on Pixel using adb install --no-streaming -r after permanent
signature verification. Installation returned Success, versionCode35 was confirmed
and LauncherActivity started. APK36 compiled successfully for the matching server
set; the grouped folder advances to pack36, preserving the verified desktop
archives/server binaries. The player subsequently confirmed on Pixel/APK35 that
Send transmits directly and Cancel sends nothing. This result is recorded in
pack36/VALIDATION.json and the root README. The distribution targets its included
compiled servers; the installed phone variant targets the currently deployed server.
