using UnityEngine;

/// <summary>
/// Shared left/right facing for movement, dodge, and attacks.
/// </summary>
public static class PlayerFacing2D
{
    public static float GetFacingSign(Transform root, SpriteRenderer visual = null)
    {
        if (root != null)
        {
            float sx = root.lossyScale.x;
            if (sx < -0.001f)
                return -1f;
        }

        if (visual != null && visual.flipX)
            return -1f;

        if (root != null)
        {
            float sx = root.lossyScale.x;
            if (!Mathf.Approximately(sx, 0f))
                return Mathf.Sign(sx);
        }

        return 1f;
    }

    public static void ApplyHorizontalFacing(Transform root, SpriteRenderer visual, float directionX)
    {
        if (Mathf.Approximately(directionX, 0f))
            return;

        if (visual != null)
        {
            visual.flipX = directionX < 0f;
            var s = root.localScale;
            s.x = Mathf.Abs(s.x);
            root.localScale = s;
            return;
        }

        var scale = root.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(directionX);
        root.localScale = scale;
    }
}
