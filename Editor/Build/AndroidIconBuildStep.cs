using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OccasionKit.EditorTools
{
    /// <summary>
    /// Adds the icon set to the Gradle project: one &lt;activity-alias&gt; per icon (the launcher entry moves from the
    /// main activity to the aliases, only "default" starts enabled), adaptive icon resources, the receiver that applies
    /// scheduled changes, and a keep rule for the Java side. Does nothing until the project has an icon.
    /// </summary>
    internal sealed class AndroidIconBuildStep : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string AliasPrefix = "com.occasionkit.Icon_";
        private const string Receiver = "com.occasionkit.OccasionIconReceiver";
        private const string ResourcePrefix = "occasionkit_";

        private static readonly (string folder, float scale)[] Densities =
        {
            ("mdpi", 1f), ("hdpi", 1.5f), ("xhdpi", 2f), ("xxhdpi", 3f), ("xxxhdpi", 4f),
        };

        // Run after other plugins have edited the manifest.
        public int callbackOrder { get => 1000; }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            var settings = OccasionKitSettings.Instance;

            if (settings == null)
                return;

            var errors = OccasionValidator.ValidateIcons(settings).Where(i => i.Severity == IssueSeverity.Error).ToList();

            if (errors.Count > 0)
                throw new BuildFailedException("OccasionKit icon set has errors (Window > OccasionKit > Validation):\n" + string.Join("\n", errors.Select(e => "• " + e.Message)));
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = OccasionKitSettings.Instance;
            var icons = settings != null
                ? settings.icons.Where(i => i != null && i.foreground != null && OccasionValidator.IsValidIconId(i.id)).GroupBy(i => i.id).Select(g => g.First()).ToList()
                : new List<AppIconVariant>();

            var retired = ReleasedIconRegistry.Load().Where(id => icons.All(i => i.id != id)).ToList();

            if (icons.Count == 0 && retired.Count == 0)
                return;

            var target = Process(path, icons, retired, (int)PlayerSettings.Android.minSdkVersion < 26);
            ReleasedIconRegistry.Add(icons.Select(i => i.id));
            Debug.Log($"[OccasionKit] Added {icons.Count} launcher icon(s){(retired.Count > 0 ? $" and {retired.Count} retired alias(es)" : "")} targeting {target}.");
        }

        /// <summary>Edits the unityLibrary module at <paramref name="path"/>. Returns the launcher activity the aliases target.</summary>
        internal static string Process(string path, IReadOnlyList<AppIconVariant> icons, IReadOnlyList<string> retired, bool buildLegacy)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            var document = new XmlDocument();
            document.Load(manifestPath);

            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("android", AndroidNs);

            if (!(document.SelectSingleNode("/manifest/application", namespaces) is XmlElement application))
                throw new BuildFailedException($"OccasionKit: no <application> in {manifestPath}.");

            var (activity, launcherFilter) = FindLauncher(application, namespaces);
            string target;

            if (activity != null)
            {
                activity.RemoveChild(launcherFilter);
                target = activity.GetAttribute("name", AndroidNs);
            }
            else if (application.SelectSingleNode($"activity-alias[@android:name='{AliasPrefix}{OccasionSystem.DefaultIconId}']", namespaces) is XmlElement previous)
            {
                // A Gradle project this step already processed: take the launcher filter back from the old default alias.
                target = previous.GetAttribute("targetActivity", AndroidNs);
                launcherFilter = previous.SelectSingleNode("intent-filter", namespaces) as XmlElement;
            }
            else
            {
                target = null;
            }

            if (string.IsNullOrEmpty(target) || launcherFilter == null)
                throw new BuildFailedException($"OccasionKit: no activity with a MAIN/LAUNCHER intent-filter in {manifestPath}. The launcher icons need it as their target.");

            launcherFilter = (XmlElement)launcherFilter.CloneNode(true);
            RemovePreviousOutput(application);

            AddAlias(document, application, OccasionSystem.DefaultIconId, target, launcherFilter, true, false);

            foreach (var icon in icons)
                AddAlias(document, application, icon.id, target, launcherFilter, false, true);

            foreach (var id in retired)
                AddAlias(document, application, id, target, launcherFilter, false, false);

            var showable = new[] { OccasionSystem.DefaultIconId }.Concat(icons.Select(i => i.id)).ToList();
            AddMetaData(document, application, "com.occasionkit.icons", string.Join(",", showable));
            AddMetaData(document, application, "com.occasionkit.aliases", string.Join(",", showable.Concat(retired)));

            var receiver = document.CreateElement("receiver");
            SetAndroid(document, receiver, "name", Receiver);
            SetAndroid(document, receiver, "exported", "false");
            application.AppendChild(receiver);

            using (var writer = XmlWriter.Create(manifestPath, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
                document.Save(writer);

            WriteResources(Path.Combine(path, "src", "main", "res"), icons, buildLegacy);
            AddProguardRule(path);
            return target;
        }

        private static (XmlElement activity, XmlElement filter) FindLauncher(XmlElement application, XmlNamespaceManager namespaces)
        {
            foreach (XmlElement activity in application.SelectNodes("activity", namespaces))
            {
                foreach (XmlElement filter in activity.SelectNodes("intent-filter", namespaces))
                {
                    var main = filter.SelectSingleNode("action[@android:name='android.intent.action.MAIN']", namespaces);
                    var launcher = filter.SelectSingleNode("category[@android:name='android.intent.category.LAUNCHER']", namespaces);

                    if (main != null && launcher != null)
                        return (activity, filter);
                }
            }

            return (null, null);
        }

        private static void RemovePreviousOutput(XmlElement application)
        {
            var stale = application.ChildNodes.OfType<XmlElement>()
                .Where(e => e.GetAttribute("name", AndroidNs).StartsWith("com.occasionkit."))
                .ToList();

            foreach (var element in stale)
                application.RemoveChild(element);
        }

        private static void AddAlias(XmlDocument document, XmlElement application, string id, string target, XmlElement launcherFilter, bool enabled, bool hasIcon)
        {
            var alias = document.CreateElement("activity-alias");
            SetAndroid(document, alias, "name", AliasPrefix + id);
            SetAndroid(document, alias, "targetActivity", target);
            SetAndroid(document, alias, "enabled", enabled ? "true" : "false");
            SetAndroid(document, alias, "exported", "true");

            // Without icon attributes an alias uses the application's icon (Player Settings).
            if (hasIcon)
            {
                SetAndroid(document, alias, "icon", $"@mipmap/{ResourcePrefix}{id}");
                SetAndroid(document, alias, "roundIcon", $"@mipmap/{ResourcePrefix}{id}_round");
            }

            alias.AppendChild(launcherFilter.CloneNode(true));
            application.AppendChild(alias);
        }

        private static void AddMetaData(XmlDocument document, XmlElement application, string name, string value)
        {
            var meta = document.CreateElement("meta-data");
            SetAndroid(document, meta, "name", name);
            SetAndroid(document, meta, "value", value);
            application.AppendChild(meta);
        }

        private static void SetAndroid(XmlDocument document, XmlElement element, string name, string value)
        {
            var attribute = document.CreateAttribute("android", name, AndroidNs);
            attribute.Value = value;
            element.Attributes.Append(attribute);
        }

        private static void WriteResources(string res, IReadOnlyList<AppIconVariant> icons, bool buildLegacy)
        {
            var colors = new StringBuilder();

            foreach (var icon in icons)
            {
                var layers = new IconLayers(icon);
                var name = ResourcePrefix + icon.id;

                foreach (var (folder, scale) in Densities)
                {
                    var directory = Path.Combine(res, "mipmap-" + folder);
                    Directory.CreateDirectory(directory);
                    var layerSize = Mathf.RoundToInt(IconLayers.LayerDp * scale);

                    File.WriteAllBytes(Path.Combine(directory, name + "_fg.png"), layers.Foreground(layerSize).EncodePng());

                    if (layers.HasBackgroundTexture)
                        File.WriteAllBytes(Path.Combine(directory, name + "_bg.png"), layers.Background(layerSize).EncodePng());

                    if (layers.HasMonochrome)
                        File.WriteAllBytes(Path.Combine(directory, name + "_mono.png"), layers.Monochrome(layerSize).EncodePng());

                    if (buildLegacy)
                    {
                        var legacySize = Mathf.RoundToInt(IconLayers.LegacyDp * scale);
                        File.WriteAllBytes(Path.Combine(directory, name + ".png"), layers.Legacy(legacySize, false).EncodePng());
                        File.WriteAllBytes(Path.Combine(directory, name + "_round.png"), layers.Legacy(legacySize, true).EncodePng());
                    }
                }

                string background;

                if (layers.HasBackgroundTexture)
                {
                    background = $"@mipmap/{name}_bg";
                }
                else
                {
                    background = $"@color/{name}_bg";
                    colors.AppendLine($"    <color name=\"{name}_bg\">#{ColorUtility.ToHtmlStringRGB(icon.backgroundColor)}</color>");
                }

                WriteAdaptiveIcon(res, "mipmap-anydpi-v26", name, background, null);

                // Themed icons (Android 13+) go in their own folder so older build tools never see <monochrome>.
                if (layers.HasMonochrome)
                    WriteAdaptiveIcon(res, "mipmap-anydpi-v33", name, background, $"@mipmap/{name}_mono");
            }

            if (colors.Length > 0)
            {
                var values = Path.Combine(res, "values");
                Directory.CreateDirectory(values);
                File.WriteAllText(Path.Combine(values, "occasionkit_colors.xml"), $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n{colors}</resources>\n");
            }
        }

        private static void WriteAdaptiveIcon(string res, string folder, string name, string background, string monochrome)
        {
            var directory = Path.Combine(res, folder);
            Directory.CreateDirectory(directory);

            var xml = new StringBuilder()
                .AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>")
                .AppendLine("<adaptive-icon xmlns:android=\"http://schemas.android.com/apk/res/android\">")
                .AppendLine($"    <background android:drawable=\"{background}\"/>")
                .AppendLine($"    <foreground android:drawable=\"@mipmap/{name}_fg\"/>");

            if (monochrome != null)
                xml.AppendLine($"    <monochrome android:drawable=\"{monochrome}\"/>");

            xml.AppendLine("</adaptive-icon>");

            File.WriteAllText(Path.Combine(directory, name + ".xml"), xml.ToString());
            File.WriteAllText(Path.Combine(directory, name + "_round.xml"), xml.ToString());
        }

        private static void AddProguardRule(string path)
        {
            const string rule = "-keep class com.occasionkit.** { *; }";
            var file = Path.Combine(path, "proguard-unity.txt");

            if (!File.Exists(file))
                return;

            var text = File.ReadAllText(file);

            if (!text.Contains(rule))
                File.AppendAllText(file, "\n" + rule + "\n");
        }
    }
}
