using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BlockData {
    public string blockName;
    public int facing = 0;
    public int state = -1;
    public bool destroyed;
    public (int x, int y) coordinates;
    public (int x, int y)[] connectedBlocks;

    public BlockData(string blockName, int facing, (int x, int y) coordinates) {
        this.blockName   = blockName;
        this.facing      = facing;
        this.coordinates = coordinates;
    }

    public bool IsPrimary() {
        return connectedBlocks != null && coordinates == connectedBlocks[0];
    }
}