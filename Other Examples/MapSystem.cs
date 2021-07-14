using System.Collections.Generic;

public class MapSystem {
    public Dictionary<string, RegionData> mapData = new Dictionary<string, RegionData>();

    public RegionData GetRegion(string regionName) {
        if (regionName == null)
            return null;

        mapData.TryGetValue(regionName, out RegionData regionData);
        return regionData;
    }
    public void AddRegion(string regionName, RegionData mapRegion) {
        if (regionName == null || mapRegion == null)
            return;

        if (GetRegion(regionName) == null)
            mapData.Add(regionName, mapRegion);
    }
    public void RemoveRegion(string regionName) {
        if (regionName == null)
            return;

        if (mapData.TryGetValue(regionName, out RegionData regionData))
            mapData.Remove(regionName);
    }
}