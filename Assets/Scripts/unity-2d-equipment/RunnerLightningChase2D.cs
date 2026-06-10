using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class RunnerLightningChase2D : MonoBehaviour
{
    public Transform target;
    public float frameRate = 14f;
    public float startDistance = 5.5f;
    public float closestDistance = 2.25f;
    public float approachSeconds = 150f;

    readonly Vector3[] _boltOffsets =
    {
        new Vector3(0f, 0.15f, 0f),
        new Vector3(-0.42f, 1.25f, 0f),
        new Vector3(0.28f, -1.05f, 0f)
    };

    readonly float[] _boltScales = { 3.25f, 2.2f, 1.9f };
    readonly float[] _frameOffsets = { 0f, 0.17f, 0.31f };
    readonly float[] _rotations = { -4f, 11f, -13f };

    SpriteRenderer[] _bolts;
    SpriteRenderer[] _glows;
    Sprite[] _frames;
    float _elapsed;

    public void Initialize(Transform player)
    {
        target = player;
        _frames = Resources.LoadAll<Sprite>("Lightning")
            .OrderBy(sprite => sprite.name)
            .ToArray();

        _bolts = new SpriteRenderer[_boltOffsets.Length];
        _glows = new SpriteRenderer[_boltOffsets.Length];
        for (int i = 0; i < _boltOffsets.Length; i++)
        {
            var glowObject = new GameObject("LightningGlow" + i);
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localPosition = _boltOffsets[i];
            glowObject.transform.localRotation = Quaternion.Euler(0f, 0f, _rotations[i]);
            glowObject.transform.localScale = Vector3.one * (_boltScales[i] * 1.28f);
            _glows[i] = glowObject.AddComponent<SpriteRenderer>();
            _glows[i].sortingOrder = 7;

            var boltObject = new GameObject("LightningBolt" + i);
            boltObject.transform.SetParent(transform, false);
            boltObject.transform.localPosition = _boltOffsets[i];
            boltObject.transform.localRotation = Quaternion.Euler(0f, 0f, _rotations[i]);
            boltObject.transform.localScale = Vector3.one * _boltScales[i];
            _bolts[i] = boltObject.AddComponent<SpriteRenderer>();
            _bolts[i].sortingOrder = 8 + i;
        }

        gameObject.SetActive(false);
    }

    public void SetChasing(bool chasing)
    {
        gameObject.SetActive(chasing && target != null && _frames != null && _frames.Length > 0);
    }

    public void ResetChase()
    {
        _elapsed = 0f;
    }

    void Update()
    {
        if (target == null || _frames == null || _frames.Length == 0)
            return;

        _elapsed += Time.deltaTime;
        for (int i = 0; i < _bolts.Length; i++)
        {
            int frame = Mathf.FloorToInt((_elapsed + _frameOffsets[i]) * frameRate) % _frames.Length;
            float pulse = 0.82f + Mathf.Sin(_elapsed * (13f + i * 2.3f) + i) * 0.18f;

            _bolts[i].sprite = _frames[frame];
            _bolts[i].color = i == 0
                ? new Color(1f, 0.98f, 0.68f, 0.98f)
                : new Color(0.72f, 0.9f, 1f, 0.78f * pulse);

            _glows[i].sprite = _frames[frame];
            _glows[i].color = new Color(0.22f, 0.62f, 1f, (i == 0 ? 0.3f : 0.18f) * pulse);
        }

        float approach = Mathf.Clamp01(_elapsed / Mathf.Max(1f, approachSeconds));
        float distance = Mathf.Lerp(startDistance, closestDistance, approach);
        float flicker = Mathf.Sin(_elapsed * 8f) * 0.1f;
        float verticalSurge = Mathf.Sin(_elapsed * 2.4f) * 0.16f;
        transform.position = new Vector3(
            target.position.x - distance + flicker,
            target.position.y + 0.35f + verticalSurge,
            target.position.z);
    }
}
