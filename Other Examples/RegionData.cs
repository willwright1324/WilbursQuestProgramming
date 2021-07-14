using System.Collections.Generic;
using UnityEngine;

public class RegionData {
    public RegionBlockData blockData;
    public GameObject regionObject;
    public Texture2D regionTexture;

    [System.Serializable]
    public class RegionBlockData {
        [System.Serializable]
        public class RegionCoordinate {
            public string type;
            public (int x, int y) coordinates;

            public RegionCoordinate(string type, (int x, int y) coordinates) {
                this.type        = type;
                this.coordinates = coordinates;
            }
        }

        public string level;
        public bool visited;
        public int[] edges;
        public List<(int x, int y)> groundDustCoordinates = new List<(int x, int y)>();
        public List<(int x, int y)> airDustCoordinates    = new List<(int x, int y)>();
        public List<RegionCoordinate> regionCoordinates   = new List<RegionCoordinate>();
        public List<RegionTunnel> regionTunnels           = new List<RegionTunnel>();

        public RegionBlockData(string level) {
            this.level = level;
        }
    }

    [System.Serializable]
    public class RegionTunnel {
        public string destinationID;
        public int tunnelID;
        public bool visited;
        public (int x, int y)[] connectedBlocks;

        public RegionTunnel(string destinationID, int tunnelID, (int x, int y)[] connectedBlocks) {
            this.destinationID   = destinationID;
            this.tunnelID        = tunnelID;
            this.connectedBlocks = new (int x, int y)[connectedBlocks.Length];
            connectedBlocks.CopyTo(this.connectedBlocks, 0);
        }
    }

    public RegionData(RegionBlockData blockData, GameObject regionObject) {
        this.blockData    = blockData;
        this.regionObject = regionObject;
    }
}