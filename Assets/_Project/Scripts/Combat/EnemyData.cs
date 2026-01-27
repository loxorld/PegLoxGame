using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName = "Goblin";
    public int maxHP = 50;
    public int attackDamage = 5;
}
