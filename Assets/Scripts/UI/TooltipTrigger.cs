using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ITooltip tooltippable;

    private void Start()
    {
        tooltippable = GetComponent<ITooltip>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltippable != null)
        {
            var data = tooltippable.GetTooltipData();
            if (data == null || data.Lines == null || data.Lines.Count == 0) return;
            Vector2 tooltipPosition = Input.mousePosition + new Vector3(50, -50, 0);

            TooltipManager.Instance.ShowTooltip(tooltippable.GetTooltipData());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Instance.HideTooltip();
    }

    public void OnDisable()
    {
        TooltipManager.Instance?.HideTooltip();
    }
}
