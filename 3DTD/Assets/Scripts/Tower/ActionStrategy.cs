using UnityEngine;

public abstract class ActionStrategy : MonoBehaviour
{
    public abstract void SetupActionStrategy(Tower tower);
    public abstract void ExecuteAction();
    public abstract bool CanShoot(GameObject enemy);

    // The strategy's fire timer, if it has one; Tower.SetActionStrategy carries it over to a swapped-in strategy
    public virtual FireCycle Cycle => null;
}
