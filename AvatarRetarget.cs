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

    private Dictionary<Transform, Quaternion> initialWorldRotations = new();
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

        if (!initialWorldRotations.ContainsKey(bone))
            initialWorldRotations[bone] = bone.rotation;

        if (!initialLocalRotations.ContainsKey(bone))
            initialLocalRotations[bone] = bone.localRotation;
    }

    public void ApplyPose(Vector3[] joints)
    {
        if (joints == null || joints.Length < 24)
            return;

        Vector3 pelvis = joints[0];
        Vector3 neckPos = joints[12];
        Vector3 leftHipPos = joints[1];
        Vector3 rightHipPos = joints[2];

        Vector3 spineDir = (neckPos - pelvis).normalized;
        Vector3 hipRight = (rightHipPos - leftHipPos).normalized;
        Vector3 bodyForward = Vector3.Cross(hipRight, spineDir).normalized;

        if (flipBodyForward)
            bodyForward = -bodyForward;

        if (rotateHips && hips != null)
        {
            Quaternion bodyRot = Quaternion.LookRotation(bodyForward, spineDir);
            bodyRot *= Quaternion.Euler(0f, bodyYawOffset, 0f);

            if (hips.parent != null)
            {
                Quaternion parentRot = hips.parent.rotation;
                hips.localRotation = Quaternion.Inverse(parentRot) * bodyRot;
            }
            else
            {
                hips.rotation = bodyRot;
            }
        }

        // ===== 상체: 첫 번째 코드 방식 =====
        RotateBoneUpperWorld(spine, joints[0], joints[12], spineAxis);
        RotateBoneUpperWorld(neck, joints[12], joints[15], neckAxis);
        RotateBoneUpperWorld(head, joints[12], joints[15], headAxis);

        //RotateBoneUpperWorld(leftUpperArm, joints[16], joints[18], leftUpperArmAxis);
        //RotateBoneUpperWorld(leftLowerArm, joints[18], joints[20], leftLowerArmAxis);

        //RotateBoneUpperWorld(rightUpperArm, joints[17], joints[19], rightUpperArmAxis);
        //RotateBoneUpperWorld(rightLowerArm, joints[19], joints[21], rightLowerArmAxis);

        // ===== 상체: 팔 좌우 교체 =====
        RotateBoneUpperWorld(leftUpperArm, joints[17], joints[19], leftUpperArmAxis);
        RotateBoneUpperWorld(leftLowerArm, joints[19], joints[21], leftLowerArmAxis);

        RotateBoneUpperWorld(rightUpperArm, joints[16], joints[18], rightUpperArmAxis); 
        RotateBoneUpperWorld(rightLowerArm, joints[18], joints[20], rightLowerArmAxis);

        // ===== 하체: local 방식 유지 =====
        //RotateBoneLocal(leftUpperLeg, joints[1], joints[4], leftUpperLegAxis);
        //RotateBoneLocal(leftLowerLeg, joints[4], joints[7], leftLowerLegAxis);

        //RotateBoneLocal(rightUpperLeg, joints[2], joints[5], rightUpperLegAxis);
        //RotateBoneLocal(rightLowerLeg, joints[5], joints[8], rightLowerLegAxis);

        // ===== 하체: 좌우 교체 =====
        RotateBoneLocal(leftUpperLeg, joints[2], joints[5], leftUpperLegAxis);
        RotateBoneLocal(leftLowerLeg, joints[5], joints[8], leftLowerLegAxis);

        RotateBoneLocal(rightUpperLeg, joints[1], joints[4], rightUpperLegAxis);
        RotateBoneLocal(rightLowerLeg, joints[4], joints[7], rightLowerLegAxis);
    }

    void RotateBoneUpperWorld(Transform bone, Vector3 start, Vector3 end, Vector3 modelAxis)
    {
        if (bone == null)
            return;

        Vector3 targetDir = (end - start).normalized;
        Quaternion correction = Quaternion.FromToRotation(modelAxis.normalized, targetDir);
        bone.rotation = correction;
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