---
name: unity-mcp-quirks
description: Non-obvious behaviour of the Unity MCP (Unity_RunCommand etc.) in the Bouncer project and how to work around it
metadata:
  node_type: memory
  type: reference
  originSessionId: c438fbaa-0968-4eba-ac75-f20257a6ac93
  modified: 2026-09-24T07:39:26.898Z
---

- Unity_RunCommand wraps code in namespace `Unity.AI.Assistant.Agent.Dynamic.Extension.Editor`: `Mesh` and `Navigation` then resolve to namespaces — write `UnityEngine.Mesh`, `UnityEngine.UI.Navigation`.
- After any script change the next MCP call often fails with "Unity not detected" while Unity recompiles; call Unity_ManageEditor GetState with WaitForCompletion and retry.
- `AssetDatabase.DeleteAsset` through MCP fails ("user interactions are not supported"); delete the file + .meta from disk, then refresh.
- NavMesh re-bake that keeps the asset GUID: `surface.BuildNavMesh()`, `EditorUtility.CopySerialized(fresh, oldAsset)`, re-add data, then reset `oldAsset.name` (copy renames it).
- URP renders to a temporary RT fail with 8x MSAA; use 1 sample and supersample instead.
- Unity_RunCommand rejects `System.Reflection` usage (e.g. BindingFlags). To set a private serialized class field (e.g. a LocalizedString) use `SerializedProperty.boxedValue`.
- UI can be checked without Play Mode: instantiate the prefab in `EditorSceneManager.NewPreviewScene()`, switch the canvas to Screen Space – Camera, render to a RenderTexture and save a PNG to the scratchpad.
- In Edit mode `LocalizationSettings.SelectedLocale` is null, so `GetLocalizedString()` returns ""; set a locale (or pass one) before checking strings.

- Unity_RunCommand sometimes answers COMPILATION_FAILED with empty logs: it happened with a very long script and with one using `AssetDatabase.DeleteAsset` + a SerializedProperty iterator. Splitting into shorter scripts and dropping those calls fixed it.

- A scene copied with AssetDatabase.CopyAsset shares the source's NavMesh asset: set `surface.navMeshData = null`, call `surface.BuildNavMesh()`, then `AssetDatabase.CreateAsset(surface.navMeshData, <scene folder>/NavMesh-Arena.asset)` and reassign — the source asset stays intact. Don't put `AssetDatabase.DeleteAsset` in such a script at all (the MCP rejects the whole script).
- URP point/spot lights fall off with the square of distance: for ~1 lux on the ground, intensity ≈ distance² (kiosk window 4 at ~2 m, Site lamp post 40 at ~6.5 m, Rink floodlight 300 at ~27 m).
- Preview renders without Play Mode: set the camera, `cam.Render()` into a RenderTexture; LampLight/PlayerLantern don't run in edit mode, so lights show their prefab values.

- Edit-mode animation previews (2026-09-25): `Animator.Update` and a plain `PlayableGraph.Evaluate` do NOT move bones outside Play Mode. Clips: `AnimationMode.StartAnimationMode` → `BeginSampling` → `SampleAnimationClip(go, clip, t)` → `EndSampling`. Controllers: PlayableGraph (Manual) + `AnimatorControllerPlayable`, set params, `graph.Evaluate(0.02f)` ×N, then `AnimationMode.SamplePlayableGraph(graph, 0, t)` inside Begin/EndSampling. Skinned meshes must be `BakeMesh`-ed into a MeshRenderer before `cam.Render()`. Sampling a Humanoid clip resets the model root to the origin — put each model under an offset parent. Always `StopAnimationMode` in `finally`.
- Humanoid auto-mapping ignores a bone named `Chest` and bones without skin weights; an explicit HumanDescription is overwritten on the first import (empty skeleton), so the kids' importer just resets the mapping each import.

- New .cs files written from bash (2026-09-27): sometimes one is imported as a MonoScript but missing from `CompilationPipeline.GetAssemblies()[...].sourceFiles`. Symptoms: other scripts get CS0246 on its type, while Unity_ReadConsole shows 0 errors. Read `~/Library/Logs/Unity/Editor.log` (grep "error CS"). Neither ImportAsset(ForceUpdate) nor a folder reimport helps. The fix: move the .cs and .meta out of Assets, Refresh, move them back, Refresh. After adding scripts, check they are in sourceFiles or that `System.Type.GetType("Ns.Type, Assembly")` is not null.

- Check renders of a whole arena: `Camera.scene` does not isolate regular scenes (every loaded scene overlaps at the origin and renders). Instantiate the roots you need into `EditorSceneManager.NewPreviewScene()` and render there. Opening arenas additively also mixes their NavMeshes, so check spawn points against colliders, not `NavMesh.SamplePosition`.
- `result.Log` in Unity_RunCommand doesn't understand format specifiers like `{0:F2}` — format in C# (`x.ToString("F2")`).

See [[stage2-status-decisions]].

- Unity_RunCommand namespace clash (2026-09-28): `Image` resolves to the `Unity.AI.Image` namespace — alias `using UIImage = UnityEngine.UI.Image;` (same for Button to be safe).
- Check `Unity_ManageEditor GetState` → IsPlaying before AssetDatabase.Refresh / script edits: the user playtests in the same editor, and a domain reload during Play Mode throws in `BallVisuals.OnEnable` (non-serialized `_block` is null after reload) and kicks them out of the run (happened 2026-09-28).

- Offline compile check when the Unity MCP isn't connected (2026-10-06): Bee keeps compiler response files at `Library/Bee/artifacts/200b0aE.dag/Bouncer.<Asm>.rsp`. Copy one, point -out/-refout to a temp dir, drop the old `"Assets/...cs"` lines and list the asmdef folder's current .cs files, swap `-r:` of rebuilt Bouncer assemblies to the temp ref dlls, then run `<Unity.app>/Contents/Resources/Scripting/NetCoreRuntime/dotnet exec .../DotNetSdkRoslyn/csc.dll /nostdlib /noconfig @file.rsp`. NGO's RPC ILPP doesn't run there — check the editor console after a refresh too.
