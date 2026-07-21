namespace OFS.Sdk;

/// <summary>Entrypoint implemented by every OFS code mod.</summary>
public interface IOFSMod
{
    /// <summary>Loads the mod and registers its events and content.</summary>
    void Load(IModContext context);

    /// <summary>Releases managed resources during an orderly framework shutdown.</summary>
    void Unload() { }
}

/// <summary>Services and directories owned by one loaded mod.</summary>
public interface IModContext
{
    ModInfo Mod { get; }
    IModRuntimeInfo Runtime =>
        throw new NotSupportedException("Runtime environment information is unavailable.");
    string GameDirectory { get; }
    string ModDirectory { get; }
    string ConfigDirectory { get; }
    IModLogger Log { get; }
    IModEvents Events { get; }
    IMainThreadScheduler MainThread { get; }
    IUnityApi Unity { get; }
    IModAssets Assets { get; }
    IGameContent Content { get; }
    IWorldApi World { get; }
    IModPhysicsApi Physics =>
        throw new NotSupportedException("Runtime physics are unavailable in this runtime.");
    IEntityApi Entities { get; }
    INpcApi Npcs { get; }
    IPlayerApi Players =>
        throw new NotSupportedException("Player discovery is unavailable in this runtime.");
    IInteractionApi Interactions { get; }
    IDialogueApi Dialogues { get; }
    IGameplayUiApi GameplayUi =>
        throw new NotSupportedException("Gameplay HUD and panels are unavailable in this runtime.");
    IModRegistry Mods =>
        throw new NotSupportedException("Loaded-mod discovery is unavailable in this runtime.");
    IModMessageBus Messages =>
        throw new NotSupportedException("Local mod messaging is unavailable in this runtime.");
    INetworkApi Network { get; }
    IModMechanics Mechanics { get; }
    IModInput Input { get; }
    IModConfig Config { get; }
    ILocalizationApi Localization { get; }
    IModSaveData SaveData { get; }
    IModHooks Hooks { get; }
    IUnsafeIl2CppApi UnsafeIl2Cpp { get; }
}

/// <summary>Lifecycle events dispatched on Unity's main thread.</summary>
public interface IModEvents
{
    event Action<IMainMenuApi>? MainMenuReady;
    event Action<SceneEvent>? SceneLoaded;
    event Action<SceneEvent>? SceneUnloaded;
    event Action? ContentReady;
    event Action<SaveEvent>? SaveCompleted;
    event Action<SaveEvent>? LoadCompleted;
    event Action<FrameEvent>? FrameUpdate;
}

public sealed record SaveEvent(
    int Slot,
    string SidecarDirectory);

public enum SceneLoadMode
{
    Single = 0,
    Additive = 1,
}

public sealed record SceneEvent(
    int Handle,
    string? Name,
    SceneLoadMode? LoadMode,
    int RawLoadMode);

public readonly record struct FrameEvent(
    int FrameCount,
    float DeltaTime,
    float UnscaledDeltaTime);

/// <summary>
/// Process-local, owner-aware messaging between loaded mods. This transport is
/// independent from multiplayer networking and always dispatches on Unity's main thread.
/// </summary>
public interface IModMessageBus
{
    IReadOnlyList<IModMessageSubscription> Subscriptions { get; }
    IModMessageSubscription Subscribe(
        string topic,
        Action<ModMessage> handler,
        ModMessageSubscriptionOptions? options = null);
    void Publish(
        string topic,
        ReadOnlyMemory<byte> payload,
        ModMessagePublishOptions? options = null);
    bool RemoveRetained(string topic, string? targetModId = null);
}

/// <summary>Hard limits applied before local message payloads enter the runtime bus.</summary>
public static class ModMessageBusLimits
{
    public const int MaximumTopicLength = 128;
    public const int MaximumPayloadBytes = 1024 * 1024;
    public const int MaximumSubscriptionsPerMod = 256;
    public const int MaximumRetainedMessagesPerMod = 128;
    public const int MaximumRetainedBytesPerMod = 4 * 1024 * 1024;
    public const int MaximumDispatchDepth = 32;
}

/// <summary>One immutable message copied into the receiving mod's callback.</summary>
public sealed record ModMessage(
    long Sequence,
    string SenderModId,
    string Topic,
    ReadOnlyMemory<byte> Payload,
    string? TargetModId,
    bool Retained);

/// <summary>Routing and retention controls for one publication.</summary>
public sealed record ModMessagePublishOptions(
    string? TargetModId = null,
    bool Retain = false);

/// <summary>Optional sender filter and retained replay for one subscription.</summary>
public sealed record ModMessageSubscriptionOptions(
    string? SenderModId = null,
    bool ReplayRetained = false);

/// <summary>A live owner-scoped subscription. Dispose and Unsubscribe are idempotent.</summary>
public interface IModMessageSubscription : IDisposable
{
    string OwnerId { get; }
    string Topic { get; }
    string? SenderModId { get; }
    bool IsSubscribed { get; }
    void Unsubscribe();
}

/// <summary>Schedules work for the next Unity EventSystem update.</summary>
public interface IMainThreadScheduler
{
    bool IsMainThread { get; }
    void Post(Action action);
}

/// <summary>Owner-aware native detours for advanced mods.</summary>
public interface IModHooks
{
    INativeDetour Install(NativeDetourDefinition definition);

    /// <summary>
    /// Resolves and detours an IL2CPP method while retaining both managed delegates
    /// for the lifetime of the returned handle.
    /// </summary>
    IIl2CppMethodDetour<TDelegate> InstallIl2Cpp<TDelegate>(
        Il2CppMethodDetourDefinition definition,
        TDelegate replacement)
        where TDelegate : Delegate;
}

/// <summary>A native replacement pointer and its stable owner-local id.</summary>
public sealed record NativeDetourDefinition(
    string Id,
    nint Target,
    nint Replacement);

/// <summary>
/// Stable, name-based target for one non-overloaded IL2CPP method signature.
/// ArgumentCount excludes the generated instance and MethodInfo parameters.
/// </summary>
public sealed record Il2CppMethodDetourDefinition(
    string Id,
    string AssemblyName,
    string Namespace,
    string ClassName,
    string MethodName,
    int ArgumentCount);

/// <summary>A live detour. Dispose removes it and restores the original target.</summary>
public interface INativeDetour : IDisposable
{
    string Id { get; }
    nint Target { get; }
    nint Replacement { get; }
    nint Original { get; }
    bool IsInstalled { get; }
    void Remove();
}

/// <summary>
/// A typed IL2CPP detour. OriginalDelegate points at the native trampoline and
/// is valid only while the detour remains installed.
/// </summary>
public interface IIl2CppMethodDetour<out TDelegate> : INativeDetour
    where TDelegate : Delegate
{
    nint MethodInfo { get; }
    TDelegate OriginalDelegate { get; }
}

/// <summary>JSON persistence confined to this mod's configuration directory.</summary>
public interface IModConfig
{
    bool Exists(string relativePath);
    T Load<T>(string relativePath, Func<T> createDefault);
    void Save<T>(string relativePath, T value);
}

/// <summary>Owner-scoped access to the game's I2 localization tables.</summary>
public interface ILocalizationApi
{
    string CurrentLanguage { get; }
    string CurrentLanguageCode { get; }
    string GetTerm(string key);
    string Translate(string term, string? languageCode = null);
    bool TryTranslate(string term, out string translation, string? languageCode = null);
    ILocalizationRegistration Register(LocalizationTermDefinition definition);
    void Refresh();
}

/// <summary>
/// One mod-local term and its translations keyed by IETF language code
/// (for example en, en-US or es).
/// </summary>
public sealed record LocalizationTermDefinition(
    string Key,
    IReadOnlyDictionary<string, string> Translations);

public interface ILocalizationRegistration : IDisposable
{
    string Key { get; }
    string Term { get; }
    bool IsRegistered { get; }
    void Unregister();
}

/// <summary>Per-save JSON sidecars owned by this mod; vanilla SAVE.GZ is never edited.</summary>
public interface IModSaveData
{
    int? CurrentSlot { get; }
    string? CurrentDirectory { get; }
    int? SchemaVersion { get; }
    SaveMigrationResult LastMigration { get; }
    bool Exists(string relativePath);
    T Load<T>(string relativePath, Func<T> createDefault);
    void Save<T>(string relativePath, T value);
    void RegisterMigrationPlan(SaveMigrationPlanDefinition definition);
}

/// <summary>
/// A complete, linear migration chain for one mod's per-save JSON sidecars.
/// Unversioned sidecars are interpreted as <paramref name="LegacyVersion"/>.
/// </summary>
public sealed record SaveMigrationPlanDefinition(
    int CurrentVersion,
    IReadOnlyList<SaveMigrationStepDefinition> Steps,
    int LegacyVersion = 0);

/// <summary>One transactional transition in a mod save schema.</summary>
public sealed record SaveMigrationStepDefinition(
    int FromVersion,
    int ToVersion,
    Action<IModSaveMigrationContext> Apply);

/// <summary>
/// File access scoped to the staging copy used by one migration step.
/// Writes and deletes become visible only if the complete chain succeeds.
/// </summary>
public interface IModSaveMigrationContext
{
    int FromVersion { get; }
    int ToVersion { get; }
    bool Exists(string relativePath);
    T Load<T>(string relativePath, Func<T> createDefault);
    void Save<T>(string relativePath, T value);
    bool Delete(string relativePath);
}

public enum SaveMigrationStatus
{
    NotConfigured = 0,
    Pending = 1,
    Initialized = 2,
    UpToDate = 3,
    Migrated = 4,
    Failed = 5,
}

/// <summary>The outcome of the latest automatic migration attempt for the active slot.</summary>
public sealed record SaveMigrationResult(
    SaveMigrationStatus Status,
    int? FromVersion = null,
    int? ToVersion = null,
    string? Error = null);

/// <summary>Loads Unity AssetBundles owned by this mod.</summary>
public interface IModAssets
{
    IReadOnlyList<IModAssetBundle> LoadedBundles { get; }
    IReadOnlyList<IModScene> LoadedScenes { get; }
    IReadOnlyList<IModImage> LoadedImages { get; }
    IReadOnlyList<IModAudioClip> LoadedAudioClips { get; }
    IReadOnlyList<IModAudioPlayback> ActiveAudioPlaybacks { get; }
    IReadOnlyList<IModMaterial> LoadedMaterials { get; }
    IReadOnlyList<IModRendererBinding> ActiveRendererBindings { get; }
    IReadOnlyList<IModMesh> LoadedMeshes { get; }
    IReadOnlyList<IModMeshBinding> ActiveMeshBindings { get; }
    IModAssetBundle LoadBundle(string relativePath);
    IModAssetBundleSet LoadBundleSet(string relativeIndexPath);
    IModImage LoadImage(
        string relativePath,
        ModImageLoadOptions? options = null);
    IModImage LoadImageBytes(
        string name,
        ReadOnlyMemory<byte> bytes,
        ModImageLoadOptions? options = null);
    IModAudioClip LoadWav(
        string relativePath,
        ModAudioClipOptions? options = null);
    IModAudioClip LoadWavBytes(
        string name,
        ReadOnlyMemory<byte> bytes,
        ModAudioClipOptions? options = null);
    UnityObject FindShader(string shaderName);
    IReadOnlyList<UnityObject> GetRendererSharedMaterials(UnityObject renderer);
    IModMaterial CreateMaterial(string shaderName, string name);
    IModMaterial CloneMaterial(UnityObject sourceMaterial, string name);
    IModRendererBinding BindRendererMaterial(
        UnityObject renderer,
        int slot,
        IModMaterial material);
    IModMesh CreateMesh(
        string name,
        ModMeshGeometry geometry,
        bool markDynamic = false,
        bool uploadMeshData = false);
    UnityObject GetMeshFilterSharedMesh(UnityObject meshFilter);
    IModMeshBinding BindMeshFilter(UnityObject meshFilter, IModMesh mesh);
}

public enum ModImageFormat
{
    Png = 0,
    Jpeg = 1,
}

/// <summary>Hard limits for loose PNG/JPEG assets decoded at runtime.</summary>
public static class ModImageLimits
{
    public const int MaximumSourceBytes = 16 * 1024 * 1024;
    public const int MaximumDimension = 8192;
    public const long MaximumPixels = 32L * 1024L * 1024L;
}

/// <summary>Sprite creation options for a loose PNG/JPEG image.</summary>
public sealed record ModImageLoadOptions(
    float PivotX = 0.5f,
    float PivotY = 0.5f,
    float PixelsPerUnit = 100f,
    bool MarkNonReadable = true,
    string? Name = null);

/// <summary>An owner-aware Texture2D and Sprite decoded from PNG/JPEG bytes.</summary>
public interface IModImage : IDisposable
{
    string OwnerId { get; }
    string Name { get; }
    string? SourcePath { get; }
    int SourceBytes { get; }
    string Sha256 { get; }
    ModImageFormat Format { get; }
    int Width { get; }
    int Height { get; }
    UnityObject Texture { get; }
    UnityObject Sprite { get; }
    bool IsLoaded { get; }
    void Unload();
}

public enum ModWaveEncoding
{
    PcmInteger = 0,
    IeeeFloat = 1,
}

/// <summary>Hard limits for loose WAV assets decoded at runtime.</summary>
public static class ModAudioLimits
{
    public const int MaximumSourceBytes = 64 * 1024 * 1024;
    public const int MaximumChannels = 8;
    public const int MaximumFrequency = 192_000;
    public const int MaximumSampleValues = 16 * 1024 * 1024;
    public const double MaximumDurationSeconds = 600d;
}

/// <summary>Creation options for a loose PCM/IEEE-float WAV clip.</summary>
public sealed record ModAudioClipOptions(string? Name = null);

/// <summary>Playback controls shared by 2D and 3D mod audio.</summary>
public sealed record ModAudioPlaybackOptions(
    float Volume = 1f,
    float Pitch = 1f,
    bool Loop = false,
    bool PersistAcrossScenes = false,
    bool AutoRelease = true,
    float MinDistance = 1f,
    float MaxDistance = 500f);

/// <summary>An owner-aware Unity AudioClip decoded from a WAV source.</summary>
public interface IModAudioClip : IDisposable
{
    string OwnerId { get; }
    string Name { get; }
    string? SourcePath { get; }
    int SourceBytes { get; }
    string Sha256 { get; }
    ModWaveEncoding Encoding { get; }
    int Channels { get; }
    int Frequency { get; }
    int BitsPerSample { get; }
    int SampleFrames { get; }
    double DurationSeconds { get; }
    UnityObject Clip { get; }
    bool IsLoaded { get; }
    IReadOnlyList<IModAudioPlayback> ActivePlaybacks { get; }
    IModAudioPlayback Play2D(ModAudioPlaybackOptions? options = null);
    IModAudioPlayback Play3D(
        UnityVector3 position,
        ModAudioPlaybackOptions? options = null);
    void Unload();
}

/// <summary>A controlled AudioSource and GameObject owned by one mod clip.</summary>
public interface IModAudioPlayback : IDisposable
{
    string OwnerId { get; }
    IModAudioClip AudioClip { get; }
    UnityObject GameObject { get; }
    UnityObject AudioSource { get; }
    bool IsAlive { get; }
    bool IsPlaying { get; }
    bool Is3D { get; }
    bool Loop { get; }
    float Volume { get; }
    float Pitch { get; }
    void SetVolume(float volume);
    void SetPitch(float pitch);
    void Stop();
}

/// <summary>An owner-aware Unity Material created or cloned by one mod.</summary>
public interface IModMaterial : IDisposable
{
    string OwnerId { get; }
    string Name { get; }
    UnityObject Material { get; }
    UnityObject Shader { get; }
    bool IsLoaded { get; }
    int RenderQueue { get; }
    IReadOnlyList<IModRendererBinding> ActiveBindings { get; }
    bool HasProperty(string propertyName);
    UnityColor GetColor(string propertyName);
    void SetColor(string propertyName, UnityColor value);
    float GetFloat(string propertyName);
    void SetFloat(string propertyName, float value);
    UnityVector4 GetVector(string propertyName);
    void SetVector(string propertyName, UnityVector4 value);
    UnityObject GetTexture(string propertyName);
    void SetTexture(string propertyName, UnityObject texture);
    UnityVector2 GetTextureOffset(string propertyName);
    void SetTextureOffset(string propertyName, UnityVector2 value);
    UnityVector2 GetTextureScale(string propertyName);
    void SetTextureScale(string propertyName, UnityVector2 value);
    bool IsKeywordEnabled(string keyword);
    void EnableKeyword(string keyword);
    void DisableKeyword(string keyword);
    void SetRenderQueue(int renderQueue);
    void Unload();
}

/// <summary>A reversible material assignment to one Renderer shared-material slot.</summary>
public interface IModRendererBinding : IDisposable
{
    string OwnerId { get; }
    UnityObject Renderer { get; }
    int Slot { get; }
    IModMaterial Material { get; }
    UnityObject OriginalMaterial { get; }
    bool IsBound { get; }
    void Unbind();
}

/// <summary>Primitive topology for one runtime mesh submesh.</summary>
public enum ModMeshTopology
{
    Triangles = 0,
    Quads = 2,
    Lines = 3,
    LineStrip = 4,
    Points = 5,
}

/// <summary>Indices and primitive topology for one runtime mesh submesh.</summary>
public sealed record ModSubMeshDefinition(
    IReadOnlyList<int> Indices,
    ModMeshTopology Topology = ModMeshTopology.Triangles);

/// <summary>Complete vertex and index payload used to create or replace a runtime mesh.</summary>
public sealed record ModMeshGeometry(
    IReadOnlyList<UnityVector3> Vertices,
    IReadOnlyList<ModSubMeshDefinition> SubMeshes,
    IReadOnlyList<UnityVector3>? Normals = null,
    IReadOnlyList<UnityVector4>? Tangents = null,
    IReadOnlyList<UnityVector2>? Uv0 = null,
    IReadOnlyList<UnityColor>? Colors = null,
    bool RecalculateNormals = true,
    bool RecalculateTangents = false,
    bool RecalculateBounds = true);

/// <summary>Hard limits for code-generated meshes.</summary>
public static class ModMeshLimits
{
    public const int MaximumVertices = 1_000_000;
    public const int MaximumIndices = 3_000_000;
    public const int MaximumSubMeshes = 64;
}

/// <summary>An owner-aware Unity Mesh created from managed geometry.</summary>
public interface IModMesh : IDisposable
{
    string OwnerId { get; }
    string Name { get; }
    UnityObject Mesh { get; }
    bool IsLoaded { get; }
    bool IsReadable { get; }
    int VertexCount { get; }
    int IndexCount { get; }
    int SubMeshCount { get; }
    IReadOnlyList<IModMeshBinding> ActiveBindings { get; }
    void Update(ModMeshGeometry geometry, bool uploadMeshData = false);
    void Unload();
}

/// <summary>A reversible assignment of one owned Mesh to a Unity MeshFilter.</summary>
public interface IModMeshBinding : IDisposable
{
    string OwnerId { get; }
    UnityObject MeshFilter { get; }
    IModMesh Mesh { get; }
    UnityObject OriginalMesh { get; }
    bool IsBound { get; }
    void Unbind();
}

/// <summary>Owner-scoped creation and control of Unity 3D physics components.</summary>
public interface IModPhysicsApi
{
    IReadOnlyList<IModCollider> Colliders { get; }
    IReadOnlyList<IModRigidbody> Rigidbodies { get; }
    IModBoxCollider AddBoxCollider(
        UnityObject gameObject,
        ModBoxColliderDefinition? definition = null);
    IModSphereCollider AddSphereCollider(
        UnityObject gameObject,
        ModSphereColliderDefinition? definition = null);
    IModCapsuleCollider AddCapsuleCollider(
        UnityObject gameObject,
        ModCapsuleColliderDefinition? definition = null);
    IModMeshCollider AddMeshCollider(
        UnityObject gameObject,
        ModMeshColliderDefinition definition);
    IModRigidbody AddRigidbody(
        UnityObject gameObject,
        ModRigidbodyDefinition? definition = null);
    bool CheckSphere(
        UnityVector3 center,
        float radius,
        int layerMask = ModPhysicsLayers.DefaultRaycast,
        ModQueryTriggerInteraction queryTriggers = ModQueryTriggerInteraction.UseGlobal);
    bool CheckBox(
        UnityVector3 center,
        UnityVector3 halfExtents,
        UnityQuaternion orientation,
        int layerMask = ModPhysicsLayers.DefaultRaycast,
        ModQueryTriggerInteraction queryTriggers = ModQueryTriggerInteraction.UseGlobal);
    bool CheckCapsule(
        UnityVector3 start,
        UnityVector3 end,
        float radius,
        int layerMask = ModPhysicsLayers.DefaultRaycast,
        ModQueryTriggerInteraction queryTriggers = ModQueryTriggerInteraction.UseGlobal);
    bool Raycast(
        UnityVector3 origin,
        UnityVector3 direction,
        out ModRaycastHit hit,
        float maxDistance = float.PositiveInfinity,
        int layerMask = ModPhysicsLayers.DefaultRaycast,
        ModQueryTriggerInteraction queryTriggers = ModQueryTriggerInteraction.UseGlobal);
    void SyncTransforms();
}

public static class ModPhysicsLayers
{
    public const int IgnoreRaycast = 1 << 2;
    public const int DefaultRaycast = ~IgnoreRaycast;
    public const int All = ~0;
}

public enum ModQueryTriggerInteraction
{
    UseGlobal = 0,
    Ignore = 1,
    Collide = 2,
}

public enum ModColliderKind
{
    Box = 0,
    Sphere = 1,
    Capsule = 2,
    Mesh = 3,
}

public enum ModCapsuleDirection
{
    X = 0,
    Y = 1,
    Z = 2,
}

public enum ModForceMode
{
    Force = 0,
    Impulse = 1,
    VelocityChange = 2,
    Acceleration = 5,
}

[Flags]
public enum ModRigidbodyConstraints
{
    None = 0,
    FreezePositionX = 1 << 1,
    FreezePositionY = 1 << 2,
    FreezePositionZ = 1 << 3,
    FreezeRotationX = 1 << 4,
    FreezeRotationY = 1 << 5,
    FreezeRotationZ = 1 << 6,
    FreezePosition = FreezePositionX | FreezePositionY | FreezePositionZ,
    FreezeRotation = FreezeRotationX | FreezeRotationY | FreezeRotationZ,
    FreezeAll = FreezePosition | FreezeRotation,
}

public enum ModCollisionDetectionMode
{
    Discrete = 0,
    Continuous = 1,
    ContinuousDynamic = 2,
    ContinuousSpeculative = 3,
}

public enum ModRigidbodyInterpolation
{
    None = 0,
    Interpolate = 1,
    Extrapolate = 2,
}

public sealed record ModBoxColliderDefinition(
    UnityVector3 Center,
    UnityVector3 Size,
    bool IsTrigger = false,
    bool Enabled = true)
{
    public ModBoxColliderDefinition() : this(UnityVector3.Zero, UnityVector3.One) { }
}

public sealed record ModSphereColliderDefinition(
    UnityVector3 Center,
    float Radius = 0.5f,
    bool IsTrigger = false,
    bool Enabled = true)
{
    public ModSphereColliderDefinition() : this(UnityVector3.Zero) { }
}

public sealed record ModCapsuleColliderDefinition(
    UnityVector3 Center,
    float Radius = 0.5f,
    float Height = 2f,
    ModCapsuleDirection Direction = ModCapsuleDirection.Y,
    bool IsTrigger = false,
    bool Enabled = true)
{
    public ModCapsuleColliderDefinition() : this(UnityVector3.Zero) { }
}

public sealed record ModMeshColliderDefinition(
    UnityObject Mesh,
    bool Convex = false,
    bool IsTrigger = false,
    bool Enabled = true);

public sealed record ModRigidbodyDefinition(
    float Mass = 1f,
    bool UseGravity = true,
    bool IsKinematic = false,
    float LinearDamping = 0f,
    float AngularDamping = 0.05f,
    bool DetectCollisions = true,
    ModRigidbodyConstraints Constraints = ModRigidbodyConstraints.None,
    ModCollisionDetectionMode CollisionDetection = ModCollisionDetectionMode.Discrete,
    ModRigidbodyInterpolation Interpolation = ModRigidbodyInterpolation.None);

public sealed record ModRaycastHit(
    UnityObject Collider,
    UnityObject GameObject,
    UnityVector3 Point,
    UnityVector3 Normal,
    float Distance,
    int TriangleIndex,
    UnityVector3 BarycentricCoordinate);

public interface IModCollider : IDisposable
{
    string OwnerId { get; }
    UnityObject GameObject { get; }
    UnityObject Collider { get; }
    ModColliderKind Kind { get; }
    bool IsAlive { get; }
    bool Enabled { get; set; }
    bool IsTrigger { get; set; }
    void Remove();
}

public interface IModBoxCollider : IModCollider
{
    UnityVector3 Center { get; set; }
    UnityVector3 Size { get; set; }
}

public interface IModSphereCollider : IModCollider
{
    UnityVector3 Center { get; set; }
    float Radius { get; set; }
}

public interface IModCapsuleCollider : IModCollider
{
    UnityVector3 Center { get; set; }
    float Radius { get; set; }
    float Height { get; set; }
    ModCapsuleDirection Direction { get; set; }
}

public interface IModMeshCollider : IModCollider
{
    UnityObject SharedMesh { get; set; }
    bool Convex { get; set; }
}

public interface IModRigidbody : IDisposable
{
    string OwnerId { get; }
    UnityObject GameObject { get; }
    UnityObject Rigidbody { get; }
    bool IsAlive { get; }
    float Mass { get; set; }
    bool UseGravity { get; set; }
    bool IsKinematic { get; set; }
    float LinearDamping { get; set; }
    float AngularDamping { get; set; }
    bool DetectCollisions { get; set; }
    ModRigidbodyConstraints Constraints { get; set; }
    ModCollisionDetectionMode CollisionDetection { get; set; }
    ModRigidbodyInterpolation Interpolation { get; set; }
    UnityVector3 LinearVelocity { get; set; }
    UnityVector3 AngularVelocity { get; set; }
    bool IsSleeping { get; }
    void AddForce(UnityVector3 force, ModForceMode mode = ModForceMode.Force);
    void AddTorque(UnityVector3 torque, ModForceMode mode = ModForceMode.Force);
    void AddForceAtPosition(
        UnityVector3 force,
        UnityVector3 position,
        ModForceMode mode = ModForceMode.Force);
    void MovePosition(UnityVector3 position);
    void MoveRotation(UnityQuaternion rotation);
    void Sleep();
    void WakeUp();
    void Remove();
}

/// <summary>A loaded Unity AssetBundle. Calls must run on Unity's main thread.</summary>
public interface IModAssetBundle : IDisposable
{
    string SourcePath { get; }
    string Name { get; }
    bool IsLoaded { get; }
    IReadOnlyList<string> AssetNames { get; }
    IReadOnlyList<string> ScenePaths { get; }
    IReadOnlyList<IModScene> LoadedScenes { get; }
    bool Contains(string assetName);
    bool ContainsScene(string scenePath);
    UnityObject LoadAsset(string assetName);
    UnityObject LoadPrefab(string assetName);
    IReadOnlyList<UnityObject> LoadAllAssets();
    IModScene LoadSceneAdditive(string scenePath, bool setActive = false);
    IModScene LoadSceneAdditiveAsync(
        string scenePath,
        ModSceneLoadOptions? options = null);
    void Unload(bool unloadLoadedObjects = false);
}

/// <summary>Lifecycle state of an owner-aware scene loaded from a mod AssetBundle.</summary>
public enum ModSceneStatus
{
    Loading = 0,
    Loaded = 1,
    Unloading = 2,
    Unloaded = 3,
    Failed = 4,
}

/// <summary>Controls an asynchronous additive scene load.</summary>
public sealed record ModSceneLoadOptions(
    bool AllowSceneActivation = true,
    bool SetActiveWhenLoaded = false);

/// <summary>
/// An additive Unity scene owned by one mod and one AssetBundle. Scene operations
/// must run on Unity's main thread.
/// </summary>
public interface IModScene : IDisposable
{
    string OwnerId { get; }
    string ScenePath { get; }
    string Name { get; }
    IModAssetBundle Bundle { get; }
    ModSceneStatus Status { get; }
    bool IsLoaded { get; }
    bool IsActive { get; }
    int? Handle { get; }
    float Progress { get; }
    bool AllowSceneActivation { get; set; }
    string? Error { get; }
    void SetActive();
    void Unload();
}

/// <summary>
/// A dependency-ordered group loaded from the deterministic ofs-bundles.json
/// emitted by the OFS authoring project.
/// </summary>
public interface IModAssetBundleSet : IDisposable
{
    string IndexPath { get; }
    string UnityVersion { get; }
    string BuildTarget { get; }
    IReadOnlyList<IModAssetBundle> Bundles { get; }
    bool IsLoaded { get; }
    IModAssetBundle GetBundle(string name);
    void Unload(bool unloadLoadedObjects = false);
}

/// <summary>Owner-aware local world spawning.</summary>
public interface IWorldApi
{
    ISpawnedObject Spawn(PrefabSpawnDefinition definition);
}

public sealed record PrefabSpawnDefinition(
    UnityObject Prefab,
    UnityVector3 Position,
    UnityQuaternion Rotation,
    UnityObject Parent = default,
    string? Name = null,
    bool Persistent = false,
    bool Active = true);

public interface ISpawnedObject : IDisposable
{
    string OwnerId { get; }
    UnityObject GameObject { get; }
    bool IsSpawned { get; }
    void Despawn();
}

/// <summary>Local NPC spawning and control over the game's NPCAnimator component.</summary>
public interface INpcApi
{
    INpc SpawnLocal(NpcSpawnDefinition definition);
    INpcDefinitionRegistration RegisterDefinition(NpcDefinition definition);
    INpc SpawnLocal(NpcDefinitionSpawnDefinition definition);
    INpcBehavior AttachBehavior(INpc npc, NpcBehaviorDefinition definition);
    bool TryGetVanillaEmployee(
        UnityObject gameObject,
        out IVanillaEmployeeController controller)
    {
        controller = default!;
        return false;
    }
    IReadOnlyList<IVanillaEmployeeController> FindVanillaEmployees(
        bool activeOnly = true) => [];
    VanillaHiredEmployeeSnapshot HireVanillaEmployeeServer(
        VanillaEmployeeHireDefinition definition) =>
        throw new NotSupportedException(
            "This OFS runtime cannot register employees with EmployeeManager.");
    bool TryGetHiredVanillaEmployee(
        string id,
        out VanillaHiredEmployeeSnapshot employee)
    {
        employee = default!;
        return false;
    }
    void FireVanillaEmployeeServer(string id) =>
        throw new NotSupportedException(
            "This OFS runtime cannot remove employees from EmployeeManager.");
}

public sealed record NpcSpawnDefinition(
    UnityObject Prefab,
    UnityVector3 Position,
    UnityQuaternion Rotation,
    string? Name = null,
    UnityObject Parent = default,
    bool Persistent = false,
    bool Active = true,
    bool RequireNpcAnimator = false,
    bool RequireNavigation = false);

/// <summary>A reusable owner-local NPC type with optional prefab variants and behaviors.</summary>
public sealed record NpcDefinition(
    string Id,
    UnityObject Prefab,
    string? DisplayName = null,
    bool Persistent = false,
    bool Active = true,
    bool RequireNpcAnimator = false,
    bool RequireNavigation = false,
    int? InitialIdleAnimation = null,
    float? DefaultMoveSpeed = null,
    IReadOnlyList<NpcVisualVariantDefinition>? Variants = null,
    IReadOnlyList<NpcBehaviorDefinition>? Behaviors = null);

public sealed record NpcVisualVariantDefinition(
    string Id,
    UnityObject Prefab,
    string? DisplayName = null);

/// <summary>Spawns one registered NPC definition and optionally selects a visual variant.</summary>
public sealed record NpcDefinitionSpawnDefinition(
    string DefinitionId,
    UnityVector3 Position,
    UnityQuaternion Rotation,
    string? VariantId = null,
    string? Name = null,
    UnityObject Parent = default,
    bool? Persistent = null,
    bool? Active = null);

public interface INpcDefinitionRegistration : IDisposable
{
    string Id { get; }
    string QualifiedId { get; }
    IReadOnlyList<string> VariantIds { get; }
    bool IsRegistered { get; }
    void Unregister();
}

/// <summary>Managed behavior callbacks attached to one owned NPC.</summary>
public sealed record NpcBehaviorDefinition(
    string Id,
    Action<INpc>? Started = null,
    Action<INpc, FrameEvent>? Update = null,
    Action<INpc>? Stopped = null,
    int Order = 0,
    bool DisableOnException = true);

public interface INpcBehavior : IDisposable
{
    string Id { get; }
    INpc Npc { get; }
    int Order { get; }
    bool Enabled { get; set; }
    bool IsAttached { get; }
    void Detach();
}

public interface INpc : IDisposable
{
    string OwnerId { get; }
    UnityObject GameObject { get; }
    UnityObject Animator { get; }
    INpcNavigation? Navigation { get; }
    bool IsSpawned { get; }
    UnityTransform Transform { get; set; }
    void SetIdleAnimation(int index);
    void PlayAction(int index);
    void StopAction();
    void Despawn();
}

/// <summary>The two employee roles implemented by the game's T_Employee FSM.</summary>
public enum VanillaEmployeeType
{
    Miner = 0,
    Sorter = 1,
}

/// <summary>Vanilla employee stat profiles stored in HiredEmployeeData.</summary>
public enum VanillaEmployeeProfile
{
    Average = 0,
    TechSmart = 1,
    StrongTough = 2,
    None = 3,
}

/// <summary>Observed server-authoritative states of T_Employee in game build 1.0.2.</summary>
public enum VanillaEmployeeWorkState
{
    Idle = 0,
    SearchingTruck = 1,
    GoingToTruck = 2,
    PickingUpFromTruck = 3,
    GoingToDropOff = 4,
    DroppingOff = 5,
    ReturningHome = 6,
    Error = 7,
}

/// <summary>An immutable main-thread view of one vanilla employee.</summary>
public sealed record VanillaEmployeeSnapshot(
    string EmployeeId,
    string FirstName,
    string LastName,
    VanillaEmployeeType Type,
    VanillaEmployeeWorkState WorkState,
    bool IsCarrying,
    bool IsWorking)
{
    public bool IsInitialized => !string.IsNullOrWhiteSpace(EmployeeId);
    public string DisplayName => string.Join(
        " ",
        new[] { FirstName, LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

/// <summary>
/// Data passed to T_Employee.ServerInitialize. Home and drop-off may be either
/// GameObjects or Transform components and must remain alive while the employee works.
/// </summary>
public sealed record VanillaEmployeeInitialization(
    string EmployeeId,
    string FirstName,
    string LastName,
    UnityObject HomePoint,
    UnityObject DropOffPoint,
    VanillaEmployeeType Type = VanillaEmployeeType.Sorter,
    VanillaEmployeeProfile Profile = VanillaEmployeeProfile.None,
    int AvatarIndex = 0,
    int Agility = 0,
    int Intelligence = 0,
    int Technique = 0,
    int Stamina = 0,
    int DailyWage = 0,
    int HiredDay = 0);

/// <summary>
/// Owner-scoped data for a persistent EmployeeManager hire. Id is local to the
/// calling mod and is qualified by the runtime before entering vanilla saves.
/// </summary>
public sealed record VanillaEmployeeHireDefinition(
    string Id,
    string FirstName,
    string LastName,
    VanillaEmployeeType Type = VanillaEmployeeType.Sorter,
    VanillaEmployeeProfile Profile = VanillaEmployeeProfile.None,
    int AvatarIndex = 0,
    int Agility = 0,
    int Intelligence = 0,
    int Technique = 0,
    int Stamina = 0,
    int DailyWage = 0,
    int HiredDay = 0,
    bool BypassCapacity = false);

/// <summary>An immutable view of one entry in EmployeeManager._hiredEmployees.</summary>
public sealed record VanillaHiredEmployeeSnapshot(
    string QualifiedId,
    string FirstName,
    string LastName,
    VanillaEmployeeType Type,
    VanillaEmployeeProfile Profile,
    int AvatarIndex,
    int Agility,
    int Intelligence,
    int Technique,
    int Stamina,
    int DailyWage,
    int HiredDay,
    bool IsFired,
    string ActiveOffsiteContractId)
{
    public string DisplayName => string.Join(
        " ",
        new[] { FirstName, LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public bool IsOnOffsiteContract => !string.IsNullOrWhiteSpace(ActiveOffsiteContractId);
}

/// <summary>
/// Non-owning controller over the game's T_Employee component. It never
/// destroys the employee. Direct speed writes require an active Mirror server;
/// request methods retain the vanilla client/server command path.
/// </summary>
public interface IVanillaEmployeeController
{
    UnityObject Component { get; }
    UnityObject GameObject { get; }
    bool IsServerActive { get; }
    VanillaEmployeeSnapshot Snapshot { get; }
    float MoveSpeed { get; set; }
    void InitializeServer(VanillaEmployeeInitialization initialization) =>
        throw new NotSupportedException(
            "This OFS runtime cannot initialize vanilla T_Employee instances.");
    void RequestToggleWork();
    void RequestUnstuck();
}

/// <summary>Owner-aware callbacks over the game's native Interactable component.</summary>
public interface IInteractionApi
{
    bool IsAvailable { get; }
    IInteractionRegistration Register(InteractionDefinition definition);
}

public sealed record InteractionDefinition(
    string Id,
    UnityObject GameObject,
    Action<InteractionEvent>? Primary = null,
    Action<InteractionEvent>? Secondary = null,
    InteractionHandling PrimaryHandling = InteractionHandling.ReplaceOriginal,
    InteractionHandling SecondaryHandling = InteractionHandling.ReplaceOriginal,
    string? DisplayName = null,
    bool DisplayNameIsLocalizationTerm = false,
    ModInteractionMode? PrimaryMode = null,
    PrimaryInteractionPrompt? PrimaryPrompt = null,
    float? PrimaryHoldSeconds = null,
    ModInteractionMode? SecondaryMode = null,
    SecondaryInteractionPrompt? SecondaryPrompt = null,
    float? SecondaryHoldSeconds = null,
    bool Persistent = false);

public readonly record struct InteractionEvent(
    IInteractionRegistration Registration,
    InteractionButton Button,
    UnityObject GameObject,
    UnityObject Interactable);

public interface IInteractionRegistration : IDisposable
{
    string Id { get; }
    string OwnerId { get; }
    UnityObject GameObject { get; }
    UnityObject Interactable { get; }
    bool Enabled { get; set; }
    bool IsRegistered { get; }
    void Unregister();
}

public enum InteractionButton { Primary = 0, Secondary = 1 }
public enum InteractionHandling { ReplaceOriginal = 0, BeforeOriginal = 1, AfterOriginal = 2 }
public enum ModInteractionMode { None = 0, Press = 1, Hold = 2 }
public enum PrimaryInteractionPrompt
{
    None = 0, Interact = 1, Pickup = 2, Drive = 3, Place = 4, OnlyName = 5,
    Resale = 6, Purchase = 7, Climb = 8, On = 9, Off = 10, Unstuck = 11,
}
public enum SecondaryInteractionPrompt
{
    None = 0, Open = 1, Relocate = 2, PickupAround = 3, AddToInventory = 4,
    SelectForRobot = 5, DeselectForRobot = 6, Sell = 7, StartWorking = 8,
    StopWorking = 9,
}

/// <summary>A localized, branching conversation presented with an in-game panel.</summary>
public interface IDialogueApi
{
    bool IsUiAvailable { get; }
    IDialogueSession Open(DialogueDefinition definition);
}

public readonly record struct DialogueText(string Value, bool IsLocalizationTerm = false)
{
    public static DialogueText Plain(string value) => new(value);
    public static DialogueText Term(string term) => new(term, true);
}

public sealed record DialogueDefinition(
    string Id,
    string StartNodeId,
    IReadOnlyList<DialogueNodeDefinition> Nodes,
    Action<DialogueClosedEvent>? Closed = null);

public sealed record DialogueNodeDefinition(
    string Id,
    DialogueText Speaker,
    DialogueText Body,
    IReadOnlyList<DialogueChoiceDefinition> Choices,
    Action<IDialogueSession>? Entered = null);

public sealed record DialogueChoiceDefinition(
    string Id,
    DialogueText Label,
    string? NextNodeId = null,
    Action<IDialogueSession>? Selected = null,
    bool Close = false);

public readonly record struct DialogueClosedEvent(
    IDialogueSession Session,
    DialogueCloseReason Reason);

public interface IDialogueSession : IDisposable
{
    string Id { get; }
    string OwnerId { get; }
    string CurrentNodeId { get; }
    bool IsOpen { get; }
    void ShowNode(string nodeId);
    void Choose(string choiceId);
    void Close(DialogueCloseReason reason = DialogueCloseReason.ModRequested);
}

public enum DialogueCloseReason
{
    Completed = 0,
    Cancelled = 1,
    Replaced = 2,
    ModRequested = 3,
    SceneUnloaded = 4,
    Error = 5,
}

/// <summary>
/// Reusable, owner-local world entities built from AssetBundle or vanilla prefabs.
/// Behaviors remain managed; optional interaction routes through Interactable.
/// </summary>
public interface IEntityApi
{
    IEntityDefinitionRegistration RegisterDefinition(EntityDefinition definition);
    IEntity Spawn(EntitySpawnDefinition definition);
    IEntityBehavior AttachBehavior(IEntity entity, EntityBehaviorDefinition definition);
}

public sealed record EntityDefinition(
    string Id,
    UnityObject Prefab,
    string? DisplayName = null,
    bool Persistent = false,
    bool Active = true,
    IReadOnlyList<EntityVisualVariantDefinition>? Variants = null,
    IReadOnlyList<EntityBehaviorDefinition>? Behaviors = null,
    EntityInteractionDefinition? Interaction = null);

public sealed record EntityVisualVariantDefinition(
    string Id,
    UnityObject Prefab,
    string? DisplayName = null);

public sealed record EntitySpawnDefinition(
    string DefinitionId,
    UnityVector3 Position,
    UnityQuaternion Rotation,
    string? VariantId = null,
    string? Name = null,
    UnityObject Parent = default,
    bool? Persistent = null,
    bool? Active = null);

public sealed record EntityBehaviorDefinition(
    string Id,
    Action<IEntity>? Started = null,
    Action<IEntity, FrameEvent>? Update = null,
    Action<IEntity>? Stopped = null,
    int Order = 0,
    bool DisableOnException = true);

public sealed record EntityInteractionDefinition(
    Action<EntityInteractionEvent>? Primary = null,
    Action<EntityInteractionEvent>? Secondary = null,
    InteractionHandling PrimaryHandling = InteractionHandling.ReplaceOriginal,
    InteractionHandling SecondaryHandling = InteractionHandling.ReplaceOriginal,
    string? DisplayName = null,
    bool DisplayNameIsLocalizationTerm = false,
    ModInteractionMode? PrimaryMode = null,
    PrimaryInteractionPrompt? PrimaryPrompt = null,
    float? PrimaryHoldSeconds = null,
    ModInteractionMode? SecondaryMode = null,
    SecondaryInteractionPrompt? SecondaryPrompt = null,
    float? SecondaryHoldSeconds = null);

public readonly record struct EntityInteractionEvent(
    IEntity Entity,
    InteractionButton Button,
    InteractionEvent NativeEvent);

public interface IEntityDefinitionRegistration : IDisposable
{
    string Id { get; }
    string QualifiedId { get; }
    IReadOnlyList<string> VariantIds { get; }
    bool IsRegistered { get; }
    void Unregister();
}

public interface IEntityBehavior : IDisposable
{
    string Id { get; }
    IEntity Entity { get; }
    int Order { get; }
    bool Enabled { get; set; }
    bool IsAttached { get; }
    void Detach();
}

public interface IEntity : IDisposable
{
    string OwnerId { get; }
    string DefinitionId { get; }
    string? VariantId { get; }
    UnityObject GameObject { get; }
    bool Persistent { get; }
    bool IsSpawned { get; }
    UnityTransform Transform { get; set; }
    IInteractionRegistration? Interaction { get; }
    void Despawn();
}

public sealed record NpcNavigationState(
    bool HasPath,
    bool PathPending,
    bool ReachedDestination,
    bool ReachedEndOfPath,
    bool IsStopped,
    float RemainingDistance);

/// <summary>Navigation over the game's Pathfinding.FollowerEntity component.</summary>
public interface INpcNavigation
{
    UnityObject Component { get; }
    float MaxSpeed { get; set; }
    NpcNavigationState State { get; }
    void MoveTo(UnityVector3 destination, float? maxSpeed = null);
    void Stop();
    void Resume();
    void Teleport(UnityVector3 position, bool clearPath = true);
}

/// <summary>Explicit Mirror prefab registration and server-authoritative spawning.</summary>
public interface INetworkApi
{
    bool IsServerActive { get; }
    bool IsClientActive { get; }
    NetworkCompatibilityProfile CompatibilityProfile { get; }
    NetworkCompatibilityResult LastCompatibilityCheck { get; }
    NetworkRemediationPlan? LastRemediationPlan => null;
    INetworkPrefabRegistration RegisterPrefab(UnityObject prefab);
    INetworkEntityDefinitionRegistration RegisterEntityDefinition(string entityDefinitionId);
    INetworkedObject SpawnServer(PrefabSpawnDefinition definition);
    INetworkedNpc SpawnNpcServer(NetworkNpcSpawnDefinition definition);
    INetworkedEntity SpawnEntityServer(NetworkEntitySpawnDefinition definition);
    bool TryResolveObject(
        NetworkObjectReference reference,
        out NetworkObjectResolution resolution);
    NetworkObjectResolution ResolveObject(NetworkObjectReference reference);
    NetworkObjectResolution ResolveOwnedObject(NetworkObjectReference reference);
    IModNetworkChannel RegisterChannel(ModNetworkChannelDefinition definition);
    IModReplicatedState<T> RegisterState<T>(ModReplicatedStateDefinition<T> definition);
    IModNetworkRpc<TRequest, TResponse> RegisterRpc<TRequest, TResponse>(
        ModNetworkRpcDefinition<TRequest, TResponse> definition);
}

/// <summary>A portable reference to one Mirror-spawned object.</summary>
public readonly record struct NetworkObjectReference(uint NetId)
{
    public bool IsValid => NetId != 0;
    public override string ToString() => IsValid ? NetId.ToString() : "<invalid>";
}

/// <summary>The active Mirror registries in which a network object was found.</summary>
[Flags]
public enum NetworkObjectSide
{
    None = 0,
    Server = 1,
    Client = 2,
}

/// <summary>A main-thread snapshot resolved from Mirror's spawned registries.</summary>
public readonly record struct NetworkObjectResolution(
    NetworkObjectReference Reference,
    UnityObject NetworkIdentity,
    UnityObject GameObject,
    NetworkObjectSide Side,
    bool IsOwnedByMod);

public enum ModNetworkDirection
{
    ClientToServer = 0,
    ServerToClient = 1,
    Bidirectional = 2,
}

/// <summary>Mirror transport channels used by the high-level mod envelope.</summary>
public enum ModNetworkTransport
{
    Reliable = 0,
    Unreliable = 1,
}

public sealed record ModNetworkChannelDefinition(
    string Id,
    Action<ModNetworkMessageEvent> Received,
    ModNetworkDirection Direction = ModNetworkDirection.Bidirectional,
    int MaxPayloadBytes = 32 * 1024,
    bool RequireAuthentication = true,
    bool DisableOnException = true);

public readonly record struct ModNetworkMessageEvent(
    string ChannelId,
    string QualifiedChannelId,
    ReadOnlyMemory<byte> Payload,
    ModNetworkTransport Transport,
    bool ReceivedByServer,
    INetworkPeer? Sender);

/// <summary>An opaque server-side handle for the client that sent a mod message.</summary>
public interface INetworkPeer
{
    int ConnectionId { get; }
    string Address { get; }
    bool IsAuthenticated { get; }
}

/// <summary>Owner-scoped binary protocol channel carried by Mirror.</summary>
public interface IModNetworkChannel : IDisposable
{
    string OwnerId { get; }
    string Id { get; }
    string QualifiedId { get; }
    ModNetworkDirection Direction { get; }
    int MaxPayloadBytes { get; }
    bool IsRegistered { get; }
    bool Enabled { get; set; }
    void SendToServer(
        ReadOnlyMemory<byte> payload,
        ModNetworkTransport transport = ModNetworkTransport.Reliable);
    void SendToClient(
        INetworkPeer peer,
        ReadOnlyMemory<byte> payload,
        ModNetworkTransport transport = ModNetworkTransport.Reliable);
    void SendToAllClients(
        ReadOnlyMemory<byte> payload,
        ModNetworkTransport transport = ModNetworkTransport.Reliable,
        bool authenticatedOnly = true);
    void Unregister();
}

public sealed record NetworkNpcSpawnDefinition(
    UnityObject Prefab,
    UnityVector3 Position,
    UnityQuaternion Rotation,
    UnityObject Parent = default,
    string? Name = null,
    bool Persistent = false,
    bool Active = true,
    bool RequireNpcAnimator = false,
    bool RequireNavigation = false);

public sealed record NetworkEntitySpawnDefinition(
    string DefinitionId,
    UnityVector3 Position,
    UnityQuaternion Rotation,
    string? VariantId = null,
    string? Name = null,
    UnityObject Parent = default,
    bool? Persistent = null,
    bool? Active = null);

public interface INetworkPrefabRegistration : IDisposable
{
    UnityObject Prefab { get; }
    bool IsRegistered { get; }
    void Unregister();
}

public interface INetworkEntityDefinitionRegistration : IDisposable
{
    string DefinitionId { get; }
    string QualifiedId { get; }
    IReadOnlyList<UnityObject> Prefabs { get; }
    bool IsRegistered { get; }
    void Unregister();
}

public interface INetworkedObject : IDisposable
{
    UnityObject GameObject { get; }
    UnityObject NetworkIdentity { get; }
    uint NetId { get; }
    NetworkObjectReference Reference { get; }
    bool IsSpawned { get; }
    void Despawn();
}

/// <summary>Server-authoritative network object with the high-level NPC controller.</summary>
public interface INetworkedNpc : INetworkedObject
{
    UnityObject Animator { get; }
    INpcNavigation? Navigation { get; }
    UnityTransform Transform { get; set; }
    void SetIdleAnimation(int index);
    void PlayAction(int index);
    void StopAction();
}

/// <summary>A declarative entity spawned by Mirror with host authority.</summary>
public interface INetworkedEntity : IEntity, INetworkedObject
{
    new UnityObject GameObject { get; }
    new bool IsSpawned { get; }
    new void Despawn();
}

/// <summary>Managed, owner-aware gameplay systems driven by the Unity frame loop.</summary>
public interface IModMechanics
{
    IMechanic Register(MechanicDefinition definition);
}

/// <summary>Owner-aware keyboard/mouse actions polled from Unity Input System.</summary>
public interface IModInput
{
    bool IsAvailable { get; }
    IModInputAction Register(ModInputActionDefinition definition);
}

public sealed record ModInputActionDefinition(
    string Id,
    InputBinding Binding,
    Action<ModInputEvent> Triggered,
    InputTrigger Trigger = InputTrigger.Pressed,
    InputModifiers Modifiers = InputModifiers.None,
    InputCapturePolicy CapturePolicy = InputCapturePolicy.NoSelectedUi,
    int Order = 0,
    bool DisableOnException = true);

public readonly record struct ModInputEvent(
    FrameEvent Frame,
    InputBinding Binding,
    InputTrigger Trigger,
    InputModifiers Modifiers);

public interface IModInputAction : IDisposable
{
    string Id { get; }
    InputBinding Binding { get; set; }
    InputTrigger Trigger { get; set; }
    InputModifiers Modifiers { get; set; }
    InputCapturePolicy CapturePolicy { get; set; }
    int Order { get; }
    bool Enabled { get; set; }
    bool IsRegistered { get; }
    void Unregister();
}

public enum InputDeviceKind
{
    Keyboard = 0,
    Mouse = 1,
}

public readonly record struct InputBinding(InputDeviceKind Device, int Code)
{
    public static InputBinding ForKey(ModKey key) => new(InputDeviceKind.Keyboard, (int)key);
    public static InputBinding ForMouse(ModMouseButton button) => new(InputDeviceKind.Mouse, (int)button);
}

[Flags]
public enum InputTrigger
{
    None = 0,
    Pressed = 1,
    Held = 2,
    Released = 4,
}

[Flags]
public enum InputModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Meta = 8,
}

public enum InputCapturePolicy
{
    Always = 0,
    NoFrameworkUi = 1,
    NoSelectedUi = 2,
}

public enum ModMouseButton
{
    Left = 0,
    Middle = 1,
    Right = 2,
    Back = 3,
    Forward = 4,
}

/// <summary>Numeric mirror of UnityEngine.InputSystem.Key.</summary>
public enum ModKey
{
    None = 0,
    Space = 1,
    Enter = 2,
    Tab = 3,
    Backquote = 4,
    Quote = 5,
    Semicolon = 6,
    Comma = 7,
    Period = 8,
    Slash = 9,
    Backslash = 10,
    LeftBracket = 11,
    RightBracket = 12,
    Minus = 13,
    Equals = 14,
    A = 15,
    B = 16,
    C = 17,
    D = 18,
    E = 19,
    F = 20,
    G = 21,
    H = 22,
    I = 23,
    J = 24,
    K = 25,
    L = 26,
    M = 27,
    N = 28,
    O = 29,
    P = 30,
    Q = 31,
    R = 32,
    S = 33,
    T = 34,
    U = 35,
    V = 36,
    W = 37,
    X = 38,
    Y = 39,
    Z = 40,
    Digit1 = 41,
    Digit2 = 42,
    Digit3 = 43,
    Digit4 = 44,
    Digit5 = 45,
    Digit6 = 46,
    Digit7 = 47,
    Digit8 = 48,
    Digit9 = 49,
    Digit0 = 50,
    LeftShift = 51,
    RightShift = 52,
    LeftAlt = 53,
    RightAlt = 54,
    LeftCtrl = 55,
    RightCtrl = 56,
    LeftMeta = 57,
    RightMeta = 58,
    ContextMenu = 59,
    Escape = 60,
    LeftArrow = 61,
    RightArrow = 62,
    UpArrow = 63,
    DownArrow = 64,
    Backspace = 65,
    PageDown = 66,
    PageUp = 67,
    Home = 68,
    End = 69,
    Insert = 70,
    Delete = 71,
    CapsLock = 72,
    NumLock = 73,
    PrintScreen = 74,
    ScrollLock = 75,
    Pause = 76,
    NumpadEnter = 77,
    NumpadDivide = 78,
    NumpadMultiply = 79,
    NumpadPlus = 80,
    NumpadMinus = 81,
    NumpadPeriod = 82,
    NumpadEquals = 83,
    Numpad0 = 84,
    Numpad1 = 85,
    Numpad2 = 86,
    Numpad3 = 87,
    Numpad4 = 88,
    Numpad5 = 89,
    Numpad6 = 90,
    Numpad7 = 91,
    Numpad8 = 92,
    Numpad9 = 93,
    F1 = 94,
    F2 = 95,
    F3 = 96,
    F4 = 97,
    F5 = 98,
    F6 = 99,
    F7 = 100,
    F8 = 101,
    F9 = 102,
    F10 = 103,
    F11 = 104,
    F12 = 105,
    Oem1 = 106,
    Oem2 = 107,
    Oem3 = 108,
    Oem4 = 109,
    Oem5 = 110,
    F13 = 112,
    F14 = 113,
    F15 = 114,
    F16 = 115,
    F17 = 116,
    F18 = 117,
    F19 = 118,
    F20 = 119,
    F21 = 120,
    F22 = 121,
    F23 = 122,
    F24 = 123,
    MediaPlayPause = 124,
    MediaRewind = 125,
    MediaForward = 126,
}

public sealed record MechanicDefinition(
    string Id,
    Action<FrameEvent> Update,
    Action<SceneEvent>? SceneLoaded = null,
    Action<SceneEvent>? SceneUnloaded = null,
    int Order = 0,
    bool DisableOnException = true);

public interface IMechanic : IDisposable
{
    string Id { get; }
    int Order { get; }
    bool Enabled { get; set; }
    bool IsRegistered { get; }
    void Unregister();
}

/// <summary>Typed registration surfaces for game content.</summary>
public interface IGameContent
{
    IItemRegistry Items { get; }
    IBuildingRegistry Buildings { get; }
    IRecipeRegistry Recipes { get; }
    IContractRegistry Contracts { get; }
    ICompanyRegistry Companies { get; }
    IFoodRegistry Foods { get; }
    IBuildingCategoryRegistry BuildingCategories { get; }
    IUpgradeRegistry Upgrades { get; }
    IOffsiteContractRegistry OffsiteContracts { get; }
    IPropertyRegistry Properties { get; }
    IItemSpawnProfileRegistry ItemSpawnProfiles { get; }
    IMiningAreaSpawnerRegistry MiningAreaSpawners { get; }
    IMiningNodeRegistry MiningNodes { get; }
}

/// <summary>Lookup and owner-aware registration for T_ItemSO assets.</summary>
public interface IItemRegistry
{
    IReadOnlyList<ItemDefinition> GetAll();
    UnityObject Clone(UnityObject source, string newItemId);
    UnityObject FindById(string itemId);
    ItemDefinition Describe(UnityObject itemScriptableObject);
    void Update(UnityObject itemScriptableObject, ItemPatch patch);
    IItemRegistration Register(UnityObject itemScriptableObject);
}

/// <summary>Typed production-recipe data embedded in T_ItemSO products.</summary>
public interface IRecipeRegistry
{
    RecipeDefinition Describe(UnityObject productItem);
    void Update(UnityObject productItem, RecipePatch patch);
}

public sealed record RecipeIngredientDefinition(
    UnityObject Item,
    string ItemId,
    int Count);

public sealed record RecipeDefinition(
    UnityObject Product,
    UnityObject ProducedBy,
    float ProductionTime,
    UnityObject Ore,
    int OreCount,
    IReadOnlyList<RecipeIngredientDefinition> Ingredients,
    bool UsesOreRecipe,
    bool UsesListRecipe);

public sealed record RecipeIngredientPatch(
    UnityObject Item,
    int Count);

public sealed record RecipePatch(
    UnityObject? ProducedBy = null,
    float? ProductionTime = null,
    UnityObject? Ore = null,
    int? OreCount = null,
    IReadOnlyList<RecipeIngredientPatch>? Ingredients = null);

/// <summary>Lookup, mutation and append-only registration for ContractSO assets.</summary>
public interface IContractRegistry
{
    int Count { get; }
    UnityObject Clone(UnityObject source, string newContractId);
    UnityObject FindById(string contractId);
    ContractDefinition Describe(UnityObject contractScriptableObject);
    IReadOnlyList<ContractDefinition> GetAll();
    void Update(UnityObject contractScriptableObject, ContractPatch patch);
    IContractRegistration Register(UnityObject contractScriptableObject);
}

public sealed record ContractMaterialDefinition(
    UnityObject Item,
    string ItemId,
    int Count);

public sealed record ContractMaterialPatch(
    UnityObject Item,
    int Count);

public sealed record ContractDefinition(
    UnityObject Asset,
    string ContractId,
    UnityObject Company,
    int PriceMin,
    int PriceMax,
    int PriceRoundingStep,
    int DeliveryDayMin,
    int DeliveryDayMax,
    IReadOnlyList<ContractMaterialDefinition> Materials,
    int RequiredLevel,
    ContractTier Tier,
    int TierDecayLevelGap);

public sealed record ContractPatch(
    UnityObject? Company = null,
    int? PriceMin = null,
    int? PriceMax = null,
    int? PriceRoundingStep = null,
    int? DeliveryDayMin = null,
    int? DeliveryDayMax = null,
    IReadOnlyList<ContractMaterialPatch>? Materials = null,
    int? RequiredLevel = null,
    ContractTier? Tier = null,
    int? TierDecayLevelGap = null);

public enum ContractTier
{
    Tier1 = 0,
    Tier2 = 1,
    Tier3 = 2,
    Tier4 = 3,
}

/// <summary>
/// Ownership handle for an append-only contract registration. The index is
/// stable for the current process and the registration cannot be removed after commit.
/// </summary>
public interface IContractRegistration
{
    string ContractId { get; }
    UnityObject Asset { get; }
    int Index { get; }
}

public sealed record ItemDefinition(
    UnityObject Asset,
    string ItemId,
    string Name,
    string Description,
    int Price,
    float Scale,
    ItemKind Kind,
    IReadOnlyList<ItemFilter> Filters,
    UnityObject Icon,
    UnityObject MiningVfx,
    UnityObject PickupVfx,
    UnityObject SpawnPrefab,
    UnityObject VisualPrefab,
    bool IsNode,
    int NodeHealth,
    int CollectAmountMin,
    int CollectAmountMax,
    UnityObject NodeVisualPrefab,
    MiningVfxKind NodeHitVfx,
    MiningSfxKind NodeHitSfx,
    bool IsMysteryItem,
    MysteryItemKind MysteryKind,
    UpgradeKind RequiredUpgrade,
    int RequiredUpgradeLevel,
    bool FullVersionOnly);

public sealed record ItemPatch(
    string? Name = null,
    string? Description = null,
    int? Price = null,
    float? Scale = null,
    ItemKind? Kind = null,
    IReadOnlyList<ItemFilter>? Filters = null,
    UnityObject? Icon = null,
    UnityObject? MiningVfx = null,
    UnityObject? PickupVfx = null,
    UnityObject? SpawnPrefab = null,
    UnityObject? VisualPrefab = null,
    bool? IsNode = null,
    int? NodeHealth = null,
    int? CollectAmountMin = null,
    int? CollectAmountMax = null,
    UnityObject? NodeVisualPrefab = null,
    MiningVfxKind? NodeHitVfx = null,
    MiningSfxKind? NodeHitSfx = null,
    bool? IsMysteryItem = null,
    MysteryItemKind? MysteryKind = null,
    UpgradeKind? RequiredUpgrade = null,
    int? RequiredUpgradeLevel = null,
    bool? FullVersionOnly = null);

/// <summary>Stable values mirrored from the game's PickupType enum.</summary>
public enum ItemKind
{
    Ore = 0,
    Resource = 1,
    Product = 2,
    Scrap = 3,
    Antique = 4,
}

/// <summary>Marketplace and machine filters mirrored from FilterType.</summary>
public enum ItemFilter
{
    Ores = 0,
    Resources = 1,
    Products = 2,
    Rocks = 3,
    BaseMetals = 4,
    Alloys = 5,
    RareMetals = 6,
    Gems = 7,
    Recyclable = 8,
}

public enum MiningVfxKind
{
    None = 0,
    Grass = 1,
    Dirt = 2,
    Stone = 3,
    Rock = 4,
    Bedrock = 5,
    Concrete = 6,
    Explosion = 7,
    Dismantle = 8,
    DynamiteHazard = 9,
}

public enum MiningSfxKind
{
    None = 0,
    Grass = 1,
    Dirt = 2,
    Stone = 3,
    Rock = 4,
    Bedrock = 5,
    Concrete = 6,
}

public enum MysteryItemKind
{
    None = 0,
    Antique = 1,
    Scrap = 2,
}

/// <summary>Upgrade gates shared by items and buildings.</summary>
public enum UpgradeKind
{
    None = 0,
    Shovel = 1,
    Pickaxe = 2,
    Jackhammer = 3,
    Detector = 4,
    Dynamite = 5,
    Backpack = 6,
    FactorySize = 10,
    TradingAbility = 11,
    Automation = 12,
    Fleet = 13,
    Employee = 14,
    MetalProcessingLicense = 20,
    AdvancedRefiningLicense = 21,
}

/// <summary>Ownership handle for one item registered by a mod.</summary>
public interface IItemRegistration : IDisposable
{
    string ItemId { get; }
    UnityObject Asset { get; }
    bool IsRegistered { get; }
    void Unregister();
}

/// <summary>
/// Lookup, mutation and append-only registration for T_BuildingItemSO assets.
/// List positions are network protocol identifiers and must remain stable.
/// </summary>
public interface IBuildingRegistry
{
    int Count { get; }
    UnityObject Clone(UnityObject source, string newBuildingId);
    UnityObject FindById(string buildingId);
    BuildingDefinition Describe(UnityObject buildingScriptableObject);
    IReadOnlyList<BuildingDefinition> GetAll();
    int GetNetworkIndex(UnityObject buildingScriptableObject);
    void Update(UnityObject buildingScriptableObject, BuildingPatch patch);
    IBuildingRegistration Register(UnityObject buildingScriptableObject);
}

public sealed record BuildingDefinition(
    UnityObject Asset,
    string BuildingId,
    string Name,
    string Description,
    int Price,
    BuildingCategory Category,
    int PackageQuantity,
    UnityObject Icon,
    UnityObject Prefab,
    float RotationStep,
    int Level,
    bool SoldInMarket,
    bool SoldBackToMarket,
    bool ResalableWithHammer,
    bool RelocatableWithHammer,
    bool ExcludedFromBoxSpawn,
    bool CheckTerrainSupport,
    bool BlockedDuringTutorial,
    bool TutorialFree,
    int AdditionalPlacementLayers,
    bool PlaceOnlyOnAdditionalLayers,
    bool PlaceOnWall,
    bool IgnoreGridSnap,
    UpgradeKind RequiredUpgrade,
    int RequiredUpgradeLevel,
    bool FullVersionOnly,
    bool UpdateAiPath);

public sealed record BuildingPatch(
    string? Name = null,
    string? Description = null,
    int? Price = null,
    BuildingCategory? Category = null,
    int? PackageQuantity = null,
    UnityObject? Icon = null,
    UnityObject? Prefab = null,
    float? RotationStep = null,
    int? Level = null,
    bool? SoldInMarket = null,
    bool? SoldBackToMarket = null,
    bool? ResalableWithHammer = null,
    bool? RelocatableWithHammer = null,
    bool? ExcludedFromBoxSpawn = null,
    bool? CheckTerrainSupport = null,
    bool? BlockedDuringTutorial = null,
    bool? TutorialFree = null,
    int? AdditionalPlacementLayers = null,
    bool? PlaceOnlyOnAdditionalLayers = null,
    bool? PlaceOnWall = null,
    bool? IgnoreGridSnap = null,
    UpgradeKind? RequiredUpgrade = null,
    int? RequiredUpgradeLevel = null,
    bool? FullVersionOnly = null,
    bool? UpdateAiPath = null);

/// <summary>Marketplace groups mirrored from the game's BuildingCategory enum.</summary>
public enum BuildingCategory
{
    Machines = 0,
    Warehouse = 1,
    Decorations = 2,
}

/// <summary>
/// Process-lifetime ownership of an appended building entry. It deliberately
/// cannot be removed in-session because doing so would shift Mirror indices.
/// </summary>
public interface IBuildingRegistration
{
    string BuildingId { get; }
    UnityObject Asset { get; }
    int NetworkIndex { get; }
    string OwnerId { get; }
}

/// <summary>API scoped to the current main-menu scene.</summary>
public interface IMainMenuApi
{
    IMenuButton AddButton(MainMenuButtonDefinition definition);
    IMenuPanel AddPanel(MainMenuPanelDefinition definition);
}

/// <summary>A live main-menu button registered by a mod.</summary>
public interface IMenuButton : IDisposable
{
    string Id { get; }
    string Label { get; set; }
    bool Visible { get; set; }
    bool IsAlive { get; }
    void Remove();
}

public sealed record MainMenuButtonDefinition(
    string Id,
    string Label,
    Action OnPressed);

/// <summary>A vanilla-styled panel scoped to the current main-menu scene.</summary>
public interface IMenuPanel : IDisposable
{
    string Id { get; }
    string Title { get; set; }
    bool Visible { get; }
    bool IsAlive { get; }
    IMenuButton AddButton(MenuPanelButtonDefinition definition);
    IMenuText AddText(MenuPanelTextDefinition definition);
    IMenuToggle AddToggle(MenuPanelToggleDefinition definition) =>
        throw new NotSupportedException("Interactive menu toggles are unavailable in this runtime.");
    IMenuChoice AddChoice(MenuPanelChoiceDefinition definition) =>
        throw new NotSupportedException("Interactive menu choices are unavailable in this runtime.");
    IMenuInput AddInput(MenuPanelInputDefinition definition) =>
        throw new NotSupportedException("Interactive menu input is unavailable in this runtime.");
    bool RemoveControl(string id) =>
        throw new NotSupportedException("Dynamic menu control removal is unavailable in this runtime.");
    void Clear() =>
        throw new NotSupportedException("Dynamic menu reconstruction is unavailable in this runtime.");
    void Show();
    void Close();
    void Remove();
}

public interface IMenuText : IDisposable
{
    string Id { get; }
    string Text { get; set; }
    bool Visible { get; set; }
    bool IsAlive { get; }
    void Remove();
}

public sealed record MainMenuPanelDefinition(
    string Id,
    string Title);

public sealed record MenuPanelButtonDefinition(
    string Id,
    string Label,
    Action OnPressed);

public sealed record MenuPanelTextDefinition(
    string Id,
    string Text);

/// <summary>A live boolean control rendered with the game's menu styling.</summary>
public interface IMenuToggle : IDisposable
{
    string Id { get; }
    string Label { get; set; }
    bool Value { get; set; }
    bool Visible { get; set; }
    bool IsAlive { get; }
    void Toggle();
    void Remove();
}

public sealed record MenuPanelToggleDefinition(
    string Id,
    string Label,
    bool InitialValue,
    Action<bool>? OnChanged = null,
    string OnText = "ON",
    string OffText = "OFF");

/// <summary>A live finite choice control that cycles through validated options.</summary>
public interface IMenuChoice : IDisposable
{
    string Id { get; }
    string Label { get; set; }
    IReadOnlyList<string> Options { get; }
    int SelectedIndex { get; set; }
    string SelectedValue { get; }
    bool Visible { get; set; }
    bool IsAlive { get; }
    void SelectNext();
    void SelectPrevious();
    void Remove();
}

public sealed record MenuPanelChoiceDefinition(
    string Id,
    string Label,
    IReadOnlyList<string> Options,
    int InitialIndex = 0,
    Action<int, string>? OnChanged = null);

/// <summary>
/// A focused text control. Keyboard capture is active only while its panel and
/// the game window are active; Enter submits the current value.
/// </summary>
public interface IMenuInput : IDisposable
{
    string Id { get; }
    string Label { get; set; }
    string Value { get; set; }
    string Placeholder { get; set; }
    int MaxLength { get; }
    bool IsFocused { get; }
    bool Visible { get; set; }
    bool IsAlive { get; }
    void Focus();
    void Blur();
    void Submit();
    void Remove();
}

public sealed record MenuPanelInputDefinition(
    string Id,
    string Label,
    string InitialValue = "",
    string Placeholder = "",
    int MaxLength = 64,
    Action<string>? OnChanged = null,
    Action<string>? OnSubmitted = null);

public sealed record ModInfo(
    string Id,
    string Name,
    Version Version,
    string Description,
    string Author,
    string Directory);

public interface IModLogger
{
    void Trace(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Error(Exception exception, string message);
}
