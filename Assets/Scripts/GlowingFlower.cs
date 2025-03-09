using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GlowingFlower : MonoBehaviour
{
    [SerializeField] private int _spawnChance;
    [SerializeField] private Vector2Int _numberOfMonsters;
    [SerializeField] private int _spawnRadius;

    [Header("Debug")]
    [SerializeField] private bool _drawGizmos;

    private List<AtikiMonster> _monsters = new();
    private WorldManager _worldManager;

    public void Start()
    {
        _worldManager = WorldManager.Instance;
        _worldManager.OnWorldReady.AddListener(SpawnMonsters);
    }

    private void SpawnMonsters()
    {
        if (!_spawnChance.RandTest()) return;

        int num = _numberOfMonsters.Random();

        Vector2Int pos = new Vector2Int(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y));

        for (int i = 0; i < num; i++)
        {
            Vector2Int? freeCell = _worldManager.GetRandomFreeCellInCircle(pos, _spawnRadius);
            if (freeCell == null) break;

            AtikiMonster atiki = MonsterFactory.Instance.SpawnMonster(MonsterType.Atiki, freeCell.Value.ToVector3(offset: .5f)) as AtikiMonster;
            _monsters.Add(atiki);
        }
    }

    public void OnDestroy()
    {
        foreach (var monster in _monsters)
        {
            if (monster != null) monster.Enrage();
        }
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        Gizmos.DrawWireSphere(transform.position, _spawnRadius);
    }
}
