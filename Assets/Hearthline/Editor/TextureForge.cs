using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Generates the greybox kit's tiling surface textures: a near-white detail map (multiplied by each
    /// material's palette colour, so colours stay owned by palette.json) and a matching normal map per surface
    /// type: bark, planks, stone, rock, concrete, corrugated tin, rust, canvas, leaves, ground grass and soil.
    /// Everything is procedural and seamless; no outside images. Stand-in art (ADR 0018).
    /// </summary>
    public static class TextureForge
    {
        public const string Folder = GreyboxKit.Folder + "/Textures";
        private const int Size = 512;

        public static readonly string[] Surfaces = { "bark", "planks", "stone", "rock", "concrete", "tin", "rust", "canvas", "leaves", "grass", "soil" };

        /// <summary>Writes every surface's albedo and normal map and sets their import settings.</summary>
        public static void GenerateAll()
        {
            SceneBuildKit.EnsureFolder(Folder);
            foreach (string surface in Surfaces) Generate(surface);
            GenerateLeafCluster();
            AssetDatabase.Refresh();
            foreach (string surface in Surfaces)
            {
                Import(Folder + "/" + surface + "_albedo.png", normal: false);
                Import(Folder + "/" + surface + "_normal.png", normal: true);
            }
            ImportCutout(LeafClusterPath);
        }

        public const string LeafClusterPath = Folder + "/leafcluster_albedo.png";
        public const float LeafCutoff = 0.5f;

        public static Texture2D LeafCluster() => AssetDatabase.LoadAssetAtPath<Texture2D>(LeafClusterPath);

        // Cut-out textures keep their coverage in the small mips, or leaves thin out and vanish with distance.
        private static void ImportCutout(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = LeafCutoff;
            importer.anisoLevel = 4;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
        }

        // ---- Leaf cluster (leaf cards, ADR 0020) ------------------------------------------------------------

        /// <summary>
        /// A spray of broad leaves on a transparent background, for the cards that make up a tree's crown: a
        /// twig enters at the bottom centre and leaves fan out from it. Near-white like the other maps (the
        /// palette colour tints it); alpha is the cut-out.
        /// </summary>
        private static void GenerateLeafCluster()
        {
            var rng = new System.Random(20);
            var shade = new float[Size, Size];
            var alpha = new float[Size, Size];
            const int leaves = 80;
            var twigs = new System.Collections.Generic.List<Vector4>(); // (x0, y0, x1, y1)
            for (int i = 0; i < leaves; i++)
            {
                // Bases scattered over a rounded spray; each leaf points away from the middle, roughly.
                float r = 0.32f * Mathf.Sqrt((float)rng.NextDouble());
                float theta = (float)rng.NextDouble() * Mathf.PI * 2f;
                float bx = 0.5f + Mathf.Cos(theta) * r, by = 0.5f + Mathf.Sin(theta) * r * 0.9f;
                float angle = Mathf.Atan2(by - 0.42f, bx - 0.5f) + ((float)rng.NextDouble() - 0.5f) * (1.3f + 4f * (1f - r / 0.32f));
                float length = 0.11f + (float)rng.NextDouble() * 0.08f;
                float width = length * (0.26f + (float)rng.NextDouble() * 0.1f);
                float tone = 0.7f + 0.3f * (float)rng.NextDouble();
                if (i % 6 == 0) twigs.Add(new Vector4(0.5f, 0.12f, bx, by));
                float dx = Mathf.Cos(angle), dy = Mathf.Sin(angle);
                float half = length + width;
                int x0 = Mathf.Max(0, (int)((bx - half) * Size)), x1 = Mathf.Min(Size - 1, (int)((bx + half) * Size));
                int y0 = Mathf.Max(0, (int)((by - half) * Size)), y1 = Mathf.Min(Size - 1, (int)((by + half) * Size));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x / (float)Size - bx, py = y / (float)Size - by;
                    float along = (px * dx + py * dy) / length;        // 0 at the stem .. 1 at the tip
                    float across = (-px * dy + py * dx) / width;       // -1 .. 1 across the blade
                    if (along <= 0f || along >= 1f) continue;
                    float edge = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(along, 0.8f)), 0.75f);
                    float a = Mathf.Abs(across);
                    if (a >= edge) continue;
                    float rib = Mathf.SmoothStep(0f, 0.08f, a);       // darker midrib
                    float veins = 0.97f + 0.03f * Mathf.Sin((along * 7f - a * 2.5f) * Mathf.PI);
                    float rim = 1f - 0.18f * Mathf.SmoothStep(0.6f, 1f, a / edge);
                    float tipLight = 0.88f + 0.12f * along;
                    // Kept near white (about 0.85 on average) like the other kit maps; the palette colour does the rest.
                    shade[x, y] = Mathf.Clamp01(0.62f + 0.38f * tone * (0.85f + 0.15f * rib) * veins * rim * tipLight);
                    alpha[x, y] = 1f;
                }
                // The leaf's stalk.
                for (float s = -0.06f; s < 0f; s += 0.5f / Size)
                {
                    int sx = (int)((bx + dx * s * length * 4f) * Size), sy = (int)((by + dy * s * length * 4f) * Size);
                    if (sx < 1 || sy < 1 || sx >= Size - 1 || sy >= Size - 1) continue;
                    shade[sx, sy] = 0.55f; alpha[sx, sy] = 1f;
                }
            }
            // Twigs from the bottom of the card out to some of the leaves, drawn behind them.
            foreach (Vector4 twig in twigs)
                for (float k = 0f; k <= 1f; k += 0.5f / Size)
                {
                    int tx = (int)(Mathf.Lerp(twig.x, twig.z, k) * Size), ty = (int)(Mathf.Lerp(twig.y, twig.w, k) * Size);
                    for (int w = -1; w <= 1; w++)
                    {
                        int xx = Mathf.Clamp(tx + w, 0, Size - 1);
                        if (alpha[xx, ty] < 1f) { shade[xx, ty] = 0.45f; alpha[xx, ty] = 1f; }
                    }
                }

            // Bleed leaf colour into the transparent pixels so filtering doesn't draw dark fringes.
            for (int pass = 0; pass < 8; pass++)
            {
                var next = (float[,])shade.Clone();
                for (int y = 1; y < Size - 1; y++)
                for (int x = 1; x < Size - 1; x++)
                {
                    if (alpha[x, y] > 0f || shade[x, y] > 0f) continue;
                    float sum = 0f; int n = 0;
                    for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++)
                        if (shade[x + i, y + j] > 0f) { sum += shade[x + i, y + j]; n++; }
                    if (n > 0) next[x, y] = sum / n;
                }
                shade = next;
            }
            Write(System.IO.Path.GetFileName(LeafClusterPath), (x, y) =>
            {
                float s = shade[x, y] > 0f ? shade[x, y] : 0.8f;
                return new Color(s, s, s, alpha[x, y]);
            }, withAlpha: true);
        }

        public static Texture2D Albedo(string surface) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + surface + "_albedo.png");
        public static Texture2D Normal(string surface) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + surface + "_normal.png");

        private static void Import(string path, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
        }

        // ---- Surfaces ---------------------------------------------------------------------------------

        private static void Generate(string surface)
        {
            var height = new float[Size, Size];
            var shade = new float[Size, Size]; // albedo brightness, 0..1
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                (float h, float a) = Sample(surface, u, v);
                height[x, y] = h;
                shade[x, y] = a;
            }
            Write(surface + "_albedo.png", (x, y) => { float a = shade[x, y]; return new Color(a, a, a, 1f); });
            float strength = surface == "canvas" || surface == "leaves" ? 2f : surface == "tin" ? 5f : 4f;
            Write(surface + "_normal.png", (x, y) =>
            {
                float l = height[(x + Size - 1) % Size, y], r = height[(x + 1) % Size, y];
                float d = height[x, (y + Size - 1) % Size], u = height[x, (y + 1) % Size];
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            });
        }

        // Height (0..1) and albedo brightness for one surface at (u, v) in [0,1).
        private static (float height, float shade) Sample(string surface, float u, float v)
        {
            switch (surface)
            {
                case "bark":
                {
                    // Long vertical fibres with deep cracks between ridges.
                    float warp = Fbm(u, v, 4, 3, 1) * 0.15f;
                    float ridges = Mathf.Abs(Mathf.Sin((u + warp) * Mathf.PI * 14f));
                    float fibre = Fbm(u * 1f, v * 0.25f, 16, 4, 2);
                    float h = Mathf.Lerp(ridges, fibre, 0.4f);
                    return (h, 0.62f + 0.38f * Mathf.Pow(h, 0.7f));
                }
                case "planks":
                {
                    // Boards across v, grain along u, dark gaps and the odd knot.
                    const int boards = 6;
                    float b = v * boards, row = Mathf.Floor(b), f = b - row;
                    float gap = Mathf.SmoothStep(0f, 1f, Mathf.Min(f, 1f - f) * 14f);
                    // Grain runs along the board (u): lines across the board's width that waver along it.
                    float grain = Fbm(u, v, 4, 3, (int)row + 3);
                    float lines = 0.5f + 0.5f * Mathf.Sin((f * 9f + grain * 3f + row * 1.3f) * Mathf.PI);
                    float tone = 0.8f + 0.2f * Hash01((int)row, 11);
                    float h = gap * (0.75f + 0.25f * lines);
                    return (h, (0.78f + 0.1f * lines + 0.12f * grain) * tone * Mathf.Lerp(0.55f, 1f, gap));
                }
                case "stone":
                case "rock":
                {
                    // Fieldstone: cells with mortar for stone; cracked strata for rock.
                    (float d1, float d2, int id) = Cells(u, v, surface == "stone" ? 6 : 4, 7);
                    float edge = Mathf.Clamp01((d2 - d1) * (surface == "stone" ? 9f : 4f));
                    float grit = Fbm(u, v, 12, 4, 5);
                    float h = surface == "stone" ? Mathf.Sqrt(edge) * (0.8f + 0.2f * grit) : Mathf.Lerp(edge, grit, 0.6f);
                    float tone = 0.75f + 0.25f * Hash01(id, 3);
                    return (h, (0.55f + 0.45f * h) * tone);
                }
                case "concrete":
                {
                    float speck = Fbm(u, v, 32, 3, 9);
                    float stain = Fbm(u, v, 3, 4, 10);
                    float crack = Mathf.Clamp01(1f - Mathf.Abs(Fbm(u, v, 5, 5, 12) - 0.5f) * 90f);
                    float h = 0.6f + 0.25f * speck - 0.35f * crack;
                    return (h, Mathf.Clamp01(0.74f + 0.16f * speck - 0.2f * stain - 0.22f * crack));
                }
                case "tin":
                {
                    // Corrugated roofing along u, with rust blooming through.
                    float corr = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 16f);
                    float rust = Mathf.SmoothStep(0.45f, 0.75f, Fbm(u, v, 5, 5, 21));
                    return (corr, Mathf.Lerp(0.85f + 0.15f * corr, 0.55f, rust));
                }
                case "rust":
                {
                    float pits = Fbm(u, v, 20, 4, 22);
                    float blotch = Fbm(u, v, 4, 4, 23);
                    return (pits, 0.55f + 0.45f * Mathf.Lerp(pits, blotch, 0.5f));
                }
                case "canvas":
                {
                    // A plain weave plus soft creases and grime.
                    float wx = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 64f), wy = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 64f);
                    float weave = ((int)(u * 64f) + (int)(v * 64f)) % 2 == 0 ? wx : wy;
                    float crease = Fbm(u, v, 3, 3, 31);
                    float grime = Fbm(u, v, 6, 4, 32);
                    return (weave * 0.5f + crease * 0.5f, 0.78f + 0.12f * weave + 0.1f * crease - 0.15f * grime);
                }
                case "leaves":
                {
                    // Clumps of leaves: bumpy cells, lighter at their tops.
                    (float d1, float d2, int id) = Cells(u, v, 18, 41);
                    float h = Mathf.Clamp01(1f - d1 * 1.1f);
                    h *= h;
                    return (h, 0.7f + 0.3f * h * (0.7f + 0.3f * Hash01(id, 2)));
                }
                case "grass":
                {
                    // Close-up ground cover: blades as short streaks, mixed with clover-ish blobs.
                    float blades = Fbm(u * 1f, v * 0.2f, 48, 3, 51);
                    float clumps = Fbm(u, v, 10, 4, 52);
                    return (blades, 0.7f + 0.3f * Mathf.Lerp(blades, clumps, 0.4f));
                }
                default: // soil
                {
                    float clods = Fbm(u, v, 14, 5, 61);
                    (float d1, _, _) = Cells(u, v, 22, 62);
                    float pebbles = Mathf.Clamp01(1f - d1 * 4.5f);
                    return (Mathf.Max(clods * 0.7f, pebbles), 0.7f + 0.3f * clods + 0.15f * pebbles);
                }
            }
        }

        // ---- Seamless noise -----------------------------------------------------------------------------

        private static float Hash01(int x, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)seed * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static float Hash01(int x, int y, int seed) => Hash01(x * 73856093 ^ y * 19349663, seed);

        // Value noise on a lattice of `period` cells that wraps, so the texture tiles.
        private static float Noise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wrap(int i) => ((i % period) + period) % period;
            float a = Hash01(Wrap(x0), Wrap(y0), seed), b = Hash01(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash01(Wrap(x0), Wrap(y0 + 1), seed), d = Hash01(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = Mathf.Max(1, basePeriod);
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f), period, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        // Distances to the nearest and second-nearest jittered cell centres, wrapping.
        private static (float d1, float d2, int id) Cells(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float d1 = 9f, d2 = 9f;
            int id = 0;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                int wx = ((gx % period) + period) % period, wy = ((gy % period) + period) % period;
                float px = gx + Hash01(wx, wy, seed), py = gy + Hash01(wx, wy, seed + 1);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < d1) { d2 = d1; d1 = d; id = wx * 131 + wy; }
                else if (d < d2) d2 = d;
            }
            return (d1, d2, id);
        }

        private static void Write(string file, Func<int, int, Color> pixel, bool withAlpha = false)
        {
            var tex = new Texture2D(Size, Size, withAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel(x, y);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Folder, file), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
