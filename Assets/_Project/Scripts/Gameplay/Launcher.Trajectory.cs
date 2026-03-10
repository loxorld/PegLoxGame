using System.Collections.Generic;
using UnityEngine;

public partial class Launcher
{
    private Vector2 ComputeLaunchVelocity(Vector2 rawDirectionWorld, Vector2 currentScreenPos)
    {
        return TryBuildLaunchComputation(rawDirectionWorld, currentScreenPos, out LaunchComputationData launchData)
            ? launchData.Velocity
            : Vector2.zero;
    }

    private float ComputePower01(Vector2 rawDirectionWorld, Vector2 currentScreenPos)
    {
        return TryBuildLaunchComputation(rawDirectionWorld, currentScreenPos, out LaunchComputationData launchData)
            ? launchData.Power01
            : 0f;
    }

    private bool TryBuildLaunchComputation(Vector2 rawDirectionWorld, Vector2 currentScreenPos, out LaunchComputationData launchData)
    {
        launchData = default;
        if (!TryComputePowerData(rawDirectionWorld, currentScreenPos, out Vector2 directionWorld, out _, out float power01))
            return false;

        float speed = Mathf.Lerp(effectiveMinLaunchSpeed, effectiveMaxLaunchSpeed, power01);
        launchData = new LaunchComputationData
        {
            Velocity = directionWorld.normalized * speed,
            Power01 = power01
        };

        return true;
    }

    private bool TryComputePowerData(
        Vector2 rawDirectionWorld,
        Vector2 currentScreenPos,
        out Vector2 directionWorld,
        out float drag01,
        out float power01)
    {
        directionWorld = Vector2.ClampMagnitude(rawDirectionWorld, maxAimMagnitude);
        if (directionWorld.sqrMagnitude < 0.0001f)
        {
            drag01 = 0f;
            power01 = 0f;
            return false;
        }

        if (useScreenSpaceDragForPower)
        {
            float pixels = Vector2.Distance(dragStartScreen, currentScreenPos);
            if (effectiveUseDpiNormalization)
            {
                float dpi = Screen.dpi;
                if (dpi > 0f && !float.IsNaN(dpi) && !float.IsInfinity(dpi))
                    pixels *= referenceDpi / dpi;
            }

            drag01 = Mathf.Clamp01(pixels / effectiveMaxDragPixels);
        }
        else
        {
            drag01 = Mathf.InverseLerp(0f, maxAimMagnitude, directionWorld.magnitude);
        }

        power01 = effectivePowerCurve != null ? effectivePowerCurve.Evaluate(drag01) : drag01;
        if (clampCurveOutput01)
            power01 = Mathf.Clamp01(power01);

        return true;
    }

    private int GetPreviewBounceBonus()
    {
        if (relicManager == null)
            return 0;

        int bonus = 0;
        IReadOnlyList<ShotEffectBase> relics = relicManager.ActiveRelics;
        for (int i = 0; i < relics.Count; i++)
        {
            if (relics[i] is IPreviewBounceModifier preview)
                bonus += Mathf.Max(0, preview.BonusPreviewBounces);
        }

        return bonus;
    }

    private void SetTrajectoryVisible(bool visible)
    {
        if (trajectoryLine != null)
            trajectoryLine.enabled = visible;
    }

    private void ClearTrajectory()
    {
        if (trajectoryLine != null)
            trajectoryLine.positionCount = 0;
    }

    private void UpdateTrajectoryPreview(Vector2 directionWorld, Vector2 currentScreenPos)
    {
        if (trajectoryLine == null || launchPoint == null)
            return;

        if (ShotManager.Instance != null && (ShotManager.Instance.IsGameOver || ShotManager.Instance.ShotInProgress))
        {
            ClearTrajectory();
            return;
        }

        if (!TryBuildLaunchComputation(directionWorld, currentScreenPos, out LaunchComputationData launchData))
        {
            ClearTrajectory();
            return;
        }

        Vector2 velocity = launchData.Velocity;
        if (velocity.sqrMagnitude < 0.0001f)
        {
            ClearTrajectory();
            return;
        }

        if (velocity.magnitude > maxPreviewSpeed)
            velocity = velocity.normalized * maxPreviewSpeed;

        PreviewPhysicsSettings physicsSettings = GetPreviewPhysicsSettings();
        Vector2 pos = launchPoint.position;
        float radius = physicsSettings.Radius;

        trajectoryPoints.Clear();
        trajectoryPoints.Add(pos);

        ApplyTrajectoryPowerStyling(launchData.Power01);
        float minLen = 0.8f;
        float maxPreviewDistance = Mathf.Lerp(minLen, maxDistancePerSegment, launchData.Power01);

        int previewBounces = Mathf.Max(0, maxBounces + GetPreviewBounceBonus());
        int bounces = 0;
        int steps = Mathf.Max(1, previewSteps);
        int minSteps = Mathf.Max(1, Mathf.FloorToInt(steps * 0.25f));
        steps = Mathf.RoundToInt(Mathf.Lerp(minSteps, steps, launchData.Power01));
        float timeStep = Mathf.Max(0.001f, previewTimeStep);
        Vector2 gravity = Physics2D.gravity * physicsSettings.GravityScale;
        float remainingDistance = maxPreviewDistance;
        float surfaceSkin = Mathf.Max(0.005f, Physics2D.defaultContactOffset);

        for (int step = 0; step < steps; step++)
        {
            velocity += gravity * timeStep;
            if (physicsSettings.LinearDrag > 0f)
                velocity *= 1f / (1f + physicsSettings.LinearDrag * timeStep);

            Vector2 displacement = velocity * timeStep;
            float distance = displacement.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                trajectoryPoints.Add(pos);
                continue;
            }

            if (distance > remainingDistance)
            {
                displacement = displacement.normalized * remainingDistance;
                distance = remainingDistance;
            }

            Vector2 direction = displacement / distance;
            RaycastHit2D hit = Physics2D.CircleCast(pos, radius, direction, distance, collisionMask);

            if (hit.collider == null)
            {
                pos += displacement;
                trajectoryPoints.Add(pos);
                remainingDistance -= distance;
                if (remainingDistance <= Mathf.Epsilon)
                    break;

                continue;
            }

            Vector2 hitSimulationPosition = hit.centroid;
            Vector2 hitDisplayPosition = hit.point;
            trajectoryPoints.Add(hitDisplayPosition);

            if (bounces >= previewBounces)
                break;

            bounces++;
            float surfaceBounciness = BallPhysicsUtility.GetColliderBounciness(hit.collider);
            velocity = BallPhysicsUtility.CalculateBounceVelocity(
                velocity,
                hit.normal,
                physicsSettings.BallBounciness,
                surfaceBounciness
            );

            if (velocity.sqrMagnitude < 0.0001f)
                break;

            pos = hitSimulationPosition + hit.normal * surfaceSkin;
            remainingDistance = bounceTailLength;
        }

        ApplyLine(trajectoryPoints);
    }

    private void ApplyTrajectoryPowerStyling(float power01)
    {
        if (trajectoryLine == null)
            return;

        if (trajectoryPowerGradient != null)
        {
            Color powerColor = trajectoryPowerGradient.Evaluate(power01);
            ApplySolidGradient(powerColor);
            trajectoryLine.colorGradient = powerPreviewGradient;
        }
        else if (baseTrajectoryGradient != null)
        {
            trajectoryLine.colorGradient = baseTrajectoryGradient;
        }

        if (trajectoryWidthByPower != null)
        {
            float widthMultiplier = trajectoryWidthByPower.Evaluate(power01);
            trajectoryLine.widthMultiplier = baseTrajectoryWidthMultiplier * widthMultiplier;
        }
        else if (baseTrajectoryWidthMultiplier > 0f)
        {
            trajectoryLine.widthMultiplier = baseTrajectoryWidthMultiplier;
        }
    }

    private void ApplySolidGradient(Color color)
    {
        powerGradientColorKeys[0] = new GradientColorKey(color, 0f);
        powerGradientColorKeys[1] = new GradientColorKey(color, 1f);
        powerGradientAlphaKeys[0] = new GradientAlphaKey(color.a, 0f);
        powerGradientAlphaKeys[1] = new GradientAlphaKey(color.a, 1f);
        powerPreviewGradient.SetKeys(powerGradientColorKeys, powerGradientAlphaKeys);
    }

    private void ApplyLine(List<Vector3> points)
    {
        trajectoryLine.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            trajectoryLine.SetPosition(i, points[i]);
    }

    private void ConfigureTrajectoryLine()
    {
        if (trajectoryLine == null)
            return;

        trajectoryLine.numCornerVertices = previewCornerVertices;
        trajectoryLine.numCapVertices = previewCapVertices;
        trajectoryLine.useWorldSpace = true;
        trajectoryLine.loop = false;
        baseTrajectoryGradient = trajectoryLine.colorGradient;
        baseTrajectoryWidthMultiplier = trajectoryLine.widthMultiplier;
        EnsurePowerStylingDefaults();
    }

    private void EnsurePowerStylingDefaults()
    {
        if (!autoConfigurePowerStyling)
            return;

        if (trajectoryLine != null && trajectoryPowerGradient == null)
        {
            Gradient defaultGradient = new Gradient();
            Color baseColor = trajectoryLine.startColor;
            Color lowColor = new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, baseColor.a * 0.75f);
            GradientColorKey[] colorKeys =
            {
                new GradientColorKey(lowColor, 0f),
                new GradientColorKey(baseColor, 1f)
            };
            GradientAlphaKey[] alphaKeys =
            {
                new GradientAlphaKey(lowColor.a, 0f),
                new GradientAlphaKey(baseColor.a, 1f)
            };
            defaultGradient.SetKeys(colorKeys, alphaKeys);
            trajectoryPowerGradient = defaultGradient;
        }

        if (trajectoryWidthByPower == null)
        {
            trajectoryWidthByPower = new AnimationCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(1f, 1.2f)
            );
        }
    }

    private float GetBallWorldRadius()
    {
        if (ballPrefab == null)
            return previewBallRadiusFallback;

        CircleCollider2D circle = ballPrefab.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Vector3 localScale = ballPrefab.transform.localScale;
            float effectiveScale = Mathf.Max(Mathf.Abs(localScale.x), Mathf.Abs(localScale.y));
            return circle.radius * effectiveScale;
        }

        Collider2D collider = ballPrefab.GetComponent<Collider2D>();
        if (collider != null)
        {
            Debug.LogWarning(
                $"Launcher: Ball prefab '{ballPrefab.name}' uses {collider.GetType().Name}. " +
                "Using fallback radius to avoid incorrect trajectory preview."
            );
        }

        return previewBallRadiusFallback;
    }
}
