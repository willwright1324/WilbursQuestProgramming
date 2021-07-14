using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.Rendering.LWRP;

// Controls player abilities and movement for the worm
public class PlayerController : MonoBehaviour {
    // Data that is saved
    [System.Serializable]
    public class PlayerData {
        public string save = "NewPlayer";
        public string currentRoom;
        public int fragments;
        public int length = 3;
        public int currMaxLength = 3;
        public int maxLength = 15;
        public List<bool> flippedBlocks;
        public BlockData[] blockData;
        public bool[] colors    = new bool[3]; // [Red, Green, Blue]
        public bool[] abilities = new bool[4]; // [Shoot, Grow, Undo, Reset]
        public List<AbilityLocation> abilityLocations = new List<AbilityLocation>();

        [System.Serializable]
        public class AbilityLocation {
            public string room;
            public (int x, int y) coordinates;

            public AbilityLocation(string room, (int x, int y) coordinates) {
                this.room        = room;
                this.coordinates = coordinates;
            }
        }
    }
    public PlayerData playerData;

    public class Worm {
        public GameObject wormObject;
        public Light2D light;
        public SpriteRenderer spriteRenderer;
        public GameObject undoObject;
        public SpriteRenderer undoSpriteRenderer;
        public GameObject groundParticles;
        public ParticleSystem groundParticlesSystem;

        public Worm(GameObject wormObject, Light2D light, SpriteRenderer spriteRenderer, GameObject undoObject, SpriteRenderer undoSpriteRenderer, GameObject groundParticles, ParticleSystem groundParticlesSystem) {
            this.wormObject            = wormObject;
            this.light                 = light;
            this.spriteRenderer        = spriteRenderer;
            this.undoObject            = undoObject;
            this.undoSpriteRenderer    = undoSpriteRenderer;
            this.groundParticles       = groundParticles;
            this.groundParticlesSystem = groundParticlesSystem;
        }
    }
    public Worm[] worm;

    public class MapElement {
        public class RegionElement {
            public string room;
            public Texture2D texture;
            public List<RegionData.RegionBlockData.RegionCoordinate> regionCoordinates = new List<RegionData.RegionBlockData.RegionCoordinate>();

            public RegionElement(string room, Texture2D texture) {
                this.room = room;
                this.texture = texture;
            }
        }

        public int index;
        public float mapScale;
        public Vector3 mapPos;
        public List<RegionElement> regionElements = new List<RegionElement>();

        public MapElement(int index) {
            this.index = index;
        }
    }

    [Header("Game")]
    public bool startSave;
    bool ending;

    [Header("Map")]
    public GameObject map;
    public GameObject mapHolder;
    int mapTraversalIndex;
    int prevMapTraversalIndex;
    float mapTraversalWait;
    float MAP_TRAVERSAL_SPEED = 40f;
    bool showMap;
    bool showingMap;
    bool mapReleased;
    bool mapEnabled;
    List<(int x, int y)> clearMapWormCoords = new List<(int x, int y)>();
    int activeMapCalculations;
    bool doneCalculatingMap;
    float MAP_MOVE_SPEED = 500;
    Vector3 MAP_START_ZOOM = Vector3.one * 2;
    Vector3 mapPosition;
    public MapElement[] mapTraversalList;
    List<MapElement> mapElements;
    List<RegionData> calculatedRegions;
    float[] mapBorders = new float[4];
    LineRenderer[] mapLines = new LineRenderer[4];
    GameObject[] mapLineObjects = new GameObject[4];
    IEnumerator mapWormBlinkCoroutine;

    [Header("Worm")]
    public Data headData;
    GameObject playerIcon;
    public GameObject headTrigger;
    public GameObject wormPiece;
    public GameObject wormHolder;
    public List<Data> wormData;
    public bool disableMovement;
    public bool playerMoving;
    public bool flyMode;
    public float moveCooldown;
    public float moveCooldownMax = 0.2f;
    IEnumerator movePlayerCoroutine;
    public Sprite[] cornerSprites;
    public Sprite[] secondCornerSprites;
    public Sprite[] growSprites;
    public GameObject eye;
    public Animator eyeAnimator;
    public Color32 eyeDefaultColor = new Color32(61, 49, 55, 255);
    public SpriteRenderer eyeSprite;
    public Light2D eyeLight;
    Color32 wormColor = new Color32(213, 144, 179, 255);
    Color32[] gradientColors;
    List<IEnumerator> gradientCoroutines = new List<IEnumerator>();

    [Header("Animation")]
    public Animator headAnimator;
    float blinkTime;
    float currentSleepTime;
    public float sleepTime = 20f;
    public bool eat;
    bool shaking;
    bool canShoot = true;
    bool sleeping;
    bool lookChecked;
    bool thinking;
    int selectedThink;
    IEnumerator resetLookFacingCoroutine;

    [Header("Undo / Reset")]
    GameObject undoHolder;
    GameObject undoOutlineHolder;
    Sprite[] undoSprites;
    float undoCooldown;
    float undoCooldownCurrentMax;
    float UNDO_COOLDOWN_MAX = 0.4f;
    float UNDO_COOLDOWN_MIN = 0.2f;
    float UNDO_COOLDOWN_STEP = 0.1f;
    float undoPitch;
    int undoCount;
    bool undoBlock;
    float RESET_TIME = 3;
    float currentResetTime;

    [Header("Grid / PanelInfo")]
    public GameObject gridObject;
    public GameObject grid;
    public bool showGrid;
    bool showingGrid;
    public float currentGridSize;
    public float gridSpeed;
    public float GRID_SPEED_MIN = 5f;
    public float GRID_SPEED_MAX = 20f;
    public float GRID_ACCELERATION = 10f;
    public int gridSize = 25;
    public GameObject[,] gridObjects;
    bool showPanelInfo;
    bool showingPanelInfo;

    private void Awake() { GameController.Init(); }

    void Start() {
        if (GameController.editMode)
            return;

        string levelName = GameObject.FindGameObjectWithTag("Level").name;
        if (levelName.StartsWith("Ending"))
            ending = true;

        undoSprites = Resources.LoadAll<Sprite>("Blocks/Sprites/block_outline_undo3");
        undoCooldownCurrentMax = UNDO_COOLDOWN_MAX;
        currentSleepTime = sleepTime;
        blinkTime = Random.Range(3, 10);
        map = GameController.GetMap();
        mapHolder = GameController.GetMapHolder();

        if (GameController.initialStart) {
            currentSleepTime = 0;
            GameController.initialStart = false;
        }

        BuildWorm();

        if (ending) {
            GameObject.Find("CutsceneController").GetComponent<CutsceneController>().Init();
            return;
        }

        GameController.InitLengthMeter();
        GameController.InitPanels();

        RegionData rd = GameController.currentMapSystem.GetRegion(playerData.currentRoom);
        mapEnabled = rd != null && rd.regionObject != null;

        if (mapEnabled)
            StartCoroutine(CalculateMap());

        gridSpeed = GRID_SPEED_MIN;
        StartCoroutine(GridScaling());
    }

    void Update() {
        if (GameController.editMode)
            return;

        // Blink timer
        if (blinkTime > 0)
            blinkTime -= Time.deltaTime;
        else {
            if (!GameController.shooting && sleepTime - currentSleepTime > 1)
                SetHeadAnimation("Blink", 2);
            blinkTime = Random.Range(3, 10);
        }

        // Sleep timer
        if (currentSleepTime > 0)
            currentSleepTime -= Time.deltaTime;
        else {
            if (!sleeping) {
                sleeping = true;
                SetHeadAnimation("Sleep", 0);
            }
        }

        // Cooldown between movements
        if (moveCooldown > 0) moveCooldown -= Time.deltaTime;
        else                  moveCooldown = 0;

        if (moveCooldown == 0 && !lookChecked && !headAnimator.GetCurrentAnimatorStateInfo(0).IsName("Nod")) {
            lookChecked = true;
            LookCheck();
        }

        // Developer Controls
        if (GameController.devControls) {
            // Slow down time
            if (Input.GetKeyDown(KeyCode.T))
                Time.timeScale = 0.1f;

            // Toggle color abilities
            if (Input.GetKeyDown(KeyCode.Alpha1)) {
                playerData.colors[0] = !playerData.colors[0];
                playerData.abilities[0] = playerData.colors[0] || playerData.colors[1] || playerData.colors[2];
            }
            if (Input.GetKeyDown(KeyCode.Alpha2)) {
                playerData.colors[1] = !playerData.colors[1];
                playerData.abilities[0] = playerData.colors[0] || playerData.colors[1] || playerData.colors[2];
            }
            if (Input.GetKeyDown(KeyCode.Alpha3)) {
                playerData.colors[2] = !playerData.colors[2];
                playerData.abilities[0] = playerData.colors[0] || playerData.colors[1] || playerData.colors[2];
            }
            // Enable grow ability and add length
            if (Input.GetKeyDown(KeyCode.Alpha4)) {
                if (playerData.currMaxLength < playerData.maxLength)
                    playerData.currMaxLength++;
                playerData.abilities[1] = true;
            }
            // Enable undo and reset abilities
            if (Input.GetKeyDown(KeyCode.Alpha5)) {
                playerData.abilities[2] = true;
                playerData.abilities[3] = true;
            }
            // Add fragment to inventory
            if (Input.GetKeyDown(KeyCode.Alpha6))
                playerData.fragments++;
            // Unlock full map
            if (Input.GetKeyDown(KeyCode.Alpha7)) {
                doneCalculatingMap = false;
                foreach (var kvp in GameController.currentMapSystem.mapData) {
                    foreach (var rt in kvp.Value.blockData.regionTunnels)
                        rt.visited = true;
                }
                StartCoroutine(CalculateMap());
            }

            // Go to previous level
            if (Input.GetKeyDown(KeyCode.Q)) {
                GameController.enterTunnel = true;
                GameController.enterTunnelID = 0;
                int nextRoom = SceneManager.GetActiveScene().buildIndex == 1 ? SceneManager.sceneCountInBuildSettings - 1 : SceneManager.GetActiveScene().buildIndex - 1;
                GameController.currentRoom = playerData.currentRoom = SceneManager.GetSceneByBuildIndex(nextRoom).name;
                SceneManager.LoadScene(nextRoom);
            }
            // Go to next level
            if (Input.GetKeyDown(KeyCode.E)) {
                GameController.enterTunnel = true;
                GameController.enterTunnelID = 0;
                int nextRoom = SceneManager.GetActiveScene().buildIndex == SceneManager.sceneCountInBuildSettings - 1 ? 1 : SceneManager.GetActiveScene().buildIndex + 1;
                GameController.currentRoom = playerData.currentRoom = SceneManager.GetSceneByBuildIndex(nextRoom).name;
                SceneManager.LoadScene(nextRoom);
            }
        }

        // Pause game
        if (Input.GetKeyDown(KeyCode.Escape) && !Input.GetKey(KeyCode.X)) {
            GameController.Pause(!GameController.paused);
            return;
        }

        // Cancel active bullets
        if (Input.GetKeyDown(KeyCode.Z) && GameController.shooting)
            GameController.cancelBullets = true;

        if (playerMoving || GameController.moving || GameController.paused || disableMovement || GameController.tunneling || GameController.shooting || GameController.applyingGravity || eat)
            return;

        if (!GameController.resetting) {
            // Think abilities
            if (Input.GetKey(KeyCode.V)) {
                WakeUp();
                if (selectedThink == 0) {
                    if (!thinking) {
                        SetHeadAnimation("Think", 0);
                        thinking = true;
                    }

                    // Map
                    if (Input.GetKeyDown(KeyCode.UpArrow)) {
                        selectedThink = 1;
                        return;
                    }
                    // Grid
                    if (Input.GetKeyDown(KeyCode.DownArrow)) {
                        selectedThink = 2;
                        return;
                    }
                    // Panel info
                    if (Input.GetKeyDown(KeyCode.RightArrow)) {
                        if (GameController.panelInfos.Count > 0 || GameController.buttonInfos.Count > 0)
                            selectedThink = 3;
                        return;
                    }
                }

                switch (selectedThink) {
                    case 1:
                        if (mapEnabled) {
                            // Show map
                            if (doneCalculatingMap)
                                ShowMap(true);
                            // Map movement
                            if (showingMap) {
                                float speed = Time.deltaTime * MAP_MOVE_SPEED;
                                Vector3 mapPos = map.transform.localPosition;
                                if (Input.GetKey(KeyCode.UpArrow) && mapPos.y > mapBorders[0])
                                    map.transform.position += Vector3.down * speed;
                                if (Input.GetKey(KeyCode.DownArrow) && mapPos.y < mapBorders[1])
                                    map.transform.position += Vector3.up * speed;
                                if (Input.GetKey(KeyCode.LeftArrow) && mapPos.x < mapBorders[2])
                                    map.transform.position += Vector3.right * speed;
                                if (Input.GetKey(KeyCode.RightArrow) && mapPos.x > mapBorders[3])
                                    map.transform.position += Vector3.left * speed;
                            }
                        }
                        break;

                    case 2:
                        // Show grid
                        if (!showGrid) {
                            currentGridSize = (int)currentGridSize;
                            showingGrid = true;
                        }
                        showGrid = true;

                        if (currentGridSize < gridSize - 1) {
                            currentGridSize += Time.deltaTime * gridSpeed;
                            gridSpeed = currentGridSize * GRID_ACCELERATION;
                            gridSpeed = Mathf.Clamp(gridSpeed, GRID_SPEED_MIN, GRID_SPEED_MAX);
                        }
                        else
                            currentGridSize = gridSize - 0.1f;
                        break;

                    case 3:
                        // Show panel info
                        if (!showingPanelInfo) {
                            showPanelInfo = true;
                            StartCoroutine(ShowPanelInfo());
                        }
                        break;
                }
            }
            else {
                if (thinking && selectedThink == 0) {
                    SetHeadAnimation("Think", 1);
                    thinking = false;
                }

                // Hide map
                if (doneCalculatingMap)
                    ShowMap(false);
                if (showingMap && !showMap)
                    map.transform.localPosition = Vector3.MoveTowards(map.transform.localPosition, mapPosition, Time.deltaTime * MAP_MOVE_SPEED);

                // Hide grid
                if (showGrid && currentGridSize < gridSize)
                    currentGridSize = (int)currentGridSize + 1;
                showGrid = false;

                if (currentGridSize > 0) {
                    currentGridSize -= Time.deltaTime * gridSpeed;
                    gridSpeed = currentGridSize * GRID_ACCELERATION;
                    gridSpeed = Mathf.Clamp(gridSpeed, GRID_SPEED_MIN, GRID_SPEED_MAX);
                }
                else
                    currentGridSize = 0;

                if (currentGridSize == 0 && showingGrid) {
                    showingGrid = false;
                    selectedThink = 0;
                    SetHeadAnimation("Think", 1);
                }

                // Hide panel info
                if (showPanelInfo) {
                    selectedThink = 0;
                    SetHeadAnimation("Think", 1);
                    showPanelInfo = false;
                }
            }
        }

        if (thinking || showingMap || showingGrid || showingPanelInfo)
            return;

        // Stop reset
        if (Input.GetKeyUp(KeyCode.B)) {
            ResetEyes(false);
            GameController.resetting = false;
            currentResetTime = 0;
            return;
        }
        // Start reset
        if (Input.GetKey(KeyCode.B)) {
            if (currentResetTime < RESET_TIME)
                currentResetTime += Time.deltaTime;
            else {
                GameController.ResetRoom(false);
                return;
            }
        }

        if (GameController.resetting)
            return;

        // Switch gravity
        if (Input.GetKey(KeyCode.D)) {
            WakeUp();
            if (Input.GetKey(KeyCode.UpArrow)) {
                SwitchGravity((0, 1));
                return;
            }
            if (Input.GetKey(KeyCode.DownArrow)) {
                SwitchGravity((0, -1));
                return;
            }
            if (Input.GetKey(KeyCode.LeftArrow)) {
                SwitchGravity((-1, 0));
                return;
            }
            if (Input.GetKey(KeyCode.RightArrow)) {
                SwitchGravity((1, 0));
                return;
            }
        }

        if (Input.GetKey(KeyCode.X) && playerData.abilities[1]) {
            if (moveCooldown <= 0) {
                if (!eyeAnimator.GetBool("Shake"))
                    SetHeadAnimation("Shake", 0);
                if (!shaking) {
                    shaking = true;
                    GameController.PlaySound(AudioController.playerShake);
                    GameController.ShowLengthMeter(true);
                }
                WakeUp();

                // Grow/Shrink
                if      (Input.GetKeyDown(KeyCode.UpArrow))    GrowPlayer((0, 1), false);
                else if (Input.GetKeyDown(KeyCode.DownArrow))  GrowPlayer((0, -1), false);
                else if (Input.GetKeyDown(KeyCode.LeftArrow))  GrowPlayer((-1, 0), false);
                else if (Input.GetKeyDown(KeyCode.RightArrow)) GrowPlayer((1, 0), false);
            }
        }
        else {
            if (shaking) {
                shaking = false;
                if (GameController.gcc.abilityTriggers[1])
                    GameController.ShowAbilityInfo(1, false);
                SetHeadAnimation("Shake", 1);
                GameController.ShowLengthMeter(false);
            }

            if (moveCooldown <= 0) {
                // Move
                if (Input.GetKey(KeyCode.UpArrow)) {
                    MovePlayer((0, 1), false);
                    return;
                }
                if (Input.GetKey(KeyCode.DownArrow)) {
                    MovePlayer((0, -1), false);
                    return;
                }
                if (Input.GetKey(KeyCode.LeftArrow)) {
                    MovePlayer((-1, 0), false);
                    return;
                }
                if (Input.GetKey(KeyCode.RightArrow)) {
                    MovePlayer((1, 0), false);
                    return;
                }

                // Shoot
                if (Input.GetKey(KeyCode.Z) && playerData.abilities[0] && canShoot) {
                    if (GameController.gcc.abilityTriggers[0])
                        GameController.ShowAbilityInfo(0, false);
                    WakeUp();
                    GameController.Shoot(headData.blockData.coordinates, headData.blockData.facing);
                }
            }

            // Reset
            if (Input.GetKey(KeyCode.B) && playerData.abilities[3]) {
                if (GameController.gateRoom) {
                    GameController.PlayRandomSound(AudioController.playerBlocked);
                    GameController.resetting = true;
                }
                else {
                    if (GameController.historyList != null && GameController.historyList.Count > 0) {
                        if (GameController.gcc.abilityTriggers[3])
                            GameController.ShowAbilityInfo(3, false);
                        WakeUp();
                        GameController.resetting = true;
                        SetHeadAnimation("StopNod", 0);
                        GameController.ShowResets();
                        ResetEyes(true);
                    }
                }
            }

            // Undo
            if (!undoBlock && !GameController.undoing) {
                if (playerData.abilities[2] && Input.GetKey(KeyCode.C)) {
                    if (undoCooldown > 0)
                        undoCooldown -= Time.deltaTime;
                    else {
                        if (undoCooldownCurrentMax > UNDO_COOLDOWN_MIN) {
                            if (undoCount < 3)
                                undoCount++;
                            else {
                                undoPitch += 0.1f;
                                undoCooldownCurrentMax -= UNDO_COOLDOWN_STEP;
                                undoCount = 0;
                            }
                        }
                        undoCooldown = undoCooldownCurrentMax;

                        if (GameController.historyList != null && (GameController.historyList.Count > 1 || (GameController.historyList.Count > 0 && GameController.startRoom == playerData.currentRoom))) {
                            if (GameController.gcc.abilityTriggers[2])
                                GameController.ShowAbilityInfo(2, false);
                            GameController.Undo(false, AudioController.GetRandomPitch(undoPitch));
                        }
                        else {
                            GameController.PlayRandomSound(AudioController.playerBlocked);
                            undoBlock = true;
                        }
                    }
                }
                else {
                    undoCooldownCurrentMax = UNDO_COOLDOWN_MAX;
                    undoCooldown = 0;
                    undoCount = 0;
                    undoPitch = 1;
                }
            }
            if (Input.GetKeyUp(KeyCode.C))
                undoBlock = false;
        }
    }

    // Move player
    public void MovePlayer((int x, int y) amount, bool tunnelMove) {
        if (playerMoving)
            return;

        WakeUp();
        lookChecked = false;

        // Move backwards into tunnel
        if (!tunnelMove && amount == GameController.backFacingDirection[headData.blockData.facing]) {
            (int x, int y) tunnelCoord = headData.blockData.connectedBlocks[1];
            Data tunnelData = GameController.currentCoordinateSystem.GetData(tunnelCoord, Layer.TUNNEL);
            if (tunnelData == null)
                return;

            tunnelCoord = tunnelData.blockData.connectedBlocks[0];
            if (tunnelData.blockData.coordinates == tunnelData.blockData.connectedBlocks[1])
                tunnelData = GameController.currentCoordinateSystem.GetData(tunnelCoord, Layer.TUNNEL);
            if (tunnelData.blockData.coordinates == tunnelCoord) {
                RegionData rd = GameController.currentMapSystem.GetRegion(playerData.currentRoom);
                RegionData.RegionTunnel tunnel = null;
                foreach (var rt in rd.blockData.regionTunnels) {
                    if (rt.connectedBlocks[0] == tunnelCoord) {
                        tunnel = rt;
                        break;
                    }
                }
                GameController.EnterTunnel(tunnel, tunnelData, true);
            }
            return;
        }

        moveCooldown = moveCooldownMax;
        (int x, int y) nextCoord = (headData.blockData.coordinates.x + amount.x, headData.blockData.coordinates.y + amount.y);
        Data eatData = null;
        History history = null;
        if (!tunnelMove && !GameController.enterTunnel) {
            // Enter tunnel
            Data data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.TUNNEL);
            if (data != null && data.blockData.IsPrimary()) {
                Data panelData = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.MISC);
                TunnelPanel tp = panelData.blockObject.GetComponent<TunnelPanel>();
                if (!tp.open) {
                    GameController.MoveBlocked(MoveType.PLAYER);
                    return;
                }

                // Check if moving into hole of tunnel
                (int x, int y) moveAmount = GameController.backFacingDirection[data.blockData.facing];
                if (amount != moveAmount)
                    return;

                RegionData rd = GameController.currentMapSystem.GetRegion(playerData.currentRoom);
                RegionData.RegionTunnel tunnel = null;
                foreach (var rt in rd.blockData.regionTunnels) {
                    if (rt.connectedBlocks[0] == nextCoord) {
                        tunnel = rt;
                        break;
                    }
                }
                GameController.EnterTunnel(tunnel, data, false);
                return;
            }

            // Check for obstacles or collectables
            if (data != null) {
                GameController.MoveBlocked(MoveType.PLAYER);
                return;
            }
            data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.PLAYER);
            if (data != null) {
                GameController.MoveBlocked(MoveType.PLAYER);
                return;
            }
            data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.COLLECT);
            if (data != null) {
                eatData = data;
                eat = true;
            }
            else {
                (int x, int y) headCoord = headData.blockData.coordinates;
                if (playerData.fragments > 0 && (nextCoord.x - headCoord.x, nextCoord.y - headCoord.y) == GameController.facingDirection[headData.blockData.facing]) {
                    data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.MISC);
                    if (data != null && data.blockData.blockName == "GateSlot") {
                        GameController.PlaceFragment(data);
                        return;
                    }
                }
            }
            data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.BLOCK);
            GameController.jumpingFallData = true;
            if (data != null) {
                if (!GameController.MoveBlock(data, amount, MoveType.PLAYER)) {
                    GameController.jumpingFallData = false;
                    return;
                }
                history = GameController.historyList[GameController.historyList.Count - 1];
            }
        }
        SetHeadAnimation("StopNod", 0);

        // Add history changes for worm blocks
        if (history == null) {
            history = new History();
            GameController.historyList.Add(history);
        }

        (int x, int y)[] connectedBlocks = new (int x, int y)[headData.blockData.connectedBlocks.Length];
        headData.blockData.connectedBlocks.CopyTo(connectedBlocks, 0);
        for (int i = 0; i < wormData.Count; i++) {
            HistoryChange.Move m = new HistoryChange.Move(wormData[i], amount, connectedBlocks);
            history.changes.Add(m);
            if (i > 0) m.location = wormData[i - 1].blockData.coordinates;
            else       m.headFlipped = playerData.flippedBlocks[0];
        }

        if (eat)
            GameController.Eat(eatData);

        // Update which way worm blocks are facing and change straight/corner pieces
        bool[] moveTypes = new bool[wormData.Count];
        int oldFacing = wormData[0].blockData.facing;
        wormData[0].blockData.facing = amount.x == 0 ? (amount.y > 0 ? 1 : 3) : (amount.x > 0 ? 0 : 2);

        for (int i = wormData.Count - 1; i > 0; i--) {
            BlockData bd = wormData[i].blockData;
            BlockData nextData = wormData[i - 1].blockData;

            if (bd.facing == nextData.facing)
                bd.state = 1;
            else {
                moveTypes[i] = true;
                bd.state = 2;
                int upper = bd.facing + 1 > 3 ? 0 : bd.facing + 1;
                int lower = bd.facing - 1 < 0 ? 3 : bd.facing - 1;
                worm[DataToTotalIndex(i)].spriteRenderer.flipY = nextData.facing == lower;
                playerData.flippedBlocks[i] = worm[DataToTotalIndex(i)].spriteRenderer.flipY;
            }

            bd.facing = nextData.facing;
        }
        moveTypes[0] = false;

        // Move worm blocks and check if worm should roll upright / erase straight vertical jump from history
        int rollCheck = 0;
        bool doRollCheck = true;
        bool jumpCheck = true;
        for (int i = 0; i < wormData.Count; i++) {
            var currCoord = wormData[i].blockData.coordinates;
            GameController.currentCoordinateSystem.SetData(nextCoord, wormData[i], false);
            nextCoord = currCoord;

            if (!tunnelMove) {
                if (doRollCheck && currCoord.y == headData.blockData.coordinates.y)
                    rollCheck++;
                else
                    doRollCheck = false;
                //GRAV CHECK
                if (jumpCheck && currCoord.x != headData.blockData.coordinates.x)
                    jumpCheck = false;
            }
        }

        bool rolled = false;
        bool fall = FallCheck();
        if (!tunnelMove) {
            if (!flyMode && fall) {
                if (shaking) {
                    shaking = false;
                    SetHeadAnimation("Shake", 1);
                    GameController.ShowLengthMeter(false);
                }
                SetHeadAnimation("Fall", 0);
            }
            else {
                //GRAV CHECK
                if (rollCheck >= playerData.length / 2) {
                    if ((wormData[1].blockData.coordinates.x < headData.blockData.coordinates.x &&  playerData.flippedBlocks[0])
                     || (wormData[1].blockData.coordinates.x > headData.blockData.coordinates.x && !playerData.flippedBlocks[0])) {
                        rolled = true;
                    }
                }
            }
        }
        if (tunnelMove || flyMode || !jumpCheck)
            GameController.jumpingFallData = false;

        UpdateConnections();

        // Check if worm is out of tunnel to fall
        if (!tunnelMove) {
            bool found = false;
            for (int i = 0; i < wormData.Count; i++) {
                Data tunnelData = GameController.currentCoordinateSystem.GetData(wormData[i].blockData.coordinates, Layer.TUNNEL);
                if (tunnelData != null) {
                    found = true;
                    break;
                }
                else
                    worm[DataToTotalIndex(i)].light.enabled = true;
            }
            if (!found) {
                flyMode = false;
                if (fall)
                    SetHeadAnimation("Fall", 0);
            }
            GameController.PlayRandomSound(AudioController.playerMove);
        }

        GameController.CheckTemporaryOpenDoors();
        PlayGroundMovingParticles();
        movePlayerCoroutine = MoveWormPieces(moveTypes);
        StartCoroutine(movePlayerCoroutine);

        if (rolled)
            Roll();
    }

    // Grow/Shrink player
    public void GrowPlayer((int x, int y) amount, bool undoChange) {
        if (playerMoving)
            return;

        movePlayerCoroutine = DoGrowPlayer(amount, undoChange);
        StartCoroutine(movePlayerCoroutine);
    }
    public IEnumerator DoGrowPlayer((int x, int y) amount, bool undoChange) {
        playerMoving = true;
        moveCooldown = moveCooldownMax;
        SetHeadAnimation("StopNod", 0);

        // Shrink if going backwards
        if (playerData.length > 3 && amount == GameController.backFacingDirection[headData.blockData.facing]) {
            if (GameController.currentCoordinateSystem.GetData(wormData[1].blockData.coordinates, Layer.TUNNEL) != null) {
                GameController.PlayRandomSound(AudioController.playerBlocked);
                playerMoving = false;
                yield break;
            }

            if (!undoChange) {
                History history = new History();
                GameController.historyList.Add(history);
                history.changes.Add(new HistoryChange.Grow(amount));
            }

            GameController.PlaySound(AudioController.playerShrink);
            playerData.length--;
            GameController.IncrementLengthMeter(-1);
            headData.blockData.facing = wormData[2].blockData.facing;

            // Move head backward
            if (!undoChange) {
                int moveIndex = -1;
                float distance = 0;
                Vector2 position = headData.blockObject.transform.position;

                while (moveIndex < 6) {
                    distance = Mathf.MoveTowards(distance, 7, GameController.BLOCK_MOVE_SPEED * Time.deltaTime);
                    if (distance > moveIndex + 1) {
                        moveIndex = Mathf.Min((int)distance, 6);
                        headData.blockObject.transform.position = position + new Vector2(amount.x * moveIndex, amount.y * moveIndex);
                        worm[1].spriteRenderer.sprite = growSprites[6 - moveIndex];
                        yield return null;
                    }
                }
            }

            // Remove second worm block
            worm[1].spriteRenderer.sprite = growSprites[3];
            wormData[playerData.length - 1].blockObject.transform.SetParent(wormHolder.transform);
            GameController.currentCoordinateSystem.RemoveData(wormData[1]);
            wormData.RemoveAt(1);
            playerData.flippedBlocks.RemoveAt(1);
            GameController.currentCoordinateSystem.MoveData(amount, headData, false);
            headData.ApplyData();

            UpdateBodyOrder();
            UpdateBodyFlips();
            UpdateGradients(undoChange, false);
            UpdateConnections();

            if (!undoChange) {
                // Do fall check for shrinking off ledge
                if (!flyMode && FallCheck()) {
                    if (shaking) {
                        shaking = false;
                        SetHeadAnimation("Shake", 1);
                        GameController.ShowLengthMeter(false);
                    }
                    SetHeadAnimation("Fall", 0);
                }
                GameController.ApplyGravity();
            }
        }
        else {
            // Grow in direction
            if (playerData.length < playerData.currMaxLength && amount != GameController.backFacingDirection[headData.blockData.facing]) {
                // Check for obstacles/collectables as if moving
                Data eatData = null;
                if (!undoChange) {
                    History history = null;
                    var nextCoord = (headData.blockData.coordinates.x + amount.x, headData.blockData.coordinates.y + amount.y);

                    Data data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.TUNNEL);
                    if (data != null) {
                        GameController.MoveBlocked(MoveType.PLAYER);
                        playerMoving = false;
                        yield break;
                    }
                    data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.PLAYER);
                    if (data != null) {
                        GameController.MoveBlocked(MoveType.PLAYER);
                        playerMoving = false;
                        yield break;
                    }
                    data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.COLLECT);
                    if (data != null) {
                        eatData = data;
                        eat = true;
                    }
                    data = GameController.currentCoordinateSystem.GetData(nextCoord, Layer.BLOCK);
                    if (data != null) {
                        if (!GameController.MoveBlock(data, amount, MoveType.PLAYER)) {
                            playerMoving = false;
                            yield break;
                        }
                        history = GameController.historyList[GameController.historyList.Count - 1];
                    }

                    if (history == null) {
                        history = new History();
                        GameController.historyList.Add(history);
                    }
                    history.changes.Add(new HistoryChange.Grow(amount));

                    if (eat)
                        GameController.Eat(eatData);
                }

                // Add new worm block
                SetHeadAnimation("Shake", 1);
                GameController.PlaySound(AudioController.playerGrow);
                playerData.length++;
                GameController.IncrementLengthMeter(1);
                headData.blockData.facing = amount.x == 0 ? (amount.y > 0 ? 1 : 3) : (amount.x > 0 ? 0 : 2);
                headData.blockObject.transform.rotation = Quaternion.Euler(Vector3.forward * headData.blockData.facing * 90);
                GameController.currentCoordinateSystem.MoveData(amount, headData, false);
                Data newWorm = new Data(new BlockData("Player", headData.blockData.facing, (headData.blockData.coordinates.x - amount.x, headData.blockData.coordinates.y - amount.y)), null);

                wormData.Insert(1, newWorm);
                playerData.flippedBlocks.Insert(1, false);
                GameController.currentCoordinateSystem.AddData(newWorm);

                UpdateBodyOrder();
                wormData[1].blockData.state = wormData[1].blockData.facing != wormData[2].blockData.facing ? 2 : 1;
                newWorm.ApplyData();
                UpdateBodyFlips();
                UpdateGradients(undoChange, true);
                UpdateConnections();

                // Move head forward
                if (!undoChange) {
                    int moveIndex = 0;
                    float distance = 1;
                    Vector2 position = headData.blockObject.transform.position;

                    while (moveIndex < 6) {
                        distance = Mathf.MoveTowards(distance, 7, GameController.BLOCK_MOVE_SPEED * Time.deltaTime);
                        if (distance > moveIndex + 1) {
                            moveIndex = Mathf.Min((int)distance, 6);
                            headData.blockObject.transform.position = position + new Vector2(amount.x * moveIndex, amount.y * moveIndex);
                            yield return null;
                        }
                        if (moveIndex < 3)
                            worm[1].spriteRenderer.sprite = growSprites[moveIndex + 1];
                    }
                }
                worm[1].spriteRenderer.sprite = growSprites[3];
                headData.ApplyData();
                GameController.ApplyGravity();
            }
            else
                GameController.MoveBlocked(MoveType.PLAYER);
        }
        playerMoving = false;
    }

    // Build worm from player icon in level editor
    public void BuildWorm() {
        playerIcon = GameObject.Find("Player");
        if (playerIcon == null)
            return;

        // Load player
        if (!GameController.inEditor) {
            if (!GameController.devMode)
                playerData = startSave ? GameController.LoadPlayer(GameController.currentSave) : null;
            else {
                if (playerData == null) {
                    playerData = new PlayerData();
                    playerData.flippedBlocks = new List<bool>();
                    for (int i = 0; i < 3; i++)
                        playerData.flippedBlocks.Add(false);
                }
                else {
                    if (!GameController.enterTunnel) {
                        playerData.blockData = null;
                        playerData.length = 3;
                    }
                }
                playerData.save = "PlayerDev";
            }
        }
        else
            playerData = GameController.LoadPlayer(GameController.currentSave);

        // Create new player
        if (playerData == null) {
            playerData = new PlayerData();
            playerData.flippedBlocks = new List<bool>(new bool[3]);
            playerData.save = "Player" + GameController.currentSave;
        }

        GameController.currentRoom = playerData.currentRoom = GameObject.FindGameObjectWithTag("Level").name;

        wormHolder = new GameObject("WormBlocks");
        wormHolder.SetActive(false);
        undoHolder = new GameObject("WormUndoHolder");
        undoHolder.SetActive(false);
        undoOutlineHolder = new GameObject("PlayerUndoHolder");
        worm = new Worm[playerData.maxLength];
        gradientColors = new Color32[playerData.maxLength];
        GameObject wormResource = Resources.Load("Blocks/Player/Worm") as GameObject;
        GameObject groundParticlesHolder = new GameObject("GroundParticlesHolder");

        // Create all worm blocks to be used during runtime
        for (int i = 0; i < playerData.maxLength; i++) {
            GameObject w = Instantiate(i == 0 ? wormResource : wormPiece, wormHolder.transform);
            GameObject g = Instantiate(GameController.GetGroundMovingParticles(), groundParticlesHolder.transform);
            GameObject u = Instantiate(Resources.Load("Blocks/UndoOutline") as GameObject, undoHolder.transform);
            w.name = "Worm";
            u.name = "UndoOutline" + i;

            worm[i] = new Worm(w, w.GetComponent<Light2D>(), w.GetComponent<SpriteRenderer>(), u, u.GetComponent<SpriteRenderer>(), g, g.GetComponent<ParticleSystem>());
            worm[i].spriteRenderer.sortingOrder = -i;
            worm[i].spriteRenderer.sprite = growSprites[3];
            worm[i].undoSpriteRenderer.color = GameController.timeColor;
            if (i == playerData.maxLength - 1) {
                worm[i].undoSpriteRenderer.flipX = true;
                worm[i].undoSpriteRenderer.sprite = undoSprites[3];
            }

            if (i > 0)
                worm[i].spriteRenderer.color = gradientColors[i] = Color32.Lerp(wormColor, new Color32(0, 0, 0, 255), (0.5f / playerData.maxLength) * i);
            else {
                gradientColors[i] = wormColor;
                worm[i].undoSpriteRenderer.sprite = undoSprites[3];
                headAnimator = w.GetComponent<Animator>();
                eyeAnimator = eye.GetComponent<Animator>();
                eyeSprite   = eye.GetComponent<SpriteRenderer>();
                eyeLight    = eye.GetComponent<Light2D>();
                eyeLight.enabled = false;
                eyeSprite.color = eyeDefaultColor;
                eye.transform.SetParent(w.transform);
                eye.transform.localPosition = Vector3.zero;
                eye.SetActive(true);
            }
        }

        Vector2 playerPos = playerIcon.transform.position;
        playerIcon.transform.position = Vector3.zero;
        playerIcon.GetComponent<SpriteRenderer>().enabled = false;

        // If exiting tunnel on level load, get data
        Data tunnelData = null;
        if (GameController.enterTunnel) {
            flyMode = true;

            RegionData rd = GameController.currentMapSystem.GetRegion(playerData.currentRoom);
            if (rd != null) {
                foreach (var rt in rd.blockData.regionTunnels) {
                    if (rt.tunnelID == GameController.enterTunnelID) {
                        tunnelData = GameController.currentCoordinateSystem.GetData(rt.connectedBlocks[0], Layer.TUNNEL);
                        break;
                    }
                }
            }
        }

        (int x, int y) tunnelCoord = (0, 0);
        (int x, int y) amount = (0, 0);
        int facing = tunnelData == null ? 0 : tunnelData.blockData.facing;

        if (playerData.blockData != null) {
            wormData = new List<Data>(new Data[playerData.blockData.Length]);

            // Build worm based on saved coordinates
            for (int i = 0; i < playerData.blockData.Length; i++) {
                GameObject w = null;
                w = worm[DataToTotalIndex(i)].wormObject;
                w.transform.SetParent(playerIcon.transform);
                w.name = "Worm";
                Data d = new Data(playerData.blockData[i], w);

                // If exiting tunnel, build worm in tunnel
                if (tunnelData != null) {
                    if (i < tunnelData.blockData.connectedBlocks.Length) {
                        tunnelCoord = tunnelData.blockData.connectedBlocks[i];
                        if (i > 0) {
                            amount = (tunnelData.blockData.connectedBlocks[i - 1].x - tunnelCoord.x,
                                      tunnelData.blockData.connectedBlocks[i - 1].y - tunnelCoord.y);
                            if (playerData.blockData[i].state != 3)
                                playerData.blockData[i].state = 1;
                            facing = amount.x == 0 ? (amount.y > 0 ? 1 : 3) : (amount.x > 0 ? 0 : 2);
                        }
                    }
                    else {
                        tunnelCoord.x -= amount.x;
                        tunnelCoord.y -= amount.y;
                    }
                    playerData.blockData[i].facing      = facing;
                    playerData.blockData[i].coordinates = tunnelCoord;
                }
                else
                    worm[DataToTotalIndex(i)].light.enabled = true;

                d.ApplyData();
                wormData[i] = d;
                GameController.currentCoordinateSystem.AddData(d);
            }
        }
        else {
            // Build new worm
            wormData = new List<Data>(new Data[3]);
            playerData.blockData = new BlockData[3];
            for (int i = 0; i < 3; i++) {
                var coord = i == 0 ? (GameController.GetGridCoordinate(playerPos.x), GameController.GetGridCoordinate(playerPos.y))
                                   : (wormData[0].blockData.coordinates.x - i, wormData[0].blockData.coordinates.y);
                GameObject w = null;
                w = worm[DataToTotalIndex(i)].wormObject;
                w.transform.SetParent(playerIcon.transform);
                w.name = "Worm";
                Data d = new Data(new BlockData("Player", 0, coord), w);
                d.blockData.state = i < 2 ? i : 3;
                playerData.blockData[i] = d.blockData;

                // If exiting tunnel, build worm in tunnel
                if (tunnelData != null) {
                    if (i < tunnelData.blockData.connectedBlocks.Length) {
                        tunnelCoord = tunnelData.blockData.connectedBlocks[i];
                        if (i > 0 && playerData.blockData[i].state != 3) {
                            amount = (tunnelData.blockData.connectedBlocks[i - 1].x - tunnelCoord.x,
                                      tunnelData.blockData.connectedBlocks[i - 1].y - tunnelCoord.y);
                            playerData.blockData[i].state = 1;
                            facing = amount.x == 0 ? (amount.y > 0 ? 1 : 3) : (amount.x > 0 ? 0 : 2);
                        }
                    }
                    else {
                        tunnelCoord.x -= amount.x;
                        tunnelCoord.y -= amount.y;
                    }
                    playerData.blockData[i].facing      = facing;
                    playerData.blockData[i].coordinates = tunnelCoord;
                }
                else
                    worm[DataToTotalIndex(i)].light.enabled = true;

                d.ApplyData();
                wormData[i] = d;
                GameController.currentCoordinateSystem.AddData(d);
            }
        }

        UpdateBodyFlips();
        UpdateGradients(true, false);
        UpdateConnections();

        // Check if player is in tunnel to prevent falling
        foreach (Data d in wormData) {
            Data td = GameController.currentCoordinateSystem.GetData(d.blockData.coordinates, Layer.TUNNEL);
            if (td != null)
                flyMode = true;
        }
        GameController.fallData[0] = headData = wormData[0];

        // Create all grid pieces
        gridObjects = new GameObject[gridSize * 2, gridSize * 2];
        grid = new GameObject("Grid");
        grid.transform.position = new Vector2(GameController.BLOCK_SIZE * gridSize, GameController.BLOCK_SIZE * gridSize);
        grid.transform.parent = headData.blockObject.transform;
        gridObjects[gridSize, gridSize] = grid;
        for (int x = 0; x < gridSize * 2; x++) {
            for (int y = 0; y < gridSize * 2; y++) {
                GameObject gridPiece = Instantiate(gridObject);
                gridPiece.transform.position = new Vector2(GameController.BLOCK_SIZE * x, GameController.BLOCK_SIZE * y);
                gridPiece.transform.parent = grid.transform;
                gridObjects[x, y] = gridPiece;
            }
        }
        grid.transform.localPosition = Vector3.zero;

        // Exit tunnel or set camera to player location
        if (GameController.enterTunnel && tunnelData != null)
            GameController.ExitTunnel(tunnelData);
        else {
            GameController.enterTunnel = false;
            if (GameController.currentCoordinateSystem.GetData(headData.blockData.coordinates, Layer.TUNNEL) != null)
                MovePlayer(GameController.facingDirection[headData.blockData.facing], true);
            GameController.SetNearestScreen(headData.blockObject.transform.position);
        }

        headTrigger.transform.position = headData.blockObject.transform.position;
        headTrigger.SetActive(true);
    }

    // Switch gravity and apply it
    void SwitchGravity((int x, int y) direction) {
        GameController.gravityDirection = direction;
        if (FallCheck())
            SetHeadAnimation("Fall", 0);
        GameController.ApplyGravity();
    }

    // Flash eyes for undo or reset
    public void TimeEyes() {
        Look(false, 0);
        StartCoroutine(DoTimeEyes());
    }
    IEnumerator DoTimeEyes() {
        eyeLight.enabled = true;
        eyeLight.color = eyeSprite.color = GameController.timeColor;

        yield return new WaitForSeconds(0.1f);

        eyeSprite.color = eyeDefaultColor;
        eyeLight.enabled = false;
    }

    // Reset eye position and color
    void ResetEyes(bool enabled) {
        Look(false, 0);

        if (enabled) {
            eyeLight.enabled = true;
            eyeLight.color = eyeSprite.color = GameController.timeColor;
        }
        else {
            eyeSprite.color = eyeDefaultColor;
            eyeLight.enabled = false;
        }
    }

    // Flash through unlocked color abilities
    public void ColorEyes() {
        StartCoroutine(DoColorEyes());
    }
    IEnumerator DoColorEyes() {
        canShoot = false;
        GameController.colorCycle = GameController.colorCycle + 1 > 2 ? 0 : GameController.colorCycle + 1;
        while (!playerData.colors[GameController.colorCycle])
            GameController.colorCycle = GameController.colorCycle + 1 > 2 ? 0 : GameController.colorCycle + 1;

        int cycle = GameController.colorCycle;
        eyeLight.color = eyeSprite.color = GameController.crystalColors[cycle];
        eyeLight.enabled = true;
        while (GameController.shooting) {
            if (playerData.colors[cycle]) {
                eyeLight.color = eyeSprite.color = GameController.crystalColors[cycle];
                yield return new WaitForSeconds(0.1f);
            }
            cycle = cycle + 1 > 2 ? 0 : cycle + 1;
        }
        eyeSprite.color = eyeDefaultColor;
        eyeLight.enabled = false;

        yield return new WaitForSeconds(0.15f);

        canShoot = true;
    }

    // Roll head to be right side up
    void Roll() {
        Look(false, 0);
        LookFacing(false);
        playerData.flippedBlocks[0] = !playerData.flippedBlocks[0];
        worm[0].spriteRenderer.flipY = playerData.flippedBlocks[0];
        eye.transform.localScale = new Vector3(1, playerData.flippedBlocks[0] ? -1 : 1, 1);
        SetHeadAnimation("Roll", 2);
        Invoke("LookCheck", 0.2f);
    }

    // Check if player fell far enough to have big eyes
    bool FallCheck() {
        foreach (Data d in wormData) {
            (int x, int y) checkCoord = d.blockData.coordinates;
            for (int i = 0; i < 5; i++) {
                checkCoord = (checkCoord.x + GameController.gravityDirection.x, checkCoord.y + GameController.gravityDirection.y);
                Data blockData = GameController.currentCoordinateSystem.GetData(checkCoord, Layer.BLOCK);
                Data tunnelData = GameController.currentCoordinateSystem.GetData(checkCoord, Layer.TUNNEL);
                if (blockData != null || tunnelData != null)
                    return false;
            }
        }
        return true;
    }

    // Check for adjacent collectables to look at
    public void LookCheck() {
        for (int i = 0; i < GameController.compassDirection.Length; i++) {
            (int x, int y) checkCoord = (headData.blockData.coordinates.x + GameController.compassDirection[i].x, headData.blockData.coordinates.y + GameController.compassDirection[i].y);
            if (GameController.currentCoordinateSystem.GetData(checkCoord, Layer.COLLECT) != null) {
                Look(true, i);
                print("look " + i);
                return;
            }
        }
    }

    // Look in direction
    void Look(bool activate, int direction) {
        if (activate) {
            SetHeadAnimation("Look", 0);
            eyeSprite.color = eyeDefaultColor;
            eye.transform.localPosition = Vector3.zero;
            eye.transform.position += new Vector3(GameController.compassDirection[direction].x, GameController.compassDirection[direction].y, 0);
            if (resetLookFacingCoroutine != null)
                StopCoroutine(resetLookFacingCoroutine);
        }
        else {
            SetHeadAnimation("Look", 1);
            eye.transform.localPosition = Vector3.zero;
        }
    }

    // Look forward
    void LookFacing(bool activate) {
        if (resetLookFacingCoroutine != null)
            StopCoroutine(resetLookFacingCoroutine);

        if (activate) {
            eyeSprite.color = eyeDefaultColor;
            eye.transform.localPosition = Vector3.zero;
            eye.transform.localPosition += Vector3.right;

            resetLookFacingCoroutine = ResetLookFacing();
            StartCoroutine(resetLookFacingCoroutine);
        }
        else
            eye.transform.localPosition = Vector3.zero;
    }
    IEnumerator ResetLookFacing() {
        yield return new WaitForSeconds(0.22f);
        eye.transform.localPosition = Vector3.zero;
    }

    // Wake up and reset sleep timer
    public void WakeUp() {
        currentSleepTime = sleepTime;
        if (sleeping) {
            sleeping = false;
            SetHeadAnimation("Sleep", 1);
        }
    }

    // Expand/Contract grid
    IEnumerator GridScaling() {
        while (true) {
            int size = (int)currentGridSize;
            if (size == 0)
                gridObjects[gridSize, gridSize].SetActive(showGrid);
            else {
                for (int coord = -(size - 1); coord < size; coord++) {
                    gridObjects[gridSize + coord, gridSize + size].SetActive(showGrid);
                    gridObjects[gridSize + size, gridSize + coord].SetActive(showGrid);
                    gridObjects[gridSize - coord, gridSize - size].SetActive(showGrid);
                    gridObjects[gridSize - size, gridSize - coord].SetActive(showGrid);
                }
            }
            if (showGrid) {
                if (currentGridSize > size + 0.5f) {
                    gridObjects[gridSize + size, gridSize + size].SetActive(true);
                    gridObjects[gridSize + size, gridSize - size].SetActive(true);
                    gridObjects[gridSize - size, gridSize + size].SetActive(true);
                    gridObjects[gridSize - size, gridSize - size].SetActive(true);
                }
            }
            else {
                if (currentGridSize < size + 0.5f) {
                    gridObjects[gridSize + size, gridSize + size].SetActive(false);
                    gridObjects[gridSize + size, gridSize - size].SetActive(false);
                    gridObjects[gridSize - size, gridSize + size].SetActive(false);
                    gridObjects[gridSize - size, gridSize - size].SetActive(false);
                }
            }
            yield return null;
        }
    }

    // Show panel info
    IEnumerator ShowPanelInfo() {
        showingPanelInfo = true;

        foreach (GameObject go in GameController.panelInfos)
            go.SetActive(true);
        foreach (GameObject go in GameController.buttonInfos) {
            switch (go.name) {
                case "GateDoor(Clone)":
                    go.SetActive(true);
                    GameController.gateAnimators[1].SetBool("On", GameController.gateData.blockData.state == 0);
                    break;

                case "CollectFragment(Clone)":
                    (int x, int y) fragCoord = (GameController.GetGridCoordinate(go.transform.position.x), GameController.GetGridCoordinate(go.transform.position.y));
                    if (GameController.currentCoordinateSystem.GetData(fragCoord, Layer.COLLECT) != null)
                        go.SetActive(true);
                    break;

                default:
                    go.SetActive(true);
                    break;
            }
        }

        float scale = 1;
        while (scale < 2) {
            scale += Time.deltaTime * 10;
            foreach (GameObject go in GameController.panelInfos)
                go.transform.localScale = new Vector3(scale, scale, 0);
            yield return null;
        }
        scale = 2;
        foreach (GameObject go in GameController.panelInfos)
            go.transform.localScale = new Vector3(scale, scale, 0);


        yield return new WaitUntil(() => !showPanelInfo);


        while (scale > 1) {
            scale -= Time.deltaTime * 10;
            foreach (GameObject go in GameController.panelInfos)
                go.transform.localScale = new Vector3(scale, scale, 0);
            yield return null;
        }
        scale = 1;
        foreach (GameObject go in GameController.panelInfos) {
            go.transform.localScale = new Vector3(scale, scale, 0);
            go.SetActive(false);
        }
        foreach (GameObject go in GameController.buttonInfos)
            go.SetActive(false);

        showingPanelInfo = false;
    }

    // Wait for gate to finish before moving
    public void GateCooldown() {
        disableMovement = false;
    }

    // Set head animation
    public void SetHeadAnimation(string parameter, int state) {
        if (parameter != "Look" && headAnimator.GetBool("Look") && parameter != "Blink")
            Look(false, 0);

        foreach (var par in headAnimator.parameters) {
            if (par.GetType() == typeof(bool))
                headAnimator.SetBool(par.name, false);
        }
        foreach (var par in eyeAnimator.parameters) {
            if (par.GetType() == typeof(bool))
                eyeAnimator.SetBool(par.name, false);
        }

        int type = -1; // 0: Head | 1: Eye | 2: Both
        switch (parameter) {
            case "Eat":     type = 2; break;
            case "Sleep":   type = 2; break;
            case "Look":    type = 0; break;
            case "Fall":    type = 1; break;
            case "Shake":   type = 1; break;
            case "Think":   type = 1; break;
            case "Blink":   type = 1; break;
            case "Roll":    type = 1; break;
            case "Nod":     type = 2; break;
            case "StopNod": type = 2; break;
        }

        if (type == 0 || type == 2) {
            if (state < 2) headAnimator.SetBool(parameter, state == 0);
            else           headAnimator.SetTrigger(parameter);
        }
        if (type == 1 || type == 2) {
            if (state < 2) eyeAnimator.SetBool(parameter, state == 0);
            else           eyeAnimator.SetTrigger(parameter);
        }

    }

    // Set correct gradient level to match length
    public void UpdateGradients(bool instant, bool growing) {
        foreach (IEnumerator ie in gradientCoroutines)
            StopCoroutine(ie);
        gradientCoroutines.Clear();

        if (!instant) {
            Color[] startColors = new Color[wormData.Count];
            for (int i = 1; i < wormData.Count; i++)
                startColors[i] = gradientColors[i + (growing ? -1 : 1)];
            for (int i = 1; i < wormData.Count; i++) {
                IEnumerator ie = LerpGradient(worm[DataToTotalIndex(i)].spriteRenderer, startColors[i], gradientColors[i]);
                gradientCoroutines.Add(ie);
                StartCoroutine(ie);
            }
        }
        else {
            for (int i = 1; i < wormData.Count; i++)
                worm[DataToTotalIndex(i)].spriteRenderer.color = gradientColors[i];
        }
    }
    IEnumerator LerpGradient(SpriteRenderer sr, Color sc, Color32 ec) {
        sr.color = sc;
        Color color = new Color(ec.r / 255.0f, ec.g / 255.0f, ec.b / 255.0f, 1);
        float time = 0;
        while (time < 1) {
            time += Time.deltaTime;
            sr.color = Color.Lerp(sr.color, color, time);
            yield return null;
        }
        sr.color = color;
    }

    // Set body pieces to correct order to match length
    public void UpdateBodyOrder() {
        for (int i = 1; i < wormData.Count - 1; i++) {
            worm[i].wormObject.transform.SetParent(playerIcon.transform);
            worm[i].light.enabled = true;
            wormData[i].blockObject = worm[i].wormObject;
            wormData[i].ApplyData();
        }
    }

    // Update which way worm blocks are facing and change straight/corner pieces
    public void UpdateBodyFlips() {
        worm[0].spriteRenderer.flipY = playerData.flippedBlocks[0];
        eye.transform.localScale = new Vector3(1, playerData.flippedBlocks[0] ? -1 : 1, 1);
    }

    // Updates stored connected blocks to match coordinates
    void UpdateConnections() {
        (int x, int y)[] cBlocks = new (int x, int y)[wormData.Count];
        for (int i = 0; i < wormData.Count; i++)
            cBlocks[i] = wormData[i].blockData.coordinates;
        foreach (Data d in wormData)
            d.blockData.connectedBlocks = cBlocks;
    }

    // Play particles when moving across the ground
    void PlayGroundMovingParticles() {
        if (!GameController.enableParticles)
            return;
        //GRAV CHECK
        for (int i = 0; i < wormData.Count; i++) {
            if (wormData[i].blockData.facing % 2 == 0 && GameController.currentCoordinateSystem.GetData(wormData[i].blockData.coordinates, Layer.TUNNEL) == null) {
                (int x, int y) groundCoord = (wormData[i].blockData.coordinates.x, wormData[i].blockData.coordinates.y - 1);
                Data groundData = GameController.currentCoordinateSystem.GetData(groundCoord, Layer.BLOCK);
                Data tunnelData = GameController.currentCoordinateSystem.GetData(groundCoord, Layer.TUNNEL);
                if (tunnelData == null && groundData != null && groundData.blockData.blockName == "Ground") {
                    Worm w = worm[DataToTotalIndex(i)];
                    GameController.ApplyData(wormData[i].blockData, w.groundParticles, false, false);
                    w.groundParticles.transform.localRotation = Quaternion.Euler(new Vector3(-90, 0, wormData[i].blockData.facing == 0 ? 0 : 180));
                    w.groundParticlesSystem.Play();
                }
            }
        }
    }

    // Update worm undo outline to match body and show it
    public void CreateUndoOutline(Data[] wormUndoData) {
        GameController.ApplyData(wormUndoData[0].blockData, worm[0].undoObject, false, true);
        GameController.ApplyData(wormUndoData[wormUndoData.Length - 1].blockData, worm[worm.Length - 1].undoObject, false, true);
        worm[0].undoObject.transform.SetParent(undoOutlineHolder.transform);
        worm[worm.Length - 1].undoObject.transform.SetParent(undoOutlineHolder.transform);
        SpriteRenderer[] tempUndoSprites = new SpriteRenderer[wormUndoData.Length];
        tempUndoSprites[0] = worm[0].undoSpriteRenderer;
        tempUndoSprites[wormUndoData.Length - 1] = worm[worm.Length - 1].undoSpriteRenderer;

        for (int i = 1; i < wormUndoData.Length - 1; i++) {
            BlockData bd = wormUndoData[i].blockData;
            BlockData nextData = wormUndoData[i + 1].blockData;
            tempUndoSprites[i] = worm[i].undoSpriteRenderer;
            GameObject uo = worm[i].undoObject;
            tempUndoSprites[i].sprite = undoSprites[bd.state];

            if (bd.facing != nextData.facing) {
                int upper = bd.facing + 1 > 3 ? 0 : bd.facing + 1;
                int lower = bd.facing - 1 < 0 ? 3 : bd.facing - 1;
                tempUndoSprites[i].flipY = nextData.facing != lower;
            }
            GameController.ApplyData(bd, uo, false, true);
            uo.transform.SetParent(undoOutlineHolder.transform);
        }
        for (int i = wormUndoData.Length - 1; i < worm.Length - 1; i++)
            worm[i].undoObject.transform.SetParent(undoHolder.transform);

        GameController.FadeUndoOutline(undoOutlineHolder, tempUndoSprites);
    }

    // Convert worm data index from current worm data to total worm data
    public int DataToTotalIndex(int i) {
        return i < wormData.Count - 1 ? i : playerData.maxLength - 1;
    }

    // If player needs to move while currently moving, snap player to correct position
    void SnapPlayer() {
        StopCoroutine(movePlayerCoroutine);
        for (int i = 0; i < wormData.Count; i++) {
            wormData[i].ApplyData();
            if (i > 0)
                worm[DataToTotalIndex(i)].spriteRenderer.sprite = cornerSprites[0];
        }
        GameController.ApplyGravity();
        playerMoving = false;
    }

    // Move worm pieces to follow each other
    IEnumerator MoveWormPieces(bool[] moveTypes) {
        playerMoving = true;
        LookFacing(true);
        wormData[0].blockObject.transform.rotation = Quaternion.Euler(Vector3.forward * wormData[0].blockData.facing * 90);

        int moveIndex = -1;
        float distance = 0;
        Vector2[] positions = new Vector2[wormData.Count];
        for (int i = 0; i < wormData.Count; i++)
            positions[i] = wormData[i].blockObject.transform.position;

        while (moveIndex < 6) {
            distance = Mathf.MoveTowards(distance, 7, GameController.BLOCK_MOVE_SPEED * Time.deltaTime);
            if (distance > moveIndex + 1) {
                moveIndex = Mathf.Min((int)distance, 6);
                for (int move = 0; move < moveTypes.Length; move++) {
                    (int x, int y) direction = GameController.facingDirection[wormData[move].blockData.facing];
                    if (moveTypes[move])
                        worm[DataToTotalIndex(move)].spriteRenderer.sprite = move == 1 ? secondCornerSprites[moveIndex] : cornerSprites[moveIndex + 1];
                    else {
                        int adjustedIndex = move == 0 ? moveIndex + 1 : moveIndex;
                        wormData[move].blockObject.transform.position = positions[move] + new Vector2(direction.x * adjustedIndex, direction.y * adjustedIndex);
                    }
                }
                yield return null;
            }
        }

        for (int i = 0; i < wormData.Count; i++) {
            wormData[i].ApplyData();
            if (i > 0)
                worm[DataToTotalIndex(i)].spriteRenderer.sprite = growSprites[i == 1 ? 2 : 3];
        }

        // Enable flying
        if (GameController.devControls && !Input.GetKey(KeyCode.RightControl))
            GameController.ApplyGravity();

        playerMoving = false;
    }

    // Blink player position on map
    IEnumerator MapWormBlink() {
        RegionData rd = GameController.currentMapSystem.GetRegion(playerData.currentRoom);
        bool showBlocks = false;
        while (showingMap) {
            showBlocks = !showBlocks;
            ShowMapWorm(showBlocks, rd);
            yield return new WaitForSeconds(0.5f);
        }
    }
    void ShowMapWorm(bool activate, RegionData regionData) {
        if (activate) {
            foreach (Data d in wormData) {
                Data cd = GameController.currentCoordinateSystem.GetData(d.blockData.coordinates, Layer.COLLECT);
                Data td = GameController.currentCoordinateSystem.GetData(d.blockData.coordinates, Layer.TUNNEL);
                if (cd == null && td == null) {
                    GameController.SetPixel(regionData.regionTexture, "Player", d.blockData.coordinates);
                    clearMapWormCoords.Add(d.blockData.coordinates);
                }
            }
        }
        else {
            foreach (var c in clearMapWormCoords)
                GameController.ResetPixel(GameController.currentCoordinateSystem, Layer.PLAYER, c, regionData.regionTexture);
            clearMapWormCoords = new List<(int x, int y)>();
        }
        regionData.regionTexture.Apply();
    }

    // Set up map traversal route from start room
    IEnumerator CalculateMap() {
        if (ending)
            yield break;

        mapElements = new List<MapElement>();
        calculatedRegions = new List<RegionData>();
        StartCoroutine(CalculateRegion(0, GameController.currentMapSystem.GetRegion(playerData.currentRoom), null));

        // Wait for map do be done calculating
        yield return new WaitUntil(() => activeMapCalculations == 0);

        // Reorder map elements from indeces and pack into groups
        float mapScale = 0;
        float mapScaleIncrement = 1.0f / mapTraversalIndex;
        mapTraversalList = new MapElement[mapTraversalIndex];
        foreach (MapElement me in mapElements) {
            if (mapTraversalList[me.index] == null) {
                mapTraversalList[me.index] = new MapElement(me.index);
                mapTraversalList[me.index].mapScale = Mathf.Lerp(MAP_START_ZOOM.x, 1, mapScale);
                mapScale += mapScaleIncrement;
            }

            if (mapTraversalList[me.index].regionElements.Count == 0)
                mapTraversalList[me.index].regionElements.Add(me.regionElements[0]);
            else {
                MapElement.RegionElement regionElement = null;
                foreach (MapElement.RegionElement re in mapTraversalList[me.index].regionElements) {
                    if (re.texture == me.regionElements[0].texture) {
                        regionElement = re;
                        break;
                    }
                }
                if (regionElement == null)
                    mapTraversalList[me.index].regionElements.Add(me.regionElements[0]);
                else {
                    foreach (var rc in me.regionElements[0].regionCoordinates)
                        regionElement.regionCoordinates.Add(rc);
                }
            }
        }

        for (int i = 0; i < mapBorders.Length; i++)
            mapBorders[i] = -mapBorders[i];

        mapTraversalIndex = 0;
        mapElements = null;
        calculatedRegions = null;
        doneCalculatingMap = true;
    }

    // Calculate order for map pieces
    IEnumerator CalculateRegion(int index, RegionData regionData, (int x, int y)[] backTunnel) {
        if (regionData == null || calculatedRegions.Contains(regionData))
            yield break;

        activeMapCalculations++;
        calculatedRegions.Add(regionData);
        yield return null;

        // If branching from another room start with tunnel
        if (backTunnel != null) {
            for (int i = backTunnel.Length - 1; i >= 0; i--) {
                MapElement tunnelElement = new MapElement(index++);
                MapElement.RegionElement tre = new MapElement.RegionElement(regionData.blockData.level, regionData.regionTexture);
                tre.regionCoordinates.Add(new RegionData.RegionBlockData.RegionCoordinate("Tunnel", backTunnel[i]));
                tunnelElement.regionElements.Add(tre);
                mapElements.Add(tunnelElement);
            }
        }

        // Order non-tunnel elements
        int[] coordBorders = new int[4];
        List<RegionData.RegionBlockData.RegionCoordinate>[] regionCoordinateLayers = new List<RegionData.RegionBlockData.RegionCoordinate>[2];
        foreach (var rc in regionData.blockData.regionCoordinates) {
            if (backTunnel == null || (backTunnel != null && rc.coordinates != backTunnel[0])) {
                if (regionCoordinateLayers[0] == null)
                    regionCoordinateLayers[0] = new List<RegionData.RegionBlockData.RegionCoordinate>();
                regionCoordinateLayers[0].Add(rc);

                // Get furthest pixels for border
                if      (rc.coordinates.y > coordBorders[0]) coordBorders[0] = rc.coordinates.y;
                else if (rc.coordinates.y < coordBorders[1]) coordBorders[1] = rc.coordinates.y;
                if      (rc.coordinates.x < coordBorders[2]) coordBorders[2] = rc.coordinates.x;
                else if (rc.coordinates.x > coordBorders[3]) coordBorders[3] = rc.coordinates.x;
            }
        }

        for (int i = 0; i < regionCoordinateLayers.Length; i++) {
            if (regionCoordinateLayers[i] == null)
                continue;

            MapElement roomElement = new MapElement(index++);
            MapElement.RegionElement rre = new MapElement.RegionElement(regionData.blockData.level, regionData.regionTexture);
            foreach (var rc in regionCoordinateLayers[i])
                rre.regionCoordinates.Add(rc);
            roomElement.regionElements.Add(rre);
            mapElements.Add(roomElement);
        }
        if (index > mapTraversalIndex)
            mapTraversalIndex = index;

        // Progressively order tunnel elements
        List<RegionData.RegionTunnel> regionTunnels = regionData.blockData.regionTunnels;
        if (regionTunnels != null) {
            for (int i = 0; i < regionTunnels.Count; i++) {
                if (regionTunnels[i].connectedBlocks == backTunnel)
                    continue;

                int tunnelIndex = index;
                for (int j = 0; j < regionTunnels[i].connectedBlocks.Length; j++) {
                    // If tunnel hasn't been visited then alternate pixels
                    if (!regionTunnels[i].visited && j % 2 == 1)
                        continue;

                    MapElement tunnelElement = new MapElement(tunnelIndex++);
                    MapElement.RegionElement tre = new MapElement.RegionElement(regionData.blockData.level, regionData.regionTexture);
                    (int x, int y) tunnelCoord = regionTunnels[i].connectedBlocks[j];
                    tre.regionCoordinates.Add(new RegionData.RegionBlockData.RegionCoordinate("Tunnel", tunnelCoord));
                    tunnelElement.regionElements.Add(tre);
                    mapElements.Add(tunnelElement);

                    // Get furthest pixels for border
                    if      (tunnelCoord.y > coordBorders[0]) coordBorders[0] = tunnelCoord.y;
                    else if (tunnelCoord.y < coordBorders[1]) coordBorders[1] = tunnelCoord.y;
                    if      (tunnelCoord.x < coordBorders[2]) coordBorders[2] = tunnelCoord.x;
                    else if (tunnelCoord.x > coordBorders[3]) coordBorders[3] = tunnelCoord.x;
                }

                if (tunnelIndex > mapTraversalIndex)
                    mapTraversalIndex = tunnelIndex;

                if (!regionTunnels[i].visited || regionTunnels[i].destinationID == null || regionTunnels[i].destinationID == "")
                    continue;

                // Get destination of tunnel and start calculating next room
                string[] destinationID = regionTunnels[i].destinationID.Split(':');
                string room = destinationID[0];
                int tunnel = int.Parse(destinationID[1]);
                RegionData nextData = GameController.currentMapSystem.GetRegion(room);

                (int x, int y)[] connectedBlocks = null;
                foreach (var rt in nextData.blockData.regionTunnels) {
                    if (rt.tunnelID == tunnel) {
                        connectedBlocks = rt.connectedBlocks;
                        break;
                    }
                }
                StartCoroutine(CalculateRegion(tunnelIndex, nextData, connectedBlocks));
            }
        }

        // Convert local border to global screen position border
        Vector3 localPos = regionData.regionObject.transform.localPosition;
        for (int i = 0; i < coordBorders.Length; i++)
            coordBorders[i] *= 5;
        if      (localPos.y + coordBorders[0] > mapBorders[0]) mapBorders[0] = localPos.y + coordBorders[0];
        else if (localPos.y + coordBorders[1] < mapBorders[1]) mapBorders[1] = localPos.y + coordBorders[1];
        if      (localPos.x + coordBorders[2] < mapBorders[2]) mapBorders[2] = localPos.x + coordBorders[2];
        else if (localPos.x + coordBorders[3] > mapBorders[3]) mapBorders[3] = localPos.x + coordBorders[3];

        activeMapCalculations--;
    }

    // Displays map
    void ShowMap(bool activate) {
        // Get current index based on time held down
        float time = Time.deltaTime * MAP_TRAVERSAL_SPEED;
        if (activate) {
            currentSleepTime = sleepTime;
            if (!showMap) {
                mapTraversalWait = (int)mapTraversalWait - 1;
                showMap = true;
            }
            if (mapTraversalWait < mapTraversalList.Length - 1)
                mapTraversalWait += mapReleased ? time * 3 : time;
            else
                mapTraversalWait = mapTraversalList.Length - 1;
        }
        else {
            if (showMap) {
                mapReleased = true;
                mapTraversalWait = (int)mapTraversalWait + 1;
                showMap = false;
            }
            if (mapTraversalWait > 0)
                mapTraversalWait -= time * 3;
            else
                mapTraversalWait = 0;
        }

        // Set starting scale and map position so worm head is in center of screen
        if (!showingMap && activate) {
            SetHeadAnimation("Think", 0);
            showingMap = true;
            RegionData rd = GameController.currentMapSystem.GetRegion(playerData.currentRoom);

            map.transform.SetParent(GameController.GetGameUI().transform, true);
            mapHolder.transform.position = rd.regionObject.transform.position;
            mapHolder.transform.position += new Vector3(headData.blockData.coordinates.x + 0.5f, headData.blockData.coordinates.y + 0.5f, 0) * 10;
            map.transform.SetParent(mapHolder.transform, true);
            mapHolder.transform.localScale = MAP_START_ZOOM;
            mapHolder.transform.localPosition = Vector3.zero;
            mapPosition = map.transform.localPosition;

            mapWormBlinkCoroutine = MapWormBlink();
            StartCoroutine(mapWormBlinkCoroutine);
        }

        // Catch up to correct index if skipping over others
        int index = (int)Mathf.Clamp(mapTraversalWait, 0, mapTraversalList.Length - 1);
        if (index != prevMapTraversalIndex) {
            int catchUp = Mathf.Abs(index - prevMapTraversalIndex);
            for (int i = 0; i < catchUp + 1; i++) {
                MapElement me = mapTraversalList[showMap ? prevMapTraversalIndex + i : prevMapTraversalIndex - i];
                mapHolder.transform.localScale = Vector3.one * me.mapScale;

                // Display/Hide each element
                foreach (MapElement.RegionElement re in me.regionElements) {
                    bool foundGate = false;
                    var gateRCS = new List<RegionData.RegionBlockData.RegionCoordinate>();
                    foreach (var rc in re.regionCoordinates) {
                        if (showMap) {
                            // Show uncollected collectables
                            if (rc.type[0] == 'C' || rc.type == "Gate") {
                                bool found = false;
                                foreach (var al in playerData.abilityLocations) {
                                    if (rc.coordinates == al.coordinates && al.room == re.room) {
                                        found = true;
                                        break;
                                    }
                                }
                                if (rc.type[0] == 'C') {
                                    if (found)
                                        GameController.ResetPixel(GameController.currentCoordinateSystem, Layer.COLLECT, rc.coordinates, re.texture);
                                    else
                                        GameController.SetPixel(re.texture, rc.type, rc.coordinates);
                                }
                                else {
                                    gateRCS.Add(rc);
                                    if (found)
                                        foundGate = true;
                                }
                            }
                            else
                                GameController.SetPixel(re.texture, rc.type, rc.coordinates);
                        }
                        else
                            GameController.SetPixel(re.texture, "Clear", rc.coordinates);
                    }
                    // Display gate as open/closed
                    if (gateRCS.Count > 0) {
                        string innerType = foundGate ? "Gate" : "CollectFragment";
                        string outerType = foundGate ? "CollectFragment" : "Gate";
                        for (int j = 0; j < gateRCS.Count; j++)
                            GameController.SetPixel(re.texture, j == 4 ? innerType : outerType, gateRCS[j].coordinates);
                    }
                    re.texture.Apply();
                }
            }
            prevMapTraversalIndex = index;
        }

        // Reset map when finished hiding
        if (showingMap && index <= 0 && !showMap) {
            StopCoroutine(mapWormBlinkCoroutine);
            selectedThink = 0;
            SetHeadAnimation("Think", 1);
            mapReleased = showingMap = false;
            mapHolder.transform.localScale = Vector3.one;
            map.transform.localPosition = mapHolder.transform.localPosition = Vector3.zero;
            RegionData rd = GameController.currentMapSystem.GetRegion(playerData.currentRoom);
            foreach (var c in clearMapWormCoords)
                GameController.SetPixel(rd.regionTexture, "Clear", c);
            rd.regionTexture.Apply();
            clearMapWormCoords = new List<(int x, int y)>();
        }
    }
}