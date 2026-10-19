using System.Collections.Generic;
using UnityEngine;

// North is -Z. Routes reproduce the plan's connections, fitted to existing landmark footprints.
public static class CampusPlanRoadNetwork
{
    static CampusRoad R(string name,float width,params float[] xy){
        var p=new MapPoint[xy.Length/2];for(int i=0;i<p.Length;i++)p[i]=new MapPoint{x=xy[i*2],z=xy[i*2+1]};
        // Round only lake-path corners, within a 0.65 m envelope of the established centreline.
        if(name.Contains("环湖")||name.Contains("方向步道")||name=="两湖直行步道"){
            var smooth=new List<MapPoint>();
            for(int i=0;i<p.Length;i++){
                if(i==0||i==p.Length-1){smooth.Add(p[i]);continue;}
                var a=new Vector2(p[i-1].x,p[i-1].z);var b=new Vector2(p[i].x,p[i].z);var c=new Vector2(p[i+1].x,p[i+1].z);
                float d=Mathf.Min(name.Contains("环湖")?.65f:3f,Mathf.Min(Vector2.Distance(a,b),Vector2.Distance(b,c))*.2f);
                var start=b+(a-b).normalized*d;var end=b+(c-b).normalized*d;
                for(int k=0;k<=3;k++){float t=k/3f;var q=(1-t)*(1-t)*start+2*t*(1-t)*b+t*t*end;smooth.Add(new MapPoint{x=q.x,z=q.y});}
            }
            p=smooth.ToArray();
        }
        return new CampusRoad{name=name,kind=width>=5.5f?"main":"secondary",width=width,points=p};
    }
    public static CampusRoad[] Create()=>new[]{
        R("教学区东侧主路",6,118,-147,118,-103,118,-40,118,-20,112,34,102,64,94,120,79,168,0,177,0,203),
        R("教学楼外围环路",6,118,-147,12,-147,12,-112,16,-84,28,-58,48,-37,68,-29,105,-29,118,-20),
        R("教学区西北主路",6,12,-147,-5,-147,-5,-112,-5,-75,-5,-62),
        R("运动场西侧园路",4,-176,-145,-178,-103,-185,-70,-193,-51,-214,-48,-214,0),
        R("体育馆至教学区主路",6,-214,0,-190,0,-158,0,-119,0,-85,0,-50,-3,-1,-3,35,-12,68,-29),
        R("西侧校内环路",6,-214,0,-190,12,-190,67,-176,70,-105,70,-77,81,-48,117,-33,161,0,177),
        R("锦屏山西侧主路",5.5f,-96,-150,-96,-95,-101,-52,-101,-20,-95,0,-85,0),
        R("田径场与体育馆连接路",4,-214,-48,-193,-51,-188,-49,-164,-49,-114,-49,-101,-52),
        R("综合楼东侧路",4,-119,0,-116,14,-109,26,-94,28,-88,45,-85,70,-77,81),
        R("综合楼门前步道",4,-176,70,-140,70,-105,70,-85,70,-39,66,0,67,22,73),
        R("图书馆与信息楼连接主路",5,-33,161,-23,128,-9,113,14,99,22,73),
        R("图书馆北侧路",4,22,73,22,61,49,61,75,57,102,64),
        R("图书馆南侧路",4,-23,128,25,123,44,123,86,123,94,120),
        R("书韵广场入口",4,14,99,30,99,42,92,45,92),
        R("天桥西侧接驳步道",7.2f,91.82f,10,35,10),
        R("两湖直行步道",4,35,10,24,10,14,6,5,-3,-15,-3),
        R("教学楼方向步道",4.6f,35,10,38,-2,46,-14,55,-23,68,-29),
        R("思源湖环湖步道",3,-50,-3,-52,-16,-50,-32,-44,-47,-36,-59,-26,-62,-17,-56,-5,-37,5,-14,5,-3,-15,-3,-50,-3),
        R("涌泉湖环湖步道",3,-50,-3,-63,-3,-75,10,-77,23,-68,34,-60,50,-49,60,-35,63,-22,62,-9,54,2,38,5,22,9,8,5,-3,-15,-3,-50,-3),
        R("湖畔至书韵广场",3.2f,-35,63,-6,62,22,73),
        R("图书馆正门方向步道",4.6f,35,10,30,27,24,43,22,55,22,73,30,85,44.6f,92),
        R("教学内庭南入口",3.5f,68,-29,74,-47,74,-66,74,-84,68,-99,58.3f,-99,58.3f,-103.8f),
        R("入口林荫步行带",3.2f,-4.8f,203,-4.8f,180,-37,163,-28,127,-12,109,14,99),
        R("体育馆运动带",2.1f,-188,5.4f,-119,5.4f,-113,0)
    };
    public static float Distance(float x,float z,CampusRoad road){float d=float.MaxValue;for(int i=1;i<road.points.Length;i++)d=Mathf.Min(d,CampusCollision.Segment(x,z,road.points[i-1],road.points[i]));return d;}
}
