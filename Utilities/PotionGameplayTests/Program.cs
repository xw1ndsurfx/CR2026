using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;

var passed = 0;
void Test(string name, Action test)
{
    test(); passed++; Console.WriteLine("PASS potions: " + name);
}
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
var green = new PotionPiece(PotionFamily.Verdant, 1);
var red = new PotionPiece(PotionFamily.Ember, 1);
var blue = new PotionPiece(PotionFamily.Arcane, 1);
PotionSessionState State() => new()
{
    RecipeId = Guid.NewGuid(), RecipeName = "Test brew", RecipeRound = 1, RequiredLevel = 1,
    Board = new int[80], CurrentFirst = PotionStateEncoding.Encode(green),
    CurrentSecond = PotionStateEncoding.Encode(red),
    Requirements = [new() { Family = 0, Level = 2, Needed = 20 }],
};

Test("all four orientations show both pieces from the top to their real landing cells", () =>
{
    foreach (var orientation in Enum.GetValues<PotionPairOrientation>())
    {
        var state = State();
        var view = new PotionBoardPresentation(); view.Accept(state);
        Check(view.BeginDrop(state, 2, orientation, 1000), "Drop rejected");
        var first = view.Sample(1000);
        Check(first.Falls.Length == 2 && first.Falls.All(fall => fall.FromRow < 0), "Piece missing at entry");
        Check(first.Falls.All(fall => first.HiddenCells.Contains(fall.ToRow * 8 + fall.Column)),
            "Final board painted underneath falling pieces");
        var lands = false;
        for (var now = 1000; now <= 3000; now += 10)
        {
            var frame = view.Sample(now);
            if (frame.Falls.Length > 0)
                foreach (var fall in frame.Falls)
                {
                    var row = fall.FromRow + (fall.ToRow - fall.FromRow) * frame.Progress * frame.Progress;
                    Check(row >= fall.FromRow && row <= fall.ToRow, "Fall overshot its landing");
                }
            else if (frame.Board.Count(piece => piece != null) == 2) lands = true;
        }
        Check(lands, "Pieces never visibly reached the board");
    }
});
Test("an early empty server result cannot erase a pair before it lands and merges", () =>
{
    var state = State();
    state.CurrentSecond = state.CurrentFirst;
    state.Board[9 * 8] = state.Board[8 * 8] = state.CurrentFirst;
    var view = new PotionBoardPresentation(); view.Accept(state);
    Check(view.BeginDrop(state, 0, PotionPairOrientation.Vertical, 1000), "Drop rejected");
    var result = State(); result.RecipeId = state.RecipeId; result.Revision = 1;
    view.Accept(result);
    Check(view.IsAnimating(1020) && view.Sample(1020).Falls.Length == 2, "Empty response cleared falling pieces");
    var landed = false; var fused = false;
    for (var now = 1000; now < 4000; now += 10)
    {
        var frame = view.Sample(now);
        if (frame.Falls.Length == 0 && frame.FadingCells.Count == 0 && frame.Board.Count(p => p != null) == 4)
            landed = true;
        if (frame.FadingCells.Count == 4) { Check(landed, "Fusion occurred before landing"); fused = true; }
    }
    Check(landed && fused && view.Sample(4000).Board.All(piece => piece == null), "Merge sequence incomplete");
});
Test("a slow server response cannot make landed ingredients vanish", () =>
{
    var state = State();
    var view = new PotionBoardPresentation(); view.Accept(state);
    Check(view.BeginDrop(state, 0, PotionPairOrientation.Vertical, 0), "Drop rejected");
    Check(!view.IsAnimating(5000) && view.Sample(5000).Board.Count(piece => piece != null) == 2,
        "Pair disappeared while waiting for acknowledgement");
});
Test("gravity after a merge starts at the actual occupied row, never above the board", () =>
{
    var state = State();
    state.CurrentSecond = PotionStateEncoding.Encode(blue);
    state.Board[9 * 8] = state.Board[9 * 8 + 1] = PotionStateEncoding.Encode(green);
    state.Board[8 * 8] = PotionStateEncoding.Encode(red);
    var view = new PotionBoardPresentation(); view.Accept(state);
    Check(view.BeginDrop(state, 2, PotionPairOrientation.Horizontal, 0), "Drop rejected");
    var moved = false;
    for (var now = 0; now < 5000; now += 10)
        if (view.Sample(now).Falls.Any(fall => fall.Piece == red && fall.Column == 0 && fall.FromRow == 8 && fall.ToRow == 9))
            moved = true;
    Check(moved, "Piece above the consumed group teleported instead of falling");
});
Test("rejecting a drop or selecting another recipe restores the authoritative board", () =>
{
    var state = State(); var view = new PotionBoardPresentation(); view.Accept(state);
    view.BeginDrop(state, 0, PotionPairOrientation.Vertical, 0);
    view.Accept(state, reset: true);
    Check(!view.IsAnimating(20) && view.Sample(20).Board.All(piece => piece == null), "Rejected prediction survived");
    view.BeginDrop(state, 0, PotionPairOrientation.Vertical, 100);
    view.Accept(State());
    Check(!view.IsAnimating(120), "Old drop continued into a new recipe");
});
Test("blocked placements do not animate or modify the board", () =>
{
    var state = State();
    for (var row = 0; row < 10; row++) state.Board[row * 8] = PotionStateEncoding.Encode(red);
    var view = new PotionBoardPresentation(); view.Accept(state);
    Check(!view.BeginDrop(state, 0, PotionPairOrientation.Vertical, 0), "Full column accepted");
    Check(!view.IsAnimating(10) && view.Sample(10).Board.Count(piece => piece != null) == 10,
        "Blocked placement changed the presentation");
});
Test("preview steps exactly reproduce authoritative board resolution without mutating their inputs", () =>
{
    var moves = 0;
    for (var seed = 1; seed <= 40; seed++)
    {
        var recipe = new PotionRecipe("Recipe", [new(PotionFamily.Verdant, 2, 20)]);
        var puzzle = new PotionPuzzle(seed, recipe);
        var random = new Random(seed);
        for (var move = 0; move < 15 && !puzzle.Complete && !puzzle.GameOver; move++)
        {
            var before = Enumerable.Range(0, 80).Select(i => puzzle.Get(i % 8, i / 8)).ToArray();
            var unchanged = before.ToArray();
            var progress = new[] { puzzle.Progress(0) };
            var oldProgress = progress[0];
            var column = random.Next(8); var orientation = (PotionPairOrientation)random.Next(4);
            var preview = PotionPuzzle.PreviewDrop(recipe, before, puzzle.Current, progress, column, orientation);
            Check(before.SequenceEqual(unchanged) && progress[0] == oldProgress, "Preview mutated live input");
            var actual = puzzle.Drop(column, orientation);
            Check(actual.Success == preview.Result.Success && actual.ScoreGained == preview.Result.ScoreGained &&
                  actual.Merges.SequenceEqual(preview.Result.Merges), "Preview disagrees with server rules");
            if (!actual.Success) continue;
            Check(preview.Steps.Last().Board.SequenceEqual(
                Enumerable.Range(0, 80).Select(i => puzzle.Get(i % 8, i / 8))), "Final board mismatch");
            moves++;
        }
    }
    Check(moves > 300, "Too few successful placements exercised");
});
Console.WriteLine($"{passed}/{passed} potion gameplay groups passed.");
