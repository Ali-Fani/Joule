using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BatteryTray.Core;
using BatteryTray.Interop;
using BatteryTray.Scan;

namespace BatteryTray.Ui;

/// <summary>
/// "Enviar um aparelho" (telas aprovadas no Claude Design em 2026-11-07:
/// https://claude.ai/artifact/UEuwDLdvBMspxkBezFobWJ; revisao de textos,
/// Voltar, marcar o aparelho e cartoes GitHub/e-mail aprovada em 2026-11-14:
/// https://claude.ai/artifact/2qsAxqAhPx2VfvTS38hLkM). Coleta como o
/// JouleScan, escuta 30 s, mostra o que achou, tira os dados pessoais e deixa
/// a pessoa enviar pelo GitHub ou por e-mail. A coleta e' sob demanda (o
/// clique de quem abriu) e a escuta e' passiva: nada e' escrito no aparelho.
/// </summary>
public partial class SendDeviceWindow : WizardWindow
{
    private const int Before = 0, Listening = 1, Found = 2, Review = 3, GitHub = 4, Email = 5;

    private readonly IReadOnlyList<DeviceEntry> _lastReading;
    private ScanReport? _report;
    private IReadOnlyList<HidCollection>? _collections;
    private CancellationTokenSource? _listen;
    private string? _file;
    private ScanReport? _clean;                  // o relatorio sem dados pessoais, para o texto do relato
    private readonly HashSet<string> _pointed = new(StringComparer.OrdinalIgnoreCase); // "este e' o meu"
    private bool _listenFailed;
    private bool _emailFromGitHub;               // Voltar do e-mail volta para onde a pessoa estava

    public SendDeviceWindow(IReadOnlyList<DeviceEntry> lastReading)
    {
        _lastReading = lastReading;
        InitializeComponent();
        InitWizard(Root, Lift, Pages, Dots, 5);

        Title = L.T($"Enviar um aparelho · {AppInfo.Name}", $"Submit a device · {AppInfo.Name}");
        BrandText.Text = AppInfo.Name;
        AddNameRow();

        AddNameButton.Click += (_, _) => AddNameRow().Focus();
        CancelButton.Click += (_, _) => Leave();
        PrimaryButton.Click += (_, _) => OnPrimary();
        SecondaryButton.Click += (_, _) => OnSecondary();
        BackButton.Click += (_, _) => OnBack();
        NotepadButton.Click += (_, _) => Open("notepad.exe", Quote(_file!));
        ToEmailLink.Click += (_, _) => { _emailFromGitHub = true; Go(Email); };
        SimilarLink.Click += (_, _) => { if (Names().FirstOrDefault() is { } name) Open(DeviceSubmission.SearchUrl(AppInfo.IssuesUrl, name)); };
        ViaGitHub.Checked += (_, _) => UpdateChrome();
        ViaEmail.Checked += (_, _) => UpdateChrome();
        GmailButton.Click += (_, _) => Open(DeviceSubmission.GmailUrl(AppInfo.ContactEmail, Subject, MailBody));
        OutlookButton.Click += (_, _) => Open(DeviceSubmission.OutlookUrl(AppInfo.ContactEmail, Subject, MailBody));
        MailAppButton.Click += (_, _) => Open(DeviceSubmission.MailtoUrl(AppInfo.ContactEmail, Subject, MailBody));
        CopyAddressButton.Click += (_, _) => CopyAddress();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Leave(); };
        // Fechar no meio da escuta encerra os handles (um CancelIoEx cada, no PassiveCapture).
        Closed += (_, _) => _listen?.Cancel();

        RepoText.Text = L.T($"{AppInfo.Repository}/issues · público", $"{AppInfo.Repository}/issues · public");
        AddressText.Text = L.T($"Outro serviço? Mande para {AppInfo.ContactEmail}", $"Another service? Send to {AppInfo.ContactEmail}");
        MailAppButton.Visibility = HasMailApp() ? Visibility.Visible : Visibility.Collapsed;
        MailButtons.Columns = MailAppButton.Visibility == Visibility.Visible ? 3 : 2;
        NoAccountNote.Visibility = HasEmail ? Visibility.Visible : Visibility.Collapsed;
        SendChoice.Visibility = HasEmail ? Visibility.Visible : Visibility.Collapsed;
        foreach (string removed in new[] { L.T("Número de série", "Serial number"), L.T("Endereço Bluetooth", "Bluetooth address"), L.T("Nome do computador", "Computer name"), L.T("Nome de usuário", "User name") })
            RemovedGrid.Children.Add(Line(removed, "M5 10h10", "Text.Tertiary", secondary: true));
        RemovedPhones.Content = Line(L.T("Celulares, caixas de som e outros Bluetooth que não são periféricos", "Phones, speakers and other Bluetooth devices that aren’t peripherals"), "M5 10h10", "Text.Tertiary", secondary: true);
        int n = 1;
        foreach (string step in new[] { L.T("Entre na sua conta do GitHub, se for solicitado.", "Sign in to your GitHub account, if asked."), L.T("Arraste o arquivo abaixo para a caixa de descrição.", "Drag the file below into the description box."), L.T("Clique no botão verde para publicar o relato.", "Click the green button to publish the report.") })
            StepsList.Children.Add(Step(n++, step));
    }

    private static bool HasEmail => AppInfo.ContactEmail.Length > 0;

    private IEnumerable<(string Name, string Percent)> Rows() => NameRows.Children.OfType<DockPanel>()
        .Select(row => (row.Children.OfType<TextBox>().First(b => b.Uid == "name").Text.Trim(),
                        row.Children.OfType<TextBox>().First(b => b.Uid == "pct").Text.Trim()));

    private List<string> Names() => Rows().Select(r => r.Name).Where(t => t.Length > 0).ToList();

    /// <summary>Bateria que a pessoa leu no programa da marca, por aparelho (0 a 100; o resto e' ignorado).</summary>
    private Dictionary<string, int> Levels()
    {
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int n = 0;
        foreach (var (name, pct) in Rows())
        {
            n++;
            if (int.TryParse(pct, out int v) && v is >= 0 and <= 100)
                levels[name.Length > 0 ? name : L.T($"Aparelho {n}", $"Device {n}")] = v;
        }
        return levels;
    }

    private string Subject => DeviceSubmission.Title(Names());
    private string IssueBody => DeviceSubmission.Body(Names(), Path.GetFileName(_file ?? ""), AppInfo.Version, _clean);
    private string MailBody => DeviceSubmission.Body(Names(), Path.GetFileName(_file ?? ""), AppInfo.Version, _clean, email: true);

    private TextBox AddNameRow()
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var box = new TextBox { Style = (Style)FindResource("Field"), Uid = "name", Tag = L.T("Marca e modelo, como está na caixa", "Brand and model, as on the box") };
        AutomationName(box, L.T($"Aparelho {NameRows.Children.Count + 1}", $"Device {NameRows.Children.Count + 1}"));
        var pct = new TextBox { Style = (Style)FindResource("Field"), Uid = "pct", Width = 96, MaxLength = 3, Margin = new Thickness(6, 0, 0, 0), Tag = L.T("Bateria %", "Battery %") };
        AutomationName(pct, L.T($"Bateria do aparelho {NameRows.Children.Count + 1}, em porcentagem", $"Battery of device {NameRows.Children.Count + 1}, in percent"));
        pct.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        DataObject.AddPastingHandler(pct, (_, e) => { if (e.DataObject.GetData(DataFormats.UnicodeText) is not string t || !t.All(char.IsDigit)) e.CancelCommand(); });
        if (NameRows.Children.Count > 0)
        {
            var remove = new Button { Style = (Style)FindResource("FlatButton"), Width = 34, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(0) };
            remove.Content = new Viewbox { Width = 12, Height = 12, Child = new System.Windows.Shapes.Path { Style = (Style)FindResource("Glyph"), Data = Geometry.Parse("M5 5l10 10M15 5 5 15"), Stroke = (Brush)FindResource("Text.Tertiary"), StrokeThickness = 1.8 } };
            AutomationName(remove, L.T("Tirar este aparelho", "Remove this device"));
            remove.Click += (_, _) => NameRows.Children.Remove(row);
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
        }
        DockPanel.SetDock(pct, Dock.Right);
        row.Children.Add(pct);
        row.Children.Add(box);
        NameRows.Children.Add(row);
        return box;
    }

    private bool Listened => _report?.Capture is not null;
    private bool ByEmail => HasEmail && ViaEmail.IsChecked == true;

    private void OnPrimary()
    {
        switch (Page)
        {
            case Before: Go(Listened ? Found : Listening); break; // voltou para corrigir o nome: segue sem escutar de novo
            case Listening when _listenFailed: _listenFailed = false; StartListening(); UpdateChrome(); break;
            case Found: Go(Review); break;
            case Review when ByEmail: _emailFromGitHub = false; Go(Email); break;
            case Review:
                Open(DeviceSubmission.GitHubUrl(AppInfo.NewIssueUrl, Subject, IssueBody));
                Go(GitHub);
                break;
            default: Leave(); break; // Concluir
        }
    }

    private void OnSecondary()
    {
        switch (Page)
        {
            case Before or Found: Go(Listening); break; // Escutar de novo, nas mesmas colecoes; soma a escuta anterior
            case Listening: _listen?.Cancel(); break;   // Encerrar a escuta: segue com o que chegou
            case GitHub: Open(DeviceSubmission.GitHubUrl(AppInfo.NewIssueUrl, Subject, IssueBody)); break;
        }
    }

    private void OnBack()
    {
        switch (Page)
        {
            case Found: Go(Before); break;
            case Review: Go(Found); break;
            case GitHub: Go(Review); break;
            case Email: Go(_emailFromGitHub ? GitHub : Review); break;
        }
    }

    protected override int DotFor(int page) => Math.Min(page, GitHub);

    protected override void OnPageShown(int page, int from, TimeSpan start)
    {
        if (page == Listening && from != Listening) StartListening();
        if (page == Found) ShowFound();
        if (page == Review) SaveFile();
        if (page is GitHub or Email)
        {
            GitChipSlot.Content = page == GitHub ? FileChip(L.T("Arraste daqui", "Drag from here")) : null;
            SimilarNote.Visibility = Names().Any() ? Visibility.Visible : Visibility.Collapsed;
            MailChipSlot.Content = page == Email ? FileChip(L.T("Anexe este arquivo, ou arraste daqui", "Attach this file, or drag from here")) : null;
        }
    }

    protected override void UpdateChrome()
    {
        (string? secondary, string? primary) = Page switch
        {
            Before when Listened => (L.T("Escutar de novo", "Listen again"), L.T("Continuar", "Continue")),
            Before => (null, L.T("Iniciar a escuta", "Start listening")),
            Listening when _listenFailed => (null, L.T("Tentar de novo", "Try again")),
            Listening => (L.T("Encerrar a escuta", "End listening"), null),
            Found => (L.T("Escutar de novo", "Listen again"), L.T("Continuar", "Continue")),
            Review => (null, ByEmail ? L.T("Enviar por e-mail", "Send by email") : L.T("Enviar pelo GitHub", "Send via GitHub")),
            GitHub => (L.T("Abrir a página novamente", "Open the page again"), L.T("Concluir", "Done")),
            _ => (null, L.T("Concluir", "Done")),
        };
        SecondaryButton.Content = secondary;
        SecondaryButton.Visibility = secondary is null ? Visibility.Collapsed : Visibility.Visible;
        PrimaryButton.Content = primary;
        PrimaryButton.Visibility = primary is null ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.Visibility = Page < GitHub ? Visibility.Visible : Visibility.Hidden;
        BackButton.Visibility = Page is Before or Listening ? Visibility.Collapsed : Visibility.Visible;
        base.UpdateChrome();
    }

    /// <summary>
    /// Primeira vez: inventario (HidInventory.Scan pede strings ao aparelho,
    /// por isso so aqui, uma vez) e depois a escuta. "Escutar de novo" repete
    /// so a escuta, nas colecoes da primeira varredura.
    /// </summary>
    private void StartListening()
    {
        SecondsText.Text = DeviceSubmission.ListenSeconds.ToString();
        TimeFill.Width = 0;
        LiveDot.Visibility = Visibility.Visible;
        LiveText.Foreground = (Brush)FindResource("Text.Secondary");
        LiveText.Text = L.T("Preparando…", "Preparing…");
        _listen = new CancellationTokenSource();
        var cancel = _listen.Token;
        var names = Names();

        Task.Run(() =>
        {
            if (_report is null) (_report, _collections) = DeviceSubmission.Inventory(_lastReading, names);
            Dispatcher.BeginInvoke(() => LiveText.Text = L.T("Aguardando dados… use os aparelhos", "Waiting for data… use your devices"));
            DeviceSubmission.Listen(_report, _collections!, (elapsed, talking) => Dispatcher.BeginInvoke(() =>
            {
                SecondsText.Text = (DeviceSubmission.ListenSeconds - elapsed).ToString();
                TimeFill.Width = TimeTrack.ActualWidth * elapsed / DeviceSubmission.ListenSeconds;
                LiveText.Text = talking switch
                {
                    0 => L.T("Nenhum aparelho enviou dados ainda", "No device has sent data yet"),
                    1 => L.T("Recebendo dados de 1 aparelho", "Receiving data from 1 device"),
                    _ => L.T($"Recebendo dados de {talking} aparelhos", $"Receiving data from {talking} devices"),
                };
            }), cancel);
        }).ContinueWith(task => Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded) return; // janela fechada no meio
            if (task.Exception is { } ex)
            {
                Log.Write($"Enviar um aparelho: falha na coleta: {ex.GetBaseException().Message}");
                LiveDot.Visibility = Visibility.Collapsed;
                LiveText.Foreground = (Brush)FindResource("Level.Alert");
                LiveText.Text = L.T("Não foi possível escutar os aparelhos.", "Couldn’t listen to the devices.");
                _listenFailed = true;
                UpdateChrome();
                return;
            }
            Log.Write($"Enviar um aparelho: coleta pronta ({_report!.Hid.Count} HID, {_report.Capture?.Groups.Count ?? 0} formatos na escuta).");
            Go(Found);
        }));
    }

    private void ShowFound()
    {
        var (read, unknown) = DeviceSubmission.Summarize(_report!);
        ReadList.Children.Clear();
        UnknownList.Children.Clear();
        foreach (var f in read) ReadList.Children.Add(ReadRow(f.Name, f.Detail));
        foreach (var f in unknown.Take(5)) UnknownList.Children.Add(PickRow(f));
        if (unknown.Count > 5)
            UnknownList.Children.Add(new TextBlock { Style = (Style)FindResource("Caption"), Margin = new Thickness(28, 4, 0, 0), Text = L.T($"E mais {unknown.Count - 5}. Todos vão no arquivo.", $"And {unknown.Count - 5} more. They all go in the file.") });
        ReadLabel.Visibility = ReadList.Visibility = read.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UnknownLabel.Visibility = UnknownList.Visibility = Legend.Visibility = unknown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        FoundLead.Text = unknown.Count > 0
            ? L.T("Marque o aparelho que o Joule deve aprender a ler.", "Check the device Joule should learn to read.")
            : L.T("Nenhum aparelho sem suporte apareceu na lista.", "No unsupported device showed up in the list.");
        _pointed.IntersectWith(unknown.Select(f => f.HardwareId)); // marcado que sumiu da lista nao vai
        UpdateQuietHint(unknown);
    }

    /// <summary>Conselho so quando importa: o aparelho marcado nao mandou nada na escuta.</summary>
    private void UpdateQuietHint(IReadOnlyList<DeviceSubmission.Found> unknown)
    {
        var quiet = unknown.Where(f => !f.Talked && _pointed.Contains(f.HardwareId)).Select(f => f.Name).ToList();
        QuietHint.Visibility = quiet.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        QuietText.Text = quiet.Count == 1
            ? L.T($"O {quiet[0]} não enviou dados. Use “Escutar de novo” enquanto mexe nele.", $"{quiet[0]} sent no data. Use “Listen again” while using it.")
            : L.T("Os aparelhos marcados não enviaram dados. Use “Escutar de novo” enquanto mexe neles.", "The checked devices sent no data. Use “Listen again” while using them.");
    }

    private void SaveFile()
    {
        _report!.ReportedDevices = Names();
        var levels = Levels();
        _report.ReportedBattery = levels.Count > 0 ? levels : null;
        _report.PointedDevices = _pointed.Count > 0 ? _pointed.ToList() : null;
        _clean = ScanPrivacy.Redact(_report);
        _file = DeviceSubmission.Save(DeviceSubmission.Render(_report), DateTime.Now, _file); // Voltar e revisar de novo regrava o mesmo arquivo
        Log.Write($"Enviar um aparelho: arquivo salvo ({new FileInfo(_file).Length / 1024} KB, {_pointed.Count} apontado(s), {levels.Count} bateria(s) informada(s)).");

        int devices = _report.Hid.Count(d => d.Transport != "virtual");
        GoesList.Children.Clear();
        foreach (string goes in new[] { L.T($"Os {devices} aparelhos que o Windows lista e o que cada um informa sobre bateria", $"The {devices} devices Windows lists and what each reports about battery"), L.T($"Os dados que eles enviaram em {_report.Capture?.Seconds ?? 0} segundos", $"The data they sent in {_report.Capture?.Seconds ?? 0} seconds"), L.T("As versões do Windows e do Joule", "The Windows and Joule versions") })
            GoesList.Children.Add(Line(goes, "m4.5 10.5 3.5 3.5 7.5-8", "Accent", secondary: false));
    }

    /// <summary>"Copiar endereco" vira "Copiado" por 2 s: sem isso o clique parecia nao fazer nada.</summary>
    private async void CopyAddress()
    {
        try { Clipboard.SetText(AppInfo.ContactEmail); }
        catch { return; } // area de transferencia ocupada: o texto continua igual
        CopyAddressButton.Content = L.T("✓ Copiado", "✓ Copied");
        await Task.Delay(2000);
        CopyAddressButton.Content = L.T("Copiar endereço", "Copy address");
    }

    // ---- pecas das telas ----

    private UIElement Line(string text, string glyph, string brush, bool secondary)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 0, 0, secondary ? 6 : 8) };
        var icon = new Viewbox
        {
            Width = secondary ? 14 : 16, Height = secondary ? 14 : 16, Margin = new Thickness(0, 2, secondary ? 8 : 10, 0), VerticalAlignment = VerticalAlignment.Top,
            Child = new System.Windows.Shapes.Path { Style = (Style)FindResource("Glyph"), Data = Geometry.Parse(glyph), Stroke = (Brush)FindResource(brush), StrokeThickness = 1.8 },
        };
        dock.Children.Add(icon);
        dock.Children.Add(new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap,
            FontSize = secondary ? 12 : 14, LineHeight = secondary ? 17 : 20,
            Foreground = (Brush)FindResource(secondary ? "Text.Secondary" : "Text.Primary"),
        });
        return dock;
    }

    private UIElement Step(int number, string text)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var badge = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Margin = new Thickness(0, 0, 12, 0), Background = (Brush)FindResource("Tile.Fill") };
        badge.Child = new TextBlock { Text = number.ToString(), FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        dock.Children.Add(badge);
        dock.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        return dock;
    }

    private UIElement ReadRow(string name, string value)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
        dock.Children.Add(new Viewbox { Width = 16, Height = 16, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Child = new System.Windows.Shapes.Path { Style = (Style)FindResource("Glyph"), Data = Geometry.Parse("m4.5 10.5 3.5 3.5 7.5-8"), Stroke = (Brush)FindResource("Accent"), StrokeThickness = 1.8 } });
        var v = new TextBlock { Text = value, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        DockPanel.SetDock(v, Dock.Right);
        dock.Children.Add(v);
        dock.Children.Add(new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        return dock;
    }

    /// <summary>Aparelho sem suporte: caixinha para "este e' o meu", nome, ligacao e a bolinha de quem enviou dados.</summary>
    private UIElement PickRow(DeviceSubmission.Found f)
    {
        var dock = new DockPanel();
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 7, Height = 7, Margin = new Thickness(12, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center,
            Fill = f.Talked ? (Brush)FindResource("Accent") : Brushes.Transparent,
            Stroke = f.Talked ? null : (Brush)FindResource("Text.Tertiary"), StrokeThickness = 1.5,
        };
        DockPanel.SetDock(dot, Dock.Right);
        dock.Children.Add(dot);
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = f.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = f.Detail, Style = (Style)FindResource("Caption") });
        dock.Children.Add(text);

        var mark = new CheckBox { Style = (Style)FindResource("Mark"), Content = dock, IsChecked = _pointed.Contains(f.HardwareId), Margin = new Thickness(0, 1, 0, 1) };
        AutomationName(mark, L.T($"{f.Name}, {(f.Talked ? "enviou dados" : "não enviou dados")}", $"{f.Name}, {(f.Talked ? "sent data" : "sent no data")}"));
        mark.Checked += (_, _) => { _pointed.Add(f.HardwareId); UpdateQuietHint(DeviceSubmission.Summarize(_report!).Unknown); };
        mark.Unchecked += (_, _) => { _pointed.Remove(f.HardwareId); UpdateQuietHint(DeviceSubmission.Summarize(_report!).Unknown); };
        return mark;
    }

    /// <summary>O arquivo, para arrastar ao GitHub ou ao e-mail, com "Mostrar na pasta".</summary>
    private UIElement FileChip(string hint)
    {
        var chip = new Border
        {
            Padding = new Thickness(12, 10, 8, 10), CornerRadius = new CornerRadius(6), Cursor = Cursors.Hand,
            Background = (Brush)FindResource("Card.Fill"), BorderBrush = (Brush)FindResource("Level.Track"), BorderThickness = new Thickness(1),
        };
        var dock = new DockPanel();
        var folder = new Button { Style = (Style)FindResource("FlatButton"), Content = L.T("Mostrar na pasta", "Show in folder") };
        folder.Click += (_, _) => Open("explorer.exe", $"/select,{Quote(_file!)}");
        DockPanel.SetDock(folder, Dock.Right);
        dock.Children.Add(folder);
        dock.Children.Add(new Viewbox
        {
            Width = 20, Height = 20, Margin = new Thickness(0, 0, 12, 0),
            Child = new System.Windows.Shapes.Path { Style = (Style)FindResource("Glyph"), Data = Geometry.Parse("M5.5 2.5h6l3 3v11a1 1 0 0 1-1 1h-8a1 1 0 0 1-1-1v-13a1 1 0 0 1 1-1zM11.5 2.5v3h3M7.5 10h5M7.5 13h5"), Stroke = (Brush)FindResource("Text.Secondary"), StrokeThickness = 1.5 },
        });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = Path.GetFileName(_file), TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = hint, Style = (Style)FindResource("Caption") });
        dock.Children.Add(text);
        chip.Child = dock;
        // Arrastar o arquivo direto para a pagina do GitHub ou para o e-mail.
        chip.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null || _file is null) return;
            e.Handled = true; // sem isto a janela trata o clique como "arrastar a janela"
            DragFile(chip, e.GetPosition(chip));
        };
        return chip;
    }

    /// <summary>
    /// Arrastar com retorno visivel (2026-11-14; antes nada mexia e parecia
    /// que nao estava arrastando): o cartao acende e fica vazado no lugar, e uma copia dele sai
    /// pelo ponto onde foi pego e segue o cursor ate o e-mail ou a pagina.
    /// </summary>
    private void DragFile(Border chip, Point grab)
    {
        var dpi = VisualTreeHelper.GetDpi(chip);
        var shot = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)Math.Ceiling(chip.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(chip.ActualHeight * dpi.DpiScaleY),
            96 * dpi.PixelsPerInchX / 96, 96 * dpi.PixelsPerInchY / 96, PixelFormats.Pbgra32);
        shot.Render(chip);

        const double margin = 16; // espaco para a sombra
        var scale = new ScaleTransform(1, 1);
        var card = new Border
        {
            Width = chip.ActualWidth, Height = chip.ActualHeight, Margin = new Thickness(margin), CornerRadius = new CornerRadius(6),
            Background = new ImageBrush(shot), BorderBrush = (Brush)FindResource("Accent"), BorderThickness = new Thickness(1.5),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35 },
            RenderTransformOrigin = new Point(grab.X / chip.ActualWidth, grab.Y / chip.ActualHeight), RenderTransform = scale, Opacity = 0,
        };
        var ghost = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, Topmost = true,
            ShowInTaskbar = false, ShowActivated = false, SizeToContent = SizeToContent.WidthAndHeight, Content = card,
        };
        Native.MakeToolWindow(ghost, noActivate: true, clickThrough: true); // o soltar cai no que esta embaixo, nunca na copia
        void Follow()
        {
            var (x, y) = Native.CursorPos();
            Native.MoveWindowTo(ghost, x - (int)((margin + grab.X) * dpi.DpiScaleX), y - (int)((margin + grab.Y) * dpi.DpiScaleY));
        }
        void OnFeedback(object? _, GiveFeedbackEventArgs __) => Follow();

        ghost.Show();
        Follow();
        Motion.Run(card, OpacityProperty, 0, 0.94, 160, Motion.Out);
        Motion.Run(scale, ScaleTransform.ScaleXProperty, 1, 1.04, 220, Motion.Out);
        Motion.Run(scale, ScaleTransform.ScaleYProperty, 1, 1.04, 220, Motion.Out);
        var track = chip.BorderBrush;
        chip.BorderBrush = (Brush)FindResource("Accent");
        Motion.Run(chip.Child, OpacityProperty, 1, 0.35, 160, Motion.Out);

        chip.GiveFeedback += OnFeedback;
        try { DragDrop.DoDragDrop(chip, new DataObject(DataFormats.FileDrop, new[] { _file! }), DragDropEffects.Copy); }
        finally
        {
            chip.GiveFeedback -= OnFeedback;
            chip.BorderBrush = track;
            Motion.Run(chip.Child, OpacityProperty, null, 1, 200, Motion.Out);
            Motion.Run(scale, ScaleTransform.ScaleXProperty, null, 0.9, 120, Motion.In);
            Motion.Run(scale, ScaleTransform.ScaleYProperty, null, 0.9, 120, Motion.In);
            Motion.Run(card, OpacityProperty, null, 0, 120, Motion.In, ghost.Close);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T) d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as T;
    }

    private static void AutomationName(DependencyObject d, string name) => System.Windows.Automation.AutomationProperties.SetName(d, name);

    private static string Quote(string path) => $"\"{path}\"";

    private static void Open(string target, string? args = null)
    {
        try { Process.Start(new ProcessStartInfo(target) { Arguments = args ?? "", UseShellExecute = true }); }
        catch (Exception ex) { Log.Write($"Enviar um aparelho: nao abriu {target.Split('?')[0]}: {ex.Message}"); }
    }

    /// <summary>O Windows tem um programa escolhido para mailto:? Sem ele, o clique so abriria "Como deseja abrir".</summary>
    private static bool HasMailApp()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\mailto\UserChoice");
            return key?.GetValue("ProgId") is string { Length: > 0 };
        }
        catch { return false; }
    }
}
