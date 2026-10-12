using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Нажми R, чтобы перезапустить текущую сцену. Временный помощник, потом рестартом займётся GameManager.
public class QuickRestart : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            Restart();
    }

    public static void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // Сцена может быть не добавлена в список сборки, в редакторе грузим по пути
        if (scene.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(scene.buildIndex);
    }
}
