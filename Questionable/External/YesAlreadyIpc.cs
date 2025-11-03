using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using ECommons.Reflection;
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

    private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(YesAlreadyIpc), "YesAlready", SafeWrapper.IPCException);
    [EzIPC("IsPluginEnabled")] public static readonly Func<bool> IsPluginEnabled;
    [EzIPC("SetPluginEnabled")] private static readonly Action<bool> SetPluginEnabled;

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
        _wasEnabled = IsPluginEnabled();
        _logger.LogInformation($"Enabled:{_wasEnabled}");

        _framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        if (IPCSubscriber_Common.IsReady("YesAlready"))
        {
            bool hasActiveQuest = (_questController.IsRunning ||
                                  _questController.AutomationType != QuestController.EAutomationType.Manual) &&
                                  !_territoryData.IsDutyInstance(_clientState.TerritoryType);
            if (hasActiveQuest)
            {
                if (IsPluginEnabled() && !_wasEnabled)
                {
                    _logger.LogDebug("it's *on*, that means i turn it *off*");
                    SetPluginEnabled(false);
                    _wasEnabled = true;
                    _logger.LogDebug("and just walk away!");
                }
            }
            else
            {
                if (!IsPluginEnabled() && _wasEnabled)
                {
                    _logger.LogDebug("it's *off*, that means i turn it *on*");
                    SetPluginEnabled(true);
                    _wasEnabled = false;
                    _logger.LogDebug("and just walk away!");
                }
            }
        }
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal class IPCSubscriber_Common
    {
        internal static bool IsReady(string pluginName) => DalamudReflector.TryGetDalamudPlugin(pluginName, out _, false, true);

        internal static Version? Version(string pluginName) => DalamudReflector.TryGetDalamudPlugin(pluginName, out var dalamudPlugin, false, true) ? dalamudPlugin.GetType().Assembly.GetName().Version : new Version(0, 0, 0, 0);

        internal static void DisposeAll(EzIPCDisposalToken[] _disposalTokens)
        {
            foreach (var token in _disposalTokens)
            {
                try
                {
                    token.Dispose();
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Error while unregistering IPC: {ex}");
                }
            }
        }
    }
}