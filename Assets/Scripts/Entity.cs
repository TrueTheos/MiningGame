using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DamageSource { Default, Weapon, Fall}

public abstract class Entity : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _art;
    [SerializeField] protected int _maxHealth;
    public int MaxHealth => _maxHealth;
    public int CurrentHealth;
    
    private float _blinkDuration = .1f;
    private Material _blinkMaterial;
    private Material _originalMaterial;
    private Coroutine _blinkRoutine;

    protected virtual void Awake()
    {
        _originalMaterial = _art.material;
        _blinkMaterial = Resources.Load<Material>("BlinkMat");
    }

    public virtual void TakeDamage(int damage, DamageSource sourceType, Transform sourcePos = null)
    {
        CurrentHealth -= damage;
        OnTakeDamage(sourceType);

        if (CurrentHealth <= 0) Die();
    }

    public void Die()
    {
        OnDie();
    }

    public virtual void OnTakeDamage(DamageSource sourceType) 
    {
        Blink();
    }

    public virtual void OnDie() { }
    public void Blink()
    {
        if (_blinkRoutine != null)
        {
            StopCoroutine(_blinkRoutine);
        }

        _blinkRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        _art.material = _blinkMaterial;

        yield return new WaitForSeconds(_blinkDuration);

        _art.material = _originalMaterial;

        _blinkRoutine = null;
    }
}
