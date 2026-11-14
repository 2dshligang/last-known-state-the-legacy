using UnityEngine;

namespace LKS
{
    public static class PlayerTextAudit
    {
        public const string LogPrefix = "LKS_PLAYER_TEXT ";

        public static void Record(string surface, string text)
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
                return;
            var entry = new Entry { surface = surface, text = text };
            Debug.Log(LogPrefix + JsonUtility.ToJson(entry));
        }

        [System.Serializable]
        public sealed class Entry
        {
            public string surface;
            public string text;
        }
    }
}
