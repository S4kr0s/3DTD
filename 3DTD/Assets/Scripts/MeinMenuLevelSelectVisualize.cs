using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeinMenuLevelSelectVisualize : MonoBehaviour
{
    [SerializeField] private CanvasGroup parentCanvasGroup;
    [SerializeField] private GameObject childObject;

    private void Update()
    {
        if (parentCanvasGroup.alpha == 0)
            childObject.SetActive(false);
        else
            childObject.SetActive(true);
    }
}
