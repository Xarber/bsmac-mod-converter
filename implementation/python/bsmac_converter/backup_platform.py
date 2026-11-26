# SPDX-License-Identifier: AGPL-3.0-only
"""Resolve the two supported Beat Saber layouts and safely wait for Windows exit."""
import ctypes
import csv
import os
from pathlib import Path
import subprocess


def windows_install(instance):
    return (instance / "Beat Saber.exe").is_file() and (instance / "Beat Saber_Data" / "Managed").is_dir()


def resources_for(instance):
    return instance if windows_install(instance) else instance / "Beat Saber.app/Contents/Resources"


def maps_for(instance):
    return (instance / "Beat Saber_Data/CustomLevels" if windows_install(instance)
            else instance / "Beat Saber.app/Contents/CustomLevels")


def wait_windows(pid):
    from ctypes import wintypes

    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.WaitForSingleObject.argtypes = (wintypes.HANDLE, wintypes.DWORD)
    kernel.WaitForSingleObject.restype = wintypes.DWORD
    kernel.CloseHandle.argtypes = (wintypes.HANDLE,)
    handle = kernel.OpenProcess(0x00100000, False, pid)
    if not handle:
        error = ctypes.get_last_error()
        if error == 87:
            return
        raise ctypes.WinError(error)
    try:
        if kernel.WaitForSingleObject(handle, 120000) != 0:
            raise RuntimeError("Could not confirm Beat Saber exited")
    finally:
        kernel.CloseHandle(handle)


def ensure_closed(instance, check_mac):
    if not windows_install(instance):
        return check_mac()
    output = subprocess.check_output(
        ["tasklist.exe", "/FI", "IMAGENAME eq Beat Saber.exe", "/FO", "CSV", "/NH"],
        text=True, creationflags=subprocess.CREATE_NO_WINDOW,
    )
    if any(row and row[0].casefold() == "beat saber.exe" for row in csv.reader(output.splitlines())):
        raise RuntimeError("Close Beat Saber before applying restore")


def safe_relative(name):
    if not name or "\\" in name or any(not part or part in (".", "..") for part in name.split("/")):
        return False
    if os.name == "nt":
        from pathlib import PureWindowsPath

        for part in name.split("/"):
            if part.endswith((" ", ".")) or any(ch in '<>:"|?*' or ord(ch) < 32 for ch in part):
                return False
            if PureWindowsPath(part).is_reserved():
                return False
    return True
