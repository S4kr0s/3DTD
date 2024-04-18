using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugScript : MonoBehaviour
{
    [SerializeField] private GameObject shootPoint;

    // Update is called once per frame
    void Update()
    {
        Debug.Log(shootPoint.GetComponent<ShootingPointReference>().IsReferenceEnabled);
    }
}
