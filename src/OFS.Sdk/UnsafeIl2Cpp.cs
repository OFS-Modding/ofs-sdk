using System.Runtime.InteropServices;

namespace OFS.Sdk;

/// <summary>How one argument is represented for <see cref="IUnsafeIl2CppApi.Invoke"/>.</summary>
public enum Il2CppArgumentKind
{
    /// <summary>The argument slot contains an IL2CPP object/reference pointer directly.</summary>
    Reference = 0,
    /// <summary>The argument slot points at caller value bytes copied to aligned native storage.</summary>
    Value = 1
}

/// <summary>
/// One argument for the managed IL2CPP invocation helper. This is an explicit
/// ABI value, not runtime type checking; the method signature remains the
/// caller's responsibility.
/// </summary>
public readonly struct Il2CppArgument
{
    private Il2CppArgument(
        Il2CppArgumentKind kind,
        nint reference,
        ReadOnlyMemory<byte> valueBytes)
    {
        Kind = kind;
        Reference = reference;
        ValueBytes = valueBytes;
    }

    public Il2CppArgumentKind Kind { get; }
    public nint Reference { get; }
    public ReadOnlyMemory<byte> ValueBytes { get; }

    public static Il2CppArgument FromReference(nint value) =>
        new(Il2CppArgumentKind.Reference, value, ReadOnlyMemory<byte>.Empty);

    public static Il2CppArgument FromValueBytes(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("An IL2CPP value argument cannot be empty.", nameof(value));
        }
        return new(Il2CppArgumentKind.Value, 0, value.ToArray());
    }

    public static Il2CppArgument FromValue<T>(T value) where T : unmanaged
    {
        var values = MemoryMarshal.CreateReadOnlySpan(ref value, 1);
        return FromValueBytes(MemoryMarshal.AsBytes(values));
    }

    public static Il2CppArgument FromBoolean(bool value) =>
        FromValueBytes(value ? [1] : [0]);

    public static Il2CppArgument FromInt32(int value) => FromValue(value);
    public static Il2CppArgument FromUInt32(uint value) => FromValue(value);
    public static Il2CppArgument FromInt64(long value) => FromValue(value);
    public static Il2CppArgument FromUInt64(ulong value) => FromValue(value);
    public static Il2CppArgument FromSingle(float value) => FromValue(value);
    public static Il2CppArgument FromDouble(double value) => FromValue(value);
}

public sealed record Il2CppClassMetadata(
    nint Pointer,
    string Namespace,
    string Name,
    string QualifiedName,
    nint Parent,
    IReadOnlyList<nint> Interfaces);

public sealed record Il2CppImageMetadata(
    nint Pointer,
    string Name,
    nuint ClassCount);

public sealed record Il2CppParameterMetadata(
    int Position,
    string Name,
    string TypeName);

public sealed record Il2CppMethodMetadata(
    nint Pointer,
    string Name,
    string ReturnTypeName,
    uint Flags,
    uint ImplementationFlags,
    IReadOnlyList<Il2CppParameterMetadata> Parameters);

public sealed record Il2CppFieldMetadata(
    nint Pointer,
    string Name,
    string TypeName,
    int Offset,
    uint Flags);

/// <summary>
/// Advanced escape hatch over the IL2CPP embedding API. Pointer validity and
/// native calling conventions are the caller's responsibility.
/// </summary>
public interface IUnsafeIl2CppApi
{
    nint GameAssemblyModule { get; }
    nint Domain { get; }

    nint FindImage(string assemblyName);
    /// <summary>Enumerates every IL2CPP image loaded when the OFS runtime attached.</summary>
    IReadOnlyList<Il2CppImageMetadata> GetImages() =>
        throw new NotSupportedException(
            "This OFS runtime does not provide IL2CPP image enumeration.");
    /// <summary>Enumerates every top-level and nested class defined in one loaded image.</summary>
    IReadOnlyList<Il2CppClassMetadata> GetClasses(string assemblyName) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide IL2CPP class enumeration.");
    nint FindClass(string assemblyName, string namespaze, string name);
    nint FindNestedClass(nint declaringClass, string name);
    /// <summary>Describes one class and its direct parent/interfaces.</summary>
    Il2CppClassMetadata GetClassMetadata(nint klass) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide IL2CPP class metadata.");
    /// <summary>Enumerates methods declared for the class.</summary>
    IReadOnlyList<Il2CppMethodMetadata> GetMethods(nint klass) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide IL2CPP method metadata.");
    /// <summary>Enumerates fields declared for the class.</summary>
    IReadOnlyList<Il2CppFieldMetadata> GetFields(nint klass) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide IL2CPP field metadata.");
    /// <summary>Runs the IL2CPP static constructor for a class if needed.</summary>
    void EnsureClassInitialized(nint klass);
    /// <summary>Allocates an IL2CPP object without invoking its constructor.</summary>
    nint NewObject(nint klass);
    /// <summary>
    /// Performs a raw shallow clone of an IL2CPP object. References are shared;
    /// constructors are not invoked.
    /// </summary>
    nint ShallowCloneObject(nint instance);
    nint GetObjectClass(nint instance);
    bool IsAssignableFrom(nint baseClass, nint candidateClass);
    nint GetTypeObject(nint klass);
    nint FindMethod(nint klass, string name, int argumentCount);
    /// <summary>
    /// Finds an overload by its exact, namespace-qualified parameter type names,
    /// for example <c>UnityEngine.GameObject</c>.
    /// </summary>
    nint FindMethodBySignature(
        nint klass,
        string name,
        IReadOnlyList<string> parameterTypeNames);
    /// <summary>Resolves the concrete override for an object and base/interface method.</summary>
    nint ResolveVirtualMethod(nint instance, nint methodInfo);
    nint GetMethodPointer(nint methodInfo);
    nint FindField(nint klass, string name);
    nint GetFieldTypeClass(nint fieldInfo);
    int GetFieldOffset(nint fieldInfo);
    nint ReadObjectReference(nint instance, nint fieldInfo);
    nint ReadStaticObjectReference(nint fieldInfo);
    void WriteObjectReference(nint instance, nint fieldInfo, nint value);
    /// <summary>
    /// Sets a static field using IL2CPP embedding conventions: pass the value
    /// address for value fields and the IL2CPP object pointer for references.
    /// </summary>
    void SetStaticFieldValue(nint fieldInfo, nint source) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide raw static IL2CPP field writes.");
    /// <summary>Writes an IL2CPP object pointer directly to a static reference field.</summary>
    void WriteStaticObjectReference(nint fieldInfo, nint value) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide static IL2CPP reference writes.");
    /// <summary>
    /// Copies one instance field into caller-owned native storage using the
    /// IL2CPP embedding API. Value-type owners must be boxed objects.
    /// </summary>
    void GetFieldValue(nint instance, nint fieldInfo, nint destination) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide raw IL2CPP field value access.");
    /// <summary>
    /// Copies a value into one instance field using the IL2CPP embedding API.
    /// Pass the value address for value fields and the IL2CPP object pointer
    /// itself for reference fields. Value-type owners must be boxed objects.
    /// </summary>
    void SetFieldValue(nint instance, nint fieldInfo, nint source) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide raw IL2CPP field value access.");
    int ReadInt32(nint instance, nint fieldInfo);
    void WriteInt32(nint instance, nint fieldInfo, int value);
    float ReadSingle(nint instance, nint fieldInfo);
    void WriteSingle(nint instance, nint fieldInfo, float value);
    bool ReadBoolean(nint instance, nint fieldInfo);
    void WriteBoolean(nint instance, nint fieldInfo, bool value);
    nint Unbox(nint boxedValue);
    /// <summary>Boxes a value copied from caller-owned native storage.</summary>
    nint BoxValue(nint valueClass, nint source) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide IL2CPP value boxing.");
    nint NewString(string value);
    string ReadString(nint value);
    /// <summary>Allocates and copies a managed byte array into the IL2CPP heap.</summary>
    nint NewByteArray(byte[] value);
    /// <summary>Copies an IL2CPP byte array into CoreCLR-owned memory.</summary>
    byte[] ReadByteArray(nint array);
    /// <summary>Allocates and copies a managed single-precision array into the IL2CPP heap.</summary>
    nint NewSingleArray(float[] value) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide single-precision IL2CPP arrays.");
    /// <summary>Copies an IL2CPP single-precision array into CoreCLR-owned memory.</summary>
    float[] ReadSingleArray(nint array) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide single-precision IL2CPP arrays.");
    /// <summary>Allocates a one-dimensional IL2CPP array for an element class.</summary>
    nint NewArray(nint elementClass, nuint length) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide generic IL2CPP array allocation.");
    /// <summary>Returns the element count of an IL2CPP one-dimensional array.</summary>
    nuint GetArrayLength(nint array);
    /// <summary>Reads one managed reference from an IL2CPP reference array.</summary>
    nint ReadArrayElementReference(nint array, nuint index);
    /// <summary>Writes one managed reference with the IL2CPP GC write barrier.</summary>
    void WriteArrayElementReference(nint array, nuint index, nint value) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide IL2CPP reference-array writes.");

    /// <summary>
    /// Builds an aligned native argument vector and calls
    /// <c>il2cpp_runtime_invoke</c>. Reference arguments are passed directly;
    /// value arguments are copied to temporary storage for the duration of the call.
    /// </summary>
    nint Invoke(
        nint methodInfo,
        nint instance,
        params Il2CppArgument[] arguments) =>
        throw new NotSupportedException(
            "This OFS runtime does not provide marshalled IL2CPP invocation.");

    /// <summary>
    /// Calls il2cpp_runtime_invoke. Parameters is a native void** array using
    /// normal IL2CPP embedding conventions. Throws when IL2CPP returns an exception.
    /// </summary>
    nint RuntimeInvoke(nint methodInfo, nint instance, nint parameters);
}
