using UnityEngine;

[CreateAssetMenu(fileName = "BaseBossData", menuName = "TowerDefense/BossData", order = 0)]
public class BossData : EnemyData
{
    [SerializeField] private bool isImmuneToCC;

    public bool IsImmuneToCC => isImmuneToCC;
}
