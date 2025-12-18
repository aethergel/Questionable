using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Humanizer;
using Humanizer.Localisation;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Windows.QuestComponents;

internal sealed class EventInfoComponent(QuestData questData,
    QuestRegistry questRegistry,
    QuestFunctions questFunctions,
    UiUtils uiUtils,
    QuestController questController,
    QuestTooltipComponent questTooltipComponent,
    Configuration configuration)
{
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Local")]
    private readonly List<EventQuest> _eventQuests =
    [
        new EventQuest("Limited Time Items", [new UnlockLinkId(568)], DateTime.MaxValue),
        new EventQuest("Starlight Celebration 2025", [new QuestId(5231)], AtDailyReset(new DateOnly(2026,1,1)))
    ];

    private readonly QuestData _questData = questData;
    private readonly QuestRegistry _questRegistry = questRegistry;
    private readonly QuestFunctions _questFunctions = questFunctions;
    private readonly UiUtils _uiUtils = uiUtils;
    private readonly QuestController _questController = questController;
    private readonly QuestTooltipComponent _questTooltipComponent = questTooltipComponent;
    private readonly Configuration _configuration = configuration;

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static DateTime AtDailyReset(DateOnly date)
    {
        return new DateTime(date, new TimeOnly(14, 59), DateTimeKind.Utc);
    }

    public bool ShouldDraw => _configuration.General.ShowIncompleteSeasonalEvents && _eventQuests.Any(IsIncomplete);

    public void Draw()
    {
        foreach (var eventQuest in _eventQuests)
        {
            if (IsIncomplete(eventQuest))
                DrawEventQuest(eventQuest);
        }
    }

    private void DrawEventQuest(EventQuest eventQuest)
    {
        if (eventQuest.EndsAtUtc != DateTime.MaxValue)
        {
            string time = (eventQuest.EndsAtUtc - DateTime.UtcNow).Humanize(
                precision: 1,
                culture: CultureInfo.InvariantCulture,
                minUnit: TimeUnit.Minute,
                maxUnit: TimeUnit.Day);
            ImGui.Text($"{eventQuest.Name} ({time})");
        }
        else
            ImGui.Text(eventQuest.Name);

        List<ElementId> startableQuests = eventQuest.QuestIds.Where(x =>
                _questRegistry.IsKnownQuest(x) &&
                _questFunctions.IsReadyToAcceptQuest(x) &&
                x != _questController.StartedQuest?.Quest.Id &&
                x != _questController.NextQuest?.Quest.Id)
            .ToList();
        foreach (var questId in eventQuest.QuestIds)
        {
            if (_questFunctions.IsQuestComplete(questId))
                continue;

            using (ImRaii.PushId($"##EventQuestSelection{questId}"))
            {
                string questName = _questData.GetQuestInfo(questId).Name;
                if (startableQuests.Contains(questId) &&
                    _questRegistry.TryGetQuest(questId, out Quest? quest))
                {
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
                    {
                        _questController.SetNextQuest(quest);
                        _questController.Start("SeasonalEventSelection");
                    }

                    bool hovered = ImGui.IsItemHovered();

                    ImGui.SameLine();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(questName);
                    hovered |= ImGui.IsItemHovered();

                    if (hovered)
                        _questTooltipComponent.Draw(quest.Info);
                }
                else
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX());

                    var style = _uiUtils.GetQuestStyle(questId);
                    if (_uiUtils.ChecklistItem(questName, style.Color, style.Icon, ImGui.GetStyle().FramePadding.X))
                        _questTooltipComponent.Draw(_questData.GetQuestInfo(questId));
                }
            }
        }
    }

    private bool IsIncomplete(EventQuest eventQuest)
    {
        if (eventQuest.EndsAtUtc <= DateTime.UtcNow)
            return false;

        return eventQuest.QuestIds.Any(ShouldShowQuest);
    }

    public IEnumerable<ElementId> GetCurrentlyActiveEventQuests()
    {
        return _eventQuests
            .Where(x => x.EndsAtUtc >= DateTime.UtcNow)
            .SelectMany(x => x.QuestIds)
            .Where(ShouldShowQuest);
    }

    private bool ShouldShowQuest(ElementId elementId) => !_questFunctions.IsQuestComplete(elementId) &&
                                                         !_questFunctions.IsQuestUnobtainable(elementId);

    private sealed record EventQuest(string Name, List<ElementId> QuestIds, DateTime EndsAtUtc);
}
