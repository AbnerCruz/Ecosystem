using Android.Content;
using Android.Widget;
using Android.Text;
using EcosystemAi.Product;
using System.Globalization;

namespace EcosystemAi.Android;

public sealed partial class MainActivity
{
    private ModelPreferences _modelPreferences = null!;
    private SavedModelConfiguration _modelConfiguration = SavedModelConfiguration.Empty;
    private Dictionary<string, string> _providerKeys = [];
    private string? _modelPreferencesError;
    private Action? _settingsBack;
    private int _settingsGeneration;

    private void LoadModelPreferences()
    {
        _modelPreferences = new ModelPreferences(this);
        try
        {
            var saved = _modelPreferences.Read();
            _modelConfiguration = saved.Configuration;
            _providerKeys = saved.Credentials;
            _settings = _modelConfiguration.Selected(id => _providerKeys.GetValueOrDefault(id));
        }
        catch (IOException e) { _modelPreferencesError = e.Message; }
        // Loading preferences never creates a budget authorization or calls a provider.
    }
    private void SaveModels(SavedModelConfiguration next, Dictionary<string, string>? keys = null)
    {
        if (_modelPreferencesError is not null) throw new IOException(_modelPreferencesError);
        var nextKeys = keys ?? _providerKeys;
        _modelPreferences.Save(next, nextKeys); // publish in memory only after durable save
        _modelConfiguration = next; _providerKeys = nextKeys;
        _settings = next.Selected(id => nextKeys.GetValueOrDefault(id));
        // Keep the same ProductExecutionHost and its reservations across model switches.
    }
    private void SettingsScreen(string title, Action back)
    {
        _settingsGeneration++;
        _settingsBack = back;
        var scroll = new ScrollView(this); scroll.SetBackgroundColor(_background);
        _content = Column(); _content.SetPadding(Dp(18), Dp(26), Dp(18), Dp(24));
        scroll.AddView(_content); SetContentView(scroll);
        _content.AddView(Button("← Voltar", back));
        _content.AddView(Text(title, 24, true));
    }
    private void BackToConversation() { _settingsGeneration++; _settingsBack = null; Render(); }
    public override void OnBackPressed()
    {
        if (_settingsBack is not null) { Guard(_settingsBack); return; }
        base.OnBackPressed();
    }
    private void ChooseSavedModel()
    {
        var providers = _modelConfiguration.Providers.Where(p => p.Models.Count > 0).ToArray();
        if (providers.Length == 0 || _modelPreferencesError is not null) { ShowAiSettings(); return; }
        var body = Column();
        body.AddView(Text("Provedor", 16, true));
        var provider = Select(providers.Select(p => p.Name).ToArray());
        var index = Array.FindIndex(providers, p => p.Id == _modelConfiguration.ProviderId);
        provider.SetSelection(Math.Max(0, index));
        body.AddView(provider);
        body.AddView(Text("Modelo", 16, true));
        var models = Select(Array.Empty<string>()); body.AddView(models);
        void Populate(int position)
        {
            var current = providers[position];
            models.Adapter = new ArrayAdapter<string>(this,
                global::Android.Resource.Layout.SimpleSpinnerDropDownItem,
                current.Models.Select(m => m.Name + " • " + m.Id).ToArray());
            var selected = current.Id == _modelConfiguration.ProviderId
                ? current.Models.ToList().FindIndex(m => m.Id == _modelConfiguration.ModelId) : 0;
            models.SetSelection(Math.Max(0, selected));
        }
        Populate(Math.Max(0, index));
        provider.ItemSelected += (_, args) => Populate(args.Position);
        Dialog("Selecionar modelo", body, "Usar modelo", () =>
        {
            var selected = providers[provider.SelectedItemPosition];
            SaveModels(_modelConfiguration with { ProviderId = selected.Id, ModelId = selected.Models[models.SelectedItemPosition].Id });
            BackToConversation();
        });
    }

    private void ShowAiSettings()
    {
        SettingsScreen("Configurações de IA", BackToConversation);
        if (_modelPreferencesError is not null)
        {
            _content.AddView(Text(_modelPreferencesError));
            _content.AddView(Button("Redefinir configurações de IA", () =>
            {
                Dialog("Apagar provedores e chaves salvos?", Column(), "Apagar", () =>
                {
                    _modelPreferences.Reset(); _modelPreferencesError = null;
                    _modelConfiguration = SavedModelConfiguration.Empty; _providerKeys = []; _settings = null;
                    ShowAiSettings();
                });
            }));
            return;
        }
        _content.AddView(Text("Provedores e modelos ficam salvos neste aparelho. A chave é protegida pelo Android e não vai para os projetos.", 14));
        _content.AddView(Button("+ Novo provedor", () => EditProvider(null), true));
        if (_modelConfiguration.Providers.Count == 0) _content.AddView(Text("Nenhum provedor cadastrado."));
        foreach (var provider in _modelConfiguration.Providers)
        {
            var box = Column(); box.AddView(Text(provider.Name, 20, true));
            box.AddView(Text(provider.BaseUrl, 13));
            box.AddView(Text($"{provider.Models.Count} modelos salvos • " + (SavedModelConfiguration.IsLocalHttp(SavedModelConfiguration.BaseUri(provider.BaseUrl)) ? "HTTP local, sem chave" : "chave protegida"), 13));
            box.AddView(Button("Modelos de " + provider.Name, () => ShowProviderModels(provider.Id)));
            box.AddView(Button("Editar " + provider.Name, () => EditProvider(provider)));
            box.AddView(Button("Remover " + provider.Name, () =>
                Dialog("Remover este provedor e sua chave?", Column(), "Remover", () =>
                {
                    var keys = new Dictionary<string, string>(_providerKeys); keys.Remove(provider.Id);
                    SaveModels(_modelConfiguration.RemoveProvider(provider.Id), keys); ShowAiSettings();
                })));
            Panel(box);
        }
        _content.AddView(Text(_settings is null ? "Selecione um modelo cadastrado para usar na conversa."
            : "Modelo selecionado: " + _settings.Model, 14));
    }
    private void EditProvider(SavedProvider? provider)
    {
        SettingsScreen(provider is null ? "Novo provedor" : "Editar provedor", ShowAiSettings);
        var name = Input("Nome do provedor", provider?.Name ?? "");
        var url = Input("URL base HTTPS ou HTTP local", provider?.BaseUrl ?? "");
        var key = Input(provider is null ? "Chave de API" : "Nova chave (vazio mantém a salva)", secret: true);
        _content.AddView(Text("API Chat Completions: HTTPS com chave, ou HTTP sem chave somente em localhost/127.0.0.1. No Android, localhost é o próprio aparelho, não seu computador.", 14));
        url.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
        _content.AddView(Text("Nome do provedor:", 14)); _content.AddView(name);
        _content.AddView(Text("URL base da API:", 14)); _content.AddView(url);
        _content.AddView(Text("Chave de API:", 14)); _content.AddView(key);
        _content.AddView(Button("Salvar provedor", () =>
        {
            var uri = SavedModelConfiguration.BaseUri((url.Text ?? "").Trim());
            var baseUrl = uri.AbsoluteUri.TrimEnd('/');
            var local = SavedModelConfiguration.IsLocalHttp(uri);
            var id = provider?.Id ?? Guid.NewGuid().ToString("N");
            var credential = string.IsNullOrEmpty(key.Text) ? _providerKeys.GetValueOrDefault(id) : key.Text;
            if (local && !string.IsNullOrWhiteSpace(key.Text))
                throw new ArgumentException("Não envie chave pela conexão HTTP local.");
            if (!local && (credential is null || credential.Length is < 8 or > 8192 || credential.Any(char.IsControl)))
                throw new ArgumentException("Provedores HTTPS exigem chave de API com ao menos 8 caracteres.");
            var updated = new SavedProvider(id, (name.Text ?? "").Trim(), baseUrl, provider?.Models ?? []);
            var keys = new Dictionary<string, string>(_providerKeys);
            if (local) keys.Remove(id); // Nunca transportar uma chave HTTPS antiga para HTTP.
            else keys[id] = credential!;
            var next = _modelConfiguration with { Providers = _modelConfiguration.Providers.Where(p => p.Id != id).Append(updated).ToArray() };
            SaveModels(next, keys); key.Text = ""; ShowProviderModels(id);
        }, true));
    }
    private void ShowProviderModels(string providerId)
    {
        var provider = _modelConfiguration.Providers.Single(p => p.Id == providerId);
        SettingsScreen("Modelos de " + provider.Name, ShowAiSettings);
        _content.AddView(Button("+ Adicionar modelo por ID", () => EditModel(providerId, null), true));
        var search = Input("Buscar nos modelos salvos"); _content.AddView(search);
        _content.AddView(Button("Buscar modelos do provedor", () => _ = DiscoverModels(provider)));
        var list = Column(); _content.AddView(list);
        void Paint()
        {
            list.RemoveAllViews();
            foreach (var model in provider.Models.Where(m => (m.Name + " " + m.Id).Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)))
            {
                list.AddView(Text(model.Name + (providerId == _modelConfiguration.ProviderId && model.Id == _modelConfiguration.ModelId ? " • selecionado" : ""), 18, true));
                list.AddView(Text(model.Id, 14));
                list.AddView(Button("Usar " + model.Name, () =>
                {
                    SaveModels(_modelConfiguration with { ProviderId = providerId, ModelId = model.Id });
                    BackToConversation();
                }));
                list.AddView(Button("Editar " + model.Name, () => EditModel(providerId, model)));
                list.AddView(Button("Remover " + model.Name, () => Dialog("Remover este modelo?", Column(), "Remover", () =>
                {
                    var changed = provider with { Models = provider.Models.Where(m => m.Id != model.Id).ToArray() };
                    var selected = _modelConfiguration.ProviderId == providerId && _modelConfiguration.ModelId == model.Id;
                    SaveModels(_modelConfiguration with
                    {
                        Providers = _modelConfiguration.Providers.Select(p => p.Id == providerId ? changed : p).ToArray(),
                        ProviderId = selected ? null : _modelConfiguration.ProviderId,
                        ModelId = selected ? null : _modelConfiguration.ModelId
                    });
                    ShowProviderModels(providerId);
                })));
            }
        }
        search.TextChanged += (_, _) => Paint(); Paint();
    }
    private void EditModel(string providerId, SavedModel? model, string? discoveredId = null)
    {
        SettingsScreen(model is null ? "Adicionar modelo" : "Editar modelo", () => ShowProviderModels(providerId));
        var id = Input("ID do modelo", model?.Id ?? discoveredId ?? "");
        var name = Input("Nome do modelo", model?.Name ?? discoveredId ?? "");
        var input = Input("Entrada USD por milhão de tokens", (model?.InputUsdPerMillion ?? 0).ToString(CultureInfo.InvariantCulture));
        var output = Input("Saída USD por milhão de tokens", (model?.OutputUsdPerMillion ?? 0).ToString(CultureInfo.InvariantCulture));
        var run = Input("Centavos USD por execução", (model?.RunBudgetCents ?? 20).ToString(CultureInfo.InvariantCulture));
        var call = Input("Centavos USD por chamada", (model?.MaxCallCents ?? 5).ToString(CultureInfo.InvariantCulture));
        var history = new CheckBox(this) { Text = "Reenviar histórico da conversa", Checked = model?.UseHistory ?? false }; history.SetTextColor(_ink);
        id.InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions;
        input.InputType = output.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
        run.InputType = call.InputType = InputTypes.ClassNumber;
        foreach (var (label, view) in new (string, global::Android.Views.View)[]
        {
            ("ID do modelo:", id), ("Nome do modelo:", name),
            ("Entrada USD por milhão de tokens:", input), ("Saída USD por milhão de tokens:", output),
            ("Centavos USD por execução:", run), ("Centavos USD por chamada:", call)
        }) { _content.AddView(Text(label, 14)); _content.AddView(view); }
        _content.AddView(history);
        _content.AddView(Text("Confira os preços do modelo. Zero só deve ser usado quando a tarifa informada for zero.", 13));
        _content.AddView(Button("Salvar modelo", () =>
        {
            decimal Price(EditText e) => decimal.Parse((e.Text ?? "").Replace(',', '.'), CultureInfo.InvariantCulture);
            var changed = new SavedModel((id.Text ?? "").Trim(), (name.Text ?? "").Trim(), Price(input), Price(output),
                long.Parse(run.Text ?? "", CultureInfo.InvariantCulture), long.Parse(call.Text ?? "", CultureInfo.InvariantCulture), history.Checked);
            var provider = _modelConfiguration.Providers.Single(p => p.Id == providerId);
            if (provider.Models.Any(m => m.Id == changed.Id && m.Id != model?.Id)) throw new ArgumentException("Este modelo já está salvo no provedor.");
            var updated = provider with { Models = provider.Models.Where(m => m.Id != model?.Id).Append(changed).ToArray() };
            SaveModels(_modelConfiguration with
            {
                Providers = _modelConfiguration.Providers.Select(p => p.Id == providerId ? updated : p).ToArray(),
                ModelId = _modelConfiguration.ProviderId == providerId && _modelConfiguration.ModelId == model?.Id ? changed.Id : _modelConfiguration.ModelId
            });
            ShowProviderModels(providerId);
        }, true));
    }
    private async Task DiscoverModels(SavedProvider provider)
    {
        var generation = _settingsGeneration;
        try
        {
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
            var ids = await ProviderModelDiscovery.ListAsync(http, provider, _providerKeys.GetValueOrDefault(provider.Id));
            if (IsFinishing || IsDestroyed || generation != _settingsGeneration) return;
            if (ids.Count == 0) { Notice("Modelos", "Nenhum modelo retornado. Você pode informar o ID manualmente."); return; }
            RunOnUiThread(() =>
            {
                SettingsScreen("Modelos disponíveis", () => ShowProviderModels(provider.Id));
                var search = Input("Buscar modelo no provedor"); _content.AddView(search);
                var list = Column(); _content.AddView(list);
                void Paint()
                {
                    list.RemoveAllViews();
                    foreach (var id in ids.Where(id => id.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)).Take(100))
                        list.AddView(Button(id, () => EditModel(provider.Id, provider.Models.SingleOrDefault(m => m.Id == id), id)));
                }
                search.TextChanged += (_, _) => Paint(); Paint();
            });
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (!IsFinishing && !IsDestroyed) RunOnUiThread(() => Notice("Modelos", "Não foi possível consultar os modelos. Confira a conexão e a chave, ou adicione o ID manualmente."));
        }
    }
    private void AuthorizeBudget()
    {
        if (_settings is null) { ShowAiSettings(); return; }
        if (_execution is not null)
        {
            Notice("Orçamento da instância", $"Restam {_execution.RemainingCents} centavos USD e {_execution.RemainingRuns} execuções. Trocar de modelo mantém as reservas já feitas.");
            return;
        }
        var body = Column(); body.AddView(Text("O modelo e, quando necessário, a chave já estão salvos. Autorize o teto desta abertura do aplicativo.", 14));
        var limit = Input("Teto em centavos USD", "100"); body.AddView(limit);
        Dialog("Autorizar gasto", body, "Autorizar", () =>
        {
            var amount = long.Parse(limit.Text ?? "", CultureInfo.InvariantCulture);
            if (amount < _settings.RunBudgetCents) throw new ArgumentException("Teto menor que uma execução.");
            _execution = new ProductExecutionHost(_catalog, _journal, amount);
            Render();
        });
    }
}
