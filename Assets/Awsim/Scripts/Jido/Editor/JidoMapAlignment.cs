using System;
using System.IO;
using UnityEngine;

namespace Awsim.Jido.Editor
{
    // Align native UTM grid geometry with the local WGS84 tangent plane.
    // Only map geometry is scaled; the vehicle and sensor mounts retain metres.
    public static class JidoMapAlignment
    {
        [Serializable] class Report {
            public int utmZone; public bool north;
            public double utmEasting, utmNorthing, lat, lon, alt;
            public double unityYawDeg, horizontalScale, validationRadiusM=1000;
            public double maximumHorizontalErrorM;
        }
        public static Transform Apply(Transform map, int zone, bool north,
            double easting, double northing, double lat, double lon, double alt)
        {
            double rad=Math.PI/180, la=lat*rad, lo=lon*rad;
            double[] Ecef(double latitude,double longitude) {
                double a=latitude*rad, b=longitude*rad, s=Math.Sin(a);
                double n=6378137/Math.Sqrt(1-.00669437999014*s*s);
                return new[]{(n+alt)*Math.Cos(a)*Math.Cos(b),
                    (n+alt)*Math.Cos(a)*Math.Sin(b),(n*(1-.00669437999014)+alt)*s};
            }
            var origin=Ecef(lat,lon);
            double[] Project(double e,double n) {
                var geo=GeographicLib.UTMUPS.Reverse(zone,north,easting+e,northing+n);
                var p=Ecef(geo.Item1,geo.Item2);
                double x=p[0]-origin[0],y=p[1]-origin[1],z=p[2]-origin[2];
                return new[]{-Math.Sin(lo)*x+Math.Cos(lo)*y,
                    -Math.Sin(la)*Math.Cos(lo)*x-Math.Sin(la)*Math.Sin(lo)*y+Math.Cos(la)*z};
            }
            var ep=Project(10,0); var em=Project(-10,0);
            var np=Project(0,10); var nm=Project(0,-10);
            double ee=(ep[0]-em[0])/20, en=(ep[1]-em[1])/20;
            double ne=(np[0]-nm[0])/20, nn=(np[1]-nm[1])/20;
            double scale=(Math.Sqrt(ee*ee+en*en)+Math.Sqrt(ne*ne+nn*nn))*.5;
            double angle=Math.Atan2(ne-en,ee+nn);
            var parent=new GameObject("JIDO WGS84 aligned map").transform;
            // The map's existing world coordinates are native grid offsets.
            map.SetParent(parent,true);
            parent.rotation=Quaternion.Euler(0,(float)(angle/rad),0);
            parent.localScale=new Vector3((float)scale,1,(float)scale);
            double maximum=0;
            for(int i=-1;i<=1;i++) for(int j=-1;j<=1;j++) {
                double e=i*1000,n=j*1000; var expected=Project(e,n);
                double actualE=scale*(Math.Cos(angle)*e+Math.Sin(angle)*n);
                double actualN=scale*(-Math.Sin(angle)*e+Math.Cos(angle)*n);
                maximum=Math.Max(maximum,Math.Sqrt(Math.Pow(actualE-expected[0],2)+Math.Pow(actualN-expected[1],2)));
            }
            if(maximum>.15) throw new InvalidOperationException("Map alignment residual exceeds 15 cm: "+maximum);
            var report=new Report {utmZone=zone,north=north,utmEasting=easting,utmNorthing=northing,
                lat=lat,lon=lon,alt=alt,unityYawDeg=angle/rad,horizontalScale=scale,maximumHorizontalErrorM=maximum};
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/jido-map-alignment.json",JsonUtility.ToJson(report,true));
            return parent;
        }
    }
}
