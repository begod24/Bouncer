using Bouncer.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bouncer.EditorTools
{
    static class BallLabMenu
    {
        const string YardScene = "Assets/_Project/Scenes/Yard.unity";

        [MenuItem("Bouncer/Площадка мячей ▶", priority = 0)]
        static void Open()
        {
            if (EditorApplication.isPlaying)
            {
                if (GameSession.Instance != null)
                    GameSession.Instance.StartBallLab();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(YardScene);
            BallLab.RequestFromEditor();
            EditorApplication.EnterPlaymode();
            Debug.Log("Площадка мячей: двор без волн. F1 — панель, 1–9 — мячи, F2 — мишени.");
        }
    }
}
