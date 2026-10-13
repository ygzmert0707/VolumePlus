using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SesPlus.Services;

public sealed record AudioOutputDevice(string Id, string Name, bool IsDefault)
{
    public override string ToString() => Name;
}

/// <summary>Reads active Windows playback endpoints without changing system audio settings.</summary>
public sealed class AudioDeviceService
{
    private const uint DeviceStateActive = 0x00000001;
    private const int EndpointNotFound = unchecked((int)0x80070490);
    private const int ChangedApartmentMode = unchecked((int)0x80010106);
    private static readonly PropertyKey FriendlyNameKey = new(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    public IReadOnlyList<AudioOutputDevice> GetOutputDevices()
    {
        // WPF already initializes its UI thread as STA. On a worker thread, initialize
        // MTA here and balance only successful calls, including S_FALSE.
        var initializationResult = CoInitializeEx(IntPtr.Zero, 0);
        if (initializationResult < 0 && initializationResult != ChangedApartmentMode)
        {
            Marshal.ThrowExceptionForHR(initializationResult);
        }

        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            var defaultId = GetDefaultOutputId(enumerator);
            ThrowIfFailed(enumerator.EnumAudioEndpoints(DataFlow.Render, DeviceStateActive, out collection));
            ThrowIfFailed(collection.GetCount(out var count));

            var devices = new List<AudioOutputDevice>();
            for (uint index = 0; index < count; index++)
            {
                IMMDevice? endpoint = null;
                try
                {
                    ThrowIfFailed(collection.Item(index, out endpoint));
                    var id = GetEndpointId(endpoint);
                    var name = GetFriendlyName(endpoint);
                    devices.Add(new AudioOutputDevice(
                        id,
                        string.IsNullOrWhiteSpace(name) ? "Audio output" : name,
                        string.Equals(id, defaultId, StringComparison.Ordinal)));
                }
                finally
                {
                    ReleaseComObject(endpoint);
                }
            }

            devices.Sort((left, right) =>
            {
                var defaultOrder = right.IsDefault.CompareTo(left.IsDefault);
                return defaultOrder != 0
                    ? defaultOrder
                    : StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
            });
            return devices.AsReadOnly();
        }
        finally
        {
            ReleaseComObject(collection);
            ReleaseComObject(enumerator);
            if (initializationResult >= 0)
            {
                CoUninitialize();
            }
        }
    }

    private static string? GetDefaultOutputId(IMMDeviceEnumerator enumerator)
    {
        IMMDevice? endpoint = null;
        try
        {
            var result = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out endpoint);
            if (result == EndpointNotFound)
            {
                return null;
            }

            ThrowIfFailed(result);
            return GetEndpointId(endpoint);
        }
        finally
        {
            ReleaseComObject(endpoint);
        }
    }

    private static string GetEndpointId(IMMDevice endpoint)
    {
        var idPointer = IntPtr.Zero;
        try
        {
            ThrowIfFailed(endpoint.GetId(out idPointer));
            return Marshal.PtrToStringUni(idPointer)
                ?? throw new InvalidOperationException("The Windows audio endpoint ID could not be read.");
        }
        finally
        {
            // IMMDevice::GetId allocates this string with CoTaskMemAlloc.
            Marshal.FreeCoTaskMem(idPointer);
        }
    }

    private static string? GetFriendlyName(IMMDevice endpoint)
    {
        IPropertyStore? properties = null;
        PropVariant name = default;
        try
        {
            ThrowIfFailed(endpoint.OpenPropertyStore(0 /* STGM_READ */, out properties));
            var key = FriendlyNameKey;
            ThrowIfFailed(properties.GetValue(ref key, out name));
            return name.ValueType switch
            {
                31 when name.Pointer != IntPtr.Zero => Marshal.PtrToStringUni(name.Pointer), // VT_LPWSTR
                8 when name.Pointer != IntPtr.Zero => Marshal.PtrToStringBSTR(name.Pointer), // VT_BSTR
                _ => null
            };
        }
        finally
        {
            // Also clear on a failed GetValue in case the provider allocated data.
            PropVariantClear(ref name);
            ReleaseComObject(properties);
        }
    }

    private static void ThrowIfFailed(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    private enum DataFlow { Render, Capture, All }
    private enum Role { Console, Multimedia, Communications }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey(Guid formatId, uint propertyId)
    {
        public readonly Guid FormatId = formatId;
        public readonly uint PropertyId = propertyId;
    }

    // The union's counted-pointer member keeps the native size correct on both
    // x86 (16 bytes total) and x64 / ARM64 (24 bytes total).
    [StructLayout(LayoutKind.Sequential)]
    private struct CountedPointer
    {
        public uint Count;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort ValueType;
        [FieldOffset(8)] public IntPtr Pointer;
        [FieldOffset(8)] public CountedPointer CountedValue;
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    // Keep every native method in declaration order, including unused slots.
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(DataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, uint classContext, IntPtr activationParameters, out IntPtr instance);
        [PreserveSig] int OpenPropertyStore(uint accessMode, out IPropertyStore properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint initializationFlags);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);
}
