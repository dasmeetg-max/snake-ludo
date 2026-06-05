using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SnakeLudo.View;
using SnakeLudo.Core;
using Newtonsoft.Json.Linq;

namespace SnakeLudo.Managers
{
    public class BoardManager : MonoBehaviour
    {
        public static BoardManager Instance;

        // ── Inspector refs ─────────────────────────────────────────
        // Only prefabs — all board config comes from server
        public GameObject tilePrefab;
        public GameObject playerPrefab;
        public GameObject startZonePrefab;
        public GameObject ladderCarvingPrefab;


        public GameObject taipanPrefab; // carved ladder relief on destination tile face
        public DiceView diceView;            // assign in Inspector — the Dice GameObject

        // ── Board data ─────────────────────────────────────────────
        private List<List<TileView>> ringTiles = new List<List<TileView>>();

        // Start zone positions per player — calculated from server ring config
        // startZoneSlots[playerIndex][slotIndex] → world position
        private Dictionary<int, List<Vector3>> startZoneSlots = new Dictionary<int, List<Vector3>>();

        // ── Players ────────────────────────────────────────────────
        private List<Player> players = new List<Player>();
        private Dictionary<Token, PlayerView> tokenViews = new Dictionary<Token, PlayerView>();

        // Maps token → which TileView it currently occupies (null if in start zone)
        private Dictionary<Token, TileView> tokenTiles = new Dictionary<Token, TileView>();

        private Color[] playerColors = new Color[]
        {
            new Color(0.2f, 0.4f, 1.0f),   // blue
            new Color(1.0f, 0.2f, 0.2f),   // red
            new Color(0.2f, 0.8f, 0.2f),   // green
            new Color(1.0f, 0.85f, 0.1f)   // yellow
        };

        // ── Game state ─────────────────────────────────────────────
        private int currentPlayerIndex = 0;
        private bool gameStarted = false;
        private bool isWaitingForToken = false;
        private List<int> validTokenIds = new List<int>();

        // ── Board config (stored from server for start zone calculation) ──
        private float level1Radius = 0f;
        private int level1Size = 0;
        private float tileSize = 2f;

        // ─────────────────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────────────────

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Debug.Log("⏳ BoardManager ready — waiting for server init...");
        }

        // ─────────────────────────────────────────────────────────
        // BOARD GENERATION
        // ─────────────────────────────────────────────────────────

        void GeneratePyramid(JArray ringConfigs)
        {
            foreach (var ring in ringTiles)
                foreach (var tile in ring)
                    if (tile != null) Destroy(tile.gameObject);
            ringTiles.Clear();

            foreach (var cfg in ringConfigs)
            {
                int level = cfg["level"].Value<int>();
                int boardSize = cfg["boardSize"].Value<int>();
                float height = cfg["height"].Value<float>();
                float radius = cfg["radius"].Value<float>();
                float ts = cfg["tileSize"].Value<float>();

                // Store level 1 data for start zone positioning
                if (level == 1)
                {
                    level1Radius = radius;
                    level1Size = boardSize;
                    tileSize = ts;
                }

                List<TileView> currentRing = new List<TileView>();

                for (int i = 0; i < boardSize; i++)
                {
                    float angle = i * Mathf.PI * 2f / boardSize;
                    Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                    Quaternion rot = Quaternion.LookRotation(new Vector3(pos.x, 0f, pos.z));

                    GameObject obj = Instantiate(tilePrefab, pos, rot, transform);
                    obj.transform.localScale = new Vector3(ts * 0.92f, ts, ts * 0.92f);

                    TileView tile = obj.GetComponent<TileView>();
                    tile.level = level;
                    tile.tileIndex = i;

                    currentRing.Add(tile);
                }

                while (ringTiles.Count < level)
                    ringTiles.Add(new List<TileView>());

                ringTiles[level - 1] = currentRing;

                Debug.Log($"🏗️ Ring {level}: {boardSize} tiles | radius: {radius:F2} | height: {height}");
            }

            Debug.Log($"✅ Pyramid built — {ringTiles.Count} levels");
        }

        // ─────────────────────────────────────────────────────────
        // START ZONES
        // ─────────────────────────────────────────────────────────

        // Builds the start zone platform and calculates token slot positions for a player.
        // The zone is placed radially outward from the player's start tile on level 1.
        void BuildStartZone(int playerIndex, int startTile, int tokensPerPlayer)
        {
            // Calculate the angle of the start tile on level 1
            float angle = startTile * Mathf.PI * 2f / level1Size;

            // Position the zone outward from the ring
            // Distance = ring radius + tile depth/2 + gap + half the zone width
            float outerTileEdge = level1Radius + (tileSize * 0.92f / 2f);
            float zoneDistance = outerTileEdge + 4.2f + 0.3f;
            float zoneX = Mathf.Cos(angle) * zoneDistance;
            float zoneZ = Mathf.Sin(angle) * zoneDistance;
            float zoneY = 0f; // same height as level 1

            Vector3 zoneCenter = new Vector3(zoneX, zoneY, zoneZ);
            Quaternion zoneRot = Quaternion.LookRotation(new Vector3(zoneX, 0f, zoneZ));

            // Spawn the start zone platform if prefab is assigned
            if (startZonePrefab != null)
            {
                // Scale zone to fit the number of tokens comfortably
                // 2 tokens wide × 1 token deep = 2*tileSize × tileSize
                // float zoneWidth = tileSize * 2.2f;
                // float zoneDepth = tileSize * 1.2f;
                // float zoneHeight = tileSize * 0.3f; // flat platform

                GameObject zone = Instantiate(startZonePrefab, zoneCenter, zoneRot, transform);
                // zone.transform.localScale = new Vector3(zoneWidth, zoneHeight, zoneDepth);

                // Color the zone to match the player
                var renderers = zone.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                    r.material.color = playerColors[playerIndex];
            }

            // Calculate token slot positions on the zone surface
            // Slots are arranged side by side along the zone's local X axis
            List<Vector3> slots = new List<Vector3>();
            float platformTop = zoneY + tileSize * 0.15f + 0.3f; // top of platform + token offset

            for (int i = 0; i < tokensPerPlayer; i++)
            {
                // Spread tokens evenly across the zone width
                float t = tokensPerPlayer == 1 ? 0f : (float)i / (tokensPerPlayer - 1) - 0.5f;
                float localX = t * tileSize * 0.8f;

                // Rotate slot offset to match zone orientation
                float slotX = zoneX + Mathf.Cos(angle) * 0f - Mathf.Sin(angle) * localX;
                float slotZ = zoneZ + Mathf.Sin(angle) * 0f + Mathf.Cos(angle) * localX;

                slots.Add(new Vector3(slotX, platformTop, slotZ));
            }

            startZoneSlots[playerIndex] = slots;
            Debug.Log($"🏠 Start zone built for P{playerIndex} at angle {angle * Mathf.Rad2Deg:F1}°");
        }

        TileView GetTile(int level, int tileIndex)
        {
            int r = level - 1;
            if (r < 0 || r >= ringTiles.Count) return null;
            var ring = ringTiles[r];
            if (tileIndex < 0 || tileIndex >= ring.Count) return null;
            return ring[tileIndex];
        }

        // ─────────────────────────────────────────────────────────
        // NETWORK EVENTS
        // ─────────────────────────────────────────────────────────

        public void OnInitReceived(JObject data)
        {
            int myPlayerIndex = data["playerIndex"].Value<int>();

            // Step 1: Build pyramid
            var ringConfigs = data["ringConfigs"] as JArray;
            GeneratePyramid(ringConfigs);

            // Step 2: Board markings
            ApplyBoardMarkings(data);

            // Step 3: Player slots
            while (players.Count < 2)
                players.Add(null);

            // Step 4: Register all known players
            var allPlayersRaw = data["allPlayers"] as JArray;
            int tokensPerPlayer = data["tokensPerPlayer"].Value<int>();

            foreach (var pd in allPlayersRaw)
            {
                int pIndex = pd["playerIndex"].Value<int>();

                var homeTilesRaw = pd["homeTiles"] as JObject;
                var homeTiles = new Dictionary<int, int>();
                foreach (var kv in homeTilesRaw)
                    homeTiles[int.Parse(kv.Key)] = kv.Value.Value<int>();

                Player p = new Player(pIndex);
                p.homeTiles = homeTiles;

                var tokensRaw = pd["tokens"] as JArray;
                foreach (var t in tokensRaw)
                {
                    int tokenId = t["tokenId"].Value<int>();
                    int level = t["currentLevel"].Value<int>();
                    int tile = t["tilePosition"].Value<int>();
                    p.tokens.Add(new Token(tokenId, level, tile));
                }

                players[pIndex] = p;

                // Build start zone then spawn tokens into it
                int startTile = p.homeTiles.ContainsKey(1) ? p.homeTiles[1] : 0;
                BuildStartZone(pIndex, startTile, tokensPerPlayer);
                SpawnTokensInStartZone(p);
            }

            // Step 5: Placeholder for opponent not yet connected
            int opponentIndex = 1 - myPlayerIndex;
            if (players[opponentIndex] == null)
            {
                Player opponent = new Player(opponentIndex);
                opponent.homeTiles = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };
                players[opponentIndex] = opponent;
                Debug.Log($"⏳ Opponent (P{opponentIndex}) not yet connected");
            }

            Debug.Log($"✅ Init complete — I am Player {myPlayerIndex}");

            if (UIManager.Instance != null)
                UIManager.Instance.Setup(myPlayerIndex);
        }

        public void OnPlayerJoined(JObject playerData)
        {
            int pIndex = playerData["playerIndex"].Value<int>();
            if (players[pIndex] != null && players[pIndex].tokens.Count > 0) return;

            var homeTilesRaw = playerData["homeTiles"] as JObject;
            var homeTiles = new Dictionary<int, int>();
            foreach (var kv in homeTilesRaw)
                homeTiles[int.Parse(kv.Key)] = kv.Value.Value<int>();

            Player p = new Player(pIndex);
            p.homeTiles = homeTiles;

            var tokensRaw = playerData["tokens"] as JArray;
            int count = 0;
            foreach (var t in tokensRaw)
            {
                int tokenId = t["tokenId"].Value<int>();
                int level = t["currentLevel"].Value<int>();
                int tile = t["tilePosition"].Value<int>();
                p.tokens.Add(new Token(tokenId, level, tile));
                count++;
            }

            players[pIndex] = p;

            int startTile = p.homeTiles.ContainsKey(1) ? p.homeTiles[1] : 0;
            BuildStartZone(pIndex, startTile, count);
            SpawnTokensInStartZone(p);

            Debug.Log($"👥 Player {pIndex} joined — tokens spawned in start zone");
        }

        void ApplyBoardMarkings(JObject data)
        {
            var safeTilesData = data["safeTiles"] as JArray;
            if (safeTilesData != null)
                foreach (var entry in safeTilesData)
                {
                    int level = entry["level"].Value<int>();
                    foreach (var t in entry["tiles"] as JArray)
                        GetTile(level, t.Value<int>())?.SetAsSafe();
                }

            var laddersData = data["ladders"] as JArray;
            if (laddersData != null)
                foreach (var l in laddersData)
                {
                    int fromLevel = l["fromLevel"].Value<int>();
                    int fromTile = l["fromTile"].Value<int>();
                    int toLevel = l["toLevel"].Value<int>();
                    int toTile = l["toTile"].Value<int>();

                    // Mark the source tile green as before
                    var sourceTile = GetTile(fromLevel, fromTile);
                    if (sourceTile != null)
                    {
                        sourceTile.transitionToLevel = toLevel;
                        sourceTile.transitionToTile = toTile;
                        sourceTile.SetAsLadder();
                    }

                    // Attach carved ladder to the DESTINATION tile's outward face
                    var destTile = GetTile(toLevel, toTile);
                    if (destTile != null && ladderCarvingPrefab != null)
                    {
                        // Instantiate as child of destination tile so it moves with it
                        GameObject carving = Instantiate(
                            ladderCarvingPrefab,
                            destTile.transform.position,
                            destTile.transform.rotation,
                            destTile.transform
                        );

                        // Reset to local origin — prefab is designed in tile-local space
                        // z = -0.55 in prefab already places it on the outward face
                        carving.transform.localPosition = Vector3.zero;
                        carving.transform.localRotation = Quaternion.identity;
                        carving.transform.localScale = Vector3.one;

                        Debug.Log($"🪜 Ladder carving placed on L{toLevel}:{toTile}");
                    }
                }

            var snakesData = data["snakes"] as JArray;
            if (snakesData != null)
            {
                foreach (var s in snakesData)
                {
                    int fromLevel = s["fromLevel"].Value<int>();
                    int fromTile = s["fromTile"].Value<int>();
                    int toLevel = s["toLevel"].Value<int>();
                    int toTile = s["toTile"].Value<int>();

                    // 1. STRICTLY fetch ONLY the starting tile
                    TileView headTile = GetTile(fromLevel, fromTile);

                    if (headTile != null)
                    {
                        // 2. Set the logic for where the trapdoor leads
                        headTile.transitionToLevel = toLevel;
                        headTile.transitionToTile = toTile;

                        // 3. Spawn the Taipan ONLY on this head tile
                        if (taipanPrefab != null)
                        {
                            GameObject snakeObj = Instantiate(
                                taipanPrefab,
                                headTile.transform.position,
                                headTile.transform.rotation,
                                headTile.transform // parent it to the head tile
                            );

                            // Scale the snake so it fits
                            snakeObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

                            // Place it DEAD CENTER (X=0, Z=0) and on the surface (Y=0.5f)
                            snakeObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        // TOKEN SPAWNING
        // ─────────────────────────────────────────────────────────

        void SpawnTokensInStartZone(Player player)
        {
            if (!startZoneSlots.ContainsKey(player.playerIndex))
            {
                Debug.LogError($"❌ No start zone for P{player.playerIndex}");
                return;
            }

            var slots = startZoneSlots[player.playerIndex];

            for (int i = 0; i < player.tokens.Count; i++)
            {
                Token token = player.tokens[i];

                GameObject obj = Instantiate(playerPrefab);
                PlayerView view = obj.GetComponent<PlayerView>();

                // Set identity so taps can identify which token was tapped
                view.playerIndex = player.playerIndex;
                view.tokenId = token.tokenId;
                view.OnTapped = OnTokenTapped;

                view.SetColor(playerColors[player.playerIndex]);

                // Place token in its start zone slot
                Vector3 slotPos = i < slots.Count ? slots[i] : slots[0];
                view.SetPosition(slotPos);

                tokenViews[token] = view;
                tokenTiles[token] = null; // not on any tile yet

                Debug.Log($"🏠 Spawned P{player.playerIndex} T{i} in start zone");
            }
        }

        // ─────────────────────────────────────────────────────────
        // OCCUPANCY HELPERS
        // ─────────────────────────────────────────────────────────

        // Register token onto a tile — tile calculates and applies slot positions
        void RegisterOnTile(Token token, TileView tile)
        {
            PlayerView view = tokenViews[token];

            // Unregister from previous tile if any
            if (tokenTiles.ContainsKey(token) && tokenTiles[token] != null)
                tokenTiles[token].UnregisterToken(view);

            tile.RegisterToken(view);
            tokenTiles[token] = tile;
        }

        // Unregister token from its current tile (e.g. when sent to start zone)
        void UnregisterFromTile(Token token)
        {
            if (!tokenViews.ContainsKey(token)) return;
            if (tokenTiles.ContainsKey(token) && tokenTiles[token] != null)
                tokenTiles[token].UnregisterToken(tokenViews[token]);
            tokenTiles[token] = null;
        }

        // ─────────────────────────────────────────────────────────
        // GAME EVENTS
        // ─────────────────────────────────────────────────────────

        public void OnGameStart(int firstPlayerIndex)
        {
            gameStarted = true;
            currentPlayerIndex = firstPlayerIndex;
            UpdateTokenSelectionVisual();
            Debug.Log($"🚀 Game started — Player {firstPlayerIndex}'s turn");

            if (UIManager.Instance != null)
                UIManager.Instance.OnGameStart(firstPlayerIndex);
        }

        public void OnTurnChanged(int playerIndex)
        {
            currentPlayerIndex = playerIndex;
            isWaitingForToken = false;
            validTokenIds.Clear();
            UpdateTokenSelectionVisual();
            Debug.Log($"🔧 Turn forced → Player {playerIndex}");

            if (UIManager.Instance != null)
                UIManager.Instance.OnTurnChanged(playerIndex);
        }

        public void OnRollResult(int playerIndex, int roll, bool autoPass, JArray validMoves)
        {
            Debug.Log($"🎲 P{playerIndex} rolled {roll}" + (autoPass ? " — auto-pass" : ""));

            // Play dice animation for everyone — both players see the result
            if (diceView != null)
                diceView.PlayRoll(roll);

            // Show roll message BEFORE any early returns
            // so both players always see roll feedback regardless of whose turn it is
            if (UIManager.Instance != null)
                UIManager.Instance.OnRollResult(playerIndex, roll, autoPass);

            if (autoPass) { isWaitingForToken = false; return; }

            if (playerIndex != NetworkManager.Instance.myPlayerIndex) return;

            validTokenIds.Clear();
            foreach (var m in validMoves)
                if (m["canMove"].Value<bool>())
                    validTokenIds.Add(m["tokenId"].Value<int>());

            isWaitingForToken = true;
            UpdateTokenSelectionVisual();

            Debug.Log($"🎮 Tap your token to move (valid: [{string.Join(", ", validTokenIds)}])");
        }

        public void OnMoveResult(int playerIndex, JObject moveResult, int? nextPlayerIndex)
        {
            StartCoroutine(HandleMoveResult(playerIndex, moveResult, nextPlayerIndex));
        }

        IEnumerator HandleMoveResult(int playerIndex, JObject moveResult, int? nextPlayerIndex)
        {
            Player player = GetPlayer(playerIndex);
            if (player == null) { Debug.LogError($"❌ Player {playerIndex} not found"); yield break; }

            int tokenId = moveResult["tokenId"].Value<int>();
            string type = moveResult["type"].Value<string>();

            Token token = player.tokens.Find(t => t.tokenId == tokenId);
            if (token == null) { Debug.LogError($"❌ Token {tokenId} not found for P{playerIndex}"); yield break; }

            PlayerView view = tokenViews.ContainsKey(token) ? tokenViews[token] : null;
            if (view == null) { Debug.LogError($"❌ No view for P{playerIndex} T{tokenId}"); yield break; }

            switch (type)
            {
                case "normal":
                    {
                        int toLevel = moveResult["level"].Value<int>();
                        int toTile = moveResult["tile"].Value<int>();

                        // If token is entering the board for the first time, arc from start zone
                        if (!token.isOnBoard)
                        {
                            TileView target = GetTile(toLevel, toTile);
                            if (target != null)
                            {
                                UnregisterFromTile(token);
                                yield return view.ArcToPosition(
                                    target.GetSlotWorldPosition(0, 1), 2f);
                                RegisterOnTile(token, target);
                            }
                            token.isOnBoard = true;
                        }
                        else
                        {
                            yield return AnimateStepMove(view, token, toLevel, toTile);
                            TileView target = GetTile(toLevel, toTile);
                            if (target != null) RegisterOnTile(token, target);
                        }

                        token.currentLevel = toLevel;
                        token.tilePosition = toTile;

                        var collision = moveResult["collision"] as JObject;
                        bool hadCollision = collision != null && collision.Type != JTokenType.Null;
                        if (hadCollision)
                            yield return HandleCollisionVisual(collision);

                        if (UIManager.Instance != null)
                            UIManager.Instance.OnMoveEvent(playerIndex, "normal", toLevel, hadCollision);

                        break;
                    }

                case "levelUp":
                    {
                        int fromLevel = moveResult["fromLevel"].Value<int>();
                        int toLevel = moveResult["toLevel"].Value<int>();
                        int toTile = moveResult["toTile"].Value<int>();

                        int homeTileOnFrom = player.homeTiles.ContainsKey(fromLevel) ? player.homeTiles[fromLevel] : 0;
                        TileView homeTileView = GetTile(fromLevel, homeTileOnFrom);
                        if (homeTileView != null)
                        {
                            RegisterOnTile(token, homeTileView);
                            yield return view.MoveToTile(homeTileView);
                        }

                        TileView nextTile = GetTile(toLevel, toTile);
                        if (nextTile != null)
                        {
                            UnregisterFromTile(token);
                            yield return view.ArcTo(nextTile, 3f);
                            RegisterOnTile(token, nextTile);
                        }

                        token.currentLevel = toLevel;
                        token.tilePosition = toTile;
                        token.isOnBoard = true;

                        Debug.Log($"⬆️ P{playerIndex} T{tokenId} → L{toLevel}:{toTile}");
                        if (UIManager.Instance != null)
                            UIManager.Instance.OnMoveEvent(playerIndex, "levelUp", toLevel, false);
                        break;
                    }

                case "ladderClimb":
                    {
                        int fromLevel = moveResult["fromLevel"].Value<int>();
                        int fromTile = moveResult["fromTile"].Value<int>();
                        int toLevel = moveResult["toLevel"].Value<int>();
                        int toTile = moveResult["toTile"].Value<int>();

                        TileView from = GetTile(fromLevel, fromTile);
                        TileView to = GetTile(toLevel, toTile);

                        if (from != null)
                        {
                            RegisterOnTile(token, from);
                            yield return view.MoveToTile(from);
                        }
                        if (to != null)
                        {
                            UnregisterFromTile(token);
                            yield return view.ArcTo(to, 4f);
                            RegisterOnTile(token, to);
                        }

                        token.currentLevel = toLevel;
                        token.tilePosition = toTile;

                        Debug.Log($"🪜 Ladder! P{playerIndex} T{tokenId} → L{toLevel}:{toTile}");
                        if (UIManager.Instance != null)
                            UIManager.Instance.OnMoveEvent(playerIndex, "ladderClimb", toLevel, false);
                        break;
                    }

                case "snakeSlide":
                    {
                        int fromLevel = moveResult["fromLevel"].Value<int>();
                        int fromTile = moveResult["fromTile"].Value<int>();
                        int toLevel = moveResult["toLevel"].Value<int>();
                        int toTile = moveResult["toTile"].Value<int>();

                        TileView from = GetTile(fromLevel, fromTile);
                        TileView to = GetTile(toLevel, toTile);

                        if (from != null)
                        {
                            RegisterOnTile(token, from);
                            yield return view.MoveToTile(from);
                        }
                        if (to != null)
                        {
                            UnregisterFromTile(token);
                            yield return view.ArcTo(to, 2f);
                            RegisterOnTile(token, to);
                        }

                        token.currentLevel = toLevel;
                        token.tilePosition = toTile;

                        Debug.Log($"🐍 Snake! P{playerIndex} T{tokenId} → L{toLevel}:{toTile}");
                        if (UIManager.Instance != null)
                            UIManager.Instance.OnMoveEvent(playerIndex, "snakeSlide", toLevel, false);
                        break;
                    }

                case "overshoot":
                    Debug.Log($"🚫 Overshoot — P{playerIndex} T{tokenId} stays put");
                    if (UIManager.Instance != null)
                        UIManager.Instance.OnMoveEvent(playerIndex, "overshoot", 0, false);
                    break;

                case "win":
                    {
                        int toLevel = moveResult["level"].Value<int>();
                        int toTile = moveResult["tile"].Value<int>();

                        TileView winTile = GetTile(toLevel, toTile);
                        if (winTile != null)
                        {
                            UnregisterFromTile(token);
                            yield return view.MoveToTile(winTile);
                            RegisterOnTile(token, winTile);
                        }

                        token.currentLevel = toLevel;
                        token.tilePosition = toTile;
                        token.hasWon = true;

                        Debug.Log($"🏆 P{playerIndex} T{tokenId} finished!");
                        if (UIManager.Instance != null)
                            UIManager.Instance.OnMoveEvent(playerIndex, "win", toLevel, false);
                        break;
                    }
            }

            if (nextPlayerIndex.HasValue)
            {
                currentPlayerIndex = nextPlayerIndex.Value;
                isWaitingForToken = false;
                validTokenIds.Clear();
                UpdateTokenSelectionVisual();
                Debug.Log($"➡️ Turn → Player {currentPlayerIndex}");

                if (UIManager.Instance != null)
                    UIManager.Instance.OnNextTurn(currentPlayerIndex);
            }
        }

        IEnumerator AnimateStepMove(PlayerView view, Token token, int toLevel, int toTile)
        {
            if (token.currentLevel != toLevel)
            {
                TileView tile = GetTile(toLevel, toTile);
                if (tile != null) yield return view.MoveToTile(tile);
                yield break;
            }

            var ring = ringTiles[token.currentLevel - 1];
            int ringSize = ring.Count;
            int current = token.tilePosition;

            while (current != toTile)
            {
                current = (current + 1) % ringSize;
                TileView nextTile = ring[current];

                // Temporarily register on each tile during step animation
                // so other tokens on those tiles reflow correctly
                UnregisterFromTile(token);
                RegisterOnTile(token, nextTile);

                yield return view.MoveToTile(nextTile);
            }
        }

        IEnumerator HandleCollisionVisual(JObject collision)
        {
            int playerIndex = collision["playerIndex"].Value<int>();
            int tokenId = collision["tokenId"].Value<int>();
            int toLevel = collision["toLevel"].Value<int>();
            int toTile = collision["toTile"].Value<int>();

            Player hitPlayer = GetPlayer(playerIndex);
            if (hitPlayer == null) yield break;

            Token hitToken = hitPlayer.tokens.Find(t => t.tokenId == tokenId);
            if (hitToken == null || !tokenViews.ContainsKey(hitToken)) yield break;

            // Unregister from current tile
            UnregisterFromTile(hitToken);

            TileView targetTile = GetTile(toLevel, toTile);
            if (targetTile != null)
            {
                yield return tokenViews[hitToken].ArcTo(targetTile, 2f);
                RegisterOnTile(hitToken, targetTile);
            }

            hitToken.currentLevel = toLevel;
            hitToken.tilePosition = toTile;

            Debug.Log($"💥 Collision — P{playerIndex} T{tokenId} → L{toLevel}:{toTile}");
        }

        // ─────────────────────────────────────────────────────────
        // DEBUG SYNC
        // ─────────────────────────────────────────────────────────

        public void OnDebugSync(Newtonsoft.Json.Linq.JArray tokens)
        {
            foreach (var t in tokens)
            {
                int playerIndex = t["playerIndex"].Value<int>();
                int tokenId = t["tokenId"].Value<int>();
                int level = t["level"].Value<int>();
                int tile = t["tile"].Value<int>();

                Player player = GetPlayer(playerIndex);
                if (player == null) { Debug.LogError($"❌ DebugSync: P{playerIndex} not found"); continue; }

                Token token = player.tokens.Find(tk => tk.tokenId == tokenId);
                if (token == null) { Debug.LogError($"❌ DebugSync: T{tokenId} not found"); continue; }

                TileView targetTile = GetTile(level, tile);
                if (targetTile == null) { Debug.LogError($"❌ DebugSync: L{level}:{tile} not found"); continue; }

                // Unregister from previous tile and register on new one
                UnregisterFromTile(token);
                RegisterOnTile(token, targetTile);

                token.currentLevel = level;
                token.tilePosition = tile;
                token.isOnBoard = true;

                Debug.Log($"📍 DebugSync: P{playerIndex} T{tokenId} → L{level}:{tile}");
            }
        }

        // ─────────────────────────────────────────────────────────
        // WIN
        // ─────────────────────────────────────────────────────────

        public void OnPlayerWon(int winnerIndex)
        {
            Debug.Log($"🏆🏆🏆 PLAYER {winnerIndex} WINS!");

            if (UIManager.Instance != null)
                UIManager.Instance.OnPlayerWon(winnerIndex);
        }

        // ─────────────────────────────────────────────────────────
        // INPUT — TOUCH / CLICK
        // ─────────────────────────────────────────────────────────

        public void OnRollDiceClicked()
        {
            if (!gameStarted)
            {
                Debug.Log("⏳ Waiting for game to start...");
                return;
            }
            if (currentPlayerIndex != NetworkManager.Instance.myPlayerIndex)
            {
                Debug.Log("🚫 Not your turn");
                return;
            }
            if (isWaitingForToken)
            {
                Debug.Log("⚠️ Tap a token to move it");
                return;
            }
            NetworkManager.Instance.RollDice();
        }

        // Called by PlayerView.OnPointerClick via the OnTapped callback
        public void OnTokenTapped(int tappedPlayerIndex, int tappedTokenId)
        {
            // Only respond if it's the local player's turn and we're waiting for selection
            if (!isWaitingForToken) return;
            if (currentPlayerIndex != NetworkManager.Instance.myPlayerIndex) return;
            if (tappedPlayerIndex != NetworkManager.Instance.myPlayerIndex) return;

            TrySelectToken(tappedTokenId);
        }

        void TrySelectToken(int tokenId)
        {
            if (!validTokenIds.Contains(tokenId))
            {
                Debug.Log($"🚫 Token {tokenId} cannot move this turn");
                return;
            }

            isWaitingForToken = false;
            UpdateTokenSelectionVisual();

            // Hide dice once player has selected their token
            if (diceView != null)
                diceView.Hide();

            NetworkManager.Instance.MoveToken(tokenId);
        }

        // ─────────────────────────────────────────────────────────
        // VISUALS
        // ─────────────────────────────────────────────────────────

        void UpdateTokenSelectionVisual()
        {
            Player myPlayer = GetPlayer(NetworkManager.Instance.myPlayerIndex);
            if (myPlayer == null) return;

            foreach (var token in myPlayer.tokens)
            {
                if (!tokenViews.ContainsKey(token)) continue;
                bool selectable = isWaitingForToken && validTokenIds.Contains(token.tokenId);
                tokenViews[token].SetSelected(selectable);
            }
        }

        // ─────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────

        Player GetPlayer(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= players.Count) return null;
            return players[playerIndex];
        }
    }
}