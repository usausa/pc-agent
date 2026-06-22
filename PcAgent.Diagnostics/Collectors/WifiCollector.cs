namespace PcAgent.Diagnostics.Collectors;

using System.ComponentModel;
using System.Reflection;

using ManagedNativeWifi;

using PcAgent.Diagnostics.Models;

// Wi-Fi インターフェイス・接続状況(ManagedNativeWifi / Native WiFi API)。WLAN AutoConfig サービスに依存。
public sealed class WifiCollector : ICollector
{
    public string Name => "wifi";

    public string DisplayName => "Wi-Fi";

    public ValueTask<CollectorResult> CollectAsync(CancellationToken cancellationToken) => new(Collect());

    private CollectorResult Collect()
    {
        List<InterfaceConnectionInfo> interfaces;
        List<BssNetworkPack> bssList;
        try
        {
            interfaces = NativeWifi.EnumerateInterfaceConnections().ToList();
            bssList = NativeWifi.EnumerateBssNetworks().ToList();
        }
        catch (Win32Exception ex)
        {
            return Unavailable(ex);
        }
        catch (TargetInvocationException ex)
        {
            return Unavailable(ex.InnerException ?? ex);
        }

        if (interfaces.Count == 0)
        {
            return new CollectorResult(Name, DisplayName, [], "Wi-Fi インターフェイスがありません。");
        }

        var groups = new List<MetricGroup>(interfaces.Count);
        foreach (var iface in interfaces)
        {
            groups.Add(BuildInterfaceGroup(iface, bssList));
        }

        return new CollectorResult(Name, DisplayName, groups, null);
    }

    private CollectorResult Unavailable(Exception ex)
        => new(Name, DisplayName, [], "Wi-Fi 情報を取得できません(WLAN サービス停止など): " + ex.Message);

    private static MetricGroup BuildInterfaceGroup(InterfaceConnectionInfo iface, List<BssNetworkPack> bssList)
    {
        var values = new List<MetricValue>
        {
            new("State", null, null, iface.State.ToString()),
            new("Radio", null, null, iface.IsRadioOn ? "On" : "Off"),
            new("Visible Networks", bssList.Count(b => b.InterfaceInfo.Id == iface.Id), null, null),
        };

        if (iface.IsConnected)
        {
            try
            {
                AddConnectionValues(iface.Id, bssList, values);
            }
            catch (Win32Exception)
            {
                // 接続詳細の取得に失敗しても、基本情報は返す。
            }
            catch (TargetInvocationException)
            {
                // 同上(WLAN クライアント生成失敗等)。
            }
        }
        else
        {
            values.Add(new("Connection", null, null, "未接続"));
        }

        return new MetricGroup("Wi-Fi: " + iface.Description, values);
    }

    private static void AddConnectionValues(Guid interfaceId, List<BssNetworkPack> bssList, List<MetricValue> values)
    {
        var (result, connection) = NativeWifi.GetCurrentConnection(interfaceId);
        if (result != ActionResult.Success)
        {
            return;
        }

        values.Add(new("SSID", null, null, connection.Ssid.ToString()));
        values.Add(new("BSSID", null, null, connection.Bssid.ToString()));
        values.Add(new("Signal Quality", connection.SignalQuality, "%", null));

        var (rssiResult, rssi) = NativeWifi.GetRssi(interfaceId);
        if (rssiResult == ActionResult.Success)
        {
            values.Add(new("RSSI", rssi, "dBm", null));
        }

        var bss = bssList.FirstOrDefault(b => b.InterfaceInfo.Id == interfaceId && b.Bssid.Equals(connection.Bssid));
        if (bss is not null)
        {
            values.Add(new("Band", bss.Band, "GHz", null));
            values.Add(new("Channel", bss.Channel, null, null));
        }

        values.Add(new("Rx Rate", connection.RxRate / 1000.0, "Mbps", null));
        values.Add(new("Tx Rate", connection.TxRate / 1000.0, "Mbps", null));
        values.Add(new("PHY", null, null, DescribePhy(connection.PhyType)));
        values.Add(new("Security", null, null, connection.IsSecurityEnabled ? connection.AuthenticationAlgorithm.ToString() : "Open"));
    }

    private static string DescribePhy(PhyType phy) => phy switch
    {
        PhyType.Eht => "Wi-Fi 7 (802.11be)",
        PhyType.He => "Wi-Fi 6 (802.11ax)",
        PhyType.Vht => "Wi-Fi 5 (802.11ac)",
        PhyType.Ht => "Wi-Fi 4 (802.11n)",
        PhyType.Erp => "802.11g",
        PhyType.Ofdm => "802.11a",
        PhyType.HrDsss => "802.11b",
        _ => phy.ToString(),
    };
}
