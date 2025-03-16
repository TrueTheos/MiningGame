using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static AudioManager;

public class CustomBuilding : PlacableItem
{
    public bool Solid;
    public int Durability;

    [SerializeField] private Vector2Int _size = new Vector2Int(1, 1);
    public Vector2Int Size => _size;

    [ContextMenuItem("Paste Positions", "PastePositions")]
    [SerializeField]
    private List<Vector2Int> _requiredFreeSpaces = new List<Vector2Int>();
    public List<Vector2Int> RequiredFreeSpaces => _requiredFreeSpaces;

    [SerializeField] private List<SFX> _overrideHitClips;
    public List<SFX> HitClips => _overrideHitClips;

    public void PastePositions()
    {
        string clipboard = UnityEditor.EditorGUIUtility.systemCopyBuffer;
        List<Vector2Int> parsedPositions = ParsePositions(clipboard);
        if (parsedPositions != null)
        {
            _requiredFreeSpaces = parsedPositions;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    public override void Holding()
    {
        base.Holding();
        var pos = MousePosToTilePos();
        bool result = WorldManager.Instance.TryPlace(pos.x, pos.y, this);

        if(result) Inventory.Instance.RemoveOne();
    }

    public virtual void PlayHitAnimation()
    {
        transform.DOScale(new Vector3(.8f, .8f, 1f), 0.05f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    transform.DOScale(Vector3.one, 0.05f)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() => {
                            WorldManager.Instance.OnHitAnimationEnd(Pos.x, Pos.y);
                        });
                });
    }

    private List<Vector2Int> ParsePositions(string text)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        string[] lines = text.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;
            string[] parts = trimmedLine.Split(',');
            if (parts.Length != 2)
                continue;
            if (int.TryParse(parts[0].Trim(), out int x) && int.TryParse(parts[1].Trim(), out int y))
            {
                list.Add(new Vector2Int(x, y));
            }
        }
        return list;
    }
}
