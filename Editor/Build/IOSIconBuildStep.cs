using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace OccasionKit.EditorTools
{
    /// <summary>
    /// Adds the icon set to the Xcode project as alternate app icons: one single-size (1024 px) app icon set per icon
    /// in Unity-iPhone/Images.xcassets, named occasionkit_&lt;id&gt;, and the build setting that tells Xcode to include
    /// them. Needs Xcode 14 or newer. The primary icon stays the Player Settings icon. Does nothing until the project has
    /// an icon.
    /// </summary>
    internal sealed class IOSIconBuildStep : IPostprocessBuildWithReport
    {
        public const string NamePrefix = "occasionkit_";
        public const int IconSize = 1024;

        private const string AlternateIconsSetting = "ASSETCATALOG_COMPILER_ALTERNATE_APPICON_NAMES";

        private const string ContentsJson =
@"{
  ""images"" : [
    {
      ""filename"" : ""icon.png"",
      ""idiom"" : ""universal"",
      ""platform"" : ""ios"",
      ""size"" : ""1024x1024""
    }
  ],
  ""info"" : {
    ""author"" : ""xcode"",
    ""version"" : 1
  }
}
";

        // Run after other plugins have edited the Xcode project.
        public int callbackOrder { get => 1000; }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            var path = report.summary.outputPath;
            var icons = IconBuild.GetIcons(OccasionKitSettings.Instance);
            var catalog = GetAssetCatalog(path);

            // Nothing to add, and nothing left from an earlier build into the same Xcode project (Append).
            if (icons.Count == 0 && (catalog == null || !Directory.EnumerateDirectories(catalog, NamePrefix + "*.appiconset").Any()))
                return;

            var names = Process(path, icons);
            SetAlternateIconNames(path, names);
            Debug.Log($"[OccasionKit] Added {names.Count} alternate app icon(s) to the Xcode project.");
        }

        /// <summary>Writes the app icon sets into the Xcode project at <paramref name="path"/>, replacing earlier ones. Returns their names.</summary>
        internal static List<string> Process(string path, IReadOnlyList<AppIconVariant> icons)
        {
            var catalog = GetAssetCatalog(path);

            if (catalog == null)
                throw new BuildFailedException($"OccasionKit: no asset catalog at {Path.Combine(path, "Unity-iPhone", "Images.xcassets")}, so the alternate app icons can't be added.");

            foreach (var previous in Directory.GetDirectories(catalog, NamePrefix + "*.appiconset"))
                Directory.Delete(previous, true);

            var names = new List<string>();

            foreach (var icon in icons)
            {
                var name = NamePrefix + icon.id;
                var folder = Path.Combine(catalog, name + ".appiconset");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "icon.png"), Render(icon, IconSize).EncodePng(false));
                File.WriteAllText(Path.Combine(folder, "Contents.json"), ContentsJson, new UTF8Encoding(false));
                names.Add(name);
            }

            return names;
        }

        /// <summary>The square a launcher shows (the layers' center 72 dp) on the background color, so it has no transparency. iOS rounds the corners itself.</summary>
        internal static RgbaImage Render(AppIconVariant icon, int size)
        {
            var color = icon.backgroundColor;
            color.a = 1;
            var image = RgbaImage.Solid(size, color);
            image.DrawOver(new IconLayers(icon).Viewport(size));
            return image;
        }

        private static string GetAssetCatalog(string path)
        {
            var catalog = Path.Combine(path, "Unity-iPhone", "Images.xcassets");
            return Directory.Exists(catalog) ? catalog : null;
        }

        private static void SetAlternateIconNames(string path, IReadOnlyList<string> names)
        {
#if UNITY_IOS
            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.SetBuildProperty(project.GetUnityMainTargetGuid(), AlternateIconsSetting, string.Join(" ", names));
            project.WriteToFile(projectPath);
#endif
        }
    }
}
