namespace OFS.Sdk;

/// <summary>
/// Scene-bound high-level UI for live gameplay. Handles never outlive the
/// Factory scene that owns their cloned Unity objects.
/// </summary>
public interface IGameplayUiApi
{
    bool IsAvailable { get; }
    IGameplayHud CreateHud(GameplayHudDefinition definition);
    IGameplayPanel CreatePanel(GameplayPanelDefinition definition);
    IComputerAppRegistration RegisterComputerApp(ComputerAppDefinition definition) =>
        throw new NotSupportedException("This OFS runtime cannot register computer applications.");
}

public sealed record ComputerAppDefinition(
    string Id,
    string Label,
    Action OnPressed,
    bool Visible = true);

/// <summary>
/// Scene-bound entry in the factory computer launcher. The runtime materializes
/// it when the vanilla computer UI is loaded and routes selection to managed code.
/// </summary>
public interface IComputerAppRegistration : IDisposable
{
    string Id { get; }
    string OwnerId { get; }
    string Label { get; set; }
    bool Visible { get; set; }
    bool IsMaterialized { get; }
    bool IsAlive { get; }
    void Remove();
}

public enum GameplayUiAnchor
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3,
}

public sealed record GameplayHudDefinition(
    string Id,
    string Title,
    string Text,
    GameplayUiAnchor Anchor = GameplayUiAnchor.TopRight,
    float OffsetX = 0f,
    float OffsetY = 0f,
    bool Visible = true);

/// <summary>A non-modal vanilla-styled status card rendered over gameplay.</summary>
public interface IGameplayHud : IDisposable
{
    string Id { get; }
    string OwnerId { get; }
    string Title { get; set; }
    string Text { get; set; }
    GameplayUiAnchor Anchor { get; set; }
    float OffsetX { get; set; }
    float OffsetY { get; set; }
    bool Visible { get; set; }
    bool IsAlive { get; }
    void Show();
    void Hide();
    void Remove();
}

public sealed record GameplayPanelDefinition(
    string Id,
    string Title,
    string Body,
    Action<GameplayPanelClosedEvent>? Closed = null);

public sealed record GameplayPanelButtonDefinition(
    string Id,
    string Label,
    Action OnPressed,
    bool ClosePanel = false);

public enum GameplayPanelCloseReason
{
    ModRequested = 0,
    UserCancelled = 1,
    Button = 2,
    Replaced = 3,
    SceneUnloaded = 4,
    Removed = 5,
    Error = 6,
}

public readonly record struct GameplayPanelClosedEvent(
    IGameplayPanel Panel,
    GameplayPanelCloseReason Reason);

/// <summary>A modal vanilla-styled gameplay panel with up to four actions.</summary>
public interface IGameplayPanel : IDisposable
{
    string Id { get; }
    string OwnerId { get; }
    string Title { get; set; }
    string Body { get; set; }
    bool Visible { get; }
    bool IsAlive { get; }
    IGameplayPanelButton AddButton(GameplayPanelButtonDefinition definition);
    bool RemoveButton(string id);
    void ClearButtons();
    void Show();
    void Close(GameplayPanelCloseReason reason = GameplayPanelCloseReason.ModRequested);
    void Remove();
}

public interface IGameplayPanelButton : IDisposable
{
    string Id { get; }
    string Label { get; set; }
    bool Visible { get; set; }
    bool IsAlive { get; }
    void Remove();
}
