using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Torch : CustomBuilding
{
    public float LightIntensity;

    public override void Place(int x, int y)
    {
        base.Place(x, y);
        LightManager.Instance.AddLight(x, y, LightIntensity);
    }

    public override void OnBreak()
    {
        LightManager.Instance.RemoveLight(Pos.x, Pos.y);
        base.OnBreak();
    }
}
