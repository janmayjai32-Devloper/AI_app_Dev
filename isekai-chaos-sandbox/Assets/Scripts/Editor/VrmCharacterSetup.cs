#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using IsekaiChaos.Animation;
using IsekaiChaos.Combat;
using IsekaiChaos.Customizer;
using IsekaiChaos.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IsekaiChaos.EditorTools
{
    /// <summary>
    /// One-click setup that turns an imported VRoid/VRM (or any Humanoid) model into a game-ready
    /// character for this project.
    ///
    /// Usage: drag the imported VRM prefab into a scene, select it, then
    ///   IsekaiChaos ▸ Characters ▸ Setup Selected As Player / Companion / Enemy
    ///
    /// What it does:
    ///   1. Wraps the model in a root object ("Player_<name>") and moves the model to a "Model" child
    ///   2. Adds CharacterController (sized from the skeleton) + NavMeshAgent
    ///   3. Adds Health, CombatController, HitStop, motor, interaction, gathering, road speed and save binder
    ///   4. Builds Hurtbox capsules on humanoid bones, mapped to BodyRegion (for clothing damage)
    ///   5. Adds a weapon socket + Hitbox on the right hand
    ///   6. Adds AnimationEventRelay on the Animator and assigns Humanoid_Base.controller if present
    /// Everything is Undo-able.
    /// </summary>
    public static class VrmCharacterSetup
    {
        const string ControllerPath = "Assets/Animation/Humanoid_Base.controller";
        const string HurtboxLayer = "Hurtbox";
        const string CharacterLayer = "Character";

        enum Role { Player, Companion, Enemy }

        [MenuItem("IsekaiChaos/Characters/Setup Selected As Player")]
        static void SetupPlayer() => Setup(Role.Player);

        [MenuItem("IsekaiChaos/Characters/Setup Selected As Companion")]
        static void SetupCompanion() => Setup(Role.Companion);

        [MenuItem("IsekaiChaos/Characters/Setup Selected As Enemy")]
        static void SetupEnemy() => Setup(Role.Enemy);

        [MenuItem("IsekaiChaos/Characters/Setup Selected As Player", true)]
        [MenuItem("IsekaiChaos/Characters/Setup Selected As Companion", true)]
        [MenuItem("IsekaiChaos/Characters/Setup Selected As Enemy", true)]
        static bool Validate() => Selection.activeGameObject && Selection.activeGameObject.GetComponentInChildren<Animator>();

        static void Setup(Role role)
        {
            var model = Selection.activeGameObject;
            var animator = model.GetComponentInChildren<Animator>();
            if (!animator || !animator.isHuman)
            {
                EditorUtility.DisplayDialog("Isekai Chaos", "Selected object needs a Humanoid Animator (VRM models have one).", "OK");
                return;
            }

            Undo.SetCurrentGroupName($"Setup {role}");
            int group = Undo.GetCurrentGroup();

            // 1. Root wrapper
            var root = new GameObject($"{role}_{model.name}");
            Undo.RegisterCreatedObjectUndo(root, "root");
            root.transform.SetPositionAndRotation(model.transform.position, model.transform.rotation);
            if (model.transform.parent) Undo.SetTransformParent(root.transform, model.transform.parent, "parent");
            Undo.SetTransformParent(model.transform, root.transform, "model");
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            SetLayer(root, CharacterLayer);

            // 2. Capsule from skeleton
            float height = MeasureHeight(animator, root.transform);
            var cc = Undo.AddComponent<CharacterController>(root);
            cc.height = height;
            cc.radius = Mathf.Clamp(height * 0.17f, 0.22f, 0.4f);
            cc.center = new Vector3(0, height * 0.5f + cc.skinWidth, 0);
            cc.stepOffset = Mathf.Min(0.35f, height * 0.2f);

            var agent = Undo.AddComponent<NavMeshAgent>(root);
            agent.radius = cc.radius;
            agent.height = height;
            agent.angularSpeed = 720f;

            // 3. Gameplay components
            var health = Undo.AddComponent<Health>(root);
            SetSerialized(health, "team", role == Role.Enemy ? (int)Team.Enemy : (int)Team.Player);
            SetSerialized(health, "animator", animator);

            var combat = Undo.AddComponent<CombatController>(root);
            SetSerialized(combat, "animator", animator);
            Undo.AddComponent<HitStopController>(root);

            if (role == Role.Enemy)
            {
                Undo.AddComponent<EnemyAI>(root);
            }
            else
            {
                Undo.AddComponent<ClickToMoveMotor>(root);
                Undo.AddComponent<InteractionController>(root);
                Undo.AddComponent<RoadAwareSpeed>(root);
                Undo.AddComponent<CharacterStateBinder>(root);
                if (role == Role.Player)
                {
                    Undo.AddComponent<GatheringController>(root);
                    Undo.AddComponent<PlayerDefeatHandler>(root);
                    root.tag = "Player";
                }
            }

            // 4. Hurtboxes
            var hurtboxes = BuildHurtboxes(animator, height);

            // 5. Weapon socket + hitbox
            var hitbox = BuildWeaponHitbox(animator, height);
            if (hitbox)
            {
                var list = new List<Hitbox> { hitbox };
                var so = new SerializedObject(combat);
                var prop = so.FindProperty("hitboxes");
                prop.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 6. Animator relay + controller
            if (!animator.GetComponent<AnimationEventRelay>()) Undo.AddComponent<AnimationEventRelay>(animator.gameObject);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // mobile: skip skinning off-screen
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl) { Undo.RecordObject(animator, "controller"); animator.runtimeAnimatorController = ctrl; }

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root;
            Debug.Log($"[VrmCharacterSetup] {root.name}: height {height:0.00} m, {hurtboxes} hurtboxes, " +
                      $"{(hitbox ? "weapon hitbox on right hand" : "no right hand bone found")}, " +
                      $"{(ctrl ? "controller assigned" : $"create {ControllerPath} and assign it")}.");
        }

        // ------------------------------------------------------------------------------------
        static float MeasureHeight(Animator a, Transform root)
        {
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            if (!head) return 1.6f;
            float headTop = root.InverseTransformPoint(head.position).y + 0.18f;   // head bone sits at skull base
            return Mathf.Clamp(headTop, 0.8f, 3f);
        }

        struct HB { public HumanBodyBones bone, child; public BodyRegion region; public float radius; public float mult; }

        static int BuildHurtboxes(Animator a, float h)
        {
            var defs = new[]
            {
                new HB { bone = HumanBodyBones.Head,          child = HumanBodyBones.LastBone,      region = BodyRegion.Head,      radius = 0.075f, mult = 1.5f },
                new HB { bone = HumanBodyBones.Chest,         child = HumanBodyBones.Neck,          region = BodyRegion.Chest,     radius = 0.11f,  mult = 1f },
                new HB { bone = HumanBodyBones.Spine,         child = HumanBodyBones.Chest,         region = BodyRegion.Abdomen,   radius = 0.10f,  mult = 1f },
                new HB { bone = HumanBodyBones.Hips,          child = HumanBodyBones.Spine,         region = BodyRegion.Hips,      radius = 0.11f,  mult = 1f },
                new HB { bone = HumanBodyBones.LeftUpperArm,  child = HumanBodyBones.LeftLowerArm,  region = BodyRegion.UpperArms, radius = 0.04f,  mult = 0.8f },
                new HB { bone = HumanBodyBones.RightUpperArm, child = HumanBodyBones.RightLowerArm, region = BodyRegion.UpperArms, radius = 0.04f,  mult = 0.8f },
                new HB { bone = HumanBodyBones.LeftLowerArm,  child = HumanBodyBones.LeftHand,      region = BodyRegion.Forearms,  radius = 0.035f, mult = 0.8f },
                new HB { bone = HumanBodyBones.RightLowerArm, child = HumanBodyBones.RightHand,     region = BodyRegion.Forearms,  radius = 0.035f, mult = 0.8f },
                new HB { bone = HumanBodyBones.LeftUpperLeg,  child = HumanBodyBones.LeftLowerLeg,  region = BodyRegion.Thighs,    radius = 0.065f, mult = 0.9f },
                new HB { bone = HumanBodyBones.RightUpperLeg, child = HumanBodyBones.RightLowerLeg, region = BodyRegion.Thighs,    radius = 0.065f, mult = 0.9f },
                new HB { bone = HumanBodyBones.LeftLowerLeg,  child = HumanBodyBones.LeftFoot,      region = BodyRegion.Shins,     radius = 0.05f,  mult = 0.8f },
                new HB { bone = HumanBodyBones.RightLowerLeg, child = HumanBodyBones.RightFoot,     region = BodyRegion.Shins,     radius = 0.05f,  mult = 0.8f },
            };

            int layer = LayerMask.NameToLayer(HurtboxLayer);
            float scale = h / 1.6f;               // VRoid default height ≈ 1.6 m
            int made = 0;
            foreach (var d in defs)
            {
                var bone = a.GetBoneTransform(d.bone);
                if (!bone) continue;
                var go = new GameObject($"Hurtbox_{d.region}");
                Undo.RegisterCreatedObjectUndo(go, "hurtbox");
                go.transform.SetParent(bone, false);
                if (layer >= 0) go.layer = layer;

                var cap = go.AddComponent<CapsuleCollider>();
                cap.isTrigger = true;
                cap.radius = d.radius * scale;
                var child = d.child != HumanBodyBones.LastBone ? a.GetBoneTransform(d.child) : null;
                if (child)
                {
                    Vector3 local = bone.InverseTransformPoint(child.position);
                    cap.direction = DominantAxis(local);
                    cap.center = local * 0.5f;
                    cap.height = local.magnitude + cap.radius * 2f;
                }
                else
                {
                    cap.direction = 1;
                    cap.center = new Vector3(0, cap.radius, 0);
                    cap.height = cap.radius * 2.6f;
                }

                var hb = go.AddComponent<Hurtbox>();
                hb.region = d.region;
                hb.damageMultiplier = d.mult;
                hb.isWeakSpot = d.bone == HumanBodyBones.Head;
                made++;
            }
            return made;
        }

        static Hitbox BuildWeaponHitbox(Animator a, float h)
        {
            var hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            if (!hand) return null;
            var socket = new GameObject("WeaponSocket");
            Undo.RegisterCreatedObjectUndo(socket, "socket");
            socket.transform.SetParent(hand, false);

            var tip = new GameObject("Tip");
            tip.transform.SetParent(socket.transform, false);
            // VRM hands point along +X (right hand) in bone space; a 0.9 m blade from the palm.
            tip.transform.localPosition = new Vector3(0.9f * h / 1.6f, 0, 0);

            var hitbox = socket.AddComponent<Hitbox>();
            var so = new SerializedObject(hitbox);
            so.FindProperty("tip").objectReferenceValue = tip.transform;
            int layer = LayerMask.NameToLayer(HurtboxLayer);
            if (layer >= 0) so.FindProperty("hurtboxMask").intValue = 1 << layer;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hitbox;
        }

        static int DominantAxis(Vector3 v)
        {
            v = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return v.x > v.y && v.x > v.z ? 0 : v.y > v.z ? 1 : 2;
        }

        static void SetLayer(GameObject go, string layerName)
        {
            int l = LayerMask.NameToLayer(layerName);
            if (l < 0) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = l;
        }

        static void SetSerialized(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value; else p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSerialized(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================================
        // MToon → AnimeToon material conversion
        // =====================================================================================
        [MenuItem("IsekaiChaos/Characters/Convert Selected Character Materials To AnimeToon")]
        static void ConvertMaterials()
        {
            var go = Selection.activeGameObject;
            if (!go) return;
            var shader = Shader.Find("IsekaiChaos/AnimeToon");
            if (!shader) { EditorUtility.DisplayDialog("Isekai Chaos", "AnimeToon shader not found (Assets/Art/Shaders).", "OK"); return; }

            string folder = "Assets/Art/Characters/Materials/" + go.name;
            Directory.CreateDirectory(folder);
            var converted = new Dictionary<Material, Material>();
            int count = 0;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (!src || src.shader == shader) continue;
                    if (!converted.TryGetValue(src, out var dst))
                    {
                        dst = new Material(shader) { name = src.name + "_Toon" };
                        CopyTex(src, dst, "_BaseMap", "_MainTex", "_BaseMap");
                        CopyColor(src, dst, "_BaseColor", "_Color", "_BaseColor");
                        CopyColor(src, dst, "_ShadowColor", "_ShadeColor");
                        CopyColor(src, dst, "_OutlineColor", "_OutlineColor");
                        CopyTex(src, dst, "_EmissionMap", "_EmissionMap");
                        CopyColor(src, dst, "_EmissionColor", "_EmissionColor");
                        CopyColor(src, dst, "_RimColor", "_RimColor", "_ParametricRimColor");

                        string n = src.name.ToLowerInvariant();
                        if (n.Contains("hair")) { dst.EnableKeyword("_HAIR_SPEC"); dst.SetFloat("_HairSpec", 1); }
                        if (n.Contains("face") || n.Contains("eye") || n.Contains("mouth")) dst.SetFloat("_OutlineWidth", 0.4f);
                        if (n.Contains("eye") || n.Contains("highlight")) dst.SetFloat("_ShadowThreshold", -1f);  // eyes never shade

                        // VRoid transparent parts (eye highlights, lashes) keep their original shader.
                        if (src.renderQueue > 2450) { converted[src] = src; continue; }

                        AssetDatabase.CreateAsset(dst, $"{folder}/{dst.name}.mat");
                        converted[src] = dst;
                        count++;
                    }
                    if (dst != src) { mats[i] = dst; changed = true; }
                }
                if (changed) { Undo.RecordObject(r, "materials"); r.sharedMaterials = mats; }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[VrmCharacterSetup] Converted {count} materials to AnimeToon → {folder}. " +
                      "Face SDF: assign a face SDF texture, enable 'Use Face SDF', add FaceShadowDriver.");
        }

        static void CopyTex(Material src, Material dst, string dstProp, params string[] srcProps)
        {
            foreach (var p in srcProps)
                if (src.HasProperty(p) && src.GetTexture(p)) { dst.SetTexture(dstProp, src.GetTexture(p)); return; }
        }

        static void CopyColor(Material src, Material dst, string dstProp, params string[] srcProps)
        {
            foreach (var p in srcProps)
                if (src.HasProperty(p)) { dst.SetColor(dstProp, src.GetColor(p)); return; }
        }
    }
}
#endif
