# Poker window: clipped text regression

## Report and cause

The first user screenshot showed a correctly sized window and button backgrounds, but only
small fragments of labels, cards, button captions and the amount input. The title was intact.
PokerWindow created these controls with FontSize but without assigning Font.

In this engine version, Gwen ControlInternal/Text.Render uses the skin default when Font is
null, but Text.SizeToContents returns early for a null Font. The internal Text control then
retains its default 10x10 bounds and clips the rendered caption. Increasing the outer label
or window size does not fix the missing measurement.

## Fix

Assign the current skin's DefaultFont explicitly before setting text in all Poker labels,
buttons and the amount TextBox. Keep the existing 12/18/22 font sizes and layout. No font files,
new artwork, JSON UI override, packet, gameplay rule or server change is required. The fix is
local to PokerWindow; it does not change text behavior throughout the engine.

An already updated milestone-3 server/editor can remain on f5ad0a9; this fix only requires
rebuilding and replacing the client and its matching client-core dependencies. Never mix the
milestone-3 poker binaries with older pre-poker binaries. No game database edits are needed.

## Regression check

From the repository root, with the engine build prerequisites installed:

```sh
dotnet run --project Utilities/PokerUiTests/Intersect.PokerUiTests.csproj --configuration Debug
```

The executable references the actual client core, creates the real PokerWindow and calls
Gwen's real text layout using synthetic font metrics. It reproduces the missing-font case,
checks all poker labels/buttons/input at four canvas sizes, and checks dynamic/disabled button
text and focus cleanup. It is run after the client build in the integration workflow.

These are headless measurement tests, not pixel comparisons, real font rendering, scrolling
interaction, or a two-client playtest. Check CI for the exact commit before reporting success.
The user should reopen the table in the rebuilt client and verify the captions, cards, amount
input and buttons at their normal resolution. The screenshot confirms the previous defect;
a post-fix in-game screenshot has not yet been supplied. All stakes remain temporary test chips.
