using UnityEngine;

public class AvatarRetarget : MonoBehaviour
{
    public Transform hips;

    private Vector3 initialHipDirection;

    void Start()
    {
        // 초기 골반 좌우 방향 저장
        Transform left = hips.parent.Find("mixamorig1:LeftUpLeg");
        Transform right = hips.parent.Find("mixamorig1:RightUpLeg");

        initialHipDirection = (right.position - left.position).normalized;
    }

    public void ApplyPose(Vector3[] joints)
    {
        if (joints == null || joints.Length < 24)
            return;

        Vector3 leftHip = joints[1];
        Vector3 rightHip = joints[2];

        Vector3 newHipDirection = (rightHip - leftHip).normalized;

        Quaternion rotation =
            Quaternion.FromToRotation(initialHipDirection, newHipDirection);

        hips.rotation = rotation;
    }
}