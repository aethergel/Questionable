using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.MathHelpers;
using Lumina.Excel.Sheets;
using Questionable.Model.Gathering;

namespace GatheringPathRenderer.Windows;

internal sealed class EditorWindow : Window
{
    private readonly RendererPlugin _plugin;
    private readonly EditorCommands _editorCommands;
    private readonly IDataManager _dataManager;
    private readonly ICommandManager _commandManager;
    private readonly ITargetManager _targetManager;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;

    private readonly Dictionary<Guid, LocationOverride> _changes = [];

    private IGameObject? _target;
    private int count;
    private bool compact;

    private (RendererPlugin.GatheringLocationContext Context, GatheringNode Node, GatheringLocation Location)?
        _targetLocation;

    public EditorWindow(RendererPlugin plugin, EditorCommands editorCommands, IDataManager dataManager, ICommandManager commandManager,
        ITargetManager targetManager, IClientState clientState, IObjectTable objectTable, ConfigWindow configWindow)
        : base($"Gathering Path Editor {typeof(EditorWindow).Assembly.GetName().Version!.ToString(2)}###QuestionableGatheringPathEditor",
            ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.AlwaysAutoResize)
    {
        _plugin = plugin;
        _editorCommands = editorCommands;
        _dataManager = dataManager;
        _commandManager = commandManager;
        _targetManager = targetManager;
        _clientState = clientState;
        _objectTable = objectTable;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 100),
        };

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new Vector2(1.5f, 1),
            Click = _ => configWindow.IsOpen = true,
            Priority = int.MinValue,
            ShowTooltip = () =>
            {
                ImGui.BeginTooltip();
                ImGui.Text("Open Configuration");
                ImGui.EndTooltip();
            }
        });

        RespectCloseHotkey = false;
        ShowCloseButton = false;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    public override void Update()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
        {
            _target = null;
            _targetLocation = null;
            return;
        }

        _target = _targetManager.Target;
        var gatheringLocations = _plugin.GetLocationsInTerritory(_clientState.TerritoryType);
        var location = gatheringLocations.ToList().SelectMany(context =>
                context.Root.Groups.SelectMany(group =>
                    group.Nodes.SelectMany(node => node.Locations
                        .Select(location =>
                        {
                            float distance;
                            if (_target != null)
                                distance = Vector3.Distance(location.Position, _target.Position);
                            else
                                distance = Vector3.Distance(location.Position, _clientState.LocalPlayer.Position);

                            return new { Context = context, Node = node, Location = location, Distance = distance };
                        })
                        .Where(location => location.Distance < (_target == null ? 3f : 0.1f)))))
            .MinBy(x => x.Distance);
        if (_target != null && _target.ObjectKind != ObjectKind.GatheringPoint)
        {
            _target = null;
            _targetLocation = null;
            return;
        }

        if (location == null)
        {
            _targetLocation = null;
            return;
        }

        _target ??= _objectTable
            .Where(x => x.ObjectKind == ObjectKind.GatheringPoint && x.BaseId == location.Node.DataId)
            .Select(x => new
            {
                Object = x,
                Distance = Vector3.Distance(location.Location.Position, _clientState.LocalPlayer.Position)
            })
            .Where(x => x.Distance < 3f)
            .OrderBy(x => x.Distance)
            .Select(x => x.Object)
            .FirstOrDefault();
        _targetLocation = (location.Context, location.Node, location.Location);
    }

    public override bool DrawConditions()
    {
        return true;//!(_clientState.TerritoryType is 0 or 939) && (_target != null || _targetLocation != null);
    }

    public override void Draw()
    {
        if (_target != null && _targetLocation != null)
        {
            var context = _targetLocation.Value.Context;
            var node = _targetLocation.Value.Node;
            var location = _targetLocation.Value.Location;
            ImGui.Text(context.File.Directory?.Name ?? string.Empty);
            ImGui.Indent();
            ImGui.Text(context.File.Name);
            ImGui.Unindent();
            ImGui.Text(
                $"{_target.BaseId} +{node.Locations.Count - 1} / {location.InternalId.ToString()[..4]}");
            ImGui.Text(string.Create(CultureInfo.InvariantCulture, $"{location.Position:G}"));

            if (!_changes.TryGetValue(location.InternalId, out LocationOverride? locationOverride))
            {
                locationOverride = new LocationOverride();
                _changes[location.InternalId] = locationOverride;
            }

            int minAngle = locationOverride.MinimumAngle ?? location.MinimumAngle.GetValueOrDefault();
            int maxAngle = locationOverride.MaximumAngle ?? location.MaximumAngle.GetValueOrDefault();
            if (ImGui.DragIntRange2("Angle", ref minAngle, ref maxAngle, 5, -360, 360))
            {
                locationOverride.MinimumAngle = minAngle;
                locationOverride.MaximumAngle = maxAngle;
            }

            float minDistance = locationOverride.MinimumDistance ?? location.CalculateMinimumDistance();
            float maxDistance = locationOverride.MaximumDistance ?? location.CalculateMaximumDistance();
            if (ImGui.DragFloatRange2("Distance", ref minDistance, ref maxDistance, 0.1f, 1f, 3f))
            {
                locationOverride.MinimumDistance = minDistance;
                locationOverride.MaximumDistance = maxDistance;
            }

            bool unsaved = locationOverride.NeedsSave();
            ImGui.BeginDisabled(!unsaved);
            if (unsaved)
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed);
            if (ImGui.Button("Save"))
            {
                if (locationOverride is { MinimumAngle: not null, MaximumAngle: not null })
                {
                    location.MinimumAngle = locationOverride.MinimumAngle ?? location.MinimumAngle;
                    location.MaximumAngle = locationOverride.MaximumAngle ?? location.MaximumAngle;
                }

                if (locationOverride is { MinimumDistance: not null, MaximumDistance: not null })
                {
                    location.MinimumDistance = locationOverride.MinimumDistance;
                    location.MaximumDistance = locationOverride.MaximumDistance;
                }

                _plugin.Save(context.File, context.Root);
            }

            if (unsaved)
                ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.Button("Reset"))
            {
                _changes[location.InternalId] = new LocationOverride();
            }

            ImGui.EndDisabled();
            
            //ImGui.SameLine();
            //if (ImGui.Button("90deg"))
            //{
            //    Vector3? position = _clientState.LocalPlayer?.Position;
            //    if (position != null && position.Value.Length() != 0 && location.Position.Length() != 0)
            //    {
            //        var angle = Math.Acos(Vector3.Dot(position.Value, location.Position) /
            //                              (position.Value.Length() * location.Position.Length()));
            //        locationOverride.MinimumAngle = (int)angle;
            //        locationOverride.MaximumAngle = (int)angle;
            //    }
            //}


            List<IGameObject> nodesInObjectTable = [.. _objectTable.Where(x => x.ObjectKind == ObjectKind.GatheringPoint && x.BaseId == _target.BaseId)];
            List<IGameObject> missingLocations = [.. nodesInObjectTable.Where(x => !node.Locations.Any(y => Vector3.Distance(x.Position, y.Position) < 0.1f))];
            if (missingLocations.Count > 0)
            {
                if (ImGui.Button("Add missing locations"))
                {
                    foreach (var missing in missingLocations)
                        _editorCommands.AddToExistingGroup(context.Root, missing);

                    _plugin.Save(context.File, context.Root);
                }
            }
        }
        else if (_target != null)
        {
            var gatheringPoint = _dataManager.GetExcelSheet<GatheringPoint>().GetRowOrDefault(_target.BaseId);
            if (gatheringPoint == null)
                return;

            var locationsInTerritory = _plugin.GetLocationsInTerritory(_clientState.TerritoryType).ToList();
            var location = locationsInTerritory.SingleOrDefault(x => x.Id == gatheringPoint.Value.GatheringPointBase.RowId);
            if (location != null)
            {
                var targetFile = location.File;
                var root = location.Root;

                if (ImGui.Button("Add to closest group"))
                {
                    _editorCommands.AddToExistingGroup(root, _target);
                    _plugin.Save(targetFile, root);
                }

                ImGui.BeginDisabled(root.Groups.Any(group => group.Nodes.Any(node => node.DataId == _target.BaseId)));
                ImGui.SameLine();
                if (ImGui.Button("Add as new group"))
                {
                    _editorCommands.AddToNewGroup(root, _target);
                    _plugin.Save(targetFile, root);
                }

                ImGui.EndDisabled();
            }
            else
            {
                if (ImGui.Button($"Create location ({gatheringPoint.Value.GatheringPointBase.RowId})"))
                {
                    var (targetFile, root) = _editorCommands.CreateNewFile(gatheringPoint.Value, _target);
                    _plugin.Save(targetFile, root);
                }
            }
        }
        if (_clientState.TerritoryType != 0 && _clientState.LocalPlayer != null && ImGui.CollapsingHeader("Unadded nodes"))
            ListLocationsInCurrentTerritory();
    }

    public bool TryGetOverride(Guid internalId, out LocationOverride? locationOverride)
        => _changes.TryGetValue(internalId, out locationOverride);

    public void ListLocationsInCurrentTerritory()
    {
        var a = _clientState.TerritoryType;
        var gatheringPoints = _dataManager.GetExcelSheet<GatheringPoint>().Where(
            _point => _point.TerritoryType.RowId.Equals(_clientState.TerritoryType) &&
            _point.GatheringPointBase.Value.GatheringType.RowId <= (uint)GatheringType.Harvesting
        );
        var loadedPoints = _plugin.GatheringLocations;
        var shownNone = true;
        ImGui.Text($"Nodes in {_clientState.TerritoryType}: ({count})");
        ImGui.SameLine();
        ImGui.Text("[vnav stop]");
        if (ImGui.IsItemClicked()) _commandManager.ProcessCommand("/vnav stop");
        ImGui.SameLine();
        ImGui.Text($"[compact {(compact ? 'y' : 'n')}]");
        if (ImGui.IsItemClicked()) compact = !compact;
        ImGui.SameLine();
        ImGui.Text($"[distant {(_plugin.DistantRange ? 'y' : 'n')}]");
        if (ImGui.IsItemClicked()) _plugin.DistantRange = !_plugin.DistantRange;
        List<string> seen = [];
        count = 0;
        foreach (GatheringPoint _point in gatheringPoints.OrderBy(x => x.PlaceName.Value.Name.ToMacroString()))
        {
            if (_point.GatheringPointBase.RowId >= 653 && _point.GatheringPointBase.RowId <= 680) continue; // obsolete skybuilders stuff
            if (!loadedPoints.Any(location => location.Root.Groups.Any(group => group.Nodes.Any(node => node.DataId.Equals(_point.RowId)))))
            {
                count += 1;
                if (compact) {
                    if (seen.Contains(_point.PlaceName.Value.Name.ToMacroString())) continue;
                    seen.Add(_point.PlaceName.Value.Name.ToMacroString());
                }
                char special = ' ';
                if (_dataManager.GetExcelSheet<GatheringPointTransient>().TryGetRow(_point.RowId, out var gatheringPointTransient) &&
                    (gatheringPointTransient.EphemeralStartTime != 65535 ||
                    gatheringPointTransient.EphemeralEndTime != 65535 ||
                    gatheringPointTransient.GatheringRarePopTimeTable.RowId != 0))
                {
                    special = '*';
                }
                ImGui.Text($"{((GatheringType)_point.GatheringPointBase.Value.GatheringType.RowId).ToString()[..1]}{special}{_point.RowId} {_point.PlaceName.Value.Name}  ");
                if (_plugin.GBRLocationData.TryGetValue(_point.RowId, out List<Vector3>? value))
                {
                    var gbr = value.FirstOrNull();
                    if (gbr != null)
                    {
                        var scale = _point.TerritoryType.Value.Map.Value.SizeFactor;
                        ImGui.SameLine();
                        ImGui.Text($"{gbr.Value.X} {gbr.Value.Y} {gbr.Value.Z}");
                        if (ImGui.IsItemClicked())
                        {
                            _commandManager.ProcessCommand($"/vnav flyto {gbr.Value.X} {gbr.Value.Y} {gbr.Value.Z}");
                        }
                        var distance = (_clientState.LocalPlayer.Position - gbr).Value.Length();
                        ImGui.SameLine();
                        if (distance < 200) ImGui.TextColored(ImGuiColors.DalamudOrange, $"({distance:F2})");
                        else ImGui.Text($"({distance:F2})");
                    }
                }
                shownNone = false;
            }
        }
        if (shownNone) ImGui.Text("No (unadded) results");
    }

    enum GatheringType {
        Mining,
        Quarrying,
        Logging,
        Harvesting,
        Fishing,
        Spearfishing
    }
}

internal sealed class LocationOverride
{
    public int? MinimumAngle { get; set; }
    public int? MaximumAngle { get; set; }
    public float? MinimumDistance { get; set; }
    public float? MaximumDistance { get; set; }

    public bool IsCone()
    {
        return MinimumAngle != null && MaximumAngle != null && MinimumAngle != MaximumAngle;
    }

    public bool NeedsSave()
    {
        return (MinimumAngle != null && MaximumAngle != null) || (MinimumDistance != null && MaximumDistance != null);
    }
}
