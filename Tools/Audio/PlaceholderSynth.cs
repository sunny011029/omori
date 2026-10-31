using System;
using System.IO;

// Original placeholder composition. No external recordings or sample libraries.
public static class PlaceholderSynth
{
    const int Rate=44100;
    static double Hz(int midi){return 440*Math.Pow(2,(midi-69)/12.0);}
    static void Note(float[][] audio,int midi,double start,double duration,double gain,double pan,bool pad)
    {
        double f=Hz(midi);int offset=(int)(start*Rate),count=(int)(duration*Rate),length=audio[0].Length;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)Rate;
            double envelope=pad?Math.Sin(Math.PI*i/count)*Math.Sin(Math.PI*i/count):Math.Min(1,t/.008)*Math.Exp(-t*5/duration)*Math.Min(1,(duration-t)/.04);
            double tone=pad?(Math.Sin(2*Math.PI*f*t)+.18*Math.Sin(4*Math.PI*f*t)):
                (Math.Sin(2*Math.PI*f*t)+.25*Math.Sin(2*Math.PI*f*2*t)*Math.Exp(-t*12)+.08*Math.Sin(2*Math.PI*f*3*t));
            double v=tone*envelope*gain;int at=(offset+i)%length;
            audio[0][at]+=(float)(v*Math.Sqrt((1-pan)/2));audio[1][at]+=(float)(v*Math.Sqrt((1+pan)/2));
        }
    }
    static void Save(string path,float[][] samples)
    {
        double peak=0;foreach(var channel in samples)foreach(var v in channel)peak=Math.Max(peak,Math.Abs(v));
        double gain=.72/Math.Max(peak,.001);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using(var w=new BinaryWriter(File.Create(path)))
        {
            int size=samples[0].Length*4;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+size);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*4);w.Write((short)4);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(size);
            for(int i=0;i<samples[0].Length;i++)for(int c=0;c<2;c++)w.Write((short)Math.Round(samples[c][i]*gain*32767));
        }
    }
    public static void Generate(string folder)
    {
        const double beat=2.0/3;int length=29400*64;
        float[][] a={new float[length],new float[length]};
        int[][] chords={new[]{60,64,67,71},new[]{57,60,64,67},new[]{53,57,60,64},new[]{55,59,62,69}};
        int[] melody={76,79,74,72, 76,72,71,69, 72,76,79,76, 74,71,69,67};
        for(int bar=0;bar<16;bar++)
        {
            var chord=chords[bar%4];double start=bar*4*beat;
            foreach(int n in chord)Note(a,n,start,4.6*beat,.055,(n%3-1)*.35,true);
            Note(a,chord[0]-24,start,2*beat,.19,0,false);Note(a,chord[0]-12,start+2*beat,1.8*beat,.10,0,false);
            for(int j=0;j<8;j++)Note(a,chord[j%4]+12,start+j*.5*beat,.7,.055,Math.Sin(j)*.45,false);
            for(int j=0;j<2;j++)Note(a,melody[(bar*2+j)%16],start+(j*2+.5)*beat,1.2*beat,.13,j==0?-.15:.15,false);
        }
        float[][] dry={(float[])a[0].Clone(),(float[])a[1].Clone()};
        for(int i=0;i<length;i++)for(int c=0;c<2;c++)
            a[c][i]+=dry[1-c][(i-11025+length)%length]*.16f+dry[c][(i-19240+length)%length]*.08f;
        Save(Path.Combine(folder,"CozyLoop_Placeholder.wav"),a);
        float[][] s={new float[15876],new float[15876]};
        Note(s,79,0,.18,.4,-.1,false);Note(s,84,.075,.26,.32,.1,false);
        Save(Path.Combine(folder,"Interact_Placeholder.wav"),s);
    }
}
