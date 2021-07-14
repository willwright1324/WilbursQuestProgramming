#if (UNITY_EDITOR)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

[ExecuteInEditMode]
public class GenerateMap : MonoBehaviour {
    public class Tunnel {
        public (int x, int y) globalCoordinates;
        public RegionData regionData;
        public RegionData.RegionTunnel regionTunnel;

        public Tunnel((int x, int y) globalCoordinates, RegionData regionData, RegionData.RegionTunnel regionTunnel) {
            this.globalCoordinates = globalCoordinates;
            this.regionData        = regionData;
            this.regionTunnel      = regionTunnel;
        }
    }

    void Update() {
        MapSystem mapSystem = GameController.LoadMap();
        GameControllerCoroutines gcc = GameObject.FindWithTag("GameController").GetComponent<GameControllerCoroutines>();
        GameObject mapRegionObject = Resources.Load("BlockMisc/MapRegionP") as GameObject;
        GameController.InitMap(transform.parent.gameObject, mapSystem);

        // Destroy any regions from the previous MapSystem that aren't in current iteration
        MapRegion[] regions = GetComponentsInChildren<MapRegion>();
        foreach (MapRegion m in regions) {
            if (mapSystem.GetRegion(m.name) == null) {
                DestroyImmediate(m.gameObject);
                continue;
            }
        }

        // Go through the checked off scenes in build
        var ebs = EditorBuildSettings.scenes;
        for (int i = 1; i < SceneManager.sceneCountInBuildSettings; i++) {
            string levelName = SceneUtility.GetScenePathByBuildIndex(i).Split('/')[2].Split('.')[0];
            RegionData regionData = mapSystem.GetRegion(levelName);
            TextAsset ta = Resources.Load("BlockData/" + levelName) as TextAsset;
            // Remove regions that are missing data, or were present but unchecked for this iteration
            if (ta == null || !ebs[i].enabled) {
                if (regionData != null) {
                    mapSystem.RemoveRegion(levelName);
                    if (regionData.regionObject != null)
                        DestroyImmediate(regionData.regionObject);
                }
                continue;
            }
            // Create regions that aren't present
            if (regionData == null) {
                regionData = new RegionData(new RegionData.RegionBlockData(levelName), null);
                mapSystem.AddRegion(levelName, regionData);
            }
            else
                regionData.blockData.regionCoordinates = new List<RegionData.RegionBlockData.RegionCoordinate>();

            // Go through coordinateSystem for region to find all empty spaces that are also connected to tunnels
            CoordinateSystem coordinateSystem = GameController.LoadData(levelName, 0);
            Dictionary<(int x, int y), bool> validEmpties = new Dictionary<(int x, int y), bool>();
            List<Data> checkList = new List<Data>();
            foreach (var kvp in coordinateSystem.coordinateData) {
                Data tunnelData = coordinateSystem.GetData(kvp.Key, Layer.TUNNEL);
                if (tunnelData != null && tunnelData.blockData.IsPrimary()) {
                    (int x, int y)[] coords = GameController.facingDirection;
                    int facing = tunnelData.blockData.facing;
                    (int x, int y) checkCoord = (tunnelData.blockData.coordinates.x + coords[facing].x, tunnelData.blockData.coordinates.y + coords[facing].y);
                    checkList.Add(new Data(new BlockData("Ground", 0, checkCoord), null));
                    int checkIndex = 0;
                    do {
                        Data d = coordinateSystem.GetData(checkList[checkIndex].blockData.coordinates, Layer.BLOCK);
                        if (d == null || d.blockData.blockName != "Ground") {
                            if (d == null)
                                d = checkList[checkIndex];
                            if (regionData.blockData.edges == null)
                                print(d + " " + regionData.blockData.level + " " + levelName + " is null");
                            if (!(d.blockData.coordinates.y > regionData.blockData.edges[0]
                               || d.blockData.coordinates.y < regionData.blockData.edges[1]
                               || d.blockData.coordinates.x < regionData.blockData.edges[2]
                               || d.blockData.coordinates.x > regionData.blockData.edges[3])) {
                                try { validEmpties.Add(d.blockData.coordinates, true); } catch { }
                                for (int j = 0; j < 4; j++) {
                                    (int x, int y) c = (d.blockData.coordinates.x + coords[j].x, d.blockData.coordinates.y + coords[j].y);
                                    Data cd = coordinateSystem.GetData(c, Layer.BLOCK);
                                    if (cd == null || cd.blockData.blockName != "Ground") {
                                        bool validated = false;
                                        try { validated = validEmpties[c]; } catch { }
                                        if (!validated)
                                            checkList.Add(new Data(new BlockData("Ground", 0, c), null));
                                        try { validEmpties.Add(c, true); } catch { }
                                    }
                                }
                            }
                        }
                    } while (++checkIndex < checkList.Count);
                }
            }
            foreach (var kvp in validEmpties)
                regionData.blockData.regionCoordinates.Add(new RegionData.RegionBlockData.RegionCoordinate("Empty", kvp.Key));

            // Create map object for newly added regionsp
            if (regionData.regionObject == null) {
                GameObject go = Instantiate(mapRegionObject, transform.parent);
                go.name = levelName;
                regionData.regionObject = go;
            }

            // Get coordinates for displayed blocks
            regionData.blockData.regionTunnels = new List<RegionData.RegionTunnel>();
            foreach (var kvp in coordinateSystem.coordinateData) {
                Data blockData = coordinateSystem.GetData(kvp.Key, Layer.BLOCK);
                Data tunnelData = coordinateSystem.GetData(kvp.Key, Layer.TUNNEL);

                // Add tunnel
                if (tunnelData != null && tunnelData.blockData.IsPrimary())
                    regionData.blockData.regionTunnels.Add(new RegionData.RegionTunnel("", -1, tunnelData.blockData.connectedBlocks));

                if (blockData != null) {
                    switch (blockData.blockData.blockName) {
                        // Add ground blocks that are touching at least one empty space
                        case "Ground":
                            bool[] nearData = GameController.GetNearBlocks(blockData.blockData.coordinates, "Ground", coordinateSystem);
                            bool empty = false;
                            for (int j = 0; j < nearData.Length; j++) {
                                if (!nearData[j]) {
                                    bool valid = false;
                                    (int x, int y) checkCoord = (blockData.blockData.coordinates.x + GameController.compassDirection[j].x,
                                                                 blockData.blockData.coordinates.y + GameController.compassDirection[j].y);
                                    try { valid = validEmpties[checkCoord]; } catch { }
                                    if (valid) {
                                        empty = true;
                                        break;
                                    }
                                }
                            }
                            if (empty) {
                                if (regionData.blockData.edges == null || regionData.blockData.edges.Length == 0)
                                    regionData.blockData.regionCoordinates.Add(new RegionData.RegionBlockData.RegionCoordinate("Ground", kvp.Key));
                                else {
                                    if (regionData.blockData.edges.Length > 0) {
                                        if (blockData.blockData.coordinates.x > regionData.blockData.edges[2]
                                            && blockData.blockData.coordinates.x < regionData.blockData.edges[3]
                                            && blockData.blockData.coordinates.y > regionData.blockData.edges[1]
                                            && blockData.blockData.coordinates.y < regionData.blockData.edges[0]) {
                                            regionData.blockData.regionCoordinates.Add(new RegionData.RegionBlockData.RegionCoordinate("Ground", kvp.Key));
                                        }
                                    }
                                }
                            }
                            break;

                        // Add gate and one pixel of surrounding blocks for UI emphasis
                        case "Gate":
                            for (int x = -1; x < 2; x++) {
                                for (int y = -1; y < 2; y++) {
                                    (int x, int y) gateCoord = (blockData.blockData.coordinates.x + x, blockData.blockData.coordinates.y  + y);
                                    regionData.blockData.regionCoordinates.Add(new RegionData.RegionBlockData.RegionCoordinate(blockData.blockData.blockName, gateCoord));
                                }
                            }
                            break;
                    }
                }
                // Add collectable
                Data collectData = coordinateSystem.GetData(kvp.Key, Layer.COLLECT);
                if (collectData != null)
                    regionData.blockData.regionCoordinates.Add(new RegionData.RegionBlockData.RegionCoordinate(collectData.blockData.blockName, collectData.blockData.coordinates));
            }
        }

        // Convert local coordinates of tunnel endings to global coordinates between map objects
        List<Tunnel> tunnels = new List<Tunnel>();
        foreach (var kvp in mapSystem.mapData) {
            string name = kvp.Value.blockData.level;
            (int x, int y) localCoord = ((int)kvp.Value.regionObject.transform.localPosition.x, (int)kvp.Value.regionObject.transform.localPosition.y);
            for (int i = 0; i < kvp.Value.blockData.regionTunnels.Count; i++) {
                RegionData.RegionTunnel rt = kvp.Value.blockData.regionTunnels[i];
                rt.tunnelID = i;
                (int x, int y) convertedCoordEnd = (rt.connectedBlocks[rt.connectedBlocks.Length - 1].x * 5, rt.connectedBlocks[rt.connectedBlocks.Length - 1].y * 5);
                (int x, int y) globalTunnelCoordEnd = (localCoord.x + convertedCoordEnd.x, localCoord.y + convertedCoordEnd.y);
                tunnels.Add(new Tunnel(globalTunnelCoordEnd, kvp.Value, rt));
            }
        }
        // Connect tunnel endings between map objects to automatically set destinations and ids
        List<Tunnel> connectedTunnels = new List<Tunnel>();
        for (int i = 0; i < tunnels.Count; i++) {
            Tunnel t = tunnels[i];
            if (connectedTunnels.Contains(t))
                continue;

            (int x, int y) inValidCoord = t.regionTunnel.connectedBlocks[t.regionTunnel.connectedBlocks.Length - 2];
            (int x, int y) inValidGlobalCoord = (inValidCoord.x * 5, inValidCoord.y * 5);

            bool found = false;
            for (int j = 0; j < 4; j++) {
                (int x, int y) checkGlobalCoord = (t.globalCoordinates.x + GameController.compassDirection[j].x * 5, t.globalCoordinates.y + GameController.compassDirection[j].y * 5);
                for (int k = 0; k < tunnels.Count; k++) {
                    Tunnel t2 = tunnels[k];
                    if (t2 == t)
                        continue;

                    if (t2.globalCoordinates == checkGlobalCoord && t2.globalCoordinates != inValidGlobalCoord) {
                        t.regionTunnel.destinationID = t2.regionData.blockData.level + ":" + t2.regionTunnel.tunnelID;
                        t2.regionTunnel.destinationID = t.regionData.blockData.level + ":" + t.regionTunnel.tunnelID;
                        connectedTunnels.Add(t2);
                        found = true;
                        break;
                    }
                }
                if (found)
                    break;
            }
            if (!found)
                print("Error: " + t.regionData.blockData.level + ", " + t.globalCoordinates);
        }

        GameController.SaveMap(mapSystem);
        enabled = false;
    }
}
#endif