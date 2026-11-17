using System;
using System.Collections.Generic;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Shows the guns a character has equipped on their body (DESIGN: every equipped item is on the model):
    /// the long gun across the back, the sidearm on the hip, and whichever one is drawn in the right hand,
    /// pointed at the aim point. Reads the simulation's gear; presentation says which gun is drawn and where
    /// it points. Stand-in models (ADR 0020 kit); placement is by the character's body, not bone axes, so it
    /// works on any humanoid.
    /// </summary>
    [RequireComponent(typeof(SimCharacterLink), typeof(Animator))]
    public sealed class GunVisuals : MonoBehaviour
    {
        [Serializable]
        public struct Model
        {
            [Tooltip("Gear visual key from vocab.yaml (rifle_lever, revolver).")]
            public string visual;
            [Tooltip("Model whose muzzle points along -Z with the grip at the origin (Blender kit export).")]
            public GameObject prefab;
        }

        public enum Drawn { None, LongGun, Sidearm }

        [SerializeField] private List<Model> models = new List<Model>();

        [Header("Placement (character space, metres and degrees)")]
        [SerializeField] private Vector3 backOffset = new Vector3(0.02f, -0.05f, -0.16f);
        [SerializeField] private Vector3 backDirection = new Vector3(-0.45f, 1f, 0f);
        [SerializeField] private Vector3 hipOffset = new Vector3(0.17f, -0.02f, 0.02f);
        [SerializeField] private Vector3 handOffset = new Vector3(0f, 0f, 0.02f);
        [Tooltip("While aiming, the long gun's grip sits this far from the right shoulder (character space), stock to the shoulder.")]
        [SerializeField] private Vector3 shoulderedOffset = new Vector3(-0.04f, -0.04f, 0.3f);
        [Tooltip("While aiming, the sidearm's grip sits this far from the right shoulder: arm out.")]
        [SerializeField] private Vector3 sidearmAimOffset = new Vector3(-0.12f, -0.02f, 0.55f);
        [Tooltip("Where a drawn gun points when not aiming (low ready), in character space.")]
        [SerializeField] private Vector3 lowReady = new Vector3(0f, -0.45f, 1f);

        private Animator _animator;
        private SimCharacterLink _link;
        private Transform _longGun, _sidearm;
        private string _longKey, _sideKey;

        /// <summary>Set by presentation each frame.</summary>
        public Drawn InHand { get; set; }
        /// <summary>World point the drawn gun aims at, or null for low ready.</summary>
        public Vector3? AimPoint { get; set; }

        /// <summary>The muzzle of the drawn gun (for tracers and flashes), or null.</summary>
        public Transform Muzzle { get; private set; }

        public void Configure(List<Model> gunModels) => models = gunModels;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _link = GetComponent<SimCharacterLink>();
        }

        private void LateUpdate()
        {
            Simulation sim = SimHost.Instance != null ? SimHost.Instance.Sim : null;
            Character who = _link.Resolve(sim);
            string longKey = null, sideKey = null;
            if (who != null && sim != null)
                foreach (GearItem g in who.Gear)
                {
                    if (!g.Equipped || !sim.Content.Items.TryGetValue(g.DefId, out ItemDef def) || def.Equip == null) continue;
                    if (def.Equip.Slot == EquipSlot.LongGun) longKey = def.Equip.Visual;
                    if (def.Equip.Slot == EquipSlot.Sidearm) sideKey = def.Equip.Visual;
                }
            Refresh(ref _longGun, ref _longKey, longKey, "LongGun");
            Refresh(ref _sidearm, ref _sideKey, sideKey, "Sidearm");

            Transform chest = _animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? _animator.GetBoneTransform(HumanBodyBones.Chest);
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Quaternion body = transform.rotation;

            if (_longGun != null)
            {
                if (InHand == Drawn.LongGun && hand != null) Hold(_longGun, hand, body, shoulderedOffset);
                else if (chest != null)
                {
                    _longGun.position = chest.position + body * backOffset;
                    _longGun.rotation = Quaternion.LookRotation(body * backDirection.normalized, body * Vector3.back);
                }
            }
            if (_sidearm != null)
            {
                if (InHand == Drawn.Sidearm && hand != null) Hold(_sidearm, hand, body, sidearmAimOffset);
                else if (hips != null)
                {
                    _sidearm.position = hips.position + body * hipOffset;
                    _sidearm.rotation = Quaternion.LookRotation(body * new Vector3(0f, -1f, 0.15f), body * Vector3.forward);
                }
            }
            Transform held = InHand == Drawn.LongGun ? _longGun : InHand == Drawn.Sidearm ? _sidearm : null;
            Muzzle = held != null ? held.Find("Muzzle") : null;
        }

        private void Hold(Transform gun, Transform hand, Quaternion body, Vector3 aimOffset)
        {
            // Aiming: placed from the shoulder along the aim line, so it reads right whatever the arm pose is.
            // Otherwise: in the hand at low ready.
            Transform shoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (AimPoint.HasValue && shoulder != null)
            {
                Vector3 aimDir = (AimPoint.Value - shoulder.position).normalized;
                Quaternion look = Quaternion.LookRotation(aimDir, body * Vector3.up);
                gun.position = shoulder.position + look * aimOffset;
                gun.rotation = Quaternion.LookRotation((AimPoint.Value - gun.position).normalized, body * Vector3.up);
                return;
            }
            gun.position = hand.position + body * handOffset;
            Vector3 dir = AimPoint.HasValue ? AimPoint.Value - gun.position : body * lowReady;
            if (dir.sqrMagnitude < 1e-4f) dir = body * Vector3.forward;
            gun.rotation = Quaternion.LookRotation(dir.normalized, body * Vector3.up);
        }

        private void Refresh(ref Transform current, ref string currentKey, string key, string label)
        {
            if (key == currentKey && (current != null) == (key != null)) return;
            if (current != null) Destroy(current.gameObject);
            current = null;
            currentKey = key;
            if (key == null) return;
            GameObject prefab = models.Find(m => m.visual == key).prefab;
            if (prefab == null) return;
            // A pivot with +Z as the muzzle direction; the kit model inside points along -Z.
            current = new GameObject(label + " (" + key + ")").transform;
            current.SetParent(transform, false);
            Transform model = Instantiate(prefab, current).transform;
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                Bounds local = r.localBounds;
                b.Encapsulate(Quaternion.Euler(0f, 180f, 0f) * local.max);
                b.Encapsulate(Quaternion.Euler(0f, 180f, 0f) * local.min);
            }
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(current, false);
            muzzle.localPosition = new Vector3(0f, b.center.y + b.extents.y * 0.4f, b.max.z);
        }
    }
}
