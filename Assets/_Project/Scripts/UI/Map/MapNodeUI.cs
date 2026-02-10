using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapNodeUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image nodeIconImage;
    [SerializeField] private Sprite combatSprite;
    [SerializeField] private Sprite eventSprite;
    [SerializeField] private Sprite shopSprite;
    [SerializeField] private Sprite bossSprite;

    private MapNodeData nodeData;

    public void Setup(MapNodeData data, System.Action<MapNodeData> callback)
    {
        nodeData = data;
        label.text = data.title;
        Sprite selectedSprite = data.nodeType switch
        {
            NodeType.Combat => combatSprite,
            NodeType.Event => eventSprite,
            NodeType.Shop => shopSprite,
            NodeType.Boss => bossSprite,
            _ => null
        };

        if (nodeIconImage != null)
        {
            nodeIconImage.sprite = selectedSprite;
            nodeIconImage.preserveAspect = true;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => callback?.Invoke(nodeData));
    }
}