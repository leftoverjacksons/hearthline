using System;
using System.Collections.Generic;

namespace Hearthline.Sim.World
{
    public struct Vec2
    {
        public double X, Z;
        public Vec2(double x, double z) { X = x; Z = z; }
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Z * s);
        public double Length => Math.Sqrt(X * X + Z * Z);
        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;
    }

    /// <summary>A point on a stream or road, with its width (m) and, for water, the surface height.</summary>
    public struct PathPoint
    {
        public Vec2 P;
        public double Width;
        public double Surface;
    }

    public sealed class Waterway
    {
        public readonly List<PathPoint> Points = new List<PathPoint>();
        /// <summary>Catchment size at the mouth, in cells: bigger is a bigger stream.</summary>
        public double Flow;
        public bool IsRiver;
    }

    public sealed class Bridge
    {
        public Vec2 From, To;
        public double DeckHeight;
    }

    /// <summary>A place where a ruin makes sense, and what's around it, so content can say which kind belongs here.</summary>
    public sealed class RuinSite
    {
        public Vec2 P;
        /// <summary>Metres to the old road's centre line, to the water's edge, and to home.</summary>
        public double Road, Water, Home;
        public double Slope;
        /// <summary>Height above the ground 40 m around (m): positive on a rise.</summary>
        public double Rise;
        /// <summary>Degrees about the vertical (0 = +z) that the site faces: toward the road.</summary>
        public double Facing;
    }

    public sealed class WorldSettings
    {
        public ulong Seed = 1;
        /// <summary>Generated square, metres. The playable region sits in the middle; the rest is backdrop.</summary>
        public double Size = 1200;
        public double Cell = 2.5;
        public double MountainHeight = 240;
        /// <summary>The homestead's flat bench (centre is the world origin).</summary>
        public double HomeRadius = 40;
        public int Droplets = 90000;
        /// <summary>Catchment (cells) for a stream to appear, and for a stream to count as the river.</summary>
        public double StreamCells = 1400, RiverCells = 12000;
        /// <summary>The stream nearest home should pass within this band (m), so the homestead has water.</summary>
        public double HomeWaterMin = 30, HomeWaterMax = 110;
    }

    /// <summary>
    /// The generated land (docs/adr/0024-world-generator.md), built in the order nature builds it:
    /// 1. **land:** valley floor, foothills and a mountain to the north, then hydraulic erosion;
    /// 2. **water** found from the land: where rain gathers, streams cut channels and join into a river;
    /// 3. **plants:** moisture, slope and height give forest density;
    /// 4. **people last:** the homestead on a flat bench near water, the old highway routed the easy way
    ///    across the land, bridges wherever it meets water, a track home, and sites where ruins make sense.
    ///
    /// Pure and seeded: the same settings always give the same world, so a save only needs the seed (and,
    /// later, generating pieces on demand for an unending world is a continuation, not a rewrite).
    /// </summary>
    public sealed class WorldMap
    {
        public readonly WorldSettings Settings;
        public readonly int N;
        public readonly double Cell, Origin;
        public readonly float[] Height;
        public readonly float[] Flow;      // catchment, cells
        public readonly float[] Moisture;  // 0..1
        public readonly float[] Forest;    // 0..1
        public readonly float[] Slope;     // rise over run
        public readonly List<Waterway> Streams = new List<Waterway>();
        public readonly List<PathPoint> Road = new List<PathPoint>();
        public readonly List<PathPoint> Track = new List<PathPoint>();
        public readonly List<Bridge> Bridges = new List<Bridge>();
        public readonly List<Vec2> RuinSites = new List<Vec2>();
        /// <summary>The ruin sites with what's around them, in the same order as <see cref="RuinSites"/>.</summary>
        public readonly List<RuinSite> Sites = new List<RuinSite>();
        /// <summary>The Elder Works: a flat bench on the mountain front, far from home and the road.</summary>
        public Vec2 ElderWorks;
        public double ElderWorksHeight;
        public const double ElderWorksRadius = 16;
        public double HomeHeight;

        public WorldMap(WorldSettings settings)
        {
            Settings = settings;
            Cell = settings.Cell;
            N = (int)Math.Round(settings.Size / Cell) + 1;
            Origin = -settings.Size / 2;
            Height = new float[N * N];
            Flow = new float[N * N];
            Moisture = new float[N * N];
            Forest = new float[N * N];
            Slope = new float[N * N];
        }

        public int Index(int i, int j) => j * N + i;
        public Vec2 PositionOf(int i, int j) => new Vec2(Origin + i * Cell, Origin + j * Cell);
        public bool Inside(double x, double z) => x >= Origin && z >= Origin && x <= -Origin && z <= -Origin;

        /// <summary>Ground height at a point, bilinear between cells.</summary>
        public double HeightAt(double x, double z) => Sample(Height, x, z);
        public double MoistureAt(double x, double z) => Sample(Moisture, x, z);
        public double ForestAt(double x, double z) => Sample(Forest, x, z);
        public double SlopeAt(double x, double z) => Sample(Slope, x, z);

        private double Sample(float[] field, double x, double z)
        {
            double fx = Math.Max(0, Math.Min(N - 1.001, (x - Origin) / Cell)), fz = Math.Max(0, Math.Min(N - 1.001, (z - Origin) / Cell));
            int i = (int)fx, j = (int)fz;
            double tx = fx - i, tz = fz - j;
            double a = field[Index(i, j)], b = field[Index(i + 1, j)], c = field[Index(i, j + 1)], d = field[Index(i + 1, j + 1)];
            return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz;
        }

        /// <summary>The nearest water to a point: distance (m), and that stream point.</summary>
        public double DistanceToWater(double x, double z, out PathPoint nearest)
        {
            nearest = default;
            double best = double.MaxValue;
            var p = new Vec2(x, z);
            foreach (Waterway s in Streams)
                foreach (PathPoint q in s.Points)
                {
                    double d = Vec2.Distance(p, q.P) - q.Width / 2;
                    if (d < best) { best = d; nearest = q; }
                }
            return Math.Max(0, best);
        }

        /// <summary>
        /// What kind of place a point is (an area tag from vocab.yaml), read from what's actually there:
        /// the homestead bench, the road and its bridges, water, ruins, high ground, then the plants.
        /// </summary>
        public string AreaAt(double x, double z)
        {
            if (Math.Sqrt(x * x + z * z) < Settings.HomeRadius * 1.5) return "homestead";
            if (Vec2.Distance(ElderWorks, new Vec2(x, z)) < ElderWorksRadius * 1.6) return "elder_works";
            foreach (Vec2 r in RuinSites) if (Vec2.Distance(r, new Vec2(x, z)) < 28) return "ruin";
            foreach (Bridge b in Bridges) if (Vec2.Distance((b.From + b.To) * 0.5, new Vec2(x, z)) < 22) return "river_crossing";
            if (DistanceToPath(Road, x, z) < 10) return "road";
            if (DistanceToWater(x, z, out _) < 8) return "creek";
            if (HeightAt(x, z) > HomeHeight + 55 || SlopeAt(x, z) > 0.7) return "ridge";
            double f = ForestAt(x, z);
            if (f > 0.72) return "deep_woods";
            if (f > 0.35) return "woods";
            return "field";
        }

        public static double DistanceToPath(List<PathPoint> path, double x, double z)
        {
            double best = double.MaxValue;
            var p = new Vec2(x, z);
            for (int k = 0; k + 1 < path.Count; k++)
            {
                Vec2 a = path[k].P, b = path[k + 1].P, ab = b - a;
                double len2 = ab.X * ab.X + ab.Z * ab.Z;
                double t = len2 < 1e-9 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * ab.X + (p.Z - a.Z) * ab.Z) / len2));
                double d = Vec2.Distance(p, a + ab * t);
                if (d < best) best = d;
            }
            return best;
        }
    }

    public static class WorldGen
    {
        public static WorldMap Generate(WorldSettings settings)
        {
            // Try seeds in a fixed sequence until the homestead has water nearby (deterministic).
            for (ulong attempt = 0; attempt < 12; attempt++)
            {
                WorldMap map = Build(settings, settings.Seed * 1000 + attempt);
                double d = map.DistanceToWater(0, 0, out _);
                if (d >= settings.HomeWaterMin && d <= settings.HomeWaterMax) return map;
                if (attempt == 11) return map;
            }
            throw new InvalidOperationException("unreachable");
        }

        private static WorldMap Build(WorldSettings s, ulong seed)
        {
            var map = new WorldMap(s);
            var rng = new SimRandom(SimRandom.Seed(seed, 91));
            var noise = new Noise(rng);
            Land(map, noise);
            Bench(map);
            Erode(map, new SimRandom(SimRandom.Seed(seed, 92)));
            Bench(map);
            Water(map);
            ComputeSlope(map);
            Plants(map, noise);
            Roads(map);
            Ruins(map, new SimRandom(SimRandom.Seed(seed, 93)));
            ElderWorks(map);
            ComputeSlope(map);
            DescribeSites(map);
            return map;
        }

        // ---- 1. Land --------------------------------------------------------------------------------------

        private static double Smooth(double a, double b, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }

        private static void Land(WorldMap map, Noise n)
        {
            WorldSettings s = map.Settings;
            for (int j = 0; j < map.N; j++)
            for (int i = 0; i < map.N; i++)
            {
                Vec2 p = map.PositionOf(i, j);
                double x = p.X, z = p.Z;
                // The valley floor: low rolling ground.
                double h = 7 * n.Fbm(x / 200, z / 200, 4) + 2.5 * n.Fbm(x / 45 + 13, z / 45 + 7, 3);
                // A mountain rising to the north, its front wandering; ridged so it has spurs and gullies.
                double front = 190 + 60 * n.Fbm(x / 380 + 3, 1.7, 3);
                double m = Smooth(front, front + 330, z);
                double ridged = 1 - Math.Abs(n.Fbm(x / 260 + 9, z / 260 + 4, 5));
                h += m * s.MountainHeight * (0.45 + 0.55 * ridged * ridged);
                // Foothills between the valley and the mountain; low hills closing the valley east and west.
                h += Smooth(front - 180, front, z) * 28 * (0.5 + 0.5 * n.Fbm(x / 110 + 21, z / 110 + 2, 4));
                h += Smooth(260, 560, Math.Abs(x)) * 45 * (0.4 + 0.6 * n.Fbm(x / 160 + 5, z / 160 + 31, 4));
                // The valley tilts gently south so water leaves it.
                h -= Smooth(-100, -600, z) * 10;
                map.Height[map.Index(i, j)] = (float)h;
            }
        }

        // The homestead's bench: flat ground at the origin, blending into the land around it.
        private static void Bench(WorldMap map)
        {
            WorldSettings s = map.Settings;
            double sum = 0; int count = 0;
            for (int j = 0; j < map.N; j++)
            for (int i = 0; i < map.N; i++)
            {
                Vec2 p = map.PositionOf(i, j);
                if (p.Length < s.HomeRadius) { sum += map.Height[map.Index(i, j)]; count++; }
            }
            double target = count > 0 ? sum / count : 0;
            map.HomeHeight = target;
            for (int j = 0; j < map.N; j++)
            for (int i = 0; i < map.N; i++)
            {
                double d = map.PositionOf(i, j).Length;
                double w = 1 - Smooth(s.HomeRadius, s.HomeRadius * 2.6, d);
                if (w <= 0) continue;
                int k = map.Index(i, j);
                map.Height[k] = (float)(map.Height[k] * (1 - w) + target * w);
            }
        }

        // Hydraulic erosion: raindrops roll downhill, pick up sediment where they speed up, drop it where they
        // slow. Carves gullies down the slopes and fans of soil at their feet.
        private static void Erode(WorldMap map, SimRandom rng)
        {
            int n = map.N;
            float[] h = map.Height;
            const double inertia = 0.06, capacityFactor = 5, minCapacity = 0.01, depositRate = 0.3, erodeRate = 0.3, evaporate = 0.015, gravity = 4;
            const int radius = 2, maxSteps = 60;
            double cell = map.Cell;
            for (int drop = 0; drop < map.Settings.Droplets; drop++)
            {
                double x = rng.NextDouble() * (n - 2), z = rng.NextDouble() * (n - 2);
                double dx = 0, dz = 0, speed = 1, water = 1, sediment = 0;
                for (int step = 0; step < maxSteps; step++)
                {
                    int i = (int)x, j = (int)z;
                    if (i < 1 || j < 1 || i >= n - 2 || j >= n - 2) break;
                    double ox = x - i, oz = z - j;
                    Gradient(h, n, x, z, out double gx, out double gz, out double height);
                    dx = dx * inertia - gx * (1 - inertia);
                    dz = dz * inertia - gz * (1 - inertia);
                    double len = Math.Sqrt(dx * dx + dz * dz);
                    if (len < 1e-9) break;
                    dx /= len; dz /= len;
                    x += dx; z += dz;
                    if (x < 1 || z < 1 || x >= n - 2 || z >= n - 2) break;
                    Gradient(h, n, x, z, out _, out _, out double newHeight);
                    double delta = newHeight - height;
                    double capacity = Math.Max(-delta / cell, minCapacity) * speed * water * capacityFactor;
                    if (sediment > capacity || delta > 0)
                    {
                        double amount = delta > 0 ? Math.Min(delta, sediment) : (sediment - capacity) * depositRate;
                        sediment -= amount;
                        h[map.Index(i, j)] += (float)(amount * (1 - ox) * (1 - oz));
                        h[map.Index(i + 1, j)] += (float)(amount * ox * (1 - oz));
                        h[map.Index(i, j + 1)] += (float)(amount * (1 - ox) * oz);
                        h[map.Index(i + 1, j + 1)] += (float)(amount * ox * oz);
                    }
                    else
                    {
                        double amount = Math.Min((capacity - sediment) * erodeRate, -delta);
                        double total = 0;
                        for (int b = -radius; b <= radius; b++) for (int a = -radius; a <= radius; a++) total += Math.Max(0, radius - Math.Sqrt(a * a + b * b));
                        for (int b = -radius; b <= radius; b++)
                        for (int a = -radius; a <= radius; a++)
                        {
                            int ci = i + a, cj = j + b;
                            if (ci < 0 || cj < 0 || ci >= n || cj >= n) continue;
                            double w = Math.Max(0, radius - Math.Sqrt(a * a + b * b)) / total;
                            h[map.Index(ci, cj)] -= (float)(amount * w);
                        }
                        sediment += amount;
                    }
                    speed = Math.Sqrt(Math.Max(0, speed * speed + delta / cell * gravity));
                    water *= 1 - evaporate;
                }
            }
        }

        private static void Gradient(float[] h, int n, double x, double z, out double gx, out double gz, out double height)
        {
            int i = (int)x, j = (int)z;
            double u = x - i, v = z - j;
            double a = h[j * n + i], b = h[j * n + i + 1], c = h[(j + 1) * n + i], d = h[(j + 1) * n + i + 1];
            gx = (b - a) * (1 - v) + (d - c) * v;
            gz = (c - a) * (1 - u) + (d - b) * u;
            height = a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
        }

        // ---- 2. Water ---------------------------------------------------------------------------------------

        private static readonly int[] Di = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] Dj = { 0, 1, 1, 1, 0, -1, -1, -1 };

        private static void Water(WorldMap map)
        {
            int n = map.N;
            WorldSettings s = map.Settings;
            // Fill pits so every cell drains to the map edge (priority flood), then flow downhill (D8).
            var filled = (float[])map.Height.Clone();
            var done = new bool[n * n];
            var queue = new MinHeap();
            for (int i = 0; i < n; i++) { Seed(i, 0); Seed(i, n - 1); }
            for (int j = 1; j < n - 1; j++) { Seed(0, j); Seed(n - 1, j); }
            void Seed(int i, int j) { int k = map.Index(i, j); if (done[k]) return; done[k] = true; queue.Push(filled[k], k); }
            while (queue.Count > 0)
            {
                (float hk, int k) = queue.Pop();
                int ci = k % n, cj = k / n;
                for (int d = 0; d < 8; d++)
                {
                    int ni = ci + Di[d], nj = cj + Dj[d];
                    if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                    int nk = map.Index(ni, nj);
                    if (done[nk]) continue;
                    done[nk] = true;
                    filled[nk] = Math.Max(filled[nk], hk + 0.001f);
                    queue.Push(filled[nk], nk);
                }
            }
            var down = new int[n * n];
            for (int k = 0; k < n * n; k++)
            {
                int ci = k % n, cj = k / n, best = -1;
                double drop = 0;
                for (int d = 0; d < 8; d++)
                {
                    int ni = ci + Di[d], nj = cj + Dj[d];
                    if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                    double dd = (filled[k] - filled[map.Index(ni, nj)]) / (d % 2 == 0 ? 1 : 1.414);
                    if (dd > drop) { drop = dd; best = map.Index(ni, nj); }
                }
                down[k] = best;
            }
            var order = new int[n * n];
            for (int k = 0; k < order.Length; k++) order[k] = k;
            Array.Sort(order, (a, b) => filled[b].CompareTo(filled[a]));
            for (int k = 0; k < n * n; k++) map.Flow[k] = 1;
            foreach (int k in order) if (down[k] >= 0) map.Flow[down[k]] += map.Flow[k];

            // Trace streams: from each cell where flow first crosses the threshold, down to the edge or a bigger stream.
            var taken = new bool[n * n];
            var sources = new List<int>();
            for (int k = 0; k < n * n; k++)
            {
                if (map.Flow[k] < s.StreamCells) continue;
                bool upstreamIsStream = false;
                int ci = k % n, cj = k / n;
                for (int d = 0; d < 8; d++)
                {
                    int ni = ci + Di[d], nj = cj + Dj[d];
                    if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                    int nk = map.Index(ni, nj);
                    if (down[nk] == k && map.Flow[nk] >= s.StreamCells) { upstreamIsStream = true; break; }
                }
                if (!upstreamIsStream) sources.Add(k);
            }
            sources.Sort((a, b) => map.Flow[b].CompareTo(map.Flow[a]));
            var raw = new List<List<int>>();
            foreach (int src in sources)
            {
                var cells = new List<int>();
                int k = src;
                while (k >= 0)
                {
                    cells.Add(k);
                    if (taken[k]) break; // joined a stream already traced
                    taken[k] = true;
                    k = down[k];
                }
                if (cells.Count > 4) raw.Add(cells);
            }

            foreach (List<int> cells in raw)
            {
                var stream = new Waterway();
                double maxFlow = 0;
                for (int c = 0; c < cells.Count; c++)
                {
                    int k = cells[c];
                    maxFlow = Math.Max(maxFlow, map.Flow[k]);
                    double width = Math.Max(1.6, Math.Min(14, 0.9 * Math.Sqrt(map.Flow[k] / s.StreamCells) * 2.2));
                    stream.Points.Add(new PathPoint { P = map.PositionOf(k % n, k / n), Width = width });
                }
                stream.Flow = maxFlow;
                stream.IsRiver = maxFlow >= s.RiverCells;
                Smooth(stream.Points, 2);
                map.Streams.Add(stream);
            }
            Carve(map);
        }

        // Rounds the corners of a grid-traced line so it meanders instead of zig-zagging.
        private static void Smooth(List<PathPoint> pts, int passes)
        {
            for (int p = 0; p < passes; p++)
                for (int i = 1; i + 1 < pts.Count; i++)
                {
                    PathPoint q = pts[i];
                    q.P = (pts[i - 1].P + pts[i].P * 2 + pts[i + 1].P) * 0.25;
                    pts[i] = q;
                }
        }

        // Cuts a channel along every stream (deeper and wider downstream) and sets the water surface.
        private static void Carve(WorldMap map)
        {
            int n = map.N;
            // Every channel is cut from the land as it was before any carving, so overlapping cuts don't dig
            // themselves ever deeper.
            var original = (float[])map.Height.Clone();
            double Original(double x, double z)
            {
                double fx = Math.Max(0, Math.Min(n - 1.001, (x - map.Origin) / map.Cell)), fz = Math.Max(0, Math.Min(n - 1.001, (z - map.Origin) / map.Cell));
                int i = (int)fx, j = (int)fz;
                double tx = fx - i, tz = fz - j;
                return (original[map.Index(i, j)] * (1 - tx) + original[map.Index(i + 1, j)] * tx) * (1 - tz)
                     + (original[map.Index(i, j + 1)] * (1 - tx) + original[map.Index(i + 1, j + 1)] * tx) * tz;
            }
            foreach (Waterway st in map.Streams)
            {
                // The bed: below the original ground, and never rising downstream (water runs downhill).
                var bed = new double[st.Points.Count];
                for (int c = 0; c < bed.Length; c++)
                {
                    PathPoint q = st.Points[c];
                    bed[c] = Original(q.P.X, q.P.Z) - (0.7 + q.Width * 0.15);
                    if (c > 0) bed[c] = Math.Min(bed[c], bed[c - 1]);
                }
                for (int c = 0; c < st.Points.Count; c++)
                {
                    PathPoint q = st.Points[c];
                    double depth = 0.7 + q.Width * 0.15;
                    double bank = q.Width / 2 + 4;
                    int ci = (int)Math.Round((q.P.X - map.Origin) / map.Cell), cj = (int)Math.Round((q.P.Z - map.Origin) / map.Cell);
                    int r = (int)Math.Ceiling(bank / map.Cell) + 1;
                    for (int b = -r; b <= r; b++)
                    for (int a = -r; a <= r; a++)
                    {
                        int i = ci + a, j = cj + b;
                        if (i < 0 || j < 0 || i >= n || j >= n) continue;
                        double d = Vec2.Distance(map.PositionOf(i, j), q.P);
                        if (d > bank) continue;
                        double t = Smooth(q.Width / 2 * 0.6, bank, d); // 0 in the bed, 1 at the top of the bank
                        int k = map.Index(i, j);
                        double target = bed[c] + (original[k] - bed[c]) * t;
                        if (target < map.Height[k]) map.Height[k] = (float)target;
                    }
                    q.Surface = Math.Min(bed[c] + depth * 0.55, c > 0 ? st.Points[c - 1].Surface : double.MaxValue);
                    st.Points[c] = q;
                }
            }
        }

        // ---- 3. Plants ---------------------------------------------------------------------------------------

        private static void ComputeSlope(WorldMap map)
        {
            int n = map.N;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int i0 = Math.Max(0, i - 1), i1 = Math.Min(n - 1, i + 1), j0 = Math.Max(0, j - 1), j1 = Math.Min(n - 1, j + 1);
                double gx = (map.Height[map.Index(i1, j)] - map.Height[map.Index(i0, j)]) / ((i1 - i0) * map.Cell);
                double gz = (map.Height[map.Index(i, j1)] - map.Height[map.Index(i, j0)]) / ((j1 - j0) * map.Cell);
                map.Slope[map.Index(i, j)] = (float)Math.Sqrt(gx * gx + gz * gz);
            }
        }

        private static void Plants(WorldMap map, Noise n)
        {
            WorldSettings s = map.Settings;
            for (int j = 0; j < map.N; j++)
            for (int i = 0; i < map.N; i++)
            {
                int k = map.Index(i, j);
                Vec2 p = map.PositionOf(i, j);
                // Moisture: wetter where water gathers (flow) and in low ground.
                double wet = Math.Min(1, Math.Log(1 + map.Flow[k]) / Math.Log(1 + s.StreamCells));
                double low = 1 - Smooth(0, 40, map.Height[k] - map.HomeHeight);
                map.Moisture[k] = (float)Math.Max(0, Math.Min(1, wet * 0.6 + low * 0.25 + 0.15 * n.Fbm(p.X / 90 + 40, p.Z / 90, 3)));
                // Forest: patches and edges, thinner on steep rock and high up, thick along water; clear at home.
                double patches = 0.5 + 0.5 * n.Fbm(p.X / 140 + 70, p.Z / 140 + 11, 4);
                double steep = Smooth(0.6, 1.2, map.Slope[k]);
                double alpine = Smooth(map.HomeHeight + 160, map.HomeHeight + 230, map.Height[k]);
                double home = Smooth(s.HomeRadius * 1.6, s.HomeRadius * 3.2, p.Length);
                double forest = (patches * 1.25 - 0.3) + map.Moisture[k] * 0.35;
                forest *= (1 - steep * 0.85) * (1 - alpine * 0.9) * home;
                map.Forest[k] = (float)Math.Max(0, Math.Min(1, forest));
            }
        }

        // ---- 4. People ---------------------------------------------------------------------------------------

        private static void Roads(WorldMap map)
        {
            int n = map.N;
            WorldSettings s = map.Settings;
            // The old highway: across the valley from the west edge to the east edge, south of the homestead,
            // taking the easy way (gentle slopes, few water crossings).
            int Cell(double x, double z) => map.Index((int)Math.Round((x - map.Origin) / map.Cell), (int)Math.Round((z - map.Origin) / map.Cell));
            int start = BestEdge(map, -s.Size / 2 + map.Cell, -260, -60);
            int goal = BestEdge(map, s.Size / 2 - map.Cell, -260, -60);
            List<int> cells = AStar(map, start, goal, waterCost: 60, corridorZ: -150);
            map.Road.AddRange(Path(map, cells, 7));
            Smooth(map.Road, 6);
            Grade(map, map.Road, 4.5);
            // Bridges: where the road runs over a stream.
            Bridges(map);
            Ramps(map);
            // A track from the homestead down to the road.
            double best = double.MaxValue; Vec2 join = default;
            foreach (PathPoint q in map.Road) { double d = q.P.Length; if (d < best) { best = d; join = q.P; } }
            List<int> track = AStar(map, Cell(0, -s.HomeRadius * 0.6), Cell(join.X, join.Z), waterCost: 25);
            map.Track.AddRange(Path(map, track, 3));
            Smooth(map.Track, 6);
            Grade(map, map.Track, 2);
        }

        // The flattest, driest crossing point on a vertical map edge, between two z values.
        private static int BestEdge(WorldMap map, double x, double z0, double z1)
        {
            int best = -1; double score = double.MaxValue;
            for (double z = z0; z <= z1; z += map.Cell)
            {
                double sc = map.SlopeAt(x, z) * 100 + map.HeightAt(x, z) * 0.1 + (map.DistanceToWater(x, z, out _) < 6 ? 1000 : 0);
                if (sc < score) { score = sc; best = map.Index((int)Math.Round((x - map.Origin) / map.Cell), (int)Math.Round((z - map.Origin) / map.Cell)); }
            }
            return best;
        }

        private static List<int> AStar(WorldMap map, int start, int goal, double waterCost, double? corridorZ = null)
        {
            int n = map.N;
            var water = new bool[n * n];
            foreach (Waterway st in map.Streams)
                foreach (PathPoint q in st.Points)
                {
                    int ci = (int)Math.Round((q.P.X - map.Origin) / map.Cell), cj = (int)Math.Round((q.P.Z - map.Origin) / map.Cell);
                    int r = (int)Math.Ceiling(q.Width / 2 / map.Cell);
                    for (int b = -r; b <= r; b++) for (int a = -r; a <= r; a++)
                    {
                        int i = ci + a, j = cj + b;
                        if (i >= 0 && j >= 0 && i < n && j < n) water[map.Index(i, j)] = true;
                    }
                }
            var cost = new double[n * n];
            for (int k = 0; k < cost.Length; k++) cost[k] = double.MaxValue;
            var from = new int[n * n];
            var open = new MinHeap();
            cost[start] = 0;
            open.Push(0, start);
            int gi = goal % n, gj = goal / n;
            while (open.Count > 0)
            {
                (float _, int k) = open.Pop();
                if (k == goal) break;
                int ci = k % n, cj = k / n;
                for (int d = 0; d < 8; d++)
                {
                    int ni = ci + Di[d], nj = cj + Dj[d];
                    if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                    int nk = map.Index(ni, nj);
                    double run = map.Cell * (d % 2 == 0 ? 1 : 1.414);
                    double grade = Math.Abs(map.Height[nk] - map.Height[k]) / run;
                    double step = run * (1 + 120 * grade * grade + (grade > 0.18 ? 40 : 0)) + (water[nk] ? waterCost * run * (water[k] ? 0.5 : 1) : 0)
                        + (corridorZ.HasValue ? run * Math.Pow((nj * map.Cell + map.Origin - corridorZ.Value) / 50, 2) : 0);
                    double c = cost[k] + step;
                    if (c >= cost[nk]) continue;
                    cost[nk] = c;
                    from[nk] = k;
                    double hx = (ni - gi) * map.Cell, hz = (nj - gj) * map.Cell;
                    open.Push((float)(c + Math.Sqrt(hx * hx + hz * hz)), nk);
                }
            }
            var path = new List<int>();
            for (int k = goal; ; k = from[k]) { path.Add(k); if (k == start || path.Count > n * n) break; }
            path.Reverse();
            return path;
        }

        private static List<PathPoint> Path(WorldMap map, List<int> cells, double width)
        {
            var pts = new List<PathPoint>();
            for (int c = 0; c < cells.Count; c += 2)
                pts.Add(new PathPoint { P = map.PositionOf(cells[c] % map.N, cells[c] / map.N), Width = width });
            int last = cells[cells.Count - 1];
            pts.Add(new PathPoint { P = map.PositionOf(last % map.N, last / map.N), Width = width });
            return pts;
        }

        // Levels the ground across a road and evens out its grade along it, blending into the land beside it.
        private static void Grade(WorldMap map, List<PathPoint> path, double halfWidth)
        {
            var target = new double[path.Count];
            for (int c = 0; c < path.Count; c++) target[c] = map.HeightAt(path[c].P.X, path[c].P.Z);
            for (int pass = 0; pass < 6; pass++)
                for (int c = 1; c + 1 < path.Count; c++) target[c] = (target[c - 1] + target[c] * 2 + target[c + 1]) / 4;
            int n = map.N;
            for (int c = 0; c < path.Count; c++)
            {
                PathPoint q = path[c];
                int ci = (int)Math.Round((q.P.X - map.Origin) / map.Cell), cj = (int)Math.Round((q.P.Z - map.Origin) / map.Cell);
                int r = (int)Math.Ceiling((halfWidth + 4) / map.Cell);
                for (int b = -r; b <= r; b++)
                for (int a = -r; a <= r; a++)
                {
                    int i = ci + a, j = cj + b;
                    if (i < 0 || j < 0 || i >= n || j >= n) continue;
                    double d = Vec2.Distance(map.PositionOf(i, j), q.P);
                    double w = 1 - Smooth(halfWidth, halfWidth + 4, d);
                    if (w <= 0) continue;
                    int k = map.Index(i, j);
                    // Don't fill stream channels: bridges cross those.
                    if (map.DistanceToWater(map.PositionOf(i, j).X, map.PositionOf(i, j).Z, out _) < 1.5) continue;
                    map.Height[k] = (float)(map.Height[k] * (1 - w) + target[c] * w);
                }
            }
        }

        // Earth ramps raise the road to each bridge's deck, falling away 1 in 8 and to the sides.
        private static void Ramps(WorldMap map)
        {
            int n = map.N;
            foreach (Bridge br in map.Bridges)
            {
                Vec2 dir = br.To - br.From;
                dir = dir * (1 / Math.Max(1e-6, dir.Length));
                foreach ((Vec2 end, double sign) in new[] { (br.From, -1.0), (br.To, 1.0) })
                {
                    double rise = br.DeckHeight - map.HeightAt(end.X, end.Z);
                    if (rise <= 0) continue;
                    double length = rise * 8 + 2;
                    int ci = (int)Math.Round((end.X - map.Origin) / map.Cell), cj = (int)Math.Round((end.Z - map.Origin) / map.Cell);
                    int r = (int)Math.Ceiling((length + 8) / map.Cell);
                    for (int b = -r; b <= r; b++)
                    for (int a = -r; a <= r; a++)
                    {
                        int i = ci + a, j = cj + b;
                        if (i < 0 || j < 0 || i >= n || j >= n) continue;
                        Vec2 p = map.PositionOf(i, j), off = p - end;
                        double along = (off.X * dir.X + off.Z * dir.Z) * sign; // metres away from the bridge
                        double across = Math.Abs(off.X * -dir.Z + off.Z * dir.X);
                        if (along < -1 || along > length || across > 8) continue;
                        if (map.DistanceToWater(p.X, p.Z, out _) < 1.5) continue;
                        double top = br.DeckHeight - Math.Max(0, along) / 8 - Math.Max(0, across - 4) * 0.5;
                        int k = map.Index(i, j);
                        if (top > map.Height[k]) map.Height[k] = (float)top;
                    }
                }
            }
        }

        private static void Bridges(WorldMap map)
        {
            // A bridge runs straight from the last dry point before the water to the first dry one after it;
            // the road between is laid onto that line.
            bool overWater = false; int begin = 0; double deck = 0;
            for (int c = 0; c < map.Road.Count; c++)
            {
                PathPoint q = map.Road[c];
                bool wet = map.DistanceToWater(q.P.X, q.P.Z, out _) < 1.0;
                if (wet && !overWater)
                {
                    overWater = true;
                    begin = Math.Max(0, c - 1);
                    deck = map.HeightAt(map.Road[begin].P.X, map.Road[begin].P.Z);
                }
                else if (!wet && overWater)
                {
                    overWater = false;
                    Vec2 from = map.Road[begin].P, to = q.P;
                    deck = Math.Max(deck, map.HeightAt(to.X, to.Z));
                    for (int k = begin + 1; k < c; k++)
                    {
                        PathPoint r = map.Road[k];
                        r.P = from + (to - from) * ((double)(k - begin) / (c - begin));
                        map.Road[k] = r;
                    }
                    // Clear the water by a metre at least.
                    double water = double.MinValue;
                    for (int k = begin; k <= c; k++)
                        if (map.DistanceToWater(map.Road[k].P.X, map.Road[k].P.Z, out PathPoint w) < 3) water = Math.Max(water, w.Surface);
                    map.Bridges.Add(new Bridge { From = from, To = to, DeckHeight = Math.Max(deck + 0.2, water + 1.0) });
                }
            }
        }

        // Ruins where people would have built: flat ground by the old road or near the river, away from home.
        private static void Ruins(WorldMap map, SimRandom rng)
        {
            var candidates = new List<(double score, Vec2 p)>();
            for (int tries = 0; tries < 4000; tries++)
            {
                double x = (rng.NextDouble() - 0.5) * map.Settings.Size * 0.6, z = (rng.NextDouble() - 0.5) * map.Settings.Size * 0.6;
                var p = new Vec2(x, z);
                if (p.Length < 110) continue;
                double slope = map.SlopeAt(x, z);
                if (slope > 0.12) continue;
                double road = WorldMap.DistanceToPath(map.Road, x, z);
                double water = map.DistanceToWater(x, z, out _);
                if (road < 10 || water < 12) continue;
                double score = Math.Min(road, 60) * 0.5 + Math.Min(Math.Abs(water - 30), 60) * 0.3 + slope * 100 + rng.NextDouble() * 10;
                candidates.Add((score, p));
            }
            candidates.Sort((a, b) => a.score.CompareTo(b.score));
            foreach (var c in candidates)
            {
                if (map.RuinSites.Exists(r => Vec2.Distance(r, c.p) < 120)) continue;
                map.RuinSites.Add(c.p);
                if (map.RuinSites.Count >= 5) break;
            }
        }

        // The Elder Works: older than the old world, so not where people built. A bench high on the mountain
        // front inside the playable square, as far from home as it gets, off the road and out of the gullies.
        private static void ElderWorks(WorldMap map)
        {
            const double reach = 270;
            double best = double.MaxValue;
            Vec2 at = new Vec2(0, reach);
            for (double z = 120; z <= reach; z += 5)
            for (double x = -reach; x <= reach; x += 5)
            {
                double h = map.HeightAt(x, z) - map.HomeHeight;
                if (h < 20) continue;
                double rough = 0;
                for (int a = 0; a < 8; a++)
                {
                    double t = a * Math.PI / 4;
                    rough = Math.Max(rough, Math.Abs(map.HeightAt(x + Math.Cos(t) * 12, z + Math.Sin(t) * 12) - map.HeightAt(x, z)));
                }
                if (rough > 6) continue;
                if (map.DistanceToWater(x, z, out _) < 25 || WorldMap.DistanceToPath(map.Road, x, z) < 50) continue;
                if (map.RuinSites.Exists(r => Vec2.Distance(r, new Vec2(x, z)) < 90)) continue;
                double score = rough * 4 - Math.Sqrt(x * x + z * z) * 0.12 - Math.Min(h, 60) * 0.2;
                if (score < best) { best = score; at = new Vec2(x, z); }
            }
            map.ElderWorks = at;
            // Level a pad for the mound and its ring of stones, blending into the slope.
            double target = map.HeightAt(at.X, at.Z);
            double r0 = WorldMap.ElderWorksRadius, r1 = WorldMap.ElderWorksRadius * 2.2;
            for (int j = 0; j < map.N; j++)
            for (int i = 0; i < map.N; i++)
            {
                double d = Vec2.Distance(map.PositionOf(i, j), at);
                if (d >= r1) continue;
                double w = 1 - Smooth(r0, r1, d);
                int k = map.Index(i, j);
                map.Height[k] = (float)(map.Height[k] * (1 - w) + target * w);
            }
            map.ElderWorksHeight = target;
        }

        // What's around each ruin site, so content siting can choose a kind for it.
        private static void DescribeSites(WorldMap map)
        {
            map.Sites.Clear();
            foreach (Vec2 p in map.RuinSites)
            {
                double around = 0;
                for (int a = 0; a < 8; a++)
                {
                    double t = a * Math.PI / 4;
                    around += map.HeightAt(p.X + Math.Cos(t) * 40, p.Z + Math.Sin(t) * 40);
                }
                Vec2 toRoad = new Vec2(0, 1);
                double nearest = double.MaxValue;
                foreach (PathPoint q in map.Road)
                {
                    double d = Vec2.Distance(p, q.P);
                    if (d < nearest) { nearest = d; toRoad = q.P - p; }
                }
                map.Sites.Add(new RuinSite
                {
                    P = p,
                    Road = WorldMap.DistanceToPath(map.Road, p.X, p.Z),
                    Water = map.DistanceToWater(p.X, p.Z, out _),
                    Home = p.Length,
                    Slope = map.SlopeAt(p.X, p.Z),
                    Rise = map.HeightAt(p.X, p.Z) - around / 8,
                    Facing = Math.Atan2(toRoad.X, toRoad.Z) * 180 / Math.PI,
                });
            }
        }

        // ---- Helpers ---------------------------------------------------------------------------------------

        /// <summary>Seeded gradient noise, -1..1.</summary>
        private sealed class Noise
        {
            private readonly int[] _perm = new int[512];
            private readonly double[] _gx = new double[256], _gz = new double[256];

            public Noise(SimRandom rng)
            {
                var p = new int[256];
                for (int i = 0; i < 256; i++) p[i] = i;
                for (int i = 255; i > 0; i--) { int j = rng.NextInt(0, i + 1); (p[i], p[j]) = (p[j], p[i]); }
                for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
                for (int i = 0; i < 256; i++) { double a = rng.NextDouble() * Math.PI * 2; _gx[i] = Math.Cos(a); _gz[i] = Math.Sin(a); }
            }

            public double Value(double x, double z)
            {
                int xi = (int)Math.Floor(x), zi = (int)Math.Floor(z);
                double xf = x - xi, zf = z - zi;
                double Dot(int ix, int iz, double fx, double fz) { int g = _perm[(_perm[ix & 255] + iz) & 511] & 255; return _gx[g] * fx + _gz[g] * fz; }
                double u = xf * xf * xf * (xf * (xf * 6 - 15) + 10), v = zf * zf * zf * (zf * (zf * 6 - 15) + 10);
                double a = Dot(xi, zi, xf, zf), b = Dot(xi + 1, zi, xf - 1, zf), c = Dot(xi, zi + 1, xf, zf - 1), d = Dot(xi + 1, zi + 1, xf - 1, zf - 1);
                return ((a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v) * 1.4;
            }

            public double Fbm(double x, double z, int octaves)
            {
                double sum = 0, amp = 0.5, norm = 0;
                for (int o = 0; o < octaves; o++) { sum += Value(x, z) * amp; norm += amp; amp *= 0.5; x = x * 2.03 + 17; z = z * 2.03 + 5; }
                return sum / norm;
            }
        }

        /// <summary>A binary min-heap of (key, index).</summary>
        private sealed class MinHeap
        {
            private readonly List<(float key, int value)> _items = new List<(float, int)>();
            public int Count => _items.Count;

            public void Push(float key, int value)
            {
                _items.Add((key, value));
                int c = _items.Count - 1;
                while (c > 0) { int p = (c - 1) / 2; if (_items[p].key <= _items[c].key) break; (_items[p], _items[c]) = (_items[c], _items[p]); c = p; }
            }

            public (float, int) Pop()
            {
                var top = _items[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int c = 0;
                while (true)
                {
                    int l = c * 2 + 1, r = l + 1, m = c;
                    if (l < _items.Count && _items[l].key < _items[m].key) m = l;
                    if (r < _items.Count && _items[r].key < _items[m].key) m = r;
                    if (m == c) break;
                    (_items[m], _items[c]) = (_items[c], _items[m]);
                    c = m;
                }
                return top;
            }
        }
    }
}
