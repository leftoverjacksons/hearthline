using System;
using System.Diagnostics;
using System.IO;
using Hearthline.Sim.World;

// A top-down map of the generated world: hillshade, forest, water, road, bridges, ruins, and the playable square.
ulong seed = args.Length > 0 ? ulong.Parse(args[0]) : 1;
string path = args.Length > 1 ? args[1] : "world.bmp";
var watch = Stopwatch.StartNew();
WorldMap map = WorldGen.Generate(new WorldSettings { Seed = seed });
Console.WriteLine($"generated in {watch.ElapsedMilliseconds} ms: {map.Streams.Count} streams ({map.Streams.FindAll(s => s.IsRiver).Count} river), road {map.Road.Count} pts, {map.Bridges.Count} bridges, {map.RuinSites.Count} ruins, water from home {map.DistanceToWater(0, 0, out _):F0} m, home h {map.HomeHeight:F1}");

int n = map.N;
var px = new byte[n, n, 3];
float lo = float.MaxValue, hi = float.MinValue;
foreach (float h in map.Height) { lo = Math.Min(lo, h); hi = Math.Max(hi, h); }
for (int j = 0; j < n; j++)
for (int i = 0; i < n; i++)
{
    int k = map.Index(i, j);
    int i1 = Math.Min(n - 1, i + 1), j1 = Math.Min(n - 1, j + 1);
    double gx = (map.Height[map.Index(i1, j)] - map.Height[k]) / map.Cell, gz = (map.Height[map.Index(i, j1)] - map.Height[k]) / map.Cell;
    double shade = Math.Clamp(0.75 - gx * 0.9 + gz * 0.9, 0.25, 1.15);
    double t = (map.Height[k] - lo) / Math.Max(1, hi - lo);
    double f = map.Forest[k];
    double r = 150 + 70 * t, g = 160 + 40 * t, b = 110 + 90 * t; // meadow -> rock
    r = r * (1 - f * 0.7) + 40 * f * 0.7; g = g * (1 - f * 0.7) + 90 * f * 0.7; b = b * (1 - f * 0.7) + 45 * f * 0.7;
    px[i, j, 0] = (byte)Math.Clamp(r * shade, 0, 255); px[i, j, 1] = (byte)Math.Clamp(g * shade, 0, 255); px[i, j, 2] = (byte)Math.Clamp(b * shade, 0, 255);
}
void Dot(double x, double z, double radius, byte r, byte g, byte b)
{
    int ci = (int)((x - map.Origin) / map.Cell), cj = (int)((z - map.Origin) / map.Cell), rr = (int)Math.Ceiling(radius / map.Cell);
    for (int dj = -rr; dj <= rr; dj++) for (int di = -rr; di <= rr; di++)
    {
        int i = ci + di, j = cj + dj;
        if (i < 0 || j < 0 || i >= n || j >= n || (di * di + dj * dj) * map.Cell * map.Cell > radius * radius) continue;
        px[i, j, 0] = r; px[i, j, 1] = g; px[i, j, 2] = b;
    }
}
foreach (Waterway s in map.Streams) foreach (PathPoint q in s.Points) Dot(q.P.X, q.P.Z, q.Width / 2 + 0.5, 50, 110, 190);
foreach (PathPoint q in map.Road) Dot(q.P.X, q.P.Z, 3, 120, 110, 100);
foreach (PathPoint q in map.Track) Dot(q.P.X, q.P.Z, 1.5, 150, 120, 80);
foreach (Bridge b in map.Bridges) for (double t = 0; t <= 1; t += 0.05) Dot(b.From.X + (b.To.X - b.From.X) * t, b.From.Z + (b.To.Z - b.From.Z) * t, 3.5, 230, 60, 40);
foreach (Vec2 r in map.RuinSites) Dot(r.X, r.Z, 9, 160, 40, 160);
Dot(0, 0, 10, 250, 230, 60);
for (double a = -300; a <= 300; a += 2) { Dot(a, -300, 1, 255, 255, 255); Dot(a, 300, 1, 255, 255, 255); Dot(-300, a, 1, 255, 255, 255); Dot(300, a, 1, 255, 255, 255); }

// 24-bit BMP, north (+z) up.
int row = (n * 3 + 3) & ~3;
using var bw = new BinaryWriter(File.Create(path));
bw.Write((byte)'B'); bw.Write((byte)'M'); bw.Write(54 + row * n); bw.Write(0); bw.Write(54);
bw.Write(40); bw.Write(n); bw.Write(n); bw.Write((short)1); bw.Write((short)24); bw.Write(0); bw.Write(row * n); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
for (int j = 0; j < n; j++)
{
    for (int i = 0; i < n; i++) { bw.Write(px[i, j, 2]); bw.Write(px[i, j, 1]); bw.Write(px[i, j, 0]); }
    for (int p = n * 3; p < row; p++) bw.Write((byte)0);
}
Console.WriteLine("wrote " + Path.GetFullPath(path));
