using UnityEditor;
using UnityEngine;

namespace ViveHandTracking {

    [InitializeOnLoad]
    public class WindowsPlayModeMonitor : MonoBehaviour
    {
      static WindowsPlayModeMonitor() { EditorApplication.playModeStateChanged += LogPlayModeState; }

      // When playing in Unity Editor, Unity XR system stops first before OnApplicationExit message.
      // This would cause access violation in aristo_interface.dll as it's still using OpenVR native
      // pointer. Use Unity callback to stop GestureProvider before exiting play mode.
      private static void LogPlayModeState(PlayModeStateChange state) {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        if (GestureProvider.Current != null) GestureProvider.Current.enabled = false;
      }
    }

}
