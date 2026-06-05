using System.Collections.Generic;

namespace SnakeLudo.Core
{
    public class Token
    {
        public int tokenId;
        public int tilePosition;
        public int currentLevel;
        public bool hasWon;
        public bool isOnBoard; // false = token is in start zone, true = on the pyramid

        public int startTile;
        public int startLevel;

        public Token(int id, int level, int tile)
        {
            tokenId = id;
            currentLevel = level;
            tilePosition = tile;
            startLevel = level;
            startTile = tile;
            hasWon = false;
            isOnBoard = false; // starts in the start zone
        }
    }

    public class Player
    {
        public int playerIndex;
        public List<Token> tokens = new List<Token>();

        // Home tile per level — from server init
        public Dictionary<int, int> homeTiles = new Dictionary<int, int>();

        public Player(int index)
        {
            playerIndex = index;
        }
    }
}