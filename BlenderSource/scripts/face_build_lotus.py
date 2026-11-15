import bpy, bmesh, math
from mathutils import Vector

def sm(x, a, b):
    t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

def g2(x, z, cx, cz, rx, rz):
    return math.exp(-(((x - cx) / rx) ** 2 + ((z - cz) / rz) ** 2))

def lerp_tab(tab, z):
    if z >= tab[0][0]: return tab[0][1]
    for (z0, a), (z1, b) in zip(tab, tab[1:]):
        if z1 <= z <= z0: return a + (b - a) * (z0 - z) / (z0 - z1)
    return tab[-1][1]

DEPTH = [(1.71, 0.082), (1.70, 0.087), (1.68, 0.092), (1.66, 0.093), (1.64, 0.092), (1.62, 0.090), (1.60, 0.086),
         (1.59, 0.083), (1.575, 0.076), (1.56, 0.066), (1.55, 0.056), (1.54, 0.045)]

FEATURES = [  # (cx, cz, rx, rz, forward amplitude, symmetric)
    (0.030, 1.684, 0.024, 0.006, 0.0035, True),    # brow ridge
    (0.0, 1.676, 0.010, 0.008, 0.0015, False),     # glabella
    (0.032, 1.657, 0.020, 0.013, -0.0040, True),   # socket
    (0.050, 1.640, 0.016, 0.011, 0.0040, True),    # cheekbone
    (0.046, 1.606, 0.014, 0.012, -0.0018, True),   # cheek hollow
    (0.064, 1.672, 0.010, 0.014, -0.0015, True),   # temple
    (0.0, 1.6235, 0.0075, 0.0075, 0.0105, False),  # nose tip
    (0.0098, 1.6185, 0.0055, 0.0050, 0.0045, True),  # alar wings
    (0.0060, 1.6140, 0.0030, 0.0022, -0.0015, True),  # nostril
    (0.0, 1.6115, 0.0045, 0.0025, -0.0012, False),  # columella base
    (0.0042, 1.6035, 0.0022, 0.0045, 0.0008, True),  # philtrum ridges
    (0.0, 1.5985, 0.0155, 0.0040, 0.0050, False),  # upper lip
    (0.0, 1.5870, 0.0135, 0.0046, 0.0060, False),  # lower lip
    (0.019, 1.5925, 0.0028, 0.0028, -0.0020, True),  # mouth corners
    (0.0, 1.5785, 0.0110, 0.0028, -0.0018, False),  # mentolabial crease
    (0.0, 1.5630, 0.0120, 0.0085, 0.0055, False),  # chin
]

R = 0.0118; EX = 0.0315; EZ = 1.657; EY = -0.0905 + R; A = 0.0128


def build_face(h, subdiv=True):
    me = h.data
    bm = bmesh.new(); bm.from_mesh(me)
    ma = bm.faces.layers.int.get("matid")
    ev = {v for f in bm.faces if f[ma] == 1 for v in f.verts}
    bmesh.ops.delete(bm, geom=list(ev), context='VERTS')
    if subdiv:
        for _ in range(2):
            sel = [f for f in bm.faces if (lambda c: c.y < -0.035 and 1.55 < c.z < 1.715 and abs(c.x) < 0.075)(f.calc_center_median())]
            bmesh.ops.subdivide_edges(bm, edges=list({e for f in sel for e in f.edges}), cuts=1, use_grid_fill=True, smooth=0.0)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    # 0) V-line jaw
    for v in bm.verts:
        if v.co.z < 1.645: v.co.x *= 1 - 0.14 * sm(1.645 - v.co.z, 0.0, 0.08)
    # 1) analytic face base, with half-width smoothed over z
    zs = {}
    for v in bm.verts:
        k = round(v.co.z * 500) / 500; zs[k] = max(zs.get(k, 0), abs(v.co.x))
    keys = sorted(zs)
    def W(z):
        vals = [zs[k] for k in keys if abs(k - z) <= 0.006]
        return sum(vals) / len(vals) if vals else 0.07
    Wc = {}
    p = 2.6
    for v in bm.verts:
        x, y, z = v.co
        if y > -0.02 or not (1.54 < z < 1.715): continue
        kz = round(z, 3); w_ = Wc.get(kz) or Wc.setdefault(kz, W(z))
        u = min(1.0, abs(x) / w_)
        yb = -lerp_tab(DEPTH, z) * (1 - u ** p) ** (1 / p)
        w = (1 - sm(u, 0.62, 0.9)) * sm(z, 1.54, 1.555) * (1 - sm(z, 1.70, 1.715))
        if w > 0: v.co.y = y + (yb - y) * w
    # smooth y in the face region to kill banding
    reg = [v for v in bm.verts if v.co.y < -0.03 and 1.545 < v.co.z < 1.71 and abs(v.co.x) < 0.07]
    for _ in range(12):
        new = {}
        for v in reg:
            nb = [e.other_vert(v) for e in v.link_edges]
            new[v] = v.co.y + (sum(n.co.y for n in nb) / len(nb) - v.co.y) * 0.5
        for v, y in new.items(): v.co.y = y
    # 2) features
    for v in reg:
        x, y, z = v.co; d = 0.0
        for cx, cz, rx, rz, a, s in FEATURES:
            d += a * g2(x, z, cx, cz, rx, rz)
            if s: d += a * g2(x, z, -cx, cz, rx, rz)
        if 1.622 < z < 1.676:
            t = (1.676 - z) / (1.676 - 1.622)
            d += (0.0025 + 0.0085 * t * t) * math.exp(-(x / (0.0055 + 0.002 * t)) ** 2)
        if abs(x) < 0.022:
            zc = 1.5925 + 0.0009 * (1 - (x / 0.019) ** 2) - 0.0005 * math.exp(-(x / 0.004) ** 2)
            d -= 0.0035 * math.exp(-((z - zc) / 0.0011) ** 2) * (1 - sm(abs(x), 0.017, 0.021))
        v.co.y = y - d
    # 3) lids wrapped on the eyeball
    lay = bm.verts.layers.float.get("lash") or bm.verts.layers.float.new("lash")
    for v in bm.verts: v[lay] = 0.0
    for v in bm.verts:
        x, y, z = v.co
        for s in (1, -1):
            dx = x - s * EX; dz = z - EZ
            if abs(dx) > 0.026 or abs(dz) > 0.017 or y > -0.06: continue
            tout = (dx * s) / A
            dzc = dz - 0.0013 * tout
            if abs(tout) < 1:
                tt = tout + 0.10
                up = 0.0066 * max(0, 1 - tt * tt) ** 0.6; lo = -0.0038 * max(0, 1 - (tout - 0.1) ** 2) ** 0.8
                if lo < dzc < up: delta = -min(up - dzc, dzc - lo); upper = dzc > (up + lo) / 2
                elif dzc >= up: delta = dzc - up; upper = True
                else: delta = lo - dzc; upper = False
            else:
                delta = math.hypot(abs(dx) - A, dzc); upper = dzc >= 0
            r2 = dx * dx + dz * dz
            if delta < 0:
                # tuck inside the eyeball, rounded so the lid edge rolls in
                v.co.y = EY - math.sqrt(max(0, R * R - r2)) * (0.85 - 0.6 * sm(-delta, 0, 0.002))
            else:
                th = 0.0009 + 0.0011 * sm(delta, 0, 0.004)
                rr = (R + th) ** 2
                yt = EY - math.sqrt(rr - r2) if r2 < rr else y
                w = 1 - sm(delta, 0.0025, 0.0085)
                v.co.y = y * (1 - w) + min(yt, y + 0.002) * w if yt > y else y * (1 - w) + yt * w
                if upper: v.co.y += 0.0007 * math.exp(-((delta - 0.0058) / 0.0012) ** 2)
                lv = math.exp(-(delta / 0.0010) ** 2) * (1.0 if upper else 0.3)
                if upper and tout > 0.6: lv = max(lv, math.exp(-(delta / 0.0016) ** 2) * sm(tout, 0.6, 1.15))
                v[lay] = max(v[lay], min(1.0, lv))
    # 4) eyeballs
    dl = bm.verts.layers.deform.verify(); gi = h.vertex_groups["DEF-spine.006"].index
    ma = bm.faces.layers.int.get("matid")
    for s in (1, -1):
        ret = bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=R)
        for vv in ret['verts']:
            vv.co += Vector((s * EX, EY, EZ)); vv[dl].clear(); vv[dl][gi] = 1.0
        for f in {f for vv in ret['verts'] for f in vv.link_faces}:
            f[ma] = 1; f.material_index = 1
    bm.to_mesh(me); bm.free()
    for pl in me.polygons: pl.use_smooth = True
    me.update()
    return len(me.vertices)
