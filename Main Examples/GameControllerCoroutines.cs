using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering.LWRP;

// Game functions that need coroutines
public class GameControllerCoroutines : MonoBehaviour {
    public class Bullet {
        public GameObject bulletObject;
        public SpriteRenderer spriteRenderer;
        public Light2D light;
        public GameObject particles;
        public ParticleSystem particleSystem;

        public Bullet(GameObject bulletObject, SpriteRenderer spriteRenderer, Light2D light, GameObject particles, ParticleSystem particleSystem) {
            this.bulletObject   = bulletObject;
            this.spriteRenderer = spriteRenderer;
            this.light          = light;
            this.particles      = particles;
            this.particleSystem = particleSystem;
        }
    }

    [Header("Game")]
    public GameObject[] blockList;
    public Material spriteDefault;
    public Material spriteLitDefault;
    GameObject[] screenObjects;
    public Screen[] screens;
    float SCREEN_MOVE_SPEED = 500;
    float SCREEN_ZOOM_SPEED = 10;
    IEnumerator[] screenCoroutines;
    int priorityCameraShake;
    IEnumerator cameraShakeCoroutine;


    [Header("UI")]
    public GameObject pauseUI;
    public GameObject gameUI;
    public GameObject[] abilityInfo;
    public bool[] abilityTriggers = new bool[4];
    public GameObject lengthMeter;
    public GameObject lengthSprite;
    public Sprite[] lengthSprites;
    public List<GameObject> lengthObjects;
    public GameObject mapHolder;
    public GameObject map;
    public GameObject mapArrows;
    public GameObject fragment;
    public GameObject fragmentCount;
    public Text fragmentCountText;

    [Header("Shooting")]
    public float bulletSpeed = 100;
    public int bulletAmount = 50;
    Bullet[] bullets;
    Queue<Bullet> bulletPool = new Queue<Bullet>();
    public int shootRollbacks = 0;
    List<(int x, int y)[]> activeRedCrystals = new List<(int x, int y)[]>();

    [Header("Particles")]
    public GameObject groundMovingParticles;
    public GameObject groundDustParticles;
    ParticleSystem groundDustParticleSystem;
    Queue<GameObject> airDustParticles = new Queue<GameObject>();
    Queue<ParticleSystem> airDustParticleSystems = new Queue<ParticleSystem>();
    bool[] usedAirDustCoords;
    List<(int x, int y)> airDustCoords;

    [Header("Other")]
    public PlayerController pc;
    Dictionary<GameObject, IEnumerator> undoCoroutines = new Dictionary<GameObject, IEnumerator>();

    private void Start() {
        if (GameController.editMode)
            return;

        pc = GameObject.Find("PlayerController").GetComponent<PlayerController>();
        fragmentCountText = fragmentCount.GetComponent<Text>();

        screenObjects = GameObject.FindGameObjectsWithTag("Screen");
        screens = new Screen[screenObjects.Length];
        for (int i = 0; i < screenObjects.Length; i++)
            screens[i] = screenObjects[i].GetComponent<Screen>();

        // Create projectile pool
        bullets = new Bullet[bulletAmount];
        GameObject bulletHolder = new GameObject();
        bulletHolder.name = "BulletHolder";
        bulletHolder.transform.parent = gameObject.transform;
        bulletHolder.transform.localPosition = Vector3.zero;
        for (int i = 0; i < bullets.Length; i++) {
            GameObject bullet = Instantiate(Resources.Load("Blocks/Player/Bullet") as GameObject, bulletHolder.transform);
            bullet.SetActive(false);
            GameObject particles = Instantiate(Resources.Load("BlockMisc/BulletParticles") as GameObject, bulletHolder.transform);
            bullets[i] = new Bullet(bullet, bullet.GetComponent<SpriteRenderer>(), bullet.GetComponent<Light2D>(), particles, particles.GetComponent<ParticleSystem>());
            bulletPool.Enqueue(bullets[i]);
        }

        if (!GameController.enableParticles)
            return;

        // Create air dust particles and particles that fall from ground
        groundDustParticleSystem = groundDustParticles.GetComponent<ParticleSystem>();
        RegionData rd = GameController.currentMapSystem.GetRegion(pc.playerData.currentRoom);
        if (rd == null)
            rd = GameController.currentMapSystem.GetRegion(GameObject.FindGameObjectWithTag("Level").name);
        if (rd != null) {
            GameObject adp = Resources.Load("BlockMisc/AirDustParticles") as GameObject;
            airDustCoords = rd.blockData.airDustCoordinates;
            usedAirDustCoords = new bool[airDustCoords.Count];
            GameObject airDustHolder = new GameObject("AirDustHolder");
            int particleAmount = airDustCoords.Count / 20;
            for (int i = 0; i < particleAmount; i++) {
                GameObject dustObject = Instantiate(adp, airDustHolder.transform);
                airDustParticles.Enqueue(dustObject);
                airDustParticleSystems.Enqueue(dustObject.GetComponent<ParticleSystem>());
                Invoke("CreateAirDustParticles", i * 0.1f);
            }
            StartCoroutine(CreateGroundDustParticles(rd.blockData.groundDustCoordinates));
        }
    }

    public GameObject GetMap() {
        return map;
    }
    public GameObject GetMapHolder() {
        return mapHolder;
    }
    public GameObject GetGameUI() {
        return gameUI;
    }

    // Initialize length meter based on player's max length
    public void InitLengthMeter() {
        lengthObjects = new List<GameObject>();
        if (pc == null)
            pc = GameObject.Find("PlayerController").GetComponent<PlayerController>();
        for (int i = 0; i < pc.playerData.maxLength; i++) {
            GameObject go = Instantiate(lengthSprite, lengthMeter.transform);
            lengthObjects.Add(go);
            go.transform.localPosition = Vector2.right * (i * lengthSprite.GetComponent<RectTransform>().rect.width - i * lengthSprite.GetComponent<RectTransform>().rect.width / 7);
            if (i == 0)
                SetLengthAnimation(go, 3);
            else {
                if (i < pc.playerData.length - 1)
                    SetLengthAnimation(go, 2);
                else {
                    if (i == pc.playerData.length - 1) SetLengthAnimation(go, 1);
                    else                               SetLengthAnimation(go, 0);
                }
            }
            if (i > pc.playerData.currMaxLength - 1)
                go.SetActive(false);
        }
    }
    // Change length meter piece to Head/Body/Tail/Empty
    public void SetLengthAnimation(GameObject block, int type) {
        if (block != null)
            block.GetComponent<Image>().sprite = lengthSprites[type];
    }

    // Continually search for open spots to play dust particles that fall from under ground blocks
    IEnumerator CreateGroundDustParticles(List<(int x, int y)> groundDustCoordinates) {
        bool[] usedCoords = new bool[groundDustCoordinates.Count];
        int cycles = 0;
        while (true) {
            yield return new WaitForSeconds(Mathf.Clamp(Random.Range(6.0f, 12.0f), 1, 6));

            int randomCoord = Random.Range(0, usedCoords.Length);
            for (int i = 0; i < 20; i++) {
                if (!usedCoords[randomCoord])
                    break;

                randomCoord = Random.Range(0, usedCoords.Length);
            }
            usedCoords[randomCoord] = true;
            if (cycles++ > 3)
                usedCoords = new bool[groundDustCoordinates.Count];

            (int x, int y) dustCoord = groundDustCoordinates[randomCoord];
            Data pushData = GameController.currentCoordinateSystem.GetData(dustCoord, Layer.BLOCK);
            Data playerData = GameController.currentCoordinateSystem.GetData(dustCoord, Layer.PLAYER);
            if (playerData != null || (pushData != null && pushData.HasTag(Tag.PUSH)))
                continue;

            GameController.ApplyCoordinates(dustCoord, groundDustParticles);
            groundDustParticleSystem.Play();
        }
    }

    // Continually search for open spots to play dust particles that float in air
    void CreateAirDustParticles() {
        StartCoroutine(DoCreateAirDustParticles(true));
    }
    IEnumerator DoCreateAirDustParticles(bool init) {
        int randomCoord = Random.Range(0, usedAirDustCoords.Length);
        for (int i = 0; i < 30; i++) {
            if (!usedAirDustCoords[randomCoord])
                break;

            randomCoord = Random.Range(0, usedAirDustCoords.Length);
        }
        usedAirDustCoords[randomCoord] = true;

        yield return new WaitForSeconds(init ? Random.Range(0, 2.0f) : Random.Range(3.0f, 5.0f));

        if (airDustParticles.Count > 0) {
            GameObject go = airDustParticles.Dequeue();
            ParticleSystem ps = airDustParticleSystems.Dequeue();
            GameController.ApplyCoordinates(airDustCoords[randomCoord], go);
            ps.Play();

            yield return new WaitForSeconds(2);

            airDustParticles.Enqueue(go);
            airDustParticleSystems.Enqueue(ps);
        }
        usedAirDustCoords[randomCoord] = false;
        StartCoroutine(DoCreateAirDustParticles(false));
    }

    // Interpolate camera to screen
    public void GoToScreen(GameObject screenObject) {
        for (int i = 0; i < screenObjects.Length; i++) {
            if (screenObjects[i] == screenObject) {
                GameController.currentScreen = screens[i];

                if (screenCoroutines != null) {
                    foreach (IEnumerator ie in screenCoroutines)
                        StopCoroutine(ie);
                }

                screenCoroutines = new IEnumerator[] { MoveToScreen(screens[i]), ZoomToScreen(screens[i]) };
                foreach (IEnumerator ie in screenCoroutines)
                    StartCoroutine(ie);
                return;
            }
        }
    }
    IEnumerator MoveToScreen(Screen screen) {
        Vector3 newPos = new Vector3(screen.transform.position.x, screen.transform.position.y, -1);
        while (Vector3.Distance(Camera.main.transform.position, newPos) > 0.1f) {
            Camera.main.transform.position = Vector3.MoveTowards(Camera.main.transform.position, newPos, Time.deltaTime * SCREEN_MOVE_SPEED);
            yield return null;
        }
        Camera.main.transform.position = newPos;
    }
    IEnumerator ZoomToScreen(Screen screen) {
        float newSize = screen.borders[0].transform.localScale.x * 2 - (GameController.BLOCK_SIZE / 10);
        while (Mathf.Abs(Camera.main.orthographicSize - newSize) > 0.1f) {
            Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, newSize, Time.deltaTime * SCREEN_ZOOM_SPEED);
            yield return null;
        }
        Camera.main.orthographicSize = newSize;
    }

    // Match gate position to sound clip
    public void OpenGate(bool open) {
        StartCoroutine(DoOpenGate(open));
    }
    IEnumerator DoOpenGate(bool open) {
        GameController.PlaySound(open ? AudioController.gateOpen : AudioController.gateClose);

        float waitTime = 0.3f;

        if (open) {
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(SetGateHeight(2));
            yield return new WaitForSeconds(waitTime);
            StartCoroutine(SetGateHeight(4));
            yield return new WaitForSeconds(waitTime);
            StartCoroutine(SetGateHeight(6));
            yield return new WaitForSeconds(waitTime);
            StartCoroutine(SetGateHeight(7));
        }
        else {
            StartCoroutine(SetGateHeight(6));
            yield return new WaitForSeconds(waitTime);
            StartCoroutine(SetGateHeight(4));
            yield return new WaitForSeconds(waitTime);
            StartCoroutine(SetGateHeight(2));
            yield return new WaitForSeconds(waitTime);
            StartCoroutine(SetGateHeight(0));
        }
    }
    IEnumerator SetGateHeight(int height) {
        Transform gate = GameController.gateAnimators[0].transform;
        while (Mathf.Abs(gate.localPosition.y - height) > 0.1f) {
            gate.localPosition = Vector2.MoveTowards(gate.localPosition, Vector2.up * height, Time.deltaTime * 15);
            yield return null;
        }
        gate.localPosition = Vector2.up * height;
    }
    public void BlinkGateLight() {
        if (GameController.gateData.blockData.state != 0)
            GameController.gateLight.enabled = false;
    }

    // Place fragment from inventory into gate slot
    public void PlaceFragment(Data slotData) {
        StartCoroutine(DoPlaceFragment(slotData));
    }
    IEnumerator DoPlaceFragment(Data slotData) {
        pc.disableMovement = true;
        pc.playerData.fragments--;
        StartCoroutine(UpdateFragmentCount());
        GameController.PlayRandomSound(AudioController.placeFragment);
        pc.SetHeadAnimation("Eat", 0);
        GameObject f = Instantiate(fragment);
        f.transform.position = pc.headData.blockObject.transform.position;

        int moveIndex = -1;
        float distance = 0;
        Vector2 position = f.transform.position;
        (int x, int y) direction = GameController.facingDirection[pc.headData.blockData.facing];

        while (moveIndex < 6) {
            distance = Mathf.MoveTowards(distance, 7, GameController.BLOCK_MOVE_SPEED * 1.5f * Time.deltaTime);
            if (distance > moveIndex + 1) {
                moveIndex = Mathf.Min((int)distance, 6);
                f.transform.position = position + new Vector2(direction.x * moveIndex, direction.y * moveIndex);
                yield return null;
            }
        }
        f.transform.position = slotData.blockObject.transform.position;

        Data fragData = GameController.currentCoordinateSystem.GetData(slotData.blockData.coordinates, Layer.COLLECT_DESTROY);
        if (fragData != null) {
            foreach (var al in pc.playerData.abilityLocations) {
                if (al.room == pc.playerData.currentRoom && al.coordinates == fragData.blockData.coordinates) {
                    pc.playerData.abilityLocations.Remove(al);
                    break;
                }
            }
            Destroy(f);
            fragData.layer = Layer.COLLECT;
            fragData.blockObject.SetActive(true);
            fragData.blockData.destroyed = false;
        }
        else {
            fragData = new Data(new BlockData("CollectFragment", 0, slotData.blockData.coordinates), f);
            GameController.currentCoordinateSystem.AddData(fragData);
        }

        var rdcs = GameController.currentMapSystem.GetRegion(pc.playerData.currentRoom).blockData.regionCoordinates;
        RegionData.RegionBlockData.RegionCoordinate regionCoordinate = null;
        foreach (var rdc in rdcs) {
            if (rdc.coordinates == fragData.blockData.coordinates && rdc.type == fragData.blockData.blockName) {
                regionCoordinate = rdc;
                break;
            }
        }
        if (regionCoordinate == null) {
            regionCoordinate = new RegionData.RegionBlockData.RegionCoordinate(fragData.blockData.blockName, fragData.blockData.coordinates);
            rdcs.Add(regionCoordinate);
        }
        pc.mapTraversalList[0].regionElements[0].regionCoordinates.Add(regionCoordinate);

        History history = GameController.historyList[GameController.historyList.Count - 1];
        HistoryChange.Place p = new HistoryChange.Place(fragData, slotData.blockData.coordinates);
        history.changes.Add(p);

        bool activateGate = true;
        foreach (var kvp in GameController.currentMiscBlocks) {
            if (activateGate && kvp.Value.blockData.blockName == "GateSlot") {
                Data fd = GameController.currentCoordinateSystem.GetData(kvp.Value.blockData.coordinates, Layer.COLLECT);
                if (fd == null)
                    activateGate = false;
                continue;
            }
        }
        GameController.SetGate(activateGate ? 0 : 2);

        yield return new WaitForSeconds(0.1f);

        pc.SetHeadAnimation("Eat", 1);
    }
    IEnumerator UpdateFragmentCount() {
        if (!GameController.showUI)
            yield break;

        fragmentCountText.text = pc.playerData.fragments + "";
        fragmentCount.SetActive(true);

        yield return new WaitForSeconds(3f);

        fragmentCount.SetActive(false);
    }

    // Eat collectable
    public void Eat(Data eatData) {
        StartCoroutine(DoEat(eatData));
    }
    IEnumerator DoEat(Data eatData) {
        pc.SetHeadAnimation("Eat", 0);
        History history = GameController.historyList[GameController.historyList.Count - 1];
        HistoryChange.Destroy d = new HistoryChange.Destroy(eatData);
        history.changes.Add(d);
        pc.playerData.abilityLocations.Add(new PlayerController.PlayerData.AbilityLocation(GameController.currentRoom, eatData.blockData.coordinates));

        // Check type of collectable and activate ability
        switch (eatData.blockData.blockName) {
            case "CollectFragment":
                GameController.PlayRandomSound(AudioController.collectFragment);
                HistoryChange.Collect cf = new HistoryChange.Collect("Fragment", 0, eatData.blockData.coordinates);
                history.changes.Add(cf);
                pc.playerData.fragments++;
                Data slotData = GameController.currentCoordinateSystem.GetData(eatData.blockData.coordinates, Layer.MISC);
                if (slotData != null && slotData.blockData.blockName == "GateSlot")
                    GameController.SetGate(1);
                StartCoroutine(UpdateFragmentCount());
                break;

            case "CollectLength":
                GameController.PlayRandomSound(AudioController.collectLength);
                HistoryChange.Collect cl = new HistoryChange.Collect("Length", 1, eatData.blockData.coordinates);
                history.changes.Add(cl);
                if (!pc.playerData.abilities[1]) {
                    pc.playerData.abilities[1] = true;
                    abilityTriggers[1] = true;
                    ShowAbilityInfo(1, true);
                }
                GameController.ShowLengthMeter(true);
                if (pc.playerData.currMaxLength < pc.playerData.maxLength) {
                    yield return new WaitForSeconds(0.4f);
                    lengthObjects[pc.playerData.currMaxLength].SetActive(true);
                    GameController.PlaySound(AudioController.lengthMeterAdd);
                    pc.playerData.currMaxLength++;
                }
                break;

            case "CollectTime":
                GameController.PlayRandomSound(AudioController.collectTime);
                HistoryChange.Collect ct = new HistoryChange.Collect("Time", 2, eatData.blockData.coordinates);
                if (!pc.playerData.abilities[2]) {
                    pc.playerData.abilities[2] = true;
                    abilityTriggers[2] = true;
                    ShowAbilityInfo(2, true);
                }
                else {
                    ct = new HistoryChange.Collect("Time", 3, eatData.blockData.coordinates);
                    pc.playerData.abilities[3] = true;
                    abilityTriggers[3] = true;
                    ShowAbilityInfo(3, true);
                    GameController.SavePlayer(pc);
                }
                history.changes.Add(ct);
                break;

            case "CollectRed":
                GameController.PlayRandomSound(AudioController.collectColor);
                HistoryChange.Collect cr = new HistoryChange.Collect("Color", 0, eatData.blockData.coordinates);
                history.changes.Add(cr);
                pc.playerData.abilities[0] = true;
                pc.playerData.colors[0] = true;
                abilityTriggers[0] = true;
                ShowAbilityInfo(0, true);
                break;

            case "CollectGreen":
                GameController.PlayRandomSound(AudioController.collectColor);
                HistoryChange.Collect cg = new HistoryChange.Collect("Color", 1, eatData.blockData.coordinates);
                history.changes.Add(cg);
                pc.playerData.abilities[0] = true;
                pc.playerData.colors[1] = true;
                abilityTriggers[0] = true;
                ShowAbilityInfo(0, true);
                break;

            case "CollectBlue":
                GameController.PlayRandomSound(AudioController.collectColor);
                HistoryChange.Collect cb = new HistoryChange.Collect("Color", 2, eatData.blockData.coordinates);
                history.changes.Add(cb);
                pc.playerData.abilities[0] = true;
                pc.playerData.colors[2] = true;
                abilityTriggers[0] = true;
                ShowAbilityInfo(0, true);
                break;
        }

        yield return new WaitForSeconds(0.1f);

        pc.SetHeadAnimation("Eat", 1);
        pc.eat = false;
    }

    // Display controls for new ability
    void ShowAbilityInfo(int ability, bool active) {
        if (!GameController.showUI)
            return;
        StartCoroutine(DoShowAbilityInfo(ability, active));
    }
    IEnumerator DoShowAbilityInfo(int ability, bool active) {
        abilityInfo[ability].SetActive(active);
        yield return null;
    }

    // Shoot projectile
    public void Shoot((int x, int y) origin, int facing) {
        StartCoroutine(DoShoot(origin, facing));
    }
    IEnumerator DoShoot((int x, int y) origin, int facing) {
        // Start animations
        if (!GameController.shooting) {
            shootRollbacks--;
            GameController.shooting = true;
            pc.SetHeadAnimation("StopNod", 0);
            pc.SetHeadAnimation("Eat", 0);
            pc.ColorEyes();
            GameController.PlayRandomSound(AudioController.playerShoot);
            GameController.DeactivateBulletButtons();
            StartCoroutine(ColorBullets());
        }
        GameController.activeBullets++;
        shootRollbacks++;

        // Init Bullet
        Bullet b = bulletPool.Dequeue();
        GameObject bullet = b.bulletObject;
        b.light.color = b.spriteRenderer.color = GameController.crystalColors[GameController.colorCycle];
        bullet.SetActive(true);
        bullet.transform.position = new Vector2(origin.x * GameController.BLOCK_SIZE, origin.y * GameController.BLOCK_SIZE);
        b.particles.transform.rotation = Quaternion.Euler(Vector3.forward * 90 * facing);
        (int x, int y) direction = GameController.facingDirection[facing];
        Vector3 worldDirection = new Vector3(direction.x, direction.y);

        // Move projectile and check for buttons/blocks
        Data hitData = null;
        (int x, int y) nextBlock = origin;
        while (hitData == null) {
            if (GameController.cancelBullets)
                break;

            nextBlock.x += direction.x;
            nextBlock.y += direction.y;

            hitData = GameController.currentCoordinateSystem.GetData(nextBlock, Layer.BLOCK);
            if (hitData == null) hitData = GameController.currentCoordinateSystem.GetData(nextBlock, Layer.TUNNEL);
            if (hitData == null) hitData = GameController.currentCoordinateSystem.GetData(nextBlock, Layer.PLAYER);

            int moveIndex = -1;
            float distance = 0;
            Vector2 position = bullet.transform.position;

            while ((hitData == null && moveIndex < 6) || (hitData != null && moveIndex < 3)) {
                distance = Mathf.MoveTowards(distance, hitData == null ? 7 : 4, GameController.BLOCK_MOVE_SPEED * 1.5f * Time.deltaTime);
                if (distance > moveIndex + 1) {
                    moveIndex = Mathf.Min((int)distance, hitData == null ? 6 : 3);
                    bullet.transform.position = position + new Vector2(direction.x * moveIndex, direction.y * moveIndex);
                    yield return null;
                }

                if (moveIndex > 2 && hitData == null) {
                    Data buttonData = GameController.currentCoordinateSystem.GetData(nextBlock, Layer.MISC);
                    if (buttonData != null) {
                        int colorIndex = GameController.ConvertColorNameToIndex(buttonData.blockData.blockName);
                        if (colorIndex != -1 && pc.playerData.colors[colorIndex])
                            GameController.SetButton(buttonData, 1, true);
                    }
                }
            }
            if (hitData == null)
                bullet.transform.position = new Vector2(nextBlock.x * GameController.BLOCK_SIZE, nextBlock.y * GameController.BLOCK_SIZE);
        }

        // Destroy projectile and create particles
        if (hitData != null && !hitData.blockData.blockName.Contains("Crystal") || GameController.cancelBullets) {
            b.particles.transform.position = bullet.transform.position;
            List<Color32> colors = new List<Color32>();
            for (int i = 0; i < 3; i++) {
                if (pc.playerData.colors[i])
                    colors.Add(GameController.crystalColors[i]);
            }

            ParticleSystem ps = b.particleSystem;
            var main = ps.main;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            if (GameController.cancelBullets) {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                if (shootRollbacks > 0)
                    shootRollbacks--;
                GameController.PlaySound(AudioController.bulletHit);
            }

            foreach (Color32 c in colors) {
                Color newColor = c;
                main.startColor = new ParticleSystem.MinMaxGradient(newColor);
                ps.Emit(9 / colors.Count);
            }
        }
        bullet.SetActive(false);
        bulletPool.Enqueue(b);
        GameController.activeBullets--;

        // Check what projectile hit, if crystal activate it
        if (hitData != null) {
            switch (hitData.blockData.blockName) {
                // Get nubs of red crystal and shoot another projectile from them, red crystals cannot be activated twice in same move
                case "RedCrystal":
                    shootRollbacks--;
                    if (!pc.playerData.colors[0])
                        break;

                    if (!activeRedCrystals.Contains(hitData.blockData.connectedBlocks)) {
                        activeRedCrystals.Add(hitData.blockData.connectedBlocks);
                        GameController.PlayRandomSound(AudioController.redShoot);
                        foreach (var c in hitData.blockData.connectedBlocks) {
                            Data d = GameController.currentCoordinateSystem.GetData(c, Layer.BLOCK);
                            d.blockObject.GetComponentInChildren<Animator>().SetBool("LoopActivate", true);

                            string type = d.blockObject.GetComponent<SpriteRenderer>().sprite.name.Split('_')[2];
                            if (int.Parse(type) % 3 == 0)
                                Shoot(c, d.blockData.facing);
                        }
                    }
                    break;

                // Move green crystal
                case "GreenCrystal":
                    if (!pc.playerData.colors[1])
                        break;

                    ActivateCrystals(hitData);
                    GameController.PlayRandomSound(AudioController.greenMove);

                    if (GameController.MoveBlock(hitData, direction, MoveType.BLOCK)) {
                        yield return new WaitUntil(() => !GameController.movingBlocks);
                        ApplyGravity();
                    }
                    else
                        GameController.PlayRandomSound(AudioController.playerBlocked);
                    break;

                // Destroy blue crystal
                case "BlueCrystal":
                    if (!pc.playerData.colors[2])
                        break;

                    ActivateCrystals(hitData);
                    GameController.PlayRandomSound(AudioController.blueBreak);
                    History history = new History();
                    GameController.historyList.Add(history);

                    Data floatData = GameController.currentCoordinateSystem.GetData(hitData.blockData.connectedBlocks[0], Layer.BLOCK);
                    GameObject floatHolder = new GameObject("FloatHolder");
                    GameController.ApplyData(floatData.blockData, floatHolder, false, false);
                    floatHolder.AddComponent(typeof(FloatMovement));
                    FloatMovement fm = floatHolder.GetComponent<FloatMovement>();
                    FloatMovement bfm = floatData.blockObject.GetComponentInChildren<FloatMovement>();
                    
                    foreach (var c in hitData.blockData.connectedBlocks) {
                        Data d = GameController.currentCoordinateSystem.GetData(c, Layer.BLOCK);
                        GameObject crystalBreak = Instantiate(Resources.Load("Blocks/BlueCrystalBreak") as GameObject);
                        GameController.ApplyData(d.blockData, crystalBreak, false, true);
                        crystalBreak.transform.SetParent(floatHolder.transform);
                        crystalBreak.GetComponent<SpriteRenderer>().color = crystalBreak.GetComponent<Light2D>().color = GameController.crystalColors[2];
                        Sprite[] sprites = Resources.LoadAll<Sprite>("Blocks/Sprites/block_bluecrystal_break");
                        if (sprites != null && sprites.Length > 0)
                            crystalBreak.GetComponent<SpriteRenderer>().sprite = sprites[d.blockData.state / 6];
                        ParticleSystem ps = crystalBreak.GetComponent<ParticleSystem>();
                        var main = ps.main;
                        main.startColor = new ParticleSystem.MinMaxGradient(GameController.crystalColors[2]);
                        ps.Play();
                        history.changes.Add(new HistoryChange.Destroy(d));
                    }

                    fm.Init(floatData, bfm.currentHeight, bfm.flip);
                    GameController.CheckLevelMisc();
                    ApplyGravity();
                    break;

                // Something else was hit
                default:
                    if (shootRollbacks > 0)
                        shootRollbacks--;
                    GameController.PlayRandomSound(AudioController.bulletHit);
                    break;
            }
        }

        // No more active projectiles, add rollbacks to history for red shoots and green moves
        if (GameController.activeBullets == 0) {
            foreach (var cc in activeRedCrystals) {
                if (cc != null) {
                    foreach (var c in cc) {
                        Data d = GameController.currentCoordinateSystem.GetData(c, Layer.BLOCK);
                        d.blockObject.GetComponentInChildren<Animator>().SetBool("LoopActivate", false);
                    }
                }
            }
            activeRedCrystals.Clear();

            if (GameController.historyList.Count > 0)
                GameController.historyList[GameController.historyList.Count - 1].rollbacks += shootRollbacks;
            shootRollbacks = 0;
            pc.SetHeadAnimation("Eat", 1);
            GameController.cancelBullets = false;
            GameController.shooting = false;
        }
    }

    // Play activation animation
    void ActivateCrystals(Data crystalData) {
        foreach (var c in crystalData.blockData.connectedBlocks) {
            Data d = GameController.currentCoordinateSystem.GetData(c, Layer.BLOCK);
            d.blockObject.GetComponentInChildren<Animator>().SetTrigger("Activate");
        }
    }

    // Flash through unlocked color abilities
    IEnumerator ColorBullets() {
        int cycle = GameController.colorCycle;
        while (!pc.playerData.colors[cycle])
            cycle = cycle + 1 > 2 ? 0 : cycle + 1;
        yield return null;

        while (GameController.shooting) {
            if (pc.playerData.colors[cycle]) {
                for (int bullet = 0; bullet < bulletAmount; bullet++) {
                    if (bullets[bullet].bulletObject.activeSelf)
                        bullets[bullet].light.color = bullets[bullet].spriteRenderer.color = GameController.crystalColors[cycle];
                }
                yield return new WaitForSeconds(0.1f);
            }
            cycle = cycle + 1 > 2 ? 0 : cycle + 1;
        }
    }

    public void FadeUndoOutline(GameObject outline, SpriteRenderer[] undoSprites) {
        undoCoroutines.TryGetValue(outline, out IEnumerator undoCoroutine);
        if (undoCoroutine == null) {
            undoCoroutine = DoFadeUndoOutline(outline, undoSprites);
            undoCoroutines.Add(outline, undoCoroutine);
        }
        else {
            StopCoroutine(undoCoroutines[outline]);
            undoCoroutines[outline] = DoFadeUndoOutline(outline, undoSprites);
        }
        StartCoroutine(undoCoroutines[outline]);
    }
    IEnumerator DoFadeUndoOutline(GameObject outline, SpriteRenderer[] undoSprites) {
        Color32 tc = GameController.timeColor;
        Color tc2 = new Color(tc.r / 255.0f, tc.g / 255.0f, tc.b / 255.0f, 0.75f);

        while (tc2.a > 0) {
            tc2.a -= Time.deltaTime * 2.5f;
            for (int i = 0; i < undoSprites.Length; i++)
                undoSprites[i].color = tc2;
            yield return null;
        }
        tc2.a = 0;
        for (int i = 0; i < undoSprites.Length; i++)
            undoSprites[i].color = tc2;
    }
    public void FadeResetOutline() {
        StartCoroutine(DoFadeResetOutline());
    }
    IEnumerator DoFadeResetOutline() {
        Color32 tc = GameController.timeColor;
        Color tc2 = new Color(tc.r / 255.0f, tc.g / 255.0f, tc.b / 255.0f);
        Color ec = new Color(pc.eyeDefaultColor.r / 255.0f, pc.eyeDefaultColor.g / 255.0f, pc.eyeDefaultColor.b / 255.0f);
        GameController.resetHolder.SetActive(true);

        float time = 0;
        while (GameController.resetting) {
            float alpha = Mathf.Sin(time * 10) * 0.75f;
            time += Time.deltaTime;
            if (alpha >= 0) {
                pc.eyeLight.intensity = alpha;
                pc.eyeSprite.color = Color.Lerp(ec, tc2, alpha);
            }
            Color color = new Color(tc2.r, tc2.g, tc2.b, alpha);
            foreach (SpriteRenderer sr in GameController.resetSprites)
                sr.color = color;
            yield return null;
        }

        foreach (SpriteRenderer sr in GameController.resetSprites)
            sr.color = new Color(tc2.r, tc2.g, tc2.b, 0);
        GameController.resetHolder.SetActive(false);
    }

    public void ShakeCamera(int fallHeight, int blockSize) {
        int priority = fallHeight * blockSize;
        if (priority <= priorityCameraShake)
            return;

        priorityCameraShake = priority;

        if (cameraShakeCoroutine != null)
            StopCoroutine(cameraShakeCoroutine);
        cameraShakeCoroutine = DoShakeCamera(Mathf.Clamp(fallHeight, 1, 5), Mathf.Clamp(blockSize, 1, 7));
        StartCoroutine(cameraShakeCoroutine);
    }
    IEnumerator DoShakeCamera(int shakes, int intensity) {
        Vector3 cameraPosition = Camera.main.transform.localPosition;
        for (int i = 0; i < shakes + 1; i++) {
            Vector3 randomPosition = cameraPosition;
            if (i < shakes)
                randomPosition = cameraPosition + new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)).normalized * Random.Range(Mathf.Clamp(intensity - 3, 1, intensity), intensity + 1);

            while (Vector3.Distance(Camera.main.transform.localPosition, randomPosition) > 0.1f) {
                Camera.main.transform.localPosition = Vector3.MoveTowards(Camera.main.transform.localPosition, randomPosition, Time.deltaTime * 50);
                yield return null;
            }
        }
        Camera.main.transform.localPosition = cameraPosition;
        priorityCameraShake = 0;
    }

    // Automatically move player through tunnel and load destination
    public void EnterTunnel(RegionData.RegionTunnel regionTunnel, Data tunnelData, bool tunnelBack) {
        StartCoroutine(DoEnterTunnel(regionTunnel, tunnelData, tunnelBack));
    }
    IEnumerator DoEnterTunnel(RegionData.RegionTunnel regionTunnel, Data tunnelData, bool tunnelBack) {
        string destinationID = regionTunnel.destinationID;
        if (destinationID == null || destinationID == "")
            destinationID = "ErrorRoom:0";

        string[] ids = destinationID.Split(':');
        string room = ids[0];
        int tunnel = int.Parse(ids[1]);

        regionTunnel.visited = true;
        RegionData rd = GameController.currentMapSystem.GetRegion(room);
        foreach (var rt in rd.blockData.regionTunnels) {
            if (rt.tunnelID == tunnel) {
                rt.visited = true;
                break;
            }
        }

        GameController.SaveMap(GameController.currentMapSystem);
        GameController.enterTunnelID = tunnel;
        GameController.startRoom = "";
        GameController.SavePlayer(pc);

        pc.flyMode = true;

        // Move backwards into tunnel
        if (tunnelBack) {
            GameController.PlayRandomSound(AudioController.playerTunnel);
            if (tunnelData.blockData.IsPrimary()) {
                int moveIndex = -1;
                float distance = 0;
                Vector2 position = pc.headData.blockObject.transform.position;
                (int x, int y) direction = GameController.backFacingDirection[pc.headData.blockData.facing];

                while (moveIndex < 6) {
                    distance = Mathf.MoveTowards(distance, 7, GameController.BLOCK_MOVE_SPEED * Time.deltaTime);
                    if (distance > moveIndex + 1) {
                        moveIndex = Mathf.Min((int)distance, 6);
                        pc.headData.blockObject.transform.position = position + new Vector2(direction.x * moveIndex, direction.y * moveIndex);
                        yield return null;
                    }
                    if (moveIndex > 2) pc.wormData[1].blockObject.SetActive(false);
                }
            }
            pc.worm[0].light.enabled = false;

            yield return new WaitForSeconds(0.2f);

            foreach (Data d in pc.wormData)
                d.blockObject.SetActive(false);

            ApplyGravity();
            yield return new WaitUntil(() => !GameController.applyingGravity);
            yield return new WaitForSeconds(0.01f);
        }
        else {
            // Set player to move through tunnel until all the way in
            (int x, int y) nextTunnel = (0, 0);
            (int x, int y) amount = (0, 0);
            float pitch = 0;

            for (int i = 0; i < pc.playerData.length + 1; i++) {
                if (i < tunnelData.blockData.connectedBlocks.Length) {
                    nextTunnel = tunnelData.blockData.connectedBlocks[i];
                    amount = (nextTunnel.x - pc.headData.blockData.coordinates.x, nextTunnel.y - pc.headData.blockData.coordinates.y);
                }
                if (!tunnelBack && i < pc.playerData.length) {
                    pc.worm[pc.DataToTotalIndex(i)].light.enabled = false;
                    if (i > 1) pc.worm[pc.DataToTotalIndex(i) - 2].spriteRenderer.enabled = false;
                    pitch = 1 + 0.1f * i;
                    GameController.PlayPitchedSound(AudioController.playerTunnel, pitch);
                }
                pc.MovePlayer(amount, true);
                yield return new WaitUntil(() => !GameController.moving && !pc.playerMoving);
                ApplyGravity();
                yield return new WaitUntil(() => !GameController.applyingGravity);
                yield return new WaitForSeconds(Mathf.Clamp(0.1f - 0.01f * pc.playerData.length, 0.01f, 0.1f));
            }
        }

        // Save level state and load next level
        pc.flyMode = false;
        GameController.enterTunnel = true;
        GameController.SaveData(GameController.levelName, GameController.currentSave, GameController.currentCoordinateSystem);
        SceneManager.LoadScene(room);
        GameController.tunneling = GameController.moving = false;
    }
    // Set player inside tunnel and move out once
    public void ExitTunnel(Data tunnelData) {
        StartCoroutine(DoExitTunnel(tunnelData));
    }
    IEnumerator DoExitTunnel(Data tunnelData) {
        if (tunnelData == null) {
            GameController.enterTunnel = false;
            yield break;
        }
        GameController.moving = true;

        // Check if tunnel was locked and open door
        Data doorData = GameController.currentCoordinateSystem.GetData(tunnelData.blockData.coordinates, Layer.MISC);
        if (doorData == null) {
            GameController.enterTunnel = false;
            GameController.moving = false;
            yield break;
        }

        GameController.ApplyData(doorData.blockData, GameController.tunnelResetSprite.gameObject, false, true);
        GameController.tunnelResetSprite.gameObject.SetActive(true);

        TunnelPanel tp = doorData.blockObject.GetComponent<TunnelPanel>();
        if (tp.colors != null && tp.colors.Length > 0)
            tp.SetOpen(0, false);

        GameController.SetNearestScreen(tunnelData.blockObject.transform.position);

        (int x, int y) amount = GameController.facingDirection[tunnelData.blockData.facing];
        if (amount.x != 0) {
            pc = GameObject.Find("PlayerController").GetComponent<PlayerController>();
            pc.worm[0].spriteRenderer.flipY = pc.playerData.flippedBlocks[0] = amount.x < 0;
            pc.eye.transform.localScale = new Vector3(1, pc.playerData.flippedBlocks[0] ? -1 : 1, 1);
        }
        GameController.SavePlayer(pc);

        yield return new WaitForSeconds(0.5f);

        // Move player out one block and push any obstacles
        var moveCoords = (tunnelData.blockData.coordinates.x + amount.x, tunnelData.blockData.coordinates.y + amount.y);
        Data moveBlock = GameController.currentCoordinateSystem.GetData(moveCoords, Layer.BLOCK);
        bool moved = false;
        if (moveBlock != null) {
            if (GameController.MoveBlock(moveBlock, amount, MoveType.PLAYER)) {
                moved = true;
                GameController.PlayRandomSound(AudioController.playerTunnel);
                pc.MovePlayer(amount, true);
                pc.worm[0].light.enabled = true;
            }
        }
        else {
            GameController.PlayRandomSound(AudioController.playerTunnel);
            pc.MovePlayer(amount, true);
            pc.worm[0].light.enabled = true;
        }
        GameController.enterTunnel = false;
        GameController.moving = false;

        if (moved) {
            yield return new WaitUntil(() => !pc.playerMoving);
            GameController.ApplyGravity();
        }
    }

    // Apply gravity
    public void ApplyGravity() {
        StartCoroutine(DoApplyGravity());
    }
    IEnumerator DoApplyGravity() {
        GameController.applyingGravity = true;

        // Get order for falling blocks
        Data[] gravityData = new Data[GameController.fallData.Length];
        List<Data> gravityList = new List<Data>();
        foreach (Data d in GameController.fallData) {
            int gravityIndex = GameController.GetListOrder(gravityList, d.blockData, GameController.gravityDirection);
            if (gravityIndex == -1) gravityList.Add(d);
            else                    gravityList.Insert(gravityIndex, d);
        }
        foreach (Data d in gravityList) {
            if (d.HasTag(Tag.PLAYER)) {
                Data dd = d;
                gravityList.Remove(d);
                gravityList.Insert(0, dd);
                break;
            }
        }
        gravityData = gravityList.ToArray();

        int[] gravityFallCounts = new int[gravityData.Length];
        int fallingBlocks = gravityData.Length;
        int rollbacks = 0;
        if (pc.flyMode)
            fallingBlocks--;

        // Move all blocks affected by gravity down 1 space at a time until they can't
        while (fallingBlocks > 0) {
            for (int i = 0; i < gravityData.Length; i++) {
                if (gravityData[i] != null) {
                    if (gravityData[i].HasTag(Tag.PLAYER)) {
                        if (pc.flyMode)
                            continue;
                    }
                    // Check if block can move down
                    if (!GameController.MoveBlock(gravityData[i], GameController.gravityDirection, MoveType.GRAVITY)) {
                        // Block fell at least once, play sound and emit particles based on distance
                        if (gravityFallCounts[i] > 0) {
                            float pitch = Mathf.Clamp(1 - 0.1f * ((gravityFallCounts[i] - 1) / 2), 0.4f, 1);
                            switch (gravityData[i].blockData.blockName) {
                                case "Rock":
                                    GameController.PlayPitchedSound(AudioController.rockLand, AudioController.GetRandomPitch(pitch));
                                    ShakeCamera(gravityFallCounts[i], gravityData[i].blockData.connectedBlocks.Length);
                                    break;
                            }
                            if (gravityData[i].HasTag(Tag.PLAYER)) {
                                pc.SetHeadAnimation("Fall", 1);
                                pc.LookCheck();
                            }

                            if (gravityData[i].blockData.blockName == "Rock" || gravityFallCounts[i] > 1) {
                                List<List<Data>> landDataList = new List<List<Data>>();
                                foreach (var c in gravityData[i].blockData.connectedBlocks) {
                                    Data d = GameController.currentCoordinateSystem.GetData(c, gravityData[i].layer);
                                    Data gd = GameController.currentCoordinateSystem.GetData((c.x, c.y - 1), Layer.BLOCK);
                                    if (gd != null && gd.blockData.blockName == "Ground") {
                                        bool foundList = false;
                                        foreach (var list in landDataList) {
                                            foreach (Data listD in list) {
                                                (int x, int y) adjacentCoords = (Mathf.Abs(listD.blockData.coordinates.x - d.blockData.coordinates.x),
                                                                                            listD.blockData.coordinates.y - d.blockData.coordinates.y);
                                                if (adjacentCoords == (1, 0)) {
                                                    list.Add(d);
                                                    foundList = true;
                                                    break;
                                                }
                                            }
                                            if (foundList)
                                                break;
                                        }
                                        if (!foundList)
                                            landDataList.Add(new List<Data>() { d });
                                    }
                                }
                                GameController.CreateLandingParticles(landDataList, gravityFallCounts[i]);
                            }
                        }
                        gravityData[i] = null;
                        fallingBlocks--;
                    }
                    else {
                        // Account for blocks pushed downward from move
                        if (GameController.additionalFallData.Count > 0) {
                            foreach (Data d in GameController.additionalFallData) {
                                for (int j = 0; j < gravityData.Length; j++) {
                                    if (d == gravityData[j]) {
                                        gravityFallCounts[j]++;
                                        break;
                                    }
                                }
                            }
                        }
                        gravityFallCounts[i]++;
                        rollbacks++;
                    }
                }
            }
            yield return new WaitUntil(() => GameController.fallingBlocks == 0);
        }

        // Fix cases for single/multiblock jumps
        if (GameController.jumpingFallData) {
            rollbacks++;
            GameController.jumpingFallData = false;
        }

        // Add rollbacks to prevent midair undos
        if (GameController.historyList != null && GameController.historyList.Count > 0)
            GameController.historyList[GameController.historyList.Count - 1].rollbacks += rollbacks;

        GameController.applyingGravity = false;
    }

    public IEnumerator MoveBlocks((int x, int y) amount, List<Data> blocks, bool gravityMove) {
        if (gravityMove)
            GameController.fallingBlocks++;

        GameController.movingBlocks = true;
        GameController.moveBlocks = blocks;
        float moveSpeed = gravityMove ? GameController.BLOCK_FALL_SPEED : GameController.BLOCK_MOVE_SPEED;

        int moveIndex = -1;
        float distance = 0;
        Vector2[] positions = new Vector2[blocks.Count];
        for (int i = 0; i < blocks.Count; i++)
            positions[i] = blocks[i].blockObject.transform.position;

        while (moveIndex < 6) {
            distance = Mathf.MoveTowards(distance, 7, moveSpeed * Time.deltaTime);
            if (distance > moveIndex + 1) {
                moveIndex = Mathf.Min((int)distance, 6);
                for (int i = 0; i < blocks.Count; i++)
                    blocks[i].blockObject.transform.position = positions[i] + new Vector2(amount.x * moveIndex, amount.y * moveIndex);
                yield return null;
            }
        }

        foreach (Data d in blocks)
            d.ApplyData();
        if (gravityMove)
            GameController.fallingBlocks--;
        GameController.movingBlocks = false;
    }
}