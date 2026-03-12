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

    public float scale = 0.2f;
    public Transform avatarHips;
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
}