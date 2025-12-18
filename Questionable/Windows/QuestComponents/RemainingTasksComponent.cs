using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Questionable.Controller;

namespace Questionable.Windows.QuestComponents;

internal sealed class RemainingTasksComponent(QuestController questController, GatheringController gatheringController)
{
    private readonly QuestController _questController = questController;
    private readonly GatheringController _gatheringController = gatheringController;

    public void Draw()
    {
        IList<string> gatheringTasks = _gatheringController.GetRemainingTaskNames();
        if (gatheringTasks.Count > 0)
        {
            ImGui.Separator();
            ImGui.BeginDisabled();
            foreach (var task in gatheringTasks)
                ImGui.TextUnformatted($"G: {task}");
            ImGui.EndDisabled();
        }
        else
        {
            var remainingTasks = _questController.GetRemainingTaskNames();
            if (remainingTasks.Count > 0)
            {
                ImGui.Separator();
                ImGui.BeginDisabled();
                foreach (var task in remainingTasks)
                    ImGui.TextUnformatted(task);
                ImGui.EndDisabled();
            }
        }
    }
}
