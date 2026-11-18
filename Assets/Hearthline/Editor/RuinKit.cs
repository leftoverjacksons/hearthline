using System;
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim.Content;
using Hearthline.Sim.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Greybox ruins by kind of place (docs/specs/ruins-and-places.md). Each is built in its own frame: the
    /// origin at the site, +z facing the road (the Elder Works faces home). Walls reach below the ground so
    /// they sit on slopes, and their box colliders become cover. Each place gets its search spots and its find
    /// (<see cref="PlaceSpot"/>); which spots exist is seeded per place, so a rebuild keeps them.
    /// </summary>
    public static class RuinKit
    {
        /// <summary>Where a search spot can be, in the place's frame: x, z, height above the ground, and what it's called.</summary>
        private readonly struct Spot
        {
            public readonly float X, Z, Y;
            public readonly string What, Model;
            public Spot(float x, float z, string what, string model = "Salvage_Pile", float y = 0f) { X = x; Z = z; What = what; Model = model; Y = y; }
        }

        private sealed class Frame
        {
            public Transform Root;
            public Func<float, float, float> Ground;
            public Random Rnd;
            public float R(double a, double b) => (float)(a + Rnd.NextDouble() * (b - a));
            public Vector3 World(float x, float z) => Root.TransformPoint(new Vector3(x, 0f, z));
            public float GroundAt(float x, float z) { Vector3 w = World(x, z); return Ground(w.x, w.z); }
        }

        /// <summary>The area box (width, depth) for a kind, in its frame. Also how far trees keep away.</summary>
        public static Vector2 Footprint(string kind)
        {
            switch (kind)
            {
                case "farmstead": return new Vector2(26f, 42f);
                case "chapel": return new Vector2(22f, 30f);
                case "filling_station": return new Vector2(24f, 26f);
                case "schoolhouse": return new Vector2(18f, 20f);
                case "feed_store": return new Vector2(22f, 22f);
                case "elder_works": return new Vector2(38f, 38f);
                default: return new Vector2(30f, 30f);
            }
        }

        public static Transform Build(PlacedPlace place, PlaceDef def, Transform parent, Func<float, float, float> ground)
        {
            var root = new GameObject("Place " + place.Id).transform;
            root.SetParent(parent, false);
            float x = (float)place.P.X, z = (float)place.P.Z;
            root.SetPositionAndRotation(new Vector3(x, ground(x, z), z), Quaternion.Euler(0f, (float)place.Facing, 0f));
            var f = new Frame { Root = root, Ground = ground, Rnd = new Random(StableHash(place.Id)) };
            List<Spot> spots;
            Spot? find;
            switch (place.Kind)
            {
                case "farmstead": Farmstead(f, out spots, out find); break;
                case "chapel": Chapel(f, out spots, out find); break;
                case "filling_station": Station(f, out spots, out find); break;
                case "schoolhouse": School(f, out spots, out find); break;
                case "feed_store": FeedStore(f, out spots, out find); break;
                case "elder_works": ElderWorks(f, out spots, out find); break;
                default: Rubble(f, out spots, out find); break;
            }
            Overgrow(f, place.Kind == "elder_works" ? 0 : 5);
            AddSpots(f, place, def, spots, def.Find != null ? find : null);
            return root;
        }

        /// <summary>Search spots for a place whose structure is built elsewhere (the overpass).</summary>
        public static void AddSpotsAt(PlacedPlace place, PlaceDef def, Transform parent, Func<float, float, float> ground, IList<Vector3> worldPoints, string what)
        {
            var root = new GameObject("Place " + place.Id).transform;
            root.SetParent(parent, false);
            var f = new Frame { Root = root, Ground = ground, Rnd = new Random(StableHash(place.Id)) };
            var spots = new List<Spot>();
            foreach (Vector3 p in worldPoints) spots.Add(new Spot(p.x, p.z, what));
            AddSpots(f, place, def, spots, null);
        }

        // ---- Pieces ---------------------------------------------------------------------------------

        private const float Sink = 0.8f;

        /// <summary>A box standing on the ground at (x, z) in the frame, reaching below it.</summary>
        private static GameObject Stand(Frame f, string name, float x, float z, Vector3 size, float yaw, string mat, bool collider = true)
        {
            float g = f.GroundAt(x, z) - f.Root.position.y;
            var local = new Vector3(x, g + (size.y - Sink) / 2f, z);
            GameObject go = Piece(f, name, mat, collider, out bool turned);
            go.transform.localPosition = local;
            Vector3 scale = new Vector3(size.x, size.y + Sink, size.z);
            // Wall runs are modelled lying along x; RuinKit's walls run along z.
            go.transform.localRotation = Quaternion.Euler(0f, yaw + (turned ? 90f : 0f), 0f);
            go.transform.localScale = turned ? new Vector3(scale.z, scale.y, scale.x) : scale;
            return go;
        }

        /// <summary>A box at a height above the ground at (x, z), turned freely: roofs, beams, fallen things.</summary>
        private static GameObject Lay(Frame f, string name, float x, float y, float z, Vector3 size, Vector3 euler, string mat, bool collider = true)
        {
            float g = f.GroundAt(x, z) - f.Root.position.y;
            GameObject go = Piece(f, name, mat, collider, out bool turned);
            go.transform.localPosition = new Vector3(x, g + y, z);
            go.transform.localRotation = Quaternion.Euler(euler) * Quaternion.Euler(0f, turned ? 90f : 0f, 0f);
            go.transform.localScale = turned ? new Vector3(size.z, size.y, size.x) : size;
            return go;
        }

        /// <summary>
        /// The modelled piece for a part (ADR 0029): a unit-sized kit model chosen by what the part is and what
        /// it's made of, so ruins keep their layout but lose the primitive boxes. Falls back to a box for anything
        /// the kit doesn't cover. <paramref name="turned"/>: the piece is modelled along x and must turn 90°.
        /// </summary>
        private static GameObject Piece(Frame f, string name, string mat, bool collider, out bool turned)
        {
            turned = false;
            string lower = name.ToLowerInvariant();
            string material = mat.StartsWith("HL_") ? mat.Substring(3) : mat;
            string model = null;
            if (lower == "wall")
            {
                turned = true;
                model = mat == "HL_Stone" ? "Unit_Wall_Stone" : mat == "HL_WoodWeathered" ? "Unit_Wall_PlanksWeathered"
                    : mat == "HL_WoodDark" ? "Unit_Wall_PlanksDark" : mat == "HL_Concrete" ? "Unit_Wall_Concrete" : null;
            }
            else if (lower.Contains("roof") || lower.Contains("hayloft") || lower.Contains("ramp") || lower.Contains("rafter") || lower.Contains("pew") || lower.Contains("desk") || lower.Contains("steeple") || lower.Contains("dock"))
                model = mat == "HL_TinRust" ? "Unit_Tin" : mat == "HL_WoodWeathered" ? "Unit_Boards_Weathered" : mat == "HL_WoodDark" ? "Unit_Boards_Dark" : null;
            else if (lower == "standing stone" || lower == "wrong shadow")
                model = "Unit_StandingStone";
            if (model == null) model = "Unit_Block_" + material;
            GameObject go;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GreyboxKit.Folder + "/" + model + ".fbx") != null)
            {
                go = GreyboxKit.Place(model, Vector3.zero, 0f, 1f, f.Root, collider);
                go.name = name;
            }
            else
            {
                turned = false;
                go = SceneBuildKit.Block(PrimitiveType.Cube, name, Vector3.zero, Vector3.one, GreyboxKit.Mat(mat), f.Root, collider);
            }
            return go;
        }

        /// <summary>
        /// A ruined wall from a to b in the frame, broken into segments of uneven height, with gaps
        /// (door and window openings, as distances along the wall: start, end).
        /// </summary>
        private static void Wall(Frame f, Vector2 a, Vector2 b, float height, float thick, string mat, float ruin, params (float, float)[] gaps)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            var cuts = new List<(float, float)>(gaps);
            cuts.Sort((p, q) => p.Item1.CompareTo(q.Item1));
            float at = 0f;
            void Piece(float from, float to)
            {
                // Split long runs so the broken top line varies.
                for (float s = from; s < to - 0.05f;)
                {
                    float e = Mathf.Min(to, s + f.R(1.6, 3.2));
                    if (to - e < 0.6f) e = to;
                    float h = height * (1f - ruin * f.R(0, 1));
                    Vector2 mid = a + d / len * ((s + e) / 2f);
                    Stand(f, "Wall", mid.x, mid.y, new Vector3(thick, Mathf.Max(0.5f, h), e - s), yaw, mat);
                    s = e;
                }
            }
            foreach ((float gs, float ge) in cuts)
            {
                if (gs > at) Piece(at, gs);
                at = Mathf.Max(at, ge);
            }
            if (at < len) Piece(at, len);
        }

        private static void Rect4(Frame f, float x0, float z0, float x1, float z1, float h, float thick, string mat, float ruin,
            (float, float)[] front = null, (float, float)[] back = null, (float, float)[] left = null, (float, float)[] right = null)
        {
            Wall(f, new Vector2(x0, z1), new Vector2(x1, z1), h, thick, mat, ruin, front ?? new (float, float)[0]);
            Wall(f, new Vector2(x0, z0), new Vector2(x1, z0), h, thick, mat, ruin, back ?? new (float, float)[0]);
            Wall(f, new Vector2(x0, z0), new Vector2(x0, z1), h, thick, mat, ruin, left ?? new (float, float)[0]);
            Wall(f, new Vector2(x1, z0), new Vector2(x1, z1), h, thick, mat, ruin, right ?? new (float, float)[0]);
        }

        private static void Overgrow(Frame f, int mounds)
        {
            for (int i = 0; i < mounds; i++)
            {
                float a = f.R(0, Math.PI * 2), r = f.R(5, 12);
                Vector3 w = f.World(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                GreyboxKit.Place("Kudzu_Mound", new Vector3(w.x, f.Ground(w.x, w.z), w.z), f.R(0, 360), f.R(0.7, 1.2), f.Root, collider: false);
            }
        }

        // ---- Kinds ----------------------------------------------------------------------------------

        // The burned farmstead: a stone foundation, charred walls and a chimney; a half-standing barn behind
        // with a hayloft up a ramp, where the find is.
        private static void Farmstead(Frame f, out List<Spot> spots, out Spot? find)
        {
            Rect4(f, -5f, -4f, 5f, 4f, 0.7f, 0.5f, "HL_Stone", 0.2f, front: new[] { (4f, 5.4f) });
            Wall(f, new Vector2(-5f, -4f), new Vector2(5f, -4f), 2.8f, 0.3f, "HL_WoodDark", 0.6f, (3.5f, 4.6f));
            Wall(f, new Vector2(-5f, -4f), new Vector2(-5f, 2.5f), 2.3f, 0.3f, "HL_WoodDark", 0.7f);
            Stand(f, "Chimney", 4.2f, -3.4f, new Vector3(1.2f, 6f, 1.2f), 0f, "HL_Stone");
            Lay(f, "Fallen roof", 0f, 1f, 0.3f, new Vector3(8.5f, 0.25f, 4.6f), new Vector3(0f, 8f, 17f), "HL_WoodDark");
            // The barn.
            Rect4(f, -6f, -19f, 6f, -11f, 4.6f, 0.3f, "HL_WoodWeathered", 0.35f, front: new[] { (7f, 11.2f) });
            Lay(f, "Hayloft", 0f, 2.6f, -16.5f, new Vector3(11.4f, 0.25f, 4.6f), Vector3.zero, "HL_WoodWeathered");
            Lay(f, "Hayloft ramp", 3.4f, 1.3f, -11.5f, new Vector3(1.6f, 0.18f, 6.6f), new Vector3(23.5f, 0f, 0f), "HL_WoodWeathered");
            Lay(f, "Barn roof", -2.5f, 4.4f, -15f, new Vector3(7.5f, 0.15f, 8.4f), new Vector3(0f, 0f, -14f), "HL_TinRust", collider: false);
            Stand(f, "Well", 7f, 5f, new Vector3(1.4f, 0.9f, 1.4f), 15f, "HL_Stone");
            spots = new List<Spot>
            {
                new Spot(2f, -1.5f, "the cold hearth", "Rubble"),
                new Spot(-3f, 1f, "a charred trunk", "Stock_Crate"),
                new Spot(0.5f, 5.2f, "the porch boards"),
                new Spot(-4f, -13f, "the barn stalls", "Stock_Sack"),
                new Spot(7.5f, 2.5f, "the well head", "Stock_Scrap"),
            };
            find = new Spot(-3.5f, -17.4f, "the hayloft", "Stock_Crate", 2.73f);
        }

        // The fallen chapel: stone nave with window gaps, roof gone but for rafters, the steeple lying in the
        // grass, pews, and the altar step at the back, where the find is.
        private static void Chapel(Frame f, out List<Spot> spots, out Spot? find)
        {
            var windows = new[] { (2.5f, 3.6f), (6.5f, 7.6f), (10.5f, 11.6f) };
            Rect4(f, -4f, -10f, 4f, 6f, 4.4f, 0.6f, "HL_Stone", 0.4f, front: new[] { (3.1f, 4.9f) }, left: windows, right: windows);
            for (int i = 0; i < 3; i++) Lay(f, "Rafter", 0f, 4.1f, -6f + i * 4.5f, new Vector3(8.4f, 0.25f, 0.25f), new Vector3(0f, 0f, f.R(-6, 6)), "HL_WoodDark", collider: false);
            Stand(f, "Tower stump", 0f, 8f, new Vector3(3f, 3f, 3f), 0f, "HL_Stone");
            Lay(f, "Fallen steeple", 4.5f, 0.9f, 11f, new Vector3(1.8f, 1.8f, 11f), new Vector3(4f, 58f, 0f), "HL_WoodWeathered");
            for (int row = 0; row < 5; row++)
                foreach (float side in new[] { -2f, 2f })
                    if (f.Rnd.NextDouble() < 0.75) Lay(f, "Pew", side, 0.25f, 3f - row * 2.1f, new Vector3(2.8f, 0.5f, 0.5f), new Vector3(0f, f.R(-12, 12), 0f), "HL_WoodWeathered");
            Lay(f, "Altar step", 0f, 0.2f, -8.6f, new Vector3(4f, 0.4f, 1.8f), Vector3.zero, "HL_Stone");
            spots = new List<Spot>
            {
                new Spot(-2.8f, -2f, "the pews", "Rubble"),
                new Spot(3f, -8.3f, "the vestry corner", "Stock_Crate"),
                new Spot(1.8f, 8.6f, "the bell tower rubble", "Rubble"),
                new Spot(2.6f, 1f, "a collapsed pew"),
                new Spot(-3f, 4.4f, "the font", "Stock_Scrap"),
            };
            find = new Spot(0f, -8.2f, "the altar step", "Stock_Crate", 0.4f);
        }

        // The filling station: a canopy on posts over two pumps, the shop with its back office (the safe is
        // the find), and a wreck at the edge of the forecourt.
        private static void Station(Frame f, out List<Spot> spots, out Spot? find)
        {
            Lay(f, "Canopy", 0f, 4.6f, 7f, new Vector3(11f, 0.35f, 7f), new Vector3(0f, 0f, 3f), "HL_Concrete", collider: false);
            foreach (float px in new[] { -4.8f, 4.8f }) foreach (float pz in new[] { 4.2f, 9.8f }) Stand(f, "Canopy post", px, pz, new Vector3(0.35f, 4.6f, 0.35f), 0f, "HL_Steel");
            foreach (float px in new[] { -1.8f, 1.8f }) Stand(f, "Pump", px, 7f, new Vector3(0.7f, 1.7f, 0.5f), 0f, "HL_TinRust");
            Lay(f, "Forecourt", 0f, -0.05f, 6f, new Vector3(14f, 0.2f, 12f), Vector3.zero, "HL_Asphalt", collider: false);
            Rect4(f, -5f, -5f, 5f, 1f, 3.3f, 0.35f, "HL_Concrete", 0.25f, front: new[] { (1f, 2.6f), (4.2f, 5.6f), (7f, 8.6f) }, left: new[] { (2f, 3.2f) });
            Wall(f, new Vector2(-5f, -2f), new Vector2(5f, -2f), 3.1f, 0.25f, "HL_Concrete", 0.1f, (6.8f, 7.9f));
            Lay(f, "Shop roof", -2.5f, 3.3f, -0.5f, new Vector3(5f, 0.3f, 3f), Vector3.zero, "HL_Concrete", collider: false);
            Lay(f, "Fallen roof", 2f, 1.6f, -3.5f, new Vector3(5f, 0.3f, 3.2f), new Vector3(0f, 0f, 32f), "HL_Concrete");
            Lay(f, "Counter", -2f, 0.5f, -0.6f, new Vector3(3f, 1f, 0.6f), Vector3.zero, "HL_WoodDark");
            Lay(f, "Wreck", -7.5f, 0.6f, 9f, new Vector3(1.9f, 1.2f, 4.2f), new Vector3(0f, 25f, 4f), "HL_TinRust");
            Lay(f, "Wreck cab", -7.6f, 1.45f, 8.8f, new Vector3(1.7f, 0.7f, 2f), new Vector3(0f, 25f, 4f), "HL_TinRust", collider: false);
            Stand(f, "Oil drum", 5.8f, -1f, new Vector3(0.6f, 0.9f, 0.6f), 0f, "HL_BarrelBlue");
            spots = new List<Spot>
            {
                new Spot(-2f, 0.2f, "the counter", "Stock_Crate"),
                new Spot(0f, 5.6f, "the pumps", "Stock_Scrap"),
                new Spot(-6f, 6.6f, "the wrecked car", "Stock_Scrap"),
                new Spot(-3.5f, -4f, "the storeroom shelves", "Stock_Crate"),
                new Spot(6.3f, -2f, "the oil drums", "Salvage_Pile"),
            };
            find = new Spot(3.6f, -4.1f, "the safe", "Safe");
        }

        // The schoolhouse: one room, its roof fallen in, desks, the stove at the back with the nurse's
        // cabinet beside it (the find), and the bell post outside.
        private static void School(Frame f, out List<Spot> spots, out Spot? find)
        {
            var windows = new[] { (2f, 3f), (5f, 6f), (8f, 9f) };
            Rect4(f, -3.5f, -5f, 3.5f, 5f, 3.2f, 0.25f, "HL_WoodWeathered", 0.3f, front: new[] { (3f, 4.2f) }, left: windows, right: windows);
            Lay(f, "Fallen roof", -1.6f, 1.5f, 0f, new Vector3(4f, 0.18f, 9.6f), new Vector3(0f, 0f, 28f), "HL_WoodDark");
            Lay(f, "Roof", 1.8f, 3.4f, -1f, new Vector3(3.8f, 0.18f, 7f), new Vector3(0f, 0f, -22f), "HL_WoodDark", collider: false);
            Stand(f, "Stove", 0f, -4.2f, new Vector3(0.8f, 1f, 0.8f), 0f, "HL_Steel");
            Stand(f, "Stovepipe", 0f, -4.2f, new Vector3(0.2f, 3f, 0.2f), 0f, "HL_Steel", collider: false);
            for (int i = 0; i < 6; i++) Lay(f, "Desk", -1.2f + (i % 2) * 2.2f, 0.35f, 2.5f - (i / 2) * 1.8f, new Vector3(1f, 0.7f, 0.6f), new Vector3(0f, f.R(-15, 15), 0f), "HL_WoodWeathered");
            Stand(f, "Bell post", 4.8f, 5.6f, new Vector3(0.25f, 4f, 0.25f), 0f, "HL_WoodDark", collider: false);
            spots = new List<Spot>
            {
                new Spot(-1.5f, -1f, "the desks", "Stock_Crate"),
                new Spot(-2.5f, 4f, "the cloakroom", "Stock_Sack"),
                new Spot(-2.2f, -4.2f, "the woodbox", "Stock_Firewood"),
                new Spot(0f, 6.2f, "the porch", "Rubble"),
            };
            find = new Spot(2f, -4.4f, "the nurse's cabinet", "Cabinet");
        }

        // The feed store: a long timber barn of a shop, half its tin roof on the floor, sacks and bins, the
        // counter (the find is under it), and a loading dock along one side.
        private static void FeedStore(Frame f, out List<Spot> spots, out Spot? find)
        {
            Rect4(f, -5f, -6f, 5f, 6f, 4f, 0.3f, "HL_WoodDark", 0.3f, front: new[] { (3.2f, 6.6f) }, right: new[] { (4f, 6f) });
            Lay(f, "Roof", 0f, 4f, -3f, new Vector3(10.4f, 0.15f, 6.2f), Vector3.zero, "HL_TinRust", collider: false);
            Lay(f, "Fallen roof", 0.5f, 1.2f, 2.5f, new Vector3(8f, 0.15f, 5.5f), new Vector3(-16f, 6f, 0f), "HL_TinRust");
            Lay(f, "Counter", 2f, 0.5f, 2.4f, new Vector3(4f, 1f, 0.8f), Vector3.zero, "HL_WoodWeathered");
            Lay(f, "Loading dock", 6.2f, 0.5f, 0f, new Vector3(2f, 1f, 11f), Vector3.zero, "HL_WoodWeathered");
            for (int i = 0; i < 4; i++) Stand(f, "Seed bin", -4.2f, -4f + i * 2f, new Vector3(1.2f, 1.1f, 1.6f), 0f, "HL_WoodWeathered");
            spots = new List<Spot>
            {
                new Spot(-2.6f, -3f, "the sacks", "Stock_Sack"),
                new Spot(-3f, 2f, "the seed bins", "Stock_Sack"),
                new Spot(-0.5f, -4.8f, "the back shelves", "Stock_Crate"),
                new Spot(1f, 4.6f, "the scale", "Stock_Scrap"),
                new Spot(7.8f, -3f, "the loading dock", "Salvage_Pile"),
            };
            find = new Spot(2f, 1.5f, "under the counter", "Stock_Crate");
        }

        // The Elder Works: a long barrow (a stone hall under an earth mound) with a stone doorway toward home,
        // and a ring of standing stones whose shadows fall a few degrees off from everything else's.
        private static void ElderWorks(Frame f, out List<Spot> spots, out Spot? find)
        {
            // The hall: walls, back stone, capstones.
            foreach (float side in new[] { -1.6f, 1.6f })
                for (float z = -9f; z < 7f; z += 2f)
                    Stand(f, "Hall stone", side, z + 1f, new Vector3(0.6f, 2.6f, 2.05f), 0f, "HL_Stone");
            Stand(f, "Back stone", 0f, -9.3f, new Vector3(3.8f, 2.6f, 0.6f), 0f, "HL_Stone");
            for (float z = -9f; z < 7f; z += 2f) Lay(f, "Capstone", 0f, 2.75f, z + 1f, new Vector3(4.2f, 0.4f, 1.95f), new Vector3(0f, f.R(-3, 3), 0f), "HL_Stone");
            // The mound over it (Unit_Mound, ADR 0029): a long earth shell, closed and rounded at the back, its
            // open face toward home filled with stones around the doorway.
            {
                float g = f.GroundAt(0f, -1.5f) - f.Root.position.y;
                GameObject shell = GreyboxKit.Place("Unit_Mound", Vector3.zero, 0f, 1f, f.Root, collider: true);
                shell.name = "Mound";
                shell.transform.localPosition = new Vector3(0f, g + 2.1f, -1.5f);
                shell.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                shell.transform.localScale = new Vector3(13f, 5.4f, 20f);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Stand(f, "Facade stone", side * 3.6f, 8.2f, new Vector3(2.4f, 2.6f, 0.7f), side * 6f, "HL_Stone");
                Stand(f, "Facade stone", side * 5.6f, 8.3f, new Vector3(2.2f, 1.4f, 0.7f), side * 10f, "HL_Stone");
            }
            Lay(f, "Gable stone", 0f, 4f, 7.9f, new Vector3(3.2f, 1.2f, 0.7f), Vector3.zero, "HL_Stone");
            // The doorway toward home.
            foreach (float side in new[] { -2f, 2f }) Stand(f, "Door stone", side, 7.6f, new Vector3(1f, 3.4f, 0.8f), 0f, "HL_Stone");
            Lay(f, "Lintel", 0f, 3.3f, 7.6f, new Vector3(5f, 0.6f, 1f), Vector3.zero, "HL_Stone");
            // The ring: twelve stones (or eleven), each with a shadow that keeps a different hour.
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f + 0.13f;
                float x = Mathf.Sin(a) * 15f, z = Mathf.Cos(a) * 15f - 1f;
                if (Mathf.Abs(x) < 3f && z > 8f) continue; // the way in stays open
                GameObject stone = Stand(f, "Standing stone", x, z, new Vector3(0.9f, f.R(2.4, 3.4), 0.55f), a * Mathf.Rad2Deg + f.R(-8, 8), "HL_Stone");
                WrongShadow(stone);
            }
            spots = new List<Spot>
            {
                new Spot(-0.8f, -3f, "a niche in the wall", "Rock_B"),
                new Spot(0.8f, -6.5f, "a niche in the wall", "Rock_B"),
            };
            find = null;
        }

        /// <summary>The stone casts no shadow of its own; a leaning, shadow-only twin casts one a few degrees off.</summary>
        private static void WrongShadow(GameObject stone)
        {
            var r = stone.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            GameObject twin = Object.Instantiate(stone, stone.transform.parent);
            twin.name = "Wrong shadow";
            Object.DestroyImmediate(twin.GetComponent<Collider>());
            twin.transform.localRotation = Quaternion.Euler(0f, 0f, 14f) * stone.transform.localRotation * Quaternion.Euler(9f, 0f, 0f);
            twin.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        private static void Rubble(Frame f, out List<Spot> spots, out Spot? find)
        {
            for (int i = 0; i < 4; i++) GreyboxKit.Place("Rubble", f.World(f.R(-8, 8), f.R(-8, 8)), f.R(0, 360), f.R(0.7, 1.2), f.Root, collider: true);
            spots = new List<Spot> { new Spot(-3f, 2f, "the rubble", "Rubble"), new Spot(3f, -2f, "the heap") };
            find = null;
        }

        // ---- Search spots ---------------------------------------------------------------------------

        private static void AddSpots(Frame f, PlacedPlace place, PlaceDef def, List<Spot> candidates, Spot? find)
        {
            int count = Mathf.Clamp(def.SearchMin + f.Rnd.Next(def.SearchMax - def.SearchMin + 1), 0, candidates.Count);
            // Seeded pick of which candidates are spots; index order is stable, so ids survive a rebuild.
            var order = new List<int>();
            for (int i = 0; i < candidates.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = f.Rnd.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            order = order.GetRange(0, count);
            order.Sort();
            for (int k = 0; k < order.Count; k++) MakeSpot(f, place, k, candidates[order[k]]);
            if (find.HasValue) MakeSpot(f, place, -1, find.Value);
        }

        private static void MakeSpot(Frame f, PlacedPlace place, int index, Spot s)
        {
            float g = f.GroundAt(s.X, s.Z) - f.Root.position.y;
            var go = new GameObject(index < 0 ? "Find" : "Search " + index);
            go.transform.SetParent(f.Root, false);
            go.transform.localPosition = new Vector3(s.X, g + s.Y, s.Z);
            go.transform.localRotation = Quaternion.Euler(0f, f.R(0, 360), 0f);
            GameObject full, searched;
            if (s.Model == "Safe" || s.Model == "Cabinet")
            {
                Vector3 size = s.Model == "Safe" ? new Vector3(0.9f, 1.1f, 0.8f) : new Vector3(0.8f, 1.6f, 0.4f);
                string mat = s.Model == "Safe" ? "HL_Steel" : "HL_TinRust";
                go.transform.localRotation = Quaternion.identity;
                full = SceneBuildKit.Block(PrimitiveType.Cube, "Closed", Vector3.zero, size, GreyboxKit.Mat(mat), go.transform, collider: true);
                full.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);
                searched = new GameObject("Opened");
                searched.transform.SetParent(go.transform, false);
                GameObject body = SceneBuildKit.Block(PrimitiveType.Cube, "Body", Vector3.zero, size, GreyboxKit.Mat(mat), searched.transform, collider: true);
                body.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);
                GameObject door = SceneBuildKit.Block(PrimitiveType.Cube, "Door", Vector3.zero, new Vector3(size.x, size.y, 0.06f), GreyboxKit.Mat(mat), searched.transform, collider: false);
                door.transform.localPosition = new Vector3(size.x * 0.5f, size.y / 2f, size.z / 2f + size.x * 0.45f);
                door.transform.localRotation = Quaternion.Euler(0f, 80f, 0f);
            }
            else
            {
                full = GreyboxKit.Place(s.Model, go.transform.position, go.transform.eulerAngles.y, 1f, go.transform, collider: false);
                full.name = "Full";
                searched = GreyboxKit.Place(s.Model, go.transform.position, go.transform.eulerAngles.y + 40f, 1f, go.transform, collider: false);
                searched.name = "Gone through";
                searched.transform.localScale = new Vector3(1.15f, 0.35f, 1.15f);
                if (s.Model.StartsWith("Rock"))
                {
                    // A loose stone in a niche, small enough not to block the hall.
                    full.transform.localScale = Vector3.one * 0.35f;
                    searched.transform.localScale = new Vector3(0.4f, 0.12f, 0.4f);
                }
            }
            searched.SetActive(false);
            go.AddComponent<PlaceSpot>().Configure(place.Id, place.Kind, index, s.What, full, searched);
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }
    }
}
