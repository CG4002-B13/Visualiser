using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(Image))]

public class UIRectObject : MonoBehaviour
{
    private RectTransform _rectangleRectTransform;
    private Image _rectangleImage;
    private TMP_Text _Text;
    // Start is called before the first frame update

    public void Awake()
    {
        _rectangleRectTransform = GetComponent<RectTransform>();
        _rectangleImage = GetComponent<Image>();
        _Text = GetComponentInChildren<TMP_Text>();
    }

    public void SetRectTransform(Rect rect)
    {

        _rectangleRectTransform.anchoredPosition = new Vector2(rect.x, rect.y);
        _rectangleRectTransform.sizeDelta = new Vector2(rect.width, rect.height);
    }

    public void SetColor(Color color)
    {
        _rectangleImage.color = color;
    }

    public void SetText(string text)
    {
        _Text.text = text;
    }

    public RectTransform GetRectTransform()
    {
        return _rectangleRectTransform;
    }
}
