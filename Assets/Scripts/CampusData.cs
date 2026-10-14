using System;
using UnityEngine;

[Serializable] public class MapPoint { public float x,z; }
[Serializable] public class CampusObstacle { public string name; public MapPoint[] points,segment; public float minX,maxX,minZ,maxZ,thickness; }
[Serializable] public class CampusFeature { public int id; public string name,kind; public MapPoint[] points; }
[Serializable] public class CampusRoad { public string name,kind; public float width; public MapPoint[] points; }
[Serializable] public class CampusRoute { public string name; public MapPoint[] points; }
[Serializable] public class CampusSpot { public string id,area; public float x,z; }
[Serializable] public class CampusData { public CampusObstacle[] obstacles; public CampusFeature[] features; public CampusRoad[] roads; public CampusSpot[] spots; public CampusRoute[] routes; }

public static class CampusCollision {
    public static float Segment(float x,float z,MapPoint a,MapPoint b) {
        float dx=b.x-a.x,dz=b.z-a.z,t=Mathf.Clamp01(((x-a.x)*dx+(z-a.z)*dz)/Mathf.Max(.00001f,dx*dx+dz*dz));
        return Vector2.Distance(new Vector2(x,z),new Vector2(a.x+dx*t,a.z+dz*t));
    }
    public static bool Inside(float x,float z,MapPoint[] p) {
        bool hit=false; for(int i=0,j=p.Length-1;i<p.Length;j=i++)
            if((p[i].z>z)!=(p[j].z>z)&&x<(p[j].x-p[i].x)*(z-p[i].z)/(p[j].z-p[i].z)+p[i].x)hit=!hit;
        return hit;
    }
    public static bool Blocked(float x,float z,CampusObstacle[] obstacles,float radius=.43f) {
        if(x< -290+radius||x>310-radius||z< -180+radius||z>235-radius)return true;
        foreach(var o in obstacles) {
            if(o.segment!=null&&o.segment.Length==2){if(Segment(x,z,o.segment[0],o.segment[1])<radius+o.thickness*.5f)return true;}
            else if(o.points!=null&&o.points.Length>2){if(Inside(x,z,o.points))return true;for(int i=0;i<o.points.Length;i++)if(Segment(x,z,o.points[i],o.points[(i+1)%o.points.Length])<radius)return true;}
            else {float dx=x-Mathf.Clamp(x,o.minX,o.maxX),dz=z-Mathf.Clamp(z,o.minZ,o.maxZ);if(dx*dx+dz*dz<radius*radius)return true;}
        } return false;
    }
    public static Vector3 Move(Vector3 p,Vector3 delta,CampusObstacle[] obstacles) {
        int steps=Mathf.Max(1,Mathf.CeilToInt(new Vector2(delta.x,delta.z).magnitude/.2f));
        for(int i=0;i<steps;i++){if(!Blocked(p.x+delta.x/steps,p.z,obstacles))p.x+=delta.x/steps;if(!Blocked(p.x,p.z+delta.z/steps,obstacles))p.z+=delta.z/steps;} return p;
    }
}
