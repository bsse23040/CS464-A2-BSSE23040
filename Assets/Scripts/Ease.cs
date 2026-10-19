using UnityEngine;

/// <summary>
/// Easing curves. Each one takes t from 0 to 1 and returns 0 at t = 0 and 1 at t = 1.
/// </summary>
public static class Ease
{
    /// <summary>Starts fast, slows to a stop.</summary>
    public static float OutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    /// <summary>Like OutQuad, with a longer, softer slow-down.</summary>
    public static float OutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    /// <summary>Goes a little past 1 (about 1.1) and settles back. Use it with LerpUnclamped, or the overshoot is lost.</summary>
    public static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
