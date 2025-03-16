using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AudioManager;

public class Door : CustomBuilding
{
    [SerializeField] private Sprite _closedSprite;
    [SerializeField] private Sprite _openSprite;
    [SerializeField] private bool _isOpen = false;
    [SerializeField] private float _autoOpenDistance = 1.5f;
    [SerializeField] private float _autoCloseDelay = 2f;
    [SerializeField] private bool _autoOpenEnabled = true;
    [SerializeField] private SFX _openSound, _closeSound;

    private BoxCollider2D _doorCollider;
    private Player _playerTransform;
    private float _closeTimer = 0f;

    private void Start()
    {
        _doorCollider = GetComponent<BoxCollider2D>();
        _playerTransform = Player.Instance;
        SpriteRend.sprite = _closedSprite;
        _doorCollider.enabled = true;
        _isOpen = false;
    }

    public override void OnPlace(int x, int y)
    {
        base.OnPlace(x, y);
        if(!Player.Instance.IsFacingRight) transform.localScale = new Vector3(-1, 1, 1);
    }

    private void Update()
    {
        if (!_isPlaced) return;
        if (_autoOpenEnabled)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.transform.position);

            if (!_isOpen && distanceToPlayer <= _autoOpenDistance)
            {
                ToggleDoor();
            }

            if (_isOpen)
            {
                if (distanceToPlayer > _autoOpenDistance)
                {
                    _closeTimer += Time.deltaTime;
                    if (_closeTimer >= _autoCloseDelay)
                    {
                        ToggleDoor();
                        _closeTimer = 0f;
                    }
                }
                else
                {
                    _closeTimer = 0f;
                }
            }
        }
    }

    public void ToggleDoor()
    {
        _isOpen = !_isOpen;

        if(_isOpen) AudioManager.Instance.Play(_openSound);
        else AudioManager.Instance.Play(_closeSound);

        SpriteRend.sprite = _isOpen ? _openSprite : _closedSprite;

        _doorCollider.enabled = !_isOpen;
    }

    public void Interact()
    {
        ToggleDoor();
    }
}
