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
            if (ShouldSkipInstantiation(prefab))
                continue;

            Instantiate(prefab);
        }
    }


    private static bool ShouldSkipInstantiation(GameObject prefab)
    {
        if (prefab == null)
            return true;

        AudioManager audioManager = prefab.GetComponent<AudioManager>();
        if (audioManager != null && AudioManager.Instance != null)
            return true;

        return false;
    }

    private void Start()
    {
        if (!loadMainMenuOnStart) return;

        SceneCatalog catalog = SceneCatalog.Load();
        if (!string.IsNullOrWhiteSpace(catalog.MainMenuScene))
            SceneManager.LoadScene(catalog.MainMenuScene, LoadSceneMode.Single);
    }
}