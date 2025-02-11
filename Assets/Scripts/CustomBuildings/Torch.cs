using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Torch : CustomBuilding
{
    public LightSource source;

    public override void OnPlace(int x, int y)
    {
        LightManager.Instance.AddLight(x, y, source);
    }

    public override void OnBreak()
    {
        LightManager.Instance.RemoveLight(Pos.x, Pos.y);
        base.OnBreak();
    }
}
