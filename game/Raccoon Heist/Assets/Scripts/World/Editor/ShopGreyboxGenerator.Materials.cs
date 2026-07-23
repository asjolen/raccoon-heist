using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using RaccoonHeist.Player;
using RaccoonHeist.World;

namespace RaccoonHeist.World.Editor
{
    // Materials and procedural textures, created once as assets; the palette lives here.
    public static partial class ShopGreyboxGenerator
    {
        // ---------- materials (created once as assets, palette lives here) ----------

        static Material Mat(string name, Color color, float smoothness = 0.08f)
        {
            if (matCache.TryGetValue(name, out var cached)) return cached;
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Greybox")) AssetDatabase.CreateFolder("Assets/Materials", "Greybox");
            string path = $"Assets/Materials/Greybox/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color; // palette tweaks here propagate to the saved asset on rebuild
            mat.SetFloat("_Smoothness", smoothness);
            matCache[name] = mat;
            return mat;
        }

        static Material EmissiveMat(string name, Color baseColor, Color emission)
        {
            var mat = Mat(name, baseColor, 0.22f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }

        static Material TransparentMat(string name, Color color, float smoothness = 0.72f)
        {
            var mat = Mat(name, color, smoothness);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f); // alpha
            mat.SetFloat("_AlphaClip", 0f);
            // Derived state (_SrcBlend, keywords, queue) must come from URP itself:
            // hand-authored values drift from what ValidateMaterial recomputes, and
            // every revalidation then rewrites the saved asset with a different look.
            BaseShaderGUI.SetupMaterialBlendMode(mat);
            BaseShaderGUI.SetMaterialKeywords(mat, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
            return mat;
        }

        static Material Wall => Mat("Wall", new Color(0.42f, 0.40f, 0.36f));
        static Material Wood => Mat("ShelfWood", new Color(0.42f, 0.31f, 0.21f));
        static Material Crate => Mat("Crate", new Color(0.55f, 0.42f, 0.28f));

        // ---------- procedural textures (generated once as PNG assets) ----------

        static Material TiledMat(string name, Color tint, Texture2D tex, float tileX, float tileY, float smoothness = 0.08f)
        {
            string key = $"{name}_{Mathf.RoundToInt(tileX)}x{Mathf.RoundToInt(tileY)}";
            if (matCache.TryGetValue(key, out var cached)) return cached;
            var mat = Mat(key, tint, smoothness);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(tileX, tileY));
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        static Texture2D EnsureTex(string name, System.Func<int, int, Color> pixel, int size = 256, int height = 0)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Greybox")) AssetDatabase.CreateFolder("Assets/Materials", "Greybox");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Greybox/Textures")) AssetDatabase.CreateFolder("Assets/Materials/Greybox", "Textures");
            string path = $"Assets/Materials/Greybox/Textures/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            if (height == 0) height = size;
            var tex = new Texture2D(size, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, pixel(x, y));
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Hash(int x, int y) => Mathf.Abs(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f) % 1f;

        static Texture2D TexAsphalt => EnsureTex("tex_asphalt", (x, y) =>
        {
            float n = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
            if (Hash(x, y) > 0.985f) n += 0.18f; // speckle
            return new Color(n, n, n);
        });

        static Texture2D TexSlabs => EnsureTex("tex_slabs", (x, y) =>
        {
            // 2 paving slabs per tile with grout lines
            float n = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
            if (x % 128 < 3 || y % 128 < 3) n *= 0.62f;
            return new Color(n, n, n);
        });

        static Texture2D TexRough => EnsureTex("tex_rough", (x, y) =>
        {
            float n = 0.72f + 0.18f * Mathf.PerlinNoise(x * 0.03f, y * 0.03f)
                             + 0.10f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
            return new Color(n, n, n);
        });

        static Texture2D TexRoad => EnsureTex("tex_road", (x, y) =>
        {
            // Asphalt with a dashed centre line and solid edge lines (v spans road width)
            float n = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
            if (Hash(x, y) > 0.985f) n += 0.15f;
            bool dash = y >= 124 && y < 132 && x < 128;
            bool edge = y < 6 || y > 249;
            if (dash || edge) return new Color(Mathf.Min(1f, 1.7f * n), Mathf.Min(1f, 1.7f * n), Mathf.Min(1f, 1.6f * n));
            return new Color(n, n, n);
        });

        static Texture2D TexBrick => EnsureTex("tex_brick", (x, y) =>
        {
            // Running-bond bricks: 8 rows per tile, alternate rows offset half a brick
            int row = y / 32;
            int xo = (row % 2 == 0) ? 0 : 32;
            bool mortar = (y % 32) < 3 || ((x + xo) % 64) < 3;
            if (mortar) return new Color(0.78f, 0.76f, 0.73f);
            float v = 0.72f + 0.28f * Hash((x + xo) / 64 * 7 + 1, row * 13 + 1);
            float n = 0.92f + 0.08f * Hash(x, y);
            return new Color(v * n, v * n * 0.96f, v * n * 0.92f);
        });

        static Texture2D TexPlanks => EnsureTex("tex_planks", (x, y) =>
        {
            // 8 floor planks per tile with grain
            float n = 0.8f + 0.2f * Hash(x / 32 * 31 + 7, 3);
            n *= 0.92f + 0.08f * Mathf.PerlinNoise(x * 0.02f, y * 0.3f);
            if (x % 32 < 2) n *= 0.55f;
            if (Hash(0, y / 96 + x / 32 * 17) > 0.8f && y % 96 < 2) n *= 0.6f; // plank ends
            return new Color(n, n * 0.94f, n * 0.86f);
        });

        static Material BrickMat(float lengthMeters) =>
            TiledMat("Brick", new Color(0.62f, 0.45f, 0.37f), TexBrick, Mathf.Max(1f, lengthMeters / 2f), 2f);

        // Every city pavement uses the same damp slab texture and palette. Tile counts
        // derive from world size so split intersection pieces keep the same one-metre
        // slab rhythm instead of stretching or changing texture at the perimeter.
        static Material SidewalkMat(float widthMeters, float depthMeters) =>
            TiledMat("SlabsDamp", new Color(0.44f, 0.45f, 0.49f), TexSlabs,
                Mathf.Max(1f, widthMeters * 0.5f), Mathf.Max(1f, depthMeters * 0.5f), 0.22f);

    }
}
