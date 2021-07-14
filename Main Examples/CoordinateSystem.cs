using System.Collections.Generic;

// Stores block locations, blocks of the same layer cannot occupy the same location
public class CoordinateSystem {
    public Dictionary<(int x, int y), List<Data>> coordinateData;

    public CoordinateSystem(Dictionary<(int x, int y), List<Data>> coordinateData) {
        if (coordinateData == null) this.coordinateData = new Dictionary<(int x, int y), List<Data>>();
        else                        this.coordinateData = coordinateData;
    }

    // Get list of all blocks in location
    public List<Data> GetList((int x, int y) coordinates) {
        coordinateData.TryGetValue(coordinates, out List<Data> dataList);
        return dataList;
    }
    // Add new list to empty location
    public void AddList((int x, int y) coordinates, List<Data> dataList) {
        if (dataList == null)
            return;

        if (GetList(coordinates) == null) {
            RemoveList(coordinates);
            coordinateData.Add(coordinates, dataList);
        }
    }
    // Remove list from location
    public void RemoveList((int x, int y) coordinates) {
        if (coordinateData.TryGetValue(coordinates, out List<Data> dataList))
            coordinateData.Remove(coordinates);
    }

    // Get data at location and layer
    public Data GetData((int x, int y) coordinates, Layer layer) {
        List<Data> dataList = GetList(coordinates);

        if (dataList != null) {
            foreach (Data d in dataList) {
                if (d.layer == layer)
                    return d;
            }
        }
        return null;
    }
    // Move data from one location to another by amount
    public void MoveData((int x, int y) amount, Data data, bool instant) {
        (int x, int y) coordinates = (data.blockData.coordinates.x + amount.x, data.blockData.coordinates.y + amount.y);
        if (data == null || GetData(coordinates, data.layer) != null)
            return;

        RemoveData(data);
        data.blockData.coordinates = coordinates;
        AddData(data);
        if (instant) data.ApplyData();
    }
    // Move data from one location to another by coordinates
    public void SetData((int x, int y) coordinates, Data data, bool instant) {
        if (data == null || GetData(coordinates, data.layer) != null)
            return;

        RemoveData(data);
        data.blockData.coordinates = coordinates;
        AddData(data);
        if (instant)
            data.ApplyData();
    }
    // Add data to list at its location
    public void AddData(Data data) {
        if (data == null)
            return;

        List<Data> dataList = GetList(data.blockData.coordinates);

        if (dataList != null) {
            foreach (Data d in dataList) {
                if (d.layer == data.layer)
                    return;
            }
            dataList.Add(data);
        }
        else {
            dataList = new List<Data>();
            dataList.Add(data);
            AddList(data.blockData.coordinates, dataList);
        }
    }
    // Remove data from its list
    public void RemoveData(Data data) {
        if (data == null)
            return;

        List<Data> dataList = GetList(data.blockData.coordinates);

        if (dataList != null) {
            dataList.Remove(data);
            if (dataList.Count == 0)
                coordinateData.Remove(data.blockData.coordinates);
        }
    }

    // Remove data from list at coordinates and layer
    public void RemoveAt((int x, int y) coordinates, Layer layer) {
        RemoveData(GetData(coordinates, layer));
    }
}