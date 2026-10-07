using Bouncer.Balls;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bouncer.EditorTools
{
    static partial class CardContentBuilder
    {
        // Мяч-ёжик: светящиеся шипы, искры за мячом, красный круг на асфальте под ним.
        static void BuildHedgehog()
        {
            const string path = Root + "Prefabs/Ball.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var visuals = root.GetComponent<BallVisuals>();
            var spikes = root.transform.Find("Mesh/Spikes");
            var spikeMesh = spikes.GetComponent<MeshFilter>().sharedMesh;
            float outer = 0f;
            foreach (var v in spikeMesh.vertices)
                outer = Mathf.Max(outer, v.magnitude);
            var glow = new Material(Shader.Find("Bouncer/SpikeGlow"));
            glow.SetFloat("_Inner", 0.5f);
            glow.SetFloat("_Outer", Mathf.Max(0.55f, outer));
            spikes.GetComponent<MeshRenderer>().sharedMaterial = SaveMaterial(glow, "M_SpikeGlow");

            var oldSparks = root.transform.Find("SpikySparks");
            if (oldSparks != null)
                Object.DestroyImmediate(oldSparks.gameObject);
            var sparks = Particles("SpikySparks", root.transform, new Color(1f, 0.75f, 0.2f), new Color(1f, 0.3f, 0.1f), 0, 1.2f, 0.45f, 0.09f,
                0.6f, 0.15f, loop: true);
            var emission = sparks.emission;
            emission.rateOverDistance = 7f;
            emission.enabled = false;
            var main = sparks.main;
            main.playOnAwake = true;
            var shards = Particles("Shards", sparks.transform, new Color(0.25f, 0.05f, 0.06f), new Color(0.55f, 0.1f, 0.08f), 0, 2f, 0.35f, 0.07f,
                1.4f, 0.15f, squares: true, loop: true);
            var shardEmission = shards.emission;
            shardEmission.rateOverDistance = 2.5f;
            var shardMain = shards.main;
            shardMain.playOnAwake = true;
            Set(visuals, "spikySparks", sparks);

            var oldWarning = root.transform.Find("SpikyWarning");
            if (oldWarning != null)
                Object.DestroyImmediate(oldWarning.gameObject);
            var warningRoot = new GameObject("SpikyWarning").transform;
            warningRoot.SetParent(root.transform, false);
            var warningMaterial = Unlit("M_SpikyWarning", Tex("T_SpikyWarning"), new Color(1f, 0.18f, 0.12f, 0.75f));
            Quad("Ring", warningRoot, warningMaterial, Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one);
            warningRoot.gameObject.SetActive(false);
            Set(visuals, "spikyWarning", warningRoot);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
