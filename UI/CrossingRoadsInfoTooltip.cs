using System;
using System.IO;
using System.Reflection;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    internal static class CrossingRoadsInfoTooltip
    {
        private static readonly string[] ResourceNames =
        {
            "PedestrianCrossingToolkit.Resources.Tooltips.Standard.png",
            "PedestrianCrossingToolkit.Resources.Tooltips.Signal.png",
            "PedestrianCrossingToolkit.Resources.Tooltips.AutoSubway.png",
            "PedestrianCrossingToolkit.Resources.Tooltips.ManualSubway.png",
            "PedestrianCrossingToolkit.Resources.Tooltips.Bridge.png",
            "PedestrianCrossingToolkit.Resources.Tooltips.AutoScan.png"
        };

        private static readonly UITextureAtlas[] Atlases = new UITextureAtlas[6];
        private static readonly NetInfo[] TooltipInfos = new NetInfo[6];
        private static Locale _registeredLocale;

        internal static bool Bind(
            UIButton button,
            CrossingRoadsIconKind kind,
            string title,
            string description,
            string hint)
        {
            if (button == null)
                return false;

            int index = (int)kind;
            if (index < 0 || index >= ResourceNames.Length)
                return false;

            UITextureAtlas atlas = GetOrCreateAtlas(kind);
            NetInfo info = GetOrCreateTooltipInfo(
                kind,
                title,
                description,
                hint);
            if (atlas == null || info == null)
                return false;

            RegisterLocalization(
                info.name,
                title ?? string.Empty,
                JoinDescription(description, hint));

            string spriteName = GetSpriteName(kind);
            info.m_InfoTooltipAtlas = atlas;
            info.m_InfoTooltipThumbnail = spriteName;

            // Match UPG's proven vanilla GeneratedScrollPanel tooltip path.
            // PCT supplies only the embedded thumbnail and descriptive proxy;
            // the game remains owner of tooltip layout and population.
            button.tooltipAnchor = UITooltipAnchor.Anchored;
            button.tooltipBox = GeneratedPanel.tooltipBox;
            button.tooltip = info.GetLocalizedTooltip();
            button.objectUserData = info;
            button.eventTooltipEnter += delegate
            {
                RoadsPanel roadsPanel = UnityEngine.Object.FindObjectOfType<RoadsPanel>();
                if (roadsPanel != null)
                    roadsPanel.OnTooltipEnter(button, info);
            };
            return true;
        }

        private static UITextureAtlas GetOrCreateAtlas(CrossingRoadsIconKind kind)
        {
            int index = (int)kind;
            if (Atlases[index] != null)
                return Atlases[index];

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(ResourceNames[index]))
                {
                    if (stream == null)
                    {
                        Debug.LogWarning(
                            "[PedestrianCrossingToolkit] Missing embedded vanilla info-tooltip image: "
                            + ResourceNames[index]);
                        return null;
                    }

                    byte[] bytes = new byte[stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read <= 0)
                            break;
                        offset += read;
                    }

                    if (offset != bytes.Length)
                        throw new EndOfStreamException("Embedded tooltip image ended early.");

                    Texture2D texture = new Texture2D(
                        SnapshotTool.tooltipWidth,
                        SnapshotTool.tooltipHeight,
                        TextureFormat.ARGB32,
                        false);
                    texture.name = GetSpriteName(kind);
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    if (!texture.LoadImage(bytes, false))
                    {
                        UnityEngine.Object.Destroy(texture);
                        return null;
                    }

                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;
                    Atlases[index] = AssetImporterThumbnails.CreateThumbnailAtlas(
                        new[] { texture },
                        "PedestrianCrossingToolkit" + kind + "InfoTooltipAtlas");
                    return Atlases[index];
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[PedestrianCrossingToolkit] Could not prepare the " + kind
                    + " vanilla info-tooltip image: " + e.Message);
                return null;
            }
        }

        private static NetInfo GetOrCreateTooltipInfo(
            CrossingRoadsIconKind kind,
            string title,
            string description,
            string hint)
        {
            int index = (int)kind;
            NetInfo existing = TooltipInfos[index];
            if (existing != null)
                return existing;

            try
            {
                string prefabName = "PCT Tooltip " + kind;
                GameObject gameObject = new GameObject(prefabName);
                gameObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(gameObject);

                NetInfo info = gameObject.AddComponent<NetInfo>();
                CrossingRoadsTooltipNetAI ai =
                    gameObject.AddComponent<CrossingRoadsTooltipNetAI>();
                info.name = prefabName;
                info.m_class = CreateItemClass(kind);
                info.m_availableIn = ItemClass.Availability.None;
                info.m_halfWidth = 4f;
                info.m_segmentLength = 8f;
                info.m_lanes = new NetInfo.Lane[0];
                info.m_segments = new NetInfo.Segment[0];
                info.m_nodes = new NetInfo.Node[0];
                info.m_netAI = ai;
                ai.m_info = info;
                ai.Tooltip = string.Empty;

                RegisterLocalization(
                    prefabName,
                    title ?? string.Empty,
                    JoinDescription(description, hint));
                TooltipInfos[index] = info;
                return info;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[PedestrianCrossingToolkit] Could not prepare the " + kind
                    + " vanilla info-tooltip metadata: " + e.Message);
                return null;
            }
        }

        private static ItemClass CreateItemClass(CrossingRoadsIconKind kind)
        {
            ItemClass itemClass = ScriptableObject.CreateInstance<ItemClass>();
            itemClass.name = "PCT Tooltip Class " + kind;
            itemClass.m_service = ItemClass.Service.Road;
            itemClass.m_subService = ItemClass.SubService.None;
            itemClass.m_level = ItemClass.Level.Level1;
            itemClass.m_layer = ItemClass.Layer.Default;
            return itemClass;
        }

        private static void RegisterLocalization(
            string prefabName,
            string title,
            string description)
        {
            LocaleManager manager = LocaleManager.instance;
            if (manager == null)
                return;

            FieldInfo localeField = typeof(LocaleManager).GetField(
                "m_Locale",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Locale locale = localeField == null
                ? null
                : localeField.GetValue(manager) as Locale;
            if (locale == null)
                return;

            if (!ReferenceEquals(locale, _registeredLocale))
                _registeredLocale = locale;

            AddOverride(locale, "NET_TITLE", prefabName, title);
            AddOverride(locale, "NET_DESC", prefabName, description);
        }

        private static void AddOverride(
            Locale locale,
            string identifier,
            string key,
            string value)
        {
            try
            {
                locale.AddLocalizedString(
                    new Locale.Key
                    {
                        m_Identifier = identifier,
                        m_Key = key
                    },
                    value);
            }
            catch (ArgumentException)
            {
                // Locale entries may survive a level reload; the exact
                // override below deliberately refreshes their display value.
            }

            Locale.SetOverriddenLocalizedStrings(identifier, key, new[] { value });
        }

        private static string JoinDescription(string description, string hint)
        {
            if (string.IsNullOrEmpty(description))
                return hint ?? string.Empty;
            if (string.IsNullOrEmpty(hint))
                return description;
            return description + " " + hint;
        }

        private static string GetSpriteName(CrossingRoadsIconKind kind)
        {
            return "PedestrianCrossingToolkit" + kind + "InfoTooltip";
        }
    }

    internal sealed class CrossingRoadsTooltipNetAI : NetAI
    {
        internal string Tooltip;

        public override string GetLocalizedTooltip()
        {
            return Tooltip ?? string.Empty;
        }
    }
}
