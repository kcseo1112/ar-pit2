using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading;

[RequireComponent(typeof(MeshRenderer))]
public class D435iBackgroundReceiver : MonoBehaviour
{
    [Header("TCP")]
    public string host = "127.0.0.1";
    public int port = 13000;

    [Header("Camera Background")]
    public Camera targetCamera;
    public float distanceFromCamera = 10f;
    public bool mirrorX = false;

    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private volatile bool running;

    private readonly object frameLock = new object();
    private byte[] latestJpeg;
    private bool hasNewFrame;

    private Texture2D texture;
    private MeshRenderer meshRenderer;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        meshRenderer = GetComponent<MeshRenderer>();
        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        meshRenderer.material.mainTexture = texture;

        if (mirrorX)
        {
            meshRenderer.material.mainTextureScale = new Vector2(-1f, 1f);
            meshRenderer.material.mainTextureOffset = new Vector2(1f, 0f);
        }

        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void Update()
    {
        byte[] jpeg = null;

        lock (frameLock)
        {
            if (hasNewFrame && latestJpeg != null)
            {
                jpeg = latestJpeg;
                hasNewFrame = false;
            }
        }

        if (jpeg != null)
            texture.LoadImage(jpeg, false);
    }

    void LateUpdate()
    {
        if (targetCamera == null)
            return;

        Transform cam = targetCamera.transform;

        transform.position = cam.position + cam.forward * distanceFromCamera;
        transform.rotation = cam.rotation;

        float height = 2f * distanceFromCamera *
                       Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * targetCamera.aspect;

        transform.localScale = new Vector3(width, height, 1f);
    }

    private void ReceiveLoop()
    {
        while (running)
        {
            try
            {
                client = new TcpClient();
                client.Connect(host, port);
                stream = client.GetStream();

                while (running && client.Connected)
                {
                    byte[] lengthBytes = ReadExact(stream, 4);
                    if (lengthBytes == null)
                        break;

                    int length = BitConverter.ToInt32(lengthBytes, 0);
                    if (length <= 0 || length > 2000000)
                        break;

                    byte[] jpegBytes = ReadExact(stream, length);
                    if (jpegBytes == null)
                        break;

                    lock (frameLock)
                    {
                        latestJpeg = jpegBytes;
                        hasNewFrame = true;
                    }
                }
            }
            catch
            {
                Thread.Sleep(200);
            }
            finally
            {
                try { stream?.Close(); } catch { }
                try { client?.Close(); } catch { }
            }
        }
    }

    private byte[] ReadExact(NetworkStream ns, int size)
    {
        byte[] buffer = new byte[size];
        int offset = 0;

        while (offset < size)
        {
            int read = ns.Read(buffer, offset, size - offset);
            if (read <= 0)
                return null;

            offset += read;
        }

        return buffer;
    }

    void OnDestroy()
    {
        running = false;

        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }

        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join(100);
    }
}
