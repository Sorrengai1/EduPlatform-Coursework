
using System;
using System.Collections.Generic;
using System.Linq;

namespace EduPlatform.Services;

public static class AnswerEvaluator
{
    public static bool IsCorrect(
        IEnumerable<string> selected,
        IEnumerable<string> correct)
    {
        var selectedList = selected.ToList();
        var selectedSet = selectedList.ToHashSet(StringComparer.Ordinal);
        var correctSet = correct.ToHashSet(StringComparer.Ordinal);

        return correctSet.Count > 0
            && selectedList.Count == selectedSet.Count
            && selectedSet.SetEquals(correctSet);
    }
}
