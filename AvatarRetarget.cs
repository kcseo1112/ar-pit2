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

    [Header("Body Option")]
    public bool rotateHips = true;
    public bool flipBodyForward = true;
    public float bodyYawOffset = 0f;

    [Header("Upper Body Axis")]
    public Vector3 spineAxis = Vector3.up;
    public Vector3 neckAxis = Vector3.up;
    public Vector3 headAxis = Vector3.up;

    public Vector3 leftUpperArmAxis = Vector3.right;
    public Vector3 leftLowerArmAxis = Vector3.right;
    public Vector3 rightUpperArmAxis = Vector3.left;
    public Vector3 rightLowerArmAxis = Vector3.left;

    [Header("Lower Body Axis")]
    public Vector3 leftUpperLegAxis = Vector3.down;
    public Vector3 leftLowerLegAxis = Vector3.up;
    public Vector3 rightUpperLegAxis = Vector3.down;
    public Vector3 rightLowerLegAxis = Vector3.up;

    private Dictionary<Transform, Quaternion> initialLocalRotations = new();

    void Start()
    {
        SaveInitial(hips);
        SaveInitial(spine);
        SaveInitial(neck);
        SaveInitial(head);

        SaveInitial(leftUpperArm);
        SaveInitial(leftLowerArm);
        SaveInitial(rightUpperArm);
        SaveInitial(rightLowerArm);

        SaveInitial(leftUpperLeg);
        SaveInitial(leftLowerLeg);
        SaveInitial(rightUpperLeg);
        SaveInitial(rightLowerLeg);
    }

    void SaveInitial(Transform bone)
    {
        if (bone == null) return;
        if (!initialLocalRotations.ContainsKey(bone))
            initialLocalRotations[bone] = bone.localRotation;
    }

    public void ApplyPose(Vector3[] joints)
    {
        if (joints == null || joints.Length < 24)
            return;

        Vector3 pelvis = joints[0];
        Vector3 neckPos = joints[12];
        Vector3 headPos = joints[15];

        Vector3 leftHipPos = joints[1];
        Vector3 rightHipPos = joints[2];

        Vector3 leftShoulderPos = joints[16];
        Vector3 rightShoulderPos = joints[17];

        // 공통 up
        Vector3 spineUp = (neckPos - pelvis).normalized;

        // 골반 기준 forward
        Vector3 hipRight = (rightHipPos - leftHipPos).normalized;
        Vector3 bodyForward = Vector3.Cross(hipRight, spineUp).normalized;

        if (flipBodyForward)
            bodyForward = -bodyForward;

        bodyForward = Quaternion.Euler(0f, bodyYawOffset, 0f) * bodyForward;

        // 1. 골반 회전
        RotateBoneLookLocal(hips, bodyForward, spineUp);

        // 2. 몸통 회전(어깨선 사용)
        Vector3 shoulderRight = (rightShoulderPos - leftShoulderPos).normalized;
        Vector3 chestUp = (neckPos - pelvis).normalized;
        Vector3 chestForward = Vector3.Cross(shoulderRight, chestUp).normalized;

        if (flipBodyForward)
            chestForward = -chestForward;

        chestForward = Quaternion.Euler(0f, bodyYawOffset, 0f) * chestForward;

        RotateBoneLookLocal(spine, chestForward, chestUp);

        // 3. 목 회전 - 몸통 회전을 어느 정도 따라가게
        Vector3 neckUpDir = (headPos - neckPos).normalized;
        RotateBoneLookLocal(neck, chestForward, neckUpDir);

        // 4. 머리 - 일단 목과 같은 forward를 공유
        RotateBoneLookLocal(head, chestForward, neckUpDir);

        // 팔
        RotateBoneUpperLocal(leftUpperArm, joints[17], joints[19], leftUpperArmAxis);
        RotateBoneUpperLocal(leftLowerArm, joints[19], joints[21], leftLowerArmAxis);

        RotateBoneUpperLocal(rightUpperArm, joints[16], joints[18], rightUpperArmAxis);
        RotateBoneUpperLocal(rightLowerArm, joints[18], joints[20], rightLowerArmAxis);

        // 다리
        RotateBoneLocal(leftUpperLeg, joints[2], joints[5], leftUpperLegAxis);
        RotateBoneLocal(leftLowerLeg, joints[5], joints[8], leftLowerLegAxis);

        RotateBoneLocal(rightUpperLeg, joints[1], joints[4], rightUpperLegAxis);
        RotateBoneLocal(rightLowerLeg, joints[4], joints[7], rightLowerLegAxis);
    }

    void RotateBoneLookLocal(Transform bone, Vector3 forward, Vector3 up)
    {
        if (bone == null || !initialLocalRotations.ContainsKey(bone))
            return;

        Quaternion targetWorld = Quaternion.LookRotation(forward.normalized, up.normalized);

        if (bone.parent != null)
        {
            Quaternion localTarget = Quaternion.Inverse(bone.parent.rotation) * targetWorld;
            bone.localRotation = localTarget * initialLocalRotations[bone];
        }
        else
        {
            bone.rotation = targetWorld;
        }
    }

    void RotateBoneUpperLocal(Transform bone, Vector3 start, Vector3 end, Vector3 modelAxis)
    {
        if (bone == null || bone.parent == null || !initialLocalRotations.ContainsKey(bone))
            return;

        Vector3 worldDir = (end - start).normalized;
        Vector3 localDir = bone.parent.InverseTransformDirection(worldDir).normalized;

        Quaternion correction = Quaternion.FromToRotation(modelAxis.normalized, localDir);
        bone.localRotation = correction * initialLocalRotations[bone];
    }

    void RotateBoneLocal(Transform bone, Vector3 start, Vector3 end, Vector3 modelAxis)
    {
        if (bone == null || bone.parent == null || !initialLocalRotations.ContainsKey(bone))
            return;

        Vector3 worldDir = (end - start).normalized;
        Vector3 localDir = bone.parent.InverseTransformDirection(worldDir).normalized;

        Quaternion correction = Quaternion.FromToRotation(modelAxis.normalized, localDir);
        bone.localRotation = correction * initialLocalRotations[bone];
    }
}