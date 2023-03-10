using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RTSPlayer : NetworkBehaviour
{
    [SerializeField] private LayerMask buildingBlockLayer = new LayerMask();
    [SerializeField] private Building[] buildings = new Building[0];
    [SerializeField] private float buildingRangeLimit = 5f;

    [SyncVar(hook = nameof(ClientHandleResourcesUpdated))]
    private int resources = 500;
    public event Action<int> ClientOnResourcesUpdated;

    private Color teamColor = new Color();

    private List<Unit> myUnits = new List<Unit>();
    private List<Building> myBuildings = new List<Building>();

    public Color GetTeamColor()
    {
        return teamColor;
    }
    
    public List<Unit> GetMyUnits()
    {
        return myUnits;
    }
    public List<Building> GetMyBuildings()
    {
        return myBuildings;
    }

    public int GetResources()
    {
        return resources;
    }
    

    public bool CanPlaceBuilding(BoxCollider buildingCollider, Vector3 point)
    {
        if (Physics.CheckBox(
            point + buildingCollider.center,
            buildingCollider.size / 2,
            Quaternion.identity,
            buildingBlockLayer))
        {
            return false;
        }

        foreach (Building building in myBuildings)
        {
            if ((point - building.transform.position).sqrMagnitude
                <= buildingRangeLimit * buildingRangeLimit)
            {
                return true;
            }
        }
        return false;
    }


    #region Server
    public override void OnStartServer()
    {
        Unit.ServerOnUnitSpawned += ServerHandleUnitsSpawned;
        Unit.ServerOnUnitDespawned += ServerHandleUnitsDespawned;

        Building.ServerOnBuildingSpawned += ServerHandleBuildingsSpawned;
        Building.ServerOnBuildingDespawned += ServerHandleBuildingsDespawned;
    }

    public override void OnStopServer()
    {
        Unit.ServerOnUnitSpawned -= ServerHandleUnitsSpawned;
        Unit.ServerOnUnitDespawned -= ServerHandleUnitsDespawned;
        
        Building.ServerOnBuildingSpawned -= ServerHandleBuildingsSpawned;
        Building.ServerOnBuildingDespawned -= ServerHandleBuildingsDespawned;
    }

    [Server]
    public void SetResources(int newResources)
    {
        resources = newResources;
    }

    [Server]
    public void SetTeamColor(Color newTeamColor)
    {
        teamColor = newTeamColor;
    }

    [Command]
    public void CmdTryPlaceBuilding(int buildingId, Vector3 point)
    {
        Building buildingToPlace = null;
        foreach (Building building in buildings)
        {
            if(building.GetId() == buildingId)
            {
                buildingToPlace = building;
                break;
            }
        }

        if(buildingToPlace == null) {return;}

        if(resources < buildingToPlace.GetPrice()) {return;}

        BoxCollider buildingCollider = buildingToPlace.GetComponent<BoxCollider>();
        
        if(!CanPlaceBuilding(buildingCollider, point)){return;}
        GameObject buildingInstance = 
            Instantiate(buildingToPlace.gameObject, point, buildingToPlace.transform.rotation);

        NetworkServer.Spawn(buildingInstance, connectionToClient);

        SetResources(resources - buildingToPlace.GetPrice());
    }



    private void ServerHandleUnitsSpawned(Unit unit)
    {
        if(unit.connectionToClient.connectionId != connectionToClient.connectionId) {return;}
        myUnits.Add(unit);
    }
    private void ServerHandleUnitsDespawned(Unit unit)
    {
        if (unit.connectionToClient.connectionId != connectionToClient.connectionId) { return; }
        myUnits.Remove(unit);
    }
    private void ServerHandleBuildingsSpawned(Building building)
    {
        if (building.connectionToClient.connectionId != connectionToClient.connectionId) { return; }
        myBuildings.Add(building);
    }

    private void ServerHandleBuildingsDespawned(Building building)
    {
        if (building.connectionToClient.connectionId != connectionToClient.connectionId) { return; }
        myBuildings.Remove(building);
    }
    #endregion
    #region Client
    public override void OnStartAuthority()
    {
        if(NetworkServer.active){return;}
        Unit.AuthorityOnUnitSpawned += AuthorityHandleUnitsSpawned;
        Unit.AuthorityOnUnitDespawned += AuthorityHandleUnitsDespawned;

        Building.AuthorityOnBuildingSpawned += AuthorityHandleBuildingsSpawned;
        Building.AuthorityOnBuildingSpawned += AuthorityHandleBuildingsDespawned;
    }

    public override void OnStopClient()
    {
        if (!isClientOnly || !isOwned) { return; }
        Unit.AuthorityOnUnitSpawned += AuthorityHandleUnitsSpawned;
        Unit.AuthorityOnUnitDespawned += AuthorityHandleUnitsDespawned;

        Building.AuthorityOnBuildingSpawned -= AuthorityHandleBuildingsSpawned;
        Building.AuthorityOnBuildingSpawned -= AuthorityHandleBuildingsDespawned;
    }

    private void AuthorityHandleUnitsSpawned(Unit unit)
    {
        myUnits.Add(unit);
    }
    private void AuthorityHandleUnitsDespawned(Unit unit)
    {
        myUnits.Add(unit);
    }

    private void AuthorityHandleBuildingsDespawned(Building building)
    {
        myBuildings.Add(building);
    }

    private void AuthorityHandleBuildingsSpawned(Building building)
    {
        myBuildings.Remove(building);
    }

    private void ClientHandleResourcesUpdated(int oldResources, int newResources)
    {
        ClientOnResourcesUpdated?.Invoke(newResources);
    }
    #endregion
}
