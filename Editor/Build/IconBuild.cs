using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace OccasionKit.EditorTools
{
    /// <summary>What the Android and iOS icon build steps share.</summary>
    internal static class IconBuild
    {
        /// <summary>The icons a build includes: a valid id and a foreground layer, the first of each id.</summary>
        public static List<AppIconVariant> GetIcons(OccasionKitSettings settings)
        {
            if (settings == null)
                return new List<AppIconVariant>();

            return settings.icons
                .Where(i => i != null && i.foreground != null && OccasionValidator.IsValidIconId(i.id))
                .GroupBy(i => i.id)
                .Select(g => g.First())
                .ToList();
        }
    }

    /// <summary>Stops an Android or iOS build whose icon set has errors, before anything is built.</summary>
    internal sealed class IconBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder { get => 0; }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android && report.summary.platform != BuildTarget.iOS)
                return;

            var settings = OccasionKitSettings.Instance;

            if (settings == null)
                return;

            var errors = OccasionValidator.ValidateIcons(settings).Where(i => i.Severity == IssueSeverity.Error).ToList();

            if (errors.Count > 0)
                throw new BuildFailedException("OccasionKit icon set has errors (Window > OccasionKit > Validation):\n" + string.Join("\n", errors.Select(e => "• " + e.Message)));
        }
    }
}
