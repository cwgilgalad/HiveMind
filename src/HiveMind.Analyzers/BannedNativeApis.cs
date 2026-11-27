namespace HiveMind.Analyzers;

/// <summary>
/// The native entry points HiveMind never imports (spec section 8.2), each with the reason it's
/// banned.
/// </summary>
public static class BannedNativeApis
{
    private const string TokenBorrowing = "borrows or forges a security context (spec §8.2)";
    private const string Injection = "process injection or memory tampering (spec §8.2)";
    private const string HandleDuplication = "duplicates handles across processes (spec §8.2, X-05)";
    private const string DynamicResolution = "hides imports from review; declare the API with CsWin32 or LibraryImport";
    private const string MemoryDumping = "process memory dumping";
    private const string Persistence = "installs a service (spec §8.2)";
    private const string OrdinalImport = "an import by ordinal hides which API it calls; import it by name";

    private static readonly Dictionary<string, string> _reasons = CreateReasons();

    /// <summary>Looks up whether a native entry point is banned, and why.</summary>
    /// <param name="entryPoint">
    /// The entry point a <c>DllImport</c> or <c>LibraryImport</c> declaration binds to.
    /// </param>
    /// <param name="reason">The reason it's banned, or an empty string if it isn't.</param>
    /// <returns>
    /// <see langword="true"/> if the entry point is banned. Matching ignores case and accepts the
    /// name with or without a trailing <c>A</c> or <c>W</c>. Every import by ordinal (<c>#123</c>) is
    /// banned.
    /// </returns>
    public static bool TryGetReason(string entryPoint, out string reason)
    {
        if (entryPoint is null)
        {
            throw new ArgumentNullException(nameof(entryPoint));
        }

        if (entryPoint.Length > 0 && entryPoint[0] == '#')
        {
            reason = OrdinalImport;
            return true;
        }

        if (_reasons.TryGetValue(entryPoint, out reason))
        {
            return true;
        }

        if (entryPoint.Length > 1 && char.ToUpperInvariant(entryPoint[entryPoint.Length - 1]) is 'A' or 'W'
            && _reasons.TryGetValue(entryPoint.Substring(0, entryPoint.Length - 1), out reason))
        {
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static Dictionary<string, string> CreateReasons()
    {
        var reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Add(TokenBorrowing,
            "DuplicateToken", "DuplicateTokenEx", "ImpersonateLoggedOnUser", "ImpersonateNamedPipeClient",
            "ImpersonateAnonymousToken", "ImpersonateDdeClientWindow", "SetThreadToken",
            "CreateProcessWithTokenW", "CreateProcessAsUser", "CreateProcessWithLogonW", "LogonUser",
            "LogonUserEx", "NtDuplicateToken", "NtImpersonateThread", "NtSetInformationThread",
            "NtCreateToken", "NtCreateTokenEx", "NtOpenProcessToken", "CoImpersonateClient",
            "RpcImpersonateClient");
        Add(Injection,
            "WriteProcessMemory", "ReadProcessMemory", "VirtualAllocEx", "VirtualProtect",
            "VirtualProtectEx", "CreateRemoteThread", "CreateRemoteThreadEx", "NtCreateThreadEx",
            "RtlCreateUserThread", "QueueUserAPC", "NtQueueApcThread", "NtQueueApcThreadEx",
            "SetWindowsHookEx", "NtWriteVirtualMemory", "NtReadVirtualMemory", "NtMapViewOfSection");
        Add(HandleDuplication, "DuplicateHandle", "NtDuplicateObject");
        Add(DynamicResolution, "GetProcAddress", "LoadLibrary", "LoadLibraryEx", "LdrLoadDll", "LdrGetProcedureAddress");
        Add(MemoryDumping, "MiniDumpWriteDump");
        Add(Persistence, "CreateService");

        return reasons;

        void Add(string reason, params string[] entryPoints)
        {
            foreach (var entryPoint in entryPoints)
            {
                reasons.Add(entryPoint, reason);
            }
        }
    }
}
