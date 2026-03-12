using UnityEngine;

public class JointVisualizer : MonoBehaviour
{
    public GameObject jointPrefab;
    private GameObject[] joints = new GameObject[24];

    void Start()
    {
        for (int i = 0; i < 24; i++)
        {
            joints[i] = Instantiate(jointPrefab);
        }
    }

    public float scale = 1f;

public void UpdateJoints(Vector3[] smplJoints)
{
    if (smplJoints == null || smplJoints.Length < 24)
        return;

    for (int i = 0; i < 24; i++)
    {
        joints[i].transform.position = smplJoints[i] * scale;
    }
}
}