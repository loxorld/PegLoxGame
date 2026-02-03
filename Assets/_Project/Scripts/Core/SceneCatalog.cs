using UnityEngine;

[CreateAssetMenu(menuName = "PegLox/Scene Catalog", fileName = "SceneCatalog")]
public class SceneCatalog : ScriptableObject
{
    [Header("Scene Names")]
    [SerializeField] private string bootScene = "BootScene";
    [SerializeField] private string mapScene = "MapScene";
    [SerializeField] private string combatScene = "SampleScene";

    public string BootScene => bootScene;
    public string MapScene => mapScene;
    public string CombatScene => combatScene;

    private static SceneCatalog cached;

    public static SceneCatalog Load()
    {
        if (cached != null) return cached;

        cached = Resources.Load<SceneCatalog>("SceneCatalog");
        if (cached == null)
        {
            cached = CreateInstance<SceneCatalog>();
            cached.bootScene = "BootScene";
            cached.mapScene = "MapScene";
            cached.combatScene = "SampleScene";
            Debug.LogWarning("[SceneCatalog] No SceneCatalog asset found in Resources. Using defaults.");
        }

        return cached;
    }
}