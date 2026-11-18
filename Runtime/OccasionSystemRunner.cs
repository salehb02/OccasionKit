using UnityEngine;

namespace OccasionKit
{
    /// <summary>Feeds app lifecycle events to <see cref="OccasionSystem"/>. Created on initialization.</summary>
    [AddComponentMenu("")]
    internal sealed class OccasionSystemRunner : MonoBehaviour
    {
        private const float TickInterval = 1f;

        private static OccasionSystemRunner _instance;
        private float _nextTick;

        internal static void Ensure()
        {
            // Edit-mode use (tools, tests) has no app lifecycle to follow.
            if (_instance != null || !Application.isPlaying)
                return;

            var go = new GameObject("[OccasionKit]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<OccasionSystemRunner>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextTick)
                return;

            _nextTick = Time.unscaledTime + TickInterval;
            OccasionSystem.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                OccasionSystem.OnBackground();
            else
                OccasionSystem.OnForeground();
        }

        private void OnApplicationQuit() => OccasionSystem.OnBackground();

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
