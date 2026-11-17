namespace BatteryTray.Core;

/// <summary>Uma novidade da tela "O que mudou" (2026-11-12). Glyph = traco 20x20, como os desenhos das telas.</summary>
/// Credit = agradecimento a quem fez (2026-11-16): "@login" do PR; nas linguas, na lingua de quem traduziu, igual em qualquer idioma do app.
public sealed record Change(string Version, string Glyph, string TitlePt, string TitleEn, string TextPt, string TextEn, string CreditPt = "", string CreditEn = "")
{
    public string Credit => L.T(CreditPt, CreditEn);
    public string Title => L.T(TitlePt, TitleEn);
    public string Text => L.T(TextPt, TextEn);
}

/// <summary>
/// Tela depois de cada atualizacao (mostra aprovada em 2026-11-12:
/// https://claude.ai/artifact/EpnC2HpjG4dvKsRHs8uBuS): resumo e apoio, depois
/// "O que mudou". Novidade nova entra no topo de All, com a versao do main.
/// </summary>
public static class Changes
{
    /// <summary>Agradecimento no alto da primeira tela, so para quem chega nesta versao (2026-11-16).</summary>
    public const string ThanksVersion = "0.2.1";
    public static string Thanks => L.T(
        "Obrigado a quem chegou pelo Instagram e por toda parte. Em poucos dias vieram dezenas de relatos, capturas e as primeiras contribuições de código e tradução. Esta versão foi feita com vocês.",
        "Obrigado to everyone who came from Instagram and everywhere else. In a few days you sent dozens of reports, captures and the first code and translation contributions. This version was made with you.");

    public static readonly Change[] All =
    {
        new("0.2.1", "M10 3a7 7 0 1 0 7 7M10 3c2 2 3 4.5 3 7s-1 5-3 7c-2-2-3-4.5-3-7s1-5 3-7zM3 10h14",
            "En français", "En français",
            "Para trocar, vá em Configurações.", "To change it, go to Settings.",
            "Merci, @TukaLeHibou !", "Merci, @TukaLeHibou !"),
        new("0.2.1", "M10 3a7 7 0 1 0 7 7M10 3c2 2 3 4.5 3 7s-1 5-3 7c-2-2-3-4.5-3-7s1-5 3-7zM3 10h14",
            "На русском", "На русском",
            "Para trocar, vá em Configurações.", "To change it, go to Settings.",
            "Спасибо, @wrldwideme!", "Спасибо, @wrldwideme!"),
        new("0.2.1", "M4 12v-2a6 6 0 0 1 12 0v2M4 12h2.5v4H4zM13.5 12H16v4h-2.5z",
            "Razer BlackShark V3", "Razer BlackShark V3",
            "A bateria do fone pelo dongle, conferida no aparelho de quem fez.", "The headset battery through the dongle, checked on the contributor’s own headset.",
            "Obrigado, @Ali-Fani", "Thanks, @Ali-Fani"),
        new("0.2.1", "M7 3h6a3 3 0 0 1 3 3v8a3 3 0 0 1-3 3H7a3 3 0 0 1-3-3V6a3 3 0 0 1 3-3zM10 3v5",
            "AULA F75 e receptores AJAZZ 8K e Zaopin", "AULA F75 and the AJAZZ 8K and Zaopin receivers",
            "Mais aparelhos lidos, com o código vindo da comunidade.", "More devices read, with code from the community.",
            "Obrigado, @wrldwideme", "Thanks, @wrldwideme"),
        new("0.2.1", "M3 7.5h12.5v5H3zM15.5 9h1.5v2h-1.5M5.5 10l2 1.5 3.5-3",
            "Verde quando completa", "Green when full",
            "Aparelho que informa a carga fica verde no widget e na bandeja ao chegar a 100%.", "Devices that report charging turn green on the widget and the tray when they reach 100%.",
            "Obrigado, Daniel Palma", "Thanks, Daniel Palma"),
        new("0.2.1", "M16 10a6 6 0 1 1-1.8-4.3M16 3v3.5h-3.5",
            "Número em dia", "Up-to-date numbers",
            "Abrir o painel lê de novo quando a última leitura passou de uma hora.", "Opening the panel reads again when the last reading is over an hour old."),
    };

    /// <summary>Novidades mais novas que a versao de antes ("" = veio de antes da tela existir: todas).</summary>
    public static IReadOnlyList<Change> Since(string last) =>
        Version.TryParse(last, out var from)
            ? All.Where(c => Version.Parse(c.Version) > from).ToList()
            : All;

    /// <summary>
    /// Versao de onde veio, quando a tela deve abrir agora; null quando nao.
    /// Abre uma vez por versao nova, nunca na primeira instalacao (as boas-vindas ja explicam).
    /// </summary>
    public static string? UpdatedFrom(string lastVersion, string currentVersion, bool onboardingDone) =>
        lastVersion == currentVersion || !onboardingDone ? null : lastVersion;
}
