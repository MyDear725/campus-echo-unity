using UnityEditor;
using UnityEngine;

// Reuse the sealed shell, physical stairs and bathroom details from C, while
// keeping each building's room arrangement and generated assets independent.
public static class TeachingABInteriorBuilder
{
    [MenuItem("Campus/Interiors/Generate Teaching A and B")]
    public static void Generate(){TeachingCInteriorBuilder.GenerateBuilding("A");TeachingCInteriorBuilder.GenerateBuilding("B");}
    public static void GenerateAndBuild(){TeachingBuilder.Generate();Generate();CampusBuilder.Build();}
    public static void RebuildInteriorsAndBuild(){Generate();CampusBuilder.Build();}
}

public static partial class TeachingCInteriorBuilder
{
    static string RoomType(float x,float start)=>Building=="A"?"阶梯教室":Building=="C"?(x<0?"多媒体教室":"计算机机房"):(x<0?(start==2?"普通教室":"多媒体教室"):(start==2?"语音教室":"研讨教室"));
    static string DirectoryText(){
        string text=Building+"楼  /  DIRECTORY\n";
        for(int f=5;f>=1;f--)text+="\n"+f+"F   "+Building+f+"01–"+f+(Building=="A"?"02":"04");
        return text+"\n\n后侧楼梯通往各层\n每层设男女卫生间";
    }
    static void TeachingFront(float cx,float front,string title,bool projection){
        B("Metal",cx,y+2.1f,front+.17f,9.4f,1.75f,.12f);B("Board",cx,y+2.1f,front+.25f,9.2f,1.6f,.035f);
        if(projection){B("Projection",cx,y+2.1f,front+.29f,4.4f,1.62f,.025f);Sign(title,V(cx,y+2.13f,front+.32f),Vector3.forward,.18f,Ink);}
        else Sign(title+"\n知行合一   学无止境",V(cx,y+2.13f,front+.29f),Vector3.forward,.2f,White);
        SoftBox("Wood",V(cx-3,y+.54f,front+1.05f),V(1.45f,1.08f,.6f),.025f,"Lectern");
        SoftBox("Oak pale",V(cx-3,y+1.12f,front+1.05f),V(1.55f,.1f,.7f),.025f);
        B("Metal",cx,y+3.28f,front+.15f,1,.32f,.08f);Sign("10:24",V(cx,y+3.28f,front+.2f),Vector3.forward,.15f,new Color(.9f,.25f,.13f));
        if(projection){B("Metal",cx,y+3.6f,front+4,.05f,.4f,.05f);SoftBox("Ceramic",V(cx,y+3.3f,front+4),V(.55f,.18f,.38f),.03f);B("Screen",cx,y+3.3f,front+3.8f,.12f,.1f,.03f);}
        foreach(float dx in new[]{-5.8f,5.8f})B("Metal",cx+dx,y+3.1f,front+.23f,.28f,.55f,.25f);
    }
    static void LectureRooms(){
        float baseY=y;
        foreach(float side in new[]{-1f,1f}){
            float cx=side*12;TeachingFront(cx,2,"阶梯教室  /  LECTURE THEATRE",true);
            // Five 180 mm risers. Each tier is a solid deck with a broad side
            // aisle; the character climbs actual colliders, including in sprint.
            for(int row=0;row<5;row++){
                float start=6.4f+row*1.8f,end=row==4?17.09f:start+1.8f,top=(row+1)*.18f;
                B("Dark tile",cx,baseY+top/2,(start+end)/2,15.7f,top,end-start,"Lecture tier "+row);
                B("Steel",cx,baseY+top+.004f,start+.025f,15.65f,.008f,.05f);
                y=baseY+top;for(int col=0;col<6;col++)Desk(cx-5+col*2,7+row*1.8f,false);y=baseY;
                foreach(float dx in new[]{-7f,7f}){B("Light",cx+dx,baseY+top*.5f,start-.006f,.24f,.04f,.012f);}
            }
            // Acoustic wood blades sit against the sealed rear wall.
            for(float x=cx-7.4f;x<cx+7.5f;x+=.28f)B("Wood",x,y+2.52f,17.03f,.075f,2.35f,.09f);
            Sign("LECTURE  /  "+Building+(floor+1)+(side<0?"01":"02"),V(cx,y+3.35f,16.95f),Vector3.back,.16f,White);
            foreach(float z in new[]{3.2f,6.7f,10.2f,13.7f})B("Curtain",side*19.7f,y+2.3f,z,.15f,2.5f,.5f);
        }
        y=baseY;
    }
    static void Headset(float x,float z){
        // Desktop language station: padded ear cups, a curved head band and mic.
        Vector3 c=V(x+.48f,y+1.02f,z+.03f);
        SoftBox("Metal",c+V(-.12f,-.07f,0),V(.07f,.14f,.1f),.025f);SoftBox("Metal",c+V(.12f,-.07f,0),V(.07f,.14f,.1f),.025f);
        for(int i=0;i<10;i++){float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;Tube("Metal",c+V(Mathf.Cos(a)*.12f,Mathf.Sin(a)*.16f-.03f,0),c+V(Mathf.Cos(b)*.12f,Mathf.Sin(b)*.16f-.03f,0),.012f,8);}
        Tube("Metal",c+V(.12f,-.12f,0),c+V(.12f,-.14f,.15f),.009f,8);
    }
    static void MixedRooms(){
        foreach(float side in new[]{-1f,1f})foreach(float start in new[]{2f,10f}){
            float cx=side*12,front=start==2?2:9.5f;bool language=side>0&&start==2,seminar=side>0&&start==10,multimedia=side<0&&start==10;
            TeachingFront(cx,front,RoomType(side,start),language||multimedia||seminar);
            if(seminar){
                // Two small group tables leave the doorway side and centre clear.
                foreach(float gx in new[]{cx-3.3f,cx+3.3f}){
                    SoftBox("Oak pale",V(gx,y+.77f,13.7f),V(3.2f,.09f,1.4f),.08f,"Seminar table");
                    foreach(float dx in new[]{-1.2f,1.2f})Tube("Metal",V(gx+dx,y,13.7f),V(gx+dx,y+.73f,13.7f),.045f);
                    foreach(float dx in new[]{-.9f,.9f})foreach(float dz in new[]{-1.25f,1.25f})StudyChair(gx+dx,13.7f+dz,dz>0?1:-1);
                    B("Ceramic",gx,y+.83f,13.7f,.55f,.018f,.38f);
                }
                SoftBox("Metal",V(cx,y+2.1f,17.04f),V(4.2f,1.6f,.08f),.025f);
                B("Ceramic",cx,y+2.1f,16.99f,4.05f,1.45f,.025f);Sign("讨论 · 协作 · 分享",V(cx,y+2.15f,16.97f),Vector3.back,.18f,Ink);
            }else for(int row=0;row<3;row++)for(int col=0;col<5;col++){
                float x=cx-4.8f+col*2.4f,z=start+2.1f+row*1.43f;Desk(x,z,language);
                if(language){Headset(x,z);SoftBox("Seat",V(x-.86f,y+1.02f,z),V(.035f,.45f,.74f),.012f);}
            }
            foreach(float z in new[]{start+.6f,start+3.4f,start+6.2f})B("Curtain",side*19.7f,y+2.2f,z,.15f,2.3f,.4f);
        }
    }
    static void StudyChair(float x,float z,float facing){
        SoftBox("Seat",V(x,y+.46f,z),V(.55f,.075f,.53f),.03f,"Study chair");SoftBox("Seat",V(x,y+.85f,z+facing*.24f),V(.55f,.45f,.07f),.025f);
        foreach(float dx in new[]{-.22f,.22f})foreach(float dz in new[]{-.2f,.2f})Tube("Metal",V(x+dx,y+.025f,z+dz),V(x+dx,y+.44f,z+dz),.018f);
    }
    static void StudyCommons(){
        // B uses a connected central commons on EVERY floor, rather than C's
        // five-storey void. Keep a 4 m unobstructed central route to the stairs.
        B("Ceiling",0,y+3.87f,-4,14.1f,.12f,12.2f,"Study ceiling");
        foreach(float side in new[]{-1f,1f}){
            for(int row=0;row<2;row++)foreach(float dx in new[]{3.1f,5.2f}){
                float x=side*dx,z=-5.8f+row*3.2f;Desk(x,z,false);
                Tube("Metal",V(x-.58f,y+.8f,z-.18f),V(x-.58f,y+1.25f,z-.18f),.015f);
                B("Light",x-.36f,y+1.25f,z-.18f,.48f,.035f,.08f);
                SoftBox("Seat",V(x,y+1.03f,z-.32f),V(1.55f,.45f,.035f),.01f);
            }
            float sx=side*6.7f;
            B("Wood",sx,y+1.15f,-4,.3f,2.3f,6.5f,"Study bookshelf");
            for(int shelf=0;shelf<4;shelf++){
                B("Oak pale",sx-side*.16f,y+.25f+shelf*.51f,-4,.5f,.055f,6.5f);
                for(int book=0;book<28;book++)B(book%3==0?"Seat":book%3==1?"Wood":"Ceramic",sx-side*.29f,y+.45f+shelf*.51f,-7+book*.22f,.3f,.32f+(book%3)*.025f,.08f);
            }
            Plant(V(side*6.5f,y,.9f),1.4f);
            for(float z=-8;z<2;z+=2.5f){B("Wood",side*3.4f,y+3.77f,z,5.5f,.09f,.14f);B("Light",side*3.4f,y+3.71f,z,3,.025f,.08f);}
            FillLight(V(side*3.6f,y+3.55f,-3),1.8f,10);
        }
        B("Metal",0,y+3.2f,1.8f,4.4f,.65f,.1f);Sign("B楼 · "+(floor+1)+"F  自习区 / STUDY",V(0,y+3.2f,1.72f),Vector3.back,.19f,White);
        Sign("请保持安静   /   QUIET PLEASE",V(0,y+3.16f,1.88f),Vector3.forward,.15f,White);
        foreach(float x in new[]{-1.9f,1.9f})B("Dark tile",x,y+.004f,-4,.09f,.008f,12);
        if(floor==0){
            B("Wood",-10,2,-9,3.3f,3.6f,.18f,"B directory wall");Sign("B 教学楼\n\n01 普通教室\n02 多媒体教室\n03 语音教室\n04 研讨教室\n\n中部自习区 · 后侧楼梯",V(-10,2.1f,-9.11f),Vector3.back,.21f,Ink);
            Sign("出口  EXIT",V(0,3.25f,-11.72f),Vector3.forward,.24f,new Color(.12f,.37f,.23f));
            foreach(float x in new[]{-1.5f,1.5f})B("Metal",x,1.45f,-11.82f,.07f,2.9f,.12f);
        }
    }
}
