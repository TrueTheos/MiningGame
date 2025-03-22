using Assets.Scripts.Managers.WorldGeneration;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class WorldLayer : MonoBehaviour
{
    public int Height;

    protected int _startY;
    protected int _endY;
    protected int _width;

    protected WorldManager _worldManager;
    protected WorldGenerator _worldGenerator;

    [SerializeField] protected GameObject _background;

    private Vector2Int[] _sixDirections =
{
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
            new Vector2Int(-1,  0),                    new Vector2Int(1,  0),
            new Vector2Int(-1,  1), new Vector2Int(0,  1), new Vector2Int(1,  1)
        };

    private void Awake()
    {
        _worldManager = GetComponentInParent<WorldManager>();
        _worldGenerator = GetComponentInParent<WorldGenerator>();
    }

    public void Generate(int startY)
    {
        _startY = startY;
        _endY = startY + Height;
        _width = _worldManager.WorldWidth;

        if (_background != null)
        {
            _background.transform.localScale = new Vector3(_width / 64, Height / 64, 1);
            _background.transform.localPosition = new Vector3(_width / 2f, _startY + Height / 2f, 0);
        }
        StartCoroutine(GenerateLayer());
    }

    protected abstract IEnumerator GenerateLayer();

    public void OnFinish()
    {
        _worldGenerator.GenerateNextLayer();
    }

    protected List<Vector2Int> GetNeighbors(int x, int y)
    {
        List<Vector2Int> res = new List<Vector2Int>();

        foreach (var dir in _sixDirections)
        {
            int nx = x + dir.x, ny = y + dir.y;
            if (nx >= 0 && nx < _width && ny >= 0 && ny < _endY)
            {
                res.Add(new Vector2Int(nx, ny));
            }
        }

        return res;
    }
}
