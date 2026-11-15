using UnityEditor;

namespace LKS.Editor.World
{
    public static class W08StationSetup
    {
        [MenuItem("LKS/World/Install W08 Shelter and Seedlings")]
        public static void Install()
        {
            W04StationGenerator.Generate();
            W06StationSetup.Install();
        }
    }
}
