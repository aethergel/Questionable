﻿using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Data;

namespace Questionable.External;

internal sealed class YesAlreadyIpc : IDisposable
{

    private readonly IFramework _framework;
    private readonly QuestController _questController;
    private readonly TerritoryData _territoryData;
    private readonly IClientState _clientState;
    private readonly ILogger<YesAlreadyIpc> _logger;

    private readonly ICallGateSubscriber<bool> _isPluginEnabled;
    private readonly ICallGateSubscriber<bool, object> _setPluginEnabled;

    private bool _wasEnabled;

    public YesAlreadyIpc(IDalamudPluginInterface pluginInterface,
        IFramework framework,
        QuestController questController,
        TerritoryData territoryData,
        IClientState clientState,
        ILogger<YesAlreadyIpc> logger)
    {
        _framework = framework;
        _questController = questController;
        _territoryData = territoryData;
        _clientState = clientState;
        _logger = logger;
        _isPluginEnabled = pluginInterface.GetIpcSubscriber<bool>("YesAlready.IsPluginEnabled");
        _setPluginEnabled = pluginInterface.GetIpcSubscriber<bool, object>("YesAlready.SetPluginEnabled");
        _wasEnabled = IsPluginEnabled();
        _logger.LogInformation($"Enabled:{_wasEnabled}");

        _framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        bool hasActiveQuest = _questController.IsRunning ||
                              _questController.AutomationType != QuestController.EAutomationType.Manual;
        if (hasActiveQuest && !_territoryData.IsDutyInstance(_clientState.TerritoryType))
        {
            SetPluginEnabled(false);
        }
        else
        {
            SetPluginEnabled(true);
        }
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        SetPluginEnabled(true);
    }

    private bool IsPluginEnabled()
    {
        try
        {
            return _isPluginEnabled.InvokeFunc();
        }
        catch (IpcError e)
        {
            _logger.LogWarning(e, "YesAlready failed IsPluginEnabled");
            return false;
        }
    }

    private void SetPluginEnabled(bool value)
    {
        try
        {
            _wasEnabled = IsPluginEnabled();
            if (value != _wasEnabled)
                _setPluginEnabled.InvokeFunc(value);
        }
        catch(IpcError e)
        {
            _logger.LogWarning(e, $"YesAlready failed SetPluginEnabled:{value}");
        }
    }
}