using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShootingPointReference : MonoBehaviour
{
    [SerializeField] private GameObject shootingPointReference;
    public bool IsReferenceEnabled { get { return shootingPointReference.activeSelf; } }
}
