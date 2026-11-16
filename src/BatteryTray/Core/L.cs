using System.Globalization;

namespace BatteryTray.Core;

/// <summary>
/// Idioma da interface: portugues, ingles ou russo. Portugues e ingles ficam
/// lado a lado onde usados; o russo e' aplicado pelo catalogo central.
/// Decidido ao abrir o app; trocar nas Configuracoes vale ao reabrir.
/// O log continua em portugues (e' para o Dillan, nao para quem usa).
/// </summary>
public static class L
{
    public static string Code { get; private set; } = "pt";
    public static bool En => Code == "en";
    public static bool Ru => Code == "ru";

    /// <summary>"auto" segue portugues/russo do Windows; os demais usam ingles.</summary>
    public static string Resolve(string? choice) =>
        choice switch
        {
            "pt" or "en" or "ru" => choice,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
            {
                "pt" => "pt",
                "ru" => "ru",
                _ => "en",
            },
        };

    public static void Set(string? choice) => Code = Resolve(choice);

    public static string T(string pt, string en) => Code switch
    {
        "pt" => pt,
        "ru" => RussianTranslations.Translate(en),
        _ => en,
    };

    /// <summary>Plural simples: um / varios.</summary>
    public static string N(int n, string ptOne, string ptMany, string enOne, string enMany) =>
        n == 1 ? T(ptOne, enOne) : T(ptMany, enMany);
}
