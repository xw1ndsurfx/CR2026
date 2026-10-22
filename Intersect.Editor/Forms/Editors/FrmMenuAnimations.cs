using System.ComponentModel;
using DarkUI.Forms;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Animations;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;

public sealed class FrmMenuAnimations : DarkForm
{
    public FrmMenuAnimations()
    {
        Text = "Main Menu animations";
        Size = new Size(1100, 600);
        MinimumSize = new Size(850, 450);
        StartPosition = FormStartPosition.CenterParent;
        var draft = JsonConvert.DeserializeObject<List<MenuAnimationDefinition>>(JsonConvert.SerializeObject(ClientConfiguration.Instance.MenuAnimations)) ?? [];
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Name="MenuAnimationsGrid", SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=true, AllowUserToDeleteRows=true };
        var animations = AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a => a.Name).ToList();
        grid.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Animation", DataPropertyName = "AnimationId", DataSource = animations, DisplayMember = "Name", ValueMember = "Id" });
        foreach (var name in new[] { "XPercent", "YPercent", "ScalePercent", "TransparencyPercent" })
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, DataPropertyName = name });
        foreach (var name in new[] { "Loop", "Foreground" })
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = name, DataPropertyName = name });
        var binding=new BindingList<MenuAnimationDefinition>(draft);
        grid.DataSource = binding;
        grid.DataError += (_, e) => e.ThrowException = false;
        var help = new Label { Dock = DockStyle.Top, Height = 60, Text = "Choose an existing animation. X/Y are screen percentages (50 = center). Foreground appears above menu graphics; clicks pass through. Definitions are copied into this installation's resources/config.json so they work before login. Distribute config.json and animation textures with the client." };
        string T(string source)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var actions=new FlowLayoutPanel {Dock=DockStyle.Top,Height=40,Padding=new Padding(4)};
        var remove=new Button {Name="DeleteMenuAnimation",Text=T("Remove selected animation"),AutoSize=true,Height=30};
        void UpdateRemove()=>remove.Enabled=grid.Rows.Cast<DataGridViewRow>().Any(row=>!row.IsNewRow);
        remove.Click+=(_,_)=>
        {
            var selected=grid.SelectedRows.Cast<DataGridViewRow>().Where(row=>!row.IsNewRow).ToArray();
            if(selected.Length==0 && grid.CurrentRow is {IsNewRow:false} current)selected=[current];
            var entries=selected.Select(row=>row.DataBoundItem).OfType<MenuAnimationDefinition>().Distinct().ToArray();
            grid.CancelEdit();BindingContext[binding].CancelCurrentEdit();
            foreach(var entry in entries)binding.Remove(entry);
            UpdateRemove();
        };
        grid.RowsAdded+=(_,_)=>UpdateRemove();grid.RowsRemoved+=(_,_)=>UpdateRemove();
        actions.Controls.Add(remove);UpdateRemove();
        var weatherDraft=JsonConvert.DeserializeObject<MenuWeatherDefinition>(JsonConvert.SerializeObject(ClientConfiguration.Instance.MenuWeather))??new();
        var weather=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=110,AutoScroll=true};
        var fog=new ComboBox {Width=190,DropDownStyle=ComboBoxStyle.DropDownList};fog.Items.Add("");
        foreach(var texture in Intersect.Editor.Content.GameContentManager.FogTextures)fog.Items.Add(Path.GetFileName(texture.GetPath()));
        fog.SelectedItem=weatherDraft.Fog;if(fog.SelectedIndex<0)fog.SelectedIndex=0;
        var opacity=new NumericUpDown {Width=70,Minimum=0,Maximum=255,Value=Math.Clamp(weatherDraft.FogOpacity,0,255)};
        var rain=new ComboBox {Width=220,DropDownStyle=ComboBoxStyle.DropDownList};rain.Items.Add("");
        foreach(var animation in animations)rain.Items.Add(animation.Name);
        rain.SelectedIndex=Math.Max(0,animations.FindIndex(animation=>animation.Id==weatherDraft.WeatherAnimationId)+1);
        weather.Controls.AddRange([new Label {Text=T("Menu fog"),Width=100},fog,new Label {Text=T("Opacity"),Width=80},opacity,new Label {Text=T("Weather animation"),Width=140},rain]);
        var wind=new NumericUpDown {Width=80,Minimum=-500,Maximum=500,Value=Math.Clamp(weatherDraft.WeatherSpeedX,-500,500)};
        var fall=new NumericUpDown {Width=80,Minimum=-500,Maximum=500,Value=Math.Clamp(weatherDraft.WeatherSpeedY,-500,500)};
        weather.Controls.AddRange([new Label {Text=T("Weather drift X"),Width=140},wind,new Label {Text=T("Weather drift Y"),Width=140},fall]);
        var brightness=new NumericUpDown {Name="MenuBrightness",Width=80,Minimum=0,Maximum=100,Value=Math.Clamp(weatherDraft.Brightness,0,100)};
        weather.Controls.AddRange([new Label {Text=T("Menu brightness (%)"),Width=155},brightness]);
        var save = new Button { Dock = DockStyle.Bottom, Height = 40, Text = "Save client menu animations" };
        save.Click += (_, _) =>
        {
            grid.EndEdit();
            BindingContext[grid.DataSource].EndCurrentEdit();
            if (draft.Count > 32 || draft.Any(r => r.XPercent is < 0 or > 100 || r.YPercent is < 0 or > 100 || r.ScalePercent is < 1 or > 2000 || r.TransparencyPercent is < 0 or > 100))
            {
                MessageBox.Show(this, "Use X/Y 0-100, scale 1-2000 and transparency 0-100 (max 32 animations).");
                return;
            }
            foreach (var rule in draft)
            {
                if (AnimationDescriptor.Get(rule.AnimationId) is { } animation)
                {
                    rule.Lower = JsonConvert.DeserializeObject<AnimationLayer>(JsonConvert.SerializeObject(animation.Lower));
                    rule.Upper = JsonConvert.DeserializeObject<AnimationLayer>(JsonConvert.SerializeObject(animation.Upper));
                }
                else if (rule.Lower == null && rule.Upper == null)
                {
                    MessageBox.Show(this, "Select an existing animation for every row.");
                    return;
                }
            }
            weatherDraft.Brightness=(int)brightness.Value;
            weatherDraft.WeatherSpeedX=(int)wind.Value;weatherDraft.WeatherSpeedY=(int)fall.Value;
            weatherDraft.Fog=fog.SelectedItem?.ToString()??"";weatherDraft.FogOpacity=(int)opacity.Value;
            weatherDraft.WeatherAnimationId=rain.SelectedIndex>0?animations[rain.SelectedIndex-1].Id:Guid.Empty;
            var weatherAnimation=AnimationDescriptor.Get(weatherDraft.WeatherAnimationId);
            weatherDraft.Lower=weatherAnimation==null?null:JsonConvert.DeserializeObject<AnimationLayer>(JsonConvert.SerializeObject(weatherAnimation.Lower));
            weatherDraft.Upper=weatherAnimation==null?null:JsonConvert.DeserializeObject<AnimationLayer>(JsonConvert.SerializeObject(weatherAnimation.Upper));
            ClientConfiguration.Instance.MenuWeather=weatherDraft;
            ClientConfiguration.Instance.MenuAnimations = draft;
            ClientConfiguration.Instance.Save();
            Close();
        };
        Controls.Add(grid);
        Controls.Add(actions);
        Controls.Add(help);
        Controls.Add(weather);
        Controls.Add(save);
        MiniGameEditorTheme.Apply(this);
    }
}
