using UnityEditor;

namespace LKS.Editor.World
{
    public static class W10EchoTerminalSetup
    {
        [MenuItem("LKS/World/Install W10 ECHO Replay")]
        public static void Install() => W06StationSetup.Install();
    }
}
