using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public enum MonsterType { Atiki }

public class MonsterFactory : MonoBehaviour
{
    public static MonsterFactory Instance { get; private set; }

    private List<Monster> _prefabs;

    private Dictionary<MonsterType, Monster> _monsters;

    private void Awake()
    {
        Instance = this;

        _prefabs = Resources.LoadAll<Monster>("Monsters").ToList();

        if (_monsters == null)
        {
            _monsters = new Dictionary<MonsterType, Monster>();
            foreach (var monster in _prefabs)
            {
                _monsters[monster.Type] = monster;
            }
        }
    }

    public Monster SpawnMonster(MonsterType type, Vector3 position)
    {
        if (_monsters.TryGetValue(type, out Monster prefab))
        {
            Monster result = Instantiate(prefab.gameObject, position, Quaternion.identity).GetComponent<Monster>();
            ChunkManager.Instance.AddObjectToChunk(result);
            return result;
        }
        Debug.LogError($"Monster type {type} not found in factory!");
        return null;
    }
}
