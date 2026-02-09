using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public class BootSceneController : MonoBehaviour
{
    [Header("Persistent Managers")]
    [SerializeField] private GameObject[] persistentPrefabs;

    [Header("Flow")]
    [FormerlySerializedAs("loadMapOnStart")]
    [SerializeField] private bool loadMainMenuOnStart = true;

    private void Awake()
    {
        if (persistentPrefabs == null) return;

        for (int i = 0; i < persistentPrefabs.Length; i++)
        {
            var prefab = persistentPrefabs[i];
            if (prefab == null) continue;

            Instantiate(prefab);
        }
    }

    private void Start()
    {
        if (!loadMainMenuOnStart) return;

        SceneCatalog catalog = SceneCatalog.Load();
        if (!string.IsNullOrWhiteSpace(catalog.MainMenuScene))
            SceneManager.LoadScene(catalog.MainMenuScene, LoadSceneMode.Single);
    }
}