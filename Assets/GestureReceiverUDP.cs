using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class GestureReceiverUDP : MonoBehaviour
{
    public int port = 5010;
    public FitRoomMainUI fitRoomUI;
    public bool logEvents = true;

    private readonly ConcurrentQueue<GestureEventMessage> pendingEvents = new ConcurrentQueue<GestureEventMessage>();
    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running;

    void Awake()
    {
        if (fitRoomUI == null)
            fitRoomUI = FindObjectOfType<FitRoomMainUI>();
    }

    void OnEnable()
    {
        StartReceiver();
    }

    void OnDisable()
    {
        StopReceiver();
    }

    void OnDestroy()
    {
        StopReceiver();
    }

    void Update()
    {
        while (pendingEvents.TryDequeue(out GestureEventMessage message))
            Dispatch(message);
    }

    private void StartReceiver()
    {
        if (running)
            return;

        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void StopReceiver()
    {
        running = false;

        try
        {
            if (udpClient != null)
                udpClient.Close();
        }
        catch
        {
        }

        udpClient = null;

        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join(200);

        receiveThread = null;
    }

    private void ReceiveLoop()
    {
        try
        {
            udpClient = new UdpClient(port);
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (running)
            {
                byte[] data = udpClient.Receive(ref remote);
                string json = Encoding.UTF8.GetString(data);
                GestureEventMessage message = JsonUtility.FromJson<GestureEventMessage>(json);
                if (message != null && !string.IsNullOrEmpty(message.type))
                    pendingEvents.Enqueue(message);
            }
        }
        catch (SocketException)
        {
            if (running)
                Debug.LogWarning("[GestureUDP] receiver socket closed unexpectedly.");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            if (running)
                Debug.LogWarning("[GestureUDP] receive error: " + ex.Message);
        }
    }

    private void Dispatch(GestureEventMessage message)
    {
        if (fitRoomUI == null)
            fitRoomUI = FindObjectOfType<FitRoomMainUI>();

        if (fitRoomUI == null)
            return;

        if (message.type == "swipe")
        {
            if (logEvents)
                Debug.Log("[GestureUDP] swipe " + message.dir);

            if (message.dir == "up")
                fitRoomUI.OnGestureSwipeUp();
            else if (message.dir == "down")
                fitRoomUI.OnGestureSwipeDown();
            else if (message.dir == "left")
                fitRoomUI.OnGestureSwipeLeft();
            else if (message.dir == "right")
                fitRoomUI.OnGestureSwipeRight();

            return;
        }

        Vector2 screenPosition = NormalizedToScreen(message.x, message.y);

        if (message.type == "press_start")
        {
            if (logEvents)
                Debug.Log("[GestureUDP] press_start x=" + message.x.ToString("F3") + " y=" + message.y.ToString("F3"));

            fitRoomUI.OnHandPressStart(screenPosition);
        }
        else if (message.type == "press_move")
        {
            fitRoomUI.OnHandPressMove(screenPosition);
        }
        else if (message.type == "press_release")
        {
            if (logEvents)
                Debug.Log("[GestureUDP] press_release x=" + message.x.ToString("F3") + " y=" + message.y.ToString("F3"));

            fitRoomUI.OnHandPressRelease(screenPosition);
        }
        else if (message.type == "fist_hold")
        {
            if (logEvents)
                Debug.Log("[GestureUDP] fist_hold");

            fitRoomUI.OnGestureFistHoldConfirmed();
        }
        else if (message.type == "thumbs_up_favorite")
        {
            if (logEvents)
                Debug.Log("[GestureUDP] thumbs_up_favorite");

            fitRoomUI.OnGestureThumbsUpFavorite();
        }
        else if (message.type == "toggle_mode")
        {
            if (logEvents)
                Debug.Log("[GestureUDP] toggle_mode");

            fitRoomUI.OnGestureToggleListMode();
        }
    }

    private Vector2 NormalizedToScreen(float x, float y)
    {
        return new Vector2(x * Screen.width, (1f - y) * Screen.height);
    }

    [Serializable]
    private class GestureEventMessage
    {
        public string type = "";
        public string dir = "";
        public string hand = "";
        public float x = 0f;
        public float y = 0f;
        public double timestamp = 0.0;
    }
}
