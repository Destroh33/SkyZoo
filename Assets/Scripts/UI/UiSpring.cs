using UnityEngine;

public static class UiSpring
{
    public static void Step(ref float value, ref float velocity, float target,
                            float stiffness, float damping, float dt)
    {
        velocity += (target - value) * stiffness * dt;
        velocity *= Mathf.Exp(-damping * dt);
        value    += velocity * dt;
    }

    public static float EaseOutBack(float x, float overshoot = 1.7f)
    {
        float c3 = overshoot + 1f;
        float p  = x - 1f;
        return 1f + c3 * p * p * p + overshoot * p * p;
    }

    public static float EaseOutCubic(float x)
    {
        float p = 1f - x;
        return 1f - p * p * p;
    }
}
