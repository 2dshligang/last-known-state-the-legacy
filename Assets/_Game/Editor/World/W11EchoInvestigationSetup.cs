using UnityEditor;

namespace LKS.Editor.World
{
    public static class W11EchoInvestigationSetup
    {
        [MenuItem("LKS/World/Install W11 ECHO Investigation")]
        public static void Install() => W06StationSetup.Install();
    }
}
