using System.Text;
using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class PlayerProfileWindow : Window
{
    private readonly Label _headline;
    private readonly Label _subtitle;
    private readonly Label _guildLine;
    private readonly Label _locationLine;
    private readonly Label _titleLine;
    private readonly Label _rankingBonusLabel;
    private readonly Label _expLine;
    private readonly ProfileProgressBar _expBar;
    private readonly List<ImagePanel> _paperdollLayers = new();
    private readonly Base _scroll;
    private readonly Base _profileActions;
    private readonly Button[] _sectionButtons;
    private readonly Button _previousPage,_nextPage;
    private readonly Label _pageLabel;
    private PlayerProfilePacket? _profile;
    private int _section,_page;
    private static string T(string text)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(ClientLanguages.Text(text),ClientLanguages.Current);
    private readonly ImagePanel _playerPreview;
    private readonly Label _previewFallback;
    private static readonly Color Gold = new(a:255,r:236,g:210,b:153);
    private static readonly Color Cream = new(a:255,r:234,g:221,b:202);

    public PlayerProfileWindow(Canvas parent) : base(parent, "Character Information", false, nameof(PlayerProfileWindow))
    {
        SetSize(700, 650);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        _headline = new Label(this, "ProfileHeadline")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 22,
            TextAlign = Pos.Center,
            TextColorOverride = Color.White,
        };
        _headline.SetBounds(246, 55, 414, 38);

        _subtitle = new Label(this, "ProfileSubtitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = new Color(a: 255, r: 205, g: 195, b: 175),
        };
        _subtitle.SetBounds(247, 98, 404, 24);
        Label HeaderLine(string name, int y)
        {
            var label = new Label(this, name)
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 10,
                TextColorOverride = Cream,
                MouseInputEnabled = false,
            };
            label.SetBounds(247, y, 402, 20);
            return label;
        }
        _guildLine = HeaderLine("ProfileGuild", 123);
        _locationLine = HeaderLine("ProfileLocation", 144);
        _titleLine = HeaderLine("ProfileTitle", 165);
        _titleLine.SetBounds(247, 165, 270, 20);
        _rankingBonusLabel = new Label(this, "ProfileRankingExpBonus")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Right | Pos.CenterV,
            TextColorOverride = new Color(a:255,r:148,g:218,b:142),
            MouseInputEnabled = false,
        };
        _rankingBonusLabel.SetBounds(522, 165, 126, 20);
        _rankingBonusLabel.Hide();
        _expLine = HeaderLine("ProfileExp", 188);
        _expBar = new ProfileProgressBar(this, "ProfileExpBar");
        _expBar.SetBounds(248, 213, 400, 10);

        _scroll = new Base(this,"ProfileContent");
        _scroll.SetBounds(27,284,645,244);
        _sectionButtons=new Button[4];
        var sections=new[]{"Stats","Equipped items","Professions","Weapon Masteries"};
        for(var i=0;i<sections.Length;i++)
        {
            var section=i;
            _sectionButtons[i]=NavalManagementWindow.Button(this,"ProfileTab"+i,T(sections[i]),27+i*161,242,155,32,()=>{_section=section;_page=0;RenderSection();});
        }
        _previousPage=NavalManagementWindow.Button(this,"ProfilePreviousPage",T("Previous"),39,531,130,28,()=>{_page--;RenderSection();});
        _nextPage=NavalManagementWindow.Button(this,"ProfileNextPage",T("Next"),526,531,130,28,()=>{_page++;RenderSection();});
        _pageLabel=NavalManagementWindow.Text(this,"ProfilePage","",177,531,340,28,11);_pageLabel.TextAlign=Pos.Center;
        _profileActions=new Base(this,"ProfileActions");_profileActions.SetBounds(27,568,645,36);

        var previewFrame = new ProfileFrame(this, "CharacterPreviewFrame");
        previewFrame.SetBounds(30, 39, 185, 194);
        previewFrame.SendToBack();
        _playerPreview = new ImagePanel(this, "ObservedPlayerSprite")
        {
            MaintainAspectRatio = false,
            ShouldDrawBackground = false,
            MouseInputEnabled = false,
        };
        _playerPreview.SetBounds(62, 62, 122, 140);
        // Paperdolls use the same ordering as the local character renderer.
        // Each image is a separate layer over the observed player's sprite.
        foreach (var slot in Options.Instance.Equipment.Paperdoll.Directions[1])
        {
            var layer = new ImagePanel(this, "ObservedPaperdoll" + _paperdollLayers.Count)
            {
                MaintainAspectRatio = false,
                ShouldDrawBackground = false,
                MouseInputEnabled = false,
            };
            layer.SetBounds(62, 62, 122, 140);
            layer.Hide();
            _paperdollLayers.Add(layer);
        }
        _previewFallback = new Label(this, "PreviewUnavailable")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 11,
            TextAlign = Pos.Center,
            TextColorOverride = Cream,
            Text = Intersect.Client.Localization.ClientLanguages.Text("Preview unavailable"),
            MouseInputEnabled = false,
        };
        _previewFallback.SetBounds(49, 108, 145, 55);

        var headerFrame = new ProfileFrame(this, "ProfileHeaderFrame");
        headerFrame.SetBounds(227, 39, 446, 194);
        headerFrame.SendToBack();

        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    public void Apply(PlayerProfilePacket profile)
    {
        if (!profile.Found)
            return;

        if (profile.OfflineHouseProfile) { ApplyOfflineHouse(profile); return; }
        _headline.Text = profile.Name;
        _subtitle.Text = Intersect.Client.Localization.ClientLanguages.Interpolated($"LEVEL {profile.Level:N0}  |  {Fallback(profile.ClassName)}");
        _guildLine.Text = Intersect.Client.Localization.ClientLanguages.Text("Guild: ") + Fallback(profile.GuildName);
        _locationLine.Text = Intersect.Client.Localization.ClientLanguages.Text("Location: ") + Fallback(profile.MapName);
        _titleLine.Text = Intersect.Client.Localization.ClientLanguages.Text("Title: ") + Fallback(profile.RankingTitle);
        if (!string.IsNullOrWhiteSpace(profile.RankingTitle))
        {
            _rankingBonusLabel.Text = Intersect.Client.Localization.ClientLanguages.Interpolated($"+{profile.RankingExperienceBonusPercent:N0}% EXP");
            _rankingBonusLabel.Show();
        }
        else
        {
            _rankingBonusLabel.Hide();
        }
        var levelThreshold = profile.LevelExperienceRequired;
        var maximum = levelThreshold > 0 ? levelThreshold : profile.Experience;
        _expBar.Progress = maximum > 0
            ? (float)Math.Clamp((double)profile.Experience / maximum, 0d, 1d) : 1f;
        _expLine.Text = levelThreshold <= 0
            ? "EXP: MAX LEVEL"
            : $"EXP: {profile.Experience:N0} / {levelThreshold:N0}  ({_expBar.Progress * 100:0.#}%)";

        _playerPreview.Hide();
        _previewFallback.Show();
        foreach (var layer in _paperdollLayers)
            layer.Hide();
        if (!string.IsNullOrWhiteSpace(profile.Sprite) &&
            GameContentManager.Current.GetTexture(TextureType.Entity, profile.Sprite) is { } texture)
        {
            _playerPreview.Texture = texture;
            var frameWidth = Math.Max(1, texture.Width / Math.Max(1, Options.Instance.Sprites.NormalFrames));
            var frameHeight = Math.Max(1, texture.Height / Math.Max(1, Options.Instance.Sprites.Directions));
            _playerPreview.SetTextureRect(0, 0, frameWidth, frameHeight);
            _playerPreview.Show();
            _previewFallback.Hide();
        }
        var paperdolls = profile.PaperdollSprites ?? [];
        for (var i = 0; i < Math.Min(paperdolls.Length, _paperdollLayers.Count); i++)
        {
            if (string.IsNullOrWhiteSpace(paperdolls[i]))
                continue;
            var paperdollTexture = GameContentManager.Current.GetTexture(TextureType.Paperdoll, paperdolls[i]);
            if (paperdollTexture == null)
                continue;
            var layer = _paperdollLayers[i];
            layer.Texture = paperdollTexture;
            layer.SetTextureRect(0, 0,
                Math.Max(1, paperdollTexture.Width / Math.Max(1, Options.Instance.Sprites.NormalFrames)),
                Math.Max(1, paperdollTexture.Height / Math.Max(1, Options.Instance.Sprites.Directions)));
            layer.Show();
        }

        if(_profile?.PlayerId!=profile.PlayerId){_section=0;_page=0;}
        _profile=profile;
        _profileActions.Show();_profileActions.DeleteAllChildren();
        var observedId=profile.IsSelf?Guid.Empty:profile.PlayerId;
        var actions=new (string Name,string Caption,Action Action)[]{
            ("OpenProfileAchievementsButton","Achievements",()=>PacketSender.SendRequestAchievementState(openWindow:true,playerId:observedId)),
            ("OpenProfileFamiliarsButton","Familiers",()=>PacketSender.SendRequestPetState(openWindow:true,playerId:observedId)),
            ("OpenProfileDungeonsButton","Dungeons",()=>PacketSender.SendRequestDungeonPanel(openWindow:true,playerId:observedId)),
            ("VisitProfileHouse",profile.IsSelf?"My house":"Visit house",()=>PacketSender.SendHouseAction(Intersect.Network.Packets.Housing.HouseAction.Visit,profile.PlayerId))};
        for(var i=0;i<actions.Length;i++)
        {
            var action=actions[i];var button=NavalManagementWindow.Button(_profileActions,action.Name,ClientLanguages.Text(action.Caption),i*158,0,150,32,action.Action);
            if(i==3)button.IsDisabled=!profile.IsSelf && !profile.PublicHouse;
        }
        RenderSection();

        Show();
        BringToFront();
    }

    private void RenderSection()
    {
        if(_profile is not {} profile)return;
        if(Interface.HasInGameUI)Interface.GameUi.ItemDescriptionWindow?.Hide();
        _scroll.DeleteAllChildren();
        var count=_section==1?(profile.Equipment?.Length??0):_section==2?(profile.Professions?.Length??0):_section==3?(profile.Masteries?.Length??0):0;
        var perPage=_section==1?4:_section==3?2:3;var pages=Math.Max(1,(count+perPage-1)/perPage);
        _page=Math.Clamp(_page,0,pages-1);
        for(var i=0;i<_sectionButtons.Length;i++){_sectionButtons[i].Show();_sectionButtons[i].IsDisabled=i==_section;}
        _previousPage.IsHidden=_nextPage.IsHidden=_pageLabel.IsHidden=pages<=1;
        _previousPage.IsDisabled=_page==0;_nextPage.IsDisabled=_page==pages-1;_pageLabel.Text=$"{_page+1} / {pages}";
        if(_section==1){AddEquipmentSection(profile,8);return;}
        if(_section==2){AddProfessionCards(profile,8);return;}
        if(_section==3){AddMasteryCards(profile);return;}
        var stats=Enum.GetValues<Stat>();
        var fields=(profile.Stats??[]).Select((value,index)=>(value,index)).Where(item=>item.index<stats.Length)
            .Select(item=>(Strings.Combat.Stats[stats[item.index]].ToString(),item.value.ToString("N0")))
            .Concat(new[]{(ClientLanguages.Text("PvP grade"),Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(ClientLanguages.ContentText(string.IsNullOrWhiteSpace(profile.PvpGrade)?"Unranked":profile.PvpGrade),ClientLanguages.Current)),
                (ClientLanguages.Text("PvP points"),profile.PvpPoints.ToString("N0"))}).ToArray();
        AddKeyValueSection("STATS",fields,8);
    }

    private void ApplyOfflineHouse(PlayerProfilePacket profile)
    {
        _headline.Text = profile.Name; _subtitle.Text = ClientLanguages.Text("Offline");
        _guildLine.Text = _titleLine.Text = _expLine.Text = string.Empty;
        _locationLine.Text = ClientLanguages.Text(profile.PublicHouse ? "Public house" : "This house is private.");
        _rankingBonusLabel.Hide(); _expBar.Progress = 0;
        _playerPreview.Hide(); foreach (var layer in _paperdollLayers) layer.Hide();
        _previewFallback.Show(); _scroll.DeleteAllChildren();_profile=null;_profileActions.Hide();
        foreach(var button in _sectionButtons)button.Hide();_previousPage.Hide();_nextPage.Hide();_pageLabel.Hide();
        var description = new Label(_scroll) { Text = ClientLanguages.Text("Offline character. House visits are available when public."), FontSize = 11 };
        description.SetBounds(12, 12, 580, 64);
        var visit = new Button(_scroll, "VisitOfflineHouse") { Text = ClientLanguages.Text("Visit house"), Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont, FontSize = 11, IsDisabled = !profile.PublicHouse };
        NavalManagementWindow.StyleButton(visit);
        visit.SetBounds(12, 90, 580, 36); visit.Clicked += (_, _) => PacketSender.SendHouseAction(Intersect.Network.Packets.Housing.HouseAction.Visit, profile.PlayerId);
        Show(); BringToFront();
    }

    private int AddProfessionCards(PlayerProfilePacket profile, int y)
    {
        var professions = (profile.Professions ?? []).OrderBy(p => p.Name).Skip(_page*3).Take(3).ToArray();
        if (professions.Length == 0)
            return AddKeyValueSection("PROFESSIONS", [("Status", "No professions learned")], y);

        foreach (var profession in professions)
        {
            var card=new ProfileFrame(_scroll,"Profession"+profession.ProfessionId);card.SetBounds(12,y,580,64);
            var label=NavalManagementWindow.Text(card,"Name",ClientLanguages.Text(ClientLanguages.ContentText(profession.Name))+" - "+ClientLanguages.Text("Level")+" "+profession.Level+"/"+profession.MaximumLevel,10,4,560,24,12);
            var progress=new ProfileProgressBar(card,"ExperienceBar") {Progress=profession.LevelExperienceRequired<=0?1f:(float)((double)profession.LevelExperience/profession.LevelExperienceRequired)};
            progress.SetBounds(10,34,560,24);
            var caption=NavalManagementWindow.Text(card,"Experience",profession.LevelExperienceRequired<=0?"MAX":Intersect.Framework.Core.Naval.NavalSetupText.Display($"{profession.LevelExperience:N0} / {profession.LevelExperienceRequired:N0} EXP"),10,34,560,24,11);
            caption.TextAlign=Pos.Center;
            y+=72;
        }
        return y+12;
    }
    private void AddMasteryCards(PlayerProfilePacket profile)
    {
        if (!profile.MasteriesAvailable)
        { NavalManagementWindow.Text(_scroll, "MasteriesUnavailable", T("Weapon mastery information is temporarily unavailable."), 12, 12, 600, 60, 11); return; }
        var y = 8;
        foreach (var mastery in (profile.Masteries ?? []).Skip(_page * 2).Take(2))
        {
            var card = new ProfileFrame(_scroll, "Mastery" + mastery.Key); card.SetBounds(12, y, 600, 104);
            NavalManagementWindow.Text(card, "MasteryName", ClientLanguages.Format("{0} · Level {1}/{2} · Points {3}",
                ClientLanguages.ContentText(T(mastery.Name)), mastery.Level, mastery.MaximumLevel, mastery.AvailablePoints), 10, 2, 580, 26, 11);
            var required = mastery.RequiredExperience; var percent = required <= 0 ? 100 : (int)Math.Clamp(mastery.LevelExperience * 100 / required, 0, 100);
            var bar = new ProfileProgressBar(card, "MasteryExperienceBar") { Progress = percent / 100f }; bar.SetBounds(10, 32, 580, 26);
            NavalManagementWindow.Text(card, "MasteryExperience", ClientLanguages.Format("XP {0}/{1} · {2}% · Remaining {3}",
                mastery.LevelExperience, required, percent, Math.Max(0, required - mastery.LevelExperience)), 14, 32, 570, 26, 10);
            NavalManagementWindow.Button(card, "MasteryDetails", T("View talents"), 10, 68, 580, 26, () => {
                if (profile.IsSelf) GameInterface.OpenWeaponMasteries();
                else Intersect.Client.Networking.Network.SendPacket(new Intersect.Network.Packets.Client.ArcheryRequestPacket {
                    Action = Intersect.Network.Packets.Client.ArcheryAction.InspectMasteries, TargetId = profile.PlayerId });
            });
            y += 112;
        }
    }

    internal sealed class ProfileProgressBar : Base
    {
        public float Progress { get; set; }
        public ProfileProgressBar(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
        }

        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var renderer=skin.Renderer;
            var previous=renderer.DrawColor;
            var r=RenderBounds;
            var border=r.Height<12?1:2;
            var innerWidth=Math.Max(0,r.Width-border*2);
            var innerHeight=Math.Max(0,r.Height-border*2);
            var progress=float.IsFinite(Progress)?Math.Clamp(Progress,0f,1f):0f;
            try
            {
                renderer.DrawColor=new Color(a:255,r:135,g:105,b:69);
                renderer.DrawFilledRect(r);
                renderer.DrawColor=new Color(a:255,r:70,g:50,b:37);
                renderer.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(r.X+border,r.Y+border,innerWidth,innerHeight));
                var fill=(int)(innerWidth*progress);
                if(fill>0 && innerHeight>0)
                {
                    renderer.DrawColor=new Color(a:255,r:79,g:175,b:99);
                    renderer.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(r.X+border,r.Y+border,fill,innerHeight));
                }
            }
            finally {renderer.DrawColor=previous;}
        }
    }

    private static string Fallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "None" : value;

    // Each fact has its own row: bitmap fonts can render \n as a missing
    // glyph in large concatenated labels, making the original text unreadable.
    private int AddKeyValueSection(string title, (string Name, string Value)[] fields, int y)
    {
        var heading = new Label(_scroll, title + "Title")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 13,
            TextColorOverride = Gold,
            Text = title,
            MouseInputEnabled = false,
        };
        heading.SetBounds(12, y, 582, 25);
        y += 31;

        for (var i = 0; i < fields.Length; i++)
        {
            var column = i % 2;
            var row = i / 2;
            var left = 12 + column * 295;
            var top = y + row * 46;
            var card = new ProfileFrame(_scroll, title + "Card" + i);
            card.SetBounds(left, top, 285, 40);
            card.SendToBack();
            var label = new Label(_scroll, title + "Name" + i)
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 9,
                Text = fields[i].Name,
                TextColorOverride = new Color(a:255,r:181,g:170,b:155),
                MouseInputEnabled = false,
            };
            label.SetBounds(left + 10, top + 3, 265, 15);
            var value = new Label(_scroll, title + "Value" + i)
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
                Text = fields[i].Value,
                TextColorOverride = Cream,
                MouseInputEnabled = false,
            };
            value.SetBounds(left + 10, top + 19, 265, 18);
        }

        return y + ((fields.Length + 1) / 2) * 46 + 19;
    }

    private sealed class ProfileFrame : Base
    {
        public ProfileFrame(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = false;
        }

        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var rect = RenderBounds;
            void Fill(int x, int y, int width, int height, Color color)
            {
                if (width <= 0 || height <= 0) return;
                skin.Renderer.DrawColor = color;
                skin.Renderer.DrawFilledRect(
                    new Intersect.Client.Framework.GenericClasses.Rectangle(x, y, width, height));
            }
            var border = new Color(a:255,r:154,g:110,b:60);
            Fill(rect.X, rect.Y, rect.Width, rect.Height, new Color(a:255,r:38,g:24,b:22));
            Fill(rect.X, rect.Y, rect.Width, 2, border);
            Fill(rect.X, rect.Bottom - 2, rect.Width, 2, border);
            Fill(rect.X, rect.Y, 2, rect.Height, border);
            Fill(rect.Right - 2, rect.Y, 2, rect.Height, border);
        }
    }

    private int AddSection(string title, string body, int y)
    {
        var titleLabel = new Label(_scroll, title + "Title")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 13,
            TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
            Text = title,
        };
        titleLabel.SetBounds(8, y, 510, 24);
        y += 26;

        var lineCount = Math.Max(1, body.Count(ch => ch == '\n') + 1);
        var bodyHeight = Math.Max(26, lineCount * 20 + 8);
        var bodyLabel = new Label(_scroll, title + "Body")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = Color.White,
            Text = body,
        };
        bodyLabel.SetBounds(18, y, 500, bodyHeight);

        return y + bodyHeight + 16;
    }

    private static string BuildCharacterText(PlayerProfilePacket profile)
    {
        var next = profile.ExperienceToNextLevel < 0
            ? "MAX"
            : profile.ExperienceToNextLevel.ToString("N0");

        var ranking =
            string.IsNullOrWhiteSpace(profile.RankingTitle)
                ? "None"
                : $"{profile.RankingTitle}  •  Rank #{profile.RankingPosition}  •  +{profile.RankingExperienceBonusPercent}% EXP  •  {profile.RankingConsecutiveDays} consecutive days";

        return
            $"Name: {profile.Name}\n" +
            $"Level: {profile.Level:N0}\n" +
            $"Class: {profile.ClassName}\n" +
            $"Guild: {(string.IsNullOrWhiteSpace(profile.GuildName) ? "None" : profile.GuildName)}\n" +
            $"Ranking title: {ranking}\n" +
            $"Experience: {profile.Experience:N0} / {next}";
    }

    private static string BuildStatsText(PlayerProfilePacket profile)
    {
        if (profile.Stats == null || profile.Stats.Length == 0)
            return "No stat information.";

        var builder = new StringBuilder();
        var stats = Enum.GetValues<Stat>();

        for (var index = 0; index < stats.Length && index < profile.Stats.Length; ++index)
        {
            var stat = stats[index];
            if (builder.Length > 0) builder.AppendLine();
            builder.Append($"{Strings.Combat.Stats[stat]}: {profile.Stats[index]:N0}");
        }

        return builder.ToString();
    }

    private int AddEquipmentSection(PlayerProfilePacket profile, int y)
    {
        var titleLabel = new Label(_scroll, "EquipmentTitle")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 13,
            TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
            Text = Intersect.Client.Localization.ClientLanguages.Text("EQUIPMENT"),
        };
        titleLabel.SetBounds(8, y, 510, 24);
        y += 26;

        var hint = new Label(_scroll, "EquipmentHint")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = new Color(a: 255, r: 205, g: 195, b: 175),
            Text = Intersect.Client.Localization.ClientLanguages.Text("Hover an equipped item to view its full description, stats and bonuses."),
        };
        hint.SetBounds(18, y, 580, 20);
        y += 24;

        var equipmentEntries = (profile.Equipment ?? [])
            .OrderBy(entry => entry.SlotIndex)
            .Skip(_page*4).Take(4).ToArray();

        if (equipmentEntries.Length == 0)
        {
            var empty = new Label(_scroll, "EquipmentEmpty")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 10,
                TextColorOverride = Color.White,
                Text = Intersect.Client.Localization.ClientLanguages.Text("No equipment information."),
            };
            empty.SetBounds(18, y, 500, 24);
            return y + 42;
        }

        const int cardWidth = 285;
        const int cardHeight = 62;
        const int horizontalGap = 9;
        const int verticalGap = 8;
        const int startX = 8;

        for (var index = 0; index < equipmentEntries.Length; ++index)
        {
            var equipment = equipmentEntries[index];
            var column = index % 2;
            var row = index / 2;
            var cardX = startX + column * (cardWidth + horizontalGap);
            var cardY = y + row * (cardHeight + verticalGap);

            var card = new Button(_scroll, $"ProfileEquipmentCard{equipment.SlotIndex}")
            {
                Text = string.Empty,
                MouseInputEnabled = true,
            };
            card.SetBounds(cardX, cardY, cardWidth, cardHeight);
            card.SetStateTexture(ComponentState.Normal, "control_button.png");
            card.SetStateTexture(ComponentState.Hovered, "control_button_hovered.png");
            card.SetStateTexture(ComponentState.Active, "control_button_clicked.png");

            var icon = new ImagePanel(card, $"ProfileEquipmentIcon{equipment.SlotIndex}")
            {
                MouseInputEnabled = false,
            };
            icon.SetBounds(8, 9, 44, 44);

            var slotLabel = new Label(card, $"ProfileEquipmentSlot{equipment.SlotIndex}")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 9,
                TextColorOverride = new Color(a: 255, r: 236, g: 210, b: 153),
                Text = equipment.SlotName,
                MouseInputEnabled = false,
            };
            slotLabel.SetBounds(62, 8, cardWidth - 72, 20);

            var itemLabel = new Label(card, $"ProfileEquipmentName{equipment.SlotIndex}")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 10,
                TextColorOverride = Color.White,
                Text = equipment.ItemId == Guid.Empty ? "Empty" : equipment.ItemName,
                MouseInputEnabled = false,
            };
            itemLabel.SetBounds(62, 29, cardWidth - 72, 23);

            if (equipment.ItemId == Guid.Empty ||
                !ItemDescriptor.TryGet(equipment.ItemId, out var itemDescriptor))
            {
                icon.Hide();
                continue;
            }

            if (GameContentManager.Current.GetTexture(
                    itemDescriptor.UsesSpellIcon?Framework.Content.TextureType.Spell:Framework.Content.TextureType.Item, itemDescriptor.DisplayIcon
                ) is { } itemTexture)
            {
                icon.Texture = itemTexture;
                icon.RenderColor = itemDescriptor.Color;
                icon.Show();
            }
            else
            {
                icon.Hide();
            }

            card.HoverEnter += (_, _) =>
            {
                Interface.GameUi.ItemDescriptionWindow?.Show(
                    itemDescriptor,
                    1,
                    equipment.Properties
                );
            };

            card.HoverLeave += (_, _) =>
            {
                Interface.GameUi.ItemDescriptionWindow?.Hide();
            };
        }

        var rows = (equipmentEntries.Length + 1) / 2;
        return y + rows * (cardHeight + verticalGap) + 12;
    }

    private static string BuildProfessionsText(PlayerProfilePacket profile)
    {
        if (profile.Professions == null || profile.Professions.Length == 0)
            return "No professions learned.";

        var builder = new StringBuilder();
        foreach (var profession in profile.Professions.OrderBy(entry => entry.Name))
        {
            if (builder.Length > 0) builder.AppendLine();

            var progress = profession.ExperienceToNextLevel < 0
                ? "MAX"
                : $"{profession.ExperienceToNextLevel:N0} XP to next level";

            builder.Append(
                $"{(Intersect.Framework.Core.Naval.NavalProfessions.IsNative(profession.ProfessionId)?Intersect.Framework.Core.Naval.NavalSetupText.Get(Intersect.Framework.Core.Naval.NavalProfessions.Caption(profession.ProfessionId),ClientLanguages.Current):profession.ProfessionId == Intersect.Framework.Core.Housing.FarmingProfession.Id ? ClientLanguages.Text("Farmer") : profession.Name)}: Level {profession.Level}/{profession.MaximumLevel}  •  " +
                $"{profession.Experience:N0} total XP  •  {progress}"
            );
        }

        return builder.ToString();
    }
}
