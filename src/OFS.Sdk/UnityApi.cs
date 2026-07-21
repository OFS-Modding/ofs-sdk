namespace OFS.Sdk;

/// <summary>Opaque pointer to an IL2CPP UnityEngine.Object.</summary>
public readonly record struct UnityObject(nint Pointer)
{
    public bool IsNull => Pointer == 0;
    public static UnityObject Null => default;
}

public readonly record struct UnityVector3(float X, float Y, float Z)
{
    public static UnityVector3 Zero => default;
    public static UnityVector3 One => new(1f, 1f, 1f);
}

public readonly record struct UnityVector2(float X, float Y)
{
    public static UnityVector2 Zero => default;
    public static UnityVector2 One => new(1f, 1f);
}

public readonly record struct UnityVector4(float X, float Y, float Z, float W)
{
    public static UnityVector4 Zero => default;
    public static UnityVector4 One => new(1f, 1f, 1f, 1f);
}

public readonly record struct UnityQuaternion(float X, float Y, float Z, float W)
{
    public static UnityQuaternion Identity => new(0f, 0f, 0f, 1f);
}

public readonly record struct UnityTransform(
    UnityVector3 Position,
    UnityQuaternion Rotation,
    UnityVector3 Scale)
{
    public static UnityTransform Identity => new(
        UnityVector3.Zero,
        UnityQuaternion.Identity,
        UnityVector3.One);
}

/// <summary>
/// Main-thread Unity operations that do not require mods to marshal IL2CPP calls.
/// </summary>
public interface IUnityApi
{
    UnityObject CreateGameObject(string name, UnityObject parent = default);
    UnityObject FindActiveGameObject(string name);
    UnityObject FindChild(UnityObject parent, string name, bool recursive = true);
    UnityObject CloneGameObject(UnityObject original, UnityObject parent = default);
    UnityObject Instantiate(
        UnityObject prefab,
        UnityVector3 position,
        UnityQuaternion rotation,
        UnityObject parent = default);
    UnityObject GetComponent(
        UnityObject gameObject,
        string assemblyName,
        string namespaze,
        string className);
    UnityObject TryGetComponent(
        UnityObject gameObject,
        string assemblyName,
        string namespaze,
        string className);
    /// <summary>
    /// Enumerates loaded components of an exact IL2CPP type. With
    /// <paramref name="activeOnly"/> disabled this may also include inactive
    /// scene objects and loaded prefab assets.
    /// </summary>
    IReadOnlyList<UnityObject> FindComponents(
        string assemblyName,
        string namespaze,
        string className,
        bool activeOnly = true) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide loaded-component enumeration.");
    UnityObject AddComponent(
        UnityObject gameObject,
        string assemblyName,
        string namespaze,
        string className);
    UnityObject GetGameObject(UnityObject component) =>
        throw new NotSupportedException("Component-to-GameObject resolution is unavailable.");

    string GetName(UnityObject instance);
    void SetName(UnityObject instance, string name);
    void SetActive(UnityObject gameObject, bool active);
    void SetText(UnityObject gameObjectWithTextMeshPro, string text);
    UnityTransform GetTransform(UnityObject gameObject);
    void SetTransform(UnityObject gameObject, UnityTransform transform);
    void SetParent(UnityObject gameObject, UnityObject parent, bool worldPositionStays = true);
    void DontDestroyOnLoad(UnityObject instance);
    void Destroy(UnityObject instance);
}
