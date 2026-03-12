using UnityEngine;
using System.Collections.Generic;

public class AvatarRetarget : MonoBehaviour
{
    [Header("Body")]
    public Transform hips;
    public Transform spine;
    public Transform neck;
    public Transform head;

    [Header("Left Arm")]
    public Transform leftUpperArm;
    public Transform leftLowerArm;

    [Header("Right Arm")]
    public Transform rightUpperArm;
    public Transform rightLowerArm;

    [Header("Left Leg")]
    public Transform leftUpperLeg;
    public Transform leftLowerLeg;

    [Header("Right Leg")]
    public Transform rightUpperLeg;
    public Transform rightLowerLeg;

    [Header("Options")]
    public bool flipBodyForward = true;
    public float bodyYawOffset = 180f;

    private Dictionary<Transform, Quaternion> initialRotations =
        new Dictionary<Transform, Quaternion>();

    void Start()
    {
        SaveInitialRotation(hips);
        SaveInitialRotation(spine);
        SaveInitialRotation(neck);
        SaveInitialRotation(head);

        SaveInitialRotation(leftUpperArm);
        SaveInitialRotation(leftLowerArm);
        SaveInitialRotation(rightUpperArm);
        SaveInitialRotation(rightLowerArm);

        SaveInitialRotation(leftUpperLeg);
        SaveInitialRotation(leftLowerLeg);
        SaveInitialRotation(rightUpperLeg);
        SaveInitialRotation(rightLowerLeg);
    }

    void SaveInitialRotation(Transform bone)
    {
        if (bone != null && !initialRotations.ContainsKey(bone))
            initialRotations[bone] = bone.rotation;
    }

    public void ApplyPose(Vector3[] joints)
    {
        if (joints == null || joints.Length < 24)
            return;

        // ===== 몸통 방향 =====
        Vector3 leftHipPos = joints[1];
        Vector3 rightHipPos = joints[2];
        Vector3 pelvisPos = joints[0];
        Vector3 neckPos = joints[12];

        Vector3 hipCenter = (leftHipPos + rightHipPos) * 0.5f;
        Vector3 spineDir = (neckPos - pelvisPos).normalized;
        Vector3 hipRight = (rightHipPos - leftHipPos).normalized;

        Vector3 bodyForward = Vector3.Cross(hipRight, spineDir).normalized;

        if (flipBodyForward)
            bodyForward = -bodyForward;

        Quaternion bodyRotation = Quaternion.LookRotation(bodyForward, spineDir);
        bodyRotation = bodyRotation * Quaternion.Euler(0f, bodyYawOffset, 0f);

        if (hips != null && initialRotations.ContainsKey(hips))
            hips.rotation = bodyRotation * initialRotations[hips];

        if (spine != null)
            RotateBoneWithInitial(spine, joints[0], joints[12], Vector3.up);

        if (neck != null)
            RotateBoneWithInitial(neck, joints[12], joints[15], Vector3.up);

        if (head != null)
            RotateBoneWithInitial(head, joints[12], joints[15], Vector3.up);

        // ===== 팔 =====
        RotateBoneWithInitial(leftUpperArm, joints[16], joints[18], Vector3.right);
        RotateBoneWithInitial(leftLowerArm, joints[18], joints[20], Vector3.right);

        RotateBoneWithInitial(rightUpperArm, joints[17], joints[19], Vector3.left);
        RotateBoneWithInitial(rightLowerArm, joints[19], joints[21], Vector3.left);

        // ===== 다리 =====
        // ===== 다리 =====
        RotateBoneWithInitial(leftUpperLeg, joints[1], joints[4], Vector3.up);
        RotateBoneWithInitial(leftLowerLeg, joints[4], joints[7], Vector3.up);

        RotateBoneWithInitial(rightUpperLeg, joints[2], joints[5], Vector3.up);
        RotateBoneWithInitial(rightLowerLeg, joints[5], joints[8], Vector3.up);
    }

    void RotateBoneWithInitial(Transform bone, Vector3 start, Vector3 end, Vector3 modelBoneAxis)
    {
        if (bone == null || !initialRotations.ContainsKey(bone))
            return;

        Vector3 targetDir = (end - start).normalized;
        Quaternion correction = Quaternion.FromToRotation(modelBoneAxis, targetDir);
        bone.rotation = correction * initialRotations[bone];
    }
}