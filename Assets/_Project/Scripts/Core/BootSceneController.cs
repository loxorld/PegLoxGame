using UnityEngine;
using UnityEngine.SceneManagement;

public class BootSceneController : MonoBehaviour
{
    [Header("Persistent Managers")]
    [SerializeField] private GameObject[] persistentPrefabs;

    [Header("Flow")]
    [SerializeField] private bool loadMapOnStart = true;

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
        if (!loadMapOnStart) return;

        SceneCatalog catalog = SceneCatalog.Load();
        if (!string.IsNullOrWhiteSpace(catalog.MapScene))
            SceneManager.LoadScene(catalog.MapScene, LoadSceneMode.Single);
    }
}