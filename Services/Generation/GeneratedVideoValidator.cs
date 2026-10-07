using System.Buffers.Binary;
using System.Text;

namespace Imagino.Api.Services.Generation;

public static class GeneratedVideoValidator
{
    public const int MaxBytes = 100 * 1024 * 1024;
    public sealed record Metadata(int Width, int Height, double DurationSeconds);
    private sealed record Box(string Type, int Start, int End);
    private static List<Box> Boxes(byte[] bytes, int start, int end)
    {
        var boxes = new List<Box>();
        while (start < end)
        {
            if (end - start < 8) throw new InvalidDataException("Truncated MP4 atom.");
            long size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(start, 4));
            var header = 8;
            if (size == 1) {
                if (end - start < 16) throw new InvalidDataException("Truncated extended MP4 atom.");
                var wide = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(start + 8, 8));
                if (wide > int.MaxValue) throw new InvalidDataException("Oversized MP4 atom.");
                size = (long)wide; header = 16;
            }
            if (size == 0) size = end - start;
            if (size < header || size > end - start) throw new InvalidDataException("Invalid MP4 atom length.");
            boxes.Add(new(Encoding.ASCII.GetString(bytes, start + 4, 4), start + header, start + (int)size));
            start += (int)size;
        }
        return boxes;
    }
    public static Metadata ReadMetadata(byte[] bytes)
    {
        if (bytes.Length < 32 || bytes.Length > MaxBytes) throw new InvalidDataException("Invalid MP4 output size.");
        var top = Boxes(bytes, 0, bytes.Length);
        var ftyp = top.FirstOrDefault(b => b.Type == "ftyp");
        if (ftyp == null || ftyp.End - ftyp.Start < 8 ||
            !new[] { "isom", "iso2", "mp41", "mp42", "avc1", "M4V ", "iso5", "iso6" }.Contains(Encoding.ASCII.GetString(bytes, ftyp.Start, 4)) ||
            !top.Any(b => b.Type == "mdat" && b.End > b.Start)) throw new InvalidDataException("Expected an MP4 container with media bytes.");
        var moov = top.SingleOrDefault(b => b.Type == "moov") ?? throw new InvalidDataException("MP4 movie metadata missing.");
        var movie = Boxes(bytes, moov.Start, moov.End);
        var mvhd = movie.SingleOrDefault(b => b.Type == "mvhd") ?? throw new InvalidDataException("MP4 duration missing.");
        if (mvhd.End - mvhd.Start < 20) throw new InvalidDataException("Truncated MP4 duration atom.");
        var v = bytes[mvhd.Start];
        if (v > 1 || mvhd.End - mvhd.Start < (v == 1 ? 32 : 20)) throw new InvalidDataException("Invalid MP4 duration atom.");
        var timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mvhd.Start + (v == 1 ? 20 : 12), 4));
        var duration = v == 1 ? BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(mvhd.Start + 24, 8)) :
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mvhd.Start + 16, 4));
        var seconds = timescale == 0 ? 0 : duration / (double)timescale;
        foreach (var track in movie.Where(b => b.Type == "trak"))
        {
            var trackBoxes = Boxes(bytes, track.Start, track.End);
            var mdia = trackBoxes.SingleOrDefault(b => b.Type == "mdia");
            var tkhd = trackBoxes.SingleOrDefault(b => b.Type == "tkhd");
            if (mdia == null || tkhd == null) continue;
            var hdlr = Boxes(bytes, mdia.Start, mdia.End).SingleOrDefault(b => b.Type == "hdlr");
            if (hdlr == null || hdlr.End - hdlr.Start < 12 || !bytes.AsSpan(hdlr.Start + 8, 4).SequenceEqual("vide"u8)) continue;
            if (tkhd.End - tkhd.Start < 84) throw new InvalidDataException("Truncated MP4 video track.");
            var version = bytes[tkhd.Start];
            var offset = version == 1 ? 88 : 76;
            if (version > 1 || tkhd.End - tkhd.Start < offset + 8) throw new InvalidDataException("Invalid MP4 video track.");
            var width = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(tkhd.Start + offset, 4)) >> 16);
            var height = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(tkhd.Start + offset + 4, 4)) >> 16);
            if (width is < 1 or > 4096 || height is < 1 or > 4096 || seconds <= 0)
                throw new InvalidDataException("Invalid MP4 video dimensions or duration.");
            return new(width, height, seconds);
        }
        throw new InvalidDataException("MP4 video track missing.");
    }
    public static Metadata Validate(byte[] bytes)
    {
        var video = ReadMetadata(bytes);
        // The one authorized PNG is square. The real Grok Lite auto_720p task returned
        // 960x960 (720p quality tier), not a literal 720px shorter edge. Keep exact bounds.
        if (video.Width != 960 || video.Height != 960 || video.DurationSeconds is < 4.5 or > 5.5)
            throw new InvalidDataException("MP4 video dimensions or duration differ from the smoke configuration.");
        return video;
    }
}
