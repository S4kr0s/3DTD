using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyShape : MonoBehaviour
{
    // Late waves pop thousands of layers; cap the effects and recycle them instead of instantiating each one
    private const int MaxActiveDeathEffects = 64;
    private const float MaxGameSpeedForEffects = 2f;

    private static readonly Dictionary<int, Stack<EnemyShape>> pool = new Dictionary<int, Stack<EnemyShape>>();
    private static int activeDeathEffects = 0;

    [SerializeField] private Animation _animation;
    [SerializeField] private Material _material;
    private MaterialPropertyBlock _propBlock;
    private Renderer _renderer;
    private int poolId = -1;
    private bool isPlaying = false;

    private void Awake()
    {
        _animation = GetComponent<Animation>();
        _renderer = gameObject.GetComponentInChildren<Renderer>();
        // sharedMaterial: .material would clone the material for every shape of every enemy
        _material = _renderer != null ? _renderer.sharedMaterial : null;
        _propBlock = new MaterialPropertyBlock();
    }

    public static void SpawnDeathEffect(GameObject source, int id, Vector3 position, Quaternion rotation)
    {
        if (source == null || activeDeathEffects >= MaxActiveDeathEffects || Time.timeScale > MaxGameSpeedForEffects)
            return;

        EnemyShape effect = null;
        if (pool.TryGetValue(id, out Stack<EnemyShape> stack))
        {
            // Pooled effects are destroyed with their scene
            while (effect == null && stack.Count > 0)
                effect = stack.Pop();
        }

        if (effect == null)
        {
            GameObject go = Instantiate(source, position, rotation, null);
            effect = go.GetComponent<EnemyShape>();
            if (effect == null)
            {
                Destroy(go);
                return;
            }
            effect.poolId = id;
        }

        effect.transform.SetPositionAndRotation(position, rotation);
        effect.gameObject.SetActive(true);
        activeDeathEffects++;
        effect.isPlaying = true;
        effect.DeathAnimation();
    }

    public void DeathAnimation()
    {
        _animation.Play();
        if (gameObject.activeSelf)
            StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float elapsedTime = 0f;
        float duration = _animation.clip.length;
        while (elapsedTime < duration)
        {
            _propBlock.SetFloat("_ShowPercent", 1.0f * (elapsedTime / duration));
            _renderer.SetPropertyBlock(_propBlock);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isPlaying)
            activeDeathEffects = Mathf.Max(0, activeDeathEffects - 1);
        isPlaying = false;

        if (poolId < 0)
        {
            Destroy(this.gameObject);
            return;
        }

        gameObject.SetActive(false);
        if (!pool.TryGetValue(poolId, out Stack<EnemyShape> stack))
        {
            stack = new Stack<EnemyShape>();
            pool[poolId] = stack;
        }
        stack.Push(this);
    }

    private void OnDestroy()
    {
        // Scene unload while the effect was playing
        if (isPlaying)
            activeDeathEffects = Mathf.Max(0, activeDeathEffects - 1);
    }
}
