using System.Collections.Generic;
using UnityEngine;

namespace OccasionKit
{
    /// <summary>Shows objects while an occasion is active (snow for Yalda, lanterns for Ramadan), or only while none is.</summary>
    [AddComponentMenu("OccasionKit/Occasion Toggle")]
    public sealed class OccasionToggle : MonoBehaviour
    {
        public enum Mode
        {
            ShowWhileAnyIsActive = 0,
            ShowWhileNoneIsActive = 1,
        }

        [OccasionId]
        public List<string> occasions = new List<string>();

        public Mode mode = Mode.ShowWhileAnyIsActive;

        [Tooltip("Objects to show or hide. Not this GameObject: put the toggle on a parent that stays active.")]
        public List<GameObject> targets = new List<GameObject>();

        private void OnEnable()
        {
            OccasionSystem.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            OccasionSystem.Changed -= Apply;
        }

        public void Apply()
        {
            var any = false;

            foreach (var id in occasions)
            {
                if (OccasionSystem.IsActive(id))
                {
                    any = true;
                    break;
                }
            }

            var show = mode == Mode.ShowWhileAnyIsActive ? any : !any;

            foreach (var target in targets)
            {
                if (target != null && target != gameObject && target.activeSelf != show)
                    target.SetActive(show);
            }
        }
    }
}
