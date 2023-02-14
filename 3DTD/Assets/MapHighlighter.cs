using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapHighlighter : MonoBehaviour
{
    [SerializeField] private GameObject[] mapObjects;
    [SerializeField] private GameObject lastMapObject;
    [SerializeField] private Material origMaterial;
    [SerializeField] private Material highlightMaterial;
    public bool highlight = true;

    private static MapHighlighter instance;
    public static MapHighlighter Instance { get { return instance; } }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
    }
    private void Start()
    {
        foreach (GameObject go in mapObjects)
        {
            //go.GetComponent<Renderer>().material = highlightMaterial;
        }
        
        StartCoroutine(highlightTile());
    }

    private IEnumerator highlightTile()
    {
        foreach (GameObject go in mapObjects)
        {
            if (highlight)
            {
                yield return new WaitForSeconds(0.075f);
                if (lastMapObject != null)
                    lastMapObject.GetComponentInChildren<MeshRenderer>().material = origMaterial;

                go.GetComponentInChildren<MeshRenderer>().material = highlightMaterial;
                lastMapObject = go;
            }
            else
            {
                break;
            }
        }
        lastMapObject.GetComponentInChildren<MeshRenderer>().material = origMaterial;

        if (highlight)
            StartCoroutine(highlightTile());
    }
}
