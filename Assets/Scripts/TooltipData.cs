using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TooltipData
{
    [Serializable]
    public class TooltipLine
    {
        public string text;
        public int fontSize = 14;
        public Color32 textColor = new Color32(255, 255, 255, 255);
    }

    public List<TooltipLine> Lines = new List<TooltipLine>();

    public static TooltipData Create(params TooltipLine[] tooltipLines)
    {
        return new TooltipData { Lines = new List<TooltipLine>(tooltipLines) };
    }
}
