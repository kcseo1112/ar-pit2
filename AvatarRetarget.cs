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

    [Header("Avatar Body Fit")]
    public Transform avatarRoot;
    public bool enableBodyFitScale = false;
    public bool autoCalibrateBodyScale = true;
    public float manualReferenceShoulderWidth = -1f;
    public float minBodyScale = 0.75f;
    public float maxBodyScale = 1.35f;
    public float bodyScaleSmooth = 10f;

    [Header("Root Follow")]
    public bool enableRootFollow = true;
    public Camera renderCamera;
    public bool mirrorRootX = false;
    public float depthToUnityScale = 1f;
    public Vector3 rootPositionOffset = Vector3.zero;
    public float rootFollowSmooth = 10f;
    public float minValidDepth = 0.3f;
    public float maxValidDepth = 5.0f;

    [Header("ROMP Neck/Head Apply")]
    public bool applyRomptoNeck = false;
    public bool applyRomptoHead = false;

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

    [Header("Head Tracking")]
    public bool invertYaw = false;
    public bool invertPitch = false;
    public bool invertRoll = false;

    [Range(0f, 1f)]
    public float neckYawWeight = 0.35f;

    [Range(0f, 1f)]
    public float neckPitchWeight = 0.30f;

    [Range(0f, 1f)]
    public float neckRollWeight = 0.25f;

    [Range(0f, 1f)]
    public float headYawWeight = 0.65f;

    [Range(0f, 1f)]
    public float headPitchWeight = 0.70f;

    [Range(0f, 1f)]
    public float headRollWeight = 0.75f;

    public Vector3 neckEulerOffset = Vector3.zero;
    public Vector3 headEulerOffset = Vector3.zero;

    [Range(0f, 30f)]
    public float maxYaw = 60f;

    [Range(0f, 30f)]
    public float maxPitch = 40f;

    [Range(0f, 30f)]
    public float maxRoll = 30f;

    private Dictionary<Transform, Quaternion> initialLocalRotations = new();
    private Vector3 initialAvatarRootScale = Vector3.one;
    private float referenceShoulderWidth = -1f;
    private float targetBodyScale = 1f;

    void Start()
    {
        if (avatarRoot == null)
            avatarRoot = transform;

        initialAvatarRootScale = avatarRoot.localScale;
        referenceShoulderWidth = manualReferenceShoulderWidth > 0f ? manualReferenceShoulderWidth : -1f;

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

    void LateUpdate()
    {
        ApplyBodyFitScale();
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

        Vector3 spineUp = (neckPos - pelvis).normalized;

        Vector3 hipRight = (rightHipPos - leftHipPos).normalized;
        Vector3 bodyForward = Vector3.Cross(hipRight, spineUp).normalized;

        if (flipBodyForward)
            bodyForward = -bodyForward;

        bodyForward = Quaternion.Euler(0f, bodyYawOffset, 0f) * bodyForward;

        if (rotateHips)
            RotateBoneLookLocal(hips, bodyForward, spineUp);

        Vector3 shoulderRight = (rightShoulderPos - leftShoulderPos).normalized;
        Vector3 chestUp = (neckPos - pelvis).normalized;
        Vector3 chestForward = Vector3.Cross(shoulderRight, chestUp).normalized;

        if (flipBodyForward)
            chestForward = -chestForward;

        chestForward = Quaternion.Euler(0f, bodyYawOffset, 0f) * chestForward;

        RotateBoneLookLocal(spine, chestForward, chestUp);

        // ROMP neck/head 적용은 옵션으로 둠
        if (applyRomptoNeck)
        {
            Vector3 neckUpDir = (headPos - neckPos).normalized;
            RotateBoneLookLocal(neck, chestForward, neckUpDir);
        }

        if (applyRomptoHead)
        {
            Vector3 neckUpDir = (headPos - neckPos).normalized;
            RotateBoneLookLocal(head, chestForward, neckUpDir);
        }

        // 팔
        RotateBoneUpperLocal(leftUpperArm, joints[17], joints[19], leftUpperArmAxis);
        RotateBoneUpperLocal(leftLowerArm, joints[19], joints[21], leftLowerArmAxis);

        RotateBoneUpperLocal(rightUpperArm, joints[16], joints[18], rightUpperArmAxis);
        RotateBoneUpperLocal(rightLowerArm, joints[18], joints[20], rightLowerArmAxis);

        // // 다리
        RotateBoneLocal(leftUpperLeg, joints[2], joints[5], leftUpperLegAxis);
        RotateBoneLocal(leftLowerLeg, joints[5], joints[8], leftLowerLegAxis);

        RotateBoneLocal(rightUpperLeg, joints[1], joints[4], rightUpperLegAxis);
        RotateBoneLocal(rightLowerLeg, joints[4], joints[7], rightLowerLegAxis);
    }

    public void ApplyBodyFit(Vector3[] joints, float screenShoulderWidth)
    {
        if (!enableBodyFitScale)
            return;

        float shoulderWidth = screenShoulderWidth;

        if (shoulderWidth <= 0.0001f && joints != null && joints.Length >= 18)
            shoulderWidth = Vector3.Distance(joints[16], joints[17]);

        if (shoulderWidth <= 0.0001f)
            return;

        if (autoCalibrateBodyScale && referenceShoulderWidth <= 0f)
            referenceShoulderWidth = shoulderWidth;

        if (referenceShoulderWidth <= 0.0001f)
            return;

        targetBodyScale = Mathf.Clamp(shoulderWidth / referenceShoulderWidth, minBodyScale, maxBodyScale);
    }

    public void ApplyRootFollow(Vector2 rootPixel, float rootDepthMeters, Vector2 frameSize)
    {
        if (!enableRootFollow)
            return;

        if (avatarRoot == null)
            avatarRoot = transform;

        if (renderCamera == null)
            renderCamera = Camera.main;

        if (renderCamera == null)
            return;

        if (frameSize.x <= 0f || frameSize.y <= 0f)
            return;

        if (rootPixel.x < 0f || rootPixel.y < 0f)
            return;

        if (rootDepthMeters < minValidDepth || rootDepthMeters > maxValidDepth)
            return;

        float viewportX = rootPixel.x / frameSize.x;
        float viewportY = 1f - (rootPixel.y / frameSize.y);

        if (mirrorRootX)
            viewportX = 1f - viewportX;

        float unityDepth = rootDepthMeters * depthToUnityScale;
        Vector3 targetWorld = renderCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, unityDepth));
        targetWorld += rootPositionOffset;

        float t = 1f - Mathf.Exp(-rootFollowSmooth * Time.deltaTime);
        avatarRoot.position = Vector3.Lerp(avatarRoot.position, targetWorld, t);
    }

    public void RecalibrateBodyFit(Vector3[] joints)
    {
        if (joints == null || joints.Length < 18)
            return;

        float shoulderWidth = Vector3.Distance(joints[16], joints[17]);

        if (shoulderWidth > 0.0001f)
            referenceShoulderWidth = shoulderWidth;
    }

    void ApplyBodyFitScale()
    {
        if (!enableBodyFitScale || avatarRoot == null)
            return;

        Vector3 targetScale = initialAvatarRootScale * targetBodyScale;
        float t = 1f - Mathf.Exp(-bodyScaleSmooth * Time.deltaTime);
        avatarRoot.localScale = Vector3.Lerp(avatarRoot.localScale, targetScale, t);
    }

    public void ApplyHeadRotation(float yaw, float pitch, float roll)
{
    Debug.Log($"Head Applied: yaw={yaw:F2}, pitch={pitch:F2}, roll={roll:F2}");
    // =========================================
    // 1. 축 재매핑
    // 현재 관찰 기준:
    // - 좌우 회전이 위/아래 보기로 들어감
    // - 위아래 움직임이 기울임으로 들어감
    //
    // 따라서 우선 한 번 이렇게 재매핑해서 테스트
    // mappedYaw   = pitch
    // mappedPitch = roll
    // mappedRoll  = yaw
    // =========================================
    float mappedYaw = pitch;
    float mappedPitch = roll;
    float mappedRoll = yaw;

    // =========================================
    // 2. 부호 반전 옵션
    // 축 방향이 반대일 경우 인스펙터 체크로 뒤집기
    // =========================================
    if (invertYaw) mappedYaw = -mappedYaw;
    if (invertPitch) mappedPitch = -mappedPitch;
    if (invertRoll) mappedRoll = -mappedRoll;

    // =========================================
    // 3. 각도 제한
    // =========================================
    mappedYaw = Mathf.Clamp(mappedYaw, -maxYaw, maxYaw);
    mappedPitch = Mathf.Clamp(mappedPitch, -maxPitch, maxPitch);
    mappedRoll = Mathf.Clamp(mappedRoll, -maxRoll, maxRoll);

    // =========================================
    // 4. neck 적용
    // Unity Euler 기준:
    // X = pitch
    // Y = yaw
    // Z = roll
    // =========================================
    if (neck != null && initialLocalRotations.ContainsKey(neck))
    {
        Vector3 neckEuler = new Vector3(
            mappedPitch * neckPitchWeight,
            mappedYaw * neckYawWeight,
            mappedRoll * neckRollWeight
        ) + neckEulerOffset;

        neck.localRotation = initialLocalRotations[neck] * Quaternion.Euler(neckEuler);
    }

    // =========================================
    // 5. head 적용
    // =========================================
    if (head != null && initialLocalRotations.ContainsKey(head))
    {
        Vector3 headEuler = new Vector3(
            mappedPitch * headPitchWeight,
            mappedYaw * headYawWeight,
            mappedRoll * headRollWeight
        ) + headEulerOffset;

        head.localRotation = initialLocalRotations[head] * Quaternion.Euler(headEuler);
    }
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
