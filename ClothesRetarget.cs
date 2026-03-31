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

    [Header("Rotation Offsets")]
    public Vector3 chestOffsetEuler = Vector3.zero;
    public Vector3 rightShoulderOffsetEuler = Vector3.zero;
    public Vector3 rightElbowOffsetEuler = Vector3.zero;
    public Vector3 leftShoulderOffsetEuler = Vector3.zero;
    public Vector3 leftElbowOffsetEuler = Vector3.zero;

    private readonly Dictionary<Transform, Quaternion> initialLocalRotations = new();

    void Start()
    {
        SaveInitial(bodyPivot);
        SaveInitial(shoulder_R);
        SaveInitial(elbow_R);
        SaveInitial(shoulder_L);
        SaveInitial(elbow_L);
    }

    void SaveInitial(Transform t)
    {
        if (t == null) return;
        if (!initialLocalRotations.ContainsKey(t))
            initialLocalRotations[t] = t.localRotation;
    }

    void LateUpdate()
    {
        ApplyFollow(bodyPivot, chest, chestOffsetEuler);
        ApplyFollow(shoulder_R, rightUpperArm, rightShoulderOffsetEuler);
        ApplyFollow(elbow_R, rightLowerArm, rightElbowOffsetEuler);
        ApplyFollow(shoulder_L, leftUpperArm, leftShoulderOffsetEuler);
        ApplyFollow(elbow_L, leftLowerArm, leftElbowOffsetEuler);
    }

    void ApplyFollow(Transform clothesPivot, Transform avatarBone, Vector3 offsetEuler)
    {
        if (clothesPivot == null || avatarBone == null) return;
        if (!initialLocalRotations.ContainsKey(clothesPivot)) return;

        Quaternion offset = Quaternion.Euler(offsetEuler);
        clothesPivot.rotation = avatarBone.rotation * offset;
    }
}