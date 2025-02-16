using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightSourceCustomBuilding : CustomBuilding
{
    public LightSource Light;

    public override void OnPlace(int x, int y)
    {
        LightManager.Instance.AddLight(x, y, Light);
    }

    public override void OnBreak()
    {
        LightManager.Instance.RemoveLight(Pos.x, Pos.y);
        base.OnBreak();
    }
}
