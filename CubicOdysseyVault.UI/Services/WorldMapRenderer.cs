using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CubicOdysseyVault.Core.Voxels;

namespace CubicOdysseyVault.UI.Services;

// Renders an aggregated set of voxels (typically all chunks for a save) as an
// isometric Bitmap. Same projection/palette as VoxelRenderer but with
// neighbor-based face culling so it scales — a typical save has ~290k solid
// voxels but only ~30–50k surface faces, an order-of-magnitude reduction.
//
// Coordinate convention: Y is up. The isometric camera looks toward
// (-x, -y, -z) — only top (+Y), +X side, and +Z side faces are visible.
//
// `optionalYRange` clips voxels by world-Y for the layer slider; pass null
// to render everything.
public static class WorldMapRenderer
{
    private const double SqrtThreeOverTwo = 0.8660254037844387;

    public sealed record Input(IReadOnlyList<WorldVoxel> Voxels);
    public readonly record struct WorldVoxel(int X, int Y, int Z, uint BlockId);

    // Top-down "satellite" render. For each (X, Z) cell, picks the
    // highest-Y solid voxel and paints it with the type-byte palette,
    // optionally dimmed by height (lower Y → darker). This is far more
    // readable than isometric for a multi-chunk world because it doesn't
    // project Y onto the screen at all — every chunk's content tiles into
    // a true 2D plan view of one chunk's terrain.
    public static Bitmap RenderTopDown(Input input, int width, int height,
        (int min, int max)? optionalYRange = null,
        bool shadeByHeight = true)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using var ctx = rtb.CreateDrawingContext();
        ctx.FillRectangle(Brushes.Black, new Rect(0, 0, width, height));
        if (input.Voxels.Count == 0) return rtb;

        // Build a per-(X,Z) "topmost" map. With ~290k voxels split across
        // (X, Z) up to 256² per chunk this is bounded; using a Dictionary
        // keyed by (x, z) keeps memory linear in the number of distinct
        // surface columns, not in chunk volume.
        var top = new Dictionary<(int x, int z), (int y, uint blockId)>();
        int xMin = int.MaxValue, xMax = int.MinValue;
        int zMin = int.MaxValue, zMax = int.MinValue;
        int yMin = int.MaxValue, yMax = int.MinValue;

        foreach (var v in input.Voxels)
        {
            if (optionalYRange is { } r && (v.Y < r.min || v.Y > r.max)) continue;
            var key = (v.X, v.Z);
            if (!top.TryGetValue(key, out var current) || v.Y > current.y)
                top[key] = (v.Y, v.BlockId);
            if (v.X < xMin) xMin = v.X; if (v.X > xMax) xMax = v.X;
            if (v.Z < zMin) zMin = v.Z; if (v.Z > zMax) zMax = v.Z;
            if (v.Y < yMin) yMin = v.Y; if (v.Y > yMax) yMax = v.Y;
        }
        if (top.Count == 0) return rtb;

        // Fit the X-Z plane into the bitmap with a small margin.
        int xs = xMax - xMin + 1, zs = zMax - zMin + 1;
        double margin = 4.0;
        double cellSize = Math.Min((width - 2 * margin) / xs, (height - 2 * margin) / zs);
        if (cellSize <= 0) cellSize = 1;
        // Whole-pixel cells avoid sub-pixel seams between adjacent voxels.
        double pxSize = Math.Max(1, Math.Floor(cellSize));
        double offsetX = (width  - xs * pxSize) * 0.5;
        double offsetY = (height - zs * pxSize) * 0.5;

        // Group cells by (typeByte, shade-step) so we can issue one
        // FillRectangle per palette entry instead of per voxel. 8 shade
        // steps keeps palette size bounded (≤ 256 type bytes × 8 = 2048
        // brushes worst case; in practice ~50 type bytes × 8 = 400).
        double yRangeSpan = yMax - yMin == 0 ? 1 : (yMax - yMin);
        var batches = new Dictionary<uint, List<(double x, double z)>>();
        foreach (var entry in top)
        {
            int x = entry.Key.x;
            int z = entry.Key.z;
            int y = entry.Value.y;
            uint blockId = entry.Value.blockId;
            byte typeByte = (byte)(blockId >> 24);
            byte shade = shadeByHeight
                ? (byte)Math.Clamp((int)Math.Round((y - yMin) / yRangeSpan * 7.0), 0, 7)
                : (byte)0;
            uint batchKey = ((uint)typeByte << 8) | shade;
            if (!batches.TryGetValue(batchKey, out var list))
            {
                list = new List<(double, double)>();
                batches[batchKey] = list;
            }
            list.Add((offsetX + (x - xMin) * pxSize, offsetY + (z - zMin) * pxSize));
        }

        foreach (var (batchKey, cells) in batches)
        {
            byte typeByte = (byte)(batchKey >> 8);
            byte shade = (byte)(batchKey & 0xff);
            var baseColor = HashToColor((uint)typeByte);
            // Shade 0 (lowest Y) → 0.55× brightness; shade 7 (highest) → 1.05×
            double tint = 0.55 + (shade / 7.0) * 0.5;
            var brush = new SolidColorBrush(Tint(baseColor, tint));
            foreach (var (px, py) in cells)
                ctx.FillRectangle(brush, new Rect(px, py, pxSize, pxSize));
        }

        return rtb;
    }

    // Empirically the high byte of a vw3 block-info uint32 is the block
    // *type* (e.g. 0xA9, 0x76); the low bytes are variant/flags that we
    // don't yet have semantics for. Bucketing by the type byte alone keeps
    // the palette to ~50–100 colors instead of ~13,000 — adjacent voxels
    // of the same conceptual block now share a hue.
    private static uint PaletteKey(uint blockId) => blockId >> 24;

    public static Bitmap Render(Input input, int width, int height, (int min, int max)? optionalYRange = null)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using var ctx = rtb.CreateDrawingContext();
        ctx.FillRectangle(Brushes.Black, new Rect(0, 0, width, height));

        if (input.Voxels.Count == 0) return rtb;

        // Build a hash set of solid positions (filtered by optional Y range)
        // for O(1) neighbor lookup. With ~290k voxels this is ~10 MB peak —
        // acceptable for the one-shot render path.
        var solid = new HashSet<(int x, int y, int z)>(input.Voxels.Count);
        int xMin = int.MaxValue, xMax = int.MinValue;
        int yMin = int.MaxValue, yMax = int.MinValue;
        int zMin = int.MaxValue, zMax = int.MinValue;
        foreach (var v in input.Voxels)
        {
            if (optionalYRange is { } r && (v.Y < r.min || v.Y > r.max)) continue;
            solid.Add((v.X, v.Y, v.Z));
            if (v.X < xMin) xMin = v.X; if (v.X > xMax) xMax = v.X;
            if (v.Y < yMin) yMin = v.Y; if (v.Y > yMax) yMax = v.Y;
            if (v.Z < zMin) zMin = v.Z; if (v.Z > zMax) zMax = v.Z;
        }
        if (solid.Count == 0) return rtb;

        // Pick a cell size that fits the projected bounding box.
        int xs = xMax - xMin + 1, ys = yMax - yMin + 1, zs = zMax - zMin + 1;
        double projW = (xs + zs) * SqrtThreeOverTwo;
        double projH = (xs + zs) * 0.5 + ys;
        double margin = 8.0;
        double scale = Math.Min((width - 2 * margin) / projW, (height - 2 * margin) / projH);
        if (scale <= 0) scale = 1;

        double cx = width / 2.0;
        double cy = height / 2.0;
        double centerProjX = ((xs - 1) - (zs - 1)) * 0.5 * SqrtThreeOverTwo;
        double centerProjY = ((xs - 1) + (zs - 1)) * 0.25 - (ys - 1) * 0.5;

        Point Project(double x, double y, double z) => new(
            cx + (((x - xMin) - (z - zMin)) * SqrtThreeOverTwo - centerProjX) * scale,
            cy + (((x - xMin) + (z - zMin)) * 0.5 - (y - yMin) - centerProjY) * scale);

        // Group voxels by (blockId, face) so we can build one StreamGeometry
        // per (color, face) pair and DrawGeometry it once instead of per-face.
        // That cuts the call count dramatically — for ~30k visible faces we
        // go from 30k DrawGeometry calls to ~150 (number of palettes × 3).
        var topBuckets = new Dictionary<uint, List<(int X, int Y, int Z)>>();
        var rightBuckets = new Dictionary<uint, List<(int X, int Y, int Z)>>();
        var frontBuckets = new Dictionary<uint, List<(int X, int Y, int Z)>>();

        foreach (var v in input.Voxels)
        {
            if (optionalYRange is { } r && (v.Y < r.min || v.Y > r.max)) continue;

            // Top face (+Y) visible iff voxel above is empty.
            if (!solid.Contains((v.X, v.Y + 1, v.Z)))
                AddTo(topBuckets, PaletteKey(v.BlockId), (v.X, v.Y, v.Z));
            // +X face visible iff voxel in +X direction is empty.
            if (!solid.Contains((v.X + 1, v.Y, v.Z)))
                AddTo(rightBuckets, PaletteKey(v.BlockId), (v.X, v.Y, v.Z));
            // +Z face visible iff voxel in +Z direction is empty.
            if (!solid.Contains((v.X, v.Y, v.Z + 1)))
                AddTo(frontBuckets, PaletteKey(v.BlockId), (v.X, v.Y, v.Z));
        }

        // Painter's algorithm sort: paint farthest faces first. Faces are
        // batched per-block so we sort at draw time by issuing the face
        // batches in (top, right, front) order and within each batch
        // sorting voxels by descending world depth (X+Y+Z low → far → first).
        var palette = new Dictionary<uint, (Color baseColor, IBrush top, IBrush right, IBrush front)>();
        IBrush BrushFor(uint blockId, double tint)
        {
            if (!palette.TryGetValue(blockId, out var entry))
            {
                var bc = HashToColor(blockId);
                entry = (bc,
                    new SolidColorBrush(Tint(bc, 1.00)),
                    new SolidColorBrush(Tint(bc, 0.78)),
                    new SolidColorBrush(Tint(bc, 0.60)));
                palette[blockId] = entry;
            }
            return tint == 1.00 ? entry.top : tint == 0.78 ? entry.right : entry.front;
        }

        // To avoid producing one DrawGeometry call per voxel we build a
        // single PathFigure per face-batch. Faces don't overlap each other
        // within the same depth class (top/right/front), so painter's order
        // only matters _between_ batches, which we naturally honour by
        // drawing faces in z-coordinate sweeps.
        DrawFaceBatch(ctx, topBuckets,   Face.Top,   Project, b => BrushFor(b, 1.00));
        DrawFaceBatch(ctx, rightBuckets, Face.Right, Project, b => BrushFor(b, 0.78));
        DrawFaceBatch(ctx, frontBuckets, Face.Front, Project, b => BrushFor(b, 0.60));

        return rtb;
    }

    private enum Face { Top, Right, Front }

    private static void AddTo(Dictionary<uint, List<(int, int, int)>> buckets, uint key, (int, int, int) v)
    {
        if (!buckets.TryGetValue(key, out var list))
        {
            list = new List<(int, int, int)>();
            buckets[key] = list;
        }
        list.Add(v);
    }

    private static void DrawFaceBatch(
        DrawingContext ctx,
        Dictionary<uint, List<(int X, int Y, int Z)>> buckets,
        Face face,
        Func<double, double, double, Point> project,
        Func<uint, IBrush> brushFor)
    {
        // Within one face direction we sort by (X + Z + Y) descending so
        // closer voxels paint over farther ones — painter's algorithm.
        foreach (var (blockId, voxels) in buckets)
        {
            voxels.Sort((a, b) =>
                ((b.X + b.Y + b.Z)).CompareTo(a.X + a.Y + a.Z));
            var brush = brushFor(blockId);
            var geom = new StreamGeometry();
            using (var g = geom.Open())
            {
                foreach (var (x, y, z) in voxels)
                {
                    Point a, b, c, d;
                    switch (face)
                    {
                        case Face.Top:    // +Y face: top of cube
                            a = project(x,     y + 1, z);
                            b = project(x + 1, y + 1, z);
                            c = project(x + 1, y + 1, z + 1);
                            d = project(x,     y + 1, z + 1);
                            break;
                        case Face.Right:  // +X face
                            a = project(x + 1, y,     z);
                            b = project(x + 1, y,     z + 1);
                            c = project(x + 1, y + 1, z + 1);
                            d = project(x + 1, y + 1, z);
                            break;
                        default:          // +Z face
                            a = project(x,     y,     z + 1);
                            b = project(x + 1, y,     z + 1);
                            c = project(x + 1, y + 1, z + 1);
                            d = project(x,     y + 1, z + 1);
                            break;
                    }
                    g.BeginFigure(a, isFilled: true);
                    g.LineTo(b);
                    g.LineTo(c);
                    g.LineTo(d);
                    g.EndFigure(isClosed: true);
                }
            }
            ctx.DrawGeometry(brush, pen: null, geom);
        }
    }

    private static Color HashToColor(uint blockId)
    {
        unchecked
        {
            uint h = blockId * 2654435761u;
            h ^= h >> 16;
            double hue = (h % 360u) / 360.0;
            return ColorFromHsl(hue, saturation: 0.55, lightness: 0.58);
        }
    }

    private static Color Tint(Color c, double factor)
    {
        byte ch(double v) => (byte)Math.Clamp(v, 0, 255);
        return Color.FromArgb(c.A, ch(c.R * factor), ch(c.G * factor), ch(c.B * factor));
    }

    private static Color ColorFromHsl(double h, double saturation, double lightness)
    {
        double q = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - lightness * saturation;
        double p = 2 * lightness - q;
        double r = HueToRgb(p, q, h + 1.0 / 3);
        double g = HueToRgb(p, q, h);
        double b = HueToRgb(p, q, h - 1.0 / 3);
        return Color.FromRgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
