using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Flags]
public enum PlacementType
{
    None = 0,
    Anywhere = 1,
    Ground = 2,
    Wall = 4,
    Ceiling = 8,
    NeedsSupport = 16,
    SameType = 32
}

public abstract class PlacableItem : Item
{
    public SpriteRenderer Art;

    [Header("Placement Settings")]
    [SerializeField] private PlacementType allowedPlacements;
    [SerializeField] private bool destroyWhenSupportDestroyed = true;

    [HideInInspector] public Vector2Int Pos;

    public abstract void Place(int x, int y);

    public abstract bool CanPlace(int x, int y);

    public Vector2Int MousePosToTilePos()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int mousePosition2D = new Vector2Int(Mathf.RoundToInt(mousePos.x - .5f), Mathf.RoundToInt(mousePos.y - .5f));

        return mousePosition2D;
    }

    public virtual void OnBreak() 
    {
        for (int x = Pos.x - 1; x <= Pos.x + 1; x++)
        {
            for (int y = Pos.y - 1; y <= Pos.y + 1; y++)
            {
                if (x == Pos.x && y == Pos.y) continue;

                var building = WorldManager.Instance.Buildings[x, y];

                if(building != null)
                {
                    building.DestroyIfInvalid();
                }
            }
        }

        Destroy(gameObject);
    }

    public void DestroyIfInvalid()
    {
        if (!destroyWhenSupportDestroyed) return;
        if (!IsPlacementValid(Pos.x, Pos.y))
        {
            OnBreak();
        }
    }

    public bool IsPlacementValid(int x, int y)
    {
        if (allowedPlacements.HasFlag(PlacementType.Anywhere))
            return true;

        if (allowedPlacements.HasFlag(PlacementType.Ground) && CheckGround(x, y))
            return true;

        if (allowedPlacements.HasFlag(PlacementType.Wall) && CheckWall(x, y))
            return true;

        if (allowedPlacements.HasFlag(PlacementType.Ceiling) && CheckCeiling(x, y))
            return true;

        if (allowedPlacements.HasFlag(PlacementType.NeedsSupport) && HasRequiredSupport(x, y))
            return true;

        return false;
    }

    private bool CheckGround(int x, int y)
    {
        var botTile = WorldManager.Instance.WorldData[x, y - 1];
        if (botTile != null && botTile.Solid) return true;

        var botBuilding = WorldManager.Instance.Buildings[x, y - 1];
        if (botBuilding != null && (botBuilding.Solid ||
            (allowedPlacements.HasFlag(PlacementType.SameType) && botBuilding.GetType() == this.GetType())))
            return true;

        return false;
    }

    private bool CheckWall(int x, int y)
    {
        var leftTile = WorldManager.Instance.WorldData[x - 1, y];
        if (leftTile != null && leftTile.Solid) return true;
        var rightTile = WorldManager.Instance.WorldData[x + 1, y];
        if (rightTile != null && rightTile.Solid) return true;

        var leftBuilding = WorldManager.Instance.Buildings[x - 1, y];
        if (leftBuilding != null && (leftBuilding.Solid ||
            (allowedPlacements.HasFlag(PlacementType.SameType) && leftBuilding.GetType() == this.GetType())))
            return true;

        var rightBuilding = WorldManager.Instance.Buildings[x + 1, y];
        if (rightBuilding != null && (rightBuilding.Solid ||
            (allowedPlacements.HasFlag(PlacementType.SameType) && rightBuilding.GetType() == this.GetType())))
            return true;

        return false;
    }

    private bool CheckCeiling(int x, int y)
    {
        var topTile = WorldManager.Instance.WorldData[x, y + 1];
        if (topTile != null && topTile.Solid) return true;

        var topBuilding = WorldManager.Instance.Buildings[x, y + 1];
        if (topBuilding != null && (topBuilding.Solid ||
            (allowedPlacements.HasFlag(PlacementType.SameType) && topBuilding.GetType() == this.GetType())))
            return true;
        return false;
    }

    private bool HasRequiredSupport(int x, int y)
    {
        if (allowedPlacements.HasFlag(PlacementType.Anywhere))
            return true;

        // Check all allowed placement types
        if (allowedPlacements.HasFlag(PlacementType.Ground) && CheckGround(x,y))
            return true;
        if (allowedPlacements.HasFlag(PlacementType.Wall) && CheckWall(x, y))
            return true;
        if (allowedPlacements.HasFlag(PlacementType.Ceiling) && CheckCeiling(x, y))
            return true;
        if (allowedPlacements.HasFlag(PlacementType.NeedsSupport))
            return CheckGround(x, y) || CheckWall(x, y) || CheckCeiling(x, y);

        return false;
    }
}
