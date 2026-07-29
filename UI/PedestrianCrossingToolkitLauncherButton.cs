using ColossalFramework.UI;
using UnityEngine;
using UnifiedTransitLauncherToolbar = ScratchyBald.CitiesSkylines.UI.UnifiedTransitLauncherToolbar;

namespace PedestrianCrossingToolkit
{
    public class PedestrianCrossingToolkitLauncherButton : UIButton
    {
        private const string ButtonName = "PedestrianCrossingToolkitLauncherButton";
        private const string IconSpriteName = "PCT_ZebraCrossingLauncherIcon";

        public static PedestrianCrossingToolkitLauncherButton Instance;

        private static UITextureAtlas _iconAtlas;

        private UISprite _iconSprite;

        public override void Start()
        {
            base.Start();

            Instance = this;
            name = ButtonName;
            width = 42;
            height = 42;
            text = string.Empty;
            tooltip = "Pedestrian Crossing Toolkit";
            canFocus = true;
            isInteractive = true;
            isVisible = true;

            normalBgSprite = "ButtonMenu";
            hoveredBgSprite = "ButtonMenuHovered";
            pressedBgSprite = "ButtonMenuPressed";
            disabledBgSprite = "ButtonMenuDisabled";

            relativePosition = UnifiedTransitLauncherToolbar.GetButtonPosition(0);
            AddLauncherIcon();
            UnifiedTransitLauncherToolbar.RegisterDragSurface(this);
            UnifiedTransitLauncherToolbar.RefreshLayout(this);
            BringToFront();

            eventClick += OnLauncherClicked;
        }

        public override void Update()
        {
            base.Update();
            UnifiedTransitLauncherToolbar.RefreshLayoutIfOwned(this);
        }

        public override void OnDestroy()
        {
            UIComponent toolbar = parent;
            eventClick -= OnLauncherClicked;
            UnifiedTransitLauncherToolbar.UnregisterDragSurface(this);

            if (Instance == this)
                Instance = null;

            base.OnDestroy();
            UnifiedTransitLauncherToolbar.RefreshLayout(toolbar);
        }

        public static void CreateIfNeeded(UIView view)
        {
            if (view == null || Instance != null)
                return;

            UIPanel toolbar = UnifiedTransitLauncherToolbar.GetOrCreate(view);
            if (toolbar == null)
                return;

            PedestrianCrossingToolkitLauncherButton existing = toolbar.Find<PedestrianCrossingToolkitLauncherButton>(ButtonName);
            if (existing != null)
            {
                Instance = existing;
                existing.isVisible = true;
                UnifiedTransitLauncherToolbar.RefreshLayout(toolbar);
                return;
            }

            UIComponent component = toolbar.AddUIComponent(typeof(PedestrianCrossingToolkitLauncherButton));
            if (component != null)
            {
                component.name = ButtonName;
                component.isVisible = true;
            }

            UnifiedTransitLauncherToolbar.RefreshLayout(toolbar);
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;

            UIPanel toolbar = UnifiedTransitLauncherToolbar.Current;
            Instance.isVisible = false;
            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
            UnifiedTransitLauncherToolbar.RefreshLayout(toolbar);
        }

        private void OnLauncherClicked(UIComponent component, UIMouseEventParameter p)
        {
            PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(false);
            if (UnifiedTransitLauncherToolbar.ConsumeDragClick())
                return;

            PedestrianCrossingToolkitPanel.Toggle();
        }

        private void AddLauncherIcon()
        {
            UITextureAtlas iconAtlas = GetOrCreateIconAtlas();
            if (iconAtlas == null)
            {
                text = "PC";
                textScale = 0.72f;
                return;
            }

            _iconSprite = AddUIComponent<UISprite>();
            _iconSprite.atlas = iconAtlas;
            _iconSprite.spriteName = IconSpriteName;
            _iconSprite.width = 30f;
            _iconSprite.height = 30f;
            _iconSprite.relativePosition = new Vector3(6f, 6f);
            _iconSprite.isInteractive = false;
        }

        private static UITextureAtlas GetOrCreateIconAtlas()
        {
            if (_iconAtlas != null)
                return _iconAtlas;

            UIView view = UIView.GetAView();
            if (view == null || view.defaultAtlas == null || view.defaultAtlas.material == null)
                return null;

            Texture2D texture = CreateCrossingIconTexture();
            Material material = new Material(view.defaultAtlas.material);
            material.mainTexture = texture;

            _iconAtlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            _iconAtlas.name = "PedestrianCrossingToolkitLauncherAtlas";
            _iconAtlas.material = material;
            _iconAtlas.AddSprite(new UITextureAtlas.SpriteInfo
            {
                name = IconSpriteName,
                texture = texture,
                region = new Rect(0f, 0f, 1f, 1f),
                border = new RectOffset()
            });

            return _iconAtlas;
        }

        private static Texture2D CreateCrossingIconTexture()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color32[] pixels = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            Color32 road = new Color32(42, 48, 54, 255);
            Color32 roadEdge = new Color32(69, 178, 191, 255);
            Color32 stripe = new Color32(245, 248, 250, 255);
            Color32 marker = new Color32(132, 222, 206, 255);

            FillRect(pixels, size, 6, 8, 20, 16, road);
            FillRect(pixels, size, 6, 8, 20, 2, roadEdge);
            FillRect(pixels, size, 6, 22, 20, 2, roadEdge);

            FillRect(pixels, size, 9, 11, 3, 10, stripe);
            FillRect(pixels, size, 14, 11, 3, 10, stripe);
            FillRect(pixels, size, 19, 11, 3, 10, stripe);

            FillRect(pixels, size, 3, 14, 3, 4, marker);
            FillRect(pixels, size, 26, 14, 3, 4, marker);

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static void FillRect(Color32[] pixels, int textureSize, int x, int y, int width, int height, Color32 color)
        {
            int maxX = Mathf.Min(textureSize, x + width);
            int maxY = Mathf.Min(textureSize, y + height);

            for (int row = Mathf.Max(0, y); row < maxY; row++)
            {
                for (int col = Mathf.Max(0, x); col < maxX; col++)
                    pixels[(row * textureSize) + col] = color;
            }
        }
    }
}
