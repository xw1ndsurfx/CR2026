using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Intersect.Framework.Core.Tutorials;
using GdiGraphics = System.Drawing.Graphics;

namespace Intersect.Editor.Forms.Tutorials;

/// <summary>
/// Lightweight illustrated preview for the editor, not a screenshot or a map
/// renderer. Positions are intentionally schematic; actual targets resolve
/// against loaded game controls, entities and map coordinates at runtime.
/// </summary>
internal sealed class RoyalGuidePreview : Control
{
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 70 };
    private TutorialStep? _step;
    private string _title = "";

    internal RoyalGuidePreview()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(23, 24, 28);
        _animation.Tick += (_, _) => Invalidate();
        _animation.Start();
    }

    internal void ShowStep(TutorialStep? step, string title)
    {
        _step = step;
        _title = title;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.Stop();
            _animation.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        if (Width < 260 || Height < 180)
            return;

        // Fixed reference stage with responsive aspect-ratio preserving scale.
        const float stageW = 800, stageH = 465;
        var scale = Math.Min((Width - 24f) / stageW, (Height - 24f) / stageH);
        g.TranslateTransform((Width - stageW * scale) / 2, (Height - stageH * scale) / 2);
        g.ScaleTransform(scale, scale);

        using var background = new SolidBrush(Color.FromArgb(41, 57, 51));
        using var gridPen = new Pen(Color.FromArgb(32, 215, 205, 155), 1);
        using var goldPen = new Pen(Color.FromArgb(250, 208, 120), 4);
        using var goldBrush = new SolidBrush(Color.FromArgb(252, 209, 118));
        using var textBrush = new SolidBrush(Color.FromArgb(248, 240, 217));
        using var darkBrush = new SolidBrush(Color.FromArgb(34, 27, 26));
        using var titleFont = new Font("Segoe UI Semibold", 15);
        using var labelFont = new Font("Segoe UI", 10);
        using var tinyFont = new Font("Segoe UI", 8);

        g.FillRectangle(background, 0, 0, stageW, stageH);
        for (var x = 18; x < 800; x += 32)
            g.DrawLine(gridPen, x, 32, x, stageH);
        for (var y = 46; y < 465; y += 32)
            g.DrawLine(gridPen, 0, y, stageW, y);

        // Decorative landscape: intentionally no false claim of previewing an actual map.
        using (var terrain = new SolidBrush(Color.FromArgb(52, 80, 63)))
            g.FillEllipse(terrain, 160, 75, 520, 340);
        using (var trail = new Pen(Color.FromArgb(136, 120, 86), 26))
        {
            g.DrawBezier(trail, new Point(180, 420), new Point(310, 370), new Point(420, 150), new Point(625, 84));
        }

        // Representative player and nearby NPC.
        DrawActor(g, 390, 280, Color.FromArgb(215, 224, 231));
        DrawActor(g, 573, 170, Color.FromArgb(221, 182, 112));
        g.DrawString("Joueur", tinyFont, textBrush, 371, 303);
        g.DrawString("PNJ", tinyFont, textBrush, 562, 195);

        using (var panel = new SolidBrush(Color.FromArgb(228, 42, 29, 26)))
        {
            g.FillRectangle(panel, 0, 0, stageW, 38);
            g.FillRectangle(panel, 0, 44, 142, 290);
            g.FillRectangle(panel, 610, 46, 180, 124);
        }
        g.DrawString("CORPS ROYAUX  •  GUIDE ROYAL", titleFont, textBrush, 17, 7);
        var labels = new[] { "Personnage", "Inventaire", "Quêtes", "Carte", "Sorts", "Guilde", "Menu" };
        for (var i = 0; i < labels.Length; i++)
        {
            g.DrawRectangle(new Pen(Color.FromArgb(94, 70, 53)), 10, 54 + i * 37, 120, 32);
            g.DrawString(labels[i], labelFont, textBrush, 18, 62 + i * 37);
        }
        g.DrawString("MINI-CARTE", labelFont, textBrush, 627, 55);
        g.DrawEllipse(goldPen, 668, 84, 64, 64);

        var highlight = _step?.HighlightTarget ?? TutorialHighlightTarget.None;
        var pulse = (Math.Sin(Environment.TickCount64 / 230d) + 1d) / 2d;
        var padding = (float)(pulse * 5);
        RectangleF rect = highlight switch
        {
            TutorialHighlightTarget.Menu => new(8, 54 + 6 * 37, 124, 35),
            TutorialHighlightTarget.Inventory => new(8, 54 + 1 * 37, 124, 35),
            TutorialHighlightTarget.Quests => new(8, 54 + 2 * 37, 124, 35),
            TutorialHighlightTarget.Character => new(8, 54, 124, 35),
            TutorialHighlightTarget.Minimap or TutorialHighlightTarget.WorldMap => new(625, 76, 124, 87),
            TutorialHighlightTarget.Hotbar => new(282, 424, 238, 28),
            TutorialHighlightTarget.Player => new(365, 254, 50, 55),
            TutorialHighlightTarget.WorldNpc => new(547, 142, 54, 60),
            TutorialHighlightTarget.WorldTile => new(450, 230, 62, 47),
            _ => new(0, 0, 0, 0),
        };
        if (rect.Width > 0)
        {
            using var glow = new Pen(Color.FromArgb((int)(110 + pulse * 135), 255, 183, 66), 7);
            rect.Inflate(padding + 2, padding + 2);
            if (highlight is TutorialHighlightTarget.Minimap or TutorialHighlightTarget.WorldMap or
                TutorialHighlightTarget.WorldNpc or TutorialHighlightTarget.Player or TutorialHighlightTarget.WorldTile ||
                _step?.HighlightStyle == TutorialHighlightStyle.Circle)
            {
                g.DrawEllipse(glow, rect);
                g.DrawEllipse(goldPen, rect);
            }
            else
            {
                g.DrawRectangle(glow, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawRectangle(goldPen, rect.X, rect.Y, rect.Width, rect.Height);
            }
            if (highlight is TutorialHighlightTarget.WorldNpc or TutorialHighlightTarget.WorldTile)
            {
                g.DrawLine(goldPen, rect.X + rect.Width / 2, rect.Top - 20, rect.X + rect.Width / 2, rect.Top - 7);
                g.FillPolygon(goldBrush, [
                    new PointF(rect.X + rect.Width / 2 - 9, rect.Top - 13),
                    new PointF(rect.X + rect.Width / 2 + 9, rect.Top - 13),
                    new PointF(rect.X + rect.Width / 2, rect.Top + 1)
                ]);
            }
        }

        // Guide card: matches the in-game burgundy/gold palette.
        g.FillRectangle(darkBrush, 165, 340, 420, 96);
        g.DrawRectangle(goldPen, 165, 340, 420, 96);
        g.DrawString("GUIDE ROYAL", tinyFont, goldBrush, 181, 349);
        var title = string.IsNullOrWhiteSpace(_title) ? "Votre prochaine étape" : _title;
        if (title.Length > 49) title = title[..46] + "...";
        g.DrawString(title, labelFont, textBrush, 181, 369);
        g.DrawString(_step?.Objective == TutorialObjective.ReachTile
            ? "Approchez-vous de la case ciblée."
            : highlight == TutorialHighlightTarget.WorldNpc
                ? "Parlez au personnage indiqué."
                : highlight == TutorialHighlightTarget.Player
                    ? "Déplacez-vous pour progresser."
                    : "Suivez l'élément mis en évidence.", tinyFont, textBrush, 181, 393);

        g.DrawString("APERÇU ILLUSTRATIF • positions réelles en jeu", tinyFont, textBrush, 440, 444);
        g.ResetTransform();
    }

    private static void DrawActor(GdiGraphics g, int x, int y, Color color)
    {
        using var brush = new SolidBrush(color);
        using var stroke = new Pen(Color.FromArgb(43, 35, 27), 2);
        g.FillEllipse(brush, x - 11, y - 28, 22, 22);
        g.DrawEllipse(stroke, x - 11, y - 28, 22, 22);
        g.FillRectangle(brush, x - 12, y - 6, 24, 27);
        g.DrawRectangle(stroke, x - 12, y - 6, 24, 27);
    }
}
