using System.Collections.Generic;
using UnityEngine;

/// <summary>D088 区域的纯本地几何校验/凹多边形剖分；不依赖编辑器、配置或物理世界。</summary>
public static class DLevel88ZoneGeometry
{
    #region 简单多边形校验和耳切
    private const float Epsilon = 0.0000001f;
    private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) => Mathf.Abs(Cross(a,b,p)) <= Epsilon && Vector2.Dot(p-a,p-b) <= Epsilon;
    private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (OnSegment(a,b,c) || OnSegment(a,b,d) || OnSegment(c,d,a) || OnSegment(c,d,b)) return true;
        return Cross(a,b,c)*Cross(a,b,d)<0 && Cross(c,d,a)*Cross(c,d,b)<0;
    }

    /// <summary>支持顺/逆时针和凹轮廓；重复点、自交、退化或超限返回 false，不修改输入。</summary>
    public static bool TryTriangulate(Vector2[] p, out int[] triangles)
    {
        triangles = new int[0];
        if (p == null || p.Length < 3 || p.Length > 64) return false;
        float area = 0;
        for (int i=0;i<p.Length;i++)
        {
            if (float.IsNaN(p[i].x) || float.IsNaN(p[i].y) || float.IsInfinity(p[i].x) || float.IsInfinity(p[i].y)) return false;
            var next = p[(i+1)%p.Length]; area += p[i].x*next.y-next.x*p[i].y;
            for (int j=i+1;j<p.Length;j++)
            {
                if ((p[i]-p[j]).sqrMagnitude < Epsilon*Epsilon) return false;
                if (j==i+1 || (i==0 && j==p.Length-1)) continue;
                if (Intersects(p[i],next,p[j],p[(j+1)%p.Length])) return false;
            }
        }
        if (Mathf.Abs(area)<Epsilon) return false;
        var remaining = new List<int>(p.Length);
        for (int i=0;i<p.Length;i++) remaining.Add(area>0 ? i : p.Length-1-i);
        var result = new List<int>((p.Length-2)*3);
        while (remaining.Count>3)
        {
            bool found = false;
            for (int i=0;i<remaining.Count;i++)
            {
                int a=remaining[(i+remaining.Count-1)%remaining.Count], b=remaining[i], c=remaining[(i+1)%remaining.Count];
                if (Cross(p[a],p[b],p[c])<=Epsilon) continue;
                bool occupied = false;
                foreach (int k in remaining)
                    if (k!=a && k!=b && k!=c && Cross(p[a],p[b],p[k])>=-Epsilon && Cross(p[b],p[c],p[k])>=-Epsilon && Cross(p[c],p[a],p[k])>=-Epsilon) { occupied=true; break; }
                if (occupied) continue;
                result.Add(a); result.Add(b); result.Add(c); remaining.RemoveAt(i); found=true; break;
            }
            if (!found) return false;
        }
        result.AddRange(remaining); triangles=result.ToArray(); return true;
    }
    #endregion
}
