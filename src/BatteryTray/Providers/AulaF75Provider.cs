using BatteryTray.Core;
using BatteryTray.Interop;

namespace BatteryTray.Providers;

/// <summary>
/// AULA F75. The wireless receiver protocol was measured on Windows and is
/// documented by sanshoku: report 0x13, read-only battery command 0x4A and a
/// wrapping sum in the last byte. No lighting/configuration command is used.
/// https://github.com/ushineko/sanshoku/tree/main/aula
/// </summary>
public sealed class AulaF75Provider : IStandaloneProvider
{
    internal const ushort WirelessVid = 0x3554, WirelessPid = 0xFA09;
    internal const ushort WiredVid = 0x258A, WiredPid = 0x010C;
    private const byte ReportId = 0x13, BatteryCommand = 0x4A;
    private const int ReportLength = 20;
    private static readonly TimeSpan AskTimeout = TimeSpan.FromMilliseconds(800);

    public string Name => "AULA F75";

    public bool Handles(ushort vid, ushort pid) =>
        (vid, pid) is (WirelessVid, WirelessPid) or (WiredVid, WiredPid);

    public IEnumerable<DeviceEntry> Discover(IReadOnlyList<HidCollection> collections)
    {
        var claimed = collections.Where(c => Handles(c.Vid, c.Pid)).ToList();
        if (claimed.Count == 0) yield break;

        var wireless = claimed.Where(c => (c.Vid, c.Pid) == (WirelessVid, WirelessPid)).ToList();
        bool wiredPresent = claimed.Any(c => (c.Vid, c.Pid) == (WiredVid, WiredPid));
        BatteryReading reading;
        string origin;
        if (wireless.Count > 0)
        {
            reading = ReadWireless(wireless);
            // Keep asking through the receiver while USB-C powers the board.
            // If the request was actually allowed but yielded no percentage,
            // cable presence is still a truthful state to show. Before consent,
            // keep NoData so the panel can present its Allow button.
            if (wiredPresent && reading.Status == ReadingStatus.NoData &&
                HidRequest.WaitingVid($"{WirelessVid:X4}:{WirelessPid:X4}") is null)
            {
                reading = BatteryReading.Charging(L.T(
                    "conectado pelo cabo; o receptor não informou um percentual confiável",
                    "wired; the receiver did not report a trustworthy percentage"));
            }
            origin = wiredPresent && reading.Status == ReadingStatus.Charging ? "Cabo" : "Dongle";
        }
        else
        {
            // 258A:010C confirms external power, but the wired feature-report
            // protocol exposes no trustworthy percentage. Show the truthful
            // cable state without probing configuration reports or inventing a
            // value. If the receiver stays plugged in, the branch above still
            // asks its documented battery getter even while the cable is used.
            reading = BatteryReading.Charging(L.T(
                "conectado pelo cabo; esta interface não informa um percentual confiável",
                "wired; this interface does not report a trustworthy percentage"));
            origin = "Cabo";
        }

        yield return new DeviceEntry
        {
            Key = "aula:f75",
            DisplayName = "AULA F75",
            HardwareId = string.Join(", ", claimed.Select(c => c.HardwareId).Distinct()),
            ProviderName = Name,
            Reading = reading,
            IsConnected = true,
            Kind = DeviceKind.Keyboard,
            Origin = origin,
        };
    }

    private static BatteryReading ReadWireless(List<HidCollection> collections)
    {
        var target = collections.FirstOrDefault(c =>
            c.UsagePage == 0xFF02 && c.InputLen == ReportLength && c.OutputLen == ReportLength);
        if (target is null)
            return BatteryReading.NoData(L.T(
                "não achei o canal de bateria 0xFF02 desta revisão",
                "couldn’t find this revision’s 0xFF02 battery channel"));

        using var session = HidRequest.Open(new[] { target });
        if (session is null)
            return BatteryReading.NoData(L.T("o canal de bateria não abriu", "the battery channel didn’t open"));

        var reply = session.Ask(target, BuildRequest(), IsBatteryReply, AskTimeout);
        return reply is null
            ? BatteryReading.NoData(L.T(
                "o receptor não respondeu; a pergunta pode estar sem permissão ou o teclado pode estar dormindo",
                "the receiver didn’t answer; the request may not be permitted or the keyboard may be asleep"))
            : Decode(reply);
    }

    internal static byte[] BuildRequest()
    {
        var report = new byte[ReportLength];
        report[0] = ReportId;
        report[1] = BatteryCommand;
        report[^1] = Checksum(report);
        return report;
    }

    internal static byte Checksum(ReadOnlySpan<byte> report)
    {
        if (report.Length < ReportLength) return 0;
        byte sum = 0;
        for (int i = 0; i < ReportLength - 1; i++) sum += report[i];
        return sum;
    }

    internal static bool IsBatteryReply(byte[] report) =>
        report.Length >= ReportLength &&
        report[0] == ReportId &&
        (report[1] & 0x7F) == BatteryCommand &&
        report[ReportLength - 1] == Checksum(report);

    internal static BatteryReading Decode(byte[] report)
    {
        if (!IsBatteryReply(report))
            return BatteryReading.NoData(L.T("resposta AULA inválida", "invalid AULA reply"));

        return report[6] switch
        {
            0x10 => BatteryReading.Charging(L.T(
                "cabo conectado; o aparelho informa 100% não confiáveis enquanto está no cabo",
                "cable connected; the device reports an unreliable 100% while wired")),
            0x01 when report[5] is >= 1 and <= 100 => BatteryReading.Percentage(report[5]),
            0x01 => BatteryReading.NoData(L.T("nível de bateria fora da faixa", "battery level is out of range")),
            _ => BatteryReading.NoData(L.T("estado de energia desconhecido", "unknown power state")),
        };
    }
}
