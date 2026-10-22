using UnityEngine;
using VRM;

// Native VRM expression presets retain the artist's eyelids instead of hiding eyes.
public sealed class VroidAvatarExpression : MonoBehaviour
{
    VRMBlendShapeProxy proxy;float nextBlink,blinkStarted=-10;
    void Awake(){proxy=GetComponentInChildren<VRMBlendShapeProxy>();nextBlink=Time.time+Random.Range(2f,4f);}
    void LateUpdate()
    {
        if(!proxy)return;
        if(Time.time>=nextBlink){blinkStarted=Time.time;nextBlink=Time.time+Random.Range(3f,5.5f);}
        float t=(Time.time-blinkStarted)/.16f;
        float weight=t>=0&&t<=1?Mathf.Sin(t*Mathf.PI):0;
        proxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink),weight);
    }
    void OnDisable(){if(proxy)proxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink),0);}
}
