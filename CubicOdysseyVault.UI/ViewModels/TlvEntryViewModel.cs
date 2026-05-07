using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CubicOdysseyVault.Core.SaveContent;

namespace CubicOdysseyVault.UI.ViewModels;

public partial class TlvEntryViewModel : ViewModelBase
{
    public TlvEntry Entry { get; }
    public string TagDisplay { get; }
    public string KindLabel { get; }
    public string ValueText { get; }
    public string HexPreview { get; }
    public ObservableCollection<TlvEntryViewModel> Children { get; } = new();

    public TlvEntryViewModel(TlvEntry entry, string? friendlyName)
    {
        Entry = entry;
        TagDisplay = friendlyName != null
            ? $"{friendlyName} (#{entry.Tag})"
            : $"#{entry.Tag}";
        KindLabel = entry.Kind switch
        {
            TlvValueKind.Byte => "byte",
            TlvValueKind.Int32 => "int32",
            TlvValueKind.UInt32 => "uint32",
            TlvValueKind.Int64 => "int64",
            TlvValueKind.Float32 => "float32",
            TlvValueKind.Float64 => "float64",
            TlvValueKind.List => "list",
            _ => $"raw type {entry.RawType}",
        };
        ValueText = DecodeValue(entry);
        HexPreview = HexPreviewOf(entry.RawData);

        if (entry.Kind == TlvValueKind.List && entry.Nested != null)
        {
            foreach (var nested in entry.Nested)
                Children.Add(new TlvEntryViewModel(nested, friendlyName: null));
        }
    }

    private static string DecodeValue(TlvEntry entry) => entry.Kind switch
    {
        TlvValueKind.Byte => entry.RawData.Length == 1 ? entry.RawData[0].ToString() : "?",
        TlvValueKind.Int32 => entry.RawData.Length == 4 ? BitConverter.ToInt32(entry.RawData).ToString() : "?",
        TlvValueKind.UInt32 => entry.RawData.Length == 4 ? BitConverter.ToUInt32(entry.RawData).ToString() : "?",
        TlvValueKind.Int64 => entry.RawData.Length == 8 ? BitConverter.ToInt64(entry.RawData).ToString() : "?",
        TlvValueKind.Float32 => entry.RawData.Length == 4 ? BitConverter.ToSingle(entry.RawData).ToString("G6") : "?",
        TlvValueKind.Float64 => entry.RawData.Length == 8 ? BitConverter.ToDouble(entry.RawData).ToString("G10") : "?",
        TlvValueKind.List => $"({entry.Nested?.Count ?? 0} entries)",
        _ => $"({entry.RawData.Length} bytes)",
    };

    private static string HexPreviewOf(byte[] data)
    {
        const int max = 16;
        int n = Math.Min(max, data.Length);
        var sb = new System.Text.StringBuilder(n * 3);
        for (int i = 0; i < n; i++)
        {
            sb.Append(data[i].ToString("x2"));
            if (i < n - 1) sb.Append(' ');
        }
        if (data.Length > max) sb.Append(" …");
        return sb.ToString();
    }
}
