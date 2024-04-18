using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyShape : MonoBehaviour
{
    [SerializeField] private Animation _animation;
    [SerializeField] private Material _material;
    private MaterialPropertyBlock _propBlock;

    private void Awake()
    {
        _animation = GetComponent<Animation>();
        _material = gameObject.GetComponentInChildren<Renderer>().material;
        _propBlock = new MaterialPropertyBlock();
    }

    public void DeathAnimation()
    {
        _animation.Play();
        if (gameObject.activeSelf)
            StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        Renderer renderer = gameObject.GetComponentInChildren<Renderer>();
        float elapsedTime = 0f;
        float duration = _animation.clip.length;
        while (elapsedTime < duration)
        {
            _propBlock.SetFloat("_ShowPercent", 1.0f * (elapsedTime / duration));
            renderer.SetPropertyBlock(_propBlock);
            //gameObject.GetComponentInChildren<Renderer>().material.SetFloat("_ShowPercent", 1.0f * ( elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        Destroy(this.gameObject);
    }
}
