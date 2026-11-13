#!/usr/bin/env python3
"""
Wadsworth city blockout generator.

Turns real map data into a low-poly OBJ that Unity can import:
  * Streets   : OpenStreetMap highways -> flat ribbons (width by road class)
  * Buildings : footprints extruded to boxes (OSM where mapped, otherwise
                Microsoft Global ML Building Footprints, which carry a height estimate)
  * Parking lots and water as flat shapes, plus one ground plane.

Only the Python standard library is needed. No Blender.

Usage (from the repo root):
  python Tools/CityBlockout/build_blockout.py                 # use cached data in Tools/CityBlockout/data
  python Tools/CityBlockout/build_blockout.py --fetch         # re-download data first (needs internet)
  python Tools/CityBlockout/build_blockout.py --half-width 600 --north 300 --south -1200   # bigger area

Output (default Tools/CityBlockout/out/):
  wadsworth_blockout.obj + .mtl   the mesh (one object per building, so you can delete/replace any of them)
  wadsworth_buildings.json        per-building footprint, oriented box and height (for placing kit prefabs later)
  wadsworth_preview.png           top-down preview (only if Pillow is installed)

Coordinates: metres. Origin (0,0,0) = Route 94 centreline in front of the match factory.
In Unity after import: +X = east, +Z = north, +Y = up (the OBJ is written so Unity's
OBJ importer, which mirrors X, lands it the right way round).

Data licences: OpenStreetMap data (c) OpenStreetMap contributors, ODbL.
Microsoft Global ML Building Footprints, ODbL. Keep the credit in the game's credits.
"""
import argparse
import gzip
import json
import math
import os
import sys
import urllib.request
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")

# Route 94 (Main Street) in front of the Ohio Match / Blue Tip factory, Wadsworth OH.
ORIGIN_LAT = 41.02069
ORIGIN_LON = -81.72969

# Download box (a bit larger than the default build area).
FETCH_BBOX = (-81.745, 41.015, -81.715, 41.045)  # minlon, minlat, maxlon, maxlat
MS_QUADKEY = "030223233"  # Microsoft dataset tile that contains Wadsworth

# Road class -> (width in metres, height above ground, material)
ROAD_CLASSES = {
    "primary": (10.0, 0.06, "RoadMain"),
    "secondary": (10.0, 0.06, "RoadMain"),
    "tertiary": (9.0, 0.05, "RoadStreet"),
    "unclassified": (7.0, 0.04, "RoadStreet"),
    "residential": (7.0, 0.04, "RoadStreet"),
    "living_street": (6.0, 0.04, "RoadStreet"),
    "service": (4.0, 0.03, "RoadService"),
}
SERVICE_WIDTHS = {"parking_aisle": 5.0, "driveway": 3.0, "alley": 4.0}

DEFAULT_HEIGHT = {
    "house": 7.0, "detached": 7.0, "garage": 3.0, "shed": 2.5, "retail": 5.0,
    "commercial": 6.0, "industrial": 9.0, "warehouse": 8.0, "school": 10.0,
    "church": 12.0, "apartments": 10.0,
}
LEVEL_HEIGHT = 3.2
MIN_HEIGHT, MAX_HEIGHT = 2.5, 25.0

MATERIALS = {
    "Ground": (0.16, 0.20, 0.13),
    "RoadMain": (0.18, 0.18, 0.19),
    "RoadStreet": (0.24, 0.24, 0.25),
    "RoadService": (0.32, 0.31, 0.30),
    "Parking": (0.30, 0.30, 0.31),
    "Water": (0.10, 0.17, 0.22),
    "Building": (0.55, 0.52, 0.48),
    "BuildingLandmark": (0.60, 0.33, 0.26),
    "Roof": (0.25, 0.24, 0.24),
}


# --------------------------------------------------------------------------- data

def fetch(args):
    os.makedirs(DATA, exist_ok=True)
    minlon, minlat, maxlon, maxlat = FETCH_BBOX
    url = f"https://api.openstreetmap.org/api/0.6/map?bbox={minlon},{minlat},{maxlon},{maxlat}"
    print("Downloading OSM:", url)
    req = urllib.request.Request(url, headers={"User-Agent": "carhorgam-blockout/1.0"})
    with urllib.request.urlopen(req, timeout=180) as r, open(os.path.join(DATA, "osm_wadsworth.osm"), "wb") as f:
        f.write(r.read())

    links = "https://minedbuildings.z5.web.core.windows.net/global-buildings/dataset-links.csv"
    print("Looking up Microsoft footprint tile", MS_QUADKEY)
    tile_url = None
    with urllib.request.urlopen(links, timeout=180) as r:
        for line in r.read().decode().splitlines():
            parts = line.split(",")
            if len(parts) > 2 and parts[0] == "UnitedStates" and parts[1] == MS_QUADKEY:
                tile_url = parts[2]
    if not tile_url:
        sys.exit("Could not find the Microsoft footprint tile.")
    print("Downloading (about 60 MB, filtered down to Wadsworth):", tile_url)
    tmp = os.path.join(DATA, "_ms_tile.csv.gz")
    urllib.request.urlretrieve(tile_url, tmp)
    keep = []
    with gzip.open(tmp, "rt") as f:
        for line in f:
            feat = json.loads(line)
            ring = feat["geometry"]["coordinates"][0]
            lon, lat = ring[0]
            if minlon < lon < maxlon and minlat < lat < maxlat:
                keep.append(feat)
    os.remove(tmp)
    with open(os.path.join(DATA, "ms_buildings_wadsworth.json"), "w") as f:
        json.dump(keep, f)
    print("Kept", len(keep), "Microsoft footprints")


K_LON = 111320.0 * math.cos(math.radians(ORIGIN_LAT))
K_LAT = 111320.0


def to_xy(lat, lon):
    """lat/lon -> local metres (x east, y north)."""
    return ((lon - ORIGIN_LON) * K_LON, (lat - ORIGIN_LAT) * K_LAT)


def load_osm():
    root = ET.parse(os.path.join(DATA, "osm_wadsworth.osm")).getroot()
    nodes = {n.get("id"): to_xy(float(n.get("lat")), float(n.get("lon"))) for n in root.iter("node")}
    ways = []
    for w in root.iter("way"):
        tags = {t.get("k"): t.get("v") for t in w.iter("tag")}
        refs = [nd.get("ref") for nd in w.iter("nd")]
        if all(r in nodes for r in refs):
            ways.append((w.get("id"), tags, refs))
    return nodes, ways


# --------------------------------------------------------------------------- geometry helpers

def signed_area(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
                     for i in range(len(poly)))


def clean_ring(pts):
    out = []
    for p in pts:
        if not out or (abs(p[0] - out[-1][0]) > 0.05 or abs(p[1] - out[-1][1]) > 0.05):
            out.append(p)
    if len(out) > 1 and abs(out[0][0] - out[-1][0]) < 0.05 and abs(out[0][1] - out[-1][1]) < 0.05:
        out.pop()
    # drop collinear points
    changed = True
    while changed and len(out) > 3:
        changed = False
        for i in range(len(out)):
            a, b, c = out[i - 1], out[i], out[(i + 1) % len(out)]
            if abs((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) < 0.02:
                out.pop(i)
                changed = True
                break
    if signed_area(out) < 0:
        out.reverse()  # counter-clockwise (seen from above)
    return out


def point_in_tri(p, a, b, c):
    def s(p1, p2, p3):
        return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])
    d1, d2, d3 = s(p, a, b), s(p, b, c), s(p, c, a)
    neg = d1 < 0 or d2 < 0 or d3 < 0
    pos = d1 > 0 or d2 > 0 or d3 > 0
    return not (neg and pos)


def triangulate(poly):
    """Ear clipping for a simple CCW polygon. Returns index triples."""
    idx = list(range(len(poly)))
    tris = []
    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = poly[i0], poly[i1], poly[i2]
            if (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]) <= 1e-9:
                continue  # reflex
            if any(point_in_tri(poly[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            break
        else:
            break  # degenerate polygon: fan the rest
    for k in range(1, len(idx) - 1):
        tris.append((idx[0], idx[k], idx[k + 1]))
    return tris


def point_in_poly(p, poly):
    inside = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if (yi > p[1]) != (yj > p[1]) and p[0] < (xj - xi) * (p[1] - yi) / (yj - yi + 1e-12) + xi:
            inside = not inside
        j = i
    return inside


def centroid(poly):
    return (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))


def oriented_box(poly):
    """Smallest-area rectangle aligned to one of the polygon's edges: centre, size, angle(deg from +X)."""
    best = None
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        c, s = math.cos(-ang), math.sin(-ang)
        rx = [p[0] * c - p[1] * s for p in poly]
        ry = [p[0] * s + p[1] * c for p in poly]
        w, h = max(rx) - min(rx), max(ry) - min(ry)
        if best is None or w * h < best[0]:
            mx, my = (max(rx) + min(rx)) / 2, (max(ry) + min(ry)) / 2
            cc, ss = math.cos(ang), math.sin(ang)
            best = (w * h, (mx * cc - my * ss, mx * ss + my * cc), (w, h), math.degrees(ang))
    return best[1], best[2], best[3]


# --------------------------------------------------------------------------- OBJ writer

class Obj:
    """Collects geometry in world space (x east, y up, z north) and writes an OBJ.

    Unity's OBJ importer mirrors X, so we store x as -east. Each triangle is wound
    so its right-handed normal points the way we ask (outward), which is what
    Unity expects after its own winding flip.
    """

    def __init__(self):
        self.v = []
        self.objects = []  # (name, [(material, [tri vertex-index triples]), ...])

    def begin(self, name, material):
        self.objects.append((name, [(material, [])]))

    def use(self, material):
        """Switch material inside the current object (one GameObject, several submeshes)."""
        self.objects[-1][1].append((material, []))

    def tri(self, a, b, c, want_normal):
        A, B, C = [(-p[0], p[1], p[2]) for p in (a, b, c)]  # file space
        W = (-want_normal[0], want_normal[1], want_normal[2])
        u = (B[0] - A[0], B[1] - A[1], B[2] - A[2])
        v = (C[0] - A[0], C[1] - A[1], C[2] - A[2])
        n = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
        if n[0] * W[0] + n[1] * W[1] + n[2] * W[2] < 0:
            B, C = C, B
        base = len(self.v)
        self.v.extend([A, B, C])
        self.objects[-1][1][-1][1].append((base + 1, base + 2, base + 3))

    def quad(self, a, b, c, d, want_normal):
        self.tri(a, b, c, want_normal)
        self.tri(a, c, d, want_normal)

    def flat_poly(self, ring, y, tris=None):
        tris = tris or triangulate(ring)
        for i, j, k in tris:
            self.tri((ring[i][0], y, ring[i][1]), (ring[j][0], y, ring[j][1]), (ring[k][0], y, ring[k][1]), (0, 1, 0))

    def write(self, path):
        mtl = os.path.splitext(os.path.basename(path))[0] + ".mtl"
        with open(os.path.join(os.path.dirname(path), mtl), "w") as f:
            for name, rgb in MATERIALS.items():
                f.write(f"newmtl {name}\nKd {rgb[0]:.3f} {rgb[1]:.3f} {rgb[2]:.3f}\nKa 0 0 0\nKs 0 0 0\nd 1\nillum 1\n\n")
        # weld duplicate vertices to keep the file small
        remap, uniq, index = {}, [], []
        for p in self.v:
            key = (round(p[0], 2), round(p[1], 3), round(p[2], 2))
            if key not in remap:
                remap[key] = len(uniq) + 1
                uniq.append(key)
            index.append(remap[key])
        with open(path, "w") as f:
            f.write("# Wadsworth blockout. Map data (c) OpenStreetMap contributors (ODbL); "
                    "Microsoft Global ML Building Footprints (ODbL).\n")
            f.write(f"mtllib {mtl}\n")
            for p in uniq:
                f.write(f"v {p[0]:.2f} {p[1]:.3f} {p[2]:.2f}\n")
            for name, parts in self.objects:
                if not any(tris for _, tris in parts):
                    continue
                f.write(f"o {name}\n")
                for mat, tris in parts:
                    if not tris:
                        continue
                    f.write(f"usemtl {mat}\n")
                    for t in tris:
                        f.write(f"f {index[t[0] - 1]} {index[t[1] - 1]} {index[t[2] - 1]}\n")
        return len(uniq), sum(len(t) for _, parts in self.objects for _, t in parts)


# --------------------------------------------------------------------------- builders

def in_area(p, area, pad=0.0):
    x0, x1, y0, y1 = area
    return x0 - pad <= p[0] <= x1 + pad and y0 - pad <= p[1] <= y1 + pad


def road_ribbon(obj, pts, width, y):
    """Flat strip along a polyline with mitred joins."""
    n = len(pts)
    if n < 2:
        return
    half = width / 2
    left, right = [], []
    for i in range(n):
        if i == 0:
            dx, dy = pts[1][0] - pts[0][0], pts[1][1] - pts[0][1]
        elif i == n - 1:
            dx, dy = pts[-1][0] - pts[-2][0], pts[-1][1] - pts[-2][1]
        else:
            ax, ay = pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]
            bx, by = pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]
            la, lb = math.hypot(ax, ay) or 1, math.hypot(bx, by) or 1
            dx, dy = ax / la + bx / lb, ay / la + by / lb
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L, dx / L
        scale = 1.0
        if 0 < i < n - 1:
            ax, ay = pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]
            la = math.hypot(ax, ay) or 1
            cos_half = abs(nx * (-ay / la) + ny * (ax / la))
            scale = min(1.0 / max(cos_half, 0.35), 2.5)
        left.append((pts[i][0] + nx * half * scale, pts[i][1] + ny * half * scale))
        right.append((pts[i][0] - nx * half * scale, pts[i][1] - ny * half * scale))
    for i in range(n - 1):
        a, b, c, d = right[i], right[i + 1], left[i + 1], left[i]
        obj.quad((a[0], y, a[1]), (b[0], y, b[1]), (c[0], y, c[1]), (d[0], y, d[1]), (0, 1, 0))


def disc(obj, c, r, y, sides=10):
    ring = [(c[0] + r * math.cos(2 * math.pi * i / sides), c[1] + r * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    obj.flat_poly(ring, y)


def clip_polyline(pts, area, pad):
    """Keep runs of a polyline that touch the area (segment-level, not exact clipping)."""
    runs, cur = [], []
    for i in range(len(pts)):
        inside = in_area(pts[i], area, pad)
        prev_in = i > 0 and in_area(pts[i - 1], area, pad)
        next_in = i < len(pts) - 1 and in_area(pts[i + 1], area, pad)
        if inside or prev_in or next_in:
            cur.append(pts[i])
        elif cur:
            runs.append(cur)
            cur = []
    if cur:
        runs.append(cur)
    return [r for r in runs if len(r) >= 2]


def build(args):
    area = (-args.half_width, args.half_width, args.south, args.north)
    nodes, ways = load_osm()
    with open(os.path.join(DATA, "ms_buildings_wadsworth.json")) as f:
        ms = json.load(f)

    obj = Obj()

    # Ground
    x0, x1, y0, y1 = area
    obj.begin("Ground", "Ground")
    obj.quad((x0, 0, y0), (x1, 0, y0), (x1, 0, y1), (x0, 0, y1), (0, 1, 0))

    # Roads
    node_use = {}
    road_count = 0
    groups = {"RoadMain": [], "RoadStreet": [], "RoadService": []}
    for wid, tags, refs in ways:
        hw = tags.get("highway")
        if hw not in ROAD_CLASSES or tags.get("area") == "yes":
            continue
        width, y, mat = ROAD_CLASSES[hw]
        if hw == "service":
            width = SERVICE_WIDTHS.get(tags.get("service"), width)
        if "width" in tags:
            try:
                width = float(tags["width"].split()[0])
            except ValueError:
                pass
        pts = [nodes[r] for r in refs]
        runs = clip_polyline(pts, area, 0)
        if not runs:
            continue
        groups[mat].append((tags.get("name", hw), runs, width, y))
        for r in refs:
            if in_area(nodes[r], area):
                node_use.setdefault(r, []).append((width, y))
    for mat, items in groups.items():
        obj.begin(mat, mat)
        for _, runs, width, y in items:
            for run in runs:
                road_ribbon(obj, run, width, y)
                road_count += 1
    # Intersection patches hide the seams where ribbons meet
    obj.begin("Intersections", "RoadStreet")
    for r, uses in node_use.items():
        if len(uses) >= 2:
            w = max(u[0] for u in uses)
            y = max(u[1] for u in uses) + 0.005
            disc(obj, nodes[r], w * 0.55, y)

    # Parking lots and water
    obj.begin("Parking", "Parking")
    for wid, tags, refs in ways:
        if tags.get("amenity") == "parking" and refs[0] == refs[-1]:
            ring = clean_ring([nodes[r] for r in refs])
            if len(ring) >= 3 and any(in_area(p, area) for p in ring):
                obj.flat_poly(ring, 0.02)
    obj.begin("Water", "Water")
    for wid, tags, refs in ways:
        if (tags.get("natural") == "water" or "water" in tags) and refs[0] == refs[-1]:
            ring = clean_ring([nodes[r] for r in refs])
            if len(ring) >= 3 and any(in_area(p, area) for p in ring):
                obj.flat_poly(ring, 0.01)

    # Buildings: OSM first (better shapes, names), then Microsoft where OSM has nothing
    buildings = []
    osm_polys = []
    for wid, tags, refs in ways:
        if "building" not in tags or refs[0] != refs[-1]:
            continue
        ring = clean_ring([nodes[r] for r in refs])
        if len(ring) < 3 or not in_area(centroid(ring), area):
            continue
        h = None
        if "height" in tags:
            try:
                h = float(tags["height"].split()[0])
            except ValueError:
                pass
        if h is None and "building:levels" in tags:
            try:
                h = float(tags["building:levels"]) * LEVEL_HEIGHT
            except ValueError:
                pass
        osm_polys.append(ring)
        buildings.append({"src": "osm", "id": wid, "ring": ring, "height": h, "type": tags["building"],
                          "name": tags.get("name") or " ".join(x for x in (tags.get("addr:housenumber"), tags.get("addr:street")) if x)})
    for i, feat in enumerate(ms):
        ring = clean_ring([to_xy(lat, lon) for lon, lat in feat["geometry"]["coordinates"][0]])
        if len(ring) < 3 or abs(signed_area(ring)) < 12:
            continue
        c = centroid(ring)
        if not in_area(c, area):
            continue
        if any(point_in_poly(c, p) or point_in_poly(centroid(p), ring) for p in osm_polys):
            continue  # already have it from OSM
        h = feat["properties"].get("height", -1)
        buildings.append({"src": "ms", "id": f"ms{i}", "ring": ring, "height": h if h and h > 0 else None,
                          "type": "yes", "name": ""})
    # Fill missing OSM heights from overlapping Microsoft data, then defaults
    for b in buildings:
        if b["height"] is None:
            area_m2 = abs(signed_area(b["ring"]))
            b["height"] = DEFAULT_HEIGHT.get(b["type"], 9.0 if area_m2 > 1500 else 6.0)
        b["height"] = max(MIN_HEIGHT, min(MAX_HEIGHT, b["height"]))

    landmarks = set(args.landmark or [])
    sidecar = []
    for n, b in enumerate(sorted(buildings, key=lambda b: (-abs(signed_area(b["ring"]))))):
        ring, h = b["ring"], b["height"]
        label = "".join(ch if ch.isalnum() else "_" for ch in b["name"])[:40].strip("_")
        name = f"Bldg_{n:04d}" + (f"_{label}" if label else "")
        mat = "BuildingLandmark" if n in landmarks else "Building"
        obj.begin(name, mat)
        cx, cy = centroid(ring)
        for i in range(len(ring)):
            a, c2 = ring[i], ring[(i + 1) % len(ring)]
            ex, ey = c2[0] - a[0], c2[1] - a[1]
            outward = (ey, 0, -ex)  # right of the edge for a CCW ring = outside
            obj.quad((a[0], 0, a[1]), (c2[0], 0, c2[1]), (c2[0], h, c2[1]), (a[0], h, a[1]), outward)
        obj.use("Roof")
        obj.flat_poly(ring, h)
        (bx, by), (bw, bd), ang = oriented_box(ring)
        sidecar.append({
            "name": name, "source": b["src"], "osmOrMsId": b["id"], "label": b["name"], "osmType": b["type"],
            "height": round(h, 2), "footprintArea": round(abs(signed_area(ring)), 1),
            "center": [round(bx, 2), round(by, 2)], "size": [round(bw, 2), round(bd, 2)],
            "yawDegreesFromEast": round(ang, 2),
            "footprint": [[round(p[0], 2), round(p[1], 2)] for p in ring],
        })

    os.makedirs(args.out, exist_ok=True)
    obj_path = os.path.join(args.out, "wadsworth_blockout.obj")
    nv, nt = obj.write(obj_path)
    with open(os.path.join(args.out, "wadsworth_buildings.json"), "w") as f:
        json.dump({"origin": {"lat": ORIGIN_LAT, "lon": ORIGIN_LON, "note": "x east, y north, metres"},
                   "area": {"xMin": x0, "xMax": x1, "yMin": y0, "yMax": y1}, "buildings": sidecar}, f, indent=1)
    print(f"Roads: {road_count} segments, buildings: {len(buildings)} "
          f"({sum(b['src'] == 'osm' for b in buildings)} OSM, {sum(b['src'] == 'ms' for b in buildings)} Microsoft)")
    print(f"OBJ: {nv} vertices, {nt} triangles -> {obj_path} ({os.path.getsize(obj_path) / 1e6:.1f} MB)")

    preview(args, area, groups, buildings, nodes, ways)


def preview(args, area, groups, buildings, nodes, ways):
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        print("Pillow not installed: skipping preview PNG")
        return
    x0, x1, y0, y1 = area
    s = args.preview_scale
    W, H = int((x1 - x0) * s), int((y1 - y0) * s)
    img = Image.new("RGB", (W, H), (40, 52, 34))
    d = ImageDraw.Draw(img)

    def px(p):
        return ((p[0] - x0) * s, (y1 - p[1]) * s)

    for wid, tags, refs in ways:
        if refs[0] != refs[-1]:
            continue
        if tags.get("amenity") == "parking":
            d.polygon([px(nodes[r]) for r in refs], fill=(70, 72, 74))
        elif tags.get("natural") == "water" or "water" in tags:
            d.polygon([px(nodes[r]) for r in refs], fill=(30, 55, 75))
    colors = {"RoadMain": (25, 25, 28), "RoadStreet": (60, 60, 64), "RoadService": (85, 83, 80)}
    for mat in ("RoadService", "RoadStreet", "RoadMain"):
        for _, runs, width, _ in groups[mat]:
            for run in runs:
                d.line([px(p) for p in run], fill=colors[mat], width=max(1, int(width * s)), joint="curve")
    for b in buildings:
        col = (200, 120, 90) if b["src"] == "osm" else (170, 165, 155)
        d.polygon([px(p) for p in b["ring"]], fill=col, outline=(20, 20, 20))
    o = px((0, 0))
    d.ellipse([o[0] - 6, o[1] - 6, o[0] + 6, o[1] + 6], outline=(255, 220, 0), width=3)
    for wid, tags, refs in ways:
        if tags.get("highway") in ROAD_CLASSES and tags.get("name") and tags.get("highway") != "service":
            mid = nodes[refs[len(refs) // 2]]
            if in_area(mid, area):
                d.text(px(mid), tags["name"], fill=(240, 240, 200))
    path = os.path.join(args.out, "wadsworth_preview.png")
    img.save(path)
    print("Preview:", path)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--fetch", action="store_true", help="download fresh OSM + Microsoft data first")
    ap.add_argument("--half-width", type=float, default=400, help="metres east and west of Route 94 (default 400)")
    ap.add_argument("--north", type=float, default=560, help="metres north of the origin (default 560, past High/Broad)")
    ap.add_argument("--south", type=float, default=-420, help="metres south of the origin (default -420, past State St)")
    ap.add_argument("--landmark", type=int, action="append", help="building index to colour as a landmark")
    ap.add_argument("--preview-scale", type=float, default=1.5, help="pixels per metre in the preview")
    ap.add_argument("--out", default=os.path.join(HERE, "out"))
    args = ap.parse_args()
    if args.fetch:
        fetch(args)
    build(args)


if __name__ == "__main__":
    main()
