using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Liquid : MonoBehaviour
{
    public enum UpdateMode { Normal, UnscaledTime }
    public UpdateMode updateMode;

    [SerializeField] float MaxWobble = 0.03f;
    [SerializeField] float WobbleSpeedMove = 1f;
    [SerializeField] float fillAmount = 0.5f;
    [SerializeField] float Recovery = 1f;
    [SerializeField] float Thickness = 1f;
    [Range(0, 1)] public float CompensateShapeAmount;
    [SerializeField] Mesh mesh;
    [SerializeField] Renderer rend;

    // MPB + property IDs
    MaterialPropertyBlock _mpb;
    static readonly int ID_WobbleX = Shader.PropertyToID("_WobbleX");
    static readonly int ID_WobbleZ = Shader.PropertyToID("_WobbleZ");
    static readonly int ID_FillAmount = Shader.PropertyToID("_FillAmount");

    Vector3 pos, lastPos, velocity, angularVelocity, comp;
    Quaternion lastRot;
    float wobbleAmountX, wobbleAmountZ, wobbleAmountToAddX, wobbleAmountToAddZ;
    float pulse, sinewave, time = 0.5f;

    void Start() => GetMeshAndRend();

    void OnValidate() => GetMeshAndRend();

    void GetMeshAndRend()
    {
        if (!mesh)
        {
            var mf = GetComponent<MeshFilter>();
            if (mf) mesh = mf.sharedMesh;
        }
        if (!rend) rend = GetComponent<Renderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        // Ensure MPB exists on this renderer so scene reloads keep per-instance overrides
        if (rend) { rend.GetPropertyBlock(_mpb); rend.SetPropertyBlock(_mpb); }
    }

    void Update()
    {
        if (!mesh || !rend) return;

        float deltaTime = 0f;
        switch (updateMode)
        {
            case UpdateMode.Normal: deltaTime = Application.isPlaying ? Time.deltaTime : 0f; break;
            case UpdateMode.UnscaledTime: deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 0f; break;
        }

        time += deltaTime;

        if (deltaTime != 0f)
        {
            // decay wobble
            wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0, deltaTime * Recovery);
            wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0, deltaTime * Recovery);

            // sine wobble
            pulse = 2f * Mathf.PI * WobbleSpeedMove;
            sinewave = Mathf.Lerp(
                sinewave,
                Mathf.Sin(pulse * time),
                deltaTime * Mathf.Clamp(velocity.magnitude + angularVelocity.magnitude, Thickness, 10f)
            );

            wobbleAmountX = wobbleAmountToAddX * sinewave;
            wobbleAmountZ = wobbleAmountToAddZ * sinewave;

            // velocities
            velocity = (lastPos - transform.position) / deltaTime;
            angularVelocity = GetAngularVelocity(lastRot, transform.rotation);

            // add motion to wobble
            wobbleAmountToAddX += Mathf.Clamp((velocity.x + (velocity.y * 0.2f) + angularVelocity.z + angularVelocity.y) * MaxWobble, -MaxWobble, MaxWobble);
            wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (velocity.y * 0.2f) + angularVelocity.x + angularVelocity.y) * MaxWobble, -MaxWobble, MaxWobble);
        }

        // ---- per-renderer property updates via MPB (not sharedMaterial) ----
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(_mpb);

        _mpb.SetFloat(ID_WobbleX, wobbleAmountX);
        _mpb.SetFloat(ID_WobbleZ, wobbleAmountZ);

        UpdatePos(deltaTime); // writes pos

        _mpb.SetVector(ID_FillAmount, pos);

        rend.SetPropertyBlock(_mpb);

        // keep last state
        lastPos = transform.position;
        lastRot = transform.rotation;
    }

    void UpdatePos(float deltaTime)
    {
        Vector3 worldCenter = transform.TransformPoint(mesh.bounds.center);
        if (CompensateShapeAmount > 0f)
        {
            comp = (deltaTime != 0f)
                ? Vector3.Lerp(comp, worldCenter - new Vector3(0, GetLowestPoint(), 0), deltaTime * 10f)
                : (worldCenter - new Vector3(0, GetLowestPoint(), 0));

            pos = worldCenter - transform.position - new Vector3(0, fillAmount - (comp.y * CompensateShapeAmount), 0);
        }
        else
        {
            pos = worldCenter - transform.position - new Vector3(0, fillAmount, 0);
        }
    }

    // https://forum.unity.com/threads/manually-calculate-angular-velocity-of-gameobject.289462/#post-4302796
    Vector3 GetAngularVelocity(Quaternion foreLastFrameRotation, Quaternion lastFrameRotation)
    {
        var q = lastFrameRotation * Quaternion.Inverse(foreLastFrameRotation);

        if (Mathf.Abs(q.w) > 1023.5f / 1024.0f) return Vector3.zero;

        float angle = (q.w < 0.0f) ? Mathf.Acos(-q.w) : Mathf.Acos(q.w);
        float gain = ((q.w < 0.0f) ? -2.0f : 2.0f) * angle / (Mathf.Sin(angle) * (Application.isPlaying ? Time.deltaTime : 0.0167f));

        Vector3 av = new Vector3(q.x * gain, q.y * gain, q.z * gain);
        if (float.IsNaN(av.z)) av = Vector3.zero;
        return av;
    }

    float GetLowestPoint()
    {
        float lowestY = float.MaxValue;
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            float y = transform.TransformPoint(vertices[i]).y;
            if (y < lowestY) lowestY = y;
        }
        return lowestY;
    }
}
