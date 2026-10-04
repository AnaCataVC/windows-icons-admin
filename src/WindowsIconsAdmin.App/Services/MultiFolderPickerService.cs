using System.Runtime.InteropServices;

namespace WindowsIconsAdmin_App.Services;

public static class MultiFolderPickerService
{
    private const uint FosPickFolders = 0x00000020;
    private const uint FosForceFileSystem = 0x00000040;
    private const uint FosAllowMultiSelect = 0x00000200;
    private const uint FosPathMustExist = 0x00000800;
    private const uint SigdnFileSysPath = 0x80058000;
    private const int ErrorCancelled = unchecked((int)0x800704C7);

    public static async Task<IReadOnlyList<string>> PickMultipleFoldersAsync(IntPtr ownerHwnd, string? title = null)
    {
        try
        {
            var selected = PickWithNativeDialog(ownerHwnd, allowMultiple: true, title ?? "Selecciona una o varias carpetas (Ctrl / Shift + clic)");
            if (selected != null)
            {
                return selected;
            }
        }
        catch
        {
            // Fallback to WinRT single folder picker if COM interop is unavailable
        }

        var fallbackPicker = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(fallbackPicker, ownerHwnd);
        fallbackPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        fallbackPicker.FileTypeFilter.Add("*");

        var single = await fallbackPicker.PickSingleFolderAsync();
        return single != null ? [single.Path] : [];
    }

    public static async Task<string?> PickSingleParentFolderAsync(IntPtr ownerHwnd, string? title = null)
    {
        try
        {
            var selected = PickWithNativeDialog(ownerHwnd, allowMultiple: false, title ?? "Selecciona la carpeta raíz para agregar sus subcarpetas");
            if (selected != null)
            {
                return selected.FirstOrDefault();
            }
        }
        catch
        {
            // Fallback to WinRT FolderPicker
        }

        var fallbackPicker = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(fallbackPicker, ownerHwnd);
        fallbackPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        fallbackPicker.FileTypeFilter.Add("*");

        var single = await fallbackPicker.PickSingleFolderAsync();
        return single?.Path;
    }

    private static IReadOnlyList<string>? PickWithNativeDialog(IntPtr ownerHwnd, bool allowMultiple, string title)
    {
        var dialogType = Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7"));
        if (dialogType == null) return null;

        var instance = Activator.CreateInstance(dialogType);
        if (instance is not INativeFileOpenDialog dialog)
        {
            if (instance != null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
            return null;
        }

        try
        {
            dialog.GetOptions(out var options);
            options |= FosPickFolders | FosForceFileSystem | FosPathMustExist;
            if (allowMultiple)
            {
                options |= FosAllowMultiSelect;
            }
            dialog.SetOptions(options);
            dialog.SetTitle(title);

            var hr = dialog.Show(ownerHwnd);
            if (hr == ErrorCancelled)
            {
                return [];
            }
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            dialog.GetResults(out var itemsArray);
            if (itemsArray == null) return [];

            try
            {
                itemsArray.GetCount(out var count);
                var result = new List<string>((int)count);
                for (uint i = 0; i < count; i++)
                {
                    itemsArray.GetItemAt(i, out var shellItem);
                    if (shellItem == null) continue;
                    try
                    {
                        shellItem.GetDisplayName(SigdnFileSysPath, out var pathPtr);
                        if (pathPtr != IntPtr.Zero)
                        {
                            var path = Marshal.PtrToStringUni(pathPtr);
                            Marshal.FreeCoTaskMem(pathPtr);
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                result.Add(path);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(shellItem);
                    }
                }
                return result;
            }
            finally
            {
                Marshal.ReleaseComObject(itemsArray);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INativeFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parentWindow);
        void Slot_SetFileTypes(uint count, IntPtr filterSpec);
        void Slot_SetFileTypeIndex(uint index);
        void Slot_GetFileTypeIndex(out uint index);
        void Slot_Advise(IntPtr events, out uint cookie);
        void Slot_Unadvise(uint cookie);
        void SetOptions(uint flags);
        void GetOptions(out uint flags);
        void Slot_SetDefaultFolder([MarshalAs(UnmanagedType.Interface)] object folder);
        void Slot_SetFolder([MarshalAs(UnmanagedType.Interface)] object folder);
        void Slot_GetFolder([MarshalAs(UnmanagedType.Interface)] out object folder);
        void Slot_GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out object item);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string titleText);
        void Slot_SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string labelText);
        void Slot_SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string labelText);
        void Slot_GetResult([MarshalAs(UnmanagedType.Interface)] out INativeShellItem item);
        void Slot_AddPlace([MarshalAs(UnmanagedType.Interface)] object item, int alignment);
        void Slot_SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Slot_Close(int hr);
        void Slot_SetClientGuid(in Guid guid);
        void Slot_ClearClientData();
        void Slot_SetFilter([MarshalAs(UnmanagedType.Interface)] object filter);
        void GetResults([MarshalAs(UnmanagedType.Interface)] out INativeShellItemArray items);
        void Slot_GetSelectedItems([MarshalAs(UnmanagedType.Interface)] out INativeShellItemArray items);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INativeShellItem
    {
        void Slot_BindToHandler(IntPtr ctx, in Guid bhid, in Guid riid, out IntPtr ppv);
        void Slot_GetParent([MarshalAs(UnmanagedType.Interface)] out INativeShellItem parent);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void Slot_GetAttributes(uint mask, out uint attributes);
        void Slot_Compare([MarshalAs(UnmanagedType.Interface)] INativeShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INativeShellItemArray
    {
        void Slot_BindToHandler(IntPtr ctx, in Guid bhid, in Guid riid, out IntPtr ppv);
        void Slot_GetPropertyStore(int flags, in Guid riid, out IntPtr ppv);
        void Slot_GetPropertyDescriptionList(IntPtr keyType, in Guid riid, out IntPtr ppv);
        void Slot_GetAttributes(int attribFlags, uint mask, out uint attributes);
        void GetCount(out uint numItems);
        void GetItemAt(uint index, [MarshalAs(UnmanagedType.Interface)] out INativeShellItem item);
        void Slot_EnumItems(out IntPtr enumShellItems);
    }
}
