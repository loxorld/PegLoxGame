using UnityEngine;
using UnityEngine.InputSystem;

public class RewardInput : MonoBehaviour
{
    [SerializeField] private RewardManager rewards;

    private void Update()
    {
        if (Keyboard.current == null || rewards == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) rewards.ChooseOrb(1);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) rewards.ChooseOrb(2);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) rewards.ChooseOrb(3);
    }
}