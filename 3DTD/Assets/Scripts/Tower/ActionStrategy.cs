using UnityEngine;

public abstract class ActionStrategy : MonoBehaviour
{
    public abstract void SetupActionStrategy(Tower tower);
    public abstract void ExecuteAction();
    public abstract bool CanShoot(GameObject enemy);
}
