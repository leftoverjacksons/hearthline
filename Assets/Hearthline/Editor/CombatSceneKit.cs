using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Game;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Hearthline.EditorTools
{
    /// <summary>
    /// Combat step 4 scene pieces: the enemy body prefab (a mannequin with hit zones, a gun, a nav agent and
    /// the player's animator), the region's NavMesh, and the spawner that stages fights. Generated: change
    /// this, then rebuild the scene.
    /// </summary>
    public static class CombatSceneKit
    {
        public const string PrefabFolder = "Assets/Hearthline/Art/Prefabs";
        public const string EnemyPrefabPath = PrefabFolder + "/Enemy_Reaver.prefab";
        private const string ArmaturePath = "Assets/ThirdParty/StarterAssets/Runtime/ThirdPersonController/Character/Models/Armature.fbx";

        public static GameObject BuildEnemyPrefab()
        {
            SceneBuildKit.EnsureFolder(PrefabFolder);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArmaturePath);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            body.name = "Enemy_Reaver";
            body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // zones are placed facing +Z at the origin

            Animator animator = body.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorBuilder.Path)
                ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorBuilder.StarterController);
            SceneBuildKit.AddHitZones(body, SceneBuildKit.EnsureHitboxLayer());
            body.AddComponent<HitReceiver>();
            var guns = body.AddComponent<GunVisuals>();
            guns.Configure(new List<GunVisuals.Model>
            {
                new GunVisuals.Model { visual = "rifle_lever", prefab = GreyboxKit.Model("Gun_Rifle_Lever") },
                new GunVisuals.Model { visual = "revolver", prefab = GreyboxKit.Model("Gun_Revolver") },
            });

            var agent = body.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 3.6f;
            agent.angularSpeed = 540f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.15f;
            agent.autoBraking = true;

            var tracer = new GameObject("Tracer").AddComponent<LineRenderer>();
            tracer.transform.SetParent(body.transform, false);
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.widthMultiplier = 0.012f;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.sharedMaterial = SceneBuildKit.TracerMaterial();
            body.AddComponent<EnemyAI>().Configure(tracer);

            // Reavers read darker than the household: weathered dark cloth.
            Material cloth = GreyboxKit.Mat("HL_WoodDark");
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer) continue;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = cloth;
                r.sharedMaterials = mats;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(body, EnemyPrefabPath);
            Object.DestroyImmediate(body);
            return prefab;
        }

        /// <summary>Bakes a NavMesh over the scene's solid colliders (trigger volumes and hit zones ignored) and saves it next to the scene.</summary>
        public static NavMeshSurface BakeNavMesh(string dataPath)
        {
            int hitbox = LayerMask.NameToLayer(HitReceiver.LayerName);
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
                if (c.isTrigger && c.GetComponent<NavMeshModifier>() == null) c.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var surface = new GameObject("Navigation").AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << hitbox) | (1 << 8)); // not hit zones, not the player
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.CreateAsset(surface.navMeshData, dataPath);
            return surface;
        }

        public const string DeerPrefabPath = PrefabFolder + "/Animal_Crookhorn.prefab";

        /// <summary>
        /// The hunted deer (docs/specs/hunt.md): the greybox buck with hit zones on head, neck, chest and legs, a nav
        /// agent and DeerAI moving its neck and legs.
        /// </summary>
        public static GameObject BuildDeerPrefab()
        {
            SceneBuildKit.EnsureFolder(PrefabFolder);
            var deer = (GameObject)PrefabUtility.InstantiatePrefab(GreyboxKit.Model("Deer_Crookhorn"));
            PrefabUtility.UnpackPrefabInstance(deer, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            deer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Transform Part(string n) => SceneBuildKit.FindChild(deer.transform, n);
            Transform body = Part("Deer_Body"), neck = Part("Deer_Neck");
            Transform[] legs = { Part("Deer_Leg_FL"), Part("Deer_Leg_FR"), Part("Deer_Leg_BL"), Part("Deer_Leg_BR") };
            int layer = SceneBuildKit.EnsureHitboxLayer();
            void Zone(Hearthline.Sim.BodyRegion region, Transform parent, Vector3 worldCentre, Vector3 size)
            {
                var zone = new GameObject("HitZone_" + region);
                zone.layer = layer;
                zone.transform.SetParent(parent, false);
                zone.transform.position = worldCentre;
                var box = zone.AddComponent<BoxCollider>(); // before HitRegion, which requires a collider
                box.isTrigger = true;
                box.size = size;
                zone.AddComponent<HitRegion>().Configure(region);
            }
            // Unity space: the deer faces +Z, about 1 m at the shoulder.
            Zone(Hearthline.Sim.BodyRegion.Head, neck, new Vector3(0f, 1.47f, 0.9f), new Vector3(0.2f, 0.26f, 0.36f));
            Zone(Hearthline.Sim.BodyRegion.Face, neck, new Vector3(0f, 1.26f, 0.64f), new Vector3(0.2f, 0.26f, 0.42f)); // the neck: a killing shot too
            Zone(Hearthline.Sim.BodyRegion.Torso, body, new Vector3(0f, 0.95f, 0f), new Vector3(0.44f, 0.54f, 1.3f));
            Zone(Hearthline.Sim.BodyRegion.ArmL, legs[0], legs[0].position + Vector3.down * 0.45f, new Vector3(0.1f, 0.9f, 0.1f));
            Zone(Hearthline.Sim.BodyRegion.ArmR, legs[1], legs[1].position + Vector3.down * 0.45f, new Vector3(0.1f, 0.9f, 0.1f));
            Zone(Hearthline.Sim.BodyRegion.LegL, legs[2], legs[2].position + Vector3.down * 0.45f, new Vector3(0.12f, 0.9f, 0.12f));
            Zone(Hearthline.Sim.BodyRegion.LegR, legs[3], legs[3].position + Vector3.down * 0.45f, new Vector3(0.12f, 0.9f, 0.12f));
            deer.AddComponent<HitReceiver>();
            var agent = deer.AddComponent<NavMeshAgent>();
            agent.radius = 0.45f; agent.height = 1.6f; agent.speed = 1.1f; agent.angularSpeed = 360f; agent.acceleration = 8f;
            agent.stoppingDistance = 0.2f;
            deer.AddComponent<DeerAI>().Configure(neck, legs);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(deer, DeerPrefabPath);
            Object.DestroyImmediate(deer);
            return prefab;
        }

        public static HuntDirector AddHuntDirector(Transform player)
        {
            var director = new GameObject("Hunt Director").AddComponent<HuntDirector>();
            director.Configure(player, BuildDeerPrefab());
            return director;
        }

        public static EnemySpawner AddSpawner(Transform player)
        {
            var spawner = new GameObject("Enemy Spawner").AddComponent<EnemySpawner>();
            spawner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) ?? BuildEnemyPrefab(), player);
            return spawner;
        }
    }
}
