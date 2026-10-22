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
                n.Contains("Ceiling")||n.Contains("ceiling")?palette.ceiling:n=="Closed room door"||n=="Door inset"?palette.oak:
                n=="Room number plate"||n=="Door frame"||n=="Main exit door"?palette.teal:
                n.Contains("handle")?palette.steel:n.Contains("Skirting")||n.Contains("skirting")||n.Contains("rail")||n.Contains("barrier")?palette.graphite:
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
        {light.color=new Color(1,.94f,.84f);light.intensity=1.65f;light.range=8;light.transform.localPosition+=Vector3.down*.23f;}
        HallDetails(kit,layout);
        kit.CombineDecorations();
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
                n.Contains("desk")||n.Contains("drawer")||n.Contains("headboard")||n.Contains("door leaf")||n.Contains("chair back")||n.Contains("cabinet")?p.oak:
                n.Contains("faucet")||n.Contains("pull")?p.steel:n.Contains("countertop")?p.terrazzo:
                n.Contains("dado")?p.sage:n.Contains("Skirting")||n.Contains("skirting")?p.graphite:null;
            if(m&&!n.Contains("glass")&&!n.Contains("view"))renderer.sharedMaterial=m;
            if(n.Contains("sliding glass")&&p.glass)renderer.sharedMaterial=p.glass;
            if(n=="Balcony planter"){renderer.sharedMaterial=p.terrazzo;kit.Soften(renderer.gameObject,.035f);}
            if(n.Contains("desk top")||n.Contains("drawer")||n.Contains("pillow")||n.Contains("mattress")||n.Contains("quilt")||n.Contains("chair")||n.Contains("headboard")||n.Contains("Loft guard"))kit.Soften(renderer.gameObject,.025f);
            if(n=="Ceiling light"||n=="Study motto"||n.StartsWith("Desk lamp")||n=="Balcony flowers")
            {renderer.enabled=false;var collider=renderer.GetComponent<Collider>();if(collider)collider.enabled=false;}
        }
        foreach(var text in root.GetComponentsInChildren<TextMesh>())
        {if(text.name=="Study motto")text.gameObject.SetActive(false);else if(!text.GetComponent<InteriorWorldLabel>())text.gameObject.AddComponent<InteriorWorldLabel>();}
        for(int i=0;i<3;i++)
        {
            float z=2.7f+i*3;kit.Luminaire(new Vector3(0,3.12f,z),1.55f);
            var go=new GameObject("Dorm room diffuse ceiling light");go.transform.SetParent(kit.transform,false);go.transform.localPosition=new Vector3(0,2.94f,z);
            go.transform.localPosition+=Vector3.down*.22f;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.95f,.87f);light.intensity=1.8f;light.range=5.2f;light.shadows=LightShadows.None;
        }
        int index=0;
        foreach(float x in new[]{-1.8f,1.8f})foreach(float z in new[]{4.3f,7.55f})
        {
            kit.Box("Writing notebook",new Vector3(x,.91f,z-.24f),new Vector3(.43f,.022f,.31f),p.linen,.008f);
            kit.Box("Notebook cover",new Vector3(x,.925f,z-.24f),new Vector3(.43f,.008f,.31f),index++%2==0?p.teal:p.sage,.003f);
            for(int book=0;book<4;book++)kit.Box("Personal textbook",new Vector3(x-.46f+book*.09f,1.10f,z+.48f),new Vector3(.07f,.30f-book*.015f,.23f),book%2==0?p.teal:p.linen,.005f);
            kit.TaskLamp(new Vector3(x+.48f,.90f,z+.30f));
            kit.Box("Felt desk mat",new Vector3(x-.05f,.898f,z-.04f),new Vector3(.88f,.012f,.54f),index%2==0?p.teal:p.fabric,.015f);
            if(index%2==0)kit.Laptop(new Vector3(x-.10f,.91f,z+.14f),p.teal);
            kit.Box("Desk power strip",new Vector3(x,1.48f,z+.73f),new Vector3(.38f,.065f,.045f),p.linen,.008f);
            for(int socket=0;socket<3;socket++)kit.Box("Power socket",new Vector3(x-.11f+socket*.11f,1.48f,z+.703f),new Vector3(.025f,.028f,.008f),p.graphite,.003f);
            kit.Box("Storage shelf",new Vector3(x,1.64f,z+.62f),new Vector3(1.15f,.055f,.28f),p.oak);
            kit.Box("Pin board backing",new Vector3(x,1.33f,z+.77f),new Vector3(1.18f,.52f,.045f),p.sage);
            for(int note=0;note<3;note++)kit.Box("Pinned study note",new Vector3(x-.35f+note*.32f,1.34f,z+.741f),new Vector3(.20f,.22f,.006f),p.linen,.001f);
        }
        kit.Box("Entry oak ceiling trim",new Vector3(0,3.1f,.2f),new Vector3(5.9f,.08f,.35f),p.oak);
        // Real cupboards sit against the rear corners, outside the central aisle.
        foreach(float side in new[]{-1f,1f})
        {
            float x=side*2.54f;
            kit.Box("Personal wardrobe carcass",new Vector3(x,1.22f,9.38f),new Vector3(.88f,2.44f,.46f),p.oak,.025f,true);
            for(int door=0;door<2;door++)
            {
                float dx=x-.217f+door*.434f;
                kit.Box("Wardrobe sage door",new Vector3(dx,1.27f,9.137f),new Vector3(.419f,2.30f,.025f),p.sage,.012f);
                kit.Box("Wardrobe recessed handle",new Vector3(x+(door==0?-.055f:.055f),1.25f,9.119f),new Vector3(.018f,.24f,.018f),p.graphite,.006f);
            }
            kit.Box("Ceiling perimeter channel",new Vector3(side*2.99f,3.14f,5.2f),new Vector3(.07f,.065f,9.7f),p.oak,.006f);
            kit.Box("Ceiling perimeter diffuser",new Vector3(side*2.94f,3.10f,5.2f),new Vector3(.018f,.014f,9.6f),p.light,.003f);
        }
        foreach(float x in new[]{-1.8f,1.8f})foreach(float z in new[]{4.3f,7.55f})
        {
            // Contrasting stitched bands break up the flat quilt without adding noise.
            foreach(float dz in new[]{-.67f,.10f})kit.Box("Quilt woven border",new Vector3(x,2.261f,z+dz),new Vector3(1.23f,.006f,.08f),p.linen,.002f);
        }
        kit.Box("Mirror vertical LED left",new Vector3(1.43f,1.92f,1.98f),new Vector3(.018f,1.19f,.012f),p.light,.003f);
        kit.Box("Mirror vertical LED right",new Vector3(2.77f,1.92f,1.98f),new Vector3(.018f,1.19f,.012f),p.light,.003f);
        kit.Box("Soap dispenser",new Vector3(2.60f,1.075f,1.65f),new Vector3(.10f,.15f,.09f),p.plaster,.018f);
        kit.Box("Soap pump",new Vector3(2.60f,1.17f,1.65f),new Vector3(.065f,.04f,.065f),p.steel,.006f);
        kit.WallComposition(new Vector3(0,2.5f,-2.99f),Vector3.forward,p.sage,2.2f);
        kit.Plant(new Vector3(2.52f,0,11.0f),.75f);
        BedroomDetails(kit,root);
        kit.CombineDecorations();
    }

    static void HallDetails(InteriorDetailKit kit,DormitoryLayout layout)
    {
        var p=kit.palette;
        foreach(int side in new[]{-1,1})
        {
            float z=side*(layout.corridorWidth*.5f-.16f);
            // Quiet stone borders guide the long perspective without raised joints.
            kit.Box("Limestone floor border",new Vector3(0,.003f,side*1.52f),new Vector3(41.8f,.005f,.22f),p.terrazzo,.001f);
            kit.Box("Floor border inlay",new Vector3(0,.006f,side*1.39f),new Vector3(41.8f,.003f,.018f),p.brass,.001f);
            if(side>0)kit.Box("Sage dado fine cap",new Vector3(0,1.265f,z),new Vector3(41.8f,.025f,.045f),p.oak,.004f);
            else foreach(int wing in new[]{-1,1})
                kit.Box("Sage dado fine cap",new Vector3(wing*11.25f,1.265f,z),new Vector3(19.5f,.025f,.045f),p.oak,.004f);
            foreach(float x in new[]{-12f,0f,12f})
            {
                if(side<0&&x==0)continue; // Keep the real entry vestibule open.
                var panel=kit.Box("Resident notice oak frame",new Vector3(x,1.95f,z),new Vector3(1.45f,.88f,.055f),p.oak,.02f);
                panel.transform.localRotation=Quaternion.Euler(0,side>0?0:180,0);
                var board=InteriorDetailKit.Create(panel.transform,"Resident notices");board.palette=p;
                board.Box("Notice felt",new Vector3(0,0,-.037f),new Vector3(1.35f,.78f,.018f),p.sage,.01f);
                for(int i=0;i<3;i++)
                {
                    float nx=-.43f+i*.43f;
                    board.Box("Community notice paper",new Vector3(nx,0,-.051f),new Vector3(.32f,.50f,.006f),p.linen,.003f);
                    board.Box("Notice heading",new Vector3(nx,.15f,-.056f),new Vector3(.22f,.035f,.003f),i%2==0?p.teal:p.oak,.001f);
                    for(int line=0;line<4;line++)board.Box("Notice print",new Vector3(nx,.045f-line*.055f,-.056f),new Vector3(.22f-line*.018f,.008f,.003f),p.graphite,.001f);
                    board.Disc("Notice pin",new Vector3(nx,.25f,-.058f),.018f,.008f,p.brass);
                }
            }
        }
        foreach(int end in new[]{-1,1})
        {
            float x=end*23;
            kit.Luminaire(new Vector3(x,3.12f,0),1.5f);
            var go=new GameObject("Stairwell warm fill");go.transform.SetParent(kit.transform,false);go.transform.localPosition=new Vector3(x,2.6f,0);
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.94f,.84f);light.intensity=1.6f;light.range=6;light.shadows=LightShadows.None;
            // Subtle wall washers have emissive faces but no extra real-time lights.
            foreach(int direction in new[]{-1,1})
            {
                kit.Box("Stair end ceiling diffuser",new Vector3(x,3.13f,direction*2.9f),new Vector3(1.1f,.025f,.16f),p.light,.005f);
            }
        }
        kit.Box("Entry vestibule oak lintel",new Vector3(0,3.08f,-2.8f),new Vector3(2.95f,.12f,1.6f),p.oak,.008f);
        kit.Box("Entry stone inset",new Vector3(0,.004f,-2.3f),new Vector3(2.7f,.006f,1.2f),p.terrazzo,.001f);
    }

    static void BedroomDetails(InteriorDetailKit kit,Transform root)
    {
        var p=kit.palette;
        var blue=kit.Tint(p.fabric,new Color(.42f,.54f,.62f));
        var clay=kit.Tint(p.fabric,new Color(.68f,.45f,.33f));
        var sand=kit.Tint(p.fabric,new Color(.72f,.66f,.52f));
        Material[] accents={p.fabric,blue,clay,sand};int bed=0;
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            if(renderer.name=="Loft bed quilt")renderer.sharedMaterial=accents[bed++%4];
        bed=0;
        foreach(float x in new[]{-1.8f,1.8f})foreach(float z in new[]{4.3f,7.55f})
        {
            var accent=accents[bed++];
            kit.Box("Personal bed cushion",new Vector3(x+.36f,2.34f,z+.72f),new Vector3(.35f,.30f,.32f),accent,.065f);
            kit.Box("Folded linen throw",new Vector3(x,2.29f,z-.78f),new Vector3(1.27f,.06f,.30f),p.linen,.02f);
            kit.Box("Throw stitched stripe",new Vector3(x,2.322f,z-.78f),new Vector3(1.20f,.006f,.045f),accent,.002f);
            kit.Disc("Ceramic study mug",new Vector3(x+.47f,.906f,z+.05f),.055f,.12f,accent);
            kit.Disc("Tea surface",new Vector3(x+.47f,1.026f,z+.05f),.043f,.002f,p.walnut);
            kit.Beam("Mug handle",new Vector3(x+.53f,.94f,z+.05f),new Vector3(x+.56f,1.0f,z+.05f),.023f,accent);
            kit.Disc("Pencil cup",new Vector3(x-.65f,.91f,z+.10f),.045f,.11f,p.plaster);
            for(int pen=0;pen<3;pen++)kit.Beam("Study pencil",new Vector3(x-.67f+pen*.018f,1.0f,z+.10f),new Vector3(x-.67f+pen*.025f,1.16f,z+.12f),.009f,pen==0?p.teal:p.oak);
            // Small photographs and reminders stay within the original pin board.
            kit.Box("Personal postcard",new Vector3(x-.35f,1.34f,z+.735f),new Vector3(.16f,.16f,.005f),accent,.001f);
            kit.Box("Postcard photo mount",new Vector3(x-.35f,1.285f,z+.731f),new Vector3(.14f,.018f,.003f),p.linen,.001f);
            for(int line=0;line<3;line++)kit.Box("Study note handwriting",new Vector3(x+.29f,1.39f-line*.04f,z+.735f),new Vector3(.13f-line*.025f,.007f,.003f),p.graphite,.001f);
            kit.Box("Underbed oak storage",new Vector3(x,.29f,z+.66f),new Vector3(1.12f,.52f,.50f),p.oak,.025f);
            kit.Box("Storage basket front",new Vector3(x,.30f,z+.402f),new Vector3(1.02f,.40f,.014f),sand,.018f);
            kit.Box("Storage pull",new Vector3(x,.38f,z+.39f),new Vector3(.16f,.035f,.02f),p.graphite,.006f);
        }
        // Linen curtains frame the view while leaving the balcony passage open.
        foreach(int side in new[]{-1,1})
        {
            kit.Box("Curtain track",new Vector3(side*2.47f,3.04f,9.45f),new Vector3(.96f,.055f,.065f),p.steel,.009f);
            for(int fold=0;fold<7;fold++)
                kit.Box("Linen curtain fold",new Vector3(side*(2.17f+fold*.11f),1.88f,9.43f+(fold%2)*.055f),new Vector3(.145f,2.24f,.08f),fold%2==0?p.linen:sand,.035f);
            kit.Box("Curtain tie",new Vector3(side*2.49f,1.12f,9.36f),new Vector3(.68f,.06f,.045f),p.oak,.012f);
        }
        kit.Box("Entry woven mat",new Vector3(0,.008f,1.12f),new Vector3(1.35f,.014f,.70f),p.fabric,.03f);
        foreach(float z in new[]{.84f,1.4f})kit.Box("Mat woven border",new Vector3(0,.016f,z),new Vector3(1.20f,.003f,.035f),p.linen,.001f);
        kit.Box("Bedroom ceiling crosspiece",new Vector3(0,3.13f,6f),new Vector3(5.95f,.08f,.13f),p.oak,.01f);
        kit.Box("Bath towel rail",new Vector3(2.91f,1.48f,2.5f),new Vector3(.045f,.04f,.6f),p.steel,.007f);
        kit.Box("Folded bathroom towel",new Vector3(2.87f,1.27f,2.5f),new Vector3(.055f,.46f,.42f),p.linen,.022f);
        kit.Box("Balcony stone sill",new Vector3(0,.13f,11.02f),new Vector3(5.85f,.18f,.18f),p.terrazzo,.015f);
        kit.Box("Balcony planter soil",new Vector3(-2.25f,.60f,10.55f),new Vector3(.78f,.025f,.37f),p.soil,.01f);
        for(int stem=0;stem<5;stem++)
        {
            var at=new Vector3(-2.55f+stem*.15f,.61f,10.55f);
            var tip=at+new Vector3(.03f,.24f+(stem%2)*.12f,0);
            kit.Beam("Balcony herb stem",at,tip,.014f,p.leaf);
            foreach(int side in new[]{-1,1})
            {
                var leaf=kit.Box("Balcony herb leaf",tip+new Vector3(side*.06f,-.08f,0),new Vector3(.15f,.018f,.09f),p.leaf,.025f);
                leaf.transform.localRotation=Quaternion.Euler(15,side*25,side*20);
            }
        }
    }
}
