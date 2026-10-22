using UnityEngine;

public static partial class GymnasiumInteriorBuilder
{
    static void Surface(string mat,float x,float z,float w,float d)=>B(mat,x,y+(mat=="Navy"||mat=="Green"||mat=="Wood"?.045f:.014f),z,w,.026f,d);
    static void Line(float x1,float z1,float x2,float z2,string mat="White",float width=.085f,float elevation=.09f){
        var a=V(x1,y+elevation,z1);var b=V(x2,y+elevation,z2);var axis=(b-a).normalized;var side=Vector3.Cross(Vector3.up,axis)*width*.5f;
        meshes[mat].Quad(a-side,b-side,b+side,a+side);
    }
    static void Rect(float x,float z,float w,float d,string mat="White"){
        Line(x-w/2,z-d/2,x+w/2,z-d/2,mat);Line(x+w/2,z-d/2,x+w/2,z+d/2,mat);
        Line(x+w/2,z+d/2,x-w/2,z+d/2,mat);Line(x-w/2,z+d/2,x-w/2,z-d/2,mat);
    }
    static void Arc(float x,float z,float radius,float start,float end,string mat="White"){
        for(int i=0;i<64;i++){float a=Mathf.Lerp(start,end,i/64f)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(i+1)/64f)*Mathf.Deg2Rad;Line(x+radius*Mathf.Cos(a),z+radius*Mathf.Sin(a),x+radius*Mathf.Cos(b),z+radius*Mathf.Sin(b),mat);}
    }
    static void Ball(string mat,Vector3 p,float r){
        for(int j=0;j<8;j++)for(int i=0;i<16;i++){
            Vector3 P(int u,int v){float a=u*Mathf.PI/8,b=-Mathf.PI/2+v*Mathf.PI/8;return p+V(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(b),Mathf.Sin(a)*Mathf.Cos(b))*r;}
            meshes[mat].Quad(P(i,j),P(i+1,j),P(i+1,j+1),P(i,j+1));
        }
    }
    static void Net(float x,float z,float width,float top,float drop,string post="Navy"){
        foreach(float dx in new[]{-width/2-.35f,width/2+.35f}){
            Tube("Steel",V(x+dx,y,z),V(x+dx,y+top+.12f,z),.047f);
            SoftBox(post,V(x+dx,y+.6f,z),V(.22f,1.2f,.24f),.045f,"Net padded post");
            B("Metal",x+dx,y+.045f,z,.58f,.09f,.65f);
        }
        B("White",x,y+top,z,width,.055f,.035f);B("White",x,y+top-drop,z,width,.035f,.025f);
        for(float dx=-width/2;dx<=width/2;dx+=.14f)B("Metal",x+dx,y+top-drop/2,z,.012f,drop,.012f);
        for(float h=top-drop;h<top;h+=.14f)B("Metal",x,y+h,z,width,.012f,.012f);
        Collider("Sports net",V(x,y+top/2,z),V(width,top,.06f));
    }
    static void HallTitle(string title,string subtitle,float x,float z,string accent="Navy"){
        B(accent,x,y+4.7f,z,11,4.8f,.10f);Sign(title,V(x,y+5.5f,z-.065f),Vector3.back,.7f,White);
        Sign(subtitle,V(x,y+4.5f,z-.066f),Vector3.back,.28f,White);
        B("White",x,y+3.8f,z-.07f,3.5f,.025f,.015f);
    }
    static void Scoreboard(float x,float z,string title){
        SoftBox("Black",V(x,y+5,z),V(5.5f,2,.2f),.08f);Sign(title,V(x,y+5.65f,z-.12f),Vector3.back,.25f,White);
        Sign("HOME   00 : 00   GUEST",V(x,y+5.1f,z-.12f),Vector3.back,.32f,new Color(1,.64f,.21f));
        Sign("00          00",V(x,y+4.55f,z-.12f),Vector3.back,.4f,White);
    }
    static void Seats(float x,float z,int count,int rows){
        for(int r=0;r<rows;r++){
            B("Floor",x,y+(r+1)*.19f,z+r*.9f,count*.72f,(r+1)*.38f,.9f,"Bleacher platform");
            for(int i=0;i<count;i++){
                float px=x+(i-(count-1)*.5f)*.72f,h=y+(r+1)*.38f;
                SoftBox("Navy",V(px,h+.38f,z+r*.9f),V(.54f,.09f,.48f),.045f);
                SoftBox("Navy",V(px,h+.66f,z+r*.9f+.24f),V(.54f,.58f,.075f),.035f);
                B("Metal",px,h+.17f,z+r*.9f,.07f,.34f,.32f);
            }
        }
    }
    static void Volleyball(){
        Surface("Oak pale",-23,7,35,40);Surface("Wood",-23,7,13,22);Rect(-23,7,9,18);
        foreach(float z in new[]{4f,7f,10f})Line(-27.5f,z,-18.5f,z);
        Net(-23,7,9.5f,2.43f,1);Seats(-23,22,16,3);
        HallTitle("VOLLEYBALL","排球馆  /  TOGETHER WE RISE",-32,29.72f);Scoreboard(-16,29.66f,"CAMPUS VOLLEYBALL");
        Bench(-37,y,-9,4);Bench(-31,y,-9,4);Ball("White",V(-34,y+.115f,-7),.115f);
        // Ball cart: tubular cage and four casters.
        for(int i=0;i<5;i++)Ball(i%2==0?"Yellow":"White",V(-37+i*.23f,y+.75f,-5),.11f);
        foreach(float x in new[]{-37.3f,-35.7f})foreach(float z in new[]{-5.5f,-4.6f})Tube("Metal",V(x,y+.1f,z),V(x,y+1,z),.023f);
        foreach(float h in new[]{.35f,1f}){B("Metal",-36.5f,y+h,-5.5f,1.6f,.035f,.035f);B("Metal",-36.5f,y+h,-4.6f,1.6f,.035f,.035f);}
    }
    static void Badminton(){
        Surface("Floor",-23,7,35,40);
        for(int i=0;i<3;i++){
            float x=-34+i*10.5f;Surface("Green",x,6.5f,8.5f,17.8f);Rect(x,6.5f,6.1f,13.4f);
            foreach(float z in new[]{6.5f-1.98f,6.5f+1.98f,6.5f-5.94f,6.5f+5.94f})Line(x-3.05f,z,x+3.05f,z);
            foreach(float dx in new[]{-2.59f,2.59f})Line(x+dx,-.2f,x+dx,13.2f);
            Line(x,-.2f,x,4.52f);Line(x,8.48f,x,13.2f);Net(x,6.5f,6.1f,1.55f,.76f,"Teal");
            Sign("COURT  0"+(i+1),V(x,y+.05f,-4),Vector3.up,.42f,Ink);Bench(x,y,21,4);
        }
        HallTitle("BADMINTON","羽毛球馆  /  MORE THAN A GAME",-23,29.72f,"Teal");
        Plant(V(-38,y,26),1.5f);Plant(V(-8,y,26),1.5f);
    }
    static void Basketball(){
        Surface("Oak pale",23,7,35,40);Rect(23,6,15,28);Line(15.5f,6,30.5f,6);Arc(23,6,1.8f,0,360);
        foreach(int side in new[]{-1,1}){
            float baseline=6+side*14,free=baseline-side*5.8f;
            Surface("Navy",23,baseline-side*2.9f,4.9f,5.8f);Rect(23,baseline-side*2.9f,4.9f,5.8f);Arc(23,free,1.8f,0,360);
            float basket=baseline-side*1.575f;Arc(23,basket,6.75f,side<0?0:180,side<0?180:360);
            Hoop(23,baseline,side);
        }
        Seats(23,24,23,3);Scoreboard(23,29.7f,"CAMPUS BASKETBALL");
        HallTitle("TEAM / SPIRIT","在运动中遇见更好的自己",11.2f,29.72f);
        Ball("Orange",V(31.8f,y+.12f,-6),.12f);
    }
    static void Hoop(float x,float baseline,int side){
        float z=baseline+side*1.2f,board=baseline-side*1.2f,rim=board-side*.4f;
        SoftBox("Navy",V(x,y+.55f,z),V(1.5f,1.1f,2),.15f,"Basketball base");
        Beam("Metal",V(x,y+.8f,z),V(x,y+3.8f,z-side*.5f),.2f);Beam("Metal",V(x,y+3.8f,z-side*.5f),V(x,y+3.8f,board),.16f);
        B("Glass",x,y+3.7f,board,1.8f,1.05f,.06f);
        foreach(float dx in new[]{-.92f,.92f})B("White",x+dx,y+3.7f,board,.055f,1.14f,.08f);
        foreach(float h in new[]{3.15f,4.25f})B("White",x,y+h,board,1.85f,.055f,.08f);
        foreach(float dx in new[]{-.29f,.29f})B("White",x+dx,y+3.47f,board-side*.045f,.035f,.45f,.018f);
        foreach(float h in new[]{3.25f,3.7f})B("White",x,y+h,board-side*.045f,.61f,.035f,.018f);
        for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Tube("Orange",V(x+Mathf.Cos(a)*.225f,y+3.05f,rim+Mathf.Sin(a)*.225f),V(x+Mathf.Cos(b)*.225f,y+3.05f,rim+Mathf.Sin(b)*.225f),.012f,6);}
        for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=a+.4f;Tube("White",V(x+Mathf.Cos(a)*.22f,y+3.04f,rim+Mathf.Sin(a)*.22f),V(x+Mathf.Cos(b)*.14f,y+2.62f,rim+Mathf.Sin(b)*.14f),.007f,5);}
    }
    static void Fitness(){
        Surface("Rubber",23,-3,35,20.9f);
        B("Wall",23,y+2.4f,8.5f,37,4.8f,.2f,"Fitness dance partition");
        B("Mirror",23,y+2,8.37f,28,2.8f,.035f);B("Metal",23,y+.6f,8.32f,28,.08f,.06f);
        Sign("FIT FOR A BETTER YOU",V(23,y+4,8.34f),Vector3.back,.5f,Ink);
        for(int i=0;i<6;i++)Treadmill(38,-10+i*2.8f);
        for(int i=0;i<4;i++){Weights(14+i*5.5f,-7);WeightBench(14+i*5.5f,-2);}
        // Dumbbell racks along mirrored wall, with three sizes and actual handles.
        for(int r=0;r<2;r++){
            float z=5.6f,x=17+r*12;foreach(float dx in new[]{-3.1f,3.1f})Beam("Metal",V(x+dx,y+.1f,z+.35f),V(x+dx,y+1.2f,z-.15f),.08f);
            foreach(float h in new[]{.6f,1.15f}){B("Metal",x,y+h,z,6.8f,.08f,.45f,"Dumbbell rack");for(int j=0;j<9;j++){float px=x-2.9f+j*.72f;Tube("Steel",V(px-.22f,y+h+.18f,z),V(px+.22f,y+h+.18f,z),.025f);foreach(float dx in new[]{-.22f,.22f})Tube("Black",V(px+dx-.06f,y+h+.18f,z),V(px+dx+.06f,y+h+.18f,z),.11f+j*.005f);}}
        }
        Plant(V(7,y,6),1.2f);B("Navy",10,y+.019f,-10,4,.01f,3);Ball("Blue",V(11,y+.35f,-10),.35f);
    }
    static void Treadmill(float x,float z){
        SoftBox("Metal",V(x,y+.16f,z),V(1.05f,.24f,2.15f),.08f,"Treadmill");B("Black",x,y+.285f,z, .76f,.025f,1.8f);
        foreach(float dx in new[]{-.48f,.48f}){Beam("Steel",V(x+dx,y+.2f,z+.7f),V(x+dx,y+1.35f,z+.55f),.065f);Tube("Black",V(x+dx,y+1.05f,z+.5f),V(x+dx,y+1.05f,z-.25f),.034f);}
        SoftBox("Metal",V(x,y+1.3f,z+.55f),V(1,.32f,.16f),.045f);B("Screen",x,y+1.31f,z+.456f,.43f,.18f,.018f);
        B("Red",x+.33f,y+1.27f,z+.45f,.07f,.07f,.02f);
    }
    static void WeightBench(float x,float z){
        SoftBox("Black",V(x,y+.53f,z),V(.55f,.12f,1.6f),.06f,"Weight bench");
        foreach(float dz in new[]{-.55f,.55f}){B("Metal",x,y+.26f,z+dz,.08f,.5f,.12f);B("Metal",x,y+.06f,z+dz,.8f,.12f,.16f);}
    }
    static void Weights(float x,float z){
        foreach(float dx in new[]{-1f,1f}){B("Metal",x+dx,y+1.25f,z,.12f,2.5f,.12f,"Power rack");B("Metal",x+dx,y+.07f,z+.6f,.16f,.14f,1.5f);for(float h=.7f;h<2.3f;h+=.18f)B("Steel",x+dx,y+h,z-.065f,.035f,.035f,.01f);}
        Tube("Metal",V(x-1,y+2.5f,z),V(x+1,y+2.5f,z),.035f);
        Tube("Steel",V(x-1.5f,y+1.2f,z-.12f),V(x+1.5f,y+1.2f,z-.12f),.035f);
        foreach(float dx in new[]{-1.18f,1.18f})Tube("Black",V(x+dx-.1f,y+1.2f,z-.12f),V(x+dx+.1f,y+1.2f,z-.12f),.23f,20);
        WeightBench(x,z+.8f);
    }
    static void Dance(){
        Surface("Oak pale",23,19,35,20.8f);
        // Lower, warmer ceiling and recessed perimeter light distinguish the studio.
        B("Wall",23,y+4.7f,19,36.5f,.18f,20.7f,"Dance acoustic ceiling");
        for(float x=5.2f;x<41.2f;x+=.3f)B("Wood",x,y+4.57f,19,.12f,.16f,20.4f);
        foreach(float z in new[]{9.1f,28.8f})B("Light",23,y+4.45f,z,35,.04f,.08f);
        for(float z=12;z<29;z+=6){B("Light",23,y+4.44f,z,25,.04f,.1f);Fill(V(23,y+4.2f,z),3,13,true);}
        for(int i=0;i<12;i++){float x=6.5f+i*2.9f;B("Mirror",x,y+2.05f,29.68f,2.85f,3.5f,.04f);B("Metal",x-1.44f,y+2.05f,29.62f,.025f,3.5f,.04f);}
        foreach(float h in new[]{.8f,1.1f})Tube("Wood",V(6,y+h,28.95f),V(40,y+h,28.95f),.035f,16);
        for(float x=7;x<41;x+=4){Tube("Steel",V(x,y+.15f,29.3f),V(x,y+1.13f,29.3f),.026f);Tube("Steel",V(x,y+1.1f,29.3f),V(x,y+1.1f,28.95f),.026f);}
        Sign("DANCE  /  身体与节奏",V(23,y+3.8f,8.65f),Vector3.forward,.48f,Ink);
        foreach(float z in new[]{15f,20f,25f}){Surface("Teal",37,z,1.4f,2.5f);}
        for(int i=0;i<4;i++)Ball(i%2==0?"Blue":"Teal",V(10+i*1.1f,y+.4f,10),.4f);
        Plant(V(39,y,10),1.4f);
    }
    static void TableTennis(){
        Surface("Floor",-23,7,35,40);
        for(int row=0;row<4;row++)for(int col=0;col<3;col++){
            float x=-35+col*11,z=-7+row*8;PingPong(x,z);
            // Waist-high fabric barriers, with wide passages around each group.
            B("Navy",x,y+.4f,z+4.2f,8,.7f,.035f,"Table tennis barrier");
            foreach(float dx in new[]{-4f,4f}){Tube("Metal",V(x+dx,y,z+4.2f),V(x+dx,y+.8f,z+4.2f),.02f);B("Metal",x+dx,y+.03f,z+4.2f,.08f,.06f,.7f);}
            Sign("CAMPUS  /  0"+(row*3+col+1),V(x,y+.44f,z+4.17f),Vector3.back,.2f,White);
        }
        HallTitle("TABLE TENNIS","乒乓 · 让热爱发光",-23,29.72f,"Blue");Plant(V(-38,y,26),1.6f);Plant(V(-8,y,26),1.6f);Bench(-23,y,26,6);
    }
    static void PingPong(float x,float z){
        SoftBox("Blue",V(x,y+.74f,z),V(1.525f,.04f,2.74f),.012f,"Table tennis table");
        foreach(float dx in new[]{-.75f,.75f})B("White",x+dx,y+.79f,z,.02f,.006f,2.72f);
        foreach(float dz in new[]{-1.36f,1.36f})B("White",x,y+.79f,z+dz,1.5f,.006f,.02f);
        B("White",x,y+.791f,z,.008f,.004f,2.7f);
        foreach(float dx in new[]{-.59f,.59f})foreach(float dz in new[]{-.95f,.95f}){B("Metal",x+dx,y+.36f,z+dz,.045f,.7f,.045f);Tube("Black",V(x+dx-.04f,y+.08f,z+dz),V(x+dx+.04f,y+.08f,z+dz),.065f);}
        B("Metal",x,y+.4f,z,1.2f,.05f,2);B("White",x,y+.918f,z,1.75f,.018f,.018f);
        for(float dx=-.85f;dx<.86f;dx+=.045f)B("Metal",x+dx,y+.84f,z,.008f,.15f,.008f);
        for(float h=.78f;h<.92f;h+=.045f)B("Metal",x,y+h,z,1.74f,.008f,.008f);
        Ball("White",V(x+.4f,y+.79f,z-.7f),.02f);
        Tube("Red",V(x-.3f,y+.77f,z-.75f),V(x-.3f,y+.785f,z-.75f),.08f,20);B("Wood",x-.3f,y+.777f,z-.87f,.035f,.018f,.16f);
    }
    static void Billiards(){
        Surface("Walnut",23,7,35,40);
        B("Rubber",23,y+4.8f,8,36.5f,.18f,43,"Billiards ceiling");
        foreach(float x in new[]{5.2f,41.2f})B("Light",x,y+4.65f,8,.06f,.04f,42);
        for(float x=5.6f;x<41;x+=.32f)B("Walnut",x,y+2,29.66f,.14f,4,.12f);
        Sign("BILLIARDS  /  以球会友",V(23,y+3.3f,29.49f),Vector3.back,.65f,White);
        for(int row=0;row<4;row++)for(int col=0;col<3;col++){
            float x=12+col*11,z=-7+row*8;PoolTable(x,z);
            foreach(float dx in new[]{-1f,1f})Tube("Black",V(x+dx,y+3.25f,z),V(x+dx,y+4.7f,z),.012f,6);
            SoftBox("Metal",V(x,y+3.2f,z),V(3.2f,.18f,1.6f),.04f);B("Light",x,y+3.1f,z,2.95f,.025f,1.35f);Fill(V(x,y+3,z),3.5f,6,true);
        }
        for(int i=0;i<6;i++){float x=10+i*.32f;Tube("Wood",V(x,y+.3f,29.15f),V(x+.16f,y+1.8f,29.15f),.014f,8);}
        B("Walnut",10.8f,y+.3f,29.15f,2.4f,.12f,.3f);B("Walnut",10.8f,y+1.6f,29.3f,2.4f,.12f,.2f);
        Plant(V(38,y,26),1.6f);Bench(23,y,26,5);
    }
    static void PoolTable(float x,float z){
        SoftBox("Walnut",V(x,y+.7f,z),V(1.95f,.28f,3.45f),.09f,"Billiards table");
        B("Felt",x,y+.855f,z,1.55f,.04f,3.05f);
        foreach(float dx in new[]{-.9f,.9f})SoftBox("Wood",V(x+dx,y+.91f,z),V(.2f,.12f,3.4f),.045f);
        foreach(float dz in new[]{-1.62f,1.62f})SoftBox("Wood",V(x,y+.91f,z+dz),V(1.8f,.12f,.2f),.045f);
        foreach(float dx in new[]{-.66f,.66f})foreach(float dz in new[]{-1.25f,1.25f}){
            Tube("Walnut",V(x+dx,y+.12f,z+dz),V(x+dx,y+.62f,z+dz),.11f,16);Ball("Walnut",V(x+dx,y+.24f,z+dz),.14f);Tube("Metal",V(x+dx,y+.02f,z+dz),V(x+dx,y+.1f,z+dz),.13f,16);
        }
        foreach(float dx in new[]{-.77f,.77f})foreach(float dz in new[]{-1.51f,0,1.51f})Tube("Black",V(x+dx,y+.87f,z+dz),V(x+dx,y+.98f,z+dz),.075f,18);
        for(int row=0;row<5;row++)for(int c=0;c<=row;c++){string mat=new[]{"Yellow","Blue","Red","Orange","White","Black"}[(row*3+c)%6];Ball(mat,V(x+(c-row*.5f)*.059f,y+.905f,z+.2f+row*.052f),.029f);}
        Ball("White",V(x+.15f,y+.905f,z-.8f),.029f);
        foreach(float dz in new[]{-1.1f,-.55f,.55f,1.1f})foreach(float dx in new[]{-.9f,.9f})Tube("White",V(x+dx,y+.97f,z+dz),V(x+dx,y+.974f,z+dz),.014f,8);
        Tube("Wood",V(x-1.15f,y+.1f,z-.7f),V(x-.9f,y+1.5f,z+.1f),.013f,8);
    }
}
