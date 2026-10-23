using Intersect.Editor.Core;
using Intersect.Framework.Core;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Utilities;

namespace Intersect.Editor.Forms;

public partial class FrmAbout : Form
{
    private static string T(string source) => QualityOfLifeText.Get(source,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public FrmAbout()
    {
        InitializeComponent();
        Icon = Program.Icon;
        BuildAboutLayout();
    }

    private void BuildAboutLayout()
    {
        // Preserve the upstream copyright and GPL notice verbatim from the original resource.
        var upstreamNotice = label1.Text;
        Controls.Clear();
        Text = T("About") + " — Logiklik 2D Engine";
        ClientSize = new Size(800, 610);
        MinimumSize = new Size(720, 560);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        BackColor = System.Drawing.Color.FromArgb(18, 23, 31);
        ForeColor = System.Drawing.Color.Gainsboro;
        Font = new Font("Segoe UI", 10);

        var header = new Panel { Dock = DockStyle.Top, Height = 180, Padding = new Padding(20) };
        picLogo.BackgroundImage = null;
        using (var stream = typeof(FrmAbout).Assembly.GetManifestResourceStream("Intersect.Editor.studio-logiklik.png"))
            if (stream != null) { using var image = Image.FromStream(stream); picLogo.Image = new Bitmap(image); }
        picLogo.BackColor = System.Drawing.Color.FromArgb(240, 244, 249);
        picLogo.SizeMode = PictureBoxSizeMode.Zoom;
        picLogo.SetBounds(20, 16, 150, 148);
        var title = new Label { Text = "Logiklik 2D Engine", Font = new Font("Segoe UI", 23, FontStyle.Bold),
            AutoSize = false, ForeColor = System.Drawing.Color.White, Left = 195, Top = 27, Width = 560, Height = 48 };
        var identity = new Label { Text = "Studio Logiklik · Corps Royaux", Left = 198, Top = 84, Width = 550, Height = 30 };
        lblVersion.AutoSize = false;
        lblVersion.SetBounds(198, 119, 550, 28);
        lblVersion.Text = T("Version") + " " + BuildIdentity.Label;
        header.Controls.AddRange([picLogo, title, identity, lblVersion]);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var credits = new TabPage(T("Credits")) { BackColor = BackColor, ForeColor = ForeColor, Padding = new Padding(18) };
        var license = new TabPage(T("License")) { BackColor = BackColor, ForeColor = ForeColor, Padding = new Padding(12) };
        var description = new Label { Dock = DockStyle.Top, Height = 115, Text =
            T("Engine modifications by Studio Logiklik. Based on Intersect, created by Ascension Game Dev and its contributors.") +
            "\n\nCopyright (c) 2020-2025 Ascension Game Dev\n" + T("Modifications: Copyright (c) 2026 Studio Logiklik") };
        var links = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        void Link(string caption, string url)
        {
            var link = new LinkLabel { Text = T(caption), AutoSize = true, Margin = new Padding(0, 8, 0, 10),
                LinkColor = System.Drawing.Color.LightSkyBlue, ActiveLinkColor = System.Drawing.Color.White };
            link.LinkClicked += (_, _) => BrowserUtils.Open(url);
            links.Controls.Add(link);
        }
        Link("Modified engine source code", "https://github.com/xw1ndsurfx/CR2026");
        Link("Original Intersect project and contributors", "https://github.com/AscensionGameDev/Intersect-Engine");
        Link("Ascension Game Dev community", "https://ascensiongamedev.com");
        Link("GNU General Public License v3", "https://www.gnu.org/licenses/gpl-3.0.html");
        credits.Controls.Add(links);
        credits.Controls.Add(description);
        using var licenseStream = typeof(FrmAbout).Assembly.GetManifestResourceStream("Logiklik.Engine.License");
        using var reader = licenseStream == null ? null : new StreamReader(licenseStream);
        var notice = new TextBox { Name = "EngineLicenseNotice", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = BackColor, ForeColor = ForeColor,
            Text = upstreamNotice + "\r\n\r\n" + reader?.ReadToEnd() };
        license.Controls.Add(notice);
        tabs.TabPages.AddRange([credits, license]);
        Controls.Add(tabs);
        Controls.Add(header);
    }

    private void lblWebsite_Click(object sender, EventArgs e) => BrowserUtils.Open("https://ascensiongamedev.com");
    private void frmAbout_Load(object sender, EventArgs e) { }
}
