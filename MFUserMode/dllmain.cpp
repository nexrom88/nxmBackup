// dllmain.cpp : Definiert den Einstiegspunkt für die DLL-Anwendung.
#include "pch.h"
#include "fltuser.h"

LPCWSTR PORT_NAME = L"\\nxmQueryPort";
HANDLE port = INVALID_HANDLE_VALUE;

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}


//connects to the MF driver
void connectToMF() {
    HRESULT hresult;

    hresult = FilterConnectCommunicationPort(PORT_NAME,
        0,
        NULL,
        0,
        NULL,
        &port);
}

