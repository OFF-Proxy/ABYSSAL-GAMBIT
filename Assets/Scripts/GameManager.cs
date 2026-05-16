using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.EventSystems;

/// ゲーム全体の管理を行うクラス。
/// ユニットの生成やチームごとの管理を担当する。
public class GameManager : Manager<GameManager>
{
    public EntitiesDatabaseSO entitiesDatabase;

    public Transform team1Parent;
    public Transform team2Parent;
    public Transform benchParent;
    public Transform team1BenchTilesParent;
    public Transform team2BenchTilesParent;
    public int benchSlotCount = 8;
    public Vector3 benchStartPosition = new Vector3(-6.5f, -3.5f, 0f);
    public float benchSlotSpacing = 1f;
    public float benchPickRadius = 0.9f;
    public Camera boardCamera;
    public bool enableMouseWheelZoom = true;
    public float mouseWheelZoomSpeed = 8f;
    public float minCameraFieldOfView = 28f;
    public float maxCameraFieldOfView = 60f;
    public float minOrthographicSize = 3.5f;
    public float maxOrthographicSize = 8f;

    public Action OnRoundStart;
    public Action OnRoundEnd;
    public Action<BaseEntity> OnUnitDied;
    public Action OnRosterChanged;

    List<BaseEntity> team1Entities = new List<BaseEntity>();
    List<BaseEntity> team2Entities = new List<BaseEntity>();
    List<BaseEntity> benchEntities = new List<BaseEntity>();
    Dictionary<BaseEntity, int> benchSlotByEntity = new Dictionary<BaseEntity, int>();

    int unitsPerTeam = 6;
    public bool IsRoundInProgress { get; private set; }
    public bool HasBenchSpace => benchEntities.Count < benchSlotCount;
    public int PlacedTeam1Count => team1Entities.Count;
    public int PlacementLimit => PlayerData.Instance != null ? PlayerData.Instance.Level : 1;

    private void Update()
    {
        HandleMouseWheelZoom();
    }

    public bool CanBuyEntity(EntitiesDatabaseSO.EntityData entityData)
    {
        return HasBenchSpace || CanCompleteUpgradeWithPurchase(entityData.name);
    }

    public void OnEntityBought(EntitiesDatabaseSO.EntityData entityData)
    {
        if (!CanBuyEntity(entityData))
        {
            Debug.LogWarning("Bench is full. Cannot buy more units.");
            return;
        }

        EnsureBenchParent();

        int slotIndex = GetFreeBenchSlot();

        BaseEntity newEntity = Instantiate(entityData.prefab, benchParent);
        newEntity.InitializeIdentity(entityData.name, entityData.cost);

        benchEntities.Add(newEntity);
        if (slotIndex != -1)
            benchSlotByEntity[newEntity] = slotIndex;

        newEntity.SetupOnBench(Team.Team1, slotIndex != -1 ? GetBenchPosition(slotIndex) : GetBenchPosition(0));
        ResolveUpgradesFor(newEntity);
        OnRosterChanged?.Invoke();
    }

    public List<BaseEntity> GetEntitiesAgainst(Team against)
    {
        if (against == Team.Team1)
            return team2Entities;
        else
            return team1Entities;
    }

    public bool CanDragEntity(BaseEntity entity)
    {
        if (entity == null || entity.Team != Team.Team1)
            return false;

        if (!IsRoundInProgress)
            return true;

        return IsEntityOnBench(entity);
    }

    public bool IsEntityOnBench(BaseEntity entity)
    {
        return entity != null && benchSlotByEntity.ContainsKey(entity);
    }

    public bool CanPlaceEntityManually(BaseEntity entity, Node node)
    {
        if (entity == null || node == null)
            return false;

        if (IsRoundInProgress)
            return false;

        if (entity.Team != Team.Team1)
            return false;

        if (!GridManager.Instance.CanManuallyPlace(entity.Team, node))
            return false;

        BaseEntity nodeOccupant = GetTeam1EntityAtNode(node);
        if (nodeOccupant != null && nodeOccupant != entity)
            return IsEntityOnBench(entity) || entity.IsOnBoard;

        if (node.IsOccupied && entity.CurrentNode != node)
            return false;

        if (!entity.IsOnBoard && PlacedTeam1Count >= PlacementLimit)
            return false;

        return true;
    }

    public bool TryPlaceEntityManually(BaseEntity entity, Node node)
    {
        if (!CanPlaceEntityManually(entity, node))
            return false;

        if (TrySwapEntityWithBoardEntity(entity, node))
            return true;

        if (entity.CurrentNode != null && entity.CurrentNode != node)
            entity.CurrentNode.SetOccupied(false);

        if (benchEntities.Remove(entity))
        {
            benchSlotByEntity.Remove(entity);
            entity.transform.SetParent(team1Parent, true);
        }

        if (!team1Entities.Contains(entity))
            team1Entities.Add(entity);

        entity.Setup(entity.Team, node);
        OnRosterChanged?.Invoke();
        return true;
    }

    private bool TrySwapEntityWithBoardEntity(BaseEntity movingEntity, Node boardNode)
    {
        if (movingEntity == null || boardNode == null)
            return false;

        BaseEntity boardEntity = GetTeam1EntityAtNode(boardNode);
        if (boardEntity == null || boardEntity == movingEntity)
            return false;

        if (benchSlotByEntity.TryGetValue(movingEntity, out int originalBenchSlot))
            return SwapBenchEntityWithBoardEntity(movingEntity, boardEntity, originalBenchSlot, boardNode);

        return SwapBoardEntityWithBoardEntity(movingEntity, boardEntity, boardNode);
    }

    private bool SwapBenchEntityWithBoardEntity(BaseEntity benchEntity, BaseEntity boardEntity, int originalBenchSlot, Node boardNode)
    {
        if (benchEntity == null || boardEntity == null || boardNode == null)
            return false;

        EnsureBenchParent();

        if (boardEntity.CurrentNode != null)
            boardEntity.CurrentNode.SetOccupied(false);

        team1Entities.Remove(boardEntity);
        if (!benchEntities.Contains(boardEntity))
            benchEntities.Add(boardEntity);

        benchSlotByEntity[boardEntity] = originalBenchSlot;
        boardEntity.transform.SetParent(benchParent, true);
        boardEntity.SetupOnBench(boardEntity.Team, GetBenchPosition(originalBenchSlot));

        benchEntities.Remove(benchEntity);
        benchSlotByEntity.Remove(benchEntity);
        benchEntity.transform.SetParent(team1Parent, true);
        if (!team1Entities.Contains(benchEntity))
            team1Entities.Add(benchEntity);

        benchEntity.Setup(benchEntity.Team, boardNode);
        OnRosterChanged?.Invoke();
        return true;
    }

    private bool SwapBoardEntityWithBoardEntity(BaseEntity movingEntity, BaseEntity targetEntity, Node targetNode)
    {
        if (movingEntity == null || targetEntity == null || targetNode == null)
            return false;

        Node originalNode = movingEntity.CurrentNode;
        if (originalNode == null)
            return false;

        originalNode.SetOccupied(false);
        targetNode.SetOccupied(false);

        targetEntity.Setup(targetEntity.Team, originalNode);
        movingEntity.Setup(movingEntity.Team, targetNode);

        OnRosterChanged?.Invoke();
        return true;
    }

    public bool CanPlaceEntityOnBench(BaseEntity entity, int slotIndex)
    {
        if (entity == null || entity.Team != Team.Team1)
            return false;

        if (slotIndex < 0 || slotIndex >= benchSlotCount)
            return false;

        if (IsRoundInProgress && !IsEntityOnBench(entity))
            return false;

        BaseEntity occupant = GetBenchEntityAtSlot(slotIndex);
        if (occupant == null || occupant == entity)
            return true;

        return IsEntityOnBench(entity) || entity.IsOnBoard;
    }

    public bool TryPlaceEntityOnBench(BaseEntity entity, int slotIndex)
    {
        if (!CanPlaceEntityOnBench(entity, slotIndex))
            return false;

        EnsureBenchParent();
        EnsureBenchTileParents();

        BaseEntity occupant = GetBenchEntityAtSlot(slotIndex);
        if (occupant != null && occupant != entity && TrySwapEntityWithBenchEntity(entity, occupant, slotIndex))
            return true;

        if (entity.CurrentNode != null)
        {
            entity.CurrentNode.SetOccupied(false);
            team1Entities.Remove(entity);
        }

        if (!benchEntities.Contains(entity))
            benchEntities.Add(entity);

        benchSlotByEntity[entity] = slotIndex;
        entity.transform.SetParent(benchParent, true);
        entity.SetupOnBench(entity.Team, GetBenchPosition(slotIndex));
        OnRosterChanged?.Invoke();
        return true;
    }

    private bool TrySwapEntityWithBenchEntity(BaseEntity movingEntity, BaseEntity targetBenchEntity, int targetSlot)
    {
        if (movingEntity == null || targetBenchEntity == null || movingEntity == targetBenchEntity)
            return false;

        EnsureBenchParent();
        EnsureBenchTileParents();

        if (benchSlotByEntity.TryGetValue(movingEntity, out int originalBenchSlot))
            return SwapBenchEntityWithBenchEntity(movingEntity, targetBenchEntity, originalBenchSlot, targetSlot);

        return SwapBoardEntityWithBenchEntity(movingEntity, targetBenchEntity, targetSlot);
    }

    private bool SwapBenchEntityWithBenchEntity(BaseEntity movingEntity, BaseEntity targetEntity, int originalSlot, int targetSlot)
    {
        if (!benchSlotByEntity.ContainsKey(targetEntity))
            return false;

        benchSlotByEntity[movingEntity] = targetSlot;
        benchSlotByEntity[targetEntity] = originalSlot;

        movingEntity.SetupOnBench(movingEntity.Team, GetBenchPosition(targetSlot));
        targetEntity.SetupOnBench(targetEntity.Team, GetBenchPosition(originalSlot));

        OnRosterChanged?.Invoke();
        return true;
    }

    private bool SwapBoardEntityWithBenchEntity(BaseEntity movingEntity, BaseEntity targetBenchEntity, int targetSlot)
    {
        Node originalNode = movingEntity.CurrentNode;
        if (originalNode == null)
            return false;

        originalNode.SetOccupied(false);

        team1Entities.Remove(movingEntity);
        if (!benchEntities.Contains(movingEntity))
            benchEntities.Add(movingEntity);

        benchSlotByEntity[movingEntity] = targetSlot;
        movingEntity.transform.SetParent(benchParent, true);
        movingEntity.SetupOnBench(movingEntity.Team, GetBenchPosition(targetSlot));

        benchEntities.Remove(targetBenchEntity);
        benchSlotByEntity.Remove(targetBenchEntity);
        targetBenchEntity.transform.SetParent(team1Parent, true);
        if (!team1Entities.Contains(targetBenchEntity))
            team1Entities.Add(targetBenchEntity);

        targetBenchEntity.Setup(targetBenchEntity.Team, originalNode);

        OnRosterChanged?.Invoke();
        return true;
    }

    public int GetBenchSlotAtWorldPosition(Team team, Vector3 worldPosition)
    {
        EnsureBenchTileParents();

        Transform benchTilesParent = team == Team.Team1 ? team1BenchTilesParent : team2BenchTilesParent;
        if (benchTilesParent == null)
            return -1;

        int closestSlot = -1;
        float closestDistance = benchPickRadius * benchPickRadius;
        for (int i = 0; i < Mathf.Min(benchSlotCount, benchTilesParent.childCount); i++)
        {
            float distance = (benchTilesParent.GetChild(i).position - worldPosition).sqrMagnitude;
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestSlot = i;
            }
        }

        return closestSlot;
    }

    public Tile GetBenchTileAtWorldPosition(Team team, Vector3 worldPosition)
    {
        int slotIndex = GetBenchSlotAtWorldPosition(team, worldPosition);
        return GetBenchTileAtSlot(team, slotIndex);
    }

    public Tile GetBenchTileAtSlot(Team team, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= benchSlotCount)
            return null;

        EnsureBenchTileParents();

        Transform benchTilesParent = team == Team.Team1 ? team1BenchTilesParent : team2BenchTilesParent;
        if (benchTilesParent == null || slotIndex >= benchTilesParent.childCount)
            return null;

        Tile tile = benchTilesParent.GetChild(slotIndex).GetComponent<Tile>();
        if (tile == null)
            tile = benchTilesParent.GetChild(slotIndex).gameObject.AddComponent<Tile>();

        if (tile != null && GridManager.Instance != null)
            GridManager.Instance.ConfigureBenchTile(tile, team);

        return tile;
    }

    public void UnitDead(BaseEntity entity)
    {
        if (entity == null)
            return;

        team1Entities.Remove(entity);
        team2Entities.Remove(entity);

        OnUnitDied?.Invoke(entity);

        entity.DestroyAfterDeathAnimation();
        TryEndRound();
        OnRosterChanged?.Invoke();
    }


    public void DebugFight()
    {
        if (IsRoundInProgress)
            return;

        if (team1Entities.Count == 0)
        {
            Debug.LogWarning("Place at least one unit before starting a fight.");
            return;
        }

        for (int i = 0; i < unitsPerTeam; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, entitiesDatabase.allEntities.Count);
            EntitiesDatabaseSO.EntityData entityData = entitiesDatabase.allEntities[randomIndex];
            BaseEntity newEntity = Instantiate(entityData.prefab, team2Parent);
            newEntity.InitializeIdentity(entityData.name, entityData.cost);

            team2Entities.Add(newEntity);

            Node freeNode = GridManager.Instance.GetFreeNode(Team.Team2);
            if (freeNode == null)
            {
                team2Entities.Remove(newEntity);
                Destroy(newEntity.gameObject);
                continue;
            }

            newEntity.Setup(Team.Team2, freeNode);
        }

        if (team2Entities.Count == 0)
            return;

        IsRoundInProgress = true;
        OnRoundStart?.Invoke();
    }

    private void EnsureBenchParent()
    {
        if (benchParent != null)
            return;

        GameObject bench = new GameObject("BenchUnits");
        bench.transform.SetParent(team1Parent != null ? team1Parent : transform);
        bench.transform.position = Vector3.zero;
        benchParent = bench.transform;
    }

    private Vector3 GetBenchPosition(int slotIndex)
    {
        EnsureBenchTileParents();

        if (team1BenchTilesParent != null && slotIndex < team1BenchTilesParent.childCount)
            return team1BenchTilesParent.GetChild(slotIndex).position;

        return benchStartPosition + new Vector3(0f, slotIndex * benchSlotSpacing, 0f);
    }

    private void EnsureBenchTileParents()
    {
        if (team1BenchTilesParent == null)
        {
            GameObject team1Bench = GameObject.Find("Grid/BenchLeft");
            if (team1Bench != null)
                team1BenchTilesParent = team1Bench.transform;
        }

        if (team2BenchTilesParent == null)
        {
            GameObject team2Bench = GameObject.Find("Grid/BenchRight");
            if (team2Bench != null)
                team2BenchTilesParent = team2Bench.transform;
        }
    }

    private int GetFreeBenchSlot()
    {
        for (int i = 0; i < benchSlotCount; i++)
        {
            if (!benchSlotByEntity.ContainsValue(i))
                return i;
        }

        return -1;
    }

    private BaseEntity GetBenchEntityAtSlot(int slotIndex)
    {
        foreach (KeyValuePair<BaseEntity, int> pair in benchSlotByEntity)
        {
            if (pair.Value == slotIndex)
                return pair.Key;
        }

        return null;
    }

    private void HandleMouseWheelZoom()
    {
        if (!enableMouseWheelZoom)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.01f)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Camera targetCamera = boardCamera != null ? boardCamera : Camera.main;
        if (targetCamera == null)
            return;

        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize - scroll * mouseWheelZoomSpeed * 0.1f,
                minOrthographicSize,
                maxOrthographicSize);
            return;
        }

        targetCamera.fieldOfView = Mathf.Clamp(
            targetCamera.fieldOfView - scroll * mouseWheelZoomSpeed,
            minCameraFieldOfView,
            maxCameraFieldOfView);
    }

    private BaseEntity GetTeam1EntityAtNode(Node node)
    {
        if (node == null)
            return null;

        for (int i = 0; i < team1Entities.Count; i++)
        {
            BaseEntity entity = team1Entities[i];
            if (entity != null && entity.CurrentNode == node)
                return entity;
        }

        return null;
    }

    public bool CanCompleteUpgradeWithPurchase(string unitId)
    {
        return GetUpgradePreviewStarWithPurchase(unitId) > 0;
    }

    public int GetUpgradePreviewStarWithPurchase(string unitId)
    {
        if (string.IsNullOrEmpty(unitId))
            return 0;

        int star1Count = CountOwnedUnits(unitId, 1) + 1;
        int star2Count = CountOwnedUnits(unitId, 2);
        int previewStarLevel = 0;

        if (star1Count >= 3)
        {
            star1Count -= 3;
            star2Count++;
            previewStarLevel = 2;
        }

        if (star2Count >= 3)
            previewStarLevel = 3;

        return previewStarLevel;
    }

    public int GetSellValue(BaseEntity entity)
    {
        if (entity == null)
            return 0;

        int starMultiplier = 1;
        if (entity.StarLevel == 2)
            starMultiplier = 3;
        else if (entity.StarLevel >= 3)
            starMultiplier = 8;

        return Mathf.Max(1, entity.BaseCost) * starMultiplier;
    }

    public bool TrySellEntity(BaseEntity entity)
    {
        if (entity == null || entity.Team != Team.Team1 || PlayerData.Instance == null)
            return false;

        int sellValue = GetSellValue(entity);
        RemoveOwnedEntity(entity);
        PlayerData.Instance.AddMoney(sellValue);
        OnRosterChanged?.Invoke();
        return true;
    }

    private void ResolveUpgradesFor(BaseEntity preferredEntity)
    {
        if (preferredEntity == null || preferredEntity.Team != Team.Team1)
            return;

        BaseEntity current = preferredEntity;
        bool upgraded;
        do
        {
            upgraded = TryUpgradeOneGroup(current.UnitId, current.StarLevel, current, out current);
        }
        while (upgraded && current != null && current.StarLevel < 3);
    }

    private bool TryUpgradeOneGroup(string unitId, int starLevel, BaseEntity preferredEntity, out BaseEntity upgradedEntity)
    {
        upgradedEntity = preferredEntity;

        if (starLevel >= 3)
            return false;

        List<BaseEntity> matches = GetOwnedUnits(unitId, starLevel);
        if (matches.Count < 3)
            return false;

        BaseEntity target = ChooseUpgradeTarget(matches, preferredEntity);
        List<BaseEntity> consumed = matches.Where(entity => entity != target).Take(2).ToList();

        for (int i = 0; i < consumed.Count; i++)
            RemoveOwnedEntity(consumed[i]);

        target.ApplyStarLevel(starLevel + 1);
        upgradedEntity = target;
        return true;
    }

    private BaseEntity ChooseUpgradeTarget(List<BaseEntity> matches, BaseEntity preferredEntity)
    {
        BaseEntity boardEntity = matches.FirstOrDefault(entity => entity.IsOnBoard);
        if (boardEntity != null)
            return boardEntity;

        BaseEntity slottedBenchEntity = matches.FirstOrDefault(entity => entity != preferredEntity && benchSlotByEntity.ContainsKey(entity));
        if (slottedBenchEntity != null)
            return slottedBenchEntity;

        if (preferredEntity != null && matches.Contains(preferredEntity))
            return preferredEntity;

        return matches[0];
    }

    private List<BaseEntity> GetOwnedUnits(string unitId, int starLevel)
    {
        return team1Entities
            .Concat(benchEntities)
            .Where(entity => entity != null && entity.Team == Team.Team1 && entity.UnitId == unitId && entity.StarLevel == starLevel)
            .Distinct()
            .ToList();
    }

    private int CountOwnedUnits(string unitId, int starLevel)
    {
        return GetOwnedUnits(unitId, starLevel).Count;
    }

    private void RemoveOwnedEntity(BaseEntity entity)
    {
        if (entity == null)
            return;

        if (entity.CurrentNode != null)
            entity.CurrentNode.SetOccupied(false);

        team1Entities.Remove(entity);
        benchEntities.Remove(entity);
        benchSlotByEntity.Remove(entity);
        Destroy(entity.gameObject);
    }

    private void TryEndRound()
    {
        if (!IsRoundInProgress)
            return;

        if (team1Entities.Count > 0 && team2Entities.Count > 0)
            return;

        IsRoundInProgress = false;
        OnRoundEnd?.Invoke();
    }
}

public enum Team
{
    Team1,
    Team2
}
