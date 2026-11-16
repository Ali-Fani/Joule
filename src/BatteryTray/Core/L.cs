using System.Globalization;

namespace BatteryTray.Core;

/// <summary>
/// Idioma da interface (2026-11-12): portugues ou ingles, cada texto com as
/// duas versoes lado a lado onde e' usado (L.T no codigo, {ui:Tr} no XAML).
/// Decidido ao abrir o app; trocar nas Configuracoes vale ao reabrir.
/// O log continua em portugues (e' para o Dillan, nao para quem usa).
///
/// French: starts from the English text and translates it through Core/French.cs,
/// leaving the existing pairs untouched. En stays true in French.
/// </summary>
public static class L
{
    public static bool En { get; private set; }
    public static bool Fr { get; private set; }

    /// <summary>Current language: "pt", "en" or "fr".</summary>
    public static string Current => Fr ? "fr" : En ? "en" : "pt";

    /// <summary>"auto" segue o idioma do Windows: portugues ou frances se ele for; senao ingles.</summary>
    public static string Resolve(string? choice) =>
        choice switch
        {
            "en" or "pt" or "fr" => choice,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch { "pt" => "pt", "fr" => "fr", _ => "en" },
        };

    public static void Set(string? choice)
    {
        string lang = Resolve(choice);
        Fr = lang == "fr";
        En = lang != "pt";
    }

    public static string T(string pt, string en) => Fr ? French.Of(en) : En ? en : pt;

    /// <summary>Plural simples: um / varios.</summary>
    public static string N(int n, string ptOne, string ptMany, string enOne, string enMany) =>
        n == 1 ? T(ptOne, enOne) : T(ptMany, enMany);
}
