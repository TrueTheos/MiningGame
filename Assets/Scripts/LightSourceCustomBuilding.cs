using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightSourceCustomBuilding : CustomBuilding
{
    public LightSource Light;
    [SerializeField] private Light2D _light2D;

    private void Awake()
    {
        _light2D.gameObject.SetActive(false);
    }

    public override void OnPlace(int x, int y)
    {
        _light2D.gameObject.SetActive(true);
        LightManager.Instance.AddLight(x, y, Light);
    }

    public override void OnBreak()
    {
        _light2D.gameObject.SetActive(false);
        LightManager.Instance.RemoveLight(Pos.x, Pos.y, Light.ID);
        base.OnBreak();
    }
}
