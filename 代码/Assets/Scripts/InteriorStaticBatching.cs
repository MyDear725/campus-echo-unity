using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keep interaction objects/colliders; combine only the immutable visual geometry.
public sealed class InteriorStaticBatching : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        foreach(var root in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<InteriorSceneRoot>()))
            if((root.buildingId=="library"||root.buildingId=="cafeteria")&&!root.isPlaceholder&&!root.GetComponent<InteriorStaticBatching>())root.gameObject.AddComponent<InteriorStaticBatching>();
    }
    static bool Moves(Transform t,Transform root)
    {
        while(t&&t!=root)
        {string n=t.name.ToLowerInvariant();if(n.Contains("lift")||n.Contains("elevator")||n.Contains("hinge")||n.Contains("door")||n.Contains("cabin")||n.Contains("电梯")||n.Contains("门")||t.GetComponent<Animator>()||t.GetComponent<Animation>()||t.GetComponent<Rigidbody>())return true;t=t.parent;}return false;
    }
    IEnumerator Start()
    {
        // Let initial material/font setup and the entry placement finish first.
        yield return null;yield return null;
        var objects=GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh&&f.sharedMesh.isReadable&&f.gameObject.activeInHierarchy&&!f.GetComponent<TextMesh>()&&!f.GetComponent<SkinnedMeshRenderer>()&&!Moves(f.transform,transform))
            .Where(f=>{var r=f.GetComponent<MeshRenderer>();return r&&r.enabled&&!r.isPartOfStaticBatch;}).Select(f=>f.gameObject).ToArray();
        if(objects.Length<100)yield break;
        StaticBatchingUtility.Combine(objects,gameObject);
        Debug.Log("INTERIOR_STATIC_BATCHED "+GetComponent<InteriorSceneRoot>().buildingId+": "+objects.Length+" immutable renderers; interaction transforms and collisions retained.");
    }
}
