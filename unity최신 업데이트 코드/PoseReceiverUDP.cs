using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

public class PoseReceiverUDP : MonoBehaviour
{
    [Header("UDP")]
    public int port = 12000;

    [Header("Reference")]
    public JointVisualizer jointVisualizer;

    [Header("Avatar")]
    public AvatarRetarget avatar;
    public ClothesRetarget clothes;

    private UdpClient client;
    private Thread receiveThread;

    private readonly object lockObj = new object();

    private Vector3[] latestJoints = new Vector3[24];
    private float latestScreenShoulderWidth = -1f;
    private bool hasNewData = false;

    private bool isRunning = false;

    // =========================
    // Start
    // =========================
    void Start()
    {
        Debug.Log($"[START] PoseReceiverUDP instance={GetInstanceID()} object={gameObject.name}");

        if (jointVisualizer == null)
            Debug.LogError("[ERROR] JointVisualizer reference is NULL");

        try
        {
            client = new UdpClient(port);
            isRunning = true;

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log("[UDP] Receiver started on port " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("UDP Start Error: " + e.Message);
        }
    }

    // =========================
    // Receive Thread (Unity API 절대 사용 금지)
    // =========================
    void ReceiveData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);

        while (isRunning)
        {
            try
            {
                byte[] data = client.Receive(ref anyIP);
                string json = Encoding.UTF8.GetString(data);

                if (string.IsNullOrEmpty(json))
                    continue;

                // 🔥 수동 파싱 (Unity API 사용 안함)
                ParsedPose parsedPose = ParseJsonManually(json);

                if (parsedPose == null)
                    continue;

                lock (lockObj)
                {
                    Array.Copy(parsedPose.joints, latestJoints, 24);
                    latestScreenShoulderWidth = parsedPose.screenShoulderWidth;
                    hasNewData = true;
                }
            }
            catch
            {
                // 무시 (끊김 대비)
            }
        }
    }

    // =========================
    // Unity Main Thread
    // =========================
    void Update()
    {
        if (!hasNewData)
            return;

        Vector3[] jointsCopy = new Vector3[24];
        float screenShoulderWidth;

        lock (lockObj)
        {
            if (!hasNewData)
                return;

            Array.Copy(latestJoints, jointsCopy, 24);
            screenShoulderWidth = latestScreenShoulderWidth;
            hasNewData = false;
        }

        if (jointVisualizer != null)
            jointVisualizer.UpdateJoints(jointsCopy);

        if (avatar != null)
        {
            avatar.ApplyPose(jointsCopy);
            avatar.ApplyBodyFit(jointsCopy, screenShoulderWidth);
        }

        if (clothes != null)
            clothes.ApplyBodyFit(jointsCopy, screenShoulderWidth);
    }

    // =========================
    // JSON 수동 파서 (Unity API 없음)
    // =========================
    private class ParsedPose
    {
        public Vector3[] joints;
        public float screenShoulderWidth;
    }

    private ParsedPose ParseJsonManually(string json)
    {
        try
        {
            // [[x,y,z],[x,y,z]...] 구조에서
            // 숫자만 추출하는 방식

            json = json.Replace("[", "")
                       .Replace("]", "");

            string[] tokens = json.Split(',');

            if (tokens.Length < 72) // 24 * 3
                return null;

            Vector3[] result = new Vector3[24];

            for (int i = 0; i < 24; i++)
            {
                float x = float.Parse(tokens[i * 3 + 0], CultureInfo.InvariantCulture);
                float y = float.Parse(tokens[i * 3 + 1], CultureInfo.InvariantCulture);
                float z = float.Parse(tokens[i * 3 + 2], CultureInfo.InvariantCulture);

                result[i] = new Vector3(x, y, z);
            }

            float screenShoulderWidth = -1f;
            if (tokens.Length >= 75)
                screenShoulderWidth = float.Parse(tokens[72], CultureInfo.InvariantCulture);

            return new ParsedPose
            {
                joints = result,
                screenShoulderWidth = screenShoulderWidth
            };
        }
        catch
        {
            return null;
        }
    }

    // =========================
    // Clean Shutdown
    // =========================
    void OnDestroy()
    {
        isRunning = false;

        try
        {
            if (client != null)
                client.Close();
        }
        catch { }

        try
        {
            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread.Join(100);
        }
        catch { }

        Debug.Log("[UDP] Receiver stopped");
    }
}
