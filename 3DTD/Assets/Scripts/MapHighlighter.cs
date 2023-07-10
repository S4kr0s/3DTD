using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapHighlighter : MonoBehaviour
{
    [SerializeField] private GameObject[] mapObjects;
    //[SerializeField] private GameObject lastMapObject;
    [SerializeField] private Material origMaterial;
    [SerializeField] private Material highlightMaterial;
    public bool highlight = true;

    private static MapHighlighter instance;
    public static MapHighlighter Instance { get { return instance; } }

    private Vector3 cachedScale;

    [SerializeField] private float highlightDelay = 0.075f;
    [SerializeField] private int highlightTimes = 1;

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        foreach (GameObject go in mapObjects)
        {
            //go.GetComponent<Renderer>().material = highlightMaterial;
        }

        if (mapObjects != null)
            cachedScale = mapObjects[0].transform.localScale;

        StartCoroutine(highlightMap(highlightTimes));
    }

    private IEnumerator highlightMap(int times)
    {
        for (int i = 0; i < times; i++)
        {
            StartCoroutine(highlightTile());
            yield return new WaitForSeconds(highlightDelay);
        }
    }

    private IEnumerator highlightTile()
    {
        GameObject lastMapObject = null;

        foreach (GameObject go in mapObjects)
        {
            if (highlight)
            {
                yield return new WaitForSeconds(highlightDelay);
                if (lastMapObject != null)
                {
                    lastMapObject.GetComponentInChildren<MeshRenderer>().material = origMaterial;
                    lastMapObject.transform.localScale = cachedScale;
                }

                go.transform.localScale = cachedScale * 1.01f;
                go.GetComponentInChildren<MeshRenderer>().material = highlightMaterial;
                lastMapObject = go;
            }
            else
            {
                break;
            }
        }
        lastMapObject.GetComponentInChildren<MeshRenderer>().material = origMaterial;
        lastMapObject.transform.localScale = Vector3.one;

        if (highlight)
            StartCoroutine(highlightTile());
    }
}
