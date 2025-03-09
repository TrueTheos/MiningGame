using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PickupInfoUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Image _icon;
    [SerializeField] private float _destroyTime;
    [SerializeField] private float _increaseDelay;
    [SerializeField] private float _fadeDuration;

    private ItemAmount _item;
    public ItemAmount ItemAmount => _item;

    private int _amount;
    private float _destroyAtTime;
    private Coroutine _fadeCoroutine;

    public void Init(ItemAmount item)
    {
        _item = item;
        _amount = item.Amount;
        _text.text = $"{_amount}x {_item.Item.Name}";
        _icon.sprite = _item.Item.SpriteRend.sprite;

        _destroyAtTime = Time.time + _destroyTime;
        ScheduleDestroy();
    }

    public void Increase(int amount)
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
            RestoreAlpha();
        }

        _amount += amount;
        _text.text = $"{_amount}x {_item.Item.Name}";

        float remainingTime = _destroyAtTime - Time.time;
        _destroyAtTime = Time.time + remainingTime + _increaseDelay;
        ScheduleDestroy();
    }

    private void ScheduleDestroy()
    {
        float timeUntilDestroy = _destroyAtTime - Time.time;
        CancelInvoke(nameof(StartFadeOut));
        Invoke(nameof(StartFadeOut), timeUntilDestroy);
    }

    private void StartFadeOut()
    {
        _fadeCoroutine = StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        Color originalTextColor = _text.color;
        Color originalIconColor = _icon.color;
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeDuration);
            _text.color = new Color(originalTextColor.r, originalTextColor.g, originalTextColor.b, alpha);
            _icon.color = new Color(originalIconColor.r, originalIconColor.g, originalIconColor.b, alpha);
            yield return null;
        }

        _text.color = new Color(originalTextColor.r, originalTextColor.g, originalTextColor.b, 0f);
        _icon.color = new Color(originalIconColor.r, originalIconColor.g, originalIconColor.b, 0f);

        Destroy(gameObject);
    }

    private void RestoreAlpha()
    {
        Color textColor = _text.color;
        Color iconColor = _icon.color;
        _text.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
        _icon.color = new Color(iconColor.r, iconColor.g, iconColor.b, 1f);
    }
}