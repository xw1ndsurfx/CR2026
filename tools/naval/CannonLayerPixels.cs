using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Offline, lossless sprite slicing. Generated images supply selection masks only;
// visible pixels are always taken from the registered game atlas.
public static class CannonLayerPixels
{
    static void Blend(int[] output,int at,uint p)
    {
        int a=(int)(p>>24);if(a==0)return;uint q=(uint)output[at];
        int r=(((int)(p>>16)&255)*a+((int)(q>>16)&255)*(255-a)+127)/255;
        int g=(((int)(p>>8)&255)*a+((int)(q>>8)&255)*(255-a)+127)/255;
        int b=(((int)p&255)*a+((int)q&255)*(255-a)+127)/255;
        output[at]=unchecked((int)(0xff000000u|(uint)r<<16|(uint)g<<8|(uint)b));
    }
    static void Blit(int[] output,int width,int height,int[] source,int sourceWidth,int sx,int sy,int sw,int sh,double x,double y,double scale,double rotation=0)
    {
        double a=rotation*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);
        int radius=(int)Math.Ceiling(Math.Sqrt(sw*sw+sh*sh)*scale/2);
        for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
        {
            int u=(int)Math.Floor((c*dx+s*dy)/scale+sw/2d),v=(int)Math.Floor((-s*dx+c*dy)/scale+sh/2d);
            int tx=(int)Math.Round(x)+dx,ty=(int)Math.Round(y)+dy;
            if(u<0 || v<0 || u>=sw || v>=sh || tx<0 || ty<0 || tx>=width || ty>=height)continue;
            Blend(output,ty*width+tx,(uint)source[(sy+v)*sourceWidth+sx+u]);
        }
    }
    public static void ScaleComparison(string shipPath,string fringePath,string coverPath,string gunPath,string bodyPath,string pantsPath,string armorPath,string helmetPath,double[] crew,double[] pivots,string outputPath)
    {
        int[][] sheets=new int[8][];string[] paths={shipPath,fringePath,coverPath,gunPath,bodyPath,pantsPath,armorPath,helmetPath};
        int[] widths=new int[8];for(int i=0;i<8;i++)using(var b=new Bitmap(paths[i])){sheets[i]=Read(b);widths[i]=b.Width;}
        const int w=1152,h=2048;int[] pixels=new int[w*h];for(int i=0;i<pixels.Length;i++)pixels[i]=unchecked((int)0xff4a8ba2);
        double[] scales={1,.75,.5},yaw={-30,25,-15,40,-35,10};
        int[] rows={3,0,1,2};
        for(int d=0;d<8;d++)for(int col=0;col<3;col++)
        {
            int ox=col*384,oy=d*256,ax=d%2*1536,ay=d/2*256;
            Blit(pixels,w,h,sheets[0],3072,ax,ay,384,256,ox+192,oy+128,1);
            for(int i=0;i<7;i++)if(crew[(d*7+i)*4+3]==0)Person(i);
            Blit(pixels,w,h,sheets[1],3072,ax,ay,384,256,ox+192,oy+128,1);
            for(int station=0;station<6;station++)
            {
                int n=d*6+station;
                Blit(pixels,w,h,sheets[3],512,n%8*64,n/8*64,64,64,ox+pivots[n*2],oy+pivots[n*2+1],1,yaw[station]);
            }
            for(int i=0;i<7;i++)if(crew[(d*7+i)*4+3]!=0)Person(i);
            Blit(pixels,w,h,sheets[2],3072,ax,ay,384,256,ox+192,oy+128,1);
            void Person(int i)
            {
                int ci=(d*7+i)*4,row=rows[(int)crew[ci+2]],left=128,right=0,bottom=0;
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                    if((uint)sheets[4][(row*128+y)*widths[4]+x]>>24>32){left=Math.Min(left,x);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);}
                double scale=.75*scales[col];
                double cx=ox+crew[ci]+(64-(left+right)/2d)*scale;
                double cy=oy+crew[ci+1]+(64-bottom)*scale;
                for(int layer=4;layer<8;layer++)
                    if(layer!=7 || i%2==0)Blit(pixels,w,h,sheets[layer],widths[layer],0,row*128,128,128,cx,cy,scale);
            }
        }
        // Nearest-neighbour enlargement exposes the pixel edges and layer ordering.
        int[] enlarged=new int[w*h*4];
        for(int y=0;y<h*2;y++)for(int x=0;x<w*2;x++)enlarged[y*w*2+x]=pixels[(y/2)*w+x/2];
        Save(enlarged,w*2,h*2,outputPath);
        int[] profile=new int[w*2*512];Array.Copy(enlarged,512*2*w*2,profile,0,profile.Length);
        Save(profile,w*2,512,System.IO.Path.Combine(System.IO.Path.GetDirectoryName(outputPath),"Comparatif_profil_100_75_50.png"));
    }
    public static int Cut(int[] ship,int[] fringe,int[] guns,int direction,int station,int[] box,int px,int py,string maskPath)
    {
        int originX=(direction%2)*1536,originY=(direction/2)*256,index=direction*6+station;
        int[] mask=null;int mw=0,mh=0;
        if(maskPath.Length>0)using(var image=new Bitmap(maskPath)){mask=Read(image);mw=image.Width;mh=image.Height;}
        var selected=new bool[384*256];int count=0;
        for(int y=box[1];y<box[1]+box[3];y++)for(int x=box[0];x<box[0]+box[2];x++)
        {
            if(x<0 || y<0 || x>=384 || y>=256)continue;
            uint c=(uint)ship[(originY+y)*3072+originX+x];
            int r=(int)(c>>16)&255,g=(int)(c>>8)&255,b=(int)c&255;
            if(c>>24<128 || Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))>45)continue;
            if(mask!=null)
            {
                int mx=(int)((x-12+.5)/.234375),my=(int)((y-8+.5)/.234375);bool hit=false;
                for(int dy=-4;dy<=4 && !hit;dy++)for(int dx=-4;dx<=4;dx++)
                    if(mx+dx>=0 && my+dy>=0 && mx+dx<mw && my+dy<mh && (uint)mask[(my+dy)*mw+mx+dx]>>24>=64){hit=true;break;}
                if(!hit)continue;
            }
            int gx=x-px+32,gy=y-py+32;
            if(gx<0 || gy<0 || gx>=64 || gy>=64)throw new InvalidOperationException("Cannon exceeds cell");
            guns[((index/8)*64+gy)*512+(index%8)*64+gx]=(int)c;
            selected[y*384+x]=true;count++;
        }
        if(count<4)throw new InvalidOperationException("Empty cannon "+direction+":"+station+" box="+string.Join(",",box)+" mask="+mw+"x"+mh+" pixels="+count);
        int[] bob={0,-1,0,1};
        double beam=(direction*45+(station>=3?90:-90))*Math.PI/180;
        int nx=(int)Math.Round(Math.Cos(beam)*5),ny=(int)Math.Round(Math.Sin(beam)*5);
        for(int y=0;y<256;y++)for(int x=0;x<384;x++)if(selected[y*384+x])
        {
            bool opaque=true;
            foreach(int side in new[]{-1,1})
            {
                int xx=Math.Max(0,Math.Min(383,x+nx*side)),yy=Math.Max(0,Math.Min(255,y+ny*side));
                if((uint)ship[(originY+yy)*3072+originX+xx]>>24<64)opaque=false;
            }
            int fill=opaque?unchecked((int)0xff19120e):0;
            for(int f=0;f<4;f++)
            {
                int target=(originY+y+bob[f])*3072+originX+f*384+x;
                ship[target]=fill;fringe[target]=0;
            }
        }
        return count;
    }
    public static void Proxy(int[] guns,int source,int target,double degrees)
    {
        int[] pixels=new int[64*64];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=guns[(source/8*64+y)*512+source%8*64+x];
        double a=degrees*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            int sx=(int)Math.Round(c*(x-32)+s*(y-32)+32),sy=(int)Math.Round(-s*(x-32)+c*(y-32)+32);
            if(sx>=0 && sy>=0 && sx<64 && sy<64)guns[(target/8*64+y)*512+target%8*64+x]=pixels[sy*64+sx];
        }
    }
    public static void Over(int[] target,int width,int height,int[] source,int sw,int sx,int sy,int size,double cx,double cy,double degrees)
    {
        double a=degrees*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);
        int radius=(int)Math.Ceiling(size*.72);
        for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++)
        {
            int u=(int)Math.Round(c*x+s*y+size/2d),v=(int)Math.Round(-s*x+c*y+size/2d);
            int dx=(int)Math.Round(cx+x),dy=(int)Math.Round(cy+y);
            if(u<0 || v<0 || u>=size || v>=size || dx<0 || dy<0 || dx>=width || dy>=height)continue;
            uint p=(uint)source[(sy+v)*sw+sx+u];if(p>>24==0)continue;
            target[dy*width+dx]=(int)p;
        }
    }
    public static void Water(int[] pixels)
    {
        for(int i=0;i<pixels.Length;i++)
        {
            uint p=(uint)pixels[i];int a=(int)(p>>24),r=(int)(p>>16)&255,g=(int)(p>>8)&255,b=(int)p&255;
            pixels[i]=unchecked((int)(0xff000000u|(uint)((r*a+74*(255-a))/255)<<16|(uint)((g*a+139*(255-a))/255)<<8|(uint)((b*a+162*(255-a))/255)));
        }
    }
    public static int[] CannonCover(int[] fringe,int[] guns,double[] positions)
    {
        var cover=(int[])fringe.Clone();int[] bob={0,-1,0,1};
        for(int d=0;d<8;d++)for(int station=0;station<6;station++)
        {
            int index=d*6+station;double radius=0;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                if((uint)guns[(index/8*64+y)*512+index%8*64+x]>>24>32)
                    radius=Math.Max(radius,Math.Sqrt((x-32)*(x-32)+(y-32)*(y-32)));
            int r=(int)Math.Ceiling(radius)+3;
            for(int f=0;f<4;f++)
            {
                int ox=d%2*1536+f*384,oy=d/2*256;
                int cx=(int)positions[index*2],cy=(int)positions[index*2+1]+bob[f];
                for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++)
                {
                    int x=cx+dx,y=cy+dy;
                    if(x<2 || y<2 || x>=382 || y>=254 || dx*dx+dy*dy>r*r)continue;
                    bool rail=false,canvas=false;
                    for(int yy=-1;yy<=1;yy++)for(int xx=-1;xx<=1;xx++)
                    {
                        uint p=(uint)fringe[(oy+y+yy)*3072+ox+x+xx];if(p>>24<32)continue;
                        int red=(int)(p>>16)&255,green=(int)(p>>8)&255,blue=(int)p&255;
                        canvas|=red>=170 && green>=145 && blue>=95 && red>=green && green>=blue && red-blue<=130;
                        rail|=(red>=150 && green>=95 && blue<140 && red>blue*1.4) || (blue>red*1.1 && blue>green*.8);
                    }
                    // Only the rail colours and their outlines are excluded from the
                    // barrel sweep. Original canvas edges always remain an occluder.
                    if(rail && !canvas)cover[(oy+y)*3072+ox+x]=0;
                }
            }
        }
        return cover;
    }
    public static void Preview(int[] ship,int[] fringe,int[] cover,int[] guns,double[] positions,string path)
    {
        const int w=1152,h=2048;var output=new int[w*h];double[] angles={-30,0,30};
        for(int d=0;d<8;d++)for(int column=0;column<3;column++)
        {
            int ox=column*384,oy=d*256,ax=d%2*1536,ay=d/2*256;
            for(int y=0;y<256;y++)for(int x=0;x<384;x++)output[(oy+y)*w+ox+x]=ship[(ay+y)*3072+ax+x];
            for(int y=0;y<256;y++)for(int x=0;x<384;x++)
            {
                int p=fringe[(ay+y)*3072+ax+x];if((uint)p>>24>=128)output[(oy+y)*w+ox+x]=p;
            }
            for(int station=0;station<6;station++)
            {
                int index=d*6+station;
                Over(output,w,h,guns,512,index%8*64,index/8*64,64,ox+positions[index*2],oy+positions[index*2+1],angles[column]);
            }
            for(int y=0;y<256;y++)for(int x=0;x<384;x++)
            {
                int p=cover[(ay+y)*3072+ax+x];if((uint)p>>24>=128)output[(oy+y)*w+ox+x]=p;
            }
        }
        Water(output);Save(output,w,h,path);
    }
    public static int[] Read(Bitmap image)
    {
        var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var pixels=new int[image.Width*image.Height];
        try {for(int y=0;y<image.Height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*image.Width,image.Width);}
        finally {image.UnlockBits(data);} return pixels;
    }
    public static void Save(int[] pixels,int width,int height,string path)
    {
        using(var image=new Bitmap(width,height,PixelFormat.Format32bppArgb))
        {
            var data=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try {for(int y=0;y<height;y++)Marshal.Copy(pixels,y*width,IntPtr.Add(data.Scan0,y*data.Stride),width);}
            finally {image.UnlockBits(data);} image.Save(path,ImageFormat.Png);
        }
    }
    public static string[] Components(string path)
    {
        using(var image=new Bitmap(path))
        {
            var p=Read(image);var used=new bool[p.Length];var result=new string[100];int found=0;var q=new int[p.Length];
            for(int i=0;i<p.Length;i++)
            {
                if(used[i] || (uint)p[i]>>24<128)continue;
                int head=0,tail=0;q[tail++]=i;used[i]=true;
                int count=0,x0=image.Width,y0=image.Height,x1=0,y1=0;
                while(head<tail)
                {
                    int t=q[head++],x=t%image.Width,y=t/image.Width;count++;
                    x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);
                    foreach(int n in new[]{t-1,t+1,t-image.Width,t+image.Width})
                    {
                        if(n<0 || n>=p.Length || Math.Abs(n%image.Width-x)+Math.Abs(n/image.Width-y)!=1 || used[n] || (uint)p[n]>>24<128)continue;
                        used[n]=true;q[tail++]=n;
                    }
                }
                if(count>=20 && found<result.Length)result[found++]=x0+","+y0+","+(x1-x0+1)+","+(y1-y0+1)+" area="+count;
            }
            Array.Resize(ref result,found);return result;
        }
    }
}
