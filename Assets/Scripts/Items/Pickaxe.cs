using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pickaxe : Item
{
    [SerializeField] private int _power;
    [SerializeField] private float _cooldown;
    [SerializeField] private AnimationClip _clip;

    private Animator _anim;

    private bool _isHolding = false;

    [HideInInspector] public Vector2Int OverridePos = Vector2Int.zero;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    [System.Obsolete]
    public void Hit()
    {
        if (!_isHolding) return;
        if (OverridePos != Vector2Int.zero)
        {
            WorldManager.Instance.Hit(OverridePos, _power);
            OverridePos = Vector2Int.zero;
        }
        else
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int mousePosition2D = new Vector2Int(Mathf.RoundToInt(mousePos.x - .5f), Mathf.RoundToInt(mousePos.y - .5f));
            WorldManager.Instance.Hit(mousePosition2D, _power);
        }
    }

    public override void UseOnce()
    {
        base.UseOnce();
        if (_isHolding) return;
        _isHolding = true;
        _anim.SetBool("Mining", true);
        StartCoroutine(Mining());
    }

    public override void EndUse()
    {
        base.EndUse();
        _isHolding = false;
        _anim.SetBool("Mining", false);
        OverridePos = Vector2Int.zero;
    }

    private IEnumerator Mining()
    {
        while (_isHolding)
        {
            _anim.speed = _clip.length / _cooldown;
            yield return new WaitForSeconds(_cooldown);
        }

        _anim.speed = 1f;
    }
}
