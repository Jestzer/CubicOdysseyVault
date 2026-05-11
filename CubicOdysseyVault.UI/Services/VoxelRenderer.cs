using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CubicOdysseyVault.Core.Voxels;

namespace CubicOdysseyVault.UI.Services;

// Renders a VoxelGrid as a small isometric Bitmap suitable for use as a
// list-row thumbnail. The projection is the standard 30°-isometric where
// +x goes down-right, +z goes down-left, +y is up. Each solid voxel draws
// three visible faces (top/+x/+z); voxels are sorted back-to-front by
// (x + z - y) so closer voxels paint over farther ones.
//
// Block colors come from a VoxelTypeCatalog when one is supplied (the
// authentic in-game m_color values from data/configs/voxels/<name>.cfg).
// When the catalog is null or doesn't know a block, the renderer falls
// back to a deterministic hash → HSL palette so unknown ids still get a
// stable, visually distinct color.
public static class VoxelRenderer
{
    private const double SqrtThreeOverTwo = 0.8660254037844387;

    public static Bitmap Render(VoxelGrid grid, int width, int height,
        VoxelTypeCatalog? catalog = null)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using var ctx = rtb.CreateDrawingContext();

        if (grid.SolidCount == 0) return rtb;

        // Bounding box of solid voxels — the grid is up to dim³ but ships
        // typically fill <10%, so projecting on the actual content gives a
        // much larger thumbnail of the silhouette.
        int xMin = int.MaxValue, xMax = int.MinValue;
        int yMin = int.MaxValue, yMax = int.MinValue;
        int zMin = int.MaxValue, zMax = int.MinValue;
        foreach (var v in grid.SolidVoxels)
        {
            if (v.X < xMin) xMin = v.X; if (v.X > xMax) xMax = v.X;
            if (v.Y < yMin) yMin = v.Y; if (v.Y > yMax) yMax = v.Y;
            if (v.Z < zMin) zMin = v.Z; if (v.Z > zMax) zMax = v.Z;
        }

        // Find the screen-space bounds of the projected bounding box, then
        // pick a cell size that fits inside the bitmap (with a small margin).
        int xs = xMax - xMin + 1;
        int ys = yMax - yMin + 1;
        int zs = zMax - zMin + 1;

        double projW = (xs + zs) * SqrtThreeOverTwo;        // in cell-units
        double projH = (xs + zs) * 0.5 + ys;                // in cell-units
        double margin = 4.0;
        double scale = Math.Min((width - 2 * margin) / projW, (height - 2 * margin) / projH);
        if (scale <= 0) scale = 1;

        // Translation so the projected bounding box is centered on the bitmap.
        double cx = width / 2.0;
        double cy = height / 2.0;
        // Center of the projected box in cell-units (relative to (xMin, yMin, zMin)):
        double centerProjX = ((xs - 1) - (zs - 1)) * 0.5 * SqrtThreeOverTwo;
        double centerProjY = ((xs - 1) + (zs - 1)) * 0.25 - (ys - 1) * 0.5;

        double Tx(double x, double z) =>
            cx + (((x - xMin) - (z - zMin)) * SqrtThreeOverTwo - centerProjX) * scale;
        double Ty(double x, double y, double z) =>
            cy + (((x - xMin) + (z - zMin)) * 0.5 - (y - yMin) - centerProjY) * scale;

        // Back-to-front sort: viewer is at (+x, +y, +z) infinity, so the
        // farther a voxel is from the viewer the smaller (x + y + z) it
        // has. Drawing low values first lets nearer voxels paint over.
        var sorted = grid.SolidVoxels.OrderBy(v => v.X + v.Y + v.Z).ToList();

        var palette = new Dictionary<uint, (IBrush top, IBrush right, IBrush front)>();

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 0.5);

        foreach (var v in sorted)
        {
            if (!palette.TryGetValue(v.BlockId, out var faces))
            {
                var baseColor = ResolveBaseColor(catalog, v.BlockId);
                faces = (
                    top: new SolidColorBrush(Tint(baseColor, 1.00)),
                    right: new SolidColorBrush(Tint(baseColor, 0.78)),
                    front: new SolidColorBrush(Tint(baseColor, 0.60)));
                palette[v.BlockId] = faces;
            }

            // Voxel cube spans [x..x+1, y..y+1, z..z+1] in cell units.
            int x = v.X, y = v.Y, z = v.Z;

            // Project the 8 corners. Avalonia's Y axis goes downward, so a
            // higher world-y projects to a smaller screen-y — Ty already
            // subtracts (y - yMin), so larger y → smaller cy term → nearer
            // top of the bitmap, which is correct.
            var p_xyz   = new Point(Tx(x,     z),     Ty(x,     y,     z));
            var p_Xyz   = new Point(Tx(x + 1, z),     Ty(x + 1, y,     z));
            var p_xyZ   = new Point(Tx(x,     z + 1), Ty(x,     y,     z + 1));
            var p_XyZ   = new Point(Tx(x + 1, z + 1), Ty(x + 1, y,     z + 1));
            var p_xYz   = new Point(Tx(x,     z),     Ty(x,     y + 1, z));
            var p_XYz   = new Point(Tx(x + 1, z),     Ty(x + 1, y + 1, z));
            var p_xYZ   = new Point(Tx(x,     z + 1), Ty(x,     y + 1, z + 1));
            var p_XYZ   = new Point(Tx(x + 1, z + 1), Ty(x + 1, y + 1, z + 1));

            // Top face (y = y+1): xYz → XYz → XYZ → xYZ
            DrawQuad(ctx, faces.top, pen, p_xYz, p_XYz, p_XYZ, p_xYZ);
            // +x face (x = x+1): Xyz → XyZ → XYZ → XYz
            DrawQuad(ctx, faces.right, pen, p_Xyz, p_XyZ, p_XYZ, p_XYz);
            // +z face (z = z+1): xyZ → XyZ → XYZ → xYZ
            DrawQuad(ctx, faces.front, pen, p_xyZ, p_XyZ, p_XYZ, p_xYZ);
        }

        return rtb;
    }

    private static void DrawQuad(DrawingContext ctx, IBrush brush, Pen pen,
        Point a, Point b, Point c, Point d)
    {
        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(a, isFilled: true);
            g.LineTo(b);
            g.LineTo(c);
            g.LineTo(d);
            g.EndFigure(isClosed: true);
        }
        ctx.DrawGeometry(brush, pen, geom);
    }

    // Catalog hit → authentic m_color; miss → deterministic hash so the
    // unknown block still gets a stable, distinct color.
    private static Color ResolveBaseColor(VoxelTypeCatalog? catalog, uint blockId)
    {
        if (catalog is not null)
        {
            var def = catalog.Lookup((byte)(blockId >> 24));
            if (def is not null) return Color.FromRgb(def.R, def.G, def.B);
        }
        return HashToColor(blockId);
    }

    // Deterministic block-id → color. Using a fixed-saturation HSL hash
    // keeps the result distinct without ever producing a near-black or
    // near-white that would muddy the isometric shading.
    private static Color HashToColor(uint blockId)
    {
        // Mix high and low halves so the fairly-correlated low byte (variant)
        // doesn't dominate the hue and sibling variants get distinct colors.
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
        double q = lightness < 0.5
            ? lightness * (1 + saturation)
            : lightness + saturation - lightness * saturation;
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
