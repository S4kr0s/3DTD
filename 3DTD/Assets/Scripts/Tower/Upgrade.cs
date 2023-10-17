using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public abstract class Upgrade : MonoBehaviour, IUpgrade
{
    public abstract void ApplyUpgrade(StatsManager statsManager);
    public abstract void RemoveUpgrade(StatsManager statsManager);
}

public interface IUpgrade
{
    public void ApplyUpgrade(StatsManager statsManager);
    public void RemoveUpgrade(StatsManager statsManager);
}