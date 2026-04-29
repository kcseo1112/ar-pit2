using UnityEngine;
using System.Collections.Generic;

public class ClothesRetarget : MonoBehaviour
{
    [Header("Avatar Bones")]
    public Transform chest;
    public Transform rightUpperArm;
    public Transform rightLowerArm;
    public Transform leftUpperArm;
    public Transform leftLowerArm;

    [Header("Clothes Pivots")]
    public Transform bodyPivot;
    public Transform shoulder_R;
    public Transform elbow_R;
    public Transform shoulder_L;
    public Transform elbow_L;

    [Header("Body Fit Scale")]
    public Transform clothesRoot;
    public bool enableBodyFitScale = true;
    public bool autoCalibrateOnFirstPose = true;
    public float manualReferenceShoulderWidth = -1f;
    public float minScale = 0.5f;
    public float maxScale = 2.5f;
    public float scaleSmooth = 12f;

    [Header("Rotation Offsets")]
    public Vector3 chestOffsetEuler = Vector3.zero;
    public Vector3 rightShoulderOffsetEuler = Vector3.zero;
    public Vector3 rightElbowOffsetEuler = Vector3.zero;
    public Vector3 leftShoulderOffsetEuler = Vector3.zero;
    public Vector3 leftElbowOffsetEuler = Vector3.zero;

    private readonly Dictionary<Transform, Quaternion> initialLocalRotations = new();
    private Vector3 initialRootScale = Vector3.one;
    private float referenceShoulderWidth = -1f;
    private float targetBodyScale = 1f;

    void Start()
    {
        SaveInitial(bodyPivot);
        SaveInitial(shoulder_R);
        SaveInitial(elbow_R);
        SaveInitial(shoulder_L);
        SaveInitial(elbow_L);

        if (clothesRoot == null)
            clothesRoot = transform;

        initialRootScale = clothesRoot.localScale;
        referenceShoulderWidth = manualReferenceShoulderWidth > 0f ? manualReferenceShoulderWidth : -1f;
    }

    void SaveInitial(Transform t)
    {
        if (t == null) return;
        if (!initialLocalRotations.ContainsKey(t))
            initialLocalRotations[t] = t.localRotation;
    }

    void LateUpdate()
    {
        ApplyBodyFitScale();

        ApplyFollow(bodyPivot, chest, chestOffsetEuler);
        ApplyFollow(shoulder_R, rightUpperArm, rightShoulderOffsetEuler);
        ApplyFollow(elbow_R, rightLowerArm, rightElbowOffsetEuler);
        ApplyFollow(shoulder_L, leftUpperArm, leftShoulderOffsetEuler);
        ApplyFollow(elbow_L, leftLowerArm, leftElbowOffsetEuler);
    }

    public void ApplyBodyFit(Vector3[] joints)
    {
        ApplyBodyFit(joints, -1f);
    }

    public void ApplyBodyFit(Vector3[] joints, float screenShoulderWidth)
    {
        if (!enableBodyFitScale || joints == null || joints.Length < 18)
            return;

        float shoulderWidth = screenShoulderWidth;

        if (shoulderWidth <= 0.0001f)
        {
            Vector3 leftShoulder = joints[16];
            Vector3 rightShoulder = joints[17];
            shoulderWidth = Vector3.Distance(leftShoulder, rightShoulder);
        }

        if (shoulderWidth <= 0.0001f)
            return;

        if (autoCalibrateOnFirstPose && referenceShoulderWidth <= 0f)
            referenceShoulderWidth = shoulderWidth;

        if (referenceShoulderWidth <= 0.0001f)
            return;

        targetBodyScale = Mathf.Clamp(shoulderWidth / referenceShoulderWidth, minScale, maxScale);
    }

    public void RecalibrateBodyFit(Vector3[] joints)
    {
        if (joints == null || joints.Length < 18)
            return;

        Vector3 rightShoulder = joints[16];
        Vector3 leftShoulder = joints[17];
        float shoulderWidth = Vector3.Distance(rightShoulder, leftShoulder);

        if (shoulderWidth > 0.0001f)
            referenceShoulderWidth = shoulderWidth;
    }

    void ApplyBodyFitScale()
    {
        if (!enableBodyFitScale || clothesRoot == null)
            return;

        Vector3 targetScale = initialRootScale * targetBodyScale;
        float t = 1f - Mathf.Exp(-scaleSmooth * Time.deltaTime);
        clothesRoot.localScale = Vector3.Lerp(clothesRoot.localScale, targetScale, t);
    }

    void ApplyFollow(Transform clothesPivot, Transform avatarBone, Vector3 offsetEuler)
    {
        if (clothesPivot == null || avatarBone == null) return;
        if (!initialLocalRotations.ContainsKey(clothesPivot)) return;

        Quaternion offset = Quaternion.Euler(offsetEuler);
        clothesPivot.rotation = avatarBone.rotation * offset;
    }
}
