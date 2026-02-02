using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapNodeUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;

    private MapNodeData nodeData;

    public void Setup(MapNodeData data, System.Action<MapNodeData> callback)
    {
        nodeData = data;
        label.text = data.title;
        button.onClick.AddListener(() => callback?.Invoke(nodeData));
    }
}
