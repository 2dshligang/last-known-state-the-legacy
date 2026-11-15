using System;
using System.Collections.Generic;
using UnityEngine;

namespace LKS.World
{
    internal static class StationTextCapture
    {
        private static readonly Dictionary<string, string> LastShown = new(StringComparer.Ordinal);

        internal static void Record(string surface, string text)
        {
            if ((!Application.isEditor && !Debug.isDebugBuild) || Event.current?.type != EventType.Repaint ||
                string.IsNullOrWhiteSpace(text)) return;
            if (LastShown.TryGetValue(surface, out string current) && current == text) return;
            LastShown[surface] = text;
            PlayerTextAudit.Record(surface, text);
        }
    }
}
