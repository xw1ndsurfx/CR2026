using System.Runtime.CompilerServices;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.Framework.Input;
using Intersect.Client.Localization;
using Newtonsoft.Json.Linq;
using Rectangle=Intersect.Client.Framework.GenericClasses.Rectangle;
using UiBase=Intersect.Client.Framework.Gwen.Control.Base;
namespace Intersect.Client.Interface.Shared;

/// <summary>Edits the actual live control tree; the game's renderer remains the preview.</summary>
internal static class UiDesignerRuntime
{
    private static readonly string FileName=Path.Combine("resources","gui","ui-overrides.json");
    private static readonly string Flag=Path.Combine("resources","gui","ui-designer.enabled");
    private static readonly ConditionalWeakTable<Canvas,Designer> Designers=new();
    private static JObject _document=new();
    private static readonly ConditionalWeakTable<UiBase,JObject> Baselines=new();
    private static long _nextRead;
    private static DateTime _written;
    private static bool _enabled;
    private static bool _dirty;
    private static string T(string text)=>ClientLanguages.Text(text);

    internal static IEnumerable<UiBase> Descendants(UiBase root)
    {
        foreach(var child in root.Children.ToArray())
        {
            if(child is Designer)continue;
            yield return child;
            foreach(var nested in Descendants(child))yield return nested;
        }
    }
    private static string NodeName(UiBase control)=>string.IsNullOrWhiteSpace(control.Name)?control.GetType().Name:control.Name;
    internal static string ControlPath(UiBase control)
    {
        var segments=new List<string>();
        for(var node=control;node!=null;node=node.Parent)
        {
            var name=NodeName(node);
            var ordinal=node.Parent?.Children.TakeWhile(child=>child!=node).Count(child=>NodeName(child)==name)??0;
            segments.Add(Uri.EscapeDataString(name)+":"+ordinal);
        }
        segments.Reverse();return string.Join("/",segments);
    }
    internal static UiBase? Resolve(Canvas canvas,string path)
    {
        var prefix=ControlPath(canvas)+"/";
        if(!path.StartsWith(prefix,StringComparison.Ordinal))return null;
        UiBase current=canvas;
        foreach(var segment in path[prefix.Length..].Split('/'))
        {
            var split=segment.LastIndexOf(':');
            if(split<0 || !int.TryParse(segment[(split+1)..],out var ordinal))return null;
            var name=Uri.UnescapeDataString(segment[..split]);
            var next=current.Children.Where(child=>NodeName(child)==name).ElementAtOrDefault(ordinal);
            if(next==null)return null;current=next;
        }
        return current;
    }

    internal static void ApplyControl(UiBase control,JObject properties)
    {
        Baselines.GetValue(control,node=>
        {
            var original=node.GetJson()??new JObject();
            if(node is Label label && node is not TextBox && node is not MultilineTextBox) original["DesignerText"]=label.Text;
            return original;
        });
        // A manual position must survive automatic docking and programmatic window placement.
        if(properties["Bounds"] is {} bounds)
        {
            control.Dock=Pos.None;control.Alignment=[];
            var rectangle=Intersect.Client.Framework.GenericClasses.Rectangle.FromString(bounds.Value<string>()!);
            if(!_enabled && Intersect.Client.Interface.Game.HudPlacement.HasSavedPosition(control))
            {rectangle.X=control.X;rectangle.Y=control.Y;}
            control.SetBounds(rectangle.X,rectangle.Y,Math.Max(1,rectangle.Width),Math.Max(1,rectangle.Height));
        }
        var rest=(JObject)properties.DeepClone();rest.Remove("Bounds");rest.Remove("DesignerText");
        if(rest["_stateSoundNames"] is JObject && control is Intersect.Client.Framework.Gwen.ControlInternal.Dragger dragger)dragger.ClearSounds();
        if(control is ImagePanel image)
        {
            if(rest.ContainsKey("HoverSound"))image.HoverSound=rest["HoverSound"]?.Value<string>();
            if(rest.ContainsKey("LeftMouseClickSound"))image.LeftMouseClickSound=rest["LeftMouseClickSound"]?.Value<string>();
            if(rest.ContainsKey("RightMouseClickSound"))image.RightMouseClickSound=rest["RightMouseClickSound"]?.Value<string>();
        }
        if(rest.HasValues)control.LoadJson(rest);
        if(properties["DesignerText"] is {} text && control is Label label && control is not TextBox && control is not MultilineTextBox) label.Text=text.Value<string>();
        if (!_enabled && control is Label compact && Intersect.Client.Interface.Game.HudPlacement.IsCompactTextHud(compact))
            Intersect.Client.Interface.Game.HudPlacement.FitTextHud(compact, compact.X, compact.Y);
    }
    internal static void Update(Canvas? canvas)
    {
        if(canvas==null)return;
        if(Environment.TickCount64>=_nextRead)
        {
            _nextRead=Environment.TickCount64+1000;_enabled=System.IO.File.Exists(Flag);
            try
            {
                var timestamp=System.IO.File.GetLastWriteTimeUtc(FileName);
                if(!_dirty && timestamp!=_written && System.IO.File.Exists(FileName) && new FileInfo(FileName).Length<4000000)
                { _document=JObject.Parse(System.IO.File.ReadAllText(FileName));_written=timestamp; }
            }
            catch(IOException){}catch(Newtonsoft.Json.JsonException){}
        }
        foreach(var entry in _document.Properties())
            if(entry.Value is JObject properties && Resolve(canvas,entry.Name) is {} control)
                try{ApplyControl(control,properties);}catch(Exception){}
        if(_enabled)
        {
            var designer=Designers.GetValue(canvas,parent=>new Designer(parent));
            designer.SetBounds(0,0,canvas.Width,canvas.Height);designer.Show();designer.BringToFront();designer.RefreshSelection();
        }
        else if(Designers.TryGetValue(canvas,out var existing))existing.Hide();
    }
    private static void Change(UiBase control,string property,JToken value)
    {
        var key=ControlPath(control);
        var properties=(_document[key] as JObject)?.DeepClone() as JObject??new JObject();
        if(property.StartsWith("_stateSoundNames.",StringComparison.Ordinal))
        {
            if(properties["_stateSoundNames"] is not JObject sounds)
                properties["_stateSoundNames"]=sounds=(control.GetJson()?["_stateSoundNames"] as JObject)?.DeepClone() as JObject??new JObject();
            sounds[property[(property.IndexOf('.')+1)..]]=value;
        }
        else properties[property]=value;
        if(property=="Bounds"){properties.Remove("Dock");properties.Remove("Alignments");}
        ApplyControl(control,properties);_document[key]=properties;_dirty=true;
    }
    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
        var temp=FileName+".tmp";System.IO.File.WriteAllText(temp,_document.ToString());System.IO.File.Move(temp,FileName,true);
        _written=System.IO.File.GetLastWriteTimeUtc(FileName);_dirty=false;
    }
    private sealed class Inspector(UiBase parent):Window(parent,T("UI Editor"),false,"LiveUiInspector")
    { protected override void EnsureInitialized(){} }
    internal static UiBase CreateDesigner(Canvas canvas)=>new Designer(canvas);
    private sealed class Designer:UiBase
    {
        private readonly Inspector _inspector;
        private readonly ComboBox _controls,_property,_sounds;
        private readonly TextBox _value;
        private readonly Label _status;
        private UiBase? _selected;
        private bool _selecting=true,_dragging,_resizing;
        private Point _hold;
        private bool _populating;
        internal Designer(Canvas canvas):base(canvas,"UiDesignerOverlay")
        {
            MouseInputEnabled=true;KeyboardInputEnabled=false;ShouldDrawBackground=false;
            Interface.InputBlockingComponents.Add(this);
            Disposed+=(_,_)=>Interface.InputBlockingComponents.Remove(this);
            _inspector=new Inspector(this);_inspector.SetBounds(12,40,320,510);_inspector.DeleteOnClose=false;_inspector.IsClosable=false;
            Button Action(string text,int x,int y,int width,Action action)
            {
                var button=new Button(_inspector,text){Text=T(text),Font=Skin.DefaultFont,FontSize=9};button.SetBounds(x,y,width,28);button.Clicked+=(_,_)=>action();return button;
            }
            Action("Select / interact",12,24,290,()=>{_selecting=!_selecting;MouseInputEnabled=_selecting;});
            _controls=new ComboBox(_inspector,"UiControls"){Font=Skin.DefaultFont,FontSize=10};_controls.SetBounds(12,62,290,28);
            _controls.ItemSelected+=(_,args)=>{if(!_populating && args.SelectedItem.UserData is UiBase control)Select(control);};
            Action("Refresh controls",12,96,190,FillControls);Action("Parent",208,96,94,()=>{if(_selected?.Parent is {} parent && parent!=Canvas)Select(parent);});
            _property=new ComboBox(_inspector,"UiProperty"){Font=Skin.DefaultFont,FontSize=10};_property.SetBounds(12,136,290,28);_property.ItemSelected+=(_,_)=>ShowValue();
            _value=new TextBox(_inspector,"UiPropertyValue"){Text="",Font=Skin.DefaultFont,FontSize=10};_value.SetBounds(12,174,290,70);
            _sounds=new ComboBox(_inspector,"UiSound"){Font=Skin.DefaultFont,FontSize=10};_sounds.SetBounds(12,252,290,28);
            _sounds.AddItem(T("None"),userData:"");
            foreach(var sound in GameContentManager.Current.SoundNames)_sounds.AddItem(sound,userData:sound);
            _sounds.ItemSelected+=(_,args)=>{if(!_populating && args.SelectedItem.UserData is string sound)_value.Text=sound;};
            Action("Apply property",12,290,290,ApplyValue);
            Action("Reset selected",12,328,290,()=>
            {
                if(_selected==null)return;_document.Remove(ControlPath(_selected));_dirty=true;
                if(Baselines.TryGetValue(_selected,out var original))ApplyControl(_selected,original);
                _status.Text=T("Original layout restored.");
            });
            Action("Save UI",12,366,290,()=>{try{Save();_status.Text=T("UI saved.");}catch(Exception){_status.Text=T("Changes could not be saved.");}});
            _status=new Label(_inspector,"UiDesignerStatus"){Text=T("Open an interface, select a control, then drag it."),Font=Skin.DefaultFont,FontSize=9,AutoSizeToContents=false,
                WrappingBehavior=WrappingBehavior.Wrapped};_status.SetBounds(12,408,290,56);
            _inspector.Closed+=(_,_)=>{_selecting=false;MouseInputEnabled=false;};
            FillControls();
        }
        public override bool IsBlockingInput=>_selecting;
        private void FillControls()
        {
            _populating=true;_controls.ClearItems();
            foreach(var control in Descendants(Canvas!).Where(control=>!IsOurControl(control)))
                _controls.AddItem(control.Name+" · "+control.GetType().Name,userData:control);
            _populating=false;
        }
        private bool IsOurControl(UiBase control)
        {for(var node=control;node!=null;node=node.Parent)if(node==this)return true;return false;}
        private void Select(UiBase control)
        {
            if(IsOurControl(control))return;
            _selected=control;_populating=true;_property.ClearItems();
            foreach(var property in (control.GetJson()??new JObject()).Properties().Where(p=>p.Name is not ("Children" or "InnerPanel" or "_stateSoundNames")))
                _property.AddItem(property.Name,userData:property.Name);
            if(control is Intersect.Client.Framework.Gwen.ControlInternal.Dragger)
                foreach(var state in new[]{"Hover","MouseDown","MouseUp"})_property.AddItem(T(state+" sound"),userData:"_stateSoundNames."+state);
            if(control is Label && control is not TextBox && control is not MultilineTextBox)_property.AddItem("DesignerText",userData:"DesignerText");
            _property.SelectByUserData("Bounds");_populating=false;ShowValue();
            _status.Text=control.Name+" · "+T("Drag to move; drag the corner to resize.");
        }
        private void ShowValue()
        {
            if(_selected==null || _property.SelectedItem?.UserData is not string property)return;
            var data=_selected.GetJson()??new JObject();
            var token=property.StartsWith("_stateSoundNames.",StringComparison.Ordinal)?data["_stateSoundNames"]?[property.Split('.')[1]]:data[property];
            _value.Text=property=="DesignerText" && _selected is Label label ? label.Text??"" : token?.Type==JTokenType.String?token.Value<string>()??"":token?.ToString()??"";
            _sounds.IsHidden=!property.Contains("Sound",StringComparison.OrdinalIgnoreCase);
        }
        private void ApplyValue()
        {
            if(_selected==null || _property.SelectedItem?.UserData is not string property)return;
            try
            {
                var old=(_selected.GetJson()??new JObject())[property];
                JToken token=old?.Type is JTokenType.Boolean or JTokenType.Integer or JTokenType.Float or JTokenType.Object or JTokenType.Array
                    ? JToken.Parse(_value.Text) : new JValue(_value.Text);
                Change(_selected,property,token);_status.Text=T("Property applied.");
            }
            catch(Exception){_status.Text=T("Invalid property value.");}
        }
        internal void RefreshSelection()
        {
            if(_selected?.Parent==null || _selected!=null && Resolve(Canvas!,ControlPath(_selected))!=_selected)_selected=null;
            _inspector.Height=Math.Min(510,Math.Max(280,Height-45));
            _inspector.SetPosition(Math.Clamp(_inspector.X,0,Math.Max(0,Width-_inspector.Width)),Math.Clamp(_inspector.Y,0,Math.Max(0,Height-_inspector.Height)));
        }
        protected override void OnMouseDown(MouseButton button,Point position,bool userAction=true)
        {
            if(!_selecting || button!=MouseButton.Left)return;
            var hit=Descendants(Canvas!).Where(control=>control.IsVisibleInTree && !IsOurControl(control) &&
                new Rectangle(control.ToCanvas(0,0).X,control.ToCanvas(0,0).Y,control.Width,control.Height).Contains(position)).LastOrDefault();
            if(hit==null)return;Select(hit);_hold=position;_dragging=true;
            var corner=hit.ToCanvas(hit.Width,hit.Height);_resizing=Math.Abs(position.X-corner.X)<12 && Math.Abs(position.Y-corner.Y)<12;
            InputHandler.MouseFocus=this;
        }
        protected override void OnMouseMoved(int x,int y,int dx,int dy)
        {
            if(!_dragging || _selected==null)return;
            var bounds=_selected.Bounds;
            if(_resizing){bounds.Width=Math.Max(8,bounds.Width+x-_hold.X);bounds.Height=Math.Max(8,bounds.Height+y-_hold.Y);}
            else{bounds.X+=x-_hold.X;bounds.Y+=y-_hold.Y;}
            _hold=new Point(x,y);Change(_selected,"Bounds",new JValue(bounds.ToString()));
            ShowValue();
        }
        protected override void OnMouseUp(MouseButton button,Point position,bool userAction=true)
        {_dragging=false;if(InputHandler.MouseFocus==this)InputHandler.MouseFocus=null;}
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            if(_selected?.IsVisibleInTree!=true)return;
            var point=_selected.ToCanvas(0,0);skin.Renderer.DrawColor=new Color(255,255,205,60);
            skin.Renderer.DrawLinedRect(new Rectangle(point.X,point.Y,_selected.Width,_selected.Height));
            skin.Renderer.DrawFilledRect(new Rectangle(point.X+_selected.Width-8,point.Y+_selected.Height-8,8,8));
        }
    }
}
