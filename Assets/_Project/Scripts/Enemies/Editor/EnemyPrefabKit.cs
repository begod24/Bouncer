using System.Collections.Generic;
using Bouncer.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies.EditorTools
{
    public static class EnemyPrefabKit
    {
        public const string Prefabs = "Assets/_Project/Prefabs/Enemies/";
        public const string Data = "Assets/_Project/Data/Enemies/";
        public const string Models = "Assets/_Project/Art/Models/Enemies/";

        public sealed class Shell
        {
            public GameObject Root;
            public Transform Visual;
            public Transform Model;
            public Transform AimPoint;

            public Transform Part(string name)
            {
                foreach (var t in Model.GetComponentsInChildren<Transform>(true))
                    if (t.name == name)
                        return t;
                Debug.LogWarning($"[EnemyPrefabKit] no part {name} in {Model.name}");
                return null;
            }
        }

        public static T Definition<T>(string assetName) where T : ScriptableObject
        {
            string path = Data + assetName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static Shell Create(string name, string modelName, float aimHeight)
        {
            var root = new GameObject(name) { layer = Layers.Enemy };
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + modelName + ".fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(visual, false);
            var aim = new GameObject("AimPoint").transform;
            aim.SetParent(root.transform, false);
            aim.localPosition = new Vector3(0f, aimHeight, 0f);
            SetLayer(root.transform, Layers.Enemy);
            return new Shell { Root = root, Visual = visual, Model = instance.transform, AimPoint = aim };
        }

        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t)
                SetLayer(child, layer);
        }

        public static NavMeshAgent AddAgent(GameObject root, float radius, float height, float speed, float acceleration)
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.speed = speed;
            agent.acceleration = acceleration;
            agent.angularSpeed = 0f;
            agent.avoidancePriority = 45;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            return agent;
        }

        public static Rigidbody AddKinematicBody(GameObject root, float mass = 1f)
        {
            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            return body;
        }

        public static CapsuleCollider AddCapsule(GameObject root, float radius, float height)
        {
            var c = root.AddComponent<CapsuleCollider>();
            c.radius = radius;
            c.height = height;
            c.center = new Vector3(0f, height * 0.5f, 0f);
            return c;
        }

        public static BoxCollider AddBox(GameObject root, Vector3 size, Vector3 center)
        {
            var c = root.AddComponent<BoxCollider>();
            c.size = size;
            c.center = center;
            return c;
        }

        public static HitFlash AddBasics(Shell shell)
        {
            var root = shell.Root;
            root.AddComponent<Health>();
            var target = root.AddComponent<Targetable>();
            var so = new SerializedObject(target);
            so.FindProperty("team").enumValueIndex = (int)Team.Enemy;
            so.FindProperty("aimPoint").objectReferenceValue = shell.AimPoint;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root.AddComponent<HitFlash>();
        }

        public static void AddReward(Shell shell, int coins, float chance = 0.35f, bool portfolio = false,
            float squash = 0.12f, float recoil = 0.1f)
        {
            var reward = shell.Root.AddComponent<EnemyReward>();
            var so = new SerializedObject(reward);
            so.FindProperty("coins").intValue = coins;
            so.FindProperty("chance").floatValue = chance;
            so.FindProperty("portfolio").boolValue = portfolio;
            so.ApplyModifiedPropertiesWithoutUndo();
            var punch = shell.Root.AddComponent<HitPunch>();
            so = new SerializedObject(punch);
            so.FindProperty("visual").objectReferenceValue = shell.Visual;
            so.FindProperty("squash").floatValue = squash;
            so.FindProperty("recoil").floatValue = recoil;
            so.FindProperty("jitter").floatValue = 0.04f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, params (string field, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var p = so.FindProperty(field);
                if (p == null)
                {
                    Debug.LogWarning($"[EnemyPrefabKit] {target.GetType().Name} has no field {field}");
                    continue;
                }
                switch (value)
                {
                    case null:
                        p.objectReferenceValue = null;
                        break;
                    case Object o:
                        p.objectReferenceValue = o;
                        break;
                    case float f:
                        p.floatValue = f;
                        break;
                    case int i:
                        p.intValue = i;
                        break;
                    case bool b:
                        p.boolValue = b;
                        break;
                    case Object[] array:
                        p.arraySize = array.Length;
                        for (int k = 0; k < array.Length; k++)
                            p.GetArrayElementAtIndex(k).objectReferenceValue = array[k];
                        break;
                    default:
                        Debug.LogWarning($"[EnemyPrefabKit] unsupported value for {field}");
                        break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject Save(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        public static GameObject BuildDebris(string name, string modelName, float totalMass = 2f)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + modelName + ".fbx");
            var temp = Object.Instantiate(model);
            var root = new GameObject(name) { layer = Layers.Ragdoll };
            var bodies = new List<Rigidbody>();
            var filters = temp.GetComponentsInChildren<MeshFilter>(true);
            float totalVolume = 0f;
            foreach (var f in filters)
                if (f.sharedMesh)
                    totalVolume += Volume(f.sharedMesh.bounds.size);
            foreach (var f in filters)
            {
                if (!f.sharedMesh)
                    continue;
                var piece = new GameObject(f.name + "Piece") { layer = Layers.Ragdoll };
                piece.transform.SetParent(root.transform, false);
                piece.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation);
                var mesh = new GameObject(f.name) { layer = Layers.Ragdoll };
                mesh.transform.SetParent(piece.transform, false);
                mesh.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                mesh.AddComponent<MeshRenderer>().sharedMaterials = f.GetComponent<MeshRenderer>().sharedMaterials;
                var bounds = f.sharedMesh.bounds;
                var box = piece.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, Vector3.one * 0.06f);
                var body = piece.AddComponent<Rigidbody>();
                body.mass = Mathf.Max(0.15f, totalMass * Volume(bounds.size) / Mathf.Max(1e-4f, totalVolume));
                bodies.Add(body);
            }
            Object.DestroyImmediate(temp);
            var debris = root.AddComponent<Debris>();
            Set(debris, ("pieces", bodies.ToArray()));
            return Save(root, Prefabs + name + ".prefab");
        }

        static float Volume(Vector3 size) => Mathf.Max(0.001f, size.x) * Mathf.Max(0.001f, size.y) * Mathf.Max(0.001f, size.z);

        public static ExpandingRing RingVariant(string name, Color color, float duration, float startWidth, float endWidth)
        {
            string path = "Assets/_Project/Prefabs/VFX/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<ExpandingRing>(path);
            if (existing != null)
                return existing;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/BlastRing.prefab");
            var copy = Object.Instantiate(source);
            copy.name = name;
            var ring = copy.GetComponent<ExpandingRing>();
            var so = new SerializedObject(ring);
            so.FindProperty("color").colorValue = color;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("startWidth").floatValue = startWidth;
            so.FindProperty("endWidth").floatValue = endWidth;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(copy, path).GetComponent<ExpandingRing>();
        }
    }
}
