const path = require("path");

// ─────────────────────────────────────────
// Debug Server
// Mounted by server.js only when DEBUG_MODE=true
// Receives references to game internals — never imports them directly
// ─────────────────────────────────────────

module.exports = function mountDebug({ app, io, gameState, RING_CONFIGS, SAFE_TILES, LADDERS, SNAKES, setForcedDice, getForcedDice }) {

    // ── Serve debug UI ────────────────────────────────────────

    app.get("/debug", (req, res) => {
        res.sendFile(path.join(__dirname, "debug.html"));
    });

    // ── Expose board constants to browser via REST ────────────
    // debug.html fetches this on load to populate its info panel

    app.get("/debug/board", (req, res) => {
        res.json({
            ringConfigs: Object.entries(RING_CONFIGS).map(([level, cfg]) => ({
                level: parseInt(level),
                boardSize: cfg.boardSize,
                height: cfg.height,
                radius: parseFloat(cfg.radius.toFixed(2))
            })),
            safeTiles: SAFE_TILES,
            ladders: LADDERS,
            snakes: SNAKES
        });
    });

    // ── Helper: apply a single teleport to server state ─────
    // Shared by both debugTeleport and debugBatchTeleport
    function applyTeleport(playerIndex, tokenId, level, tile) {
        const player = gameState.players[playerIndex];
        if (!player) return { error: `Player ${playerIndex} not found` };

        const token = player.tokens.find(t => t.tokenId === tokenId);
        if (!token) return { error: `Token ${tokenId} not found` };

        const boardSize = RING_CONFIGS[level]?.boardSize;
        if (!boardSize) return { error: `Invalid level ${level}` };

        if (tile < 0 || tile >= boardSize)
            return { error: `Tile ${tile} out of range (0-${boardSize - 1}) for level ${level}` };

        token.currentLevel = level;
        token.tilePosition = tile;
        return { ok: true };
    }

    // ── Debug socket events ───────────────────────────────────

    io.on("connection", (socket) => {

        // Force next dice value — null resets to random
        socket.on("debugSetDice", ({ value }) => {
            const val = (value !== null && value >= 1 && value <= 6) ? value : null;
            setForcedDice(val);
            const msg = val !== null ? `Forced dice → ${val}` : "Dice reset to random";
            console.log(`🔧 [DEBUG] ${msg}`);
            socket.emit("debugAck", { action: "setDice", value: val, msg });
        });

        // Teleport a single token — kept for manual use from the teleport form
        socket.on("debugTeleport", ({ playerIndex, tokenId, level, tile }) => {
            const result = applyTeleport(playerIndex, tokenId, level, tile);
            if (result.error) return socket.emit("debugAck", { action: "teleport", error: result.error });

            // Single teleport sends a debugSync with just this one token
            io.emit("debugSync", { tokens: [{ playerIndex, tokenId, level, tile }] });
            socket.emit("debugAck", { action: "teleport", playerIndex, tokenId, level, tile });
        });

        // Batch teleport — moves multiple tokens at once in a single event
        // Used by scenario buttons to avoid race conditions from rapid individual emits
        socket.on("debugBatchTeleport", ({ teleports }) => {
            const errors = [];

            for (const t of teleports) {
                const result = applyTeleport(t.playerIndex, t.tokenId, t.level, t.tile);
                if (result.error) errors.push(result.error);
                else console.log(`🔧 [DEBUG] Teleport P${t.playerIndex} T${t.tokenId} → L${t.level}:${t.tile}`);
            }

            if (errors.length > 0)
                return socket.emit("debugAck", { action: "batchTeleport", errors });

            // One single event to Unity with all new positions — no race condition possible
            io.emit("debugSync", { tokens: teleports });
            socket.emit("debugAck", { action: "batchTeleport", count: teleports.length });
        });

        // Force game phase
        socket.on("debugSetPhase", ({ phase }) => {
            if (!["waiting", "playing", "finished"].includes(phase))
                return socket.emit("debugAck", { action: "setPhase", error: "Invalid phase" });

            gameState.gamePhase = phase;
            console.log(`🔧 [DEBUG] Phase → ${phase}`);

            if (phase === "playing")
                io.emit("gameStart", { currentPlayerIndex: gameState.currentPlayerIndex });

            socket.emit("debugAck", { action: "setPhase", phase });
        });

        // Set current turn to any player index
        socket.on("debugSetTurn", ({ playerIndex }) => {
            if (playerIndex < 0 || playerIndex >= gameState.players.length)
                return socket.emit("debugAck", { action: "setTurn", error: `Player ${playerIndex} not found` });

            gameState.currentPlayerIndex = playerIndex;
            gameState.pendingMove = null; // clear any pending roll
            console.log(`🔧 [DEBUG] Turn → P${playerIndex}`);

            // Notify all Unity clients so their turn state updates
            io.emit("turnChanged", { currentPlayerIndex: playerIndex });

            socket.emit("debugAck", { action: "setTurn", playerIndex });
        });

        // Full reset — clears all players and state
        socket.on("debugResetGame", () => {
            gameState.players = [];
            gameState.currentPlayerIndex = 0;
            gameState.gamePhase = "waiting";
            gameState.pendingMove = null;
            setForcedDice(null);
            console.log("🔧 [DEBUG] Game fully reset");
            socket.emit("debugAck", { action: "resetGame" });
        });

        // Return current game state snapshot
        socket.on("debugGetState", () => {
            const state = {
                gamePhase: gameState.gamePhase,
                currentPlayerIndex: gameState.currentPlayerIndex,
                pendingMove: gameState.pendingMove,
                forcedDice: getForcedDice(),
                players: gameState.players.map(p => ({
                    playerIndex: p.playerIndex,
                    tokens: p.tokens.map(t => ({
                        tokenId: t.tokenId,
                        currentLevel: t.currentLevel,
                        tilePosition: t.tilePosition,
                        hasWon: t.hasWon
                    }))
                }))
            };
            console.log("🔧 [DEBUG] State:", JSON.stringify(state, null, 2));
            socket.emit("debugState", state);
        });
    });
};