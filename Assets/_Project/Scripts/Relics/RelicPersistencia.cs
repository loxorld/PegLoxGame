using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Relics/Persistencia", fileName = "Relic_Persistencia")]
public class RelicPersistencia : ShotEffectBase, IPreviewBounceModifier
{
    [SerializeField, Min(0)] private int bonusPreviewBounces = 1;

    public int BonusPreviewBounces => bonusPreviewBounces;
}