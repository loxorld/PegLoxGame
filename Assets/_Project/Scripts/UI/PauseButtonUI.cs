using UnityEngine;
using UnityEngine.UI;

public class PauseButtonUI : MonoBehaviour
{
    [SerializeField] private GameFlowManager flow;
    [SerializeField] private Button button;

    private void Awake()
    {
        if (flow == null)
            flow = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClickPause);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClickPause);
    }

    private void OnClickPause()
    {
        flow?.Pause();
    }
}
