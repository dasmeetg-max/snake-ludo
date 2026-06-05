using System;
using System.Collections.Generic;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SnakeLudo.Managers;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    public SocketIOUnity socket;
    public int myPlayerIndex = -1;

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        var uri = new Uri("http://localhost:3000");

        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            EIO = EngineIO.V4,
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        // Critical: use Newtonsoft serializer so JObject parsing works
        socket.JsonSerializer = new NewtonsoftJsonSerializer();

        // ── Connection events ──────────────────────────────────

        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("✅ Connected to server");
        };

        socket.OnDisconnected += (sender, e) =>
        {
            Debug.LogWarning($"🔌 Disconnected: {e}");
        };

        socket.OnError += (sender, e) =>
        {
            Debug.LogError($"❌ Socket error: {e}");
        };

        socket.OnReconnectAttempt += (sender, e) =>
        {
            Debug.Log($"🔄 Reconnect attempt #{e}");
        };

        // ── Game events (all on Unity main thread) ─────────────

        socket.OnUnityThread("init", response =>
        {
            var data = response.GetValue<JObject>();
            myPlayerIndex = data["playerIndex"].Value<int>();

            Debug.Log($"🎮 I am Player {myPlayerIndex}");
            BoardManager.Instance.OnInitReceived(data);
        });

        socket.OnUnityThread("playerJoined", response =>
        {
            var data = response.GetValue<JObject>();
            int total = data["totalPlayers"].Value<int>();
            var playerData = data["playerData"] as Newtonsoft.Json.Linq.JObject;

            Debug.Log($"👥 Players in room: {total}/2");

            // Spawn the joining player's tokens on all existing clients
            if (playerData != null)
                BoardManager.Instance.OnPlayerJoined(playerData);
        });

        socket.OnUnityThread("gameStart", response =>
        {
            var data = response.GetValue<JObject>();
            int firstPlayer = data["currentPlayerIndex"].Value<int>();

            Debug.Log($"🚀 Game started — Player {firstPlayer} goes first");
            BoardManager.Instance.OnGameStart(firstPlayer);
        });

        socket.OnUnityThread("turnChanged", response =>
        {
            var data = response.GetValue<JObject>();
            int playerIndex = data["currentPlayerIndex"].Value<int>();

            Debug.Log($"🔧 [DEBUG] Turn forced → Player {playerIndex}");
            BoardManager.Instance.OnTurnChanged(playerIndex);
        });

        // Debug only — single event carrying all teleport positions at once
        // Eliminates race conditions from multiple rapid moveResult events
        socket.OnUnityThread("debugSync", response =>
        {
            var data = response.GetValue<JObject>();
            var tokens = data["tokens"] as Newtonsoft.Json.Linq.JArray;
            BoardManager.Instance.OnDebugSync(tokens);
        });

        socket.OnUnityThread("rollResult", response =>
        {
            var data = response.GetValue<JObject>();
            int playerIndex = data["playerIndex"].Value<int>();
            int roll = data["roll"].Value<int>();
            bool autoPass = data["autoPass"].Value<bool>();
            var validMoves = data["validMoves"] as JArray;

            BoardManager.Instance.OnRollResult(playerIndex, roll, autoPass, validMoves);
        });

        socket.OnUnityThread("moveResult", response =>
        {
            var data = response.GetValue<JObject>();
            int playerIndex = data["playerIndex"].Value<int>();
            var moveResult = data["moveResult"] as JObject;
            int? nextPlayerIndex = data["nextPlayerIndex"].Type != JTokenType.Null
                ? data["nextPlayerIndex"].Value<int>()
                : (int?)null;

            BoardManager.Instance.OnMoveResult(playerIndex, moveResult, nextPlayerIndex);
        });

        socket.OnUnityThread("playerWon", response =>
        {
            var data = response.GetValue<JObject>();
            int winnerIndex = data["playerIndex"].Value<int>();

            BoardManager.Instance.OnPlayerWon(winnerIndex);
        });

        socket.OnUnityThread("error", response =>
        {
            string msg;
            try
            {
                var data = response.GetValue<JObject>();
                msg = data["message"].Value<string>();
            }
            catch
            {
                msg = response.GetValue<string>();
            }

            Debug.LogWarning($"⚠️ Server error: {msg}");
        });

        // ── Connect ────────────────────────────────────────────

        Debug.Log("🔌 Connecting to http://localhost:3000 ...");
        socket.Connect();
    }

    async void OnApplicationQuit()
    {
        if (socket != null && socket.Connected)
            await socket.DisconnectAsync();

        socket?.Dispose();
    }

    // ── Emitters ───────────────────────────────────────────────

    public void RollDice()
    {
        Debug.Log("📤 Emitting rollDice");
        socket.Emit("rollDice");
    }

    public void MoveToken(int tokenId)
    {
        Debug.Log($"📤 Emitting moveToken tokenId={tokenId}");
        socket.Emit("moveToken", new { tokenId });
    }
}