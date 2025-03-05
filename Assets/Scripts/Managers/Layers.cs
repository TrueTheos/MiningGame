using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Layers : MonoBehaviour
{
    [SerializeField] private LayerMask _monsterLayer;
    public static LayerMask MONSTER_LAYER => _instance?._monsterLayer ?? 0;

    [SerializeField] private LayerMask _groundLayer;
    public static LayerMask GROUND_LAYER => _instance?._groundLayer ?? 0;

    [SerializeField] private LayerMask _playerLayer;
    public static LayerMask PLAYER_LAYER => _instance?._playerLayer ?? 0;

    private static Layers _instance;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
