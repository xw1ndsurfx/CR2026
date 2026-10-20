using Intersect.Compression;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Maps;
using Intersect.Server.Naval;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    // Explicit opt-in. Never commits game databases or launches services against user data.
    [Test, Explicit("Set NAVAL_AUDIT_DB to a copied gamedata.db for a read-only terrain audit")]
    public void ImportedOceanTerrainAndTransitions()
    {
        var source = Environment.GetEnvironmentVariable("NAVAL_AUDIT_DB");
        Assert.That(File.Exists(source), Is.True, "NAVAL_AUDIT_DB must name a copied database");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = source, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        var previousList = MapList.List;
        var previousWidth = Options.Instance.Map.MapWidth; var previousHeight = Options.Instance.Map.MapHeight;
        var maps = new List<MapController>();
        var oldMaps = new Dictionary<Guid, MapController>();
        var details = new List<object>(); var crossings = new List<object>();
        try
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT JsonData FROM MapFolders";
                using var rows = command.ExecuteReader();
                var list = new MapList();
                while (rows.Read()) list.Items.AddRange(ReadMapList(JObject.Parse(rows.GetString(0))).Items);
                MapList.List = list;
            }
            var ocean = OceanMapResolver.Maps();
            Assert.That(ocean.Count, Is.GreaterThan(0), "No Ocean folder maps in this database");
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, Name, Up, Down, Left, Right, Attributes, Events, NpcSpawns FROM Maps";
                using var rows = command.ExecuteReader();
                while (rows.Read())
                {
                    var id = Guid.Parse(rows.GetString(0));
                    if (!ocean.Contains(id)) continue;
                    var bytes = (byte[])rows[6];
                    var raw = JArray.Parse(LZ4.UnPickleString(bytes));
                    var width = raw.Count; var height = ((JArray)raw[0]!).Count;
                    Options.Instance.Map.MapWidth = width; Options.Instance.Map.MapHeight = height;
                    var map = new MapController(id) { Name = rows.GetString(1), Up = Guid.Parse(rows.GetString(2)),
                        Down = Guid.Parse(rows.GetString(3)), Left = Guid.Parse(rows.GetString(4)), Right = Guid.Parse(rows.GetString(5)) };
                    map.AttributeData = bytes;
                    if (MapController.Get(id) is { } old) oldMaps[id] = old;
                    MapController.Lookup.Set(id, map); maps.Add(map);
                    var placement = ShipPlacement.Find(id, width, height, (x,y) => NavalSystem.IsBlockingAttribute(map.Attributes[x,y]));
                    var blocking = map.Attributes.Cast<Intersect.Framework.Core.GameObjects.Maps.MapAttribute>()
                        .Count(NavalSystem.IsBlockingAttribute);
                    details.Add(new { map.Id, map.Name, Width = width, Height = height, BlockingTiles = blocking,
                        Events = rows.GetString(7), NpcSpawns = rows.GetString(8), TerrainPlacement = placement });
                    TestContext.Progress.WriteLine($"Ocean {id}: {width}x{height}, blocking={blocking}, terrain placement={placement?.X},{placement?.Y}");
                }
            }
            Assert.That(maps.Count, Is.EqualTo(ocean.Count), "Missing Ocean map records");
            Assert.That(maps.Select(m => (m.Attributes.GetLength(0),m.Attributes.GetLength(1))).Distinct().Count(), Is.EqualTo(1));
            bool Clear(Guid id, double x, double y)
            {
                foreach (var tile in ShipFootprint.Tiles(2,4,x,y))
                {
                    var target = id; double tx = tile.X + .5, ty = tile.Y + .5;
                    if (!OceanMapResolver.Resolve(ocean, ref target, ref tx, ref ty)) return false;
                    if (NavalSystem.IsBlockingAttribute(MapController.Get(target).Attributes[(int)tx,(int)ty])) return false;
                }
                return true;
            }
            foreach (var map in maps)
            {
                foreach (var east in new[] { true, false })
                {
                    var neighbor = east ? map.Right : map.Left;
                    if (!ocean.Contains(neighbor) || (east ? MapController.Get(neighbor)?.Left : MapController.Get(neighbor)?.Right) != map.Id) continue;
                    var validRows = new List<double>();
                    for (double y = 3.5; y < Options.Instance.Map.MapHeight - 3; y++)
                    {
                        var width = Options.Instance.Map.MapWidth;
                        var startX = east ? width-3.5 : 3.5;
                        if (!Clear(map.Id,startX,y)) continue;
                        var state = new ShipState { MapId = map.Id, X = startX, Y = y, Heading = east ? 90 : 270, Speed = 3 };
                        var ship = new ShipDefinition { MapId = map.Id };
                        for (var tick=0; tick<30; tick++)
                            ShipMovement.Step(state,ship,new(1,0,false),.1,(id,x,py,heading) =>
                            {
                                var success = OceanMapResolver.Resolve(ocean,ref id,ref x,ref py) && Clear(id,x,py);
                                return (success,id,x,py);
                            });
                        if (state.MapId == neighbor) validRows.Add(y);
                    }
                    crossings.Add(new { From = map.Id, To = neighbor, Direction = east ? "E" : "W", ClearRows = validRows });
                    TestContext.Progress.WriteLine($"Ocean crossing {map.Id} -> {neighbor}: {validRows.Count} clear rows");
                }
            }
            var report = new { OceanMaps = details, Crossings = crossings,
                Scope = "Read-only terrain simulation with production movement, footprint and topology. Does not test connected clients, graphics, live entities or event conditions. Placement is not a dock/water certification." };
            var output = Environment.GetEnvironmentVariable("NAVAL_AUDIT_REPORT");
            if (!string.IsNullOrWhiteSpace(output)) File.WriteAllText(output,JsonConvert.SerializeObject(report,Formatting.Indented));
        }
        finally
        {
            foreach (var map in maps) { MapController.Lookup.Delete(map); if (oldMaps.TryGetValue(map.Id,out var old)) MapController.Lookup.Set(map.Id,old); }
            MapList.List = previousList;
            Options.Instance.Map.MapWidth = previousWidth; Options.Instance.Map.MapHeight = previousHeight;
        }
    }

    private static MapList ReadMapList(JObject node)
    {
        var list = new MapList();
        foreach (var item in (node["Items"] as JArray ?? []))
            if (item["Children"] is JObject children)
                list.Items.Add(new MapListFolder { Name = item["Name"]?.ToString() ?? "", Children = ReadMapList(children) });
            else if (Guid.TryParse(item["MapId"]?.ToString(),out var id))
                list.Items.Add(new MapListMap { Name = item["Name"]?.ToString() ?? "", MapId = id });
        return list;
    }
}
