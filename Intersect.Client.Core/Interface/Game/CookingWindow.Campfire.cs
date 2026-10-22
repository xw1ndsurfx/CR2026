namespace Intersect.Client.Interface.Game;
internal sealed partial class CookingWindow
{
    private static void DrawCampfireEnvironment(Action<int,int,int,int,Color> fill, long now)
    {
        fill(0,0,1024,720,new Color(255,15,25,35));
        // Night sky, forest silhouettes and an earthen campsite.
        for (var i=0;i<60;i++) fill((i*157)%1024,(i*53)%260,2,2,new Color(210,210,220,235));
        fill(0,390,1024,330,new Color(255,42,47,27));
        for(var i=0;i<14;i++)
        {
            var x=i*83;
            fill(x,140+i%3*35,45,280,new Color(255,16,35,25));
            fill(x+17,100+i%3*35,12,320,new Color(255,25,31,20));
        }
        // Stone ring and logs stay visible through the translucent game board.
        for(var i=0;i<9;i++) fill(492+i*35,594+(i%2)*13,40,24,new Color(255,105,101,86));
        fill(535,582,220,18,new Color(255,101,60,30));
        fill(570,601,180,17,new Color(255,74,45,25));
        for(var i=0;i<14;i++)
        {
            var h=50+(int)((now/70+i*17)%70);
            fill(547+i*14,590-h,22,h,new Color(210,238,99+i%4*15,26));
            fill(552+i*14,590-h/2,10,h/2,new Color(240,255,201,74));
        }
        // Tripod with hanging cooking pot.
        fill(557,396,7,175,new Color(255,130,107,75));
        fill(755,396,7,175,new Color(255,130,107,75));
        fill(557,393,205,7,new Color(255,130,107,75));
        fill(654,400,4,65,new Color(255,100,100,93));
        fill(607,466,102,51,new Color(255,61,64,60));
        fill(601,462,114,10,new Color(255,103,104,92));
        for(var i=0;i<5;i++)
        {
            var rise=(int)((now/35+i*23)%65);
            fill(630+i*12,451-rise,9,16,new Color(80,220,222,210));
        }
    }
}
