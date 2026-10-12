using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// R = перезапустить бой, Esc = в главное меню.
public class QuickRestart : MonoBehaviour
{
    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.rKey.wasPressedThisFrame) Restart();
        if (kb.escapeKey.wasPressedThisFrame) ToMenu();
    }

    // Esc = в главное меню (если сцена MainMenu добавлена в список сцен сборки)
    public static void ToMenu()
    {
        if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            SceneManager.LoadScene("MainMenu");
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
