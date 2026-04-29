using UnityEngine;

public class JointVisualizer : MonoBehaviour
{
    public GameObject jointPrefab;
    private GameObject[] joints = new GameObject[24];

    public float scale = 0.2f;
    public Transform avatarHips;

    [Header("Debug View")]
    public bool showJoints = true;

    private bool lastShowJoints;

    void Start()
    {
        for (int i = 0; i < 24; i++)
        {
            joints[i] = Instantiate(jointPrefab);
            joints[i].transform.SetParent(transform);
        }

        lastShowJoints = showJoints;
        SetJointVisible(showJoints);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleJointVisible();
        }

        if (lastShowJoints != showJoints)
        {
            SetJointVisible(showJoints);
            lastShowJoints = showJoints;
        }
    }

    public void UpdateJoints(Vector3[] smplJoints)
    {
        if (smplJoints == null || smplJoints.Length < 24)
            return;

        Vector3 pelvis = smplJoints[0];

        for (int i = 0; i < 24; i++)
        {
            Vector3 pos = (smplJoints[i] - pelvis) * scale;

            if (avatarHips != null)
                joints[i].transform.position = avatarHips.position + pos;
            else
                joints[i].transform.position = pos;
        }
    }

    public void SetJointVisible(bool visible)
    {
        showJoints = visible;

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;

            Renderer renderer = joints[i].GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    public void ToggleJointVisible()
    {
        SetJointVisible(!showJoints);
        lastShowJoints = showJoints;
    }
}