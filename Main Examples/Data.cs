using UnityEngine;

public class Data {
    public BlockData blockData;
    public GameObject blockObject;
    public GameObject undoHolder;
    public SpriteRenderer[] undoSprites;
    public Tag[] tags;
    public Layer layer;

    public Data(BlockData blockData, GameObject blockObject) {
        this.blockData   = blockData;
        this.blockObject = blockObject;
        tags  = GameController.infoDictionary[blockData.blockName].tags;
        layer = GameController.infoDictionary[blockData.blockName].layer;
    }
    public Data(Data data) {
        blockData       = new BlockData(data.blockData.blockName, data.blockData.facing, data.blockData.coordinates);
        blockData.state = data.blockData.state;
        blockObject     = data.blockObject;
        undoHolder      = data.undoHolder;
        undoSprites     = data.undoSprites;

        if (data.tags != null) {
            tags = new Tag[data.tags.Length];
            data.tags.CopyTo(tags, 0);
        }
        layer = data.layer;
    }

    // Check if tags contains any of given tags
    public bool HasTag(params Tag[] tags) {
        if (this.tags == null)
            return false;

        foreach (Tag t in tags) {
            foreach (Tag tt in this.tags) {
                if (t == tt)
                    return true;
            }
        }
        return false;
    }
    // Check if tags contains all of given tags
    public bool HasAllTags(params Tag[] tags) {
        if (this.tags == null || this.tags.Length < tags.Length)
            return false;

        foreach (Tag t in tags) {
            bool found = false;
            foreach (Tag tt in this.tags) {
                if (t == tt) {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }

    // Applies coordinates and rotation to transform
    public void ApplyData() {
        if (blockObject == null)
            return;

        blockObject.transform.eulerAngles = Vector3.forward * blockData.facing * 90;
        blockObject.transform.position = new Vector2(blockData.coordinates.x * GameController.BLOCK_SIZE, blockData.coordinates.y * GameController.BLOCK_SIZE);
    }
}