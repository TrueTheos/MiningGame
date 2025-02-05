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

    private void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    [System.Obsolete]
    public void Hit()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int mousePosition2D = new Vector2Int(Mathf.RoundToInt(mousePos.x - .5f), Mathf.RoundToInt(mousePos.y - .5f));
        WorldManager.Instance.Hit(mousePosition2D, _power);
    }

    public override void UseOnce()
    {
        _isHolding = true;
        _anim.SetBool("Mining", true);
        StartCoroutine(Mining());
    }

    public override void EndUse()
    {
        _isHolding = false;
        _anim.SetBool("Mining", false);
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
