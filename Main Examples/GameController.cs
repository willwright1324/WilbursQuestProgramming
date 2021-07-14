using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.Rendering.LWRP;

public enum Tag { PLAYER, STOP, PUSH, CONNECT, FLOAT }
public enum Layer { MISC, BLOCK, TUNNEL, PLAYER, COLLECT, BLOCK_DESTROY, COLLECT_DESTROY, SUPPORT, BG1, BG2 }
public enum MoveType { PLAYER, GRAVITY, BLOCK }

// Game functions
public class GameController : MonoBehaviour {
    // Developer Settings
    public static bool demoMode = false;
    public static bool devMode = false;
    public static bool devControls = true;
    public static bool showUI = true;
    public static bool enableParticles = true;
    public static bool enableParallax = true;

    // States
    public static bool initData;
    public static bool inEditor;
    public static bool editMode;
    public static bool initialStart;
    public static bool generatingBlocks;
    public static bool disableEditors;
    public static bool paused;
    public static bool resetRoom;
    public static bool undoing;
    public static bool moving;
    public static bool movingBlocks;
    public static bool tunneling;
    public static bool resetting;
    public static bool shooting;
    public static bool cancelBullets;
    public static bool applyingGravity;

    // Game Info
    public static string startRoom;
    public static string levelName;
    public static int currentSave = 1;
    public static int demoIndex = 1;
    public static float BLOCK_SIZE = 6.999f;
    public static float BLOCK_MOVE_SPEED = 60;
    public static float BLOCK_FALL_SPEED = 100;
    public static (int x, int y) gravityDirection = (0, -1);
    public static (int x, int y)[] facingDirection = { (1, 0), (0, 1), (-1, 0), (0, -1) };
    public static (int x, int y)[] backFacingDirection = { (-1, 0), (0, -1), (1, 0), (0, 1) };
    public static (int x, int y)[] compassDirection = { (0, 1), (0, -1), (-1, 0), (1, 0) };
    public static Material spriteDefault;
    public static Material spriteLitDefault;
    public static int colorCycle = 3;
    public static Color32[] crystalColors;
    public static Color32 timeColor = new Color32(255, 255, 0, 255);
    public static int activeBullets;
    public static int fallingBlocks;
    public static int enterTunnelID;
    public static bool enterTunnel;
    public static string currentRoom;

    public static Data[] fallData;
    public static GameObject[] blockList;
    public static CoordinateSystem currentCoordinateSystem;
    public static Dictionary<(int x, int y), Data> currentMiscBlocks;
    public static MapSystem currentMapSystem;
    public static List<Data> moveBlocks = new List<Data>();
    static IEnumerator moveBlocksCoroutine;
    public static List<Data> fragmentChecks = new List<Data>();
    public static Screen currentScreen;
    public static GameObject resetHolder;
    public static List<SpriteRenderer> resetSprites;
    public static SpriteRenderer tunnelResetSprite;
    static List<TunnelPanel> tunnelPanels;
    static int[] activeButtons = new int[3];
    public static bool jumpingFallData;
    public static List<Data> additionalFallData = new List<Data>();
    public static Sprite[] panelSprites = new Sprite[2];
    public static Sprite[] buttonSprites = new Sprite[2];
    public static List<GameObject> panelInfos;
    public static List<GameObject> buttonInfos;
    static List<Data> startButtons;
    static GameObject camCenter;
    public static bool gateRoom;
    public static Data gateData;
    public static Animator[] gateAnimators;
    public static Light2D gateLight;

    // History
    public static List<History> historyList;
    static List<Data> movedHistoryData;
    static Data[] wormUndoMoveData;
    static Data[] wormUndoGrowData;
    public static bool foundUndoPlayer;
    public static int historyRollbacks;

    // Other Modules
    public static AudioController ac;
    public static GameControllerCoroutines gcc;
    static PlayerController pc;

    // Tags
    public class BlockInfo {
        public Color32 mapColor;
        public Tag[] tags;
        public Layer layer;
        public int[] tiles;

        public BlockInfo(Color32 mapColor, Tag[] tags, Layer layer, int[] tiles) {
            this.mapColor = mapColor;
            this.tags = tags;
            this.layer = layer;
            this.tiles = tiles;
        }
    }
    public static Dictionary<string, BlockInfo> infoDictionary = new Dictionary<string, BlockInfo> {
        { "RedButton",       new BlockInfo(new Color32(255, 0, 255, 255),   null,                                            Layer.MISC,    null)},
        { "GreenButton",     new BlockInfo(new Color32(255, 0, 255, 255),   null,                                            Layer.MISC,    null)},
        { "BlueButton",      new BlockInfo(new Color32(255, 0, 255, 255),   null,                                            Layer.MISC,    null)},
        { "CollectRed",      new BlockInfo(new Color32(255, 0, 0, 255),     null,                                            Layer.COLLECT, null)},
        { "CollectGreen",    new BlockInfo(new Color32(0, 255, 0, 255),     null,                                            Layer.COLLECT, null)},
        { "CollectBlue",     new BlockInfo(new Color32(0, 0, 255, 255),     null,                                            Layer.COLLECT, null)},
        { "CollectLength",   new BlockInfo(new Color32(150, 50, 150, 255),  null,                                            Layer.COLLECT, null)},
        { "CollectTime",     new BlockInfo(new Color32(255, 255, 0, 255),   null,                                            Layer.COLLECT, null)},
        { "CollectFragment", new BlockInfo(new Color32(0, 255, 255, 255),   null,                                            Layer.COLLECT, null)},
        { "RedCrystal",      new BlockInfo(new Color32(255, 0, 0, 255),     new Tag[] { Tag.PUSH, Tag.CONNECT, Tag.FLOAT },  Layer.BLOCK,   new int[] { 10, 10, 10 })},
        { "GreenCrystal",    new BlockInfo(new Color32(0, 255, 0, 255),     new Tag[] { Tag.PUSH, Tag.CONNECT, Tag.FLOAT },  Layer.BLOCK,   new int[] { 10, 10, 10 })},
        { "BlueCrystal",     new BlockInfo(new Color32(0, 0, 255, 255),     new Tag[] { Tag.PUSH, Tag.CONNECT, Tag.FLOAT },  Layer.BLOCK,   new int[] { 10, 10, 10 })},
        { "Edge",            new BlockInfo(new Color32(30, 255, 30, 150),   null,                                            Layer.BLOCK,   null)},
        { "Empty",           new BlockInfo(new Color32(0, 0, 0, 150),       null,                                            Layer.BLOCK,   null)},
        { "Gate",            new BlockInfo(new Color32(100, 100, 100, 255), new Tag[] { Tag.STOP },                          Layer.BLOCK,   null)},
        { "GateSlot",        new BlockInfo(new Color32(255, 0, 255, 255),   null,                                            Layer.MISC,    null)},
        { "Ground",          new BlockInfo(new Color32(255, 255, 255, 150), new Tag[] { Tag.STOP },                          Layer.BLOCK,   new int[] { 90, 90, 70, 70, 20, 10, 5 })},
        { "GroundBG1",       new BlockInfo(new Color32(255, 255, 255, 150), null,                                            Layer.BG1,     new int[] { 90, 90, 70, 70, 20, 10, 5 })},
        { "GroundBG2",       new BlockInfo(new Color32(255, 255, 255, 150), null,                                            Layer.BG2,     new int[] { 90, 90, 70, 70, 20, 10, 5 })},
        { "Player",          new BlockInfo(new Color32(213, 144, 179, 255), new Tag[] { Tag.PLAYER, Tag.CONNECT, Tag.STOP }, Layer.PLAYER,  null)},
        { "Rock",            new BlockInfo(new Color32(100, 100, 100, 255), new Tag[] { Tag.PUSH, Tag.CONNECT },             Layer.BLOCK,   new int[] { 70, 70, 70, 20, 10, 5 })},
        { "Support",         new BlockInfo(new Color32(100, 100, 100, 255), null,                                            Layer.SUPPORT, new int[] { 10, 10, 10, 10 })},
        { "Tunnel",          new BlockInfo(new Color32(255, 255, 255, 255), new Tag[] { Tag.CONNECT, Tag.STOP },             Layer.TUNNEL,  new int[] { 10, 10, 10 })},
        { "TunnelDoor",      new BlockInfo(new Color32(255, 0, 255, 255),   null,                                            Layer.MISC,    null)},
        { "TunnelPanel",     new BlockInfo(new Color32(255, 0, 255, 255),   null,                                            Layer.MISC,    null)}
    };

    // Sprite Tiling
    public static bool[][][] spriteTypes = new bool[][][] {
        // Center
        new bool[][] { new bool[] { true, true, true, true } },
        // Centerside
        new bool[][] { new bool[] { false, false, true, true },
                       new bool[] { true, true, false, false } },
        // Corner
        new bool[][] { new bool[] { true, false, false, true },
                       new bool[] { true, false, true, false },
                       new bool[] { false, true, true, false },
                       new bool[] { false, true, false, true } },
        // End
        new bool[][] { new bool[] { false, false, true, false },
                       new bool[] { false, true, false, false },
                       new bool[] { false, false, false, true },
                       new bool[] { true, false, false, false } },
        // Side
        new bool[][] { new bool[] { false, true, true, true },
                       new bool[] { true, true, false, true },
                       new bool[] { true, false, true, true },
                       new bool[] { true, true, true, false } },
        // Single
        new bool[][] { new bool[] { false, false, false, false } }
    };

    // Initialize current level
    public static void Init() {
        inEditor = false;
#if (UNITY_EDITOR)
        inEditor = true;
        currentSave = 3;
#endif
        if (devControls && currentSave == 3)
            showUI = false;

        crystalColors = new Color32[] { new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255), new Color32(0, 114, 255, 255) };

        for (int i = 0; i < 2; i++) {
            panelSprites[i] = Resources.Load<Sprite>("Blocks/Sprites/panel_" + (i == 0 ? "on" : "off"));
            buttonSprites[i] = Resources.Load<Sprite>("Blocks/Sprites/button_" + (i == 0 ? "on" : "off"));
        }

        GameObject editor = GameObject.Find("LevelEditor");
        Cursor.visible = false;
        if (demoMode)
            editor = null;
        if (editor != null) {
            if (disableEditors)
                editor.SetActive(false);
            else {
                editMode = true;
                return;
            }
        }
        if (!initData) {
            InitData();
            initData = true;
        }

        ac = Camera.main.GetComponent<AudioController>();
        levelName = GameObject.FindWithTag("Level").name;
        pc = GameObject.Find("PlayerController").GetComponent<PlayerController>();
        GameObject[] screens = GameObject.FindGameObjectsWithTag("Screen");
        foreach (GameObject go in screens) {
            Screen s = go.GetComponent<Screen>();
            if (s == null)
                continue;
            if (!s.showBorder) {
                s.GetComponent<SpriteRenderer>().enabled = false;
                foreach (GameObject b in s.borders)
                    b.GetComponent<SpriteRenderer>().enabled = false;
            }
            s.enabled = false;
        }

        GetBlockList();
        GetCoordinateSystem(levelName, resetRoom ? 0 : currentSave);

        resetRoom = false;
        LoadBlocks(levelName, currentSave, currentCoordinateSystem);
        GetFallData();
        CheckLevelMisc();
        historyList = new List<History>();
        gcc = GameObject.FindGameObjectWithTag("GameController").GetComponent<GameControllerCoroutines>();
        gcc.pauseUI.SetActive(false);

        spriteDefault = gcc.spriteDefault;
        spriteLitDefault = gcc.spriteLitDefault;
        currentMapSystem = LoadMap();
        InitMap(gcc.map, currentMapSystem);
    }

    // Get list of all block types
    public static GameObject[] GetBlockList() {
        if (gcc == null)
            gcc = GameObject.FindGameObjectWithTag("GameController").GetComponent<GameControllerCoroutines>();
        blockList = gcc.blockList;
        return blockList;
    }

    // Get level's coordinate system
    public static CoordinateSystem GetCoordinateSystem(string fileName, int slot) {
        currentCoordinateSystem = LoadData(fileName, slot);
        return currentCoordinateSystem;
    }

    // Get blocks affected by gravity
    public static Data[] GetFallData() {
        List<Data> fallList = new List<Data>();
        fallList.Add(null);
        List<(int x, int y)[]> connections = new List<(int x, int y)[]>();

        foreach (var kvp in currentCoordinateSystem.coordinateData) {
            Data data = currentCoordinateSystem.GetData(kvp.Key, Layer.BLOCK);
            if (data != null && data.HasTag(Tag.CONNECT) && !data.HasTag(Tag.FLOAT) && !data.HasTag(Tag.PLAYER) && data.HasTag(Tag.PUSH)) {
                if (!connections.Contains(data.blockData.connectedBlocks)) {
                    connections.Add(data.blockData.connectedBlocks);
                    (int x, int y) lowestY = (999, 999);
                    foreach (var c in data.blockData.connectedBlocks) {
                        if (c.y < lowestY.y || lowestY == (999, 999))
                            lowestY = c;
                    }
                    fallList.Add(currentCoordinateSystem.GetData(lowestY, data.layer));
                }
            }
        }

        fallData = fallList.ToArray();
        return fallData;
    }

    // Get grid value from worldspace
    public static int GetGridCoordinate(float value) {
        return Mathf.RoundToInt(SnapToGrid(value) / BLOCK_SIZE);
    }

    // Snap worldspace value to grid
    public static float SnapToGrid(float worldCoordinate) {
        float sign = Mathf.Sign(worldCoordinate);
        float remainder = Mathf.Abs(worldCoordinate) % BLOCK_SIZE;
        return remainder <= BLOCK_SIZE / 2 ? worldCoordinate - sign * remainder : worldCoordinate - sign * remainder + sign * BLOCK_SIZE;
    }

    // Get data adjacent to coordinates
    public static List<Data>[] GetNearData((int x, int y) coordinates, CoordinateSystem coordinateSystem) {
        List<Data>[] nearData = new List<Data>[4];

        for (int i = 0; i < compassDirection.Length; i++)
            nearData[i] = coordinateSystem.GetList((coordinates.x + compassDirection[i].x, coordinates.y + compassDirection[i].y));

        return nearData;
    }

    // Return which adjecent blocks are of the same type
    public static bool[] GetNearBlocks((int x, int y) coordinates, string blockName, CoordinateSystem coordinateSystem) {
        Data[] nearBlocksFound = new Data[4];
        bool[] nearBlocksNamed = new bool[4];

        for (int i = 0; i < compassDirection.Length; i++)
            nearBlocksFound[i] = coordinateSystem.GetData((coordinates.x + compassDirection[i].x, coordinates.y + compassDirection[i].y), Layer.BLOCK);

        for (int i = 0; i < nearBlocksFound.Length; i++) {
            if (nearBlocksFound[i] != null && nearBlocksFound[i].blockData.blockName == blockName)
                nearBlocksNamed[i] = true;
        }

        return nearBlocksNamed;
    }

    // Match block object transform to data
    public static void ApplyData(BlockData blockData, GameObject block, bool applyName, bool applyFacing) {
        if (applyName)
            block.name = blockData.blockName;
        if (applyFacing)
            block.transform.eulerAngles = Vector3.forward * blockData.facing * 90;
        ApplyCoordinates(blockData.coordinates, block);
    }

    // Apply data coordinates to worldspace transform
    public static void ApplyCoordinates((int x, int y) coords, GameObject block) {
        block.transform.position = new Vector2(coords.x * BLOCK_SIZE, coords.y * BLOCK_SIZE);
    }

    // Move block
    public static bool MoveBlock(Data data, (int x, int y) amount, MoveType moveType) {
        moving = true;

        // Block is immovable
        if ((data.HasTag(Tag.STOP) && !data.HasTag(Tag.PLAYER)) || (data.HasTag(Tag.PLAYER) && !applyingGravity))
            return MoveBlocked(moveType);

        // Check if moving block pushes other blocks recursively and add cleared blocks to move list
        List<Data> moveList = new List<Data>();
        List<Data> checkList = new List<Data>();
        List<(int x, int y)[]> clearedConnections = new List<(int x, int y)[]>();
        Layer[] checkLayers = { Layer.BLOCK, Layer.PLAYER, Layer.TUNNEL };
        checkList.Add(data);
        int checkIndex = 0;
        do {
            Data d = checkList[checkIndex];
            if (d.HasTag(Tag.PUSH, Tag.PLAYER)) {
                // If made of more than 1 block, check all connected blocks
                if (d.HasTag(Tag.CONNECT)) {
                    // Check if blocks have already been cleared
                    if (!clearedConnections.Contains(d.blockData.connectedBlocks)) {
                        foreach (var c in d.blockData.connectedBlocks) {
                            // Check if next block is immovable
                            foreach (Layer layer in checkLayers) {
                                Data dC = currentCoordinateSystem.GetData(c, d.layer);
                                int nextX = c.x + amount.x;
                                int nextY = c.y + amount.y;
                                Data nextdC = currentCoordinateSystem.GetData((nextX, nextY), layer);

                                if (nextdC != null && dC != null) {
                                    if (!(moveType == MoveType.BLOCK && !dC.HasTag(Tag.PLAYER) && nextdC.HasTag(Tag.PLAYER))) {
                                        if ((nextdC.HasTag(Tag.STOP) && !(nextdC.HasTag(Tag.PLAYER) && dC.HasTag(Tag.PLAYER)))
                                        || (moveType == MoveType.PLAYER && !dC.HasTag(Tag.PLAYER) && nextdC.HasTag(Tag.PLAYER))
                                        || (moveType == MoveType.GRAVITY && nextdC.HasTag(Tag.FLOAT)))
                                            return MoveBlocked(moveType);
                                    }
                                    else
                                        checkList.Add(nextdC);
                                }
                            }

                            // Check if next block is pushable or empty, if pushable add to checklist
                            foreach (Layer layer in checkLayers) {
                                Data dC = currentCoordinateSystem.GetData(c, d.layer);
                                int nextX = c.x + amount.x;
                                int nextY = c.y + amount.y;
                                Data nextdC = currentCoordinateSystem.GetData((nextX, nextY), layer);

                                if (nextdC != null && dC != null) {
                                    if (nextdC.HasTag(Tag.PUSH, Tag.PLAYER)) {
                                        int moveIndex = GetListOrder(moveList, dC.blockData, amount);
                                        if (moveIndex == -1) moveList.Add(dC);
                                        else                 moveList.Insert(moveIndex, dC);

                                        if (nextdC.blockData.connectedBlocks != d.blockData.connectedBlocks)
                                            checkList.Add(nextdC);
                                        break;
                                    }
                                }
                                else {
                                    if (dC != null) {
                                        int moveIndex = GetListOrder(moveList, dC.blockData, amount);
                                        if (moveIndex == -1) moveList.Add(dC);
                                        else                 moveList.Insert(moveIndex, dC);
                                        break;
                                    }
                                }
                            }
                        }
                        // All connected blocks have been cleared
                        clearedConnections.Add(d.blockData.connectedBlocks);
                    }
                }
            }
        } while (++checkIndex < checkList.Count); // Stop checking when blocks have stopped being added

        // Add history changes and move blocks in move list
        History history = new History();
        List<(int x, int y)[]>[] connections = new List<(int x, int y)[]>[4];
        for (int i = 0; i < connections.Length; i++)
            connections[i] = new List<(int x, int y)[]>();
        List<Data> playGroundParticlesList = new List<Data>();
        additionalFallData = new List<Data>();
        bool rockMoved = false;
        bool crystalMoved = false;
        foreach (Data d in moveList) {
            if (history != null) {
                (int x, int y)[] connectedBlocks = null;
                if (d.blockData.connectedBlocks != null) {
                    if (moveType == MoveType.GRAVITY && d != data && !d.HasTag(Tag.FLOAT) && d.blockData.connectedBlocks != data.blockData.connectedBlocks)
                        additionalFallData.Add(d);

                    foreach (var list in connections[(int)d.layer]) {
                        bool found = true;
                        for (int i = 0; i < list.Length; i++) {
                            if (list[i] != d.blockData.connectedBlocks[i]) {
                                found = false;
                                break;
                            }
                        }
                        if (found) {
                            connectedBlocks = list;
                            break;
                        }
                    }
                    if (connectedBlocks == null) {
                        connectedBlocks = new (int x, int y)[d.blockData.connectedBlocks.Length];
                        d.blockData.connectedBlocks.CopyTo(connectedBlocks, 0);
                        connections[(int)d.layer].Add(connectedBlocks);
                    }
                }
                else {
                    if (moveType == MoveType.GRAVITY && d != data && !d.HasTag(Tag.FLOAT))
                        additionalFallData.Add(d);
                }
                history.changes.Add(new HistoryChange.Move(d, amount, connectedBlocks));
            }
            if (d.HasTag(Tag.FLOAT)) {
                jumpingFallData = false;
                crystalMoved = true;
            }
            else {
                // Check if rock moved against something to make sound
                if (amount.x != 0 && d.HasTag(Tag.PUSH)) {
                    playGroundParticlesList.Add(d);
                    if (!rockMoved) {
                        for (int i = 0; i < 2; i++) {
                            (int x, int y) belowCoord = (d.blockData.coordinates.x + i, d.blockData.coordinates.y + gravityDirection.y);
                            Data blockData = currentCoordinateSystem.GetData(belowCoord, Layer.BLOCK);
                            if (blockData != null && d.blockData.connectedBlocks != null) {
                                foreach (var c in d.blockData.connectedBlocks) {
                                    var cc = (c.x + amount.x, c.y + amount.y);
                                    if (blockData.blockData.coordinates == cc) {
                                        blockData = null;
                                        break;
                                    }
                                }
                            }
                            if (blockData == null)
                                blockData = currentCoordinateSystem.GetData(belowCoord, Layer.TUNNEL);
                            if (blockData == null) {
                                blockData = currentCoordinateSystem.GetData(belowCoord, Layer.PLAYER);
                                if (blockData != null && moveType == MoveType.PLAYER && blockData == pc.headData)
                                    blockData = null;
                            }
                            if (blockData != null) {
                                rockMoved = true;
                                break;
                            }
                        }
                    }
                }
            }
            currentCoordinateSystem.MoveData(amount, d, false);
        }
        PlayGroundMovingParticles(playGroundParticlesList, amount.x);
        if (history != null)
            historyList.Add(history);

        // Update connected blocks coordinates
        foreach (var cc in clearedConnections) {
            for (int i = 0; i < cc.Length; i++) {
                cc[i].x += amount.x;
                cc[i].y += amount.y;
            }
        }

        if (rockMoved)    PlayRandomSound(AudioController.rockMove);
        if (crystalMoved) PlayRandomSound(AudioController.crystalMove);

        MoveBlocks(amount, moveList, moveType == MoveType.GRAVITY);
        if (moveType != MoveType.GRAVITY)
            CheckLevelMisc();

        return true;
    }

    // Player or block cannot be moved
    public static bool MoveBlocked(MoveType moveType) {
        if (moveType == MoveType.PLAYER && !pc.headAnimator.GetCurrentAnimatorStateInfo(0).IsName("Nod")) {
            PlayRandomSound(AudioController.playerBlocked);
            pc.SetHeadAnimation("StopNod", 1);
            pc.SetHeadAnimation("Nod", 2);
        }
        moving = false;
        return false;
    }

    public static void MoveBlocks((int x, int y) amount, List<Data> blocks, bool gravityMove) {
        moveBlocksCoroutine = gcc.MoveBlocks(amount, blocks, gravityMove);
        gcc.StartCoroutine(moveBlocksCoroutine);
    }

    public static void SnapBlocks() {
        gcc.StopCoroutine(moveBlocksCoroutine);
        foreach (Data d in moveBlocks)
            d.ApplyData();
        moving = false;
        movingBlocks = false;
    }

    // Prevents blocks moving into each other by ordering based on move direction
    public static int GetListOrder(List<Data> moveList, BlockData blockData, (int x, int y) amount) {
        if (amount.x == 0) {
            if (amount.y < 0) {
                for (int i = 0; i < moveList.Count; i++) {
                    if (blockData.coordinates.y <= moveList[i].blockData.coordinates.y)
                        return i;
                }
            }
            else {
                for (int i = 0; i < moveList.Count; i++) {
                    if (blockData.coordinates.y >= moveList[i].blockData.coordinates.y)
                        return i;
                }
            }
        }
        else {
            if (amount.x < 0) {
                for (int i = 0; i < moveList.Count; i++) {
                    if (blockData.coordinates.x <= moveList[i].blockData.coordinates.x)
                        return i;
                }
            }
            else {
                for (int i = 0; i < moveList.Count; i++) {
                    if (blockData.coordinates.x >= moveList[i].blockData.coordinates.x)
                        return i;
                }
            }
        }
        return -1;
    }

    // Eat collectable
    public static void Eat(Data eatData) {
        gcc.Eat(eatData);
    }

    // Place fragment in slot
    public static void PlaceFragment(Data slotData) {
        gcc.PlaceFragment(slotData);
    }

    // Shoot projectile
    public static void Shoot((int x, int y) origin, int facing) {
        gcc.Shoot(origin, facing);
    }

    // Undo history
    public static void Undo(bool silentUndo, float pitch) {
        // No history
        if (historyList == null || historyList.Count == 0) {
            if (!silentUndo) PlayRandomSound(AudioController.playerBlocked);
            return;
        }

        if (!undoing) {
            wormUndoMoveData = null;
            movedHistoryData = new List<Data>();
            pc.SetHeadAnimation("StopNod", 0);
            if (!silentUndo) {
                foundUndoPlayer = false;
                PlayPitchedSound(AudioController.playerUndo, pitch);
                pc.TimeEyes();
            }
        }
        undoing = true;

        pc.WakeUp();
        Data[] newWormData = null;

        // Get last history and go through all changes made
        History history = historyList[historyList.Count - 1];
        historyRollbacks += history.rollbacks;
        for (int i = history.changes.Count - 1; i >= 0; i--) {
            switch (history.changes[i].changeType) {
                // Move block to previous spot
                case ChangeType.MOVE:
                    HistoryChange.Move m = (HistoryChange.Move)history.changes[i];
                    Data data = currentCoordinateSystem.GetData(m.location, m.data.layer);
                    Data oldData = data;
                    currentCoordinateSystem.RemoveData(data);
                    data = m.data;
                    currentCoordinateSystem.AddData(data);
                    data.ApplyData();

                    if (data.blockData.IsPrimary() && data.HasTag(Tag.FLOAT))
                        data.blockObject.GetComponentInChildren<FloatMovement>().Init(data);
                    // Get data for player undo outline
                    if (data.HasTag(Tag.PLAYER)) {
                        if (!foundUndoPlayer) {
                            if (wormUndoMoveData == null)
                                wormUndoMoveData = new Data[oldData.blockData.connectedBlocks.Length];
                            for (int j = 0; j < wormUndoMoveData.Length; j++) {
                                if (oldData.blockData.coordinates == oldData.blockData.connectedBlocks[j]) {
                                    wormUndoMoveData[j] = oldData;
                                    break;
                                }
                            }
                        }

                        if (newWormData == null)
                            newWormData = new Data[data.blockData.connectedBlocks.Length];
                        for (int j = 0; j < newWormData.Length; j++) {
                            if (data.blockData.coordinates == data.blockData.connectedBlocks[j]) {
                                newWormData[j] = data;
                                break;
                            }
                        }
                        if (data.blockData.IsPrimary() && historyList.Count > 1)
                            pc.playerData.flippedBlocks[0] = m.headFlipped;
                    }
                    else {
                        // Get starting point of block for undo outline
                        if (!silentUndo && data.blockData.IsPrimary()) {
                            if (!movedHistoryData.Contains(m.originalData)) {
                                movedHistoryData.Add(m.originalData);
                                SetUndoOutline(oldData);
                            }
                        }
                    }
                    break;

                // Do opposite of grow/shrink
                case ChangeType.GROW:
                    wormUndoGrowData = new Data[pc.wormData.Count];
                    for (int j = 0; j < wormUndoGrowData.Length; j++)
                        wormUndoGrowData[j] = new Data(pc.wormData[j]);

                    HistoryChange.Grow g = (HistoryChange.Grow)history.changes[i];
                    pc.GrowPlayer(g.backAmount, true);
                    break;

                // Reactivate destroyed blocks
                case ChangeType.DESTROY:
                    HistoryChange.Destroy d = (HistoryChange.Destroy)history.changes[i];
                    d.data.layer = d.layer;
                    d.data.blockObject.SetActive(true);
                    d.data.blockData.destroyed = false;
                    break;

                // Reactivate collectable and relock its ability
                case ChangeType.COLLECT:
                    HistoryChange.Collect c = (HistoryChange.Collect)history.changes[i];
                    if (pc.playerData.abilityLocations.Count > 0) {
                        foreach (var al in pc.playerData.abilityLocations) {
                            if (al.room == currentRoom && al.coordinates == c.coordinates) {
                                pc.playerData.abilityLocations.Remove(al);
                                break;
                            }
                        }
                    }

                    switch (c.abilityType) {
                        case "Fragment":
                            pc.playerData.fragments--;
                            break;

                        case "Length":
                            pc.playerData.currMaxLength--;
                            ShowLengthMeter(false);
                            gcc.lengthObjects[pc.playerData.currMaxLength].SetActive(false);
                            IncrementLengthMeter(-1);
                            pc.playerData.abilities[1] = pc.playerData.currMaxLength != 3;
                            ShowAbilityInfo(1, false);
                            break;

                        case "Color":
                            pc.playerData.colors[c.abilityIndex] = false;
                            pc.playerData.abilities[0] = pc.playerData.colors[0] || pc.playerData.colors[1] || pc.playerData.colors[2];
                            ShowAbilityInfo(0, false);
                            break;

                        case "Time":
                            pc.playerData.abilities[c.abilityIndex] = false;
                            ShowAbilityInfo(c.abilityIndex, false);
                            break;
                    }
                    break;

                // Add fragment back to inventory and remove it from slot
                case ChangeType.PLACE:
                    HistoryChange.Place p = (HistoryChange.Place)history.changes[i];
                    Destroy(p.data.blockObject);
                    currentCoordinateSystem.RemoveData(p.data);
                    pc.playerData.fragments++;
                    pc.playerData.abilityLocations.Add(new PlayerController.PlayerData.AbilityLocation(pc.playerData.currentRoom, p.data.blockData.coordinates));
                    break;

                // Change button back to previous state
                case ChangeType.BUTTON:
                    HistoryChange.Button b = (HistoryChange.Button)history.changes[i];
                    SetButton(b.data, b.state, true);
                    break;

                // Reopen locked doors that close after the player leaves them
                case ChangeType.TEMPDOOR:
                    HistoryChange.TempDoor t = (HistoryChange.TempDoor)history.changes[i];
                    t.tunnelPanel.SetTemporaryDoor(true);
                    break;
            }
        }
        // Set player to old worm blocks
        if (newWormData != null) {
            pc.wormData = new List<Data>();
            foreach (Data d in newWormData)
                pc.wormData.Add(d);
            pc.headData = newWormData[0];
            pc.UpdateBodyFlips();
        }

        GetFallData();
        fallData[0] = pc.headData;

        if (wormUndoMoveData != null)
            foundUndoPlayer = true;

        // Recursively undo for multiple changes at a time
        historyList.RemoveAt(historyList.Count - 1);
        if (historyRollbacks > 0) {
            historyRollbacks--;
            Undo(true, 0);
        }
        else {
            // Create worm outline and update doors/panels
            if (wormUndoMoveData != null)
                pc.CreateUndoOutline(wormUndoMoveData);
            else {
                if (wormUndoGrowData != null)
                    pc.CreateUndoOutline(wormUndoGrowData);
            }
            if (historyList.Count < 2)
                pc.flyMode = true;

            CheckLevelMisc();
            undoing = false;
        }
    }

    // Reset room
    public static void ResetRoom(bool emptyReset) {
        resetting = false;
        enterTunnel = !emptyReset;
        resetRoom = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    // Display reset outlines
    public static void ShowResets() {
        gcc.FadeResetOutline();
    }

    // Enter tunnel
    public static void EnterTunnel(RegionData.RegionTunnel regionTunnel, Data tunnelData, bool tunnelBack) {
        if (tunneling)
            return;

        tunneling = true;
        gcc.EnterTunnel(regionTunnel, tunnelData, tunnelBack);
    }
    // Exit tunnel
    public static void ExitTunnel(Data tunnelData) {
        gcc.ExitTunnel(tunnelData);
    }

    // Pause game
    public static void Pause(bool active) {
        if (gcc.pauseUI.GetComponent<MenuController>().activeConfirmation)
            return;

        paused = active;
        gcc.fragmentCountText.text = pc.playerData.fragments + "";
        gcc.fragmentCount.SetActive(paused);
        gcc.pauseUI.SetActive(paused);
    }

    // Create, show, and set length of length meter
    public static void InitLengthMeter() {
        gcc.InitLengthMeter();
    }
    public static void ShowLengthMeter(bool active) {
        if (!showUI)
            return;

        gcc.lengthMeter.SetActive(active);
    }
    public static void IncrementLengthMeter(int amount) {
        if (amount > 0) {
            GameObject prevLength = gcc.lengthObjects[pc.playerData.length - 2];
            GameObject nextLength = gcc.lengthObjects[pc.playerData.length - 1];
            gcc.SetLengthAnimation(prevLength, 2);
            gcc.SetLengthAnimation(nextLength, 1);
        }
        else {
            GameObject prevLength = gcc.lengthObjects[pc.playerData.length];
            GameObject nextLength = gcc.lengthObjects[pc.playerData.length - 1];
            gcc.SetLengthAnimation(prevLength, 0);
            gcc.SetLengthAnimation(nextLength, 1);
        }
    }

    // Show information for new ability
    public static void ShowAbilityInfo(int abilityIndex, bool active) {
        gcc.abilityInfo[abilityIndex].SetActive(active);
    }

    // Initialize tunnel panels
    public static void InitPanels() {
        foreach (var kvp in currentMiscBlocks) {
            if (kvp.Value.blockData.blockName == "TunnelDoor")
                kvp.Value.blockObject.GetComponent<TunnelPanel>().Init();
        }

        foreach (Data d in startButtons) {
            if (d.blockData.state == 0) {
                d.blockData.state = -1;
                SetButton(d, 0, true);
                continue;
            }
            if (d.blockData.state == 1) {
                d.blockData.state = -1;
                SetButton(d, 1, true);
                continue;
            }
        }
    }

    public static void EnableMapArrows(bool enable) {
        gcc.mapArrows.SetActive(enable);
    }

    public static GameObject GetGroundMovingParticles() {
        return gcc.groundMovingParticles;
    }

    public static GameObject GetMap() {
        return gcc.GetMap();
    }
    public static GameObject GetMapHolder() {
        return gcc.GetMapHolder();
    }

    public static GameObject GetGameUI() {
        return gcc.GetGameUI();
    }

    // Get color of pixel on map at coordinates
    public static Color GetPixel(Texture2D texture, (int x, int y) coordinates) {
        return texture.GetPixel(50 + coordinates.x, 50 + coordinates.y);
    }
    // Set color of pixel on map based on type
    public static void SetPixel(Texture2D texture, string type, (int x, int y) coordinates) {
        Color color = Color.clear;
        if (type != "Clear") {
            if (type == "Empty") {
                Color32 getColor = GetPixel(texture, coordinates);
                color = getColor == Color.clear ? infoDictionary[type].mapColor : getColor;
            }
            else {
                if (type == "EmptyOverride")
                    type = "Empty";
                color = infoDictionary[type].mapColor;
            }
        }
        texture.SetPixel(50 + coordinates.x, 50 + coordinates.y, color);
    }
    // Set color of pixel on map based on pixel that was overridden
    public static void ResetPixel(CoordinateSystem coordinateSystem, Layer layer, (int x, int y) coordinates, Texture2D texture) {
        Layer[] layerOrder = new Layer[] { Layer.COLLECT, Layer.TUNNEL, Layer.PLAYER };
        List<Data> dataList = coordinateSystem.GetList(coordinates);
        string type = "EmptyOverride";
        if (dataList != null) {
            foreach (Layer l in layerOrder) {
                if (l == layer)
                    continue;

                foreach (Data d in dataList) {
                    if (d.layer == l) {
                        type = d.blockData.blockName;
                        break;
                    }
                }
                if (type != "EmptyOverride")
                    break;
            }
        }
        SetPixel(texture, type, coordinates);
    }

    // Set camera to screen instantly
    public static void SetScreen(Screen screen) {
        currentScreen = screen;
        Camera.main.transform.position = new Vector3(screen.transform.position.x, screen.transform.position.y, -1);
        Camera.main.orthographicSize = screen.borders[0].transform.localScale.x * 2 - (BLOCK_SIZE / 10);
    }
    // Set camera to nearest screen
    public static void SetNearestScreen(Vector2 pos) {
        Screen[] screens = FindObjectsOfType<Screen>();
        if (screens != null && screens.Length > 0) {
            Screen screen = screens[0];
            foreach (Screen s in screens) {
                if (Vector2.Distance(s.transform.position, pos) < Vector2.Distance(screen.transform.position, pos))
                    screen = s;
            }
            SetScreen(screen);
        }
    }
    // Interpolate camera to screen
    public static void GoToScreen(GameObject screenObject) {
        gcc.GoToScreen(screenObject);
    }

    // Open/Close gate, 0: Open | 1: Close | 2: Blink
    public static void SetGate(int state) {
        if (gateData.blockData.state == state)
            return;

        pc.disableMovement = true;
        switch (state) {
            case 0:
                gateData.blockData.state = 0;
                pc.playerData.abilityLocations.Add(new PlayerController.PlayerData.AbilityLocation(pc.playerData.currentRoom, gateData.blockData.coordinates));
                gateAnimators[0].ResetTrigger("Blink");
                foreach (Animator a in gateAnimators)
                    a.SetBool("On", true);

                gateLight.enabled = true;
                gateData.layer = Layer.BLOCK_DESTROY;
                gcc.OpenGate(true);
                break;

            case 1:
                gateData.blockData.state = 1;
                foreach (var al in pc.playerData.abilityLocations) {
                    if (al.room == pc.playerData.currentRoom && al.coordinates == gateData.blockData.coordinates) {
                        pc.playerData.abilityLocations.Remove(al);
                        break;
                    }
                }
                foreach (Animator a in gateAnimators)
                    a.SetBool("On", false);

                gateData.layer = Layer.BLOCK;
                gateLight.enabled = false;
                gcc.OpenGate(false);
                break;

            case 2:
                gateAnimators[0].SetTrigger("Blink");
                gateLight.enabled = true;
                gcc.Invoke("BlinkGateLight", 0.4f);
                break;
        }
        pc.Invoke("GateCooldown", state == 2 ? 0.2f : 1f);
    }

    // Set button on/off, 0: On (Crystal) | 1: On (Bullet) | 2: Off
    public static void SetButton(Data buttonData, int state, bool updatePanels) {
        if (buttonData.blockData.state == state)
            return;

        if (historyList != null && historyList.Count > 0) {
            History history = historyList[historyList.Count - 1];
            if (history == null) {
                history = new History();
                historyList.Add(history);
            }
            HistoryChange.Button b = new HistoryChange.Button(buttonData, buttonData.blockData.state);
            history.changes.Add(b);
        }

        buttonData.blockData.state = state;
        bool activate = state == 0 || state == 1;
        PlayRandomSound(activate ? AudioController.buttonOn : AudioController.buttonOff);
        buttonData.blockObject.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Blocks/Sprites/button_" + (activate ? "on" : "off"));
        buttonData.blockObject.GetComponent<Light2D>().enabled = activate;
        activeButtons[ConvertColorNameToIndex(buttonData.blockData.blockName)] += activate ? 1 : -1;

        if (!updatePanels)
            return;

        ButtonColor[] buttonColors = { ButtonColor.Red, ButtonColor.Green, ButtonColor.Blue };
        foreach (TunnelPanel tp in tunnelPanels) {
            if (tp.hasDoor)
                tp.SetButton(activate, buttonColors[ConvertColorNameToIndex(buttonData.blockData.blockName)]);
        }
    }

    // Turn off buttons powered by bullets
    public static void DeactivateBulletButtons() {
        foreach (var kvp in currentMiscBlocks) {
            if (kvp.Value.blockData.blockName.Contains("Button") && kvp.Value.blockData.state == 1)
                SetButton(kvp.Value, -1, true);
        }
    }

    // Create particles when block lands on ground
    public static void CreateLandingParticles(List<List<Data>> landDataList, int fallAmount) {
        if (!enableParticles)
            return;

        fallAmount = Mathf.Clamp(fallAmount, 0, 10);
        for (int i = 0; i < landDataList.Count; i++) {
            float xAverage = 0;
            foreach (var d in landDataList[i])
                xAverage += d.blockObject.transform.position.x;
            xAverage /= landDataList[i].Count;

            GameObject go = Instantiate(Resources.Load("BlockMisc/GroundLandingParticles") as GameObject,
                                        new Vector2(xAverage, landDataList[i][0].blockObject.transform.position.y),
                                        Quaternion.Euler(new Vector2(-90, 0)));
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1 + fallAmount * 0.5f);
            var shape = ps.shape;
            shape.scale = new Vector3(BLOCK_SIZE * landDataList[i].Count, 1, 1);
            ps.Emit(20 + fallAmount * 5);
        }
    }

    // Play particles for blocks moving across ground
    static void PlayGroundMovingParticles(List<Data> moveList, int xAmount) {
        foreach (Data d in moveList) {
            (int x, int y) groundCoord = (d.blockData.coordinates.x, d.blockData.coordinates.y - 1);
            (int x, int y) lastGroundCoord = (d.blockData.coordinates.x - xAmount, d.blockData.coordinates.y - 1);
            Data groundData = currentCoordinateSystem.GetData(groundCoord, Layer.BLOCK);
            Data lastGroundData = currentCoordinateSystem.GetData(lastGroundCoord, Layer.BLOCK);
            Data tunnelData = currentCoordinateSystem.GetData(lastGroundCoord, Layer.TUNNEL);
            if (tunnelData == null && lastGroundData != null && lastGroundData.blockData.blockName == "Ground" && groundData != null && groundData.blockData.blockName == "Ground") {
                ParticleSystem ps = d.blockObject.GetComponentInChildren<ParticleSystem>();
                ps.gameObject.transform.SetParent(null);
                ps.gameObject.transform.rotation = Quaternion.Euler(new Vector3(-90, 0, xAmount == 1 ? 0 : 180));
                ps.gameObject.transform.SetParent(d.blockObject.transform);
                ps.Play();
            }
        }
    }

    // Update undo outline for a block and show it
    public static void SetUndoOutline(Data data) {
        if (data.undoHolder != null) {
            ApplyData(data.blockData, data.undoHolder, false, false);
            data.undoHolder.SetActive(true);
            FadeUndoOutline(data.undoHolder, data.undoSprites);
        }
    }
    public static void FadeUndoOutline(GameObject outline, SpriteRenderer[] undoSprites) {
        gcc.FadeUndoOutline(outline, undoSprites);
    }

    // Get index for crystalColors
    public static int ConvertColorNameToIndex(string crystalName) {
        if (crystalName.Contains("Red")) return 0;
        if (crystalName.Contains("Green")) return 1;
        if (crystalName.Contains("Blue")) return 2;
        return -1;
    }

    // Close locked doors opened by player leaving tunnel
    public static void CheckTemporaryOpenDoors() {
        foreach (TunnelPanel tp in tunnelPanels) {
            if (tp.temporaryOpen) {
                Data playerData = currentCoordinateSystem.GetData(tp.doorData.blockData.coordinates, Layer.PLAYER);
                if (playerData == null)
                    tp.SetTemporaryDoor(false);
            }
        }
    }

    // Check for activated buttons and gate slots
    public static void CheckLevelMisc() {
        List<(Data, int)>[] buttonActivations = new List<(Data, int)>[3];
        foreach (var kvp in currentMiscBlocks) {
            Data data = kvp.Value;

            if (kvp.Value.blockData.blockName.Contains("Button")) {
                string color = data.blockData.blockName.Replace("Button", "|");
                color = color.Split('|')[0];
                int colorType = ConvertColorNameToIndex(kvp.Value.blockData.blockName);

                Data d = currentCoordinateSystem.GetData(data.blockData.coordinates, Layer.BLOCK);
                if (d == null) {
                    if (data.blockData.state == 0) {
                        if (buttonActivations[colorType] == null)
                            buttonActivations[colorType] = new List<(Data, int)>();
                        buttonActivations[colorType].Insert(0, (data, -1));
                    }
                    continue;
                }

                if (d.blockData.blockName == color + "Crystal") {
                    if (data.blockData.state == -1) {
                        if (buttonActivations[colorType] == null)
                            buttonActivations[colorType] = new List<(Data, int)>();
                        buttonActivations[colorType].Add((data, 0));
                    }
                    if (data.blockData.state == 1)
                        data.blockData.state = 0;
                }
                else {
                    if (data.blockData.state == 1) {
                        if (buttonActivations[colorType] == null)
                            buttonActivations[colorType] = new List<(Data, int)>();
                        buttonActivations[colorType].Insert(0, (data, -1));
                    }
                }
            }
        }

        bool[] updatePanels = new bool[3];
        for (int i = 0; i < buttonActivations.Length; i++) {
            if (buttonActivations[i] != null) {
                int count = 0;
                foreach (var ba in buttonActivations[i])
                    count += ba.Item2 == 0 ? 1 : -1;
                if (count != 0) {
                    updatePanels[i] = true;
                    break;
                }
            }
        }
        for (int i = 0; i < buttonActivations.Length; i++) {
            if (buttonActivations[i] != null) {
                foreach (var ba in buttonActivations[i])
                    SetButton(ba.Item1, ba.Item2, updatePanels[i]);
            }
        }
    }

    // Apply gravity
    public static void ApplyGravity() {
        if (applyingGravity)
            return;

        gcc.ApplyGravity();
    }

    // Apply random tiling based on adjacent blocks and sprite sheet
    public static void ApplyTiling(Data data, CoordinateSystem coordinateSystem) {
        // Check if block already has set tiling
        if (data.blockData.state > -1) {
            Sprite[] sprites = Resources.LoadAll<Sprite>("Blocks/Sprites/block_" + data.blockData.blockName.ToLower());
            if (sprites != null && sprites.Length > 0)
                data.blockObject.GetComponent<SpriteRenderer>().sprite = sprites[data.blockData.state];
            data.ApplyData();
            return;
        }

        bool[] nearBlocks = null;
        // If made of more than 1 block, only tile with connected blocks, else tile with blocks of same type
        if (data.HasTag(Tag.CONNECT)) {
            // Check which adjacent spaces have blocks
            nearBlocks = new bool[4];
            foreach (var c in data.blockData.connectedBlocks) {
                if (c != data.blockData.coordinates) {
                    (int x, int y) direction = (c.x - data.blockData.coordinates.x, c.y - data.blockData.coordinates.y);
                    for (int i = 0; i < nearBlocks.Length; i++) {
                        for (int j = 0; j < compassDirection.Length; j++) {
                            if (direction == compassDirection[j]) {
                                nearBlocks[i] = true;
                                break;
                            }
                        }
                    }
                }
            }
        }
        else
            nearBlocks = GetNearBlocks(data.blockData.coordinates, data.blockData.blockName, currentCoordinateSystem);

        if (nearBlocks != null) {
            // Match adjacent blocks to sprite types to determine which type of tiling it has
            bool done = false;
            for (int blockType = 0; blockType < spriteTypes.Length; blockType++) {
                for (int facingType = 0; facingType < spriteTypes[blockType].Length; facingType++) {
                    bool equal = true;
                    for (int i = 0; i < nearBlocks.Length; i++) {
                        if (nearBlocks[i] != spriteTypes[blockType][facingType][i]) {
                            equal = false;
                            break;
                        }
                    }
                    // Randomly pick block variation for tiling type
                    if (equal) {
                        int tileChance = 100;
                        int tileIndex = 0;
                        int[] tiles = infoDictionary[data.blockData.blockName].tiles;
                        if (tiles != null) {
                            for (int i = 0; i < tiles.Length; i++) {
                                int randomChance = Random.Range(0, 100 - tiles[i]);
                                if (randomChance < tileChance) {
                                    tileChance = randomChance;
                                    tileIndex = i;
                                }
                            }
                        }
                        Sprite[] sprites = Resources.LoadAll<Sprite>("Blocks/Sprites/block_" + data.blockData.blockName.ToLower());
                        int blockIndex = blockType + tileIndex * spriteTypes.Length;
                        if (sprites != null && sprites.Length > 0) {
                            if (data.blockData.state < 0 || data.blockData.state > sprites.Length - 1) {
                                data.blockObject.GetComponent<SpriteRenderer>().sprite = sprites[blockIndex];
                                data.blockData.state = blockIndex;
                            }
                            else
                                data.blockObject.GetComponent<SpriteRenderer>().sprite = sprites[data.blockData.state];
                        }

                        if (blockType == 0)
                            facingType = Random.Range(0, 4);
                        data.blockData.facing = facingType;
                        done = true;
                        break;
                    }
                }
                if (done)
                    break;
            }
        }
        // No adjacent blocks
        else {
            Sprite sprite = Resources.Load<Sprite>("Blocks/Sprites/block_" + data.blockData.blockName.ToLower() + "_single");
            if (sprite != null)
                data.blockObject.GetComponent<SpriteRenderer>().sprite = sprite;
        }
        data.ApplyData();
    }

    // Outputs history changes
    public static void VisualizeHistory() {
        foreach (History h in historyList) {
            string c = "";
            foreach (HistoryChange hc in h.changes)
                c += hc.changeType + ", ";
            print(c + " " + h.rollbacks);
        }
        print("\n");
    }
    // Outputs block object locations
    public static void VisualizeBlocks() {
        int size = 30;
        for (int i = size; i > -size; i--) {
            string line = "";
            for (int j = -size; j < size; j++) {
                line += "[";
                List<Data> dataList = currentCoordinateSystem.GetList((j, i));
                if (dataList != null) {
                    foreach (Data d in dataList)
                        line += " " + d.blockObject.name[0];
                }
                else
                    line += "   ";
                line += " ]";
            }
            print(line);
        }
    }
    // Outputs data locations
    public static void VisualizeData() {
        int size = 30;
        for (int i = size; i > -size; i--) {
            string line = "";
            for (int j = -size; j < size; j++) {
                line += "[";
                List<Data> dataList = currentCoordinateSystem.GetList((j, i));
                if (dataList != null) {
                    foreach (Data d in dataList)
                        line += " " + d.blockData.blockName[0];
                }
                else
                    line += "   ";
                line += " ]";
            }
            print(line);
        }
    }

    // Sounds
    public static void PlaySound(AudioClip clip) {
        if (!undoing)
            ac.audioSound.PlayOneShot(clip);
    }
    public static void PlayPitchedSound(AudioClip clip, float pitch) {
        if (!undoing)
            ac.PlayPitched(clip, pitch);
    }
    public static void PlayRandomSound(AudioClip clip) {
        if (!undoing)
            ac.PlayRandom(clip);
    }

    // Check if save files exist, if not create new level data
    public static void InitData() {
        string blockDataPath = Application.persistentDataPath + Path.DirectorySeparatorChar + "BlockData";
        if (!File.Exists(blockDataPath))
            Directory.CreateDirectory(blockDataPath);
        for (int i = 0; i < 4; i++) {
            if (!File.Exists(blockDataPath + Path.DirectorySeparatorChar + i))
                Directory.CreateDirectory(blockDataPath + Path.DirectorySeparatorChar + i);
        }

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++) {
            string levelName = SceneUtility.GetScenePathByBuildIndex(i).Split('/')[2].Split('.')[0];

            TextAsset ta = Resources.Load("BlockData/" + levelName) as TextAsset;
            if (ta == null)
                continue;

            for (int j = 0; j < 4; j++) {
                string path = blockDataPath + Path.DirectorySeparatorChar + j + Path.DirectorySeparatorChar + levelName + ".bld";
                if (!File.Exists(path)) {
                    File.WriteAllBytes(path, ta.bytes);
                }
            }
        }

        string playerDataPath = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData";
        if (!File.Exists(playerDataPath))
            Directory.CreateDirectory(playerDataPath);
        for (int i = 1; i < 4; i++) {
            if (!File.Exists(playerDataPath + Path.DirectorySeparatorChar + i))
                Directory.CreateDirectory(playerDataPath + Path.DirectorySeparatorChar + i);
        }

        if (!editMode) {
            for (int i = 1; i < 4; i++) {
                string mapPath = playerDataPath + Path.DirectorySeparatorChar + i + Path.DirectorySeparatorChar + "WorldMap" + ".mpd";
                if (!File.Exists(mapPath)) {
                    TextAsset ta = Resources.Load("PlayerData/" + "WorldMap") as TextAsset;
                    if (ta != null)
                        File.WriteAllBytes(mapPath, ta.bytes);
                }
            }
        }
    }
    // Save level state
    public static void SaveData(string fileName, int slot, CoordinateSystem coordinateSystem) {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "BlockData" + Path.DirectorySeparatorChar + slot + Path.DirectorySeparatorChar + fileName + ".bld";
        string resourcePath = "Assets/Resources/BlockData/" + fileName + ".bytes";

        List<BlockData> blockDatasList = new List<BlockData>();
        foreach (var kvp in coordinateSystem.coordinateData) {
            foreach (var data in kvp.Value) {
                if (!data.HasTag(Tag.PLAYER))
                    blockDatasList.Add(data.blockData);
            }
        }
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(path);
        BlockData[] blockDatas = new BlockData[blockDatasList.Count];
        blockDatasList.CopyTo(blockDatas, 0);
        bf.Serialize(file, blockDatas);
        file.Close();

        if (slot != 0)
            return;

        // Save default level state and delete old ones
#if (UNITY_EDITOR)
        bf = new BinaryFormatter();
        file = File.Create(resourcePath);
        bf.Serialize(file, blockDatas);
        file.Close();
        UnityEditor.AssetDatabase.Refresh();

        for (int i = 1; i < 4; i++) {
            string deletePath = Application.persistentDataPath + Path.DirectorySeparatorChar + "BlockData" + Path.DirectorySeparatorChar + i + Path.DirectorySeparatorChar + fileName + ".bld";
            if (File.Exists(deletePath))
                File.Delete(deletePath);
        }
        InitData();
#endif
    }
    // Delete all save data and refresh
    public static void DeleteData(int slot) {
        DirectoryInfo di = new DirectoryInfo(Application.persistentDataPath);

        foreach (DirectoryInfo dir in di.GetDirectories()) {
            if (dir.Name == "BlockData" || dir.Name == "PlayerData") {
                foreach (DirectoryInfo ddir in dir.GetDirectories()) {
                    if (ddir.Name == slot + "")
                        ddir.Delete(true);
                }
            }
        }
        InitData();
    }
    // Load level state
    public static CoordinateSystem LoadData(string fileName, int slot) {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "BlockData" + Path.DirectorySeparatorChar + slot + Path.DirectorySeparatorChar + fileName + ".bld";

        if (File.Exists(path)) {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            BlockData[] blockDatas = (BlockData[])bf.Deserialize(file);
            file.Close();

            GameObject level = GameObject.FindWithTag("Level");
            CoordinateSystem coordinateSystem = new CoordinateSystem(null);
            for (int i = 0; i < blockDatas.Length; i++) {
                Data newData = new Data(blockDatas[i], null);
                coordinateSystem.AddData(newData);
                if (newData.blockData.blockName == "CollectFragment")
                    fragmentChecks.Add(newData);
#if (UNITY_EDITOR)
                if (UnityEditor.EditorApplication.isPlaying) {
#endif
                    // Create pushable blocks
                    if (newData.HasTag(Tag.PUSH)) {
                        GameObject newBlock = Instantiate(Resources.Load("Blocks/" + newData.blockData.blockName) as GameObject);
                        newBlock.name = newData.blockData.blockName;
                        newData.blockObject = newBlock;
                        newData.ApplyData();
                        newBlock.transform.parent = level.transform;
                        if (newData.HasTag(Tag.FLOAT))
                            newBlock.GetComponent<SpriteRenderer>().color = crystalColors[ConvertColorNameToIndex(newData.blockData.blockName)];

                        if (newData.blockData.state == -1)
                            ApplyTiling(newData, coordinateSystem);
                        else {
                            Sprite[] sprites = Resources.LoadAll<Sprite>("Blocks/Sprites/block_" + newData.blockData.blockName.ToLower());
                            if (sprites != null && sprites.Length > 0)
                                newBlock.GetComponent<SpriteRenderer>().sprite = sprites[newData.blockData.state];
                        }
                    }
#if (UNITY_EDITOR)
                }
#endif
            }
            return coordinateSystem;
        }
        return null;
    }
    // Match gameobjects to stored data and set up other details
    public static void LoadBlocks(string fileName, int slot, CoordinateSystem coordinateSystem) {
        GameObject level = GameObject.FindWithTag("Level");
        PlayerController.PlayerData playerData = LoadPlayer(slot);
        bool inEditor = false;
#if (UNITY_EDITOR)
        inEditor = !UnityEditor.EditorApplication.isPlaying;
#endif

        if (level != null && level.name == fileName) {
            // Get original data for reset sprites
            Sprite[] undoSprites = Resources.LoadAll<Sprite>("Blocks/Sprites/block_outline_undo3");
            BinaryFormatter bf = new BinaryFormatter();
            TextAsset ta = Resources.Load("BlockData/" + fileName) as TextAsset;
            Stream s = new MemoryStream(ta.bytes);
            BlockData[] blockDatas = (BlockData[])bf.Deserialize(s);
            gateRoom = false;

            GameObject undoHolders = null;
            Color tc = new Color(timeColor.r / 255.0f, timeColor.g / 255.0f, timeColor.b / 255.0f);
            if (!inEditor && !editMode) {
                camCenter   = new GameObject("CamCenter");
                undoHolders = new GameObject("UndoHolders");
                resetHolder = new GameObject("ResetHolder");
                resetHolder.transform.position = Vector3.zero;
                resetHolder.SetActive(false);
                resetSprites = new List<SpriteRenderer>();
                tunnelResetSprite = Instantiate(Resources.Load("BlockMisc/TunnelOutline") as GameObject).GetComponent<SpriteRenderer>();
                resetSprites.Add(tunnelResetSprite);
                tunnelResetSprite.color = tc;
                tunnelResetSprite.gameObject.SetActive(false);
                tunnelResetSprite.transform.SetParent(resetHolder.transform);

                foreach (BlockData bd in blockDatas) {
                    Data d = new Data(bd, null);
                    if (d.HasTag(Tag.PUSH)) {
                        GameObject go = Instantiate(Resources.Load("Blocks/UndoOutline") as GameObject);
                        ApplyData(bd, go, false, true);
                        go.transform.SetParent(resetHolder.transform);
                        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                        resetSprites.Add(sr);
                        sr.color = tc;
                        sr.sprite = undoSprites[d.blockData.state < undoSprites.Length ? d.blockData.state : d.blockData.state % 6];
                        sr.sortingLayerName = "Undo";
                    }
                }
            }

            // Get blocks already present and set up additional effects
            Transform[] levelBlocks = level.GetComponentsInChildren<Transform>(true);
            if (levelBlocks != null) {
                currentMiscBlocks = new Dictionary<(int x, int y), Data>();
                tunnelPanels      = new List<TunnelPanel>();
                panelInfos        = new List<GameObject>();
                buttonInfos       = new List<GameObject>();
                startButtons      = new List<Data>();
                GameObject panelInfoHolder = null;
                GameObject parallax = null;
                GameObject[] parallaxHolders = null;
                Layer[] parallaxLayers = new Layer[] { Layer.SUPPORT, Layer.BG1, Layer.BG2 };
                GameObject panelInfoOutline = Resources.Load("BlockMisc/PanelInfoOutline") as GameObject;

                if (!inEditor) {
                    panelInfoHolder = new GameObject("PanelInfoHolder");
                    parallax = new GameObject("Parallax");
                    parallax.tag = "Parallax";
                    parallax.transform.SetParent(GameObject.Find("PlayerController").transform);
                    parallax.transform.localPosition = Vector3.zero;
                    parallaxHolders = new GameObject[] { new GameObject("SupportHolder"), new GameObject("GroundBG1Holder"), new GameObject("GroundBG2Holder") };
                    foreach (GameObject go in parallaxHolders) {
                        go.tag = "ParallaxHolder";
                        go.transform.SetParent(parallax.transform);
                        go.transform.localPosition = Vector3.zero;
                    }
                }

                foreach (Transform t in levelBlocks) {
                    if (t.tag != "Level" && t.transform.parent.transform.parent == null) {
                        t.position = new Vector2(SnapToGrid(t.position.x), SnapToGrid(t.position.y));
                        var coord = (GetGridCoordinate(t.position.x), GetGridCoordinate(t.position.y));
                        Data data = coordinateSystem.GetData(coord, infoDictionary[t.gameObject.name].layer);
                        data.blockObject = t.gameObject;

                        if (inEditor || editMode)
                            continue;

                        if (parallaxHolders != null) {
                            for (int i = 0; i < parallaxLayers.Length; i++) {
                                if (data.layer == parallaxLayers[i]) {
                                    data.blockObject.transform.SetParent(parallaxHolders[i].transform);
                                    break;
                                }
                            }
                        }

                        if (data.blockData.blockName == "Gate") {
                            gateRoom = true;
                            gateData = data;
                            gateAnimators = new Animator[2];
                            gateAnimators[0] = data.blockObject.GetComponentInChildren<Animator>();
                            gateLight = data.blockObject.GetComponentInChildren<Light2D>();
                            GameObject gi = Instantiate(data.blockObject.transform.GetChild(0).gameObject, data.blockObject.transform.GetChild(0));
                            gi.SetActive(false);
                            SpriteRenderer gsr = gi.GetComponent<SpriteRenderer>();
                            gateAnimators[1] = gi.GetComponent<Animator>();
                            gsr.sortingLayerName = "PanelInfo";
                            gsr.material = gcc.spriteDefault;

                            Instantiate(Resources.Load("BlockMisc/GateInfoOutline") as GameObject, gi.transform);
                            buttonInfos.Add(gi);
                            if (data.blockData.state == 0) {
                                gateAnimators[0].SetBool("On", true);
                                gateAnimators[1].SetBool("On", true);
                                gateAnimators[0].transform.localPosition = Vector3.up * 7;
                                gateLight.enabled = true;
                                data.layer = Layer.BLOCK_DESTROY;
                            }
                            currentMiscBlocks.Add(coord, data);
                            continue;
                        }

                        if (data.layer == Layer.MISC) {
                            currentMiscBlocks.Add(coord, data);
                            if (data.blockData.blockName.Contains("Button")) {
                                data.blockObject.GetComponent<SpriteRenderer>().color = data.blockObject.GetComponent<Light2D>().color = crystalColors[ConvertColorNameToIndex(data.blockData.blockName)];

                                GameObject bi = Instantiate(data.blockObject, panelInfoHolder.transform);
                                bi.SetActive(false);
                                SpriteRenderer[] srs = bi.GetComponentsInChildren<SpriteRenderer>();
                                foreach (SpriteRenderer sr in srs) {
                                    sr.sortingLayerName = "PanelInfo";
                                    sr.material = gcc.spriteDefault;
                                    if (sr.CompareTag("LevelMisc"))
                                        sr.sprite = buttonSprites[0];
                                }
                                Instantiate(panelInfoOutline, bi.transform);
                                buttonInfos.Add(bi);
                                startButtons.Add(data);
                                continue;
                            }

                            switch (data.blockData.blockName) {
                                case "TunnelPanel":
                                    Transform[] children = data.blockObject.GetComponentsInChildren<Transform>();
                                    foreach (Transform c in children) {
                                        if (!c.CompareTag("LevelMisc"))
                                            c.GetComponent<SpriteRenderer>().color = c.GetComponent<Light2D>().color = crystalColors[ConvertColorNameToIndex(c.name)];
                                    }

                                    GameObject tpi = Instantiate(data.blockObject, panelInfoHolder.transform);
                                    tpi.SetActive(false);
                                    SpriteRenderer[] srs = tpi.GetComponentsInChildren<SpriteRenderer>();
                                    foreach (SpriteRenderer sr in srs) {
                                        sr.sortingLayerName = "PanelInfo";
                                        sr.material = gcc.spriteDefault;
                                        if (!sr.CompareTag("LevelMisc"))
                                            sr.sprite = panelSprites[0];
                                    }
                                    Instantiate(panelInfoOutline, tpi.transform);
                                    panelInfos.Add(tpi);
                                    continue;

                                case "TunnelDoor":
                                    tunnelPanels.Add(data.blockObject.GetComponent<TunnelPanel>());
                                    continue;

                                case "GateSlot":
                                    GameObject gsi = Instantiate(data.blockObject, panelInfoHolder.transform);
                                    GameObject fi = Instantiate(Resources.Load("BlockMisc/CollectFragment") as GameObject, gsi.transform);
                                    fi.GetComponent<Light2D>().enabled = false;
                                    gsi.SetActive(false);
                                    fi.SetActive(false);
                                    fi.transform.SetParent(panelInfoHolder.transform);
                                    SpriteRenderer gssr = gsi.GetComponent<SpriteRenderer>();
                                    SpriteRenderer fsr = fi.GetComponent<SpriteRenderer>();
                                    gssr.sortingLayerName = fsr.sortingLayerName = "PanelInfo";
                                    gssr.material = fsr.material = gcc.spriteDefault;
                                    fsr.sortingOrder = 1;

                                    Instantiate(panelInfoOutline, gsi.transform);
                                    buttonInfos.Add(gsi);
                                    buttonInfos.Add(fi);
                                    continue;
                            }
                        }

                        if (!editMode && data.HasTag(Tag.PUSH)) {
                            if (data.blockData.IsPrimary()) {
                                // Init undo sprites
                                GameObject undoHolder = new GameObject("UndoHolder");
                                undoHolder.SetActive(false);
                                undoHolder.transform.SetParent(undoHolders.transform);
                                ApplyData(data.blockData, undoHolder, false, false);
                                data.undoHolder = undoHolder;
                                
                                foreach (var c in data.blockData.connectedBlocks) {
                                    Data d = currentCoordinateSystem.GetData(c, data.layer);
                                    GameObject go = Instantiate(Resources.Load("Blocks/UndoOutline") as GameObject);
                                    ApplyData(d.blockData, go, false, true);
                                    go.transform.SetParent(undoHolder.transform);
                                    SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                                    sr.color = tc;
                                    if (undoSprites != null && undoSprites.Length > 0) {
                                        sr.sprite = undoSprites[d.blockData.state < undoSprites.Length ? d.blockData.state : d.blockData.state % 6];
                                        sr.sortingLayerName = "Undo";
                                    }
                                }
                                data.undoSprites = undoHolder.GetComponentsInChildren<SpriteRenderer>();
                            }
                            // Init ground moving particles
                            if (!data.HasTag(Tag.FLOAT))
                                Instantiate(gcc.groundMovingParticles, data.blockObject.transform);
                        }

                        // Init floating sprites
                        if (!inEditor && data.HasTag(Tag.FLOAT)) {
                            GameObject crystralActivation = Resources.Load("BlockMisc/CrystalActivation") as GameObject;
                            Instantiate(crystralActivation, data.blockObject.transform);

                            if (data.blockData.IsPrimary()) {
                                GameObject floatHolder = new GameObject("FloatHolder");
                                ApplyData(data.blockData, floatHolder, false, false);
                                floatHolder.AddComponent(typeof(FloatMovement));
                                FloatMovement fm = floatHolder.GetComponent<FloatMovement>();
                                Sprite[] sprites = Resources.LoadAll<Sprite>("Blocks/Sprites/block_outline_" + data.blockData.blockName.ToLower());
                                
                                foreach (var c in data.blockData.connectedBlocks) {
                                    Data d = currentCoordinateSystem.GetData(c, data.layer);
                                    GameObject go = Instantiate(Resources.Load("Blocks/CrystalOutline") as GameObject);
                                    ApplyData(d.blockData, go, false, true);
                                    go.transform.SetParent(floatHolder.transform);
                                    Light2D light = go.GetComponent<Light2D>();
                                    go.GetComponent<SpriteRenderer>().color = light.color = crystalColors[ConvertColorNameToIndex(data.blockData.blockName)];
                                    if (sprites != null && sprites.Length > 0) {
                                        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                                        light.lightCookieSprite = sr.sprite = sprites[d.blockData.state < sprites.Length ? d.blockData.state : d.blockData.state % 6];
                                        sr.sortingLayerName = "Float";
                                    }
                                }
                                floatHolder.transform.SetParent(data.blockObject.transform);
                                fm.Init(data);
                            }
                        }

                        // Deactivate blocks that were destroyed
                        if ((data.layer == Layer.BLOCK || data.layer == Layer.BLOCK_DESTROY) && data.blockData.destroyed) {
                            if (data.blockData.IsPrimary()) {
                                GameObject floatHolder = new GameObject("FloatHolder");
                                ApplyData(data.blockData, floatHolder, false, false);
                                floatHolder.AddComponent(typeof(FloatMovement));
                                FloatMovement fm = floatHolder.GetComponent<FloatMovement>();
                                
                                foreach (var c in data.blockData.connectedBlocks) {
                                    Data d = currentCoordinateSystem.GetData(c, Layer.BLOCK);
                                    GameObject crystalBreak = Instantiate(Resources.Load("Blocks/BlueCrystalBreak") as GameObject);
                                    ApplyData(d.blockData, crystalBreak, false, true);
                                    crystalBreak.transform.SetParent(floatHolder.transform);
                                    crystalBreak.GetComponent<SpriteRenderer>().color = crystalBreak.GetComponent<Light2D>().color = crystalColors[2];
                                    Sprite[] sprites = Resources.LoadAll<Sprite>("Blocks/Sprites/block_bluecrystal_break");
                                    if (sprites != null && sprites.Length > 0)
                                        crystalBreak.GetComponent<SpriteRenderer>().sprite = sprites[d.blockData.state / 6];
                                }
                                fm.Init(data);
                            }

                            data.blockObject.SetActive(false);
                            data.layer = Layer.BLOCK_DESTROY;
                        }

                        if (data.layer == Layer.COLLECT) {
                            if (data.blockData.blockName != "CollectFragment") {
                                int colorIndex = ConvertColorNameToIndex(data.blockData.blockName);
                                if (colorIndex != -1)
                                    data.blockObject.GetComponent<SpriteRenderer>().color = data.blockObject.GetComponent<Light2D>().color = crystalColors[colorIndex];
                                else {
                                    if (data.blockData.blockName == "CollectTime")
                                        data.blockObject.GetComponent<SpriteRenderer>().color = data.blockObject.GetComponent<Light2D>().color = timeColor;
                                }
                            }

                            // Deactivate collectables that were collected
                            if (playerData != null && playerData.abilityLocations.Count > 0) {
                                foreach (var al in playerData.abilityLocations) {
                                    if (al.coordinates == data.blockData.coordinates && al.room == fileName) {
                                        data.blockObject.SetActive(false);
                                        data.blockData.destroyed = true;
                                        data.layer = Layer.COLLECT_DESTROY;
                                    }
                                    else {
                                        if (data.blockData.blockName != "CollectFragment") {
                                            data.blockObject.GetComponent<FloatMovement>().Init(data);
                                            break;
                                        }
                                    }
                                }
                            }
                            else {
                                if (data.blockData.blockName != "CollectFragment")
                                    data.blockObject.GetComponent<FloatMovement>().Init(data);
                            }

                        }
                    }
                }
                if (fragmentChecks.Count > 0) {
                    foreach (Data d in fragmentChecks) {
                        if (d.blockObject == null) {
                            GameObject go = Instantiate(Resources.Load("BlockMisc/CollectFragment") as GameObject);
                            d.blockObject = go;
                            if (d.blockData.destroyed) {
                                d.blockObject.SetActive(false);
                                d.blockData.destroyed = true;
                                d.layer = Layer.COLLECT_DESTROY;
                            }
                            d.ApplyData();
                        }
                        Data slotData = coordinateSystem.GetData(d.blockData.coordinates, Layer.MISC);
                        if (slotData == null || slotData.blockData.blockName != "GateSlot")
                            d.blockObject.GetComponent<FloatMovement>().Init(d);
                    }
                    fragmentChecks = new List<Data>();
                }
            }
        }
    }
    // Save player state
    public static void SavePlayer(PlayerController playerController) {
        if (playerController == null)
            return;

        if (!File.Exists(Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + currentSave))
            Directory.CreateDirectory(Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + currentSave);

        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + currentSave + Path.DirectorySeparatorChar + "Player.pld";

        playerController.playerData.blockData = new BlockData[playerController.wormData.Count];
        for (int i = 0; i < playerController.playerData.blockData.Length; i++)
            playerController.playerData.blockData[i] = playerController.wormData[i].blockData;

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(path);
        bf.Serialize(file, playerController.playerData);
        file.Close();
    }
    // Load player state
    public static PlayerController.PlayerData LoadPlayer(int slot) {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + slot + Path.DirectorySeparatorChar + "Player.pld";

        if (File.Exists(path)) {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            PlayerController.PlayerData playerData = (PlayerController.PlayerData)bf.Deserialize(file);
            file.Close();

            return playerData;
        }
        return null;
    }
    // Save region data
    public static void SaveMap(MapSystem mapSystem) {
        GameControllerCoroutines gcc = GameObject.FindGameObjectWithTag("GameController").GetComponent<GameControllerCoroutines>();
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + currentSave + Path.DirectorySeparatorChar + gcc.map.name + ".mpd";
        string resourcePath = "Assets/Resources/PlayerData/" + gcc.map.name + ".bytes";

        List<RegionData.RegionBlockData> mrd = new List<RegionData.RegionBlockData>();
        foreach (var kvp in mapSystem.mapData)
            mrd.Add(kvp.Value.blockData);

        RegionData.RegionBlockData[] mapRegions = new RegionData.RegionBlockData[mrd.Count];
        mrd.CopyTo(mapRegions);

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(path);
        bf.Serialize(file, mapRegions);
        file.Close();

#if (UNITY_EDITOR)
        if (UnityEditor.EditorApplication.isPlaying)
            return;

        foreach (var rbd in mapRegions) {
            rbd.visited = false;
            foreach (var rt in rbd.regionTunnels)
                rt.visited = false;
        }

        bf = new BinaryFormatter();
        file = File.Create(resourcePath);
        bf.Serialize(file, mapRegions);
        file.Close();
#endif
    }
    // Load region data
    public static MapSystem LoadMap() {
        GameControllerCoroutines gcc = GameObject.FindGameObjectWithTag("GameController").GetComponent<GameControllerCoroutines>();
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + currentSave + Path.DirectorySeparatorChar + gcc.map.name + ".mpd";
        MapSystem mapSystem = new MapSystem();

        if (File.Exists(path)) {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            RegionData.RegionBlockData[] rbd = (RegionData.RegionBlockData[])bf.Deserialize(file);
            file.Close();

            for (int i = 0; i < rbd.Length; i++) {
                RegionData rd = new RegionData(rbd[i], null);
                mapSystem.AddRegion(rbd[i].level, rd);
            }
        }
        return mapSystem;
    }
    public static void InitMap(GameObject map, MapSystem mapSystem) {
        if (map != null) {
            MapRegion[] regions = map.GetComponentsInChildren<MapRegion>();
            foreach (MapRegion m in regions) {
                RegionData rd = mapSystem.GetRegion(m.name);
                if (rd != null) {
                    rd.regionObject = m.gameObject;
                    rd.regionTexture = new Texture2D(100, 100);
                    rd.regionTexture.filterMode = FilterMode.Point;
                    for (int y = 0; y < rd.regionTexture.height; y++) {
                        for (int x = 0; x < rd.regionTexture.width; x++)
                            rd.regionTexture.SetPixel(x, y, Color.clear);
                    }
                    rd.regionTexture.Apply();
                    m.gameObject.GetComponent<RawImage>().texture = rd.regionTexture;
                }
            }
            if (gcc != null && gcc.mapHolder != null)
                gcc.mapHolder.SetActive(true);
        }
    }
}