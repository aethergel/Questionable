using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using ECommons.ExcelServices;
using JetBrains.Annotations;
using LLib.GameData;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model.Questing;
using Questionable.Windows;
using Questionable.Windows.QuestComponents;

namespace Questionable.External;

internal sealed class QuestionableIpc : IDisposable
{
    private const string IpcIsRunning = "Questionable.IsRunning";
    private const string IpcGetCurrentQuestId = "Questionable.GetCurrentQuestId";
    private const string IpcGetCurrentStepData = "Questionable.GetCurrentStepData";
    private const string IpcGetCurrentlyActiveEventQuests = "Questionable.GetCurrentlyActiveEventQuests";
    private const string IpcStartQuest = "Questionable.StartQuest";
    private const string IpcStartSingleQuest = "Questionable.StartSingleQuest";
    private const string IpcIsQuestLocked = "Questionable.IsQuestLocked";
    private const string IpcIsQuestComplete = "Questionable.IsQuestComplete";
    private const string IpcIsReadyToAcceptQuest = "Questionable.IsReadyToAcceptQuest";
    private const string IpcIsQuestAccepted = "Questionable.IsQuestAccepted";
    private const string IpcIsQuestUnobtainable = "Questionable.IsQuestUnobtainable";
    private const string IpcImportQuestPriority = "Questionable.ImportQuestPriority";
    private const string IpcClearQuestPriority = "Questionable.ClearQuestPriority";
    private const string IpcAddQuestPriority = "Questionable.AddQuestPriority";
    private const string IpcInsertQuestPriority = "Questionable.InsertQuestPriority";
    private const string IpcExportQuestPriority = "Questionable.ExportQuestPriority";
    private const string IpcStartGathering = "Questionable.StartGathering";
    private const string IpcStartGatheringComplex = "Questionable.StartGatheringComplex";
    private const string IpcStop = "Questionable.Stop";

    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;

    private readonly ICallGateProvider<bool> _isRunning;
    private readonly ICallGateProvider<string?> _getCurrentQuestId;
    private readonly ICallGateProvider<StepData?> _getCurrentStepData;
    private readonly ICallGateProvider<List<string>> _getCurrentlyActiveEventQuests;
    private readonly ICallGateProvider<string, bool> _startQuest;
    private readonly ICallGateProvider<string, bool> _startSingleQuest;
    private readonly ICallGateProvider<string, bool> _isQuestLocked;
	private readonly ICallGateProvider<string, bool> _isQuestComplete;
	private readonly ICallGateProvider<string, bool> _isReadyToAcceptQuest;
	private readonly ICallGateProvider<string, bool> _isQuestAccepted;
	private readonly ICallGateProvider<string, bool> _isQuestUnobtainable;
    private readonly ICallGateProvider<string, bool> _importQuestPriority;
    private readonly ICallGateProvider<string, bool> _addQuestPriority;
    private readonly ICallGateProvider<bool> _clearQuestPriority;
    private readonly ICallGateProvider<int, string, bool> _insertQuestPriority;
    private readonly ICallGateProvider<string> _exportQuestPriority;
    private readonly ICallGateProvider<uint, uint, byte, int, bool> _startGathering;
    private readonly ICallGateProvider<uint, uint, byte, int, ushort, bool> _startGatheringComplex;
    private readonly ICallGateProvider<string, bool> _stop;

    public QuestionableIpc(
        QuestController questController,
        EventInfoComponent eventInfoComponent,
        QuestRegistry questRegistry,
        QuestFunctions questFunctions,
        PriorityWindow priorityWindow,
        IDalamudPluginInterface pluginInterface)
    {
        _questController = questController;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;

        _isRunning = pluginInterface.GetIpcProvider<bool>(IpcIsRunning);
        _isRunning.RegisterFunc(() =>
            questController.AutomationType != QuestController.EAutomationType.Manual || questController.IsRunning);

        _getCurrentQuestId = pluginInterface.GetIpcProvider<string?>(IpcGetCurrentQuestId);
        _getCurrentQuestId.RegisterFunc(() => questController.CurrentQuest?.Quest.Id.ToString());

        _getCurrentStepData = pluginInterface.GetIpcProvider<StepData?>(IpcGetCurrentStepData);
        _getCurrentStepData.RegisterFunc(GetStepData);

        _getCurrentlyActiveEventQuests =
            pluginInterface.GetIpcProvider<List<string>>(IpcGetCurrentlyActiveEventQuests);
        _getCurrentlyActiveEventQuests.RegisterFunc(() =>
            eventInfoComponent.GetCurrentlyActiveEventQuests().Select(q => q.ToString()).ToList());

        _startQuest = pluginInterface.GetIpcProvider<string, bool>(IpcStartQuest);
        _startQuest.RegisterFunc(questId => StartQuest(questId, false));

        _startSingleQuest = pluginInterface.GetIpcProvider<string, bool>(IpcStartSingleQuest);
        _startSingleQuest.RegisterFunc(questId => StartQuest(questId, true));

        _isQuestLocked = pluginInterface.GetIpcProvider<string, bool>(IpcIsQuestLocked);
        _isQuestLocked.RegisterFunc(IsQuestLocked);

		_isQuestComplete = pluginInterface.GetIpcProvider<string, bool>(IpcIsQuestComplete);
		_isQuestComplete.RegisterFunc(IsQuestComplete);

		_isReadyToAcceptQuest = pluginInterface.GetIpcProvider<string, bool>(IpcIsReadyToAcceptQuest);
		_isReadyToAcceptQuest.RegisterFunc(IsReadyToAcceptQuest);

		_isQuestAccepted = pluginInterface.GetIpcProvider<string, bool>(IpcIsQuestAccepted);
		_isQuestAccepted.RegisterFunc(IsQuestAccepted);

		_isQuestUnobtainable = pluginInterface.GetIpcProvider<string, bool>(IpcIsQuestUnobtainable);
		_isQuestUnobtainable.RegisterFunc(IsQuestUnobtainable);

        _importQuestPriority = pluginInterface.GetIpcProvider<string, bool>(IpcImportQuestPriority);
        _importQuestPriority.RegisterFunc(ImportQuestPriority);

        _addQuestPriority = pluginInterface.GetIpcProvider<string, bool>(IpcAddQuestPriority);
        _addQuestPriority.RegisterFunc(AddQuestPriority);

        _clearQuestPriority = pluginInterface.GetIpcProvider<bool>(IpcClearQuestPriority);
        _clearQuestPriority.RegisterFunc(ClearQuestPriority);

        _insertQuestPriority = pluginInterface.GetIpcProvider<int, string, bool>(IpcInsertQuestPriority);
        _insertQuestPriority.RegisterFunc(InsertQuestPriority);

        _exportQuestPriority = pluginInterface.GetIpcProvider<string>(IpcExportQuestPriority);
        _exportQuestPriority.RegisterFunc(priorityWindow.EncodeQuestPriority);

        _startGathering = pluginInterface.GetIpcProvider<uint, uint, byte, int, bool>(IpcStartGathering);
        _startGathering.RegisterFunc(StartGathering);

        _startGatheringComplex = pluginInterface.GetIpcProvider<uint, uint, byte, int, ushort, bool>(IpcStartGatheringComplex);
        _startGatheringComplex.RegisterFunc(StartGatheringComplex);

        _stop = pluginInterface.GetIpcProvider<string, bool>(IpcStop);
        _stop.RegisterFunc(Stop);
    }

    private bool StartQuest(string questId, bool single)
    {
        if (ElementId.TryFromString(questId, out var elementId) && elementId != null &&
            _questRegistry.TryGetQuest(elementId, out var quest))
        {
            _questController.SetNextQuest(quest);
            if (single)
                _questController.StartSingleQuest("IPCQuestSelection");
            else
                _questController.Start("IPCQuestSelection");
            return true;
        }

        return false;
    }

    private StepData? GetStepData()
    {
        var progress = _questController.CurrentQuest;
        if (progress == null)
            return null;

        string questId = progress.Quest.Id.ToString();
        if (string.IsNullOrEmpty(questId))
            return null;

        QuestStep? step = progress.Quest.FindSequence(progress.Sequence)?.FindStep(progress.Step);
        if (step == null)
            return null;

        return new StepData
        {
            QuestId = questId,
            Sequence = progress.Sequence,
            Step = progress.Step,
            InteractionType = step.InteractionType.ToString(),
            Position = step.Position,
            TerritoryId = step.TerritoryId
        };
    }

    private bool IsQuestLocked(string questId)
    {
        if (ElementId.TryFromString(questId, out ElementId? elementId) && elementId != null &&
            _questRegistry.TryGetQuest(elementId, out _))
        {
            return _questFunctions.IsQuestLocked(elementId);
        }

        return true;
    }

    	private bool IsQuestComplete(string questId)
	{
		if (ElementId.TryFromString(questId, out ElementId? elementId) && elementId != null)
		{
			return _questFunctions.IsQuestComplete(elementId);
		}
		return false;
	}

	private bool IsReadyToAcceptQuest(string questId)
	{
		if (ElementId.TryFromString(questId, out ElementId? elementId) && elementId != null)
		{
			return _questFunctions.IsReadyToAcceptQuest(elementId);
		}
		return false;
	}

	private bool IsQuestAccepted(string questId)
	{
		if (ElementId.TryFromString(questId, out ElementId? elementId) && elementId != null)
		{
			return _questFunctions.IsQuestAccepted(elementId);
		}
		return false;
	}

	private bool IsQuestUnobtainable(string questId)
	{
		if (ElementId.TryFromString(questId, out ElementId? elementId) && elementId != null)
		{
			return _questFunctions.IsQuestUnobtainable(elementId);
		}
		return false;
	}

    private bool ImportQuestPriority(string encodedQuestPriority)
    {
        List<ElementId> questElements = PriorityWindow.DecodeQuestPriority(encodedQuestPriority);
        _questController.ImportQuestPriority(questElements);
        return true;
    }

    private bool ClearQuestPriority()
    {
        _questController.ClearQuestPriority();
        return true;
    }

    private bool AddQuestPriority(string questId)
    {
        if (ElementId.TryFromString(questId, out var elementId) && elementId != null &&
            _questRegistry.IsKnownQuest(elementId))
        {
            return _questController.AddQuestPriority(elementId);
        }

        return true;
    }

    private bool InsertQuestPriority(int index, string questId)
    {
        if (ElementId.TryFromString(questId, out var elementId) && elementId != null &&
            _questRegistry.IsKnownQuest(elementId))
        {
            return _questController.InsertQuestPriority(index, elementId);
        }

        return true;
    }

    private bool StartGathering(uint npcId, uint itemId, byte classJob, int quantity)
    {
        return StartGatheringComplex(npcId, itemId, classJob, quantity);
    }

    private bool StartGatheringComplex(uint npcId, uint itemId, byte classJob = ((byte)Job.MIN), int quantity = 1, ushort collectability = 0)
    {
        return _questController.StartGathering(npcId, itemId, (Job)classJob, quantity, collectability);
    }

    private bool Stop(string label)
    {
        _questController.StopAllDueToConditionFailed($"IPC: {label}");
        return true;
    }

    public void Dispose()
    {
        _exportQuestPriority.UnregisterFunc();
        _insertQuestPriority.UnregisterFunc();
        _clearQuestPriority.UnregisterFunc();
        _addQuestPriority.UnregisterFunc();
        _importQuestPriority.UnregisterFunc();
        _isQuestLocked.UnregisterFunc();
        _isQuestComplete.UnregisterFunc();
        _isReadyToAcceptQuest.UnregisterFunc();
        _isQuestAccepted.UnregisterFunc();
        _isQuestUnobtainable.UnregisterFunc();
        _startSingleQuest.UnregisterFunc();
        _startQuest.UnregisterFunc();
        _getCurrentlyActiveEventQuests.UnregisterFunc();
        _getCurrentStepData.UnregisterFunc();
        _getCurrentQuestId.UnregisterFunc();
        _isRunning.UnregisterFunc();
    }

    [UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
    public sealed class StepData
    {
        public required string QuestId { get; init; }
        public required byte Sequence { get; init; }
        public required int Step { get; init; }
        public required string InteractionType { get; init; }
        public required Vector3? Position { get; init; }
        public required ushort TerritoryId { get; init; }
    }
}
