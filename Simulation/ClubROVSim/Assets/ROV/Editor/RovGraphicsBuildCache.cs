using UnityEditor;
using UnityEditor.Rendering;

namespace ClubROV.Simulation.Editor
{
    [InitializeOnLoad]
    public static class RovGraphicsBuildCache
    {
        static RovGraphicsBuildCache()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                // URP shader compilation can cache the temporary runtime pipeline during Play.
                // Clear that build-only cache after the HUD restores the persistent asset.
                if(state==PlayModeStateChange.EnteredEditMode) CoreBuildData.instance.Dispose();
            };
        }
    }
}
