﻿using System;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Questionable.Controller.Utils;
// Adapted from https://github.com/electr0sheep/ItemVendorLocation/blob/main/ItemVendorLocation/HighlightObject.cs
internal sealed class HighlightObject : IDisposable
{
    private uint[] _targetNpcDataId = [];
    private DateTime _lastUpdateTime = DateTime.Now;
    private readonly IFramework _framework;
    private readonly Configuration _configuration;
    private readonly ILogger<HighlightObject> _logger;
    private readonly IObjectTable _objectTable;

    public HighlightObject(
        IFramework framework,
        Configuration configuration,
        ILogger<HighlightObject> logger,
        IObjectTable objectTable)
    {
        
        _framework = framework;
        _configuration = configuration;
        _logger = logger;
        _objectTable = objectTable;
        _framework.Update += Framework_OnUpdate;
    }

    private void Framework_OnUpdate(IFramework framework)
    {
        //we want to update every 300 ms
        if (DateTime.Now - _lastUpdateTime <= TimeSpan.FromMilliseconds(300))
        {
            return;
        }

        _lastUpdateTime = DateTime.Now;

        if (!_configuration.Advanced.HighlightSelectedNpc || _targetNpcDataId.Length == 0)
        {
            return;
        }

        ToggleHighlight(true);
    }

    public void AddHighlight(uint Id)
    {
        _ = _framework.Run(() =>
        {
            if (!_targetNpcDataId.Contains(Id))
            {
                _logger.LogDebug($"Adding {Id} to highlight");
                _targetNpcDataId = _targetNpcDataId.Append(Id).ToArray();
            }
        });
    }

    public void RemoveHighlight(uint Id)
    {
        _ = _framework.Run(() =>
        {
            _logger.LogDebug($"Removing {Id} from highlight");
            _targetNpcDataId = _targetNpcDataId.Where(n => n != Id).ToArray();
        });
    }

    public void SetHighlight(uint[] Ids)
    {
        _ = _framework.Run(() =>
        {
            ToggleHighlight(false);
            if (_targetNpcDataId.Length == 0 && Ids.Length == 0)
                return;
            _logger.LogDebug($"Setting highlight to {Ids}");
            _targetNpcDataId = Ids;
            ToggleHighlight(true);
        });
    }

    public unsafe void ToggleHighlight(bool on)
    {
        if (_targetNpcDataId.All(n => n == 0))
        {
            return;
        }

        var gameObjects = _objectTable.Where(i =>
        {
            if (!i.IsValid())
                return false;
            var obj = (GameObject*)i.Address;
            return _targetNpcDataId.Contains(obj->BaseId);
        }).ToArray();

        if (gameObjects.Length == 0)
        {
            return;
        }

        foreach (var obj in gameObjects)
        {
            ((GameObject*)obj.Address)->Highlight(on ? _configuration.Advanced.HighlightColor : ObjectHighlightColor.None);
        }
    }

    public void Dispose()
    {
        _framework.Update -= Framework_OnUpdate;
    }
}
