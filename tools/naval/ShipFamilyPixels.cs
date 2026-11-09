using System;
using System.Drawing;

// Registration, alpha cleanup and atlas assembly for authored imagegen sheets.
// Foreground selection supplies a mask; its painted colours are never copied.
public static class ShipFamilyPixels
{
    const int W=384,H=256,AW=3072,AH=1024;
    static int Clean(int pixel,bool spectral=false)
    {
        uint p=(uint)pixel;int a=(int)(p>>24),r=(int)(p>>16)&255,g=(int)(p>>8)&255,b=(int)p&255;
        if(a<(spectral && (g>r*1.15 || b>r*1.15)?8:96) || a<220 && r>180 && g<60 && b<80)return 0; // transparent colour-matte residue
        if(spectral && a<220 && g>180 && b<g*.55 && r<g*.55)return 0;
        return pixel;
    }
    static bool HullColour(int pixel,int palette=0)
    {
        uint p=(uint)pixel;int r=(int)(p>>16)&255,g=(int)(p>>8)&255,b=(int)p&255;
        if(p>>24<96)return false;
        return palette==1?r>g*2 && r>b*1.5 && r>60:palette==2?g>r*1.3 && g>b*1.15 && g>50:b>r*1.35 && b>g*1.1 && b>60;
    }
    static int[] Bounds(int[] pixels,int palette=0)
    {
        int left=W,top=H,right=0,bottom=0;
        for(int y=H/2;y<H;y++)for(int x=0;x<W;x++)if(HullColour(pixels[y*W+x],palette))
        {left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
        if(right-left<16 || bottom-top<3)throw new InvalidOperationException("Missing royal hull registration features");
        return new[]{left,top,right,bottom};
    }
    static int[] SpriteBounds(int[] pixels)
    {
        int left=W,top=H,right=0,bottom=0;
        for(int y=0;y<H;y++)for(int x=0;x<W;x++)if((uint)pixels[y*W+x]>>24>96)
        {left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
        return new[]{left,top,right,bottom};
    }
    static double[] AlignMask(int[] art,int[] mask)
    {
        var points=new (int X,int Y,int R,int G,int B)[W*H/4];int count=0;
        for(int y=0;y<H;y+=2)for(int x=0;x<W;x+=2)
        {
            uint p=(uint)mask[y*W+x];int r=(int)(p>>16)&255,g=(int)(p>>8)&255,b=(int)p&255;
            if(p>>24>=220 && r>150 && g>90 && r>b*1.4 && g>b*1.2)points[count++]=(x,y,r,g,b);
        }
        if(count<12)return new[]{1d,0d,0d};
        Array.Resize(ref points,count);
        double best=double.NegativeInfinity,bs=1,bx=0,by=0;
        void Try(double s,double dx,double dy)
        {
            double score=0;
            foreach(var p in points)
            {
                int x=(int)Math.Round(p.X*s+dx),y=(int)Math.Round(p.Y*s+dy),distance=765;
                if(x>=1 && y>=1 && x<W-1 && y<H-1)
                    for(int yy=-1;yy<=1;yy++)for(int xx=-1;xx<=1;xx++)
                    {
                        uint q=(uint)art[(y+yy)*W+x+xx];if(q>>24<160)continue;
                        int r=(int)(q>>16)&255,g=(int)(q>>8)&255,b=(int)q&255;
                        distance=Math.Min(distance,Math.Abs(p.R-r)+Math.Abs(p.G-g)+Math.Abs(p.B-b));
                    }
                score+=distance<100?1-distance/100d:-.6;
            }
            score-=Math.Abs(s-1)*count*.05;
            if(score>best){best=score;bs=s;bx=dx;by=dy;}
        }
        for(int k=0;k<5;k++)for(int y=-16;y<=16;y+=4)for(int x=-24;x<=24;x+=4)Try(.9+k*.05,x,y);
        double cs=bs,cx=bx,cy=by;
        for(int k=-2;k<=2;k++)for(int y=-3;y<=3;y++)for(int x=-3;x<=3;x++)Try(cs+k*.01,cx+x,cy+y);
        return best<count*.1?new[]{1d,0d,0d}:new[]{bs,bx,by};
    }
    public static double[] Assemble(string artPath,string maskPath,string referencePath,string foregroundPath,double[] pivots,string target,string fringeTarget,int palette=0)
    {
        int[] source,mask,reference,oldForeground;int sw,sh,mw,mh;
        using(var b=new Bitmap(artPath)){source=CannonLayerPixels.Read(b);sw=b.Width;sh=b.Height;}
        using(var b=new Bitmap(maskPath)){mask=CannonLayerPixels.Read(b);mw=b.Width;mh=b.Height;}
        using(var b=new Bitmap(referencePath))reference=CannonLayerPixels.Read(b);
        using(var b=new Bitmap(foregroundPath))oldForeground=CannonLayerPixels.Read(b);
        int[] atlas=new int[AW*AH],fringe=new int[AW*AH];var registration=new double[8*4];int[] bob={0,-1,0,1};
        for(int d=0;d<8;d++)
        {
            var authored=new int[W*H];var selection=new bool[W*H];var authoredMask=new int[W*H];var original=new int[W*H];
            int col=d%2,row=d/2,ox=col*1536,oy=row*H;
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                int sx=Math.Min(sw-1,(int)((col+(x+.5)/W)*sw/2d));
                int sy=Math.Min(sh-1,(int)((row+(y+.5)/H)*sh/4d));
                int mx=Math.Min(mw-1,(int)((col+(x+.5)/W)*mw/2d));
                int my=Math.Min(mh-1,(int)((row+(y+.5)/H)*mh/4d));
                if(mw==AW && mh==AH){mx=col*1536+x;my=row*H+y;}
                authored[y*W+x]=Clean(source[sy*sw+sx],palette==4);authoredMask[y*W+x]=Clean(mask[my*mw+mx]);
                original[y*W+x]=reference[(oy+y)*AW+ox+x];
            }
            var maskTransform=AlignMask(authored,authoredMask);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                int mx=(int)Math.Round((x-maskTransform[1])/maskTransform[0]),my=(int)Math.Round((y-maskTransform[2])/maskTransform[0]);
                for(int dy=-1;dy<=1 && !selection[y*W+x];dy++)for(int dx=-1;dx<=1;dx++)
                    if(mx+dx>=0 && my+dy>=0 && mx+dx<W && my+dy<H && (uint)authoredMask[(my+dy)*W+mx+dx]>>24>=180){selection[y*W+x]=true;break;}
            }
            var before=palette>=3?SpriteBounds(original):Bounds(original);var after=palette>=3?SpriteBounds(authored):Bounds(authored,palette);
            double scaleX=(before[2]-before[0])/(double)(after[2]-after[0]);
            if(palette>0 && (scaleX<.8 || scaleX>1.2))
            {
                before=SpriteBounds(original);after=SpriteBounds(authored);
                scaleX=(before[2]-before[0])/(double)(after[2]-after[0]);
            }
            double scaleY=(before[3]-before[1])/(double)(after[3]-after[1]);
            // Vertical hull stripe thickness is small. Keep the aspect ratio;
            // translate its bottom edge instead of amplifying rounding noise.
            var artBounds=SpriteBounds(authored);
            scaleX=Math.Min(scaleX,Math.Min((W-8d)/(artBounds[2]-artBounds[0]+1),(H-8d)/(artBounds[3]-artBounds[1]+1)));
            scaleY=scaleX;
            double tx=before[0]-after[0]*scaleX,ty=before[3]-after[3]*scaleY;
            tx=Math.Clamp(tx,3-artBounds[0]*scaleX,W-4-artBounds[2]*scaleX);
            ty=Math.Clamp(ty,3-artBounds[1]*scaleY,H-4-artBounds[3]*scaleY);
            if(scaleX<.8 || scaleX>1.2)throw new InvalidOperationException("Hull changed scale: "+d);
            registration[d*4]=scaleX;registration[d*4+1]=scaleY;registration[d*4+2]=tx;registration[d*4+3]=ty;
            for(int y=1;y<H-1;y++)for(int x=1;x<W-1;x++)
            {
                int sx=(int)Math.Round((x-tx)/scaleX),sy=(int)Math.Round((y-ty)/scaleY);
                int pixel=0,front=0;
                if(sx>=0 && sy>=0 && sx<W && sy<H)
                {pixel=authored[sy*W+sx];if(selection[sy*W+sx])front=pixel;}
                uint p=(uint)pixel;int r=(int)(p>>16)&255,g=(int)(p>>8)&255,b=(int)p&255;
                for(int gun=0;gun<6;gun++)
                {
                    double px=pivots[(d*6+gun)*2],py=pivots[(d*6+gun)*2+1];
                    if((x-px)*(x-px)+(y-py)*(y-py)>20*20 || p>>24<96 || Math.Max(r,Math.Max(g,b))>175 || Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))>45)continue;
                    // The authored fixed iron is replaced by the original,
                    // independently animated gun. Its wooden carriage stays.
                    bool onHull=original[y*W+x]!=0;
                    pixel=onHull?unchecked((int)0xff19120e):0;front=0;break;
                }
                for(int f=0;f<4;f++)
                {
                    int at=(oy+y+bob[f])*AW+ox+f*W+x;atlas[at]=pixel;fringe[at]=front;
                }
            }
        }
        CannonLayerPixels.Save(atlas,AW,AH,target);CannonLayerPixels.Save(fringe,AW,AH,fringeTarget);
        return registration;
    }
    public static int[] RegisteredCannonCover(int[] fringe,int[] referenceCover,int[] guns,double[] positions)
    {
        var cover=(int[])fringe.Clone();int[] bob={0,-1,0,1};
        for(int d=0;d<8;d++)for(int gun=0;gun<6;gun++)
        {
            int n=d*6+gun;double radius=0;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                if((uint)guns[(n/8*64+y)*512+n%8*64+x]>>24>32)radius=Math.Max(radius,Math.Sqrt((x-32)*(x-32)+(y-32)*(y-32)));
            int r=(int)Math.Ceiling(radius)+3;
            for(int f=0;f<4;f++)for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++)
            {
                int x=(int)positions[n*2]+dx,y=(int)positions[n*2+1]+bob[f]+dy;
                if(x<1 || y<1 || x>=W-1 || y>=H-1 || dx*dx+dy*dy>r*r)continue;
                int at=(d/2*H+y)*AW+d%2*1536+f*W+x;
                if((uint)referenceCover[at]>>24<32)cover[at]=0;
            }
        }
        return cover;
    }
    public static void SpectralLayers(string basePath,string fringePath,string coverPath,string gunSource,string gunTarget)
    {
        int[] body,front,cover,guns;
        using(var b=new Bitmap(basePath))body=CannonLayerPixels.Read(b);
        using(var b=new Bitmap(fringePath))front=CannonLayerPixels.Read(b);
        using(var b=new Bitmap(coverPath))cover=CannonLayerPixels.Read(b);
        using(var b=new Bitmap(gunSource))guns=CannonLayerPixels.Read(b);
        int Fade(int pixel)
        {
            uint p=(uint)pixel;int a=(int)Math.Round((p>>24)*.68);
            return unchecked((int)((p&0xffffffu)|((uint)a<<24)));
        }
        for(int i=0;i<body.Length;i++)
        {
            // Each translucent source pixel belongs to exactly one render pass.
            // Repainting the same sail in the base and foreground would make it opaque.
            if((uint)front[i]>>24>0)body[i]=0;
            if((uint)cover[i]>>24>0)front[i]=0;
            body[i]=Fade(body[i]);front[i]=Fade(front[i]);cover[i]=Fade(cover[i]);
            if((((uint)body[i]>>24>0?1:0)+((uint)front[i]>>24>0?1:0)+((uint)cover[i]>>24>0?1:0))>1)
                throw new InvalidOperationException("Spectral layers overlap");
        }
        for(int i=0;i<guns.Length;i++)
        {
            uint p=(uint)guns[i];int l=(((int)(p>>16)&255)+((int)(p>>8)&255)+((int)p&255))/3;
            guns[i]=Fade(unchecked((int)((p&0xff000000u)|((uint)(l/3)<<16)|((uint)Math.Min(255,55+l)<<8)|(uint)Math.Min(255,75+l))));
        }
        CannonLayerPixels.Save(body,AW,AH,basePath);CannonLayerPixels.Save(front,AW,AH,fringePath);
        CannonLayerPixels.Save(cover,AW,AH,coverPath);CannonLayerPixels.Save(guns,512,384,gunTarget);
    }
    public static void Sinking(string artPath,string target,bool spectral=false)
    {
        int[] source;int sw,sh;using(var image=new Bitmap(artPath)){source=CannonLayerPixels.Read(image);sw=image.Width;sh=image.Height;}
        var output=new int[AW*AH];int[] descend={0,42,112,256};
        for(int d=0;d<8;d++)
        {
            var pose=new int[W*H];int col=d%2,row=d/2;
            for(int y=8;y<H-8;y++)for(int x=12;x<W-12;x++)
            {
                int sx=Math.Min(sw-1,(int)((col+(x-12+.5)/(W-24))*sw/2d));
                int sy=Math.Min(sh-1,(int)((row+(y-8+.5)/(H-16))*sh/4d));
                int pixel=Clean(source[sy*sw+sx],spectral);
                if(spectral){uint p=(uint)pixel;pixel=unchecked((int)((p&0xffffffu)|((uint)Math.Round((p>>24)*.68)<<24)));}
                pose[y*W+x]=pixel;
            }
            var bounds=SpriteBounds(pose);int waterline=Math.Min(H-5,bounds[3]);
            for(int phase=0;phase<4;phase++)for(int y=0;y<=waterline;y++)for(int x=0;x<W;x++)
            {
                int sy=y-descend[phase];if(sy<0 || sy>=H)continue;
                output[(row*H+y)*AW+col*1536+phase*W+x]=pose[sy*W+x];
            }
        }
        CannonLayerPixels.Save(output,AW,AH,target);
    }
    public static long Validate(string path,string fringePath,string coverPath,bool sinking,bool spectral)
    {
        int[] body,front=null,cover=null;
        using(var b=new Bitmap(path))
        {
            if(b.Width!=AW || b.Height!=AH)throw new InvalidOperationException("Unexpected atlas dimensions: "+path);
            body=CannonLayerPixels.Read(b);
        }
        if(!sinking)
        {
            using(var b=new Bitmap(fringePath))front=CannonLayerPixels.Read(b);
            using(var b=new Bitmap(coverPath))cover=CannonLayerPixels.Read(b);
            if(front.Length!=body.Length || cover.Length!=body.Length)throw new InvalidOperationException("Layer dimensions differ");
        }
        long translucent=0;
        for(int d=0;d<8;d++)for(int frame=0;frame<4;frame++)
        {
            int count=0,ox=d%2*1536+frame*W,oy=d/2*H;
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                int at=(oy+y)*AW+ox+x;uint a=(uint)body[at]>>24;
                uint f=front==null?0:(uint)front[at]>>24,c=cover==null?0:(uint)cover[at]>>24;
                if(a+f+c>0)count++;
                if(spectral)
                {
                    if((a>0?1:0)+(f>0?1:0)+(c>0?1:0)>1)throw new InvalidOperationException("Ghost pixel painted in more than one pass");
                    if(a>174 || f>174 || c>174)throw new InvalidOperationException("Opaque ghost pixel");
                    if(a>0 || f>0 || c>0)translucent++;
                }
            }
            if(sinking && frame==3?count!=0:count<40)throw new InvalidOperationException("Missing pose or nontransparent final sinking phase: "+d+"/"+frame);
        }
        return translucent;
    }
    public static void Preview(string[] states,string[] fronts,string[] covers,string gunPath,double[] pivots,string target,bool sinkingOnly=false)
    {
        var output=new int[W*4*H*8];for(int i=0;i<output.Length;i++)output[i]=unchecked((int)0xff4a8ba2);
        int[] guns;using(var b=new Bitmap(gunPath))guns=CannonLayerPixels.Read(b);
        for(int col=0;col<4;col++)
        {
            int[] art,front=null,cover=null;using(var b=new Bitmap(states[sinkingOnly?3:col]))art=CannonLayerPixels.Read(b);
            if(!sinkingOnly && fronts[col].Length>0)using(var b=new Bitmap(fronts[col]))front=CannonLayerPixels.Read(b);
            if(!sinkingOnly && covers[col].Length>0)using(var b=new Bitmap(covers[col]))cover=CannonLayerPixels.Read(b);
            for(int d=0;d<8;d++)
            {
                int ox=col*W,oy=d*H,phase=sinkingOnly?col:0,ax=d%2*1536+phase*W,ay=d/2*H;
                Draw(art);
                if(front!=null)Draw(front);
                if(!sinkingOnly && col<3)for(int gun=0;gun<6;gun++)
                {
                    int n=d*6+gun;
                    CannonLayerPixels.Over(output,W*4,H*8,guns,512,n%8*64,n/8*64,64,ox+pivots[n*2],oy+pivots[n*2+1],0);
                }
                if(cover!=null)Draw(cover);
                void Draw(int[] source)
                {
                    for(int y=0;y<H;y++)for(int x=0;x<W;x++)
                    {
                        uint p=(uint)source[(ay+y)*AW+ax+x];int a=(int)(p>>24);if(a==0)continue;
                        int at=(oy+y)*W*4+ox+x;uint q=(uint)output[at];
                        int r=(((int)(p>>16)&255)*a+((int)(q>>16)&255)*(255-a)+127)/255;
                        int g=(((int)(p>>8)&255)*a+((int)(q>>8)&255)*(255-a)+127)/255;
                        int b=(((int)p&255)*a+((int)q&255)*(255-a)+127)/255;
                        output[at]=unchecked((int)(0xff000000u|(uint)r<<16|(uint)g<<8|(uint)b));
                    }
                }
            }
        }
        CannonLayerPixels.Save(output,W*4,H*8,target);
    }
}
