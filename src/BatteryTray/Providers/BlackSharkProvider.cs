using BatteryTray.Core;
using BatteryTray.Interop;

namespace BatteryTray.Providers;

/// <summary>
/// Razer BlackShark V3 (dongle 2.4 GHz, 1532:057A). SOMENTE LEITURA.
///
/// Formato (relatorio 0x02 de 64 bytes, colecao 0xFF14), conferido numa captura
/// passiva real com o fone em 35%:
///   02 02 60 00 00 00 05 00 00 80 21 01 01 &lt;bat&gt; ...
///   [10] classe 0x21 (bateria)  [11] 01 resposta  [12] 01 argumento  [13] percentual direto
/// O fone manda este relatorio sozinho ao ligar/desligar; entre uma leitura e outra o
/// Joule so escuta. Em cada leitura sob demanda faz UMA pergunta pela porta do
/// HidRequest (sujeita a licenca da marca e a "So escutar"). O resto do
/// enquadramento bate com o que o openrazer (PR 2794) e o HaloBattery (#367)
/// documentam; so o formato, nenhum codigo copiado.
/// </summary>
public sealed class BlackSharkProvider : IBatteryProvider, IPassiveListenerProvider, IDisposable
{
    private const ushort Vid = 0x1532;
    private const ushort Pid = 0x057A;
    private const ushort VendorUsagePage = 0xFF14;
    private const int ReportLength = 64;

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(1.5);

    private static bool IsTarget(HidCollection c) =>
        c.Vid == Vid && c.Pid == Pid && c.UsagePage == VendorUsagePage && c.InputLen == ReportLength;

    private readonly PassiveReportListener _listener = new("BlackShark", IsTarget, ParsePercent);

    public string Name => "BlackShark";

    public IReadOnlyList<(ushort Vid, ushort Pid)> SupportedIds { get; } = new[] { (Vid, Pid) };

    public string LogicalKey(ushort vid, ushort pid) => "razer:blackshark v3";

    public string DisplayName(ReadContext ctx) => "Razer BlackShark V3";

    public void EnsureListening(IReadOnlyList<HidCollection> collections) => _listener.Ensure(collections);

    public void StopListening() => _listener.Stop();

    public bool IsListening => _listener.IsListening;

    public event Action? ValueChanged
    {
        add => _listener.ValueChanged += value;
        remove => _listener.ValueChanged -= value;
    }

    public void Dispose() => _listener.Dispose();

    public BatteryReading Read(ReadContext ctx)
    {
        if (!ctx.IsPresent(Vid, Pid))
            return BatteryReading.Disconnected(L.T("dongle do BlackShark V3 nao esta presente", "BlackShark V3 dongle not present"));

        if (!ctx.Settings.PassiveListenersEnabled)
            return BatteryReading.NoData(L.T("escuta passiva desligada", "passive listening off"));

        try
        {
            _listener.Ensure(ctx.AllCollections);
        }
        catch (Exception ex)
        {
            Log.Error("BlackShark.Ensure", ex);
            return BatteryReading.Error(ex.Message);
        }

        // Uma pergunta por leitura (nunca em laco: o autor do driver do openrazer relata que
        // pergunta frequente reinicia o fone). Sem licenca ou em "So escutar", Ask devolve null
        // e vale o que o listener ja viu.
        if (ctx.AllCollections.FirstOrDefault(IsTarget) is { } target)
        {
            using var session = HidRequest.Open(new[] { target });
            if (session?.Ask(target, BuildRequest(), r => ParsePercent(r) is not null, AskTimeout) is { } reply
                && ParsePercent(reply) is int asked)
                return BatteryReading.Percentage(asked, observedUtc: DateTime.UtcNow);
        }

        if (_listener.Last is { } last)
            return BatteryReading.Percentage(last.Percent, observedUtc: last.SeenUtc);

        return BatteryReading.NoData(_listener.IsListening
            ? L.T("o valor chega quando o fone ou o Synapse trocar a bateria; ligue o fone", "the value arrives when the headset or Synapse reports battery; switch the headset on")
            : L.T("nao foi possivel abrir a colecao 0xFF14 para escuta", "couldn’t open collection 0xFF14 to listen"));
    }

    /// <summary>
    /// Pergunta de bateria: 02 00 60 00 00 00 04 00 00 80 21 00 00 .. [62]=XOR dos bytes 0..61.
    /// Conferida em aparelho real: uma pergunta, uma resposta 21 01 01 &lt;bat&gt;.
    /// </summary>
    internal static byte[] BuildRequest()
    {
        var r = new byte[ReportLength];
        r[0] = 0x02; r[2] = 0x60; r[6] = 0x04; r[9] = 0x80; r[10] = 0x21;
        for (int i = 0; i < 62; i++) r[62] ^= r[i];
        return r;
    }

    /// <summary>Relatorio 02, classe 0x21, resposta (01), um argumento, percentual &lt;= 100; senao null.</summary>
    internal static int? ParsePercent(byte[] r)
    {
        if (r.Length < ReportLength) return null;
        if (r[0] != 0x02 || r[10] != 0x21 || r[11] != 0x01 || r[12] != 0x01) return null;
        return r[13] <= 100 ? r[13] : null;
    }
}
