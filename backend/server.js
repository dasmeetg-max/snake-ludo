const express = require("express");
const http = require("http");
const { Server } = require("socket.io");

const app = express();
const server = http.createServer(app);
const io = new Server(server, {
    cors: { origin: "*" },
    pingInterval: 25000,
    pingTimeout: 60000
});

// ─────────────────────────────────────────
// Debug Toggle
// Set to false for production — nothing else needs changing
// ─────────────────────────────────────────

const DEBUG_MODE = true;

// ─────────────────────────────────────────
// Board Configuration
// Three concentric squares separated by water moats.
//   tilesPerSide = boardSize / 4
//   halfSide     = (boardSize * tileSize) / 8      — square analogue of radius
//   moatWidth    = (outer - inner) * tileSize / 8  — derived, not free
// Ring sizes must each be divisible by 4 (equal sides), differ by an equal
// amount (equal moats), and that difference must itself be a multiple of 4.
// 32 / 24 / 16 → sides of 8 / 6 / 4, differences of 8, both moats one tile wide.
// The client receives boardSize, tileSize and halfSide and lays out the squares.
// ─────────────────────────────────────────

const TILE_SIZE = 2.0;

const BOARD_CONFIG = {
    1: { boardSize: 32 },
    2: { boardSize: 24 },
    3: { boardSize: 16 }
};

// Distance from the board centre to the middle of a side — the square analogue
// of the old ring radius. Side length is boardSize/4 tiles, so 2*halfSide is
// (boardSize/4) * tileSize.
function getHalfSide(boardSize) {
    return (boardSize * TILE_SIZE) / 8;
}

const RING_CONFIGS = Object.fromEntries(
    Object.entries(BOARD_CONFIG).map(([level, config]) => [
        parseInt(level),
        {
            boardSize: config.boardSize,
            tileSize: TILE_SIZE,
            halfSide: getHalfSide(config.boardSize)
        }
    ])
);

const MAX_LEVEL = Object.keys(BOARD_CONFIG).length;
const TOKENS_PER_PLAYER = 2;

// ─────────────────────────────────────────
// Face boundaries — tile 0 sits on a corner
//   L1 (32): A 0–7   B 8–15   C 16–23  D 24–31
//   L2 (24): A 0–5   B 6–11   C 12–17  D 18–23
//   L3 (16): A 0–3   B 4–7    C 8–11   D 12–15
//
// The whole board is invariant under 180° rotation: add 16 to an L1 index,
// 12 to an L2 index, or 8 to an L3 index (mod ring size) and you land on the
// mirrored feature. Preserve this in any adjustment.
// ─────────────────────────────────────────

// ─────────────────────────────────────────
// Safe Tiles (tile-local index per level)
// Four corners (quarter points) plus both home tiles per ring
// ─────────────────────────────────────────

const SAFE_TILES = {
    1: [0, 4, 8, 16, 20, 24],
    2: [0, 3, 6, 12, 15, 18],
    3: [0, 2, 4, 8, 10, 12]
};

// ─────────────────────────────────────────
// Ladders — inward shortcuts, one level up
//
// Both endpoints share a face AND sit at the same offset along it, so every
// crossing is perpendicular and exactly one moat wide. A tile at face-local
// index j sits at x = -halfSide + tileSize*j, so with per-side counts differing
// by 2 at every moat the alignment rule is: innerIndex = outerIndex - 1.
// Break that and the crossing goes diagonal and needs its own sprite.
// ─────────────────────────────────────────

const LADDERS = [
    { fromLevel: 1, fromTile: 11, toLevel: 2, toTile: 8 },   // face B
    { fromLevel: 1, fromTile: 27, toLevel: 2, toTile: 20 },  // face D
    { fromLevel: 2, fromTile: 4, toLevel: 3, toTile: 3 },    // face A
    { fromLevel: 2, fromTile: 16, toLevel: 3, toTile: 11 },  // face C
];

// ─────────────────────────────────────────
// Snakes — outward, one level down
// Same alignment rule as the ladders above
// ─────────────────────────────────────────

const SNAKES = [
    { fromLevel: 2, fromTile: 1, toLevel: 1, toTile: 2 },    // face A
    { fromLevel: 2, fromTile: 13, toLevel: 1, toTile: 18 },  // face C
    { fromLevel: 3, fromTile: 6, toLevel: 2, toTile: 9 },    // face B
    { fromLevel: 3, fromTile: 14, toLevel: 2, toTile: 21 },  // face D
];

// ─────────────────────────────────────────
// Player Starts
// homeTiles: completion point per level — also reset point after collision
// Homes sit at side midpoints so every bridge crosses its moat perpendicular
// and all four are the same length. P0 on face A, P1 antipodal on face C.
// ─────────────────────────────────────────

const PLAYER_STARTS = [
    {
        playerIndex: 0,
        startLevel: 1,
        startTile: 4,
        homeTiles: { 1: 4, 2: 3, 3: 2 }
    },
    {
        playerIndex: 1,
        startLevel: 1,
        startTile: 20,
        homeTiles: { 1: 20, 2: 15, 3: 10 }
    }
];

// ─────────────────────────────────────────
// Game State
// ─────────────────────────────────────────

const gameState = {
    players: [],
    currentPlayerIndex: 0,
    gamePhase: "waiting",  // waiting | playing | finished
    pendingMove: null        // { playerIndex, roll }
};

// Exposed to debugServer so it can override dice
let forcedDice = null;
function setForcedDice(val) { forcedDice = val; }
function getForcedDice() { return forcedDice; }

// ─────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────

function getBoardSize(level) {
    return RING_CONFIGS[level].boardSize;
}

function rollDice() {
    if (forcedDice !== null) {
        const val = forcedDice;
        forcedDice = null;
        console.log(`🎲 [DEBUG] Forced dice: ${val}`);
        return val;
    }
    return Math.floor(Math.random() * 6) + 1;
}

function isPlayersTurn(socketId) {
    const current = gameState.players[gameState.currentPlayerIndex];
    return current && current.id === socketId;
}

function getNextPlayer() {
    gameState.currentPlayerIndex =
        (gameState.currentPlayerIndex + 1) % gameState.players.length;
    return gameState.players[gameState.currentPlayerIndex];
}

function isSafe(level, tile) {
    return (SAFE_TILES[level] || []).includes(tile);
}

// ─────────────────────────────────────────
// Player / Token Factory
// ─────────────────────────────────────────

function createPlayer(socketId, playerIndex) {
    const start = PLAYER_STARTS[playerIndex] || {
        startLevel: 1, startTile: 0, homeTiles: { 1: 0, 2: 0, 3: 0 }
    };

    const tokens = [];
    for (let i = 0; i < TOKENS_PER_PLAYER; i++) {
        tokens.push({
            tokenId: i,
            currentLevel: start.startLevel,
            tilePosition: start.startTile,
            hasWon: false
        });
    }

    return {
        id: socketId,
        playerIndex,
        tokens,
        homeTiles: start.homeTiles
    };
}

// ─────────────────────────────────────────
// Movement
// ─────────────────────────────────────────

function calculateMove(token, roll, homeTile, nextHomeTile) {
    const level = token.currentLevel;
    const boardSize = getBoardSize(level);
    const stepsToComplete = ((homeTile - token.tilePosition) + boardSize) % boardSize || boardSize;

    if (level === MAX_LEVEL) {
        if (roll === stepsToComplete) return { type: "win", tile: homeTile, level };
        if (roll > stepsToComplete) return { type: "overshoot", tile: token.tilePosition, level };
        return { type: "normal", tile: (token.tilePosition + roll) % boardSize, level };
    }

    if (roll >= stepsToComplete) {
        const remainingSteps = roll - stepsToComplete;
        const nextLevel = level + 1;
        const nextBoardSize = getBoardSize(nextLevel);
        const toTile = (nextHomeTile + remainingSteps) % nextBoardSize;
        return { type: "levelUp", fromLevel: level, toLevel: nextLevel, toTile, stepsBeforeLevel: stepsToComplete, remainingSteps };
    }

    return { type: "normal", tile: (token.tilePosition + roll) % boardSize, level };
}

// ─────────────────────────────────────────
// Valid Moves
// ─────────────────────────────────────────

function getValidMoves(player, roll) {
    return player.tokens.map(token => {
        if (token.hasWon) return { tokenId: token.tokenId, canMove: false, reason: "won" };

        if (token.currentLevel === MAX_LEVEL) {
            const homeTile = player.homeTiles[token.currentLevel];
            const boardSize = getBoardSize(token.currentLevel);
            const stepsToComplete = ((homeTile - token.tilePosition) + boardSize) % boardSize || boardSize;
            if (roll > stepsToComplete) return { tokenId: token.tokenId, canMove: false, reason: "overshoot" };
        }

        return { tokenId: token.tokenId, canMove: true, reason: "normal" };
    });
}

// ─────────────────────────────────────────
// Collision
// ─────────────────────────────────────────

function checkCollision(movingPlayer, movingToken) {
    if (isSafe(movingToken.currentLevel, movingToken.tilePosition)) return null;

    for (const player of gameState.players) {
        if (player.id === movingPlayer.id) continue;

        for (const token of player.tokens) {
            if (
                token.currentLevel === movingToken.currentLevel &&
                token.tilePosition === movingToken.tilePosition &&
                !token.hasWon
            ) {
                const prevLevel = Math.max(1, token.currentLevel - 1);
                const prevHomeTile = player.homeTiles[prevLevel] || 0;

                token.currentLevel = prevLevel;
                token.tilePosition = prevHomeTile;

                console.log(`💥 Collision: P${player.playerIndex} T${token.tokenId} → L${prevLevel}:${prevHomeTile}`);

                return { playerIndex: player.playerIndex, tokenId: token.tokenId, toLevel: prevLevel, toTile: prevHomeTile };
            }
        }
    }
    return null;
}

// ─────────────────────────────────────────
// Execute Move
// ─────────────────────────────────────────

function executeMove(player, token, roll) {
    const homeTile = player.homeTiles[token.currentLevel];
    const nextLevel = token.currentLevel + 1;
    const nextHomeTile = player.homeTiles[nextLevel] || 0;
    const moveCalc = calculateMove(token, roll, homeTile, nextHomeTile);

    if (moveCalc.type === "win") {
        token.tilePosition = homeTile;
        token.hasWon = true;
        const allWon = player.tokens.every(t => t.hasWon);
        console.log(`🏆 P${player.playerIndex} T${token.tokenId} won! All won: ${allWon}`);
        return { type: "win", tokenId: token.tokenId, tile: homeTile, level: token.currentLevel, playerWon: allWon };
    }

    if (moveCalc.type === "overshoot") {
        console.log(`🚫 Overshoot: P${player.playerIndex} T${token.tokenId}`);
        return { type: "overshoot", tokenId: token.tokenId, tile: token.tilePosition, level: token.currentLevel };
    }

    if (moveCalc.type === "levelUp") {
        token.currentLevel = moveCalc.toLevel;
        token.tilePosition = moveCalc.toTile;
        console.log(`⬆️ P${player.playerIndex} T${token.tokenId} → L${moveCalc.toLevel}:${moveCalc.toTile}`);
        return { type: "levelUp", tokenId: token.tokenId, fromLevel: moveCalc.fromLevel, toLevel: moveCalc.toLevel, toTile: moveCalc.toTile, stepsBeforeLevel: moveCalc.stepsBeforeLevel, remainingSteps: moveCalc.remainingSteps };
    }

    token.tilePosition = moveCalc.tile;
    token.currentLevel = moveCalc.level;

    const ladder = LADDERS.find(l => l.fromLevel === token.currentLevel && l.fromTile === token.tilePosition);
    if (ladder) {
        token.currentLevel = ladder.toLevel;
        token.tilePosition = ladder.toTile;
        console.log(`🪜 Ladder: P${player.playerIndex} T${token.tokenId} L${ladder.fromLevel}:${ladder.fromTile} → L${ladder.toLevel}:${ladder.toTile}`);
        return { type: "ladderClimb", tokenId: token.tokenId, fromLevel: ladder.fromLevel, fromTile: ladder.fromTile, toLevel: ladder.toLevel, toTile: ladder.toTile };
    }

    const snake = SNAKES.find(s => s.fromLevel === token.currentLevel && s.fromTile === token.tilePosition);
    if (snake) {
        token.currentLevel = snake.toLevel;
        token.tilePosition = snake.toTile;
        console.log(`🐍 Snake: P${player.playerIndex} T${token.tokenId} L${snake.fromLevel}:${snake.fromTile} → L${snake.toLevel}:${snake.toTile}`);
        return { type: "snakeSlide", tokenId: token.tokenId, fromLevel: snake.fromLevel, fromTile: snake.fromTile, toLevel: snake.toLevel, toTile: snake.toTile };
    }

    const collision = checkCollision(player, token);
    return { type: "normal", tokenId: token.tokenId, tile: token.tilePosition, level: token.currentLevel, collision: collision || null };
}

// ─────────────────────────────────────────
// Init Payload
// ─────────────────────────────────────────

function serializePlayer(player) {
    return {
        playerIndex: player.playerIndex,
        homeTiles: player.homeTiles,
        tokens: player.tokens.map(t => ({
            tokenId: t.tokenId,
            currentLevel: t.currentLevel,
            tilePosition: t.tilePosition,
            hasWon: t.hasWon
        }))
    };
}

function buildInitPayload(player) {
    return {
        playerIndex: player.playerIndex,
        homeTiles: player.homeTiles,
        tokens: player.tokens.map(t => ({
            tokenId: t.tokenId,
            currentLevel: t.currentLevel,
            tilePosition: t.tilePosition,
            hasWon: t.hasWon
        })),
        allPlayers: gameState.players.map(serializePlayer),
        ringConfigs: Object.entries(RING_CONFIGS).map(([level, cfg]) => ({
            level: parseInt(level),
            boardSize: cfg.boardSize,
            tileSize: cfg.tileSize,
            halfSide: parseFloat(cfg.halfSide.toFixed(4))
        })),
        safeTiles: Object.entries(SAFE_TILES).map(([level, tiles]) => ({
            level: parseInt(level), tiles
        })),
        ladders: LADDERS,
        snakes: SNAKES,
        maxLevel: MAX_LEVEL,
        tokensPerPlayer: TOKENS_PER_PLAYER
    };
}

// ─────────────────────────────────────────
// Socket.IO — Game Events
// ─────────────────────────────────────────

io.on("connection", (socket) => {
    console.log(`✅ Socket connected: ${socket.id}`);

    // Debug browser identifies itself — skip player registration
    if (socket.handshake.query.role === "debug") return;

    if (gameState.players.length >= 2) {
        socket.emit("error", { message: "Game is full" });
        socket.disconnect();
        return;
    }

    const playerIndex = gameState.players.length;
    const player = createPlayer(socket.id, playerIndex);
    gameState.players.push(player);

    console.log(`🎮 Player ${playerIndex} joined (${gameState.players.length}/2)`);

    socket.emit("init", buildInitPayload(player));

    io.emit("playerJoined", {
        playerIndex,
        totalPlayers: gameState.players.length,
        playerData: serializePlayer(player)
    });

    if (gameState.players.length === 2) {
        gameState.gamePhase = "playing";
        io.emit("gameStart", { currentPlayerIndex: gameState.currentPlayerIndex });
        console.log("🚀 Game started — Player 0 goes first");
    }

    socket.on("rollDice", () => {
        if (gameState.gamePhase === "finished") return socket.emit("error", { message: "Game already finished" });
        if (gameState.gamePhase === "waiting") return socket.emit("error", { message: "Waiting for second player" });
        if (!isPlayersTurn(socket.id)) return socket.emit("error", { message: "Not your turn" });
        if (gameState.pendingMove) return socket.emit("error", { message: "Select a token to move first" });

        const currentPlayer = gameState.players[gameState.currentPlayerIndex];
        const roll = rollDice();

        console.log(`🎲 P${currentPlayer.playerIndex} rolled ${roll}`);

        const validMoves = getValidMoves(currentPlayer, roll);
        const anyCanMove = validMoves.some(m => m.canMove);

        if (!anyCanMove) {
            const nextPlayer = getNextPlayer();
            console.log(`⏭️ Auto-pass → P${nextPlayer.playerIndex}`);
            io.emit("rollResult", { playerIndex: currentPlayer.playerIndex, roll, validMoves, autoPass: true, nextPlayerIndex: nextPlayer.playerIndex });
            return;
        }

        gameState.pendingMove = { playerIndex: currentPlayer.playerIndex, roll };
        io.emit("rollResult", { playerIndex: currentPlayer.playerIndex, roll, validMoves, autoPass: false, nextPlayerIndex: null });
    });

    socket.on("moveToken", ({ tokenId }) => {
        if (!isPlayersTurn(socket.id)) return socket.emit("error", { message: "Not your turn" });
        if (!gameState.pendingMove) return socket.emit("error", { message: "Roll the dice first" });

        const currentPlayer = gameState.players[gameState.currentPlayerIndex];
        const { roll } = gameState.pendingMove;
        const token = currentPlayer.tokens.find(t => t.tokenId === tokenId);

        if (!token) return socket.emit("error", { message: "Invalid token" });

        const validMoves = getValidMoves(currentPlayer, roll);
        const move = validMoves.find(m => m.tokenId === tokenId);
        if (!move || !move.canMove) return socket.emit("error", { message: `Token ${tokenId} cannot move: ${move?.reason}` });

        gameState.pendingMove = null;

        const moveResult = executeMove(currentPlayer, token, roll);

        if (moveResult.type === "win" && moveResult.playerWon) {
            gameState.gamePhase = "finished";
            io.emit("moveResult", { playerIndex: currentPlayer.playerIndex, roll, moveResult, nextPlayerIndex: null });
            io.emit("playerWon", { playerIndex: currentPlayer.playerIndex });
            console.log(`🏆 Player ${currentPlayer.playerIndex} wins!`);
            return;
        }

        const nextPlayer = getNextPlayer();
        io.emit("moveResult", { playerIndex: currentPlayer.playerIndex, roll, moveResult, nextPlayerIndex: nextPlayer.playerIndex });
        console.log(`➡️ Turn → P${nextPlayer.playerIndex}`);
    });

    socket.on("disconnect", () => {
        console.log(`❌ Player ${playerIndex} disconnected`);
        gameState.players = gameState.players.filter(p => p.id !== socket.id);
        if (gameState.players.length < 2) {
            gameState.gamePhase = "waiting";
            gameState.pendingMove = null;
            console.log("⏳ Waiting for players...");
        }
    });
});

// ─────────────────────────────────────────
// Debug — mounted only when DEBUG_MODE=true
// ─────────────────────────────────────────

if (DEBUG_MODE) {
    const mountDebug = require("./debug/debugserver");
    mountDebug({ app, io, gameState, RING_CONFIGS, SAFE_TILES, LADDERS, SNAKES, setForcedDice, getForcedDice });
}

// ─────────────────────────────────────────
// Error Handling
// ─────────────────────────────────────────

process.on("uncaughtException", (err) => console.error("Uncaught Exception:", err));
process.on("unhandledRejection", (reason) => console.error("Unhandled Rejection:", reason));

// ─────────────────────────────────────────
// Start
// ─────────────────────────────────────────

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
    console.log(`🚀 Snake Ludo server on port ${PORT}`);
    console.log("📋 Board config:");
    Object.entries(RING_CONFIGS).forEach(([level, cfg]) => {
        console.log(`   Level ${level}: ${cfg.boardSize} tiles | ${cfg.boardSize / 4} per side | halfSide: ${cfg.halfSide.toFixed(2)} | tileSize: ${cfg.tileSize}`);
    });
    if (DEBUG_MODE) console.log("🔧 Debug mode ON — open http://localhost:3000/debug");
});