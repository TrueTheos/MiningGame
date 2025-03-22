using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AudioManager;

public class Pike : Item, IWeapon, IDamageColliderReceiver
{
    [SerializeField] private int _damage;
    [SerializeField] private SFX _whooshAudio;
    public int Damage => _damage;

    private Animation _animation;
    private BoxCollider2D _collider;

    private bool _canAttack = true;

    private Vector3 _defaultRotation;
    private Vector3 _defaultArtRotation;

    private void Start()
    {
        _animation = GetComponent<Animation>();
        _collider = GetComponentInChildren<BoxCollider2D>();
        _canAttack = true;
        _defaultRotation = transform.localRotation.eulerAngles;
        _defaultArtRotation = SpriteRend.transform.localRotation.eulerAngles;
    }

    public override void UseOnce()
    {
        if (!_canAttack) return;
        StartCoroutine(Attack());
        AudioManager.Instance.Play(_whooshAudio);

        Vector2 handPos = Player.Instance.transform.position;
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePos - handPos;

        float horizontalDirection = 0;

        if (mousePos.x > Player.Instance.transform.position.x)
        {
            horizontalDirection = 1;
        }
        else if (mousePos.x < Player.Instance.transform.position.x)
        {
            horizontalDirection = -1; // Move left
        }
        Player.Instance.Movement.AddForce(new Vector2(horizontalDirection * 2f, 0), .1f);

        if (!Player.Instance.IsFacingRight)
        {
            direction = -direction;
        }

        transform.right = direction;
    }

    private IEnumerator Attack()
    {
        SpriteRend.enabled = true;
        transform.position = Player.Instance.transform.position;
        _canAttack = false;
        _animation.Play();
        yield return new WaitForSeconds(_animation.clip.length);
        _canAttack = true;
        _animation.Stop();
        SpriteRend.transform.localRotation = Quaternion.Euler(_defaultArtRotation);
        //ransform.position = _hand.position;
        //transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(_defaultRotation);
        SpriteRend.enabled = false;
    }

    public void OnDisable()
    {
        _canAttack = true;
    }

    public void Receive(GameObject collider)
    {
        if (_canAttack) return;
        if (collider.TryGetComponent(out Entity entity))
        {
            entity.TakeDamage(_damage, DamageSource.Weapon);
        }
    }
}
