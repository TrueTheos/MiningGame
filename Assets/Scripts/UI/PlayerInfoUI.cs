using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerInfoUI : MonoBehaviour
{
    private Player _player;

    [SerializeField] private TextMeshProUGUI _health;
    [SerializeField] private TextMeshProUGUI _depth;
    [SerializeField] private TextMeshProUGUI _temperature;

    private void Start()
    {
        _player = Player.Instance;
    }

    private void Update()
    {
        if (!WorldManager.Instance.Ready) return;
        Color healthColor;
        if (_player.CurrentHealth < _player.MaxHealth / 4)
        {
            healthColor = Color.red;
        }
        else if (_player.CurrentHealth < _player.MaxHealth / 2)
        {
            healthColor = Color.yellow;
        }
        else
        {
            healthColor = Color.green;
        }

        string hexColor = ColorUtility.ToHtmlStringRGB(healthColor);
        _health.text = $"Health:\n<color=#{hexColor}>{_player.CurrentHealth}/{_player.MaxHealth}</color>";

        _depth.text = $"Depth:\n{_player.Depth}";

        Color tempColor;
        if (_player.Temperature < _player.CurrentWarningTemperature)
        {
            tempColor = Color.green;
        }
        else if (_player.Temperature < _player.CurrentDangerousTemperature)
        {
            tempColor = Color.yellow;
        }
        else
        {
            tempColor = Color.red;
        }

        string tempHex = ColorUtility.ToHtmlStringRGB(tempColor);
        _temperature.text = $"Temp:\n<color=#{tempHex}>{_player.Temperature}C</color>";
    }
}
