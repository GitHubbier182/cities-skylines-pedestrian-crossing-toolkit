using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    internal enum CrossingRoadsIconKind
    {
        Standard,
        Signal,
        AutoSubway,
        ManualSubway,
        Bridge,
        AutoScan
    }

    internal static class CrossingRoadsIconFactory
    {
        private const int TextureSize = 96;
        private const int Supersample = 3;
        private static readonly UITextureAtlas[] Atlases = new UITextureAtlas[6];
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);
        private static readonly Color32 Road = new Color32(37, 45, 54, 255);
        private static readonly Color32 RoadLane = new Color32(113, 125, 134, 255);
        private static readonly Color32 StructureShadow = new Color32(18, 24, 30, 220);
        private static readonly Color32 RoadEdge = new Color32(84, 196, 207, 255);
        private static readonly Color32 White = new Color32(240, 245, 247, 255);
        private static readonly Color32 Cyan = new Color32(95, 220, 211, 255);
        private static readonly Color32 Blue = new Color32(85, 170, 232, 255);
        private static readonly Color32 Yellow = new Color32(247, 200, 65, 255);
        private static readonly Color32 Red = new Color32(236, 82, 78, 255);
        private static readonly Color32 Green = new Color32(91, 205, 125, 255);

        internal static string GetSpriteName(CrossingRoadsIconKind kind)
        {
            return "PCT_Roads_" + kind;
        }

        internal static UITextureAtlas GetAtlas(CrossingRoadsIconKind kind)
        {
            int index = (int)kind;
            if (Atlases[index] != null)
                return Atlases[index];

            UIView view = UIView.GetAView();
            if (view == null || view.defaultAtlas == null || view.defaultAtlas.material == null)
                return null;

            Texture2D texture = CreateTexture(kind);
            Material material = new Material(view.defaultAtlas.material);
            material.mainTexture = texture;
            UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            atlas.name = GetSpriteName(kind) + "_Atlas";
            atlas.material = material;
            atlas.AddSprite(new UITextureAtlas.SpriteInfo
            {
                name = GetSpriteName(kind),
                texture = texture,
                region = new Rect(0f, 0f, 1f, 1f),
                border = new RectOffset()
            });
            Atlases[index] = atlas;
            return atlas;
        }

        private static Texture2D CreateTexture(CrossingRoadsIconKind kind)
        {
            int size = TextureSize * Supersample;
            Color32[] high = new Color32[size * size];
            for (int i = 0; i < high.Length; i++)
                high[i] = Transparent;

            switch (kind)
            {
                case CrossingRoadsIconKind.Standard:
                    DrawStandard(high, size);
                    break;
                case CrossingRoadsIconKind.Signal:
                    DrawSignal(high, size);
                    break;
                case CrossingRoadsIconKind.AutoSubway:
                    DrawAutoSubway(high, size);
                    break;
                case CrossingRoadsIconKind.ManualSubway:
                    DrawManualSubway(high, size);
                    break;
                case CrossingRoadsIconKind.Bridge:
                    DrawBridge(high, size);
                    break;
                default:
                    DrawAutoScan(high, size);
                    break;
            }

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.ARGB32, false);
            texture.name = GetSpriteName(kind);
            texture.SetPixels32(Downsample(high, size));
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static void DrawStandard(Color32[] pixels, int size)
        {
            DrawVerticalRoad(pixels, size);
            DrawCrossingBars(pixels, size, 24, 72, 34, 62);
            FillCircle(pixels, size, 16, 48, 5, Cyan);
            FillCircle(pixels, size, 80, 48, 5, Cyan);
        }

        private static void DrawSignal(Color32[] pixels, int size)
        {
            DrawVerticalRoad(pixels, size);
            DrawCrossingBars(pixels, size, 21, 68, 35, 61);
            FillRoundedRect(pixels, size, 67, 8, 91, 84, 8, StructureShadow);
            DrawLine(pixels, size, 79, 84, 79, 93, 4, White);
            FillCircle(pixels, size, 79, 24, 6, Red);
            FillCircle(pixels, size, 79, 46, 6, Yellow);
            FillCircle(pixels, size, 79, 68, 6, Green);
        }

        private static void DrawAutoSubway(Color32[] pixels, int size)
        {
            DrawVerticalRoad(pixels, size);
            DrawSubwayPortal(pixels, size, 5, 31);
            DrawSubwayPortal(pixels, size, 67, 31);
            DrawLine(pixels, size, 19, 73, 28, 83, 4, Blue);
            DrawLine(pixels, size, 28, 83, 68, 83, 4, Blue);
            DrawLine(pixels, size, 68, 83, 77, 73, 4, Blue);
            DrawLine(pixels, size, 48, 24, 48, 54, 5, Cyan);
            FillTriangle(pixels, size, 38, 51, 58, 51, 48, 64, Cyan);
            FillCircle(pixels, size, 48, 14, 5, Yellow);
        }

        private static void DrawManualSubway(Color32[] pixels, int size)
        {
            DrawVerticalRoad(pixels, size);
            DrawSubwayPortal(pixels, size, 5, 40);
            DrawSubwayPortal(pixels, size, 67, 40);
            DrawLine(pixels, size, 19, 78, 28, 86, 4, Blue);
            DrawLine(pixels, size, 28, 86, 68, 86, 4, Blue);
            DrawLine(pixels, size, 68, 86, 77, 78, 4, Blue);
            FillCircle(pixels, size, 18, 26, 8, Yellow);
            FillCircle(pixels, size, 78, 26, 8, Yellow);
            FillCircle(pixels, size, 18, 26, 3, White);
            FillCircle(pixels, size, 78, 26, 3, White);
            DrawLine(pixels, size, 24, 26, 72, 26, 2.5f, Cyan);
        }

        private static void DrawSubwayPortal(Color32[] pixels, int size, float x, float y)
        {
            FillRoundedRect(pixels, size, x, y, x + 24, y + 35, 6, Blue);
            FillRoundedRect(pixels, size, x + 6, y + 7, x + 18, y + 35, 3, StructureShadow);
            for (float stepY = y + 17; stepY <= y + 30; stepY += 5)
                DrawLine(pixels, size, x + 8, stepY, x + 16, stepY, 1.5f, White);
        }

        private static void DrawBridge(Color32[] pixels, int size)
        {
            DrawVerticalRoad(pixels, size);
            FillRoundedRect(pixels, size, 23, 39, 73, 57, 4, StructureShadow);
            FillRoundedRect(pixels, size, 21, 36, 75, 51, 4, Blue);
            DrawLine(pixels, size, 21, 35, 34, 23, 4, Cyan);
            DrawLine(pixels, size, 34, 23, 62, 23, 4, Cyan);
            DrawLine(pixels, size, 62, 23, 75, 35, 4, Cyan);
            DrawLine(pixels, size, 28, 51, 28, 68, 5, White);
            DrawLine(pixels, size, 68, 51, 68, 68, 5, White);
            DrawLine(pixels, size, 6, 84, 28, 54, 6, Blue);
            DrawLine(pixels, size, 90, 84, 68, 54, 6, Blue);
            for (int step = 0; step < 4; step++)
            {
                float offset = step * 5f;
                DrawLine(pixels, size, 7 + offset, 82 - offset * 1.35f, 16 + offset, 82 - offset * 1.35f, 2, White);
                DrawLine(pixels, size, 80 - offset, 82 - offset * 1.35f, 89 - offset, 82 - offset * 1.35f, 2, White);
            }
        }

        private static void DrawAutoScan(Color32[] pixels, int size)
        {
            DrawLine(pixels, size, 12, 24, 70, 24, 13, Road);
            DrawLine(pixels, size, 29, 9, 29, 72, 13, Road);
            DrawLine(pixels, size, 12, 24, 70, 24, 2, RoadEdge);
            DrawLine(pixels, size, 29, 9, 29, 72, 2, RoadEdge);
            FillCircle(pixels, size, 56, 54, 24, Blue);
            FillCircle(pixels, size, 56, 54, 17, StructureShadow);
            DrawLine(pixels, size, 72, 71, 88, 87, 9, Blue);
            DrawLine(pixels, size, 48, 54, 64, 54, 3, Cyan);
            DrawLine(pixels, size, 56, 46, 56, 62, 3, Cyan);
            FillCircle(pixels, size, 19, 24, 4, White);
            FillCircle(pixels, size, 29, 58, 4, Green);
            FillCircle(pixels, size, 57, 24, 4, Yellow);
        }

        private static void DrawVerticalRoad(Color32[] pixels, int size)
        {
            FillRoundedRect(pixels, size, 24, 5, 72, 91, 8, Road);
            DrawLine(pixels, size, 27, 9, 27, 87, 2.5f, RoadEdge);
            DrawLine(pixels, size, 69, 9, 69, 87, 2.5f, RoadEdge);
            for (int y = 10; y <= 78; y += 17)
                DrawLine(pixels, size, 48, y, 48, y + 8, 2, RoadLane);
        }

        private static void DrawCrossingBars(Color32[] pixels, int size, float left, float right, float top, float bottom)
        {
            for (float y = top; y <= bottom; y += 7)
                FillRoundedRect(pixels, size, left, y, right, y + 4, 1.5f, White);
        }

        private static Color32[] Downsample(Color32[] source, int sourceSize)
        {
            Color32[] result = new Color32[TextureSize * TextureSize];
            int samples = Supersample * Supersample;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    int weightedR = 0;
                    int weightedG = 0;
                    int weightedB = 0;
                    int totalAlpha = 0;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            Color32 color = source[
                                ((y * Supersample + sy) * sourceSize)
                                + (x * Supersample + sx)];
                            weightedR += color.r * color.a;
                            weightedG += color.g * color.a;
                            weightedB += color.b * color.a;
                            totalAlpha += color.a;
                        }
                    }

                    result[(y * TextureSize) + x] = totalAlpha == 0
                        ? Transparent
                        : new Color32(
                            (byte)(weightedR / totalAlpha),
                            (byte)(weightedG / totalAlpha),
                            (byte)(weightedB / totalAlpha),
                            (byte)(totalAlpha / samples));
                }
            }

            return result;
        }

        private static void FillRoundedRect(
            Color32[] pixels,
            int size,
            float left,
            float top,
            float right,
            float bottom,
            float radius,
            Color32 color)
        {
            float scaledLeft = left * Supersample;
            float scaledTop = top * Supersample;
            float scaledRight = right * Supersample;
            float scaledBottom = bottom * Supersample;
            float scaledRadius = radius * Supersample;
            int minX = Mathf.Max(0, Mathf.FloorToInt(scaledLeft));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(scaledRight));
            int minY = Mathf.Max(0, Mathf.FloorToInt(scaledTop));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(scaledBottom));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float nearestX = Mathf.Clamp(px, scaledLeft + scaledRadius, scaledRight - scaledRadius);
                    float nearestY = Mathf.Clamp(py, scaledTop + scaledRadius, scaledBottom - scaledRadius);
                    float dx = px - nearestX;
                    float dy = py - nearestY;
                    if (dx * dx + dy * dy <= scaledRadius * scaledRadius)
                        pixels[(y * size) + x] = color;
                }
            }
        }

        private static void FillCircle(
            Color32[] pixels,
            int size,
            float centerX,
            float centerY,
            float radius,
            Color32 color)
        {
            float cx = centerX * Supersample;
            float cy = centerY * Supersample;
            float r = radius * Supersample;
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(cy + r));
            float radiusSqr = r * r;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= radiusSqr)
                        pixels[(y * size) + x] = color;
                }
            }
        }

        private static void DrawLine(
            Color32[] pixels,
            int size,
            float startX,
            float startY,
            float endX,
            float endY,
            float width,
            Color32 color)
        {
            float x0 = startX * Supersample;
            float y0 = startY * Supersample;
            float x1 = endX * Supersample;
            float y1 = endY * Supersample;
            float radius = width * Supersample * 0.5f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - radius));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - radius));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + radius));
            float dx = x1 - x0;
            float dy = y1 - y0;
            float lengthSqr = dx * dx + dy * dy;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float t = lengthSqr <= 0.001f
                        ? 0f
                        : Mathf.Clamp01(((px - x0) * dx + (py - y0) * dy) / lengthSqr);
                    float nearestX = x0 + dx * t;
                    float nearestY = y0 + dy * t;
                    float distanceX = px - nearestX;
                    float distanceY = py - nearestY;
                    if (distanceX * distanceX + distanceY * distanceY <= radius * radius)
                        pixels[(y * size) + x] = color;
                }
            }
        }

        private static void FillTriangle(
            Color32[] pixels,
            int size,
            float x0,
            float y0,
            float x1,
            float y1,
            float x2,
            float y2,
            Color32 color)
        {
            x0 *= Supersample;
            y0 *= Supersample;
            x1 *= Supersample;
            y1 *= Supersample;
            x2 *= Supersample;
            y2 *= Supersample;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, Mathf.Min(x1, x2))));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(x0, Mathf.Max(x1, x2))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, Mathf.Min(y1, y2))));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(y0, Mathf.Max(y1, y2))));
            float denominator = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2);
            if (Mathf.Abs(denominator) < 0.001f)
                return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float a = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / denominator;
                    float b = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / denominator;
                    float c = 1f - a - b;
                    if (a >= 0f && b >= 0f && c >= 0f)
                        pixels[(y * size) + x] = color;
                }
            }
        }
    }
}
