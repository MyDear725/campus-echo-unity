using System.IO;
using UnityEditor;
using UnityEngine;

// Generates the procedural surface textures the library interior uses: speckled granite
// floor tiles, warm oak grain and grey stone for the dado. Kept deterministic so the
// build is reproducible, and written as real PNG assets so materials can reference them.
public static class LibraryTextureGen
{
    const string Dir = "Assets/Interiors/Textures";

    [MenuItem("Campus/Interiors/Regenerate Library Textures")]
    public static void Generate()
    {
        Directory.CreateDirectory(Dir);

        Save("Library_Granite_Tile.png", GraniteTile(1024));
        Save("Library_Oak_Grain.png", OakGrain(512));
        Save("Library_Stone_Dado.png", StoneDado(512));

        AssetDatabase.Refresh();
        Configure("Library_Granite_Tile.png", 8f, 0f);
        Configure("Library_Oak_Grain.png", 1f, 1f);
        Configure("Library_Stone_Dado.png", 4f, 0f);
        AssetDatabase.SaveAssets();
        Debug.Log("LIBRARY_TEXTURES_DONE");
    }

    static void Save(string name, Texture2D tex)
    {
        File.WriteAllBytes(Path.Combine(Dir, name), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void Configure(string name, float tiling, float aniso)
    {
        string path = Dir + "/" + name;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!imp) return;
        imp.textureType = TextureImporterType.Default;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.filterMode = FilterMode.Bilinear;
        imp.anisoLevel = Mathf.RoundToInt(aniso) > 0 ? 4 : 1;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
    }

    // 2x2 tiles of fine speckled grey granite with narrow darker joints. The repeat gives
    // a 1.2 m module once tiled in the material.
    static Texture2D GraniteTile(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        var px = new Color32[size * size];
        var rng = new System.Random(20240517);

        int half = size / 2;
        const int joint = 3;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int lx = x % half, ly = y % half;
                bool isJoint = lx < joint || ly < joint;

                // Base tone varies very slightly per tile so the floor is not flat.
                int tileIndex = (x / half) + (y / half) * 2;
                float tileShift = ((tileIndex * 37) % 11 - 5) * 0.0016f;

                float n = (float)rng.NextDouble();
                float speck = n < 0.045f ? -0.16f : n > 0.955f ? 0.13f : 0f;

                float v = 0.455f + tileShift + speck + ((float)rng.NextDouble() - 0.5f) * 0.05f;
                if (isJoint) v = 0.30f;

                var c = new Color32(
                    (byte)Mathf.Clamp(v * 255f, 0f, 255f),
                    (byte)Mathf.Clamp((v + 0.004f) * 255f, 0f, 255f),
                    (byte)Mathf.Clamp((v + 0.012f) * 255f, 0f, 255f),
                    255);
                px[y * size + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // Straight, low-contrast oak grain: warm base with soft darker fibres running along Y.
    static Texture2D OakGrain(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        var px = new Color32[size * size];
        var rng = new System.Random(7771);

        float[] fibres = new float[size];
        for (int i = 0; i < size; i++) fibres[i] = (float)rng.NextDouble();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float warp = Mathf.Sin(x * 0.055f) * 2.2f;
                int fi = Mathf.Clamp(Mathf.RoundToInt((x + warp) % size + size) % size, 0, size - 1);
                float grain = Mathf.Sin((y * 0.13f + fibres[fi] * 9f) * 1.4f) * 0.5f + 0.5f;

                float v = 0.78f + grain * 0.13f + (fibres[fi] - 0.5f) * 0.06f;
                var c = new Color32(
                    (byte)Mathf.Clamp(v * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(v * 0.845f * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(v * 0.70f * 255f, 0f, 255f),
                    255);
                px[y * size + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // Horizontal bedding lines for the grey stone dado.
    static Texture2D StoneDado(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        var px = new Color32[size * size];
        var rng = new System.Random(4242);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float band = Mathf.Sin(y * 0.085f) * 0.5f + 0.5f;
                float v = 0.50f + band * 0.055f + ((float)rng.NextDouble() - 0.5f) * 0.075f;
                if (y % 128 < 2) v -= 0.10f;   // course joints
                var c = new Color32(
                    (byte)Mathf.Clamp(v * 255f, 0f, 255f),
                    (byte)Mathf.Clamp((v + 0.006f) * 255f, 0f, 255f),
                    (byte)Mathf.Clamp((v + 0.014f) * 255f, 0f, 255f),
                    255);
                px[y * size + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }
}
