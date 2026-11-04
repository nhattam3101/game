using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Local, simple UGUI helpers. No TMP/font asset dependencies.
internal static class MS7J1HubUI
{
    internal static readonly Color Ink = new Color(.035f,.026f,.070f,1f);
    internal static readonly Color Card = new Color(.105f,.077f,.163f,1f);
    internal static readonly Color CardSelected = new Color(.19f,.13f,.30f,1f);
    internal static readonly Color Gold = new Color(.96f,.75f,.40f,1f);
    internal static readonly Color Pale = new Color(.91f,.87f,.96f,1f);
    internal static readonly Color Muted = new Color(.65f,.62f,.75f,1f);

    internal static RectTransform Rect(string name, Transform parent, Vector2 amin, Vector2 amax,
        Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var t = go.GetComponent<RectTransform>();
        // Place runtime UI inside its owning Scene so unloading PlayerHub
        // cannot leave orphan canvases behind in MainMenu.
        if(parent != null && parent.gameObject.scene.IsValid() && parent.gameObject.scene.isLoaded &&
           go.scene != parent.gameObject.scene)
            SceneManager.MoveGameObjectToScene(go,parent.gameObject.scene);
        t.SetParent(parent,false);
        t.anchorMin=amin; t.anchorMax=amax; t.pivot=pivot;
        t.anchoredPosition=pos; t.sizeDelta=size;
        return t;
    }
    internal static RectTransform Stretch(string name,Transform parent)
    {
        return Rect(name,parent,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
    }
    internal static Image Fill(GameObject go, Color c, bool raycast=true)
    {
        var img=go.AddComponent<Image>(); img.color=c; img.raycastTarget=raycast; return img;
    }
    internal static Text Label(string name,Transform parent,string value,int fontSize,TextAnchor alignment,Color color,
        Vector2 amin,Vector2 amax,Vector2 pivot,Vector2 size,Vector2 pos)
    {
        var r=Rect(name,parent,amin,amax,pivot,size,pos);
        var t=r.gameObject.AddComponent<Text>();
        t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text=value;t.color=color;t.fontSize=fontSize;t.alignment=alignment;
        t.resizeTextForBestFit=true;t.resizeTextMinSize=12;t.resizeTextMaxSize=fontSize;
        t.raycastTarget=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;
        t.verticalOverflow=VerticalWrapMode.Truncate;
        return t;
    }
    internal static Button Button(string name,Transform parent,string caption,Color fill,Color fg,
        Vector2 amin,Vector2 amax,Vector2 pivot,Vector2 size,Vector2 pos,UnityEngine.Events.UnityAction click)
    {
        var r=Rect(name,parent,amin,amax,pivot,size,pos);
        var img=Fill(r.gameObject,fill);
        var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=img;
        var colors=b.colors;
        colors.highlightedColor=Color.Lerp(fill,Color.white,.15f);
        colors.pressedColor=Color.Lerp(fill,Color.black,.2f);
        b.colors=colors;
        b.onClick.AddListener(click);
        Label("Caption",r,caption,25,TextAnchor.MiddleCenter,fg,
            new Vector2(.03f,.05f),new Vector2(.97f,.95f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
        return b;
    }
    internal static Canvas Canvas(string name,Transform parent,int sorting)
    {
        var go=new GameObject(name,typeof(RectTransform));
        go.layer=5;
        if(parent != null && parent.gameObject.scene.IsValid() && parent.gameObject.scene.isLoaded &&
           go.scene != parent.gameObject.scene)
            SceneManager.MoveGameObjectToScene(go,parent.gameObject.scene);
        go.transform.SetParent(parent,false);
        var c=go.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.overrideSorting=true;c.sortingOrder=sorting;
        var sc=go.AddComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution=new Vector2(1920,1080);sc.matchWidthOrHeight=.5f;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }
}
