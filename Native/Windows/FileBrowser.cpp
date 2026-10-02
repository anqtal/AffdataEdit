#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shobjidl.h>
#include <wrl/client.h>
#include <cstring>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace {
HRESULT AppendPath(IShellItem* item, std::wstring& paths) {
    PWSTR path = nullptr;
    HRESULT hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
    if (FAILED(hr)) return hr;
    try {
        if (!paths.empty()) paths += L'\n';
        paths += path;
    } catch (...) {
        CoTaskMemFree(path);
        throw;
    }
    CoTaskMemFree(path);
    return S_OK;
}

HRESULT Show(int mode, HWND owner, LPCWSTR title, LPCWSTR directory,
             LPCWSTR name, LPCWSTR extension, LPCWSTR filters, bool multiple,
             std::wstring& paths) {
    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
    if (FAILED(hr)) return hr;
    struct Apartment { ~Apartment() { CoUninitialize(); } } apartment;
    ComPtr<IFileDialog> dialog;
    hr = CoCreateInstance(mode == 2 ? CLSID_FileSaveDialog : CLSID_FileOpenDialog,
                          nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(dialog.GetAddressOf()));
    if (FAILED(hr)) return hr;
    FILEOPENDIALOGOPTIONS options;
    hr = dialog->GetOptions(&options);
    if (FAILED(hr)) return hr;
    options |= FOS_FORCEFILESYSTEM | FOS_NOCHANGEDIR | FOS_PATHMUSTEXIST;
    if (mode == 1) options |= FOS_PICKFOLDERS;
    else if (mode == 0) options |= FOS_FILEMUSTEXIST;
    else options |= FOS_OVERWRITEPROMPT;
    if (multiple && mode != 2) options |= FOS_ALLOWMULTISELECT;
    hr = dialog->SetOptions(options);
    if (FAILED(hr)) return hr;
    if (title && *title) {
        hr = dialog->SetTitle(title);
        if (FAILED(hr)) return hr;
    }
    if (directory && *directory) {
        ComPtr<IShellItem> folder;
        hr = SHCreateItemFromParsingName(directory, nullptr, IID_PPV_ARGS(folder.GetAddressOf()));
        if (FAILED(hr)) return hr;
        hr = dialog->SetFolder(folder.Get());
        if (FAILED(hr)) return hr;
    }
    if (name && *name) {
        hr = dialog->SetFileName(name);
        if (FAILED(hr)) return hr;
    }
    if (extension && *extension) {
        hr = dialog->SetDefaultExtension(extension);
        if (FAILED(hr)) return hr;
    }
    std::vector<std::wstring> labels, patterns;
    std::wistringstream input(filters ? filters : L"");
    std::wstring line;
    while (std::getline(input, line)) {
        const auto tab = line.find(L'\t');
        if (tab == std::wstring::npos) return E_INVALIDARG;
        labels.push_back(line.substr(0, tab));
        patterns.push_back(line.substr(tab + 1));
    }
    std::vector<COMDLG_FILTERSPEC> specs;
    for (size_t i = 0; i < labels.size(); ++i)
        specs.push_back({ labels[i].c_str(), patterns[i].c_str() });
    if (!specs.empty() && mode != 1) {
        hr = dialog->SetFileTypes(static_cast<UINT>(specs.size()), specs.data());
        if (FAILED(hr)) return hr;
    }
    hr = dialog->Show(owner);
    if (FAILED(hr)) return hr;
    if (mode == 2) {
        ComPtr<IShellItem> item;
        hr = dialog->GetResult(item.GetAddressOf());
        return FAILED(hr) ? hr : AppendPath(item.Get(), paths);
    }
    ComPtr<IFileOpenDialog> open;
    hr = dialog.As(&open);
    if (FAILED(hr)) return hr;
    ComPtr<IShellItemArray> items;
    hr = open->GetResults(items.GetAddressOf());
    if (FAILED(hr)) return hr;
    DWORD count = 0;
    hr = items->GetCount(&count);
    if (FAILED(hr)) return hr;
    for (DWORD i = 0; i < count; ++i) {
        ComPtr<IShellItem> item;
        hr = items->GetItemAt(i, item.GetAddressOf());
        if (FAILED(hr)) return hr;
        hr = AppendPath(item.Get(), paths);
        if (FAILED(hr)) return hr;
    }
    return S_OK;
}
}

extern "C" __declspec(dllexport) HRESULT __cdecl AdeShowFileDialog(
    int mode, HWND owner, LPCWSTR title, LPCWSTR directory, LPCWSTR name,
    LPCWSTR extension, LPCWSTR filters, int multiple, PWSTR* result) {
    if (!result) return E_POINTER;
    *result = nullptr;
    if (mode < 0 || mode > 2) return E_INVALIDARG;
    try {
        HRESULT hr = E_FAIL;
        std::wstring paths;
        // Unity may use an MTA thread; the Shell dialogs require their own STA.
        std::thread thread([&] {
            try { hr = Show(mode, owner, title, directory, name, extension, filters, multiple != 0, paths); }
            catch (...) { hr = E_FAIL; }
        });
        // The owner thread must continue dispatching Windows messages while the
        // STA thread shows the modal dialog, otherwise cross-thread calls can hang.
        HANDLE handle = thread.native_handle();
        bool quit = false;
        WPARAM exitCode = 0;
        while (MsgWaitForMultipleObjects(1, &handle, FALSE, INFINITE, QS_ALLINPUT) == WAIT_OBJECT_0 + 1) {
            MSG message;
            while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
                if (message.message == WM_QUIT) { quit = true; exitCode = message.wParam; }
                else { TranslateMessage(&message); DispatchMessageW(&message); }
            }
        }
        thread.join();
        if (quit) PostQuitMessage(static_cast<int>(exitCode));
        if (FAILED(hr)) return hr;
        const size_t bytes = (paths.size() + 1) * sizeof(wchar_t);
        *result = static_cast<PWSTR>(CoTaskMemAlloc(bytes));
        if (!*result) return E_OUTOFMEMORY;
        std::memcpy(*result, paths.c_str(), bytes);
        return S_OK;
    } catch (...) { return E_FAIL; }
}
