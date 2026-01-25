using TMPro;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI pegsHitText;
    [SerializeField] private TextMeshProUGUI playerHpText;

    [SerializeField] private PlayerStats player;

    private void Update()
    {
        if (ShotManager.Instance != null)
        {
            pegsHitText.text =
                $"Orb: {ShotManager.Instance.HudOrbName} | Hits: {ShotManager.Instance.HudTotalHits} (N:{ShotManager.Instance.HudNormalHits} C:{ShotManager.Instance.HudCriticalHits}) x{ShotManager.Instance.HudMultiplier}";
        }


        if (player != null)
        {
            playerHpText.text = $"HP: {player.CurrentHP}/{player.MaxHP}";
        }
    }
}
