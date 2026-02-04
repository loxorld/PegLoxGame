using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class OrbInstance
{
    [field: SerializeField] public OrbData BaseData { get; private set; }
    [field: SerializeField, Min(1)] public int Level { get; private set; } = 1;
    [field: SerializeField, Min(0)] public int DamagePerHit { get; private set; }

    public string OrbName => BaseData != null ? BaseData.orbName : "Orb";
    public Sprite Icon => BaseData != null ? BaseData.icon : null;
    public string Description => BaseData != null ? BaseData.description : "";
    public Color Color => BaseData != null ? BaseData.color : Color.white;
    public float Bounciness => BaseData != null ? BaseData.bounciness : 0.9f;
    public float LinearDrag => BaseData != null ? BaseData.linearDrag : 0f;
    public IReadOnlyList<ShotEffectBase> OrbEffects => BaseData != null ? BaseData.orbEffects : null;

    public OrbInstance(OrbData baseData, int level = 1)
    {
        BaseData = baseData;
        Level = Mathf.Max(1, level);
        RecalculateDamage();
    }

    public void SetLevel(int level)
    {
        Level = Mathf.Max(1, level);
        RecalculateDamage();
    }

    public void RecalculateDamage()
    {
        if (BaseData == null)
        {
            DamagePerHit = 0;
            return;
        }

        DamagePerHit = BaseData.damagePerHit + Mathf.Max(0, Level - 1);
    }
}