using UnityEngine;

public static class DormitoryInteriorRefinement
{
    public static void Hall(Transform root,DormitoryLayout layout)
    {
        var palette=InteriorRefinementPalette.Load();if(!palette)return;
        var kit=InteriorDetailKit.Create(root,"Dormitory · architectural finish");
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(renderer.GetComponent<TextMesh>())continue;
            string n=renderer.name;
            Material m=n=="Corridor floor"||n.Contains("Stair tread")||n.Contains("landing")?palette.floor:
                n.Contains("Ceiling")?palette.ceiling:n=="Closed room door"||n=="Door inset"?palette.oak:
                n=="Room number plate"||n=="Door frame"||n=="Main exit door"?palette.teal:
                n.Contains("handle")?palette.steel:n.Contains("Skirting")||n.Contains("rail")||n.Contains("barrier")?palette.graphite:
                n.Contains("dado")||n.Contains("Dado")?palette.sage:n.Contains("wall")||n.Contains("jamb")?palette.plaster:null;
            if(m)renderer.sharedMaterial=m;
            if(n=="Closed room door"||n=="Door inset"||n=="Room number plate"||n=="Door handle")kit.Soften(renderer.gameObject,.012f);
            if(n=="Tile joint"||n=="Hallway motto"||n=="Ceiling luminaire")renderer.enabled=false;
        }
        foreach(var text in root.GetComponentsInChildren<TextMesh>(true))
        {if(text.name=="Hallway motto")text.gameObject.SetActive(false);else if(!text.GetComponent<InteriorWorldLabel>())text.gameObject.AddComponent<InteriorWorldLabel>();}
        for(int side=-1;side<=1;side+=2)
        {
            float z=side*(layout.corridorWidth/2-.15f);
            kit.Box("Door-side skirting",new Vector3(side*11.1f,.08f,z),new Vector3(19.2f,.16f,.045f),palette.graphite);
            // Continuous coves sit above the door heads, away from the walking capsule.
            kit.Box("Warm ceiling cove",new Vector3(0,3.10f,z),new Vector3(41,.025f,.055f),palette.light,.005f);
            for(int i=0;i<layout.roomsPerSide;i++)
            {
                float x=layout.firstRoomX+i*layout.roomPitch;
                kit.Box("Door kick plate",new Vector3(x,.19f,z-side*.025f),new Vector3(.96f,.20f,.018f),palette.steel,.004f);
                kit.Box("Room entry reveal",new Vector3(x+1.25f,1.67f,z),new Vector3(.07f,2.6f,.035f),palette.oak);
            }
        }
        for(int i=-3;i<=3;i++)kit.Luminaire(new Vector3(i*6,3.10f,0),2.4f);
        kit.Plant(new Vector3(-1.13f,0,-3.1f),.7f);kit.Plant(new Vector3(1.13f,0,-3.1f),.7f);
        kit.Sign("憩苑生活区","DORMITORY  /  安静 · 整洁 · 友善",new Vector3(-3,2.30f,-1.75f),Vector3.forward,3,.62f);
        foreach(var light in root.GetComponentsInChildren<Light>())
        {light.color=new Color(1,.96f,.88f);light.intensity=2.3f;light.range=7;}
    }
    public static void Bedroom(Transform root)
    {
        var p=InteriorRefinementPalette.Load();if(!p)return;
        var kit=InteriorDetailKit.Create(root,"Dorm room · warm oak and personal details");
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(renderer.GetComponent<TextMesh>())continue;
            string n=renderer.name;
            Material m=n.Contains("floor")||n.Contains("landing")?p.floor:n.Contains("plaster")||n.Contains("wall")||n.Contains("lintel")?p.plaster:
                n.Contains("ceiling")?p.ceiling:n.Contains("quilt")||n.Contains("chair seat")?p.fabric:
                n.Contains("mattress")||n.Contains("pillow")?p.linen:n.Contains("frame")||n.Contains("rail")||n.Contains("leg")||n.Contains("post")?p.graphite:
                n.Contains("desk")||n.Contains("drawer")||n.Contains("headboard")||n.Contains("door leaf")||n.Contains("chair back")?p.oak:
                n.Contains("dado")?p.sage:n.Contains("Skirting")||n.Contains("skirting")?p.graphite:null;
            if(m&&!n.Contains("glass")&&!n.Contains("view"))renderer.sharedMaterial=m;
            if(n.Contains("sliding glass")&&p.glass)renderer.sharedMaterial=p.glass;
            if(n.Contains("desk top")||n.Contains("drawer")||n.Contains("pillow")||n.Contains("mattress")||n.Contains("quilt")||n.Contains("chair")||n.Contains("headboard")||n.Contains("Loft guard"))kit.Soften(renderer.gameObject,.025f);
            if(n=="Ceiling light"||n=="Study motto")renderer.enabled=false;
        }
        foreach(var text in root.GetComponentsInChildren<TextMesh>())
        {if(text.name=="Study motto")text.gameObject.SetActive(false);else if(!text.GetComponent<InteriorWorldLabel>())text.gameObject.AddComponent<InteriorWorldLabel>();}
        for(int i=0;i<3;i++)
        {
            float z=2.7f+i*3;kit.Luminaire(new Vector3(0,3.12f,z),1.55f);
            var go=new GameObject("Dorm room diffuse ceiling light");go.transform.SetParent(kit.transform,false);go.transform.localPosition=new Vector3(0,2.94f,z);
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.96f,.89f);light.intensity=2.4f;light.range=4.7f;light.shadows=LightShadows.None;
        }
        int index=0;
        foreach(float x in new[]{-1.8f,1.8f})foreach(float z in new[]{4.3f,7.55f})
        {
            kit.Box("Writing notebook",new Vector3(x,.91f,z-.24f),new Vector3(.43f,.022f,.31f),p.linen,.008f);
            kit.Box("Notebook cover",new Vector3(x,.925f,z-.24f),new Vector3(.43f,.008f,.31f),index++%2==0?p.teal:p.sage,.003f);
            for(int book=0;book<4;book++)kit.Box("Personal textbook",new Vector3(x-.46f+book*.09f,1.10f,z+.48f),new Vector3(.07f,.30f-book*.015f,.23f),book%2==0?p.teal:p.linen,.005f);
            kit.Box("Desk task light",new Vector3(x+.48f,1.24f,z-.34f),new Vector3(.30f,.018f,.08f),p.light,.007f);
            kit.Box("Storage shelf",new Vector3(x,1.64f,z+.62f),new Vector3(1.15f,.055f,.28f),p.oak);
            kit.Box("Pin board backing",new Vector3(x,1.33f,z+.77f),new Vector3(1.18f,.52f,.045f),p.sage);
            for(int note=0;note<3;note++)kit.Box("Pinned study note",new Vector3(x-.35f+note*.32f,1.34f,z+.741f),new Vector3(.20f,.22f,.006f),p.linen,.001f);
        }
        kit.Box("Entry oak ceiling trim",new Vector3(0,3.1f,.2f),new Vector3(5.9f,.08f,.35f),p.oak);
        kit.Plant(new Vector3(2.52f,0,9.08f),.75f);
    }
}
