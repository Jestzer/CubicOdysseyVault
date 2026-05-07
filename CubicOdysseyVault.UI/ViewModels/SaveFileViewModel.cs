using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.SaveContent;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class SaveFileViewModel : ViewModelBase
{
    public string FullPath { get; }
    public string FileName { get; }
    public long CompressedSize { get; }

    [ObservableProperty] private long _decompressedSize;
    [ObservableProperty] private bool _isTlvSave;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private ObservableCollection<TlvEntryViewModel> _tlvEntries = new();
    [ObservableProperty] private ObservableCollection<ExtractedString> _strings = new();
    [ObservableProperty] private string _hexText = "";

    private bool _loaded;

    public string SizeLabel => DecompressedSize > 0 && DecompressedSize != CompressedSize
        ? $"{FormatBytes(CompressedSize)} (zstd → {FormatBytes(DecompressedSize)})"
        : FormatBytes(CompressedSize);

    public SaveFileViewModel(string fullPath, long compressedSize)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        CompressedSize = compressedSize;
    }

    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var blob = SaveBlobReader.ReadFile(FullPath);
            var bytesForViews = blob.DecompressedBytes ?? blob.RawBytes;
            DecompressedSize = blob.DecompressedBytes?.Length ?? 0;

            // Decoded TLV view — only attempt if decompression succeeded.
            if (blob.DecompressedBytes != null)
            {
                var doc = TlvParser.Parse(blob.DecompressedBytes);
                if (doc.ParseError == null && doc.HeaderTag == 8 && doc.Entries.Count > 0)
                {
                    IsTlvSave = true;
                    foreach (var e in doc.Entries)
                    {
                        var name = KnownTagAnnotations.Lookup(FileName, e.Tag);
                        TlvEntries.Add(new TlvEntryViewModel(e, name));
                    }
                }
                else if (doc.ParseError != null)
                {
                    ErrorMessage = "Could not parse TLV: " + doc.ParseError;
                }
            }
            else if (blob.ErrorMessage != null)
            {
                ErrorMessage = blob.ErrorMessage;
            }

            // Strings — extract from decompressed if available, else raw.
            foreach (var s in StringsExtractor.Extract(bytesForViews))
                Strings.Add(s);

            // Hex view — capped to keep things responsive on multi-MB files.
            const int hexCap = 64 * 1024;
            var hexBytes = bytesForViews.Length > hexCap
                ? bytesForViews.AsSpan(0, hexCap).ToArray()
                : bytesForViews;
            var lines = HexFormatter.Format(hexBytes);
            HexText = string.Join("\n", lines);
            if (bytesForViews.Length > hexCap)
                HexText += $"\n\n… {bytesForViews.Length - hexCap:N0} more bytes hidden ({FormatBytes(bytesForViews.Length - hexCap)}).";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load: {ex.Message}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double v = bytes;
        string[] units = { "KB", "MB", "GB", "TB" };
        int i = -1;
        do { v /= 1024; i++; } while (v >= 1024 && i < units.Length - 1);
        return $"{v:0.##} {units[i]}";
    }
}
