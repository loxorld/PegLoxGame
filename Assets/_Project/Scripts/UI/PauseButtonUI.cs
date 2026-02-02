using UnityEngine;
using UnityEngine.UI;

public class PauseButtonUI : MonoBehaviour
{
    [SerializeField] private GameFlowManager flow;
    [SerializeField] private Button button;

    private void ResolveFlow()
    {
        if (GameFlowManager.Instance != null && flow != GameFlowManager.Instance)
        {
            flow = GameFlowManager.Instance;
            return;
        }

        if (flow == null)
            flow = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);
    }

    private void Awake()
    {
        ResolveFlow();
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClickPause);
    }

    private void OnEnable()
    {
        ResolveFlow();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClickPause);
    }

    private void OnClickPause()
    {
        ResolveFlow();
        flow?.Pause();
    }
}