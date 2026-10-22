using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class CampusPlanBuilder
{
    static void BuildBridgeArrivalGarden(){
        // North is -Z. Looking west from the bridge: library left, lakes ahead, teaching right.
        // Reclaim obsolete zigzag paths as lawn, with the new surfaces cut into it at one height.
        for(float x=23;x<96;x+=2)for(float z=-6;z<30;z+=2)
            GroundPolygon("Grass",0,V(x,0,z),V(x+2,0,z),V(x+2,0,z+2),V(x,0,z+2));
        // Entire junction is walkable; the compass detail is a flush stone inlay, not an obstacle.
        const float cx=35,cz=10,r=7.1f;
        for(int i=0;i<72;i++){
            float a=i*Mathf.PI/36,b=(i+1)*Mathf.PI/36;
            Vector3 Q(float angle,float radius)=>V(cx+Mathf.Cos(angle)*radius,0,cz+Mathf.Sin(angle)*radius);
            GroundPolygon("Promenade",36,V(cx,0,cz),Q(a,r),Q(b,r));
            var mid=Q((a+b)/2,r);
            bool opening=data.roads.Any(road=>road.name=="天桥西侧接驳步道"||road.name.Contains("方向步道")||road.name=="两湖直行步道"
                ?CampusPlanRoadNetwork.Distance(mid.x,mid.z,road)<road.width/2+.3f:false);
            if(!opening){
                GroundPolygon("PaleStone",55,Q(a,r-.22f),Q(a,r),Q(b,r),Q(b,r-.22f));
                GroundPolygon("DarkBand",56,Q(a,r-.29f),Q(a,r-.22f),Q(b,r-.22f),Q(b,r-.29f));
            }
            GroundPolygon("BorderStone",58,Q(a,.78f),Q(a,.9f),Q(b,.9f),Q(b,.78f));
        }
        for(int i=0;i<4;i++){
            float a=i*Mathf.PI/2;Vector3 Q(float angle,float radius)=>V(cx+Mathf.Cos(angle)*radius,0,cz+Mathf.Sin(angle)*radius);
            GroundPolygon("PaleStone",59,V(cx,0,cz),Q(a-.2f,.22f),Q(a,.68f),Q(a+.2f,.22f));
        }
        // Calm axial paving rhythm: sparse cross bands, never road-paint stripes on pedestrian space.
        foreach(float x in new[]{48f,60f,72f,84f}){
            GroundPolygon("PaleStone",57,V(x-.10f,0,6.9f),V(x+.10f,0,6.9f),V(x+.10f,0,13.1f),V(x-.10f,0,13.1f));
            foreach(float side in new[]{-1f,1f}){
                float z=10+side*7.2f;
                if(FreeScenery(x,z,1.25f)){SceneryTree(x,z,1.02f);SceneryBed(x,z,2.1f,2.1f);}
                float lx=x+4,zl=10+side*4.65f;
                if(FreeScenery(lx,zl,.25f))SceneryLamp(lx,zl);
                foreach(float xx in new[]{x+3.5f,x+4.8f,x+6.1f}){
                    float zs=10+side*5.65f;if(FreeScenery(xx,zs,.55f)){Shrubs(xx,zs,.55f);OrnamentalGrass(xx+.45f,zs+side*.45f);}
                }
            }
        }
        foreach(float x in new[]{54f,78f})foreach(float side in new[]{-1f,1f}){
            float z=10+side*7.2f;if(!FreeScenery(x,z,1.4f))continue;
            GroundRect("Paving",x-1.8f,x+1.8f,z-1.1f,z+1.1f);
            SceneryBench(x,z,side>0?0:180);
        }
        DressRoute("图书馆正门方向步道",14,true);
        DressRoute("教学楼方向步道",14,true);
        DressRoute("两湖直行步道",13,true);
        // Grounded, human-scale direction board facing arriving pedestrians.
        const float sx=42,sz=21.5f;float y=H(sx,sz);
        GroundRect("Paving",sx-1.3f,sx+1.3f,sz-1.8f,sz+1.8f);
        foreach(float dz in new[]{-.9f,.9f})Box("Metal",V(sx,y+1.2f,sz+dz),V(.09f,2.4f,.09f),true);
        Box("SignBoard",V(sx,y+1.65f,sz),V(.16f,1.35f,2.6f),true);
        Box("Wood",V(sx,y+2.36f,sz),V(.19f,.10f,2.7f));
        Label("← 图书馆正门\n↑ 思源湖 · 涌泉湖\n→ ABC 教学楼",V(sx+.095f,y+1.65f,sz),.31f,270);
        Directory.CreateDirectory("QA");File.WriteAllText("QA/bridge-arrival-layout.txt",
            "Bridge west descent (91.82,10), axial promenade to (35,10): 56.82 m long, 7.2 m wide.\nFacing the campus: left/south -> library main entrance (44.6,92); ahead/west -> shared path between the lakes; right/north -> ABC teaching courtyard (68,-29).\nBuilding footprints, lake outlines, raised bridge deck, straight flights and north-low dormitory LG retained. Reconstructed layout follows supplied plan relationships; no survey-scale claim.\nWarm grey stone, narrow inset edging, flush junction, regular tree rows, side seating, low planting and grounded directions.\n");
    }
}
