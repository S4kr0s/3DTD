using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingBlock : MonoBehaviour
{
    [SerializeField] private List<AnchorPoint> anchorPoints;

    private void Start()
    {
        //CheckAnchorPointPositions();
    }

    // Removed because when selling the anchorpoints don't return.

    /*
    private void CheckAnchorPointPositions()
    {
        foreach (AnchorPoint anchorPoint in anchorPoints)
        {
            Ray ray = new Ray(transform.position, anchorPoint.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 1f, anchorPoint.LayerMask))
            {
                anchorPoint.gameObject.SetActive(false);
            }
            else
            {
                anchorPoint.gameObject.SetActive(true);
            }
        }
    }
    */
}
