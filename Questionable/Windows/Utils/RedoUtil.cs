using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ECommons;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Questionable.Windows.Utils;
internal sealed class RedoUtil
{
    public Dictionary<uint, List<uint>> Dict;
    public Stopwatch Last { get; private set; }

    public RedoUtil()
    {
        Dict = [];
        Last = Generate();
    }

    public ReadOnlySeString GetChapter(uint questId)
    {
        var result = Dict.FirstOrDefault(entry => entry.Value.Contains(questId));
        return GenericHelpers.GetSheet<QuestRedoChapterUI>().GetRow(result.Key).ChapterName;
    }

    public Stopwatch Generate()
    {
        var watch = Stopwatch.StartNew();
        foreach (var chapter in GenericHelpers.GetSheet<QuestRedo>())
        {
            if (chapter.Chapter.RowId == 0)
                continue;
            if (!Dict.ContainsKey(chapter.Chapter.RowId))
                Dict[chapter.Chapter.RowId] = [];
            foreach (var quest in chapter.QuestRedoParam)
            {
                if (quest.Quest.RowId != 0)
                    Dict[chapter.Chapter.RowId].Add(quest.Quest.RowId);
            }
        }
        watch.Stop();
        return watch;
    }
}
