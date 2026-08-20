using System.Collections.Generic;
using UnityEngine;

public class EffectDirection : MonoBehaviour
{
    [SerializeField]
    private Transform _mirrorRoot;

    [SerializeField]
    private Transform _rotationRoot;

    [SerializeField]
    private float _rotationOffset;

    public Vector2 Direction { get; private set; } = Vector2.right;

    private readonly Dictionary<ParticleSystem, Vector3>
        _originalStartRotations = new();

    private void Awake()
    {
        CacheParticleStartRotations();
    }

    public void SetDirection(Vector2 direction)
    {
        Direction =
            direction.sqrMagnitude > 0f
                ? direction.normalized
                : Vector2.right;

        ApplyMirror();
        ApplyRotation();
        ApplyParticleStartRotation();
    }

    private void ApplyMirror()
    {
        if (_mirrorRoot == null)
            return;

        bool isLeft = Direction.x < 0f;

        _mirrorRoot.localRotation =
            Quaternion.Euler(
                0f,
                isLeft ? 180f : 0f,
                0f
            );
    }

    private void ApplyRotation()
    {
        if (_rotationRoot == null)
            return;

        float angle =
            Mathf.Asin(Direction.y)
            * Mathf.Rad2Deg;

        _rotationRoot.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                angle + _rotationOffset
            );
    }

    private void CacheParticleStartRotations()
    {
        ParticleSystem[] particles =
            GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particle in particles)
        {
            var main = particle.main;

            if (!main.startRotation3D)
                continue;

            _originalStartRotations[particle] =
                new Vector3(
                    main.startRotationX.constant,
                    main.startRotationY.constant,
                    main.startRotationZ.constant
                );
        }
    }

    private void ApplyParticleStartRotation()
    {
        bool isLeft = Direction.x < 0f;

        foreach (var pair in _originalStartRotations)
        {
            ParticleSystem particle = pair.Key;

            if (particle == null)
                continue;

            Vector3 original = pair.Value;

            float sign = isLeft ? -1f : 1f;

            var main = particle.main;

            main.startRotationX = original.x * sign;
            main.startRotationY = original.y * sign;
            main.startRotationZ = original.z * sign;
        }
    }
}