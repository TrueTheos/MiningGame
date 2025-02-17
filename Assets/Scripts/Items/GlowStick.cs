using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static UnityEditor.PlayerSettings;

public class GlowStick : ThrowableItem
{
    public LightSource Light;
    [SerializeField] private Light2D _light2D;

    private void Awake()
    {
        base.Awake();
        _light2D.gameObject.SetActive(false);
    }

    public override void OnThrow()
    {
        _light2D.gameObject.SetActive(true);
    }

    private Vector2Int _lastPos = Vector2Int.zero;
    
    private void Update()
    {
        if (!IsThrown) return;
        Vector2Int newPos = new(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y));

        if (_lastPos != newPos)
        {
            LightManager.Instance.AddLight(newPos.x, newPos.y, Light);
            LightManager.Instance.RemoveLight(_lastPos.x, _lastPos.y, Light.ID);
        }

        _lastPos = newPos;
       
    }

    private void OnDestroy()
    {
        
    }
}
