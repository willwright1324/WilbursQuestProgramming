// Types of history changes
public enum ChangeType { MOVE, DESTROY, GROW, COLLECT, PLACE, BUTTON, TEMPDOOR }
public class HistoryChange {
    public ChangeType changeType;

    public class Move : HistoryChange {
        public Data data;
        public Data originalData;
        public (int x, int y) location;
        public bool headFlipped;

        public Move(Data data, (int x, int y) amount, (int x, int y)[] connectedBlocks) {
            changeType = ChangeType.MOVE;
            this.data = new Data(data);
            originalData = data;
            location = (data.blockData.coordinates.x + amount.x, data.blockData.coordinates.y + amount.y);
            this.data.blockData.connectedBlocks = connectedBlocks;
        }
    }

    public class Grow : HistoryChange {
        public (int x, int y) amount;
        public (int x, int y) backAmount;

        public Grow((int x, int y) amount) {
            changeType = ChangeType.GROW;
            this.amount = amount;
            backAmount = (-amount.x, -amount.y);
        }
    }

    public class Destroy : HistoryChange {
        public Data data;
        public Layer layer;
        public Destroy(Data data) {
            changeType = ChangeType.DESTROY;
            this.data = data;
            layer = data.layer;
            data.layer = layer == Layer.BLOCK ? Layer.BLOCK_DESTROY : Layer.COLLECT_DESTROY;
            data.blockData.destroyed = true;
            data.blockObject.SetActive(false);
        }
    }

    public class Collect : HistoryChange {
        public string abilityType;
        public int abilityIndex;
        public (int x, int y) coordinates;

        public Collect(string abilityType, int abilityIndex, (int x, int y) coordinates) {
            changeType = ChangeType.COLLECT;
            this.abilityType = abilityType;
            this.abilityIndex = abilityIndex;
            this.coordinates = coordinates;
        }
    }

    public class Place : HistoryChange {
        public Data data;
        public (int x, int y) coordinates;

        public Place(Data data, (int x, int y) coordinates) {
            changeType = ChangeType.PLACE;
            this.data = data;
            this.coordinates = coordinates;
        }
    }

    public class Button : HistoryChange {
        public Data data;
        public int state;

        public Button(Data data, int state) {
            changeType = ChangeType.BUTTON;
            this.data = data;
            this.state = state;
        }
    }

    public class TempDoor : HistoryChange {
        public TunnelPanel tunnelPanel;

        public TempDoor(TunnelPanel tunnelPanel) {
            changeType = ChangeType.TEMPDOOR;
            this.tunnelPanel = tunnelPanel;
        }
    }
}